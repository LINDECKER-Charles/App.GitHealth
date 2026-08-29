const secondsPerMinute = 60;
const minutesPerHour = 60;
const hoursPerDay = 24;
const millisecondsPerSecond = 1000;

/** Formule française compacte : « à l'instant », « il y a 12 min », « il y a 3 h », « il y a 2 j ». */
export function relativeTime(instant: string | null): string {
  if (instant === null) {
    return 'date inconnue';
  }

  const elapsed = Date.now() - Date.parse(instant);
  if (Number.isNaN(elapsed)) {
    return 'date inconnue';
  }

  const seconds = Math.max(0, Math.floor(elapsed / millisecondsPerSecond));
  if (seconds < secondsPerMinute) {
    return "à l'instant";
  }

  const minutes = Math.floor(seconds / secondsPerMinute);
  if (minutes < minutesPerHour) {
    return `il y a ${minutes} min`;
  }

  const hours = Math.floor(minutes / minutesPerHour);
  return hours < hoursPerDay ? `il y a ${hours} h` : `il y a ${Math.floor(hours / hoursPerDay)} j`;
}

/** Durée d'une analyse, en secondes avec virgule décimale française. */
export function elapsedDuration(startedAt: string, completedAt: string | null): string {
  if (completedAt === null) {
    return '—';
  }

  const elapsed = Date.parse(completedAt) - Date.parse(startedAt);
  if (Number.isNaN(elapsed) || elapsed < 0) {
    return '—';
  }

  return `${(elapsed / millisecondsPerSecond).toFixed(1).replace('.', ',')} s`;
}
