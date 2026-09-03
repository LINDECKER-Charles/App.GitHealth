import { Injectable, computed, inject } from '@angular/core';
import { displayReference } from '../branches/branch-labels';
import { pluralMessage } from '../i18n/plural-message';
import { phaseDetail, phaseLabel } from '../workspace/analysis-phases';
import { AnalysisRunStore } from './analysis-run-store';
import { buildPhaseSteps } from './analysis-phase-steps';

const totalPhaseCount = 5;

/**
 * Says in words what the run is doing. The full scene and the collapsed strip show the same
 * run at two sizes, so the sentences are written once, here, and read twice.
 */
@Injectable({ providedIn: 'root' })
export class AnalysisRunNarration {
  private readonly run = inject(AnalysisRunStore);

  readonly isWorking = computed(() => this.run.phase() !== 'Finished');

  readonly steps = computed(() =>
    buildPhaseSteps(this.run.phase(), this.run.processed(), this.run.total()),
  );

  readonly phaseTitle = computed(() => phaseLabel(this.run.phase()));

  readonly phaseDetail = computed(() => {
    const phase = this.run.phase();
    return phase === 'Finished' ? finishedDetail(this.run.total()) : `· ${phaseDetail(phase)}`;
  });

  readonly progressLabel = computed(() => {
    const total = this.run.total();
    if (total === 0) {
      return '';
    }

    const phase = this.run.phase();
    if (phase === 'Waiting') {
      return referenceCountLabel(total);
    }

    const isPerReference = phase === 'Topology' || phase === 'Enrichment';
    return branchProgressLabel(isPerReference ? this.run.processed() : total, total);
  });

  /** What the run has its hands on right now: a reference, the database, or the repository. */
  readonly currentTarget = computed(() => {
    const reading = this.run.reading();
    if (reading.length > 0) {
      return displayReference(reading[0].referenceName);
    }

    return this.run.phase() === 'Persistence'
      ? $localize`:@@analysisRun.target.database:githealth.db`
      : $localize`:@@analysisRun.target.repository:opening the repository`;
  });

  readonly stepLabel = computed(() => {
    const reached = this.steps().filter((step) => step.isDone || step.isCurrent).length;
    return stepOfLabel(Math.max(1, reached), totalPhaseCount);
  });
}

function finishedDetail(total: number): string {
  const branches = branchCountLabel(total);
  return $localize`:@@analysisRun.detail.finished:· ${branches}:branches: read · no Git write`;
}

function referenceCountLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@analysisRun.references.one:${count}:count: reference`,
    other: $localize`:@@analysisRun.references.many:${count}:count: references`,
  });
}

function branchCountLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@analysisRun.branches.one:${count}:count: branch`,
    other: $localize`:@@analysisRun.branches.many:${count}:count: branches`,
  });
}

function branchProgressLabel(processed: number, total: number): string {
  return $localize`:@@analysisRun.progress.branches:${processed}:processed: / ${total}:total: branches`;
}

function stepOfLabel(step: number, total: number): string {
  return $localize`:@@analysisRun.progress.step:step ${step}:step: of ${total}:total:`;
}
