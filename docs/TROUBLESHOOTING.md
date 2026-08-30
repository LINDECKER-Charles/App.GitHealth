# Troubleshooting

## The browser does not open

The application may be perfectly functional even when the automatic launch fails. Copy
the `http://127.0.0.1:<port>` address printed in the console. The `--no-browser` option
disables that launch on purpose.

## The requested port is unavailable

Free the port, pick another one with `--port`, or omit the option and let the system
select an available port. GitHealth refuses to start rather than silently falling back
to a network interface.

## Another instance is using the database

Only one instance can write to a given data directory. Close the other process, or start
the new instance with a different `--data-dir`. Only delete the
`githealth.db.instance.lock` file after checking that no GitHealth process is using that
database.

## Git cannot be found

Install Git, check that `git --version` works in a new terminal, then restart GitHealth.
The `/health` diagnostic stays available and describes this error.

## The repository is rejected or unreachable

- use an absolute path and check the read permissions;
- under Docker, use a path below `/repositories`;
- check that the mounted folder really contains the expected repository;
- for a linked worktree, keep the main repository reachable;
- avoid a symbolic link that leaves the container's allowed root.

A project that has become unreachable keeps its last successful snapshot available for
reading. Open **Policies**, then **Relocate repository** to attach its new path without
losing the analyses. The reference already configured and the last known baseline commit
must exist in that repository.

## The project is busy during a relocation

GitHealth refuses to relocate a project while it is being analysed, and refuses to start
an analysis while it is being relocated. Wait for the operation to finish, then try
again. This lock prevents a snapshot from being attached to the old path after the move.

## The new path does not match the known repository

The `repository.identity_mismatch` code means the candidate does not contain the
baseline commit of the last successful snapshot. Select another copy of the same
repository, or restore that commit before relocating; never attach a history to an
unrelated repository.

## The baseline or a branch is missing

GitHealth runs neither `fetch` nor `remote prune`. Update the repository deliberately
with your usual tools, then run the analysis again. Remote-tracking branches are the
references present locally under `refs/remotes`.

## The analysis exceeds a limit

A Git command that is too slow, too verbose, or a saturated queue is stopped with an
explicit error. Check the repository's integrity with the Git tools, narrow the branch
scope, then try again. These limits prevent a hostile repository from monopolising the
machine; their configuration is described in [DEVOPS.md](DEVOPS.md).

## The last result does not change after a failure

This is the expected behaviour. Persistence is transactional: only successful scans
replace the last snapshot. The failure stays visible in the history.

After an abrupt shutdown, an analysis left running appears as cancelled with the
`analysis.interrupted` code on the next startup. It can be restarted normally.

## The CSV export opens badly in a spreadsheet

Import the file as UTF-8 and choose the comma as separator. Cells that start like a
formula are neutralised on purpose, to prevent the spreadsheet from executing them.

## Restoring a SQLite backup

1. Stop every GitHealth instance pointing at the database.
2. Copy the current database to a safe location.
3. Replace `githealth.db` with the exported file.
4. Restart GitHealth and check `/health`.

Do not replace the database while the application is running. The export produced by
GitHealth is self-contained and does not need the SQLite `-wal` or `-shm` files.

## Docker does not start

Run `docker compose config`, check the value of `GITHEALTH_REPOSITORIES_ROOT`, then read
`docker compose logs githealth`. The data volume must stay writable, while the
`/repositories` mount must stay read-only.

## macOS blocks the executable

The published archives are neither signed nor notarised. Verify the archive and its
checksum, then explicitly allow the first launch from the security settings. Signing and
notarisation are planned before any wide distribution.
