import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

const pickFolderKind = 'pickFolder';
const openExternalKind = 'openExternal';
const saveFileKind = 'saveFile';

/**
 * Message bridge exposed by the desktop shell. It only exists in a window: under Docker
 * and in a browser the object is absent, and every caller keeps what the browser already
 * gives it.
 */
interface DesktopHost {
  sendMessage(message: string): void;
  receiveMessage(callback: (message: string) => void): void;
}

interface DesktopReply {
  readonly id: string;
  readonly kind: string;
  readonly path: string | null;
  readonly isHandled: boolean;
}

interface PendingRequest {
  readonly id: string;
  readonly resolve: (reply: DesktopReply | null) => void;
}

/**
 * Asks the desktop shell for what a webview cannot do on its own: the system folder
 * dialog, opening an address outside the window, and writing a file to disk.
 * Strictly additive: with no shell, `isAvailable` is false and every call resolves to
 * nothing, so the caller falls back to the browser.
 */
@Injectable({ providedIn: 'root' })
export class DesktopBridge {
  private readonly host = resolveHost(inject(DOCUMENT).defaultView);

  /** One request in flight per kind: each one ends in a modal dialog or a disk write. */
  private readonly pending = new Map<string, PendingRequest>();
  private lastRequestNumber = 0;

  readonly isAvailable = this.host !== null;

  constructor() {
    // A single subscription for the whole session: every receiveMessage call adds one more
    // listener on the host side, it never replaces any.
    this.host?.receiveMessage((message) => this.onMessage(message));
  }

  /** Resolves the chosen path, or `null` when the user cancels or the shell is absent. */
  async pickFolder(): Promise<string | null> {
    return (await this.send(pickFolderKind, {}))?.path ?? null;
  }

  /** Resolves true once the system browser has taken the address over. */
  async openExternal(url: string): Promise<boolean> {
    return (await this.send(openExternalKind, { url }))?.isHandled ?? false;
  }

  /** Resolves the path written, or `null` when nothing reached the disk. */
  async saveFile(fileName: string, contents: string): Promise<string | null> {
    return (await this.send(saveFileKind, { fileName, contents }))?.path ?? null;
  }

  private send(kind: string, payload: Record<string, string>): Promise<DesktopReply | null> {
    const host = this.host;
    if (host === null) {
      return Promise.resolve(null);
    }

    // The previous request of that kind is dropped: its dialog is gone by now.
    this.pending.get(kind)?.resolve(null);
    const id = `${++this.lastRequestNumber}`;
    return new Promise<DesktopReply | null>((resolve) => {
      this.pending.set(kind, { id, resolve });
      host.sendMessage(JSON.stringify({ ...payload, id, kind }));
    });
  }

  private onMessage(message: string): void {
    const reply = readReply(message);
    const pending = reply === null ? undefined : this.pending.get(reply.kind);
    if (reply === null || pending === undefined || pending.id !== reply.id) {
      return;
    }

    this.pending.delete(reply.kind);
    pending.resolve(reply);
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
function readReply(message: string): DesktopReply | null {
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
  if (typeof reply['id'] !== 'string' || typeof reply['kind'] !== 'string') {
    return null;
  }

  const path = reply['path'];
  return {
    id: reply['id'],
    kind: reply['kind'],
    path: typeof path === 'string' && path.length > 0 ? path : null,
    isHandled: reply['isHandled'] === true,
  };
}
