# AstroFinder RealityKit AR Continuation Prompt

Use this prompt in a Mac-backed AstroFinder workspace with Xcode/signing already working.

## Goal

Replace AstroFinder's current AR screen with a minimal reproducible AR diagnostic, then keep the design aligned for a later star-hop map that is anchored in world space the same way as the diagnostic cross.

The user explicitly chose the long-term iOS direction:

- iOS should use ARKit with RealityKit `ARView`
- Android should use ARCore
- The diagnostic should stay minimal and explicit, not framework-heavy or over-abstracted

## Exact diagnostic requirements

Implement a temporary AR debug screen with only:

- camera passthrough / AR view
- debug HUD
- one simple cross marker fixed in world space

Remove or keep out of the diagnostic path:

- all astronomy logic
- star-hop logic
- overlays, labels, reticles, guides
- screen-space projection math
- gesture placement
- tap-to-place
- compass / heading / CoreLocation / GPS / CLLocation
- plane visualization
- continuous raycasts
- per-frame repositioning
- anchor updates tied to camera movement

### Session rules

#### iOS

- use `ARWorldTrackingConfiguration`
- `worldAlignment = .gravity`
- do not use `.gravityAndHeading`
- no plane detection
- no environment texturing
- start the session once
- do not restart or reconfigure it during normal updates

#### Android

- use ARCore basic world tracking only
- no geospatial mode
- no cloud anchors
- no augmented images
- no depth extras unless absolutely required by the base setup
- do not recreate the session repeatedly

### Marker rules

- add exactly one world-space marker
- place it exactly once
- place it about 1.5 meters in front of the camera/device when the AR session first becomes usable
- after placement, never move it, never recreate it, never smooth it, never re-anchor it
- no per-frame correction logic

### Marker appearance

- simple red cross / plus marker
- tiny 3D cross made from two thin bars
- roughly 5 to 10 cm
- no labels
- no animation
- no billboarding
- no screen-space reticle

### HUD and logging

HUD must show:

- current tracking state
- anchor placed count
- whether session is initialized
- platform name

Console logging must include:

- when the AR session starts
- when the marker is placed
- marker world position / pose
- tracking state changes

## Later feature direction after the diagnostic

After the diagnostic passes, the next AR feature will be a star-hop map anchored in world space like the cross and oriented so it reads naturally when the user looks up.

That means the iOS long-term target should stay RealityKit rather than SceneKit.

Recommended architecture for the later map:

- keep the native AR layer thin
- let shared/app logic compute anchor transform and orientation inputs
- let the native AR layer only start the session, create the anchor, and render anchored entities

## Important context from this Windows session

This Windows workspace could not finish an exact RealityKit implementation because the local .NET iOS bindings available here did not expose `RealityKit.ARView` directly.

Because of that, a temporary iOS SceneKit fallback was partially added in the repo only as a placeholder. On the Mac-backed continuation, do not treat that fallback as the intended final design.

## Latest update from Mac-backed continuation (Apr 20, 2026)

The Mac continuation confirmed the same underlying constraint in this toolchain: RealityKit types are still not available to the .NET iOS compiler surface used by this workspace (for example `RealityKit.ARView`, `AnchorEntity`, `Entity` not found at compile time).

A bridge-first structure was added so the app-facing handler can stay stable while the native iOS implementation is swapped later:

- `ArCameraViewHandler` now targets `UIView` and consumes a native bridge interface.
- `ArDiagnosticNativeBridge` currently routes to the SceneKit diagnostic host.
- The bridge is the intended seam for a future Swift-backed RealityKit host.

The iOS diagnostic view was also repaired to compile and run with this binding set:

- split frame callbacks into `ARSessionDelegate` (for `DidUpdateFrame`) and scene rendering into `ARSCNViewDelegate`
- keep one-time anchor placement semantics (no per-frame repositioning)
- preserve HUD/status callbacks to MAUI

Additional startup hardening was added while diagnosing splash-time failures on an iPhone SE (iOS 15.8.7):

- iOS debug mtouch startup environment now disables diagnostics/profiler startup paths in project settings
- `MainPage.OnAppearing` initialization was moved to fire-and-forget async startup with guard + exception logging so the first page can render sooner

This improved launch behavior enough to install and launch consistently via `mlaunch`, though further iOS UX verification is still required on device.

## Files changed in this session for the diagnostic path

These changes were made toward the temporary diagnostic refactor:

- `AstroFinder.App/Controls/ArCameraView.cs`
- `AstroFinder.App/Views/ArStarHopPage.xaml`
- `AstroFinder.App/Views/ArStarHopPage.xaml.cs`
- `AstroFinder.App/Views/StarMapPage.xaml.cs`
- `AstroFinder.App/MauiProgram.cs`
- `AstroFinder.App/Platforms/Android/Ar/ArCameraViewHandler.cs`
- `AstroFinder.App/Platforms/Android/Ar/ArCoreGLView.cs`
- `AstroFinder.App/Platforms/Android/Ar/DiagnosticCrossShader.cs`
- `AstroFinder.App/Platforms/iOS/Ar/ArCameraViewHandler.cs`
- `AstroFinder.App/Platforms/iOS/Ar/ArKitDiagnosticView.cs`
- `AstroFinder.App/Platforms/iOS/Ar/ArDiagnosticNativeBridge.cs`
- `AstroFinder.App/Platforms/iOS/Ar/ArKitDiagnosticView.WindowsFallback.cs`

### What those changes mean

- The MAUI AR page was simplified into a diagnostic HUD page.
- Android was moved toward a one-anchor diagnostic implementation.
- iOS currently contains a SceneKit fallback implementation using `ARSCNView`, wrapped behind a bridge seam for future RealityKit swap-in.
- The MAUI iOS handler no longer depends directly on a specific native AR view type.

## What to do next on the Mac-backed continuation

1. Inspect current diffs in AstroFinder and isolate the AR diagnostic edits from unrelated repo changes.
2. Keep the simplified MAUI diagnostic page shape if it is useful.
3. Keep the bridge seam (`IArDiagnosticNativeBridge`) and replace only the bridge implementation with a true native RealityKit host.
4. Implement the RealityKit host in native Swift if the .NET binding surface still omits `ARView`.
5. Keep the iOS implementation explicit and minimal.
6. Verify the marker is placed once and remains world-locked as the device moves.
7. Preserve the Android one-anchor diagnostic path unless it proves broken during validation.
8. Update `docs/github-agent-handoff-app-overview.md` if the user-visible AR workflow has materially changed.

## What to do next on Windows

When switching back to Windows, treat iOS as non-runnable but keep iOS code build-safe:

1. Continue Android AR diagnostic iteration only.
2. Keep `ArKitDiagnosticView.WindowsFallback.cs` in place for Windows-side iOS compile compatibility.
3. Do not attempt to validate RealityKit behavior from Windows; reserve that for Mac+iPhone sessions.
4. Keep all app-level AR contracts and HUD semantics shared so Android and iOS remain behaviorally aligned.
5. Keep the bridge interface stable so the future Swift RealityKit bridge can drop in without MAUI handler changes.

## RealityKit guidance for the Mac-backed implementation

Preferred iOS implementation shape:

- native `ARView`
- run `ARWorldTrackingConfiguration` once
- place one anchor once when tracking becomes usable
- attach one red cross entity to that anchor
- expose only status and diagnostics back to MAUI

Keep comments explaining:

- why the marker must only be placed once
- why forward is negative Z in camera-like iOS space
- common mistakes that make a marker appear to follow the camera

## Validation target

Success condition for the diagnostic:

When moving the iPhone/iPad around, the cross should appear stuck in one real-world location.

If it follows the device, shifts with rotation, jumps, or gets recreated, the bug is still in the AR/session/anchoring layer.

## Suggested continuation prompt to paste

"Continue the AstroFinder AR diagnostic work. Use the repo file `docs/realitykit-ar-continuation-prompt.md` as the source of truth. Replace the temporary iOS SceneKit fallback with the requested RealityKit `ARView` implementation in the Mac-backed workspace, keep Android on the one-anchor ARCore diagnostic path, and validate the one-time world anchor behavior on device."