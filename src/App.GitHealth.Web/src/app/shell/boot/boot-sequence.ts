/** Une étape de la séquence d'ouverture : ce que GitHealth fait, dans l'ordre où il le fait. */
export interface BootStep {
  readonly label: string;
  readonly revealDelayMs: number;
  readonly settleDelayMs: number;
}

const firstStepDelayMs = 1050;
const stepIntervalMs = 540;
const settleOffsetMs = 430;

const stepLabels: readonly string[] = [
  'Ouverture du dépôt',
  'Lecture des références',
  'Résolution de la référence de comparaison',
  'Recherche des bases communes',
  'Comptage avance / retard',
  'Agrégation des contributeurs',
  'Application de la politique',
  'Enregistrement du snapshot',
];

export const bootSteps: readonly BootStep[] = stepLabels.map((label, index) => {
  const revealDelayMs = firstStepDelayMs + index * stepIntervalMs;
  return { label, revealDelayMs, settleDelayMs: revealDelayMs + settleOffsetMs };
});

/** Instant où le voile commence à s'effacer, puis celui où le composant se retire. */
export const bootFadeStartMs = 8350;
export const bootCompleteMs = 8800;

/** Fenêtre pendant laquelle les compteurs montent vers les chiffres réels du snapshot. */
export const counterStartMs = 5500;
export const counterDurationMs = 1900;
