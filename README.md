# MapNavigationRecorder

Lightweight .NET MAUI app to record GPS tracks, snap them to roads (optional), and save/load recorded routes.

Works with .NET 9 and uses a WebView-based map (local `map.html`) for visualization.

## Features
- Record GPS points with a distance threshold to avoid dense sampling
- Optional road snapping using OSRM (online)
- Save / load / rename / delete recorded routes (stored as JSON in app data)
- Windows fallback to IP-based location when device geolocation is not available
- Simple UI: Record, Stop, Clear, Saved Routes

## Quick start

Prerequisites
- .NET 9 SDK
- .NET MAUI workloads installed (`dotnet workload install maui`)
- Visual Studio with MAUI support (or another MAUI-capable IDE) and platform workloads for the targets you need (Android / Windows / MacCatalyst)

Open & run
1. Clone the repo:
   git clone https://github.com/alijatoi/MapNavigationRecorder
2. Open the solution in Visual Studio.
3. In __Solution Explorer__, choose the startup project and the target platform (Android / Windows / MacCatalyst).
4. Run the app via __Debug > Start Debugging__ (F5) or use the appropriate run command for your platform.

CLI example (build):
- `dotnet build`  
Platform run support varies by SDK and workload; prefer running from Visual Studio for MAUI apps.

## Usage
- Tap **Record** to begin collecting GPS points (requests location permission).
- Tap **Stop** to end recording. The app will try to snap the recorded track to roads (OSRM).
- When stopping, you can save the route with a name.
- Use **Saved Routes** to list, display, rename, or delete saved routes.
- **Clear** removes the current in-memory track and clears the map.

## Important files
- `MainPage.xaml` / `MainPage.xaml.cs` — UI and recording logic
- `Services/RouteStorageService.cs` — save/load/rename/delete routes (stored as `{Id}.json` in `FileSystem.AppDataDirectory`)
- `Models/SavedRoute.cs`, `Models/GpsPoint.cs` — route and point models
- `map.html` (app package) — local HTML/JS used to render the map inside the WebView

## Data & privacy
- Recorded location points are stored locally in the app data directory as JSON files.
- The app sends recorded coordinates to the public OSRM demo service (https://router.project-osrm.org) to compute snapped routes. You can disable or replace this if you need offline or private snapping.

## Troubleshooting
- If the map fails to load, verify `map.html` exists in the app package. The app shows a fallback message when the resource is missing.
- On Windows without geolocation hardware, the app attempts IP-based lookup via `ipapi.co` as a fallback.
- Ensure location permission is granted on device/emulator.

## Contributing
Issues and pull requests are welcome. Keep changes small and focused. Add tests for non-UI logic where feasible.

