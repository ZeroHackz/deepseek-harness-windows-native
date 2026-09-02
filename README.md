# DeepSeek Harness Desktop (Windows Native)

A **browser-independent Windows desktop app** for the official [DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness) by DeepSeek. Instead of opening `dsh web` in a browser tab, double-click one portable executable and your harness opens in its own **native window** - sessions, settings, and all. No terminal, no browser tab, no browser profiles, no accounts, no sync.

The window is rendered by the **WebView2 Runtime** - Microsoft's embeddable engine that ships with Windows 11 (and most Windows 10 installs) and is completely separate from the Edge browser, Firefox, Chrome, and any user accounts.

### Screenshots
*The DeepSeek Harness UI inside the native window, with the window icon and title bar themed to match the app.*

![DeepSeek Harness native window](screenshots/Screenshot%202026-09-02%20134408.png)

*Window chrome follows the rendered page - dark page, dark title bar.*

![DeepSeek Harness native window, dark chrome](screenshots/Screenshot%202026-09-02%20134449.png)

## ✨ Features

*   **Native window, no browser involved:** The UI runs in an embedded WebView2 window - no Edge/Chrome/Firefox, no browser profiles, no sign-in or sync prompts, nothing shared with your browsing.
*   **Portable single-file executable:** One `.exe`, no DLLs next to it, no Python/.NET/Node installation needed on the target machine (runtime is bundled). Download, double-click, done.
*   **Always the latest harness:** On launch the app runs `npm install -g @deepseek-ai/dsh@latest` (non-fatal, skippable with `--no-update`) and boots whatever npm delivers.
*   **Managed server lifecycle:** It starts `dsh web --no-open` on `127.0.0.1:3080` as a hidden child and **closing the window stops the server**. If a server is already running (e.g. one you started by hand), it **attaches** instead: opens the window, never touches that server, and leaves it running when the window closes.
*   **Your data stays local files:** Sessions, settings and storages live under `DSH_HOME` (default `C:\Users\<you>\.dsh`) - nothing is uploaded or synced; the wrapper only reads/writes what `dsh` itself uses.
*   **Official DeepSeek artwork:** Window/exe icons are fetched from the real websites - the blue whale from `deepseek.com` in light mode, the white whale from `platform.deepseek.com` in dark mode - and the window swaps them with the Windows theme.
*   **Theme-aware window chrome:** The title bar follows the *rendered page*: after the UI loads, its background color is measured and applied to the caption/border (Windows 11 22H2+), with a dark immersive caption for dark pages - no hard-coded white bar, no white startup flash.
*   **Visual Studio friendly:** `.sln`, one modern `.csproj`, and ready-made Publish profiles (`PortableFolder` / `PortableSingleFile`) for one-click publishing from the VS Publish dialog.

## 💻 How to Use (Easy Way)

1.  Go to the [**Releases**](https://github.com/ZeroHackz/deepseek-harness-windows-native/releases) page.
2.  Download the latest `DeepSeekHarness-portable-win-x64-<version>.zip` (or the `.exe` directly).
3.  Unzip anywhere (or just run the exe) and double-click `DeepSeekHarness.exe`.
4.  First launch may take a minute: it checks npm for the latest `@deepseek-ai/dsh`, boots the server, and opens the window.
5.  That's it - your sessions appear. **Close the window to stop the server** (unless you attached to an already-running one).

Only requirement on the target PC: the **WebView2 Runtime** (preinstalled on Windows 11 and most Windows 10 machines - the app shows an install link if it's missing) and Node.js with `npm install -g @deepseek-ai/dsh` for the harness itself.

## 🛠️ For Developers (Building from Source)

Prerequisites: **Windows 10/11 x64**, **PowerShell 7+**, and the **.NET 8 SDK**.

1.  Clone the repository:
    ```bash
    git clone https://github.com/ZeroHackz/deepseek-harness-windows-native.git
    cd deepseek-harness-windows-native
    ```
2.  Build the portable single-file executable (no DLLs, runtime bundled):
    ```powershell
    pwsh -File .\build_portable.ps1
    ```
    The `.exe` and a `.zip` land in `release\` (git-ignored - publish them via GitHub Releases).
3.  Alternative builds:
    ```powershell
    .\build.ps1                   # dev exe -> dist\ (framework-dependent, needs .NET 8 runtime)
    .\build.ps1 -Portable         # self-contained folder + zip (CyberleekViewer-style layout)
    .\build.ps1 -SingleFile       # lone self-contained exe + zip
    .\build_portable.ps1 -RefreshIcons   # re-fetch official DeepSeek icons first
    ```
4.  **Visual Studio:** open `DeepSeekHarness.sln`, right-click the project → **Publish…** → pick the `PortableFolder` or `PortableSingleFile` profile.

**GitHub Actions:** every push to `main` (and every `v*` tag) publishes the portable build and uploads it as a build artifact.

## 🚀 Usage

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

## 📂 Where Things Live

| What | Where |
| --- | --- |
| Sessions, settings, storages | `DSH_HOME` - default `C:\Users\<you>\.dsh` (shared with every `dsh web`) |
| App logs | `%LOCALAPPDATA%\DeepSeekHarness\logs\` (`desktop.log`, `server-*.log`, `npm-update.*.log`) |
| WebView2 local web state (trust cookie) | `%LOCALAPPDATA%\DeepSeekHarness\webview2\` - safe to delete, nothing valuable |
| Portable artifacts | `release\` (git-ignored) |

## 🖼️ Icons & Theming

`tools\update-icons.ps1` fetches the official DeepSeek artwork from the websites (`deepseek.com/favicon.ico` - blue whale for light mode; `platform.deepseek.com/favicon.svg` - re-tinted white for dark-mode legibility; the untouched black original is kept in `assets\source`) and regenerates the multi-size `.ico` files. The window icon follows the Windows theme, and the title-bar colors follow the measured page background via DWM (Windows 11 22H2+). Attribution: DeepSeek logos are trademarks of DeepSeek, used here only to identify the wrapped application.

## 🛡️ Troubleshooting

*   **Window shows "Starting …" forever / connection error** - the server didn't come up in time; run `DeepSeekHarness.exe --self-test`, retry with `--ready-timeout 240`, and check the server logs.
*   **`WebView2 failed to initialize`** - WebView2 Runtime missing; the window shows the official install link.
*   **Port already in use** - the app attaches to whatever answers (it's a dsh web you started? attach is fine). `--stop` only stops servers this app started.
*   **Server left running** (crash etc.) - `DeepSeekHarness.exe --stop`.

## Notes

*   **No accounts, no sync**: the app talks only to your local `dsh web` on the loopback address; harness data never leaves the machine.
*   This project is an independent wrapper: not affiliated with or endorsed by DeepSeek beyond invoking the official `dsh` CLI and using its public artwork.

## License

MIT - see [LICENSE](LICENSE).
