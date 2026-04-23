# compact-signal-operator

Use whenever the operator is a non-developer or when token efficiency matters.

Procedure:
1. Treat compact signal files as the source of operational truth.
2. Surface one short state, one next action, and whether human attention is required.
3. Prefer Desktop-visible actions over PowerShell instructions where possible.

Canonical artifacts:
- `scripts/card-game/Get-CardGameManagerSignal.ps1`
- `scripts/card-game/Get-CardGameRelaySignal.ps1`
- `scripts/card-game/Wait-CardGameRelaySignal.ps1`

Output contract:
- status must be understandable without reading raw logs
- next action must be one of `prepare`, `run`, `complete`, `blocked`, or `wait`
