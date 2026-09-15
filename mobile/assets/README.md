# Mobile app assets

Drop these four PNG files into this directory. The paths are already wired in `mobile/app.json`, so as soon as the files exist here `eas build` picks them up.

| File | Purpose | Size | Background |
| --- | --- | --- | --- |
| `icon.png` | iOS home-screen icon | 1024×1024 | **Must be opaque** (Apple rejects transparency) |
| `adaptive-icon.png` | Android adaptive icon (foreground layer) | 1024×1024 | Transparent OK — Android applies the emerald `#0b3d2e` behind it (declared in `app.json`) |
| `splash-icon.png` | Launch screen center image | 1024×1024 | Transparent OK — Expo composites it onto emerald |
| `favicon.png` | Optional web favicon (used only if you `expo start --web`) | 48×48 | Transparent OK |

## Fastest way to produce all four from the LVSS shield logo

Two-minute path, no image editor required:

1. Save the LVSS shield source PNG somewhere on disk (any size).
2. Open <https://icon.kitchen> in a browser.
3. Drop the shield PNG onto the page.
4. In the sidebar:
   - **Background color:** `#0b3d2e` (matches `adaptiveIcon.backgroundColor` in `app.json`)
   - **Padding:** ~15% (so the shield doesn't touch the rounded corners iOS applies)
5. Click **Download** — you'll get a ZIP with correctly-sized `icon.png` and `adaptive-icon.png`.
6. Rename / copy them into this directory. For `splash-icon.png` and `favicon.png`, resize/reuse the `adaptive-icon.png` output.

## Alternative: hand-craft in any image editor

- Canvas: 1024×1024
- Background: solid `#0b3d2e` (for `icon.png`), transparent (for the other three)
- Center the shield with ~10–15% padding around it
- Export as PNG

## What NOT to do

- No transparent `icon.png` — Apple's App Store review will reject the build.
- No rounded corners on `icon.png` — Apple applies them; adding your own gives a double-rounded look.
- Don't commit `.psd` / `.ai` / `.sketch` sources here — export the final PNG only.
