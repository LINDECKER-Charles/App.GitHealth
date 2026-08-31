import { AnalysisHistoryItem } from '../../core/api/api.models';
import { sourceLocale } from '../../core/i18n/locale';

/** The maximum the API accepts for the history; beyond it the request is rejected. */
export const captureHistoryPageSize = 100;

/** The capture being read lives in the URL: it is shareable, reloadable and survives a back. */
export const captureQueryParam = 'capture';

const shortCommitLength = 8;
const separator = ' · ';
const latestMarker = $localize`:@@capture.option.latestMarker:latest`;
/** Same word as `relativeAge` uses: the two are read in the same tab. */
const todayLabel = $localize`:@@capture.date.today:today`;
const shortDate = new Intl.DateTimeFormat(sourceLocale, { day: 'numeric', month: 'short' });
const shortTime = new Intl.DateTimeFormat(sourceLocale, { hour: '2-digit', minute: '2-digit' });

/** A usable capture: the server requires both fields to read its branches back. */
export type CompletedAnalysis = AnalysisHistoryItem & {
  readonly capturedAtUtc: string;
  readonly referenceCommit: string;
};

export interface CaptureOption {
  readonly analysisId: string;
  readonly short: string;
  readonly label: string;
  readonly isLatest: boolean;
}

/** Chronological order: every view assumes the oldest capture comes first. */
export function comparableAnalyses(
  items: readonly AnalysisHistoryItem[],
): readonly CompletedAnalysis[] {
  return items
    .filter(isComparable)
    .slice()
    .sort((left, right) => left.capturedAtUtc.localeCompare(right.capturedAtUtc));
}

/**
 * Day then time: several analyses a day are the norm, and without the time they would all
 * read the same. The current day is spelled out instead of dated.
 */
export function shortCaptureDate(capturedAtUtc: string, now: Date): string {
  const captured = new Date(capturedAtUtc);
  const isToday =
    captured.getFullYear() === now.getFullYear() &&
    captured.getMonth() === now.getMonth() &&
    captured.getDate() === now.getDate();
  const day = isToday ? todayLabel : shortDate.format(captured);
  return `${day} ${shortTime.format(captured)}`;
}

export function captureLabel(short: string, referenceCommit: string): string {
  return `${short}${separator}${referenceCommit.slice(0, shortCommitLength)}`;
}

/**
 * The most recent capture carries its rank in its label: without it, only its position in
 * the list would say so, and nothing would tell it apart from a capture of the same day.
 */
export function toCaptureOptions(
  analyses: readonly CompletedAnalysis[],
  now: Date,
): readonly CaptureOption[] {
  const lastIndex = analyses.length - 1;
  return analyses.map((analysis, index) => {
    const short = shortCaptureDate(analysis.capturedAtUtc, now);
    const label = captureLabel(short, analysis.referenceCommit);
    const isLatest = index === lastIndex;
    return {
      analysisId: analysis.analysisId,
      short,
      label: isLatest ? `${label}${separator}${latestMarker}` : label,
      isLatest,
    };
  });
}

function isComparable(item: AnalysisHistoryItem): item is CompletedAnalysis {
  return (
    item.status === 'Completed' && item.capturedAtUtc !== null && item.referenceCommit !== null
  );
}
