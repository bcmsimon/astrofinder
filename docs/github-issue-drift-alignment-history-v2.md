# feat(v2): add deterministic drift alignment history and coaching to AstroFinder

## Summary
Add a deterministic Drift Alignment History feature to AstroFinder so prior drift sessions become useful, explainable coaching instead of disposable one-off runs.

The feature should persist sessions, measurements, and correction steps, then use that history to surface clear guidance such as:
- persistent initial altitude or azimuth bias
- improvement in rough alignment over time
- reduction in time to acceptable alignment
- repeated instability in certain probe types

This must not introduce a gamified score, vague "better/worse" messaging, or opaque recommendations.

## Why
AstroFinder favors deterministic, explainable guidance over black-box behavior. Drift alignment history fits that model if the app only reports trend-backed, actionable observations that can be reproduced from stored session data and configured thresholds.

Users should be able to answer practical questions such as:
- Do I usually start with the same azimuth or altitude bias?
- Is my rough alignment process becoming more repeatable?
- Which axis usually needs the most rework?
- Are some probe types giving unstable repeated measurements?

## Scope

### Persistent data model
Add persistent storage and migrations for:

#### DriftAlignmentSession
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

#### DriftMeasurement
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

#### DriftCorrectionStep
- `id`
- `sessionId`
- `createdAtUtc`
- `axis` enum: `altitude`, `azimuth`
- `adjustmentDirection` enum: `increase`, `decrease`, `east`, `west`, `unknown`
- `adjustmentMagnitude` enum: `tiny`, `small`, `medium`, `large`, `unknown`
- `basedOnMeasurementId` nullable
- `beforeEstimatedErrorArcMin` nullable
- `afterEstimatedErrorArcMin` nullable

### Deterministic metrics and insights
Implement deterministic derived metrics only.

Compute:
- `initialTotalErrorArcMin = sqrt(initialAltErrorArcMin^2 + initialAzErrorArcMin^2)` when both values exist
- trend metrics over completed sessions
- persistent sign bias by axis
- median `timeToAcceptableSec`
- corrections per axis
- measurement stability or variance where repeated probes exist

Add an insights generator with explicit thresholds to suppress noise.

Suggested thresholds:
- Bias insight only if at least 5 completed sessions exist, the same sign appears on an axis in at least 70 percent of sessions, and median absolute axis error exceeds 5 arcmin
- Improvement insight only if at least 6 completed sessions exist and the recent 3-session median improves over the previous 3-session median by at least 15 percent
- Efficiency insight only if at least 5 sessions contain valid completion times
- Stability insight only if repeated measurements consistently exceed a configured variance threshold

Insight principles:
- deterministic
- explainable
- actionable
- non-judgmental
- no opaque scoring

Example guidance to allow:
- `Your initial azimuth alignment usually starts slightly east of the pole.`
- `Recent rough alignment has improved over your earlier average.`
- `You typically need more corrections on azimuth than altitude.`

Example guidance to avoid:
- `You did worse than last time.`
- `Alignment score 82.`
- `AI thinks your setup is poor.`

### UI
Add a new history UI reachable from the drift alignment flow.

#### History list screen
Show completed sessions and optionally in-progress sessions.

Each row should include:
- date and time
- optional site or location label if available
- initial total error
- final total error
- time to acceptable threshold
- compact badge for any high-confidence detected pattern

Include filter controls for:
- all vs completed
- site
- recent window such as 7, 30, or 90 sessions

#### Session detail screen
Show:
- summary card with initial altitude and azimuth errors, final residuals, time to threshold, and number of corrections
- event timeline of measurements and corrections
- charts for error vs time and altitude-vs-azimuth error evolution
- insights panel with only high-confidence deterministic insights

#### Trend dashboard
Show:
- initial alignment trend
- persistent bias
- median time to threshold
- axis causing the most rework

Recommended charts:
- initial total error by session
- time to acceptable threshold by session
- initial altitude vs azimuth split

#### New-session contextual hint
At the start of a new drift session, optionally show one dismissible hint derived from history, for example:
- `Recent sessions often start with a small eastward azimuth bias.`

This must never block the workflow.

## Architecture constraints
- Keep core trend and insight generation in deterministic engine or service layers, not UI.
- Keep outputs reproducible for the same inputs.
- No black-box ML or probabilistic recommendation system.
- Make thresholds configurable.
- Design for future site-specific and equipment-specific filtering.

## Deliverables
- schema and migrations
- backend persistence layer
- deterministic trend and insight service
- UI screens or components for history list, detail view, and trend dashboard
- wiring into the existing drift alignment workflow
- tests for derived metrics and insight threshold behavior

## Tests
Add coverage for:
- `initialTotalErrorArcMin` calculations
- persistent bias detection
- improvement detection
- suppression of insights when data is insufficient
- UI rendering for sessions with partial or missing fields
- stable handling of abandoned and in-progress sessions

## Acceptance criteria
- [ ] Drift alignment sessions, measurements, and correction steps are persisted and can be reloaded.
- [ ] `in_progress`, `completed`, and `abandoned` sessions are handled cleanly.
- [ ] Initial total error is derived deterministically when both axis values exist.
- [ ] Bias, improvement, efficiency, and stability insights only appear when thresholds are satisfied.
- [ ] Insights are plain-language, actionable, and non-judgmental.
- [ ] No score, rating, or opaque quality number is introduced.
- [ ] History list, session detail, and trend dashboard tolerate partial data without inventing values.
- [ ] A new-session hint is optional, dismissible, and never blocks the workflow.
- [ ] Tests cover metric derivation, threshold gating, and partial-data UI behavior.

## Notable tradeoffs
- A dedicated local history store adds migration work, but it is the right foundation for timeline and trend queries.
- Strict confidence thresholds will suppress some observations, but that is preferable to noisy or misleading coaching.
- Persisting raw session events plus derived summaries increases storage slightly, but preserves explainability and allows deterministic recomputation.

## Assumptions
- AstroFinder has, or will gain in v2, a drift alignment workflow that can emit measurements and correction events.
- Local offline persistence is the first implementation slice.
- Shared drift-measurement primitives can still come from `AstroAnalysis.Apps`, while AstroFinder owns history persistence and coaching behavior.