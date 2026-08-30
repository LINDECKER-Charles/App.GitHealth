import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { DesktopBridge } from './desktop-bridge';

/** Coque de bureau simulée : capture les messages émis et rejoue les réponses de l'hôte. */
class FakeDesktopHost {
  readonly sent: string[] = [];
  private callback: ((message: string) => void) | null = null;

  sendMessage(message: string): void {
    this.sent.push(message);
  }

  receiveMessage(callback: (message: string) => void): void {
    this.callback = callback;
  }

  reply(payload: Record<string, unknown>): void {
    this.callback?.(JSON.stringify(payload));
  }

  raw(message: string): void {
    this.callback?.(message);
  }

  lastRequestId(): string {
    return (JSON.parse(this.sent[this.sent.length - 1]) as { id: string }).id;
  }
}

function bridgeWith(external: unknown): DesktopBridge {
  TestBed.configureTestingModule({
    providers: [{ provide: DOCUMENT, useValue: { defaultView: { external } } }],
  });
  return TestBed.inject(DesktopBridge);
}

describe('DesktopBridge', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reste indisponible sans coque de bureau', async () => {
    const bridge = bridgeWith(undefined);

    expect(bridge.isAvailable).toBe(false);
    await expect(bridge.pickFolder()).resolves.toBeNull();
  });

  it('ignore un objet hôte incomplet', () => {
    expect(bridgeWith({ sendMessage: () => undefined }).isAvailable).toBe(false);
  });

  it('demande un dossier à la coque et résout le chemin choisi', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);
    expect(bridge.isAvailable).toBe(true);

    const selection = bridge.pickFolder();
    const request = JSON.parse(host.sent[0]) as { id: string; kind: string };
    expect(request.kind).toBe('pickFolder');
    host.reply({ id: request.id, kind: 'pickFolder', path: 'D:\\Projets\\mon-depot' });

    await expect(selection).resolves.toBe('D:\\Projets\\mon-depot');
  });

  it('résout null quand la coque signale une annulation', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const selection = bridge.pickFolder();
    host.reply({ id: host.lastRequestId(), kind: 'pickFolder', path: null });

    await expect(selection).resolves.toBeNull();
  });

  it('ignore une réponse illisible ou destinée à une autre demande', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const selection = bridge.pickFolder();
    host.raw('pas du json');
    host.reply({ id: 'autre', kind: 'pickFolder', path: 'D:\\ignore' });
    host.reply({ id: host.lastRequestId(), kind: 'autre', path: 'D:\\ignore' });
    host.reply({ id: host.lastRequestId(), kind: 'pickFolder', path: 'D:\\retenu' });

    await expect(selection).resolves.toBe('D:\\retenu');
  });

  it('abandonne la demande précédente : un seul dialogue modal à la fois', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const abandoned = bridge.pickFolder();
    const current = bridge.pickFolder();
    host.reply({ id: host.lastRequestId(), kind: 'pickFolder', path: 'D:\\retenu' });

    await expect(abandoned).resolves.toBeNull();
    await expect(current).resolves.toBe('D:\\retenu');
  });
});
