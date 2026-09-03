import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  viewChild,
  ElementRef,
} from '@angular/core';
import { AnalysisReferenceProgress } from '../../core/api/api.models';
import { toLedgerRow } from '../../core/analysis/analysis-ledger-row';
import { DsBadge } from '../../ui/core/ds-badge';

/** Height of one row in the stylesheet: the scroll maths needs it in numbers. */
const rowHeight = 30;

/**
 * The exhaustive list of what the run is reading, one line per reference, filled in as the
 * facts land. It scrolls itself so the reference being read stays under the eye.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsBadge],
  selector: 'app-analysis-ledger',
  styleUrl: './analysis-ledger.scss',
  templateUrl: './analysis-ledger.html',
})
export class AnalysisLedger {
  readonly references = input.required<readonly AnalysisReferenceProgress[]>();

  protected readonly rows = computed(() => this.references().map(toLedgerRow));

  private readonly body = viewChild<ElementRef<HTMLElement>>('body');

  private readonly readingRank = computed(() =>
    this.references().findIndex(
      (reference) => reference.state === 'Measuring' || reference.state === 'Enriching',
    ),
  );

  constructor() {
    effect(() => this.follow(this.readingRank()));
  }

  /** Centres the row being read; with nothing being read, stays where the reader left it. */
  private follow(rank: number): void {
    const body = this.body()?.nativeElement;
    if (body === undefined || rank < 0) {
      return;
    }

    const target = Math.max(0, rank * rowHeight + rowHeight / 2 - body.clientHeight / 2);
    if (Math.abs(body.scrollTop - target) > 2) {
      body.scrollTop = target;
    }
  }
}
