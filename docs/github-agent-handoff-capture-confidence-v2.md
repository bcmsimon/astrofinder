# Backlog Item: AstroFinder v2 — passive framing and capture confidence

## Suggested GitHub issue title
`feat(v2): add passive framing and capture-confidence monitoring to AstroFinder`

## Summary
AstroFinder v2 should help users answer a simple field question:

> **"Should I trust the data I am collecting right now, and is my target still framed as expected?"**

This must be implemented as a **passive observer of captured frames**, not as a phone-mounted shared-view system. The phone should not be required to share the same optical path as the imaging camera.

---

## Product framing

### What this is
- A lightweight capture-health instrument for beginners using a tripod or optional tracker.
- A framing-confidence layer that complements AstroFinder's existing star-hop and overlay workflow.
- A consumer of shared frame-analysis metrics from `AstroAnalysis.Apps`.

### What this is not
- Not a second camera alignment workflow.
- Not plate solving.
- Not direct camera control as the first requirement.
- Not a generic diagnostics dashboard overloaded with graphs.

---

## First implementation slice now in repo

The first deterministic core has been added so the feature can grow incrementally:

- `AstroFinder.Engine/CaptureConfidence/` — shared models + `CaptureConfidenceEvaluator`
- `AstroFinder.Domain/CaptureConfidence/` — rolling `CaptureConfidenceMonitor`
- deterministic tests for stable / degrading / problem scenarios

This gives AstroFinder a compile-ready trust-classification core before wiring in real frame sources.

---

## Recommended roadmap

### Phase 1 — shared inputs
- Consume a neutral frame-source seam (`IFrameSource`, `CapturedFrame`) rather than vendor SDKs directly.
- Reuse shared metrics from `AstroAnalysis.Apps`:
  - star count
  - HFR/FWHM
  - eccentricity
  - background level

### Phase 2 — universal MVP
- Add `manual import` and `folder watcher` as the first usable inputs.
- Build a rolling session baseline from recent good frames.
- Surface the statuses:
  - `Stable`
  - `Degrading`
  - `Problem detected`

### Phase 3 — AstroFinder-specific framing confidence
- Add target/anchor field-match confidence.
- Messages should stay plain and actionable, for example:
  - `Fewer stars detected — possible cloud`
  - `Stars becoming larger — check focus`
  - `Field match confidence is low — framing may have drifted`

### Phase 4 — optional integrations
- Only after the file-based path proves valuable:
  1. Canon
  2. Nikon
  3. Alpaca / INDI
  4. Sony only if real demand appears

---

## Acceptance criteria
- No requirement for the phone camera to share the telescope or lens optical path.
- Same input metrics always produce the same trust classification.
- Folder-based workflows remain fully supported for non-brand-specific coverage.
- AstroFinder can eventually report both capture-health confidence and framing confidence in plain language.

---

## Next implementation step
Wire the new monitor to a real input path:
1. manual sample import, or
2. folder-watcher session feed.

That should happen before any brand-specific camera integration.