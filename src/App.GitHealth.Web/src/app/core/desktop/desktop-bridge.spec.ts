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

  it('asks the shell to open an address outside the window', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const opening = bridge.openExternal('https://example.test/guide');
    const request = JSON.parse(host.sent[0]) as { kind: string; url: string };
    expect(request.kind).toBe('openExternal');
    expect(request.url).toBe('https://example.test/guide');
    host.reply({ id: host.lastRequestId(), kind: 'openExternal', isHandled: true });

    await expect(opening).resolves.toBe(true);
  });

  it('reports an address the shell could not hand over', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const opening = bridge.openExternal('https://example.test/guide');
    host.reply({ id: host.lastRequestId(), kind: 'openExternal', isHandled: false });

    await expect(opening).resolves.toBe(false);
  });

  it('asks the shell to write a file and resolves where it landed', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const saving = bridge.saveFile('branches.csv', 'YSxi');
    const request = JSON.parse(host.sent[0]) as {
      kind: string;
      fileName: string;
      contents: string;
    };
    expect(request.kind).toBe('saveFile');
    expect(request.fileName).toBe('branches.csv');
    expect(request.contents).toBe('YSxi');
    host.reply({
      id: host.lastRequestId(),
      kind: 'saveFile',
      path: '/home/u/Downloads/branches.csv',
    });

    await expect(saving).resolves.toBe('/home/u/Downloads/branches.csv');
  });

  it('resolves null when nothing reached the disk', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const saving = bridge.saveFile('branches.csv', 'YSxi');
    host.reply({ id: host.lastRequestId(), kind: 'saveFile', path: null });

    await expect(saving).resolves.toBeNull();
  });

  it('keeps requests of different kinds independent', async () => {
    const host = new FakeDesktopHost();
    const bridge = bridgeWith(host);

    const selection = bridge.pickFolder();
    const opening = bridge.openExternal('https://example.test/guide');
    const ids = host.sent.map((message) => (JSON.parse(message) as { id: string }).id);
    host.reply({ id: ids[1], kind: 'openExternal', isHandled: true });
    host.reply({ id: ids[0], kind: 'pickFolder', path: 'D:\\kept' });

    await expect(opening).resolves.toBe(true);
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
