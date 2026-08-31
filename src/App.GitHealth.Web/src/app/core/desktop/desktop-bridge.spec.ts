import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { DesktopBridge } from './desktop-bridge';

/** Simulated desktop shell: captures the sent messages and replays the host replies. */
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

  it('stays unavailable with no desktop shell', async () => {
    const bridge = bridgeWith(undefined);

    expect(bridge.isAvailable).toBe(false);
    await expect(bridge.pickFolder()).resolves.toBeNull();
  });

  it('ignores an incomplete host object', () => {
    expect(bridgeWith({ sendMessage: () => undefined }).isAvailable).toBe(false);
  });

  it('asks the shell for a folder and resolves the chosen path', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);
    expect(bridge.isAvailable).toBe(true);

    const selection = bridge.pickFolder();
    const request = JSON.parse(host.sent[0]) as { id: string; kind: string };
    expect(request.kind).toBe('pickFolder');
    host.reply({ id: request.id, kind: 'pickFolder', path: 'D:\\Projects\\my-repository' });

    await expect(selection).resolves.toBe('D:\\Projects\\my-repository');
  });

  it('resolves null when the shell reports a cancellation', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const selection = bridge.pickFolder();
    host.reply({ id: host.lastRequestId(), kind: 'pickFolder', path: null });

    await expect(selection).resolves.toBeNull();
  });

  it('ignores a reply that is unreadable or meant for another request', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const selection = bridge.pickFolder();
    host.raw('not json');
    host.reply({ id: 'other', kind: 'pickFolder', path: 'D:\\ignore' });
    host.reply({ id: host.lastRequestId(), kind: 'other', path: 'D:\\ignore' });
    host.reply({ id: host.lastRequestId(), kind: 'pickFolder', path: 'D:\\kept' });

    await expect(selection).resolves.toBe('D:\\kept');
  });

  it('drops the previous request: a single modal dialog at a time', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const abandoned = bridge.pickFolder();
    const current = bridge.pickFolder();
    host.reply({ id: host.lastRequestId(), kind: 'pickFolder', path: 'D:\\kept' });

    await expect(abandoned).resolves.toBeNull();
    await expect(current).resolves.toBe('D:\\kept');
  });
});
