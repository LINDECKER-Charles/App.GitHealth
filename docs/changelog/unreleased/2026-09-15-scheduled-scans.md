# Scanning a repository on a schedule

- **Type** — `feat`
- **Scope** — `core`, `api`, `front`, `docs`
- **Landed** — 2026-09-15
- **Commits** — `a68001f`, `6681eae`, `6411bb5`

## What shipped

A repository can now re-measure itself on a cron expression instead of waiting to be asked.
The **Scheduled scan** panel on the Policies screen carries a switch, a rhythm picker — every
15 minutes, hourly, every 4 hours, daily, weekdays, Mondays — and the cron expression itself,
which stays visible and editable whichever rhythm is picked. The panel states which clock the
hour fields are counted on, when the schedule last fired and when it will fire next.

Two routes back it:

- `GET /api/projects/{projectId}/schedule` — the switch, the expression, the last firing and
  the next one, computed at the moment of the read;
- `PUT /api/projects/{projectId}/schedule` — `{ isEnabled, cronExpression }`, answering
  `400 schedule.invalid` for anything the scheduler could not have read.

A firing goes through `AnalysisLaunchService`, the same door as the analysis button: every
baseline of the project is measured, the queue is honoured, and a project reserved by a
relocation is refused on the same terms.

Expressions are the five classic fields — minute, hour, day of month, month, day of week —
with `*`, `5`, `1-5`, `*/15`, `1-5/2`, `9/2` and comma-separated lists. Sunday is 0 or 7.
Two restricted day fields combine with "or", as every cron does. Names such as `MON` are
refused: the interface writes numbers, and a form that takes two spellings has to teach both.

Three settings govern the installation: `GitHealth:Schedule:Enabled` turns scheduled scanning
off outright, `TickSeconds` sets how often due schedules are looked for, and `TimeZone` names
the clock the expressions are read on.

## Why

GitHealth's readings go stale on their own: branches move while nobody is looking, and a
capture from last Tuesday classifies a branch as active that has been abandoned since. Asking
for a scan by hand is exactly the thing a reader forgets to do, and forgets in the direction
that makes the dashboard lie.

**Cron rather than an interval.** "Every 4 hours" and "weekdays at nine" are both schedules,
but only the second one is a rhythm anyone actually wants for a repository they read in the
morning. An interval cannot express it. Cron can express both, and the picker keeps the
common cases to one click.

**Parsed here rather than pulled in.** `App.GitHealth.Core` has no package reference at all,
and that is deliberate: the branch classification, the Git output parsing and the divergence
arithmetic are all written out. A five-field parser with a next-occurrence search is around
two hundred lines and fully testable, against a NuGet dependency to carry, to audit and to
list in `THIRD-PARTY-NOTICES.md` for the same result.

**Local wall clock, not UTC.** A reader who writes `0 9 * * *` means nine in the morning where
they are. Reading the expression in UTC would be wrong for most of the world and wrong twice a
year by an extra hour for the rest. `CronExpression.GetNextOccurrence` therefore takes the zone
and searches on its wall clock: a time the clock skipped forward over still fires, pushed to
the moment the clock resumed, and a wall time replayed when the clock goes back does not fire
a second time.

**The anchor is the later of the last firing and the last edit**, which is the whole subtlety
of the feature and is why both dates are stored:

- counting from the *edit* is what stops **Save** launching a scan on the spot. Writing "every
  minute" at 12:00:30 waits for 12:01 like everyone else;
- counting from the *firing* is what makes a window missed while GitHealth was closed fire
  **once** when it opens, rather than once per window gone by. Three days offline on an hourly
  schedule is one scan, not seventy-two.

**The firing is recorded before the launch, never after.** A launch that fails would otherwise
leave the window open, and the next tick would try again seconds later, and the one after
that. A repository that cannot be read must go quiet, not loud.

**The switch and the expression are stored apart.** Turning a schedule off for a fortnight
must not cost the reader the expression they worked out, and turning it back on must not ask
them to write it again. A single nullable column would have made the switch a delete button
wearing a disguise.

## Consequences

- **A migration adds four columns to `Projects`** — `IsScheduleEnabled`, `ScheduleCron`,
  `ScheduleChangedAtUtc`, `ScheduleLastRunAtUtc` — and an index on the first. Nothing is
  backfilled: every project already stored keeps no schedule, which is what it had.
- **A schedule only fires while GitHealth is running.** That is not a limitation to work
  around but the shape of the product: it is a local application, not a service, and it
  installs nothing that outlives its own window. The panel says so, and a missed window fires
  once at the next start.
- **A screen already open does not refresh itself when a scheduled scan lands.** The new
  capture is read the next time the repository is opened or the baseline switched. Following
  a run nobody asked for would mean polling from every screen; it was left out rather than
  half-built.
- **Nothing caps how often a schedule may fire.** `* * * * *` is accepted, and on a large
  repository that is a real load. The queue's duplicate guard stops runs piling up on the same
  baseline, and the retention setting bounds what the history keeps; beyond that, the rhythm
  is the reader's to choose.
