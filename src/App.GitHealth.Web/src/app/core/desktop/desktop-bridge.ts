import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

const pickFolderKind = 'pickFolder';

/**
 * Message bridge exposed by the desktop shell. It only exists in a window: under Docker
 * and in a browser the object is absent, and the application keeps its HTML folder
 * browser.
 */
interface DesktopHost {
  sendMessage(message: string): void;
  receiveMessage(callback: (message: string) => void): void;
}

interface PendingRequest {
  readonly id: string;
  readonly resolve: (path: string | null) => void;
}

/**
 * Opens the system folder dialog when the desktop shell offers it.
 * Strictly additive: with no shell, `isAvailable` is false and the caller falls back to
 * the folder browser served by the API.
 */
@Injectable({ providedIn: 'root' })
export class DesktopBridge {
  private readonly host = resolveHost(inject(DOCUMENT).defaultView);
  private pending: PendingRequest | null = null;
  private lastRequestNumber = 0;

  readonly isAvailable = this.host !== null;

  constructor() {
    // A single subscription for the whole session: every receiveMessage call adds one more
    // listener on the host side, it never replaces any.
    this.host?.receiveMessage((message) => this.onMessage(message));
  }

  /** Resolves the chosen path, or `null` when the user cancels or the shell is absent. */
  pickFolder(): Promise<string | null> {
    const host = this.host;
    if (host === null) {
      return Promise.resolve(null);
    }

    // The dialog is modal: one in-flight request is enough, and the previous one is dropped.
    this.pending?.resolve(null);
    const id = `${++this.lastRequestNumber}`;
    return new Promise<string | null>((resolve) => {
      this.pending = { id, resolve };
      host.sendMessage(JSON.stringify({ id, kind: pickFolderKind }));
    });
  }

  private onMessage(message: string): void {
    const reply = readReply(message);
    const pending = this.pending;
    if (reply === null || pending === null || reply.id !== pending.id) {
      return;
    }

    this.pending = null;
    pending.resolve(reply.path);
  }
}

function resolveHost(view: Window | null): DesktopHost | null {
  try {
    const candidate = (view as { external?: Partial<DesktopHost> } | null)?.external;
    return typeof candidate?.sendMessage === 'function' &&
      typeof candidate.receiveMessage === 'function'
      ? (candidate as DesktopHost)
      : null;
  } catch {
    return null;
  }
}

/** The host speaks in text: any unreadable reply is ignored rather than propagated. */
function readReply(message: string): { id: string; path: string | null } | null {
  let value: unknown;
  try {
    value = JSON.parse(message);
  } catch {
    return null;
  }

  if (value === null || typeof value !== 'object') {
    return null;
  }

  const reply = value as Record<string, unknown>;
  if (reply['kind'] !== pickFolderKind || typeof reply['id'] !== 'string') {
    return null;
  }

  const path = reply['path'];
  return { id: reply['id'], path: typeof path === 'string' && path.length > 0 ? path : null };
}
