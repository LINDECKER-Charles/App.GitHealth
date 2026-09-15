import { SelectOption } from '../../../ui/forms/ds-select';

/** Value the picker carries when the expression matches none of the offered rhythms. */
export const customPreset = 'custom';

/**
 * The rhythms worth one click. Cron is written, not chosen, but nobody should have to
 * remember five fields to say "every morning" — and the expression stays visible underneath,
 * so picking one teaches the syntax rather than hiding it.
 */
export const cronPresets: readonly SelectOption[] = [
  { value: '*/15 * * * *', label: $localize`:@@schedule.preset.quarterHour:Every 15 minutes` },
  { value: '0 * * * *', label: $localize`:@@schedule.preset.hourly:Every hour` },
  { value: '0 */4 * * *', label: $localize`:@@schedule.preset.fourHours:Every 4 hours` },
  { value: '0 9 * * *', label: $localize`:@@schedule.preset.daily:Every day at 09:00` },
  { value: '0 9 * * 1-5', label: $localize`:@@schedule.preset.weekdays:Weekdays at 09:00` },
  { value: '0 9 * * 1', label: $localize`:@@schedule.preset.weekly:Mondays at 09:00` },
];

const cronFieldCount = 5;

/** Characters a cron field is built from. Anything else is a typo, not a rhythm. */
const cronFieldPattern = /^[\d*/,-]+$/;

/**
 * The picker's own value for an expression: the matching rhythm, or "custom". Comparison is
 * on the normalised form, so an expression written with extra spaces still finds its preset.
 */
export function presetOf(expression: string): string {
  const normalized = normalizeCron(expression);
  const match = cronPresets.find((preset) => preset.value === normalized);
  return match?.value ?? customPreset;
}

/** Collapses runs of whitespace, which is exactly what the API stores back. */
export function normalizeCron(expression: string): string {
  return expression.trim().split(/\s+/).filter(Boolean).join(' ');
}

/**
 * Whether the text is shaped like a cron expression. A shape check, not a parse: the API
 * owns the verdict, and this only stops the obviously empty form being sent to be refused.
 */
export function hasCronShape(expression: string): boolean {
  const fields = normalizeCron(expression).split(' ').filter(Boolean);
  return fields.length === cronFieldCount && fields.every((field) => cronFieldPattern.test(field));
}

/** The picker's options, with "custom" last so the written rhythms read as the shortcuts. */
export function cronPresetOptions(): readonly SelectOption[] {
  return [
    ...cronPresets,
    { value: customPreset, label: $localize`:@@schedule.preset.custom:Custom expression` },
  ];
}
