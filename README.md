# Keyboop for Windows

Experimental, independent Windows port of [Keyboop](https://github.com/iffuno/keyboop) by Ivan Smetanin. Original project is MIT licensed; this is not an official release.

## First prototype

- C# / .NET 10 WPF application for Windows 10/11.
- **Manual-only** hotkey: Ctrl + Alt + Space converts the last **unfinished word** typed in the active window between standard Russian ЙЦУКЕН and English QWERTY layouts.
- Win32 `WH_KEYBOARD_LL` + `SendInput` with Unicode input; **does not modify the clipboard**.
- No persistence of typed characters. Short volatile buffer is cleared on focus change and common editing actions.
- No automatic correction, selection conversion, password field detection or elevated-app support yet.
- No voice transcription in this prototype.
- Editable design source on OpenPencil VPS: `keyboop-windows-settings-v1.fig` (draft, visual QA pending).

## Build

Windows 10/11 + .NET 10 SDK:

```powershell
dotnet build src/Keyboop.Windows/Keyboop.Windows.csproj -c Release
dotnet run --project src/Keyboop.Windows/Keyboop.Windows.csproj
dotnet run --project tests/Keyboop.Core.Smoke/Keyboop.Core.Smoke.csproj
```

The CI builds on Windows, runs the conversion smoke checks and archives a build artifact. **The real keyboard hook has not yet been validated on an interactive Windows desktop.** Do not install this prototype as an everyday keyboard filter.

## Safety and next work

- Exclude protected password fields using real UI Automation + secure input policy, avoid holding even volatile password chars.
- Track caret movement, mouse events, window/thread focus and IME/dead keys; test Windows RDP, browsers, Office, IDEs.
- Check `SendInput` return value and handle failed / partial injection without silent data loss.
- Port Keyboop's language detector, dictionary assets and exception rules under preserved attribution.
- Enable automatic correction **only** after system-level safety gates pass.
- Record Win32 integration test results and editable design/visual QA.

## Attribution

Original Keyboop © Ivan Smetanin (MIT), https://github.com/iffuno/keyboop. Third-party dictionaries are **not** redistributed in the current first prototype.
