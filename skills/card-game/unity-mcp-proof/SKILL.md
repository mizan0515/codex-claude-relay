# unity-mcp-proof

Use when a slice claims Unity verification, editor navigation, or QA state proof.

Procedure:
1. Prefer a single Unity MCP action that directly tests the claim.
2. Good first actions:
   - `read_console`
   - `execute_menu_item`
   - `manage_editor` play/stop when the QA path requires it
3. Confirm the relay evidence marker reflects actual Unity MCP use.

Required evidence:
- `[RELAY_EVIDENCE] unity_mcp=observed ...`

Failure rule:
- if Unity MCP was required but not observed, do not mark the slice complete.
