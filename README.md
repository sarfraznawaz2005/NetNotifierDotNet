# NetNotifier

A simple system tray app that watches your internet connection and tells you when it
drops or comes back, built with WPF on .NET 8.

## Features

- Sits in the system tray. Green icon when online, red when offline.
- Checks a real network adapter first, then confirms with an HTTP request to a couple
  of test sites (Google, Bing by default) - both configurable.
- Speaks "Connection Lost" / "Connection Restored" out loud (optional, uses Windows'
  built-in text-to-speech).
- Tray tooltip shows your public IP, uptime, disconnect count, and % availability.
- Settings window: test URLs, check interval, HTTP timeout, voice alerts on/off, and
  Start with Windows.
- Checks run in the background, so the UI never freezes. This is a .NET rewrite of an
  older AutoHotkey version that had that problem.

## Network access

This app is a connectivity checker, so reaching out to the internet is the whole point:

- HTTP GET requests to the test URLs you configure (default: google.com, bing.com), to
  detect whether you're online.
- A public IP lookup (api.ipify.org, falling back to icanhazip.com) to show your IP in
  the tooltip.

No other network access. No telemetry, analytics, or update checks.

## Data and privacy

All data stays on your machine, under your own user account:

- Settings: `%APPDATA%\NetNotifier\settings.json`, plain JSON, human-readable.
- "Start with Windows" writes one value under
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. No admin rights are
  used or required, and nothing is written to `HKEY_LOCAL_MACHINE`.

## Building

Requires the .NET 8 SDK (`dotnet --version` should report 8.x).

- `build.bat` (or `build.ps1`) - cleans, then publishes a slim, framework-dependent,
  single-file production exe to `publish\NetNotifier.exe`. "Framework-dependent" means
  it needs the .NET 8 Desktop Runtime already on the machine that runs it - that's what
  keeps it small instead of bundling the whole runtime.
- `run.bat` (or `run.ps1`) - stops any already-running instance launched from this
  project's own folders, cleans, then runs the app straight from source in dev mode
  (`dotnet run`, Debug config).

## Project layout

```
Models/               AppSettings - what's persisted
Services/             ConnectivityChecker, PublicIpService, SettingsService,
                      StartupService, SpeechService
NetNotifierApp.cs     orchestrates checks, stats, status changes
TrayIconManager.cs    tray icon, tooltip, context menu
SettingsWindow.xaml   the Settings window
```
