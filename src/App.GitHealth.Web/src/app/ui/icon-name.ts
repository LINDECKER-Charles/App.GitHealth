/**
 * Lucide glyphs copied into `public/ds/icons`. The type closes the list: a name absent
 * from the folder does not compile, so no icon can disappear at runtime.
 * `star-filled` reuses the `star` outline, filled: a favourite reads solid, not just coloured.
 */
export type IconName =
  | 'arrow-right'
  | 'book-open'
  | 'check'
  | 'chevron-down'
  | 'chevron-left'
  | 'chevron-right'
  | 'chevron-up'
  | 'circle-alert'
  | 'circle-arrow-up'
  | 'circle-check'
  | 'clock'
  | 'command'
  | 'copy'
  | 'download'
  | 'external-link'
  | 'eye'
  | 'eye-off'
  | 'folder'
  | 'folder-open'
  | 'funnel'
  | 'git-branch'
  | 'info'
  | 'list'
  | 'lock'
  | 'minus'
  | 'moon'
  | 'play'
  | 'plus'
  | 'refresh-cw'
  | 'search'
  | 'settings'
  | 'sparkles'
  | 'star'
  | 'star-filled'
  | 'sun'
  | 'terminal'
  | 'trash-2'
  | 'triangle-alert'
  | 'x';

/** Semantic tones of the system: a state is always a tinted background with its border. */
export type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'brand' | 'merged';
