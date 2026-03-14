# Tools Directory

Place external tool binaries here for the app to discover them automatically:

- `adb.exe` - Android Debug Bridge (from Android SDK Platform Tools)
- `scrcpy.exe` + supporting DLLs - scrcpy screen mirroring tool
- RustDesk client is typically installed system-wide

## Download Links

- **ADB**: https://developer.android.com/tools/releases/platform-tools
- **scrcpy**: https://github.com/Genymobile/scrcpy/releases
- **RustDesk**: https://github.com/rustdesk/rustdesk/releases

Extract the tools into this directory so the app can find them at relative paths.
