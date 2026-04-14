# Backlog Item: Make mount selection mandatory in AstroFinder

## Suggested GitHub issue title
`feat(mount): require explicit mount selection before RA/Dec adjustment guidance`

## Why
RA/Dec guidance is currently hardcoded to Star Adventurer behavior. To avoid incorrect instructions, the app must require mount selection and derive adjustment guidance from the selected mount profile.

## Scope
- Add mandatory mount selection to the main workflow.
- Block RA/Dec guidance display until a mount is selected.
- Pull guidance from profile data instead of hardcoded text.

## Required behavior
- User cannot view adjustment guidance without selecting a mount.
- Mount selection UI is clear and persisted.
- Guidance text changes based on mount profile.
- If mount profile is missing required guidance fields, show a clear validation/error state.

## Implementation outline
1. Add mount selection UI on main screen (or as required pre-step).
2. Store selected mount in view model state and app settings.
3. Gate `ShowDeltasCommand` guidance output by mount selection.
4. Replace hardcoded Star Adventurer text with profile-driven guidance.
5. Add tests for:
   - command gating when no mount selected
   - profile-specific guidance rendering
   - persistence/restore of selected mount

## Acceptance criteria
- [ ] No mount selected: guidance cannot be shown and user sees a prompt.
- [ ] Mount selected: flyout shows deltas + correct mount-specific guidance.
- [ ] Build and tests pass for AstroFinder.

## Phase 1 Follow-Up (Temporary Behavior Cleanup)
- [ ] Replace temporary always-enabled `Manual Goto` button behavior in the RA/Dec flyout.
- [ ] Enable `Manual Goto` only when selected mount profile is Star Adventurer 2i.
- [ ] Add profile-driven manual-goto instruction templates for additional built-in mount profiles.
- [ ] Add tests that verify button visibility/enabled state per selected mount profile.
