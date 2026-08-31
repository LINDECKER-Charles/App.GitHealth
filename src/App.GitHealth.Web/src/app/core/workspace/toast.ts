import { DestroyRef, Injectable, inject, signal } from '@angular/core';

const visibleDurationMs = 2600;

/** System messages say what happened, with the figure. Never "Success!". */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly current = signal<string | null>(null);
  private timer?: ReturnType<typeof setTimeout>;

  readonly message = this.current.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  show(message: string): void {
    this.current.set(message);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.current.set(null), visibleDurationMs);
  }

  dismiss(): void {
    clearTimeout(this.timer);
    this.current.set(null);
  }
}
