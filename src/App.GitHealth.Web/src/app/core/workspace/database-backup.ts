import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { databaseBackupUrl } from './app-identity';
import { FileDownloader } from './file-download';

const databaseMimeType = 'application/vnd.sqlite3';
const fallbackFileName = 'githealth-backup.db';
const fileNamePattern = /filename=(?:"([^"]+)"|([^;]+))/i;

/**
 * Saves the whole local database.
 *
 * In a browser the top-bar anchor does it alone, and streams. Inside the desktop window it
 * cannot: a webview download never lands, so the page reads the bytes itself and hands
 * them to the host. The whole database therefore passes through memory — accepted, for a
 * base that holds branch facts and no repository content.
 */
@Injectable({ providedIn: 'root' })
export class DatabaseBackup {
  private readonly http = inject(HttpClient);
  private readonly downloader = inject(FileDownloader);

  /** Resolves where the backup landed, or `null` when nothing was saved. */
  async save(): Promise<string | null> {
    const backup = await this.read();
    return backup === null
      ? null
      : this.downloader.saveBytes(backup.fileName, backup.bytes, databaseMimeType);
  }

  private async read(): Promise<{ fileName: string; bytes: Uint8Array<ArrayBuffer> } | null> {
    try {
      const response = await firstValueFrom(
        this.http.get(databaseBackupUrl, { observe: 'response', responseType: 'arraybuffer' }),
      );
      return response.body === null
        ? null
        : { fileName: readFileName(response), bytes: new Uint8Array(response.body) };
    } catch {
      // An unreachable API is reported by the caller, not by a thrown promise.
      return null;
    }
  }
}

/** The API stamps the backup with the moment it was taken: that name is the one to keep. */
function readFileName(response: HttpResponse<ArrayBuffer>): string {
  const match = fileNamePattern.exec(response.headers.get('content-disposition') ?? '');
  const name = (match?.[1] ?? match?.[2] ?? '').trim();
  return name.length > 0 ? name : fallbackFileName;
}
