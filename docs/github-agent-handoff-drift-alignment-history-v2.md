# Backlog Item: AstroFinder v2 - deterministic drift alignment history

## Suggested GitHub issue title
`feat(v2): add deterministic drift alignment history and coaching to AstroFinder`

## Summary
AstroFinder v2 should persist drift alignment sessions and measurements so the app can turn repeated setup behavior into clear, deterministic coaching.

The feature should help users answer field questions such as:

> **"Do I usually start with the same rough polar alignment bias, and is my setup process becoming more repeatable over time?"**

This must be implemented as a deterministic history and insight feature. It should surface explainable metrics and trend-backed guidance, not gamified scores or opaque judgments.

---

## Product framing

### What this is
- A persistent history of drift alignment sessions, measurements, and correction steps.
- A deterministic coaching layer for rough polar alignment trends.
- A way to identify repeatable altitude or azimuth bias, time-to-threshold improvements, and probe instability.
- A feature whose outputs remain reproducible for the same stored sessions and configured thresholds.

### What this is not
- Not a gamified alignment score.
- Not vague `better/worse` messaging without a concrete metric.
- Not black-box recommendations or ML-based coaching.
- Not UI-owned business logic.

---

## Concise implementation summary
- Add a dedicated persisted history store for drift alignment sessions, measurements, and correction steps.
- Compute deterministic derived metrics such as total initial error, persistent sign bias, median time to acceptable alignment, per-axis rework count, and repeated-probe stability.
- Add a deterministic insights generator with explicit thresholds and suppression rules to avoid noisy guidance.
- Add a history list, session detail view, trend dashboard, and an optional dismissible new-session hint.
- Keep trend and insight logic in engine/domain layers; keep storage and UI wiring outside those cores.

---

## Schema changes

Add a dedicated history schema and migration path for the following entities.

### DriftAlignmentSession
- `id`
- `userId`
- `startedAtUtc`
- `completedAtUtc` nullable
- `siteLatDeg` nullable
- `siteLonDeg` nullable
- `hemisphere` nullable
- `mountType` nullable
- `sideOfPier` nullable
- `roughAlignmentMethod` nullable
- `initialAltErrorArcMin` nullable
- `initialAzErrorArcMin` nullable
- `finalAltErrorArcMin` nullable
- `finalAzErrorArcMin` nullable
- `finalTotalErrorArcMin` nullable
- `timeToAcceptableSec` nullable
- `totalDurationSec` nullable
- `acceptableThresholdArcMin` nullable
- `status` enum: `in_progress`, `completed`, `abandoned`
- `notes` nullable

### DriftMeasurement
- `id`
- `sessionId`
- `takenAtUtc`
- `measurementType` enum: `altitude_probe`, `azimuth_probe`
- `skyRegion` enum: `meridian_equator`, `east_equator`, `west_equator`, `custom`
- `raHours` nullable
- `decDeg` nullable
- `hourAngleHours` nullable
- `altitudeDeg` nullable
- `azimuthDeg` nullable
- `driftRateArcSecPerMin`
- `driftDirection` enum: `north`, `south`
- `inferredAxis` enum: `altitude`, `azimuth`
- `inferredErrorArcMin` nullable
- `confidence` number `0..1`
- `seeingQuality` nullable

### DriftCorrectionStep
- `id`
- `sessionId`
- `createdAtUtc`
- `axis` enum: `altitude`, `azimuth`
- `adjustmentDirection` enum: `increase`, `decrease`, `east`, `west`, `unknown`
- `adjustmentMagnitude` enum: `tiny`, `small`, `medium`, `large`, `unknown`
- `basedOnMeasurementId` nullable
- `beforeEstimatedErrorArcMin` nullable
- `afterEstimatedErrorArcMin` nullable

### Migration and indexing notes
- Use a dedicated local history store with explicit versioned migrations rather than extending `Preferences` storage.
- Index sessions by `userId`, `status`, and `startedAtUtc`.
- Index measurements by `sessionId` and `takenAtUtc`.
- Index corrections by `sessionId` and `createdAtUtc`.
- Keep the schema ready for future site-specific and equipment-specific filtering without breaking the v1 field contract.

---

## Service and API changes

### Engine
- Add a deterministic `DriftAlignmentMetricsCalculator` for:
  - `initialTotalErrorArcMin = sqrt(initialAltErrorArcMin^2 + initialAzErrorArcMin^2)` when both values exist
  - trend metrics over completed sessions
  - persistent sign bias by axis
  - median `timeToAcceptableSec`
  - correction counts per axis
  - repeated-probe stability and variance
- Add a deterministic `DriftAlignmentInsightsGenerator` with configurable thresholds.
- Add typed options for thresholds so behavior is explicit and testable.

### Domain
- Add query models and filter objects for session history windows, site filters, and completion-state filters.
- Add an orchestration service that assembles session summaries, trend dashboard data, and detail timelines from persisted records plus engine-derived metrics.
- Add a lightweight contextual-hint selector that returns at most one dismissible hint for a new session.

### App and persistence
- Add repository/store interfaces for sessions, measurements, and correction steps.
- Provide a local persistence implementation with migrations.
- Wire the drift alignment flow to create `in_progress` sessions, append measurements/corrections as events occur, and mark sessions `completed` or `abandoned` deterministically.
- Expose read models for:
  - history list rows
  - session detail summary and timeline
  - trend dashboard tiles and charts
  - new-session hint text

### Suggested thresholds
- Bias insight only if at least 5 completed sessions exist, the same sign appears on an axis in at least 70 percent of sessions, and median absolute axis error exceeds 5 arcmin.
- Improvement insight only if at least 6 completed sessions exist and the recent 3-session median improves over the previous 3-session median by at least 15 percent.
- Efficiency insight only if at least 5 sessions contain valid completion times.
- Stability insight only if repeated measurements consistently exceed a configured variance threshold.

---

## UI changes

### History list screen
- Reachable from the drift alignment flow.
- Show completed sessions and optionally `in_progress` sessions.
- Each row should include:
  - date and time
  - optional site or location label when a site can be resolved; otherwise blank or coordinates-derived text
  - initial total error
  - final total error
  - time to acceptable threshold
  - a compact badge for any high-confidence detected pattern
- Add filter controls for:
  - all vs completed
  - site
  - recent window such as `7`, `30`, or `90` sessions

### Session detail screen
- Show a summary card with:
  - initial altitude and azimuth errors
  - final residuals
  - time to threshold
  - number of corrections
- Show an event timeline of measurements and correction steps.
- Show charts for:
  - error vs time
  - altitude-vs-azimuth error evolution
- Show an insights panel with only high-confidence deterministic insights.

### Trend dashboard
- Show:
  - initial alignment trend
  - persistent bias
  - median time to threshold
  - axis causing the most rework
- Recommended charts:
  - initial total error by session
  - time to acceptable threshold by session
  - initial altitude vs azimuth split

### New-session contextual hint
- At the start of a new drift session, optionally show one dismissible hint derived from history.
- Example: `Recent sessions often start with a small eastward azimuth bias.`
- This must never block the workflow.

---

## Architecture constraints
- Keep derived metric and insight generation in deterministic engine or domain layers, not in the UI.
- Keep outputs reproducible for the same persisted inputs and configured thresholds.
- Keep thresholds configurable and named.
- Keep storage concerns in app infrastructure or persistence adapters, not in deterministic core layers.
- Design read models and filters so future site-specific and equipment-specific filtering can be added without rewriting the core metric logic.

---

## Tests to add
- `initialTotalErrorArcMin` calculations.
- Persistent bias detection.
- Improvement detection.
- Suppression of insights when data is insufficient.
- Stability insight gating when repeated-probe variance does or does not exceed thresholds.
- UI rendering for sessions with missing or partial fields.
- Stable handling of `abandoned` and `in_progress` sessions.
- Timeline ordering and summary-card counts for mixed measurement and correction events.

---

## Acceptance criteria
- Same session history and threshold configuration always produce the same insights.
- The app can persist and reload `in_progress`, `completed`, and `abandoned` sessions.
- History screens tolerate partial data without crashing or inventing values.
- High-confidence insights are actionable, metric-backed, and non-judgmental.
- No score, rating, or opaque quality number is introduced.
- A new-session hint is optional, dismissible, and never blocks the drift workflow.

---

## Notable tradeoffs
- A dedicated history store adds migration work, but it is the correct foundation for session timelines and trend queries; `Preferences` storage is too limited for this feature.
- Persisting both raw event history and derived summaries increases storage volume slightly, but it keeps the feature explainable and allows deterministic re-computation when thresholds change.
- Showing only high-confidence insights will suppress some potentially interesting observations, but that is preferable to noisy or misleading coaching.
- Site labels should be resolved from existing site context when possible rather than stored as the primary source of truth in this first slice.

---

## Assumptions
- AstroFinder already has, or will have in v2, a drift alignment workflow that can emit measurements and correction events.
- Shared drift-measurement primitives from `AstroAnalysis.Apps` can continue to inform probe selection or measurement suitability, while history persistence and coaching remain AstroFinder-owned.
- The first implementation should prioritize local offline persistence and deterministic queries rather than cloud sync or cross-device history merge.
- If a measurement lacks enough data to infer axis error confidently, the record should still persist and simply remain excluded from metrics that require that field.

---

## Recommended implementation order
1. Add schema, migrations, repositories, and write-path wiring from the drift session flow.
2. Add deterministic metrics and insights services with threshold-focused tests.
3. Add history list and session detail screens using read models that tolerate missing fields.
4. Add the trend dashboard and optional new-session hint after core history rendering is stable.