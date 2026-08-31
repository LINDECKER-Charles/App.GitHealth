import { sourceLocale } from '../i18n/locale';

const secondsPerMinute = 60;
const minutesPerHour = 60;
const hoursPerDay = 24;
const millisecondsPerSecond = 1000;

const countFormatter = new Intl.NumberFormat(sourceLocale, { maximumFractionDigits: 0 });

const secondsFormatter = new Intl.NumberFormat(sourceLocale, {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

const unknownDateLabel = $localize`:@@time.relative.unknownDate:unknown date`;

/** Compact form: "just now", "12 min ago", "3 h ago", "2 d ago". */
export function relativeTime(instant: string | null): string {
  if (instant === null) {
    return unknownDateLabel;
  }

  const elapsed = Date.now() - Date.parse(instant);
  if (Number.isNaN(elapsed)) {
    return unknownDateLabel;
  }

  const seconds = Math.max(0, Math.floor(elapsed / millisecondsPerSecond));
  if (seconds < secondsPerMinute) {
    return $localize`:@@time.relative.justNow:just now`;
  }

  const minutes = Math.floor(seconds / secondsPerMinute);
  if (minutes < minutesPerHour) {
    const minuteCount = countFormatter.format(minutes);
    return $localize`:@@time.relative.minutes:${minuteCount}:minuteCount: min ago`;
  }

  const hours = Math.floor(minutes / minutesPerHour);
  if (hours < hoursPerDay) {
    const hourCount = countFormatter.format(hours);
    return $localize`:@@time.relative.hours:${hourCount}:hourCount: h ago`;
  }

  const dayCount = countFormatter.format(Math.floor(hours / hoursPerDay));
  return $localize`:@@time.relative.days:${dayCount}:dayCount: d ago`;
}

/** Duration of an analysis, in seconds formatted for the application locale. */
export function elapsedDuration(startedAt: string, completedAt: string | null): string {
  if (completedAt === null) {
    return '—';
  }

  const elapsed = Date.parse(completedAt) - Date.parse(startedAt);
  if (Number.isNaN(elapsed) || elapsed < 0) {
    return '—';
  }

  const secondCount = secondsFormatter.format(elapsed / millisecondsPerSecond);
  return $localize`:@@time.duration.seconds:${secondCount}:secondCount: s`;
}
