using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DShNative;

/** Self-update from GitHub releases. Fails soft, never blocks the app. */
public static class AppUpdate
{
    public static string? PendingApply { get; set; }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string ApiUrl =
        "https://api.github.com/repos/ZeroHackz/deepseek-harness-windows-native/releases/latest";

    public static string StagingDir => Path.Combine(AppPaths.Root, "updates");

    /// Tag of the running build, zero-padded to the vYYYY.MM.DD scheme.
    public static string CurrentVersionTag()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "v0.0.0" : $"v{v.Major:D4}.{v.Minor:D2}.{v.Build:D2}";
    }

    /// Checks GitHub for a newer release and stages the win-x64 exe asset.
    /// Returns the staged path, or null when up to date / nothing usable.
    public static async Task<string?> DownloadLatestAsync()
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "DeepSeekHarness");
            req.Headers.Add("Accept", "application/vnd.github+json");
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Warn($"update check failed: http {(int)resp.StatusCode}");
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            if (string.IsNullOrEmpty(tag) || string.CompareOrdinal(tag, CurrentVersionTag()) <= 0)
                return null; // release scheme is zero-padded, so ordinal compare is a date compare

            string? assetUrl = null;
            long? assetSize = null;
            foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                if (name != null
                    && name.StartsWith("DeepSeekHarness-win-x64-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    assetUrl = a.GetProperty("browser_download_url").GetString();
                    assetSize = a.TryGetProperty("size", out var s) ? s.GetInt64() : null;
                    break;
                }
            }
            if (assetUrl == null)
            {
                Log.Warn($"release {tag} has no win-x64 exe asset; skipping update");
                return null;
            }

            Directory.CreateDirectory(StagingDir);
            var staged = Path.Combine(StagingDir, "DeepSeekHarness-" + tag + ".exe");
            var part = staged + ".part";
            Log.Info($"downloading {tag} ...");
            using (var stream = await Http.GetStreamAsync(assetUrl))
            using (var f = File.Create(part))
                await stream.CopyToAsync(f);

            if (assetSize is long want && new FileInfo(part).Length != want)
            {
                Log.Warn("downloaded update size mismatch; discarding");
                File.Delete(part);
                return null;
            }
            File.Move(part, staged, overwrite: true);
            Log.Info($"update staged: {staged}");
            return staged;
        }
        catch (Exception ex)
        {
            Log.Warn("update check failed: " + ex.Message);
            return null;
        }
    }

    public static void DiscardStaged(string staged)
    {
        try { if (staged != null && File.Exists(staged)) File.Delete(staged); } catch { }
    }

    /** Spawn a self-copy that swaps the exe once this process exits, then relaunches. */
    public static void ApplyAndExit(string staged)
    {
        try
        {
            var dest = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(dest))
            {
                Log.Warn("cannot resolve the current exe path; update not applied");
                return;
            }
            Directory.CreateDirectory(StagingDir);
            var marker = Path.Combine(StagingDir, "pending.json");
            var payload = new { staged, dest, relaunch = true };
            File.WriteAllText(marker, JsonSerializer.Serialize(payload));
            var helper = Path.Combine(StagingDir, "updater-helper.exe");
            File.Copy(dest, helper, overwrite: true);
            Process.Start(new ProcessStartInfo
            {
                FileName = helper,
                ArgumentList = { "--apply-update", marker },
                UseShellExecute = true,
            });
            Log.Info("updater helper spawned; exiting so the swap can run");
        }
        catch (Exception ex)
        {
            Log.Warn("apply failed: " + ex.Message);
        }
    }

    /** Helper mode: wait for the main app to exit, swap the exe, relaunch it. */
    public static int ApplyNow(string markerPath)
    {
        try
        {
            if (!File.Exists(markerPath)) return 2;
            using var doc = JsonDocument.Parse(File.ReadAllText(markerPath));
            var staged = doc.RootElement.GetProperty("staged").GetString() ?? "";
            var dest = doc.RootElement.GetProperty("dest").GetString() ?? "";
            var relaunch = !doc.RootElement.TryGetProperty("relaunch", out var r) || r.GetBoolean();
            if (!File.Exists(staged) || !File.Exists(dest)) return 3;

            var self = Environment.ProcessId;
            for (var i = 0; i < 300; i++)
            {
                var busy = Process.GetProcessesByName("DeepSeekHarness").Any(p => p.Id != self);
                if (!busy) break;
                System.Threading.Thread.Sleep(200);
            }

            File.Move(staged, dest, overwrite: true);
            try { File.Delete(markerPath); } catch { }
            Log.Info("exe swapped; relaunching the new version");
            if (relaunch)
                Process.Start(new ProcessStartInfo { FileName = dest, UseShellExecute = true });
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("apply-update failed: " + ex);
            return 1;
        }
    }
}
