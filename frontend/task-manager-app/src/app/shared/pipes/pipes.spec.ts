import { RelativeTimePipe } from './relative-time.pipe';
import { TruncatePipe } from './truncate.pipe';

describe('RelativeTimePipe', () => {
  const pipe = new RelativeTimePipe();

  it('formats past timestamps as relative time', () => {
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000).toISOString();
    expect(pipe.transform(fiveMinutesAgo)).toBe('5 minutes ago');
  });

  it('falls back to "just now" for very recent timestamps', () => {
    expect(pipe.transform(new Date().toISOString())).toBe('just now');
  });

  it('returns empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });
});

describe('TruncatePipe', () => {
  const pipe = new TruncatePipe();

  it('leaves short strings untouched', () => {
    expect(pipe.transform('short', 10)).toBe('short');
  });

  it('truncates long strings with an ellipsis', () => {
    expect(pipe.transform('a'.repeat(100), 10)).toBe(`${'a'.repeat(10)}…`);
  });

  it('returns empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
