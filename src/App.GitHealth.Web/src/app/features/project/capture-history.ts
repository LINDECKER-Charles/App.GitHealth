import { AnalysisHistoryItem } from '../../core/api/api.models';

/** Le maximum accepté par l'API pour l'historique ; au-delà elle rejette la requête. */
export const captureHistoryPageSize = 100;

/** La capture regardée vit dans l'URL : elle se partage, se recharge et survit au retour arrière. */
export const captureQueryParam = 'capture';

const shortCommitLength = 8;
const separator = ' · ';
const latestMarker = 'dernière';
/** Apostrophe droite : c'est celle de `relativeAge`, et les deux se lisent dans le même onglet. */
const todayLabel = "aujourd'hui";
const shortDate = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short' });
const shortTime = new Intl.DateTimeFormat('fr-FR', { hour: '2-digit', minute: '2-digit' });

/** Une capture exploitable : le serveur exige ces deux champs pour relire ses branches. */
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

/** Ordre chronologique : toutes les vues supposent la plus ancienne en tête. */
export function comparableAnalyses(
  items: readonly AnalysisHistoryItem[],
): readonly CompletedAnalysis[] {
  return items
    .filter(isComparable)
    .slice()
    .sort((left, right) => left.capturedAtUtc.localeCompare(right.capturedAtUtc));
}

/**
 * Jour puis heure : plusieurs analyses par jour sont la norme, et sans l'heure elles
 * s'affichent toutes pareil. Le jour même se dit en toutes lettres.
 */
export function shortCaptureDate(capturedAtUtc: string, now: Date): string {
  const captured = new Date(capturedAtUtc);
  const isToday =
    captured.getFullYear() === now.getFullYear() &&
    captured.getMonth() === now.getMonth() &&
    captured.getDate() === now.getDate();
  const day = isToday ? todayLabel : shortDate.format(captured).replace(/\.$/, '');
  return `${day} ${shortTime.format(captured)}`;
}

export function captureLabel(short: string, referenceCommit: string): string {
  return `${short}${separator}${referenceCommit.slice(0, shortCommitLength)}`;
}

/**
 * La plus récente porte son rang dans son libellé : sans cela, seule sa position dans la
 * liste le dirait, et rien ne distinguerait « la dernière » d'une capture du même jour.
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
