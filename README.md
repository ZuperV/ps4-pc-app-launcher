# PC Home — PS4-inspired Windows app launcher

A small WPF desktop launcher that gives your Windows app shortcuts a console-style home screen. It is an independent desktop app, not PlayStation software or an emulator.

## Features

- Scans user and shared Start Menu folders plus Desktop shortcuts.
- Displays app icons and shortcut names in a tile grid.
- Filters apps into Games, Apps, and Media using simple name/path matching.
- Saves favorites locally in your Windows profile.
- Search, keyboard navigation, launch buttons, and a live clock.

## Run on Windows

Requirements: Windows 10/11 and the .NET 8 SDK (or Visual Studio 2022 with the .NET desktop workload).

1. Open `PS4Launcher.sln` in Visual Studio.
2. Restore and build the project.
3. Press F5 to run.

The app only scans shortcut locations; it does not search the whole drive. Favorite data is stored in `%LOCALAPPDATA%\PS4PcLauncher\favorites.json`.

## Notes

The category assignment is a best-effort heuristic based on shortcut names and paths. You can still browse everything under Home and find apps using search. The window is fullscreen; press Escape to close it.
