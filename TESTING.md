# Keyboop for Windows: first user test (2026-10-29)

**Status:** experimental manual-only prototype; no claim of full Keyboop parity.

## Download

Self-contained Windows x64 ZIP: https://keyboop-builds.37-114-42-105.sslip.io/keyboop-windows-0ac9217fd450-win-x64.zip

Source revision: `0ac9217fd4505b37178dced114e9933e21e07636`

SHA-256: `64bdc689d5acb1a12d234de4cacb06e39d20f73607692d1261e44a29c948c30e`

The VPS built the executable with .NET SDK 10.0.403, Windows targeting, runtime bundled. Linux cross-build and 8 core smoke tests passed. The archive was downloaded back via public HTTPS and was byte-for-byte identical to the build artifact, with ZIP integrity verified.

## Manual test procedure

1. Download the ZIP to Windows 10/11 x64 and extract it **completely** to a new directory.
2. Optionally verify the SHA-256 in PowerShell: `Get-FileHash .\keyboop-windows-0ac9217fd450-win-x64.zip -Algorithm SHA256`.
3. Run `Keyboop.Windows.exe` from the extracted directory. No installation or administrator rights should be required. Binary is unsigned; Windows may report unknown publisher.
4. In the application test field, type `ghbdtn` and press **Преобразовать пример**. Expect `привет`. This checks the conversion logic only.
5. Click **Включить тест перехвата**, then click into the test field or open standard **Блокнот**. Type `ghbdtn` **without a trailing space**, press `Ctrl+Alt+Space` and release all keys. Expected: word changes to `привет` without clipboard changes.
6. Click **Отключить тест перехвата** or close the application to unregister hooks.

## Known restrictions

- Keyboard capture is off by default and restricted to the Keyboop test window and notepad.exe.
- Does **not** monitor other applications, browser fields, passwords, remote sessions or elevated programs.
- Only incomplete last-word RU/EN QWERTY/ЙЦУКЕН manual conversion; no automatic language identification, snippet engine, speech, translation or full original Keyboop capabilities.
- This is the first build of a low-level keyboard hook; real Windows desktop compatibility has not yet been validated.
- Do not enter passwords, confidential text or critical work content during this test. The test hook uses no clipboard and never persists keystrokes.
- `SendInput` can fail due to integrity boundaries or input focus; if status reports partial injection, inspect the text before retrying. A restore/undo transaction is not yet implemented.

## Source and design

Source: https://github.com/genrudko/keyboop-windows

Editable OpenPencil draft on VPS: `/home/admin/openpencil-service/designs/keyboop-windows-settings-v2.fig`. This is not yet a pixel-perfect copy of the original macOS UI.
