# PS4 PC App Launcher

A Windows desktop launcher with a PlayStation 4-inspired interface. It scans apps installed on your PC and displays them as large PS4-style tiles, letting you launch them with one click.

This project is not a real PS4 app or emulator. It is a Windows application that imitates the look and feel of a PS4 dashboard while launching software already installed on your computer.

## Features

- Dark PS4-style UI
- Search box to filter apps
- Loads app shortcuts from your Start Menu
- Uses each app's icon for the tile
- Launches installed EXE files
- Works on Windows desktop

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with .NET desktop development workload

## Run locally

1. Open the solution in Visual Studio 2022.
2. Restore NuGet packages.
3. Set the startup project to `PS4Launcher`.
4. Press F5 to run.

## Project structure

- `PS4Launcher/` – WPF app source code
- `README.md` – project overview
- `PS4Launcher.sln` – Visual Studio solution

## Notes

The app scans common Start Menu folders and Desktop shortcuts to find installed apps. It uses Windows shell integration to read app shortcuts and launch them.

If you want, this project can be expanded with:

- categories like Games, Apps, Media, Utilities
- PS4-style vertical navigation
- animated backgrounds and UI effects
- keyboard navigation using PS4 controller-like controls
