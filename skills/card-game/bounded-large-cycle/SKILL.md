# bounded-large-cycle

Use when validating autopilot -> relay -> Desktop loops or any real multi-turn
session that could waste tokens or leave an operator waiting.

Procedure:
1. Run bounded sessions only.
2. Read compact manager/relay/evidence artifacts first.
3. Stop on stale, hung, paused-error, or approval-required states.
4. Convert new failure modes into deterministic guards and compact markers.

Preferred artifacts:
- `relay-manager-signal.json/.txt`
- `relay-live-signal.json/.txt`
- `Get-CardGameRelayEvidence.ps1`

Forbidden:
- blind log tailing
- open-ended waiting without a timeout or signal stop rule
