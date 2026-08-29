import { elapsedDuration, relativeTime } from './relative-time';

const now = Date.parse('2026-08-29T12:00:00Z');

describe('relativeTime', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
  });

  afterEach(() => vi.useRealTimers());

  it('rend « à l’instant » sous la minute', () => {
    expect(relativeTime('2026-08-29T11:59:30Z')).toBe("à l'instant");
  });

  it('rend des minutes puis des heures puis des jours', () => {
    expect(relativeTime('2026-08-29T11:48:00Z')).toBe('il y a 12 min');
    expect(relativeTime('2026-08-29T09:00:00Z')).toBe('il y a 3 h');
    expect(relativeTime('2026-08-27T12:00:00Z')).toBe('il y a 2 j');
  });

  it('reste lisible sans date', () => {
    expect(relativeTime(null)).toBe('date inconnue');
    expect(relativeTime('pas une date')).toBe('date inconnue');
  });
});

describe('elapsedDuration', () => {
  it('rend des secondes à la virgule française', () => {
    expect(elapsedDuration('2026-08-29T12:00:00.000Z', '2026-08-29T12:00:01.800Z')).toBe('1,8 s');
  });

  it('rend un tiret tant que l’analyse n’est pas terminée', () => {
    expect(elapsedDuration('2026-08-29T12:00:00Z', null)).toBe('—');
  });
});
