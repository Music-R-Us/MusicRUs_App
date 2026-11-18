# MusicRUs

MusicRUs is a music community frontend built with standalone Blazor WebAssembly on .NET 10 and Tailwind CSS 4.3.3. It connects to a separate backend API for music data, accounts, and community features.

## Features

- Search artists and albums, view artist details and album tracks.
- Manage owned and wanted music collections.
- Browse concerts and subscribe to artists.
- Create forum posts with music links and add comments.
- Customize profiles and favorite artists and albums.
- Manage friendships and blocked users.
- Sign up or sign in, including Google sign-in, and manage account and two-factor authentication settings.
- Switch between light and dark themes.

## Requirements

- .NET SDK 10.0.401 (or a newer patch in the same feature band)
- Node.js 22 LTS with npm
- A running compatible MusicRUs backend API for data and account features; the backend is not included in this repository.

The SDK version and patch roll-forward policy are defined in `global.json`. Frontend package versions are pinned in `package.json` and `package-lock.json`.

## Backend configuration

Configure the API connection in `wwwroot/appsettings.json`:

```json
"BackendApi": {
  "UseBackendApi": true,
  "BaseUrl": "https://localhost:5230/"
}
```

Set `BaseUrl` to your backend address, keeping the trailing slash. The default frontend origin is `http://localhost:5047`; configure the backend's CORS policy to allow it. If the backend uses local HTTPS, trust its development certificate as required by your backend setup.

Configuration under `wwwroot` is downloaded by the browser and must contain only public settings. Google sign-in also requires the Google client and backend authentication configuration to accept the frontend origin.

## Run

```powershell
dotnet run --launch-profile http
```

Run the command from the repository root, then open http://localhost:5047. The first build installs the pinned npm dependencies and every build compiles Tailwind CSS automatically. Initial setup requires network access to restore NuGet and npm packages.

To use the HTTPS frontend profile, run `dotnet run --launch-profile https` and open https://localhost:7118. Allow that origin in the backend's CORS configuration as well.

For live development, run these commands in separate terminals:

```powershell
npm.cmd run css:watch
dotnet watch --launch-profile http
```

Use `npm.cmd` on Windows if PowerShell blocks `npm.ps1`. On other platforms use `npm`.

## Build and publish

```powershell
dotnet build
dotnet publish -c Release
```

Deploy `bin/Release/net10.0/publish/wwwroot` to a static host with fallback routing to `index.html` so direct links to pages such as `/music` and `/posts` work. Before publishing, set the backend URL for the target environment and allow the deployed frontend origin in the backend's CORS configuration. The backend must be reachable from users' browsers.

The app uses `<base href="/">` in `wwwroot/index.html`, which assumes hosting at the domain root. Adjust it if hosting under a subpath.

## Styling

Add Tailwind utilities directly to Razor components. `Styles/app.css` imports Tailwind and declares the Razor/HTML sources; `wwwroot/css/tailwind.css` is generated. Blazor loading and error styles live in `wwwroot/css/app.css`.

After changing npm dependencies, run `npm.cmd ci` to synchronize your local installation with `package-lock.json`.

## Project structure

| Path | Purpose |
| --- | --- |
| `Pages/` | Routed pages for music, collections, posts, concerts, profiles, and login |
| `Components/` | Shared account, sign-in, and two-factor settings components |
| `Layout/` | Main layout, navigation, and toast notifications |
| `Services/` | API clients, authentication, and feature services |
| `Models/` | API request and response models |
| `Styles/app.css` | Tailwind entry point, source scanning, and theme definitions |
| `wwwroot/` | Public configuration, JavaScript helpers, images, and styles |
| `Program.cs` | Application startup and service registration |

## Troubleshooting

- **API data does not load or sign-in fails:** check that the backend is running, `BackendApi:BaseUrl` is correct, the browser trusts its HTTPS certificate, and CORS allows your frontend origin. Browser developer tools can help identify failed requests.
- **PowerShell blocks npm:** use `npm.cmd` for npm commands.
- **Styles are missing or stale:** run `npm.cmd ci` followed by `npm.cmd run css:build`, or restart the CSS watcher.
- **Refreshing a page returns a hosting error:** configure the static host to fall back to `index.html` for application routes.
