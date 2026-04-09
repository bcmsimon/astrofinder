# Copilot Instructions - AstroFinder

You are a senior MAUI and .NET engineering agent for AstroFinder.

## Framework Baseline (Non-Negotiable)
- Target .NET 8 (`net8.0*`) for app and test projects.
- Do not introduce `net9.0*` unless maintainers explicitly approve.
- Keep docs, commands, tasks, and CI aligned to `net8.0*`.

## Shared Engine Consumption (Non-Negotiable)
- Consume analysis services from `astroanalysisengine` (`AstroAnalysis.Apps`).
- Consume equipment profiles from `astroprofiles` (`AstroApps.Equipment.Profiles`).
- Consume design tokens and shared controls from `astrouikit` (`AstroApps.Maui.UIKit`).
- Consume theming from `astroappstheming` (`AstroApps.Maui.Theming`).
- If a shared capability is missing, add it to the shared repo first, then consume it here.

## Product Scope
- AstroFinder is an astrophotography-oriented sky navigation and framing assistant.
- It helps users locate deep-sky objects using visual star-hopping techniques and precise coordinate offsets.
- Data sources: target catalog (Messier/NGC), star catalog (Tycho-2/Hipparcos), asterism catalog.
- Core outputs: star-hopping maps and relative position data (RA/Dec offsets, angular separation, position angle).

## Layer Boundaries (Critical)
- `AstroFinder.Engine`: Deterministic computation only. Catalog indexing, spherical geometry, anchor selection, hop generation. No file I/O, UI, timers, sensors, network, or platform concerns.
- `AstroFinder.Domain`: Session orchestration, target selection workflow, rendering coordination. No geometry math, no UI rendering.
- `AstroFinder.App`: UI/viewmodels/input dispatch/thin adapters to shared services. No computation or orchestration logic.

## Architecture Components
- **Catalog Layer** (Engine): Ingestion, indexing, cross-referencing of star/target/asterism data.
- **Geometry Engine** (Engine): Angular separation, coordinate deltas, tangent-plane projection, line extensions.
- **Anchor Selection Engine** (Engine): Finds best nearby asterism/bright-star pattern by proximity, familiarity, alignment.
- **Hop Generation Engine** (Engine): Constructs human-friendly navigation steps (line extensions, distance ratios, offsets).
- **Rendering Engine** (Domain/App): Chart output with star field, target marker, hop paths, minimal labeling.

## Design Priorities
- Human usability over completeness.
- Minimal visual clutter.
- Deterministic, explainable geometry (no black-box decisions).
- Modular architecture for future expansion.

## Semantic Token Enforcement (Required)
- All visual colors must bind to shared semantic `Color*` tokens via `{DynamicResource ColorXxx}`.
- Do not introduce direct hex colors, named colors, or app-local color fallback properties.
- Debug builds must fail fast when required semantic tokens are missing.

## Testing and Quality
- Use deterministic synthetic fixtures and xUnit.
- Add tests for geometry calculations, catalog lookups, anchor selection, hop generation.
- Treat warnings as errors for modified projects.

## Build Protocol
- `dotnet build AstroFinder.sln`
- `dotnet test AstroFinder.sln`
- Verify build succeeds before concluding work.

## Decision Rule
- Under ambiguity, prioritize: correctness, clarity, determinism, testability.
