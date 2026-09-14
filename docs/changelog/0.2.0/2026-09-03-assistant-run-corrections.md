# An effort refused the same way on every machine, and an agent free to exit early

- **Type** — `fix`, `test`
- **Scope** — `api`
- **Landed** — 2026-09-03
- **Commits** — `cf703f7`, `e981bfb`, `991e59e`

## What shipped

`POST /api/projects/{id}/assistant/runs` refuses an effort no agent declares with `400` and
`assistant.effort_unsupported`, on every machine where the assistant is enabled.
`AssistantRunService.Accept` now judges the request against the catalogue alone — the
installation switch, a non-empty question, a known agent identifier — and returns the
`AgentDefinition`; `ResolveEffort` checks the effort against that definition's declared
levels; only then does `LocateAsync` look for an executable. The agent used to be located
first, so the same malformed request was answered `503 assistant.agent_unavailable`,
"Claude Code is not available on this machine", wherever the CLI happened to be missing.

An agent that exits before reading its prompt now ends its run rather than throwing.
`AgentProcessRunner.WritePromptAsync` already tolerated a broken pipe on the write; closing
the writer flushes what the write could not, so it failed the same way one call later, from
the `finally` and outside the catch. The escaping `IOException` meant the run never reached
its outcome — no exit code, no buffered standard error, nothing the output pumps had not
already drained — and the run failed carrying the pipe error as its message. The close now
fails in the same silence as the write.

A bridge token nobody ever issued was spelt as thirty-two hex characters in
`AgentCommandLineTests.cs`, `AssistantBridgeEndpointTests.cs` and
`AssistantMcpSessionRegistryTests.cs`. It reads `not-a-real-token-never-issued`.

## Why

**The catalogue declares the levels, and needs no CLI installed to do it.** A request
naming an effort that does not exist is wrong whatever the machine holds, and an answer
blaming the environment sends the caller hunting for a missing install rather than at the
field they got wrong. The allowlist itself did not move — `AgentEffort.IsSupported` read
the agent's declared levels before and reads them now. What changed is the order: the
catalogue is reached without the CLI having to be found first.
`AnEffortTheAgentDoesNotAcceptIsRefused` runs against the real availability service, so it
passed where Claude Code was installed and failed where it was not. Widening it to accept
either answer was the cheaper repair, and would have written the machine into the contract.

**This supersedes a sentence in [asking a local agent about a
capture](2026-09-02-local-agent-assistant.md).** That entry says of the effort ladder:
*"it is allowlisted against what the agent declares; an unsupported one is refused rather
than downgraded"*. The refusal is unchanged and the reason for it holds. What the sentence
described, against the code as it then stood, was an allowlist read off the agent that had
just been located — so the refusal only happened where the CLI was present. It no longer
depends on that.

**An agent is free to exit before reading its prompt, and its own output says why.** The
prompt is lost either way and the run is read from what the process printed, so a broken
pipe is a thing to ignore rather than a way to fail. It reproduces wherever the child closes
its end before the parent's last write lands — the common case on Linux, not on macOS, which
is why the narration test passed locally and failed on the runner. That test launches a
script printing a recording and exiting without reading standard input.

**A fake secret shaped like a real one costs something.** Thirty-two hex characters are
what a secret scanner is built to flag, and the constant exists precisely to name a token
the registry never handed out. Spelling it as words keeps the test readable and stops it
being reported.

**One entry for three commits.** They land on the same day and correct the same feature,
but they belong to two implementations documented elsewhere — the effort check came in with
`8dca00b`, the process runner with `ae7fbfe`. Neither is an ancestor of `v0.1.0`, so nothing
a released user ever saw was broken, and none of this produces a line under **Fixed** in
`CHANGELOG.md`. The journal still records it, because the journal records implementations
and their reasoning rather than user-facing news. Grouping several small corrections into a
single entry is the shape
[dependency updates and two flaky tests closed](../0.1.0/2026-08-30-toolchain-and-test-stability.md)
already uses.
