import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { DesktopBridge } from '../desktop/desktop-bridge';
import { FileDownloader } from './file-download';

class FakeDesktopBridge {
  readonly saved: { fileName: string; contents: string }[] = [];

  constructor(readonly isAvailable: boolean) {}

  saveFile(fileName: string, contents: string): Promise<string | null> {
    this.saved.push({ fileName, contents });
    return Promise.resolve(this.isAvailable ? `/home/u/Downloads/${fileName}` : null);
  }
}

/** Anchor the downloader builds when no shell is there to write the file. */
class FakeAnchor {
  href = '';
  download = '';
  wasClicked = false;

  click(): void {
    this.wasClicked = true;
  }
}

function downloaderWith(desktop: FakeDesktopBridge, anchor: FakeAnchor): FileDownloader {
  TestBed.configureTestingModule({
    providers: [
      { provide: DesktopBridge, useValue: desktop },
      { provide: DOCUMENT, useValue: { createElement: () => anchor } },
    ],
  });
  return TestBed.inject(FileDownloader);
}

describe('FileDownloader', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('lets the desktop host write the file, base64 encoded', async () => {
    const desktop = new FakeDesktopBridge(true);
    const anchor = new FakeAnchor();
    const downloader = downloaderWith(desktop, anchor);

    const path = await downloader.saveText('branches.csv', 'name;é', 'text/csv');

    expect(path).toBe('/home/u/Downloads/branches.csv');
    expect(anchor.wasClicked).toBe(false);
    // UTF-8 bytes, not the latin1 that btoa would have made of the string itself.
    expect(desktop.saved).toEqual([{ fileName: 'branches.csv', contents: 'bmFtZTvDqQ==' }]);
  });

  it('hands the file to the browser when no shell is there', async () => {
    const desktop = new FakeDesktopBridge(false);
    const anchor = new FakeAnchor();
    const downloader = downloaderWith(desktop, anchor);

    const path = await downloader.saveText('branches.csv', 'name', 'text/csv');

    expect(path).toBe('branches.csv');
    expect(desktop.saved).toEqual([]);
    expect(anchor.wasClicked).toBe(true);
    expect(anchor.download).toBe('branches.csv');
  });

  it('encodes a payload larger than one chunk', async () => {
    const desktop = new FakeDesktopBridge(true);
    const downloader = downloaderWith(desktop, new FakeAnchor());
    const bytes = new Uint8Array(0x8000 * 2 + 7).fill(65);

    await downloader.saveBytes('githealth-backup.db', bytes, 'application/vnd.sqlite3');

    expect(atob(desktop.saved[0].contents)).toHaveLength(bytes.length);
  });
});
