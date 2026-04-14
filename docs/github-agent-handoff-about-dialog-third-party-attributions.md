# Backlog Item: Pre-release audit for About dialog third-party attributions

## Suggested GitHub issue title
`chore(about): pre-release attribution audit for third-party libraries and license links`

## Why
Before release, AstroFinder must ensure all shipped third-party libraries are transparently credited in About, with working links to their license text.

## Scope
- Review all third-party packages that ship with AstroFinder.
- Verify every library appears in About attributions.
- Verify each attribution includes a valid project URL and a valid license URL.
- Verify links open correctly on supported targets.

## Required behavior
- Every third-party library included in the app distribution appears in About.
- Every listed library includes license information and a license link.
- No placeholder, broken, or empty attribution links remain.

## Implementation outline
1. Build a release-candidate package inventory from project dependencies.
2. Cross-check inventory against `IThirdPartyAttributionCatalog` entries used by About.
3. Add missing attributions and license links.
4. Validate link formatting and runtime navigation behavior from About.
5. Add/refresh tests for attribution completeness and required link fields.

## Acceptance criteria
- [ ] Attribution inventory matches shipped third-party dependency inventory.
- [ ] Every attribution has non-empty library name, license name, project URL, and license URL.
- [ ] About opens all attribution links successfully on the primary release target.
- [ ] Build and tests pass for AstroFinder.

## Release gating
- [ ] Mark this item complete before release candidate sign-off.
