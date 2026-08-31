import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { RecommendationKind } from '../../core/api/api.models';
import {
  displayReference,
  recommendationLabels,
  recommendationTones,
} from '../../core/branches/branch-labels';
import { appVersion } from '../../core/workspace/app-identity';
import { ProjectContext } from '../../features/project/project-context';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { Tone } from '../../ui/icon-name';
import { bootCompleteMs, bootSteps, counterDurationMs, counterStartMs } from './boot-sequence';

interface BootTile {
  readonly label: string;
  readonly tone: Tone;
  readonly count: number;
  readonly share: number;
}

const tileOrder: readonly RecommendationKind[] = ['Keep', 'Review', 'CleanupCandidate', 'Excluded'];

/**
 * Opening sequence. It describes the real steps of the analysis and shows only measured
 * figures: the counters climb towards those of the snapshot loading behind it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsBadge, DsIcon, DsStatusDot],
  selector: 'app-boot-intro',
  styleUrls: ['./boot-intro.scss', './boot-graph.scss'],
  templateUrl: './boot-intro.html',
})
export class BootIntro {
  private readonly context = inject(ProjectContext);
  private frame = 0;
  private timer?: ReturnType<typeof setTimeout>;

  /** Natural end of the sequence. */
  readonly done = output<void>();

  /** The user cut it short: the sequence will not play again in this session. */
  readonly skipped = output<void>();

  protected readonly steps = bootSteps;
  protected readonly version = appVersion;
  protected readonly progress = signal(0);

  protected readonly referenceLabel = computed(() => {
    const snapshot = this.context.snapshot();
    return snapshot === null
      ? $localize`:@@boot.reference.fallback:the baseline`
      : displayReference(snapshot.referenceName);
  });

  protected readonly branchCount = computed(() => this.context.snapshot()?.branches.length ?? 0);

  protected readonly totalTile = computed<BootTile>(() => ({
    label: $localize`:@@boot.tile.all:All`,
    tone: 'info',
    count: this.scaled(this.branchCount()),
    share: this.progress(),
  }));

  protected readonly tiles = computed<readonly BootTile[]>(() => {
    const branches = this.context.snapshot()?.branches ?? [];
    const total = Math.max(branches.length, 1);
    return tileOrder.map((kind) => {
      const count = branches.filter((branch) => branch.recommendation === kind).length;
      return {
        label: recommendationLabels[kind],
        tone: recommendationTones[kind],
        count: this.scaled(count),
        share: (count / total) * this.progress(),
      };
    });
  });

  constructor() {
    const startedAt = performance.now();
    const step = (now: number): void => {
      this.progress.set(ease(clamp((now - startedAt - counterStartMs) / counterDurationMs)));
      if (this.progress() < 1) {
        this.frame = requestAnimationFrame(step);
      }
    };
    this.frame = requestAnimationFrame(step);
    this.timer = setTimeout(() => this.done.emit(), bootCompleteMs);
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  protected skip(): void {
    this.stop();
    this.skipped.emit();
  }

  private scaled(target: number): number {
    return Math.round(target * this.progress());
  }

  private stop(): void {
    cancelAnimationFrame(this.frame);
    clearTimeout(this.timer);
  }
}

function clamp(value: number): number {
  return Math.min(1, Math.max(0, value));
}

function ease(value: number): number {
  return 1 - Math.pow(1 - value, 3);
}
