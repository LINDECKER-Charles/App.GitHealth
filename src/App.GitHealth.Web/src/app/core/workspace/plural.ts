/** En français, seuls les nombres strictement supérieurs à un prennent la marque du pluriel. */
export function plural(count: number, singular: string): string {
  return `${count} ${singular}${count > 1 ? 's' : ''}`;
}
