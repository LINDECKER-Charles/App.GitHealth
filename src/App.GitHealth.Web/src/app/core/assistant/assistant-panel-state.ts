import { Injectable, computed, signal } from '@angular/core';

/** The panel shows one thread, or the list of them. Never both. */
export type AssistantPanelView = 'thread' | 'history';

/**
 * Whether the assistant panel is open, and what it is showing. It sits outside the panel
 * itself because the shell opens it — from a button and from a keyboard shortcut — and a
 * component cannot be asked to open before it exists.
 */
@Injectable({ providedIn: 'root' })
export class AssistantPanelState {
  private readonly openState = signal(false);
  private readonly viewState = signal<AssistantPanelView>('thread');
  private readonly briefingState = signal(false);

  readonly isOpen = this.openState.asReadonly();
  readonly view = this.viewState.asReadonly();

  /** The exact text the agent can read, folded away until someone asks to see it. */
  readonly isBriefingOpen = this.briefingState.asReadonly();

  readonly isHistory = computed(() => this.viewState() === 'history');

  toggle(): void {
    this.openState.update((open) => !open);
  }

  open(): void {
    this.openState.set(true);
  }

  close(): void {
    this.openState.set(false);
  }

  showThread(): void {
    this.viewState.set('thread');
  }

  toggleHistory(): void {
    this.viewState.update((view) => (view === 'history' ? 'thread' : 'history'));
  }

  toggleBriefing(): void {
    this.briefingState.update((shown) => !shown);
  }
}
