# Watching the agent work

- **Type** — `feat`
- **Scope** — `api`, `front`, `docs`
- **Landed** — 2026-09-03
- **Commits** — to be filled by the commit carrying this entry

## What shipped

A run of the assistant now says what it is doing while it does it. Where the panel showed a
spinner and a line of text for as long as a question took — often a minute, sometimes three —
it now shows the steps as the agent reaches them:

```
✓ Asking the model
✓ Thinking
✓ Reading the capture
✓ Reading the branches   verdict=cleanup candidate, take=50
✓ Counting the branches  groupBy=author
⟳ Writing
```

The step in progress carries a spinner, the ones above it a tick, and a tool call carries the
arguments the agent chose — which is the interesting half of a call: "reading the branches"
says far less than the filter it read them with. The elapsed time sits next to **Stop**, and
the answer appears underneath as it is written.

**The steps are shown and never stored.** They disappear with the run they describe. A
conversation reopened an hour later holds the question and the answer, as before; nothing in
the database, the backup or the exported history knows what the agent did to get there.

Both CLIs are now launched in their own JSON mode — `--output-format stream-json --verbose
--include-partial-messages` for Claude Code, `--json` for Codex CLI — and
`GET /api/assistant/runs/{id}` carries a `steps` array beside the answer: a kind (`Waiting`,
`Thinking`, `Tool`, `Writing`), the tool called, what it asked for, and when.

`GitHealth:Assistant:MaximumOutputBytes` rises from 512 KiB to 4 MiB.

## Why

**A progress bar that does not move is worse than no progress bar.** The feature's whole cost
to the user is the wait, and the wait was unexplained: nothing said whether the agent was
thinking, reading, blocked or about to fail. The information existed the entire time — both
CLIs narrate themselves — and GitHealth was asking them for their human log and showing it
raw, which for Claude Code meant showing nothing at all until the answer arrived.

**Asking for JSON rather than parsing prose.** `--output-format text` prints one thing at the
end; `stream-json` prints every turn as it happens. The same change makes the answer better
defined rather than worse: it now comes from the event that states it as the final result,
instead of being whatever the process happened to leave on standard output. What the agent
wrote is still kept, and is what is read back when a run is stopped short — but only then, so
a run that failed halfway reads as a failure rather than as an answer made of the first thing
it said.

**Two readers, one vocabulary.** Claude Code streams Anthropic's messages, blocks and deltas;
Codex announces whole thread items. Each has a reader of its own, and both produce the same
four kinds of step, because the panel has to phrase them in the interface's language — a step
the interface cannot name is a step it cannot show. Adding a third agent means adding a
reader, not touching anything downstream.

**Reasoning is reported, not invented.** Claude Code streams the *length* of a thinking block
and never its text — the content is encrypted and the CLI does not publish it. So a thinking
step says that the model is thinking and stops there. Codex publishes a summary when the
model produces one, and that summary is shown. Neither is paraphrased into something the
agent did not say.

**The steps are sent whole on every poll, not since an offset like the answer is.** The list
is bounded — the same activity twice running is folded into one, and it stops at 200 — so a
poll costs a few kilobytes on loopback, and stating the list rather than accumulating it
means a poll that is retried or dropped cannot duplicate or lose a step.

**Standard output is no longer retained.** It used to be accumulated in full so the answer
could be scraped out of it; now it is read as it arrives and only the steps and the answer
are kept. That is what makes the larger budget affordable — and the larger budget is
necessary, because the stream now carries the whole exchange, tool results included, where it
used to carry an answer.

## Consequences

**`MaximumOutputBytes` means something else.** It bounded what an agent printed as an answer;
it now bounds a whole exchange — every event and every capture row a tool call sends back
through the stream. An installation that pinned it in `appsettings.json` should raise it: at
512 KiB a run reading a large capture would be stopped mid-answer.

**A CLI that changes its event format degrades the narration, not the run.** Every field is
read defensively and an unrecognised line means nothing, so a version that renames something
loses steps and keeps answering. The recorded output of both installed CLIs is pinned in the
tests, which is what would catch it.
