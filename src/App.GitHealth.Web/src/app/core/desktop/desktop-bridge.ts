import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

const pickFolderKind = 'pickFolder';

/**
 * Pont de messages exposé par la coque de bureau. Il n'existe qu'en fenêtre : en Docker
 * et dans un navigateur, l'objet est absent et l'application garde son navigateur de
 * dossiers HTML.
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
 * Ouvre le dialogue de dossier du système quand la coque de bureau le propose.
 * Strictement additif : sans coque, `isAvailable` est faux et l'appelant retombe sur le
 * navigateur de dossiers servi par l'API.
 */
@Injectable({ providedIn: 'root' })
export class DesktopBridge {
  private readonly host = resolveHost(inject(DOCUMENT).defaultView);
  private pending: PendingRequest | null = null;
  private lastRequestNumber = 0;

  readonly isAvailable = this.host !== null;

  constructor() {
    // Un seul abonnement pour toute la session : chaque appel à receiveMessage ajoute un
    // écouteur de plus côté hôte, il n'en remplace jamais aucun.
    this.host?.receiveMessage((message) => this.onMessage(message));
  }

  /** Résout le chemin choisi, ou `null` si l'utilisateur annule ou si la coque est absente. */
  pickFolder(): Promise<string | null> {
    const host = this.host;
    if (host === null) {
      return Promise.resolve(null);
    }

    // Le dialogue est modal : une requête en vol suffit, et la précédente est abandonnée.
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

/** L'hôte parle en texte : toute réponse illisible est ignorée plutôt que propagée. */
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
