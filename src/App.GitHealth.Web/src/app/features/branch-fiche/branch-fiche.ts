import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { SnapshotDetailResponse } from '../../core/api/api.models';
import {
  activityLabels,
  activityTones,
  deleteCommand,
  displayReference,
  recommendationIcons,
  recommendationLabels,
  recommendationTones,
  referenceSource,
} from '../../core/branches/branch-labels';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { KeyValueItem, DsKeyValueList } from '../../ui/surfaces/ds-key-value-list';
import { DsCodeBlock } from '../../ui/surfaces/ds-code-block';
import { Tone } from '../../ui/icon-name';
import { ProjectContext } from '../project/project-context';
import { TraceLine, buildTrace } from './branch-trace';

const shortShaLength = 12;

/** Fiche d'une branche : la recommandation, sa trace, les faits et la commande à copier. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsBadge, DsButton, DsCodeBlock, DsIcon, DsIconButton, DsKeyValueList],
  selector: 'app-branch-fiche',
  styleUrl: './branch-fiche.scss',
  templateUrl: './branch-fiche.html',
})
export class BranchFiche {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly context = inject(ProjectContext);

  readonly snapshotId = input.required<string>();
  readonly closed = output<void>();
  readonly moved = output<string>();

  protected readonly detail = signal<SnapshotDetailResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly displayReference = displayReference;
  protected readonly recommendationLabels = recommendationLabels;
  protected readonly recommendationIcons = recommendationIcons;

  protected readonly canEditPolicy = computed(() => this.context.project() !== null);
  protected readonly hasNext = computed(() => this.context.visibleBranchIds().length > 1);

  protected readonly branchName = computed(() => {
    const detail = this.detail();
    return detail === null ? '' : displayReference(detail.snapshot.referenceName);
  });

  protected readonly source = computed(() => {
    const detail = this.detail();
    return detail === null ? '' : referenceSource(detail.snapshot.referenceName);
  });

  protected readonly flags = computed<readonly { tone: Tone; label: string }[]>(() => {
    const detail = this.detail();
    if (detail === null) {
      return [];
    }

    const flags: { tone: Tone; label: string }[] = [];
    if (detail.snapshot.isProtected) {
      flags.push({ tone: 'brand', label: 'Protégée' });
    }

    if (detail.snapshot.isExcluded) {
      flags.push({ tone: 'neutral', label: 'Exclue' });
    }

    flags.push({
      tone: activityTones[detail.snapshot.activity],
      label: activityLabels[detail.snapshot.activity],
    });
    return flags;
  });

  protected readonly recommendationTone = computed<Tone>(() => {
    const detail = this.detail();
    return detail === null ? 'neutral' : recommendationTones[detail.snapshot.recommendation];
  });

  protected readonly trace = computed<readonly TraceLine[]>(() => {
    const detail = this.detail();
    return detail === null ? [] : buildTrace(detail);
  });

  protected readonly divergenceShare = computed(() => {
    const detail = this.detail();
    if (detail === null) {
      return { ahead: '0%', behind: '0%' };
    }

    const total = Math.max(detail.snapshot.aheadCount + detail.snapshot.behindCount, 1);
    return {
      ahead: `${Math.round((detail.snapshot.aheadCount / total) * 100)}%`,
      behind: `${Math.round((detail.snapshot.behindCount / total) * 100)}%`,
    };
  });

  protected readonly coordinates = computed<readonly KeyValueItem[]>(() => {
    const detail = this.detail();
    if (detail === null) {
      return [];
    }

    return [
      { label: 'Référence complète', value: detail.snapshot.referenceName },
      { label: 'SHA de la branche', value: detail.snapshot.commitId.slice(0, shortShaLength) },
      { label: 'SHA de la référence', value: detail.referenceCommit.slice(0, shortShaLength) },
      { label: 'Comparée à', value: detail.referenceName },
      { label: 'Capture', value: formatInstant(detail.capturedAtUtc) },
    ];
  });

  protected readonly command = computed(() => {
    const detail = this.detail();
    return detail === null ? '' : deleteCommand(detail.snapshot);
  });

  constructor() {
    effect(() => this.load(this.snapshotId()));
  }

  protected next(): void {
    const ids = this.context.visibleBranchIds();
    const current = ids.indexOf(this.snapshotId());
    if (ids.length > 0) {
      this.moved.emit(ids[(current + 1) % ids.length]);
    }
  }

  protected protect(): void {
    this.addPattern('protected');
  }

  protected exclude(): void {
    this.addPattern('excluded');
  }

  private addPattern(kind: 'protected' | 'excluded'): void {
    const project = this.context.project();
    const detail = this.detail();
    if (project === null || detail === null) {
      return;
    }

    const reference = detail.snapshot.referenceName;
    const isProtected = kind === 'protected';
    const current = isProtected ? project.protectedPatterns : project.excludedPatterns;
    if (current.includes(reference)) {
      return;
    }

    this.context.savePolicy(
      {
        activeUntilDays: project.activeUntilDays,
        inactiveAfterDays: project.inactiveAfterDays,
        protectedPatterns: isProtected ? [...current, reference] : project.protectedPatterns,
        excludedPatterns: isProtected ? project.excludedPatterns : [...current, reference],
      },
      `${isProtected ? 'Motif protégé' : 'Motif d’exclusion'} ajouté : ${reference}`,
      () => this.load(this.snapshotId()),
    );
  }

  private load(snapshotId: string): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.api
      .getSnapshot(snapshotId)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (detail) => this.detail.set(detail),
        error: (error: unknown) => {
          this.detail.set(null);
          this.error.set(apiErrorMessage(error, 'Cette branche ne peut pas être expliquée.'));
        },
      });
  }
}

function formatInstant(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '—' : parsed.toLocaleString('fr-FR');
}
