# Build / Сборка

Source files are in the separate GitHub/source archive. / Исходники находятся в отдельном GitHub-архиве.

On Windows x64 with .NET SDK 10, obtain `HIDMaestro.Core.dll` 2.1.0 from the official HIDMaestro release, then run from the source root:

```powershell
powershell -NoProfile -File .\build.ps1 -SdkDll 'C:\path\to\HIDMaestro.Core.dll'
```

The helper builds into `.build/app`, runs built-in tests without creating HID devices, and fails on errors. It does not install a driver. Run `.build/app/Aion2RemotePlay.exe` after the tests pass.

Скрипт собирает программу в `.build/app` и запускает встроенные тесты без создания HID-устройств. Драйвер не устанавливает. Для работы отдельно нужны драйвер HIDMaestro и права администратора.
