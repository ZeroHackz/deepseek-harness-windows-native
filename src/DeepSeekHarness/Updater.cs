using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DShNative;

/**
 * Keeps the globally installed dsh current.
 *
 * A launch used to run `npm install -g @deepseek-ai/dsh@latest` before every
 * boot, which takes ~30-70s even when nothing changed. Instead we ask the npm
 * registry for the version (a fraction of a second) and only install when it
 * is actually newer, on a background thread after the app is up. The update
 * then applies to the next launch and never blocks startup. Fails soft.
 */
public static class Updater
{
    private const string RegistryLatest = "https://registry.npmjs.org/@deepseek-ai/dsh/latest";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    /** Version string from the registry, or null when the check fails. */
    public static async Task<string?> LatestVersionAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync(RegistryLatest, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Warn($"dsh version check failed: http {(int)resp.StatusCode}");
                return null;
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch (Exception ex)
        {
            Log.Warn("dsh version check failed: " + ex.Message);
            return null;
        }
    }

    /** Version currently installed next to the dsh CLI entry point. */
    public static string? InstalledVersion(Tools t)
    {
        try
        {
            if (!string.IsNullOrEmpty(t.DshCli))
            {
                var pkg = Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(t.DshCli)!, "..", "package.json"));
                if (File.Exists(pkg))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(pkg));
                    if (doc.RootElement.TryGetProperty("version", out var v))
                        return v.GetString();
                }
            }
        }
        catch
        {
            // fall back to the version Tools cached
        }
        return t.DshVersion;
    }

    /** True when the registry has a different (presumably newer) version. */
    public static bool IsNewer(string latest, string? installed) =>
        string.IsNullOrEmpty(installed) ||
        !string.Equals(latest, installed, StringComparison.OrdinalIgnoreCase);

    /**
     * Background check + optional install. Never blocks the boot: the app is
     * already running on the installed version while this runs.
     */
    public static void ScheduleBackground(Tools t, bool install)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var installed = InstalledVersion(t);
                var latest = await LatestVersionAsync();
                if (latest == null)
                {
                    Log.Warn($"latest dsh version unknown (installed: {installed ?? "unknown"})");
                    return;
                }
                if (!IsNewer(latest, installed))
                {
                    Log.Info($"dsh is up to date ({installed})");
                    return;
                }
                if (!install)
                {
                    Log.Info($"dsh {latest} is available (installed {installed}); the attached server keeps the current version");
                    return;
                }

                Log.Info($"dsh {latest} is available (installed {installed}); installing in the background, applies on the next launch");
                var outFile = Path.Combine(AppPaths.LogsDir, "npm-update.out.log");
                var errFile = Path.Combine(AppPaths.LogsDir, "npm-update.err.log");
                var code = Proc.Run(t.Node!, new[]
                {
                    t.NpmCli!, "install", "-g", "@deepseek-ai/dsh@latest",
                    "--no-audit", "--no-fund", "--loglevel=error"
                }, outFile, errFile, 600_000);

                if (code == 0)
                    Log.Info($"dsh {latest} installed; the next launch uses it");
                else
                    Log.Warn($"background dsh update failed (exit {code}); keeping {installed}");
            }
            catch (Exception ex)
            {
                Log.Warn("background dsh update failed: " + ex.Message);
            }
        });
    }

    /** Synchronous install, only used when dsh is missing entirely. */
    public static bool InstallNow(Tools t, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(t.Node) || string.IsNullOrEmpty(t.NpmCli))
        {
            Log.Warn("node or npm not found; cannot install @deepseek-ai/dsh");
            return false;
        }
        var outFile = Path.Combine(AppPaths.LogsDir, "npm-install.out.log");
        var errFile = Path.Combine(AppPaths.LogsDir, "npm-install.err.log");
        var code = Proc.Run(t.Node, new[]
        {
            t.NpmCli, "install", "-g", "@deepseek-ai/dsh",
            "--no-audit", "--no-fund", "--loglevel=error"
        }, outFile, errFile, 300_000, ct);
        return code == 0;
    }
}
