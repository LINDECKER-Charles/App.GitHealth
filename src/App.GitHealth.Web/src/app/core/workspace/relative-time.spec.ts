import { elapsedDuration, relativeTime } from './relative-time';

const now = Date.parse('2026-08-29T12:00:00Z');

describe('relativeTime', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
  });

  afterEach(() => vi.useRealTimers());

  it('renders "just now" under the minute', () => {
    expect(relativeTime('2026-08-29T11:59:30Z')).toBe('just now');
  });

  it('renders minutes, then hours, then days', () => {
    expect(relativeTime('2026-08-29T11:48:00Z')).toBe('12 min ago');
    expect(relativeTime('2026-08-29T09:00:00Z')).toBe('3 h ago');
    expect(relativeTime('2026-08-27T12:00:00Z')).toBe('2 d ago');
  });

  it('stays readable with no date', () => {
    expect(relativeTime(null)).toBe('unknown date');
    expect(relativeTime('not a date')).toBe('unknown date');
  });
});

describe('elapsedDuration', () => {
  it('renders seconds with the locale decimal separator', () => {
    expect(elapsedDuration('2026-08-29T12:00:00.000Z', '2026-08-29T12:00:01.800Z')).toBe('1.8 s');
  });

  it('renders a dash while the analysis is not finished', () => {
    expect(elapsedDuration('2026-08-29T12:00:00Z', null)).toBe('—');
  });
});
