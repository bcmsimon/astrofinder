# AstroFinder Expert AI Handoff

## Purpose

AstroFinder is an astrophotography-oriented sky navigation and framing assistant. It helps users locate deep-sky objects by combining human-friendly star-hopping guidance with precise geometric reference data such as RA/Dec offsets, angular separation, and position angle.

The app is intended to help a user get from a known star pattern to a target object with clear, explainable guidance rather than black-box automation.

## Main Features

### 1. Star-Hopping Maps

- Generates visual sky charts for navigation.
- Uses magnitude-filtered star fields to reduce clutter.
- Highlights useful reference stars, recognizable patterns, and suggested hop paths.

### 2. Relative Position Guidance

- Produces precise RA/Dec offsets between reference points and target objects.
- Reports angular separation and position angle.
- Supports both visual navigation and framing/planning workflows.

### 3. Asterism Recognition

- Uses curated familiar star-group patterns.
- Helps the user orient using recognizable shapes rather than raw catalog data alone.

### 4. Anchor Selection

- Chooses the best nearby bright-star pattern or anchor reference for a target.
- Balances proximity, recognizability, and alignment usefulness.

### 5. Finder and Orientation Overlays

- Can overlay navigation aids such as Telrad circles, finder fields of view, and orientation markers.
- Keeps the chart useful for real telescope and finder workflows.

### 6. Catalog-Driven Navigation

- Uses a target catalog for deep-sky objects.
- Uses a star catalog for positions and magnitudes.
- Uses an asterism catalog for familiar navigation patterns.

### 7. Deterministic Navigation Engine

- Geometry, anchor selection, and hop generation are deterministic and explainable.
- The engine is designed so the same input produces the same result.

## Product Boundaries

- AstroFinder is not a general planetarium app.
- It is focused on practical target location and framing assistance for astrophotography workflows.
- Core geometry and navigation logic belong in deterministic engine layers, not the UI.

## Handoff Guidance

- Treat this file as the canonical overview for an expert AI that needs fast product context.
- Keep it current when a main workflow or user-visible feature is added, removed, renamed, or materially changed.
- Debug builds include an AR fixture replay mode for mounted-pointing verification, and the AR page shows a persistent replay badge whenever that mode is active.
- Existing `docs/github-agent-handoff-*.md` files are feature-specific supplements, not replacements for this overview.