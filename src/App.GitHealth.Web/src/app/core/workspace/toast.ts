import { DestroyRef, Injectable, inject, signal } from '@angular/core';

const visibleDurationMs = 2600;

/** Les messages système disent ce qui s'est passé, avec le chiffre. Jamais « Succès ! ». */
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
