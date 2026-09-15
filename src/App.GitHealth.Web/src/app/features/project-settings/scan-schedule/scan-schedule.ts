import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Uuid } from '../../../core/api/api.models';
import { absoluteTime, relativeTime } from '../../../core/workspace/relative-time';
import { DsButton } from '../../../ui/core/ds-button';
import { DsStatusDot } from '../../../ui/core/ds-status-dot';
import { DsInput } from '../../../ui/forms/ds-input';
import { DsSelect } from '../../../ui/forms/ds-select';
import { DsSwitch } from '../../../ui/forms/ds-switch';
import { DsCallout } from '../../../ui/surfaces/ds-callout';
import { DsPanel } from '../../../ui/surfaces/ds-panel';
import { ScheduleStore } from './schedule-store';
import {
  cronPresetOptions,
  customPreset,
  hasCronShape,
  normalizeCron,
  presetOf,
} from './cron-presets';

/**
 * Scheduled scanning, seen from the policy screen: how often this repository re-measures
 * itself, when it last did and when it next will. The expression stays on screen whichever
 * rhythm is picked — the picker is a shortcut into cron, not a replacement for it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsCallout, DsInput, DsPanel, DsSelect, DsStatusDot, DsSwitch],
  selector: 'app-scan-schedule',
  styleUrl: './scan-schedule.scss',
  templateUrl: './scan-schedule.html',
})
export class ScanSchedule {
  protected readonly store = inject(ScheduleStore);

  readonly projectId = input.required<Uuid>();

  protected readonly presetOptions = cronPresetOptions();

  protected readonly isEnabled = signal(false);
  protected readonly expression = signal('');
  protected readonly preset = signal(customPreset);

  protected readonly isValid = computed(() => !this.isEnabled() || hasCronShape(this.expression()));

  protected readonly isDirty = computed(() => {
    const schedule = this.store.schedule();
    if (schedule === null) {
      return false;
    }

    return (
      this.isEnabled() !== schedule.isEnabled ||
      normalizeCron(this.expression()) !== (schedule.cronExpression ?? '')
    );
  });

  protected readonly canSave = computed(
    () => this.isDirty() && this.isValid() && !this.store.isSaving(),
  );

  /** What the switch amounts to right now, which is not always what it was set to. */
  protected readonly stateTitle = computed(() => {
    const schedule = this.store.schedule();
    if (schedule === null || !schedule.isEnabled) {
      return $localize`:@@schedule.state.off:No scheduled scan`;
    }

    return schedule.isSchedulerRunning
      ? $localize`:@@schedule.state.on:Scanning on a schedule`
      : $localize`:@@schedule.state.suspended:Schedule saved, scheduler switched off`;
  });

  protected readonly nextRunDetail = computed(() => {
    const schedule = this.store.schedule();
    if (schedule === null || !schedule.isEnabled) {
      return $localize`:@@schedule.next.none:This repository is only measured when you ask it to be.`;
    }

    if (!schedule.isSchedulerRunning) {
      return $localize`:@@schedule.next.suspended:Scheduled scanning is off for this installation, so nothing will fire.`;
    }

    return schedule.nextRunAtUtc === null
      ? $localize`:@@schedule.next.never:This expression names a date that never comes.`
      : nextNotice(absoluteTime(schedule.nextRunAtUtc));
  });

  protected readonly lastRunDetail = computed(() => {
    const lastRun = this.store.schedule()?.lastRunAtUtc ?? null;
    return lastRun === null ? $localize`:@@schedule.last.never:never fired` : relativeTime(lastRun);
  });

  protected readonly timeZoneId = computed(() => this.store.schedule()?.timeZoneId ?? '');

  constructor() {
    effect(() => this.store.load(this.projectId()));
    effect(() => this.reset());
  }

  protected reset(): void {
    const schedule = this.store.schedule();
    if (schedule === null) {
      return;
    }

    this.isEnabled.set(schedule.isEnabled);
    this.expression.set(schedule.cronExpression ?? '');
    this.preset.set(presetOf(schedule.cronExpression ?? ''));
  }

  /** Picking a rhythm writes its expression; "custom" leaves whatever is already written. */
  protected applyPreset(value: string): void {
    this.preset.set(value);
    if (value !== customPreset) {
      this.expression.set(value);
    }
  }

  /** Typing over a picked rhythm moves the picker rather than leaving it lying. */
  protected editExpression(value: string): void {
    this.expression.set(value);
    this.preset.set(presetOf(value));
  }

  protected save(): void {
    if (!this.canSave()) {
      return;
    }

    const expression = normalizeCron(this.expression());
    this.store.save(
      this.projectId(),
      {
        isEnabled: this.isEnabled(),
        cronExpression: expression.length === 0 ? null : expression,
      },
      $localize`:@@schedule.toast.saved:Schedule saved · no Git write`,
    );
  }
}

function nextNotice(when: string): string {
  return $localize`:@@schedule.next.at:Next scan on ${when}:when:`;
}
