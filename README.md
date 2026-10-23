# MusicRUs

Standalone Blazor WebAssembly app targeting .NET 10, with Tailwind CSS 4.3.3.

## Requirements

- .NET SDK 10.0.401 (or a newer patch in the same feature band)
- Node.js 22 LTS with npm

## Run

```powershell
dotnet run --launch-profile http
```

Open http://localhost:5047. The first build installs the pinned npm dependencies and every build compiles Tailwind CSS automatically.

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

Deploy `bin/Release/net10.0/publish/wwwroot` to a static host with fallback routing to `index.html`.

## Styling

Add Tailwind utilities directly to Razor components. `Styles/app.css` imports Tailwind and declares the Razor/HTML sources; `wwwroot/css/tailwind.css` is generated. Blazor loading and error styles live in `wwwroot/css/app.css`.

After changing npm dependencies, run `npm.cmd ci` to synchronize your local installation with `package-lock.json`.
