# deepseek-harness-windows-native

A **browser-independent Windows desktop app** for the [DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness) web UI. Double-click one executable and your harness opens in its own native window, showing your sessions — no terminal, no browser tab, no browser involved at all.

The window is rendered by the **WebView2 Runtime** — Microsoft's embeddable browser engine that is already present on Windows 10/11 and is *separate from the Edge browser, your browser profiles, accounts, and sync*. There is no sign-in, no sync, nothing to do with Firefox, Chrome, or Edge setups.

Like its script-based sibling [`deepseek-harness-window-wrapper`](https://github.com/ZeroHackz/deepseek-harness-window-wrapper), this app is a **thin wrapper around the globally installed npm package `@deepseek-ai/dsh`** (the `dsh web` server):

1. If nothing is running on the port it runs `npm install -g @deepseek-ai/dsh@latest` (non-fatal, skippable with `--no-update`).
2. It starts `dsh web --no-open --host 127.0.0.1 --port 3080` as a hidden child and waits until the server answers.
3. It opens the native window and shows the UI.
4. **Close the window → the managed server stops** and the app exits.

If something already serves the port (a `dsh web` you started by hand, or another instance), the app switches to **attach mode**: it opens the window, does not run the npm update, and leaves that server running when the window closes.

All harness state lives in files under `DSH_HOME` (default `C:\Users\<you>\.dsh`) — sessions, settings, storages. The app itself only keeps logs and a private WebView2 data folder under `%LOCALAPPDATA%\DeepSeekHarness\`, which you can delete anytime without losing anything.

## Requirements

- **Windows 10/11** (64-bit)
- **WebView2 Runtime** — preinstalled on Windows 11 and most Windows 10 machines; verify with `--self-test`, install from https://go.microsoft.com/fwlink/p/?LinkId=2124703 if missing
- **Node.js 18+ / npm** with the harness installed globally:
  ```powershell
  npm install -g @deepseek-ai/dsh
  ```
- To *build* the exe: the **.NET 8 SDK** (not needed just to run the prebuilt exe)

No PowerShell and no browser are needed at runtime.

## Get the exe

- **Build it yourself** (you have the .NET SDK):
  ```powershell
  .\build.ps1              # framework-dependent single file (needs .NET 8 runtime)
  .\build.ps1 -SelfContained   # standalone, runs on any Win10/11 x64
  # or directly:
  # dotnet publish src/DeepSeekHarness -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
  ```
  Output: `dist\DeepSeekHarness.exe`
- **GitHub Actions**: every push to `main` (and every `v*` tag) builds a self-contained `DeepSeekHarness-win-x64` artifact — grab it from the Actions page of your fork/copy of this repo.

Double-click `dist\DeepSeekHarness.exe` — done.

## Usage

```
DeepSeekHarness.exe                    normal launch (auto-update, own server)
DeepSeekHarness.exe --no-update        launch with the currently installed dsh
DeepSeekHarness.exe --port 8080        serve on another port
DeepSeekHarness.exe --self-test        environment report and exit
DeepSeekHarness.exe --stop             stop the server this app started
DeepSeekHarness.exe --no-window        headless boot test: start, verify, stop
```

| Flag | Default | Meaning |
| --- | --- | --- |
| `--port <n>` | `3080` | Port of the web UI |
| `--address <host>` | `127.0.0.1` | Bind address for the managed server |
| `--ready-timeout <sec>` | `120` | How long to wait for the server to come up |
| `--no-update` | off | Skip `npm install -g @deepseek-ai/dsh@latest` |
| `--no-window` | off | Owned-mode boot test (used by CI/verification) |
| `--self-test` | off | Print an environment report and exit |
| `--stop` | off | Stop the managed server and exit |

## Layout

```
deepseek-harness-windows-native/
├─ build.ps1                    # publish helper (dist\DeepSeekHarness.exe)
├─ src\DeepSeekHarness\         # C# .NET 8 WinForms + WebView2 app
│  ├─ DeepSeekHarness.csproj
│  ├─ Program.cs / Options.cs   # entry point and CLI parsing
│  ├─ Orchestrator.cs           # update -> boot -> ready -> window flow
│  ├─ ServerManager.cs          # spawn/kill of `dsh web`
│  ├─ MainForm.cs               # the WebView2 window
│  └─ ...                       # tools discovery, logging, probes, updater
├─ .github\workflows\build.yml  # Windows build of the self-contained exe
├─ README.md
└─ LICENSE
```

## Logs & troubleshooting

Everything is logged to `%LOCALAPPDATA%\DeepSeekHarness\logs\`:

- `desktop.log` — app steps and decisions
- `server-*.out.log` / `server-*.err.log` — the `dsh web` server output
- `npm-update.out.log` / `npm-update.err.log` — auto-update output

Common issues:

- **The window shows "Starting DeepSeek Harness ..." forever / a connection error** — the server did not come up in time; run `DeepSeekHarness.exe --self-test`, check `--ready-timeout`, and inspect the server logs.
- **`WebView2 failed to initialize`** — the WebView2 Runtime is missing; install the Evergreen runtime (link shown in the window).
- **Port already in use by something else** — the app attaches to whatever answers; use `--stop` only for servers this app started.
- **A server was left running** — run `DeepSeekHarness.exe --stop`.

## Notes

- **No accounts, no sync**: the app never talks to browser sync, Microsoft/Google accounts, or anything beyond your local `dsh web` server on the loopback address.
- **Always latest via npm**: `latest` dist-tag of `@deepseek-ai/dsh` is booted; use `--no-update` to pin the installed version for a while.
- The WebView2 data folder (`%LOCALAPPDATA%\DeepSeekHarness\webview2`) holds only local web state such as the loopback trust cookie; delete the whole `DeepSeekHarness` folder to reset logs and web state (harness data in `DSH_HOME` is untouched).
- Companion project [`deepseek-harness-window-wrapper`](https://github.com/ZeroHackz/deepseek-harness-window-wrapper) is the script-only variant that opens Edge/Chrome `--app` windows instead of a native one.

## License

MIT — see [LICENSE](LICENSE).
