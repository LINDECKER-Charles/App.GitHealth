import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Déclenche un téléchargement local : rien ne transite par le réseau. */
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
