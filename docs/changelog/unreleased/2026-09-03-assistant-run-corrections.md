# A run refused for the right reason, and an agent allowed to exit first

- **Type** — `fix`, `test`
- **Scope** — `api`
- **Landed** — 2026-09-03
- **Commits** — `cf703f7`, `e981bfb`, `991e59e`

## What shipped

`POST /api/projects/{id}/assistant/runs` refuses an effort no agent declares with `400` and
`assistant.effort_unsupported`, on every machine. `AssistantRunService.Accept` judges the
request against the catalog alone — the installation switch, a non-empty question, a known
agent identifier — the effort is checked against that agent's declared levels, and only
then does `LocateAsync` look for an executable. The agent used to be located first, so the
same request was answered `503 assistant.agent_unavailable`, "Claude Code is not available
on this machine", wherever the CLI was missing.

An agent that exits before reading its prompt now ends its run rather than throwing.
`AgentProcessRunner.WritePromptAsync` already tolerated a broken pipe on the write; closing
the writer flushes what the write could not, so it failed the same way one call later, from
the `finally` and outside the catch. That `IOException` escaped before the output pumps were
awaited, so whatever the agent had printed was never read back and the run failed carrying
the pipe error.

A bridge token nobody ever issued was spelt as thirty-two hex characters in three test
files. It reads `not-a-real-token-never-issued`.

## Why

**The catalog declares the levels, and needs no CLI installed to do it.** A request naming
an effort that does not exist is wrong whatever the machine holds, and a refusal blaming the
environment sends the caller hunting for a missing install rather than at the field they got
wrong. `AnEffortTheAgentDoesNotAcceptIsRefused` runs against the real availability service,
so it passed where Claude Code was installed and failed where it was not; widening it to
accept either answer would only have written the machine into the contract.

**This supersedes a sentence in [asking a local agent about a
capture](2026-09-02-local-agent-assistant.md).** It says of the effort ladder that *"it is
allowlisted against what the agent declares; an unsupported one is refused rather than
downgraded"*. The refusal is unchanged. What the sentence described, against the code as it
then stood, was an allowlist read off the agent that had just been located, so it only
applied where the CLI was present. The allowlist is the catalog now, and that is the whole
point of the fix.

**An agent is free to exit before reading its prompt, and its own output says why.** The
prompt is lost either way and the run is read from what the process printed, so a broken
pipe is a thing to ignore, not a way to fail. It reproduces where the child closes its end
before the parent's last write lands — common on Linux, rare on macOS: the narration test
launches a script that prints a recording and never reads its input, and it passed locally
while failing on the runner.

**A fake secret that looks real is still a finding.** Thirty-two hex characters are what a
scanner is built to flag, and the point of the constant is that it names a token the
registry never handed out.

**Three commits, one entry, dated for themselves.** The journal dates a file with the day
its last commit landed. These landed on the 3rd, the assistant entries are dated the 2nd,
and folding them in would either misdate those files or force renames that
[`unreleased/README.md`](README.md) links by name. [Dependency updates and two flaky tests
closed](../0.1.0/2026-08-30-toolchain-and-test-stability.md) is the precedent for grouping
small corrections to one implementation.

## Consequences

**Nothing a released user saw was broken.** Both defects belong to code that landed inside
this same unreleased window — the effort check with `8dca00b`, the process runner with
`ae7fbfe`, neither of them in `v0.1.0`. That is why none of this appears under **Fixed** in
`CHANGELOG.md`.
