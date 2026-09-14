import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { DesktopBridge } from '../desktop/desktop-bridge';

/** Chunked, so a large file does not overflow the argument list of `fromCharCode`. */
const base64ChunkSize = 0x8000;

/**
 * Saves a file locally: nothing travels over the network.
 *
 * Inside the desktop window the host writes it. A webview drops a download for want of a
 * destination — Photino declares no `WKDownloadDelegate` on macOS, and no download handler
 * on Linux — so an anchor carrying `download` reaches the disk in a browser only.
 */
@Injectable({ providedIn: 'root' })
export class FileDownloader {
  private readonly document = inject(DOCUMENT);
  private readonly desktop = inject(DesktopBridge);

  /** Resolves where the file landed, or `null` when nothing was saved. */
  saveText(fileName: string, contents: string, mimeType: string): Promise<string | null> {
    return this.saveBytes(fileName, new TextEncoder().encode(contents), mimeType);
  }

  /** Resolves where the file landed, or `null` when nothing was saved. */
  async saveBytes(
    fileName: string,
    contents: Uint8Array<ArrayBuffer>,
    mimeType: string,
  ): Promise<string | null> {
    if (this.desktop.isAvailable) {
      return this.desktop.saveFile(fileName, toBase64(contents));
    }

    // A browser names its own destination, and never says which one it chose.
    this.handToBrowser(fileName, new Blob([contents], { type: mimeType }));
    return fileName;
  }

  private handToBrowser(fileName: string, file: Blob): void {
    const url = URL.createObjectURL(file);
    const anchor = this.document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}

/** The bridge carries text: the file travels base64-encoded. */
function toBase64(contents: Uint8Array): string {
  let binary = '';
  for (let offset = 0; offset < contents.length; offset += base64ChunkSize) {
    binary += String.fromCharCode(...contents.subarray(offset, offset + base64ChunkSize));
  }

  return btoa(binary);
}
