import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AnalysisRunNarration } from '../../core/analysis/analysis-run-narration';
import { AnalysisRunStore } from '../../core/analysis/analysis-run-store';
import { DsButton } from '../../ui/core/ds-button';
import { DsSpinner } from '../../ui/core/ds-spinner';

/**
 * The run, reduced to one line, for a reader who would rather look at the last capture
 * while it goes on. Same phases, same counters, one row high.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsSpinner],
  selector: 'app-analysis-run-strip',
  styles: `
    :host {
      display: block;
      margin: 12px 20px 0;
      padding: 10px 12px;
      border: 1px solid var(--status-info-border);
      background: var(--status-info-bg);
      border-radius: var(--radius-md);
      animation: gh-in var(--dur-base) var(--ease-out);
    }

    .strip-status {
      display: flex;
      align-items: center;
      gap: 8px;
      font: var(--type-small);
      color: var(--status-info-fg);
    }

    .strip-status strong {
      font-weight: var(--weight-medium);
    }

    .strip-figure {
      font: var(--type-code-sm);
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .strip-target {
      font: var(--type-code-sm);
      min-width: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .strip-separator {
      color: var(--text-muted);
    }

    .strip-spacer {
      flex: 1;
    }

    .strip-phases {
      display: flex;
      gap: 3px;
      margin-top: 8px;
    }

    .strip-phases span {
      flex: 1;
      height: 3px;
      border-radius: 2px;
      background: var(--border-default);
    }

    .strip-phases span.is-reached {
      background: var(--status-info-solid);
    }
  `,
  template: `
    <div class="strip-status">
      <ds-spinner [size]="13" />
      <strong>{{ narration.phaseTitle() }}</strong>
      <span class="strip-figure">{{ narration.progressLabel() }}</span>
      <span class="strip-separator">·</span>
      <span class="strip-target">{{ narration.currentTarget() }}</span>
      <span class="strip-spacer"></span>
      <button dsButton size="sm" variant="ghost" iconLeft="eye" (click)="expand()">
        <ng-container i18n="@@analysisRun.action.expand">Show the analysis</ng-container>
      </button>
      <span class="strip-figure">{{ narration.stepLabel() }}</span>
    </div>
    <div class="strip-phases">
      @for (step of narration.steps(); track step.phase) {
        <span [class.is-reached]="step.isDone || step.isCurrent"></span>
      }
    </div>
  `,
})
export class AnalysisRunStrip {
  private readonly run = inject(AnalysisRunStore);

  protected readonly narration = inject(AnalysisRunNarration);

  protected expand(): void {
    this.run.expand();
  }
}
