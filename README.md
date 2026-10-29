# Keyboop for Windows

Experimental, independent Windows port of [Keyboop](https://github.com/iffuno/keyboop) by Ivan Smetanin. Original project is MIT-licensed; this is not an official release.

## Status: first prototype

- C# / .NET 10 WPF Windows app.
- **Manual-only** hotkey `Ctrl+Alt+Space` converts the most recent unfinished word between standard ЙЦУКЕН and QWERTY layouts.
- Win32 `WH_KEYBOARD_LL` + `SendInput` with Unicode events. No clipboard mutation.
- Short **volatile** character buffer; no keystroke data is saved to disk.
- Automatic correction, password-field protection, selection correction, IME, voice, and compatibility testing are **not implemented**. This is not ready as an everyday keyboard utility.
- Editable OpenPencil design draft on VPS: `keyboop-windows-settings-v2.fig` (not yet an accepted faithful reproduction of upstream UI).

## VPS development and verification

**GitHub is source control only**. No GitHub Actions CI is used. Our isolated Linux build workspace is:

```
/home/admin/experiments/keyboop-windows/
├── repo/                  # source checkout
├── toolchain/dotnet/      # local .NET SDK 10.0.403
├── nuget/                 # isolated NuGet cache
├── tmp/                   # avoids VPS tmpfs exhaustion
└── artifacts/builds/      # immutable-by-commit build outputs
```

From the VPS repo checkout:

```bash
git fetch --depth=1 origin main && git reset --hard origin/main
bash scripts/vps-build.sh
```

The script runs the .NET core smoke checks, cross-compiles the Windows WPF project and publishes a **self-contained win-x64** directory and ZIP. It writes a receipt including source revision, SDK and SHA-256. Local SDK version on initial setup: 10.0.403.

A Linux cross-build does **not** verify a Windows keyboard hook or GUI behavior. Those require a real Windows test session.

## Manual Windows build (optional)

```powershell
dotnet build src/Keyboop.Windows/Keyboop.Windows.csproj -c Release
dotnet run --project tests/Keyboop.Core.Smoke/Keyboop.Core.Smoke.csproj
```

## Safety / release blockers

- Detect password and protected fields before recording even transient keys; ensure safe behavior with privilege boundaries.
- Track cursor and mouse movement, focus, synthetic input and `SendInput` partial failures.
- Verify on Windows in browsers, IDEs, Office, terminals, UAC and remote sessions.
- Port original Keyboop language detection and dictionaries, retaining license attribution.
- Only then consider automatic correction.

## Attribution

Original Keyboop © Ivan Smetanin (MIT), https://github.com/iffuno/keyboop. Upstream third-party dictionary assets are not redistributed in this prototype.
