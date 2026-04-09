# AstroFinder

Astrophotography-oriented sky navigation and framing assistant for the [AstroApps](https://github.com/bcmsimon/astroapps) ecosystem.

AstroFinder helps users locate deep-sky objects using both visual star-hopping techniques and precise coordinate offsets.

## Features

- **Star-Hopping Maps** — Visual sky charts with magnitude-filtered star fields, highlighted asterisms, and suggested hop paths
- **Relative Position Output** — Precise RA/Dec offsets, angular separations, and position angles between reference stars and targets
- **Asterism Recognition** — Curated pattern matching for familiar star groups (Big Dipper, Orion's Belt, etc.)
- **Anchor Selection** — Automatic identification of the best nearby bright-star pattern for navigation
- **Finder Overlays** — Optional Telrad circles, finder FOV, and orientation markers

## Architecture

```
AstroFinder.Engine     — Deterministic computation (catalogs, geometry, hop generation, anchor selection)
AstroFinder.Domain     — Session orchestration, target selection, rendering coordination
AstroFinder.App        — MAUI UI (Windows, iOS, Android)
```

### Shared Libraries

| Library | Purpose |
|---------|---------|
| AstroApps.Maui.UIKit | Design tokens, shared controls |
| AstroApps.Maui.Theming | Theme framework and runtime apply |
| AstroAnalysis.Apps | Shared analysis services |
| AstroApps.Equipment.Profiles | Equipment profile models |

## Data Sources

1. **Target Catalog** — Messier/NGC objects with RA/Dec coordinates
2. **Star Catalog** — Tycho-2/Hipparcos star positions and magnitudes
3. **Asterism Catalog** — Curated recognizable star patterns

## Build

```bash
dotnet build AstroFinder.sln
```

### Run (Windows)

```bash
dotnet build AstroFinder.App/AstroFinder.App.csproj -f net8.0-windows10.0.19041.0 -t:Run
```

### Test

```bash
dotnet test AstroFinder.sln
```

## Target Frameworks

- .NET 8 (net8.0) for Engine/Domain
- .NET 8 MAUI (net8.0-android, net8.0-ios, net8.0-windows) for App

## License

See [LICENSE](LICENSE) for details.
