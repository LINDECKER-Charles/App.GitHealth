import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Triggers a local download: nothing travels over the network. */
@Injectable({ providedIn: 'root' })
export class FileDownloader {
  private readonly document = inject(DOCUMENT);

  download(fileName: string, contents: string, mimeType: string): void {
    const url = URL.createObjectURL(new Blob([contents], { type: mimeType }));
    const anchor = this.document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
