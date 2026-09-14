import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, inject } from '@angular/core';
import { DesktopBridge } from './desktop-bridge';

const externalTarget = '_blank';
const webSchemes = ['http:', 'https:'];

/**
 * Hands every external link to the system browser when the page runs inside the desktop
 * window.
 *
 * WKWebView on macOS and WebKitGTK on Linux answer a `target="_blank"` with nothing at
 * all — Photino implements no `createWebView` delegate — so the link would die in
 * silence. Listening once on the document covers what the shell renders and what the
 * assistant writes alike, and leaves the templates untouched.
 */
@Injectable({ providedIn: 'root' })
export class ExternalLinkInterceptor {
  private readonly document = inject(DOCUMENT);
  private readonly desktop = inject(DesktopBridge);
  private readonly destroyRef = inject(DestroyRef);

  /** Does nothing outside the desktop window: a browser opens its own tabs. */
  start(): void {
    if (!this.desktop.isAvailable) {
      return;
    }

    // Listened to on the way up, after the components: the handover only happens for a
    // link none of them claimed.
    const onClick = (event: MouseEvent): void => this.onClick(event);
    this.document.addEventListener('click', onClick);
    this.destroyRef.onDestroy(() => this.document.removeEventListener('click', onClick));
  }

  private onClick(event: MouseEvent): void {
    if (event.defaultPrevented || event.button !== 0) {
      return;
    }

    const url = externalHref(event.target);
    if (url === null) {
      return;
    }

    event.preventDefault();
    void this.desktop.openExternal(url);
  }
}

/** The address of the external link the click landed on, or `null` when there is none. */
function externalHref(target: EventTarget | null): string | null {
  const anchor = target instanceof Element ? target.closest('a') : null;
  if (anchor === null || anchor.target !== externalTarget) {
    return null;
  }

  return webSchemes.includes(anchor.protocol) ? anchor.href : null;
}
