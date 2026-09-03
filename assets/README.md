# Icon assets

## Sources (official DeepSeek websites)

| File | Origin | Notes |
| --- | --- | --- |
| `source/deepseek-light-source.png` | `https://deepseek.com/favicon.ico` | Light/color variant: the blue whale on transparency (extracted from the site's 225×225 favicon). |
| `source/platform-dark-source.svg` | `https://fe-static.deepseek.com/platform/favicon.svg` | Dark variant source, **untouched** as served by platform.deepseek.com (the whale path ships `fill="#000"`). |
| `source/platform-dark-white.svg` | generated from the SVG above | Same drawing with `fill="#ffffff"` - the legible dark-mode rendition. |

## Generated files

`tools\update-icons.ps1` rasterizes the SVG (headless Chromium) and writes
multi-size `.ico` files (16/24/32/40/48/64/128/256 PNG frames) into
`src\DeepSeekHarness\assets\`:

- `icon-light.ico` - blue whale (light mode)
- `icon-dark.ico` - white whale (dark mode)
- `app.ico` - copy of `icon-light.ico`, compiled into the exe as `ApplicationIcon`

Regenerate any time:

```powershell
pwsh -File tools\update-icons.ps1            # re-fetch from the websites + rebuild
pwsh -File tools\update-icons.ps1 -SkipDownload   # offline, from the committed sources
```

DeepSeek logo and related artwork are trademarks of DeepSeek and are used
here only to identify the wrapped application; all rights remain with their
owner.
