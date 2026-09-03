import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { AnalysisRunNarration } from '../../core/analysis/analysis-run-narration';
import { AnalysisRunStore } from '../../core/analysis/analysis-run-store';
import { displayReference } from '../../core/branches/branch-labels';
import { DsButton } from '../../ui/core/ds-button';
import { DsSpinner } from '../../ui/core/ds-spinner';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { ProjectContext } from '../project/project-context';
import { AnalysisConsole } from './analysis-console';
import { AnalysisLedger } from './analysis-ledger';
import { AnalysisTopologyGraph } from './analysis-topology-graph';

/** Ten ticks a second: the elapsed counter shows tenths, so it has to move that often. */
const tickIntervalMs = 100;
const millisecondsPerSecond = 1000;

/**
 * What the repository is being asked, while it is being asked. The collapsed strip follows
 * the same run: this is the full reading of it, not a second source of truth.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AnalysisConsole,
    AnalysisLedger,
    AnalysisTopologyGraph,
    DsButton,
    DsSpinner,
    DsStatusDot,
  ],
  selector: 'app-analysis-run-scene',
  styleUrl: './analysis-run-scene.scss',
  templateUrl: './analysis-run-scene.html',
})
export class AnalysisRunScene {
  private readonly context = inject(ProjectContext);
  private readonly run = inject(AnalysisRunStore);
  private readonly now = signal(Date.now());

  protected readonly narration = inject(AnalysisRunNarration);
  protected readonly references = this.run.references;
  protected readonly commands = this.run.commands;
  protected readonly commandCount = this.run.commandCount;

  protected readonly elapsedLabel = computed(() => {
    const started = this.run.startedAtMs();
    const elapsed = started === 0 ? 0 : Math.max(0, this.now() - started);
    return `${(elapsed / millisecondsPerSecond).toFixed(1)} s`;
  });

  protected readonly baseline = computed(() => {
    const reference = this.context.baseline() ?? this.context.project()?.referenceName ?? null;
    return reference === null ? '' : displayReference(reference);
  });

  constructor() {
    // The counter stops with the run: the closing frame states a duration, not a stopwatch.
    const ticker = setInterval(() => {
      if (!this.run.isClosing()) {
        this.now.set(Date.now());
      }
    }, tickIntervalMs);
    inject(DestroyRef).onDestroy(() => clearInterval(ticker));
  }

  protected collapse(): void {
    this.run.collapse();
  }
}
