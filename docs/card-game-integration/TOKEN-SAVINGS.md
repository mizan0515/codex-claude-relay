# Token Savings

## Default rule

For `D:\Unity\card game`, do not read full relay JSONL logs during routine operation.

Read only:

- `relay-live-signal.json` / `relay-live-signal.txt`
- `relay-manager-signal.json` / `relay-manager-signal.txt`
- compact relay evidence for Unity MCP usage

## Why

Unity tasks already spend context on:

- large runtime files
- scoped `AGENTS.md`
- research files
- compile/test/QA evidence

Reading full relay logs on top of that is usually wasted budget.

## Cheap status path

1. Check manager signal.
2. Check relay signal.
3. If needed, check one compact evidence script.
4. Only if still blocked, open the smallest relevant event-log snippet.

## Prompt shape

- Keep the stable prefix fixed.
- Put the task-specific tail at the end.
- Reuse the same operator wording across sessions.
- Prefer route mode when the backlog slice is obviously `direct-codex` or docs-local.

## Unity-specific savings

- Prefer Unity MCP over broad source rereads when the real need is:
  - editor alive/dead check
  - console error check
  - QA menu execution
  - screenshot capture
  - one focused EditMode run
- Prefer `project.ps1 start-smart` or `start-lite` when the slice is `.autopilot` or docs-local.

## Official references

- OpenAI prompt caching:
  - [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching)
- OpenAI prompt guidance:
  - [Prompting](https://developers.openai.com/api/docs/guides/prompting)
