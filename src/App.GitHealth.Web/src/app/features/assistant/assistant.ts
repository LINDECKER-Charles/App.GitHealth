import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { AssistantStore } from '../../core/assistant/assistant-store';
import { displayReference } from '../../core/branches/branch-labels';
import { pluralMessage } from '../../core/i18n/plural-message';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsSpinner } from '../../ui/core/ds-spinner';
import { DsCheckbox } from '../../ui/forms/ds-checkbox';
import { DsInput } from '../../ui/forms/ds-input';
import { DsSelect } from '../../ui/forms/ds-select';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsEmptyState } from '../../ui/surfaces/ds-empty-state';
import { DsMarkdown } from '../../ui/surfaces/ds-markdown';
import { BaselineStore } from '../project/baseline/baseline-store';

/** Questions worth one click, chosen to be answerable from the capture alone. */
const suggestions: readonly string[] = [
  $localize`:@@assistant.suggestion.cleanup:Which branches look safe to clean up, and why?`,
  $localize`:@@assistant.suggestion.review:Which branches need a review before anything else?`,
  $localize`:@@assistant.suggestion.owners:Group the branches by author and say who has the most work in flight.`,
];

/**
 * Asks a locally installed agent to read the capture GitHealth already took. The agent
 * never sees the repository — only the briefing shown on this screen, which is the whole
 * reason the exchange can be agreed to rather than merely announced.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DsBadge,
    DsButton,
    DsCallout,
    DsCheckbox,
    DsEmptyState,
    DsInput,
    DsMarkdown,
    DsSelect,
    DsSpinner,
  ],
  selector: 'app-assistant',
  styleUrl: './assistant.scss',
  templateUrl: './assistant.html',
})
export class Assistant {
  private readonly baselines = inject(BaselineStore);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.parent?.paramMap ?? this.route.paramMap, {
    requireSync: true,
  });

  protected readonly store = inject(AssistantStore);
  protected readonly suggestions = suggestions;
  protected readonly briefingShowLabel = $localize`:@@assistant.briefing.show:Read the exact text`;
  protected readonly briefingHideLabel = $localize`:@@assistant.briefing.hide:Hide the text`;

  protected readonly projectId = computed(() => this.params().get('projectId') ?? '');

  protected readonly baselineLabel = computed(() => displayReference(this.baselines.selected()));

  /** Nothing to brief means nothing to ask about: the capture is the whole input. */
  protected readonly hasCapture = computed(() => this.store.briefing() !== null);

  protected readonly branchCountLabel = computed(() => {
    const briefing = this.store.briefing();
    return briefing === null ? '' : branchCount(briefing.branchCount);
  });

  protected readonly omittedLabel = computed(() => {
    const omitted = this.store.briefing()?.omittedBranchCount ?? 0;
    return omitted === 0 ? null : omittedNotice(omitted);
  });

  protected readonly failure = computed(() => {
    const run = this.store.run();
    return run?.status === 'Failed' ? (run.failureMessage ?? '') : null;
  });

  protected readonly wasCancelled = computed(() => this.store.run()?.status === 'Cancelled');

  constructor() {
    effect(() => this.store.loadAgents());
    effect(() => this.store.loadBriefing(this.projectId(), this.baselines.requested()));
  }

  protected ask(): void {
    this.store.start(this.projectId(), this.baselines.requested());
  }

  protected useSuggestion(suggestion: string): void {
    this.store.question.set(suggestion);
  }

  protected lookAgain(): void {
    this.store.loadAgents(true);
  }
}

function branchCount(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@assistant.branches.one:${count}:count: branch`,
    other: $localize`:@@assistant.branches.many:${count}:count: branches`,
  });
}

function omittedNotice(count: number): string {
  return $localize`:@@assistant.omitted:${count}:count: further branches are measured but left out of this capture, which the agent is told.`;
}
