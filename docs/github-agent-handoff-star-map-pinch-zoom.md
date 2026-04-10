# Backlog Item: Finish reliable pinch-zoom refactor for AstroFinder star map

## Suggested GitHub issue title
`fix(starmap): complete reliable pinch-zoom viewport transform for AstroFinder`

## Summary
AstroFinder's star hop map needs a **reliable Android-friendly pinch-to-zoom and pan implementation**.

The chosen architecture is:
- `GraphicsView`
- **world-coordinate redraw model**
- single authoritative `StarMapViewportTransform`
- pinch zoom around the finger centroid
- pan via incremental deltas
- no dependence on scaling/translating the MAUI view tree as the source of truth

This work is **partially implemented and paused at a clean checkpoint**. Do **not** assume the current working tree has been fully verified after the latest transform refactor.

---

## Current checkpoint

### Completed in code
- Added a new transform helper:
  - `AstroFinder.App/Views/StarMapViewportTransform.cs`
- Added focused tests for the transform math:
  - `AstroFinder.App.Tests/Views/StarMapViewportTransformTests.cs`
- Refactored star-map gesture handling toward redraw-based viewport updates:
  - `AstroFinder.App/Views/StarMapPage.xaml`
  - `AstroFinder.App/Views/StarMapPage.xaml.cs`
- Wired `StarMapDrawable` to consume the transform during drawing:
  - `AstroFinder.App/Views/StarMapDrawable.cs`
- Existing related behavior already in place:
  - observer-aware sky orientation setting and permission flow
  - richer context stars in the map
  - background stars unlabeled
  - key star/target labels with faint callout lines

### Important note
The latest viewport-transform refactor was **intentionally paused before final verification** so it can be handed off cleanly. The next agent must **build/test first** and treat any failures as expected handoff work.

---

## User-facing goal
When viewing the star hop map:
1. **Two-finger pinch** should zoom smoothly and predictably.
2. Zoom should stay centered around the user's fingers.
3. **One-finger drag** should pan the map after zooming in.
4. **Reset** and **double-tap reset** should restore the default view.
5. Key labels should remain readable and reasonably placed.
6. Extra field stars should remain visible but **unlabeled**.

---

## Required acceptance criteria
- [ ] `dotnet build AstroFinder.sln` succeeds
- [ ] `dotnet test AstroFinder.sln --no-build` succeeds
- [ ] Android install succeeds for `AstroFinder.App`
- [ ] On device, pinch zoom works reliably for the star map
- [ ] On device, pan works only once zoomed in
- [ ] Label placement remains readable and does not regress badly
- [ ] No regression to target marker, hop path, or sky-orientation behavior

---

## Recommended implementation approach
Use the **viewport-transform redraw model** already started.

### Keep
- `GraphicsView`
- `StarMapDrawable`
- map data in logical coordinates
- one transform state object

### Avoid
- treating `Scale`, `TranslationX`, and `TranslationY` on the MAUI container as the long-term truth
- resizing individual primitives directly to simulate zoom
- rasterizing the map into an image for interaction

### If Android still misbehaves
The next escalation path should be:
1. keep the transform model,
2. unify touch handling further on a single gesture surface,
3. only consider custom touch handling or SkiaSharp **if needed after verification**.

---

## Files to inspect first
- `AstroFinder.App/Views/StarMapViewportTransform.cs`
- `AstroFinder.App/Views/StarMapPage.xaml`
- `AstroFinder.App/Views/StarMapPage.xaml.cs`
- `AstroFinder.App/Views/StarMapDrawable.cs`
- `AstroFinder.App.Tests/Views/StarMapViewportTransformTests.cs`

Also keep an eye on the surrounding flow:
- `AstroFinder.App/ViewModels/MainPageViewModel.cs`
- `AstroFinder.App/Views/StarMapData.cs`

---

## Verification commands
Run these in `c:\Users\simon\source\repos\astrofinder`:

```powershell
dotnet build AstroFinder.sln
dotnet test AstroFinder.sln --no-build
dotnet build "AstroFinder.App/AstroFinder.App.csproj" -f net8.0-android -t:Install
& "C:\Users\simon\source\platform-tools\adb.exe" shell monkey -p com.astroapps.astrofinder -c android.intent.category.LAUNCHER 1
```

---

## Handoff note for the next agent
Please continue from the current viewport-transform refactor rather than reverting to container scaling.

The right end state is a **deterministic redraw-based zoom/pan model** for the star map, not a UI-tree scaling workaround.
