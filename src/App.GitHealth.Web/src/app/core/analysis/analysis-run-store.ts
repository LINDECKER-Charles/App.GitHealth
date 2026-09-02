import { Injectable, computed, signal } from '@angular/core';
import {
  AnalysisCommandTrace,
  AnalysisPhase,
  AnalysisReferenceProgress,
  AnalysisStatusResponse,
} from '../api/api.models';

/**
 * The API answers with the tail of the commands only. Keeping a few screens of them here
 * gives the console something to scroll back through without holding a whole run in memory.
 */
const retainedCommands = 240;

/**
 * A scan reaches its last stage and ends in the same breath. Holding the scene on that
 * frame is what lets the reader see the run close — and read "no Git write" one last time.
 */
const closingFrameMs = 900;

/**
 * What a running analysis is doing, assembled poll after poll. The API sends the ledger
 * whole every time and the commands as a moving tail: the ledger is replaced, the commands
 * are appended by rank, so nothing is shown twice and nothing already read disappears.
 */
@Injectable({ providedIn: 'root' })
export class AnalysisRunStore {
  readonly phase = signal<AnalysisPhase>('Waiting');
  readonly references = signal<readonly AnalysisReferenceProgress[]>([]);
  readonly commands = signal<readonly AnalysisCommandTrace[]>([]);
  readonly commandCount = signal(0);
  readonly startedAtMs = signal(0);

  /** Set by the reader: the run keeps going, only the scene steps aside. */
  readonly isCollapsed = signal(false);

  /** True while the scene holds its last frame, after the run itself has ended. */
  readonly isClosing = signal(false);

  private closing?: ReturnType<typeof setTimeout>;

  readonly total = computed(() => this.references().length);

  /** References the current stage is done with; the count restarts at each stage. */
  readonly processed = computed(() => this.references().filter(isProcessed).length);

  readonly reading = computed(() => this.references().filter(isReading));

  start(startedAtMs: number): void {
    clearTimeout(this.closing);
    this.phase.set('Waiting');
    this.references.set([]);
    this.commands.set([]);
    this.commandCount.set(0);
    this.startedAtMs.set(startedAtMs);
    this.isCollapsed.set(false);
    this.isClosing.set(false);
  }

  /** A status without progress is a run nobody follows any more: what was read stays. */
  apply(status: AnalysisStatusResponse): void {
    this.phase.set(status.phase);
    const progress = status.progress;
    if (progress === null) {
      return;
    }

    this.references.set(progress.references);
    this.commandCount.set(progress.commandCount);
    this.commands.update((known) => appendCommands(known, progress.commands));
  }

  /** The run has landed: hold the closing frame, then hand the tab back to the capture. */
  close(): void {
    clearTimeout(this.closing);
    this.isClosing.set(true);
    this.closing = setTimeout(() => this.isClosing.set(false), closingFrameMs);
  }

  /** A run that failed says so through the error callout, not through a last frame. */
  abandon(): void {
    clearTimeout(this.closing);
    this.isClosing.set(false);
  }

  collapse(): void {
    this.isCollapsed.set(true);
  }

  expand(): void {
    this.isCollapsed.set(false);
  }
}

/** Keeps the commands already shown and adds only those ranked after the last one seen. */
export function appendCommands(
  known: readonly AnalysisCommandTrace[],
  incoming: readonly AnalysisCommandTrace[],
): readonly AnalysisCommandTrace[] {
  const lastSeen = known.length === 0 ? 0 : known[known.length - 1].sequence;
  const fresh = incoming.filter((command) => command.sequence > lastSeen);
  if (fresh.length === 0) {
    return known;
  }

  const merged = known.concat(fresh);
  return merged.length <= retainedCommands ? merged : merged.slice(-retainedCommands);
}

function isReading(reference: AnalysisReferenceProgress): boolean {
  return reference.state === 'Measuring' || reference.state === 'Enriching';
}

function isProcessed(reference: AnalysisReferenceProgress): boolean {
  return reference.state === 'Measured' || reference.state === 'Read';
}
