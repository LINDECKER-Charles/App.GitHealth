/**
 * Glyphes Lucide copiés dans `public/ds/icons`. Le type ferme la liste : un nom
 * absent du dossier ne compile pas, donc aucune icône ne peut disparaître à l'exécution.
 * `star-filled` reprend le tracé de `star`, rempli : un favori se lit plein, pas seulement coloré.
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
  | 'circle-check'
  | 'clock'
  | 'command'
  | 'copy'
  | 'download'
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
  | 'star'
  | 'star-filled'
  | 'sun'
  | 'trash-2'
  | 'triangle-alert'
  | 'x';

/** Tonalités sémantiques du système : un état est toujours un fond teinté avec sa bordure. */
export type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'brand' | 'merged';
