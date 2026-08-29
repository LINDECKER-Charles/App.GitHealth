import { Injectable, inject } from '@angular/core';
import { BranchSnapshotResponse } from '../api/api.models';
import { FileDownloader } from '../workspace/file-download';
import { ToastService } from '../workspace/toast';
import { plural } from '../workspace/plural';
import { csvByteLength, toSnapshotCsv } from './snapshot-csv';

const csvMimeType = 'text/csv;charset=utf-8';
const bytesPerKilobyte = 1024;
const slugSeparator = '-';

/** Écrit le CSV depuis les faits déjà chargés : aucune requête, donc l'export suit la vue. */
@Injectable({ providedIn: 'root' })
export class SnapshotExporter {
  private readonly downloader = inject(FileDownloader);
  private readonly toast = inject(ToastService);

  export(projectName: string, branches: readonly BranchSnapshotResponse[]): void {
    const csv = toSnapshotCsv(branches);
    this.downloader.download(`${slug(projectName)}-branches.csv`, csv, csvMimeType);
    this.toast.show(
      `Export CSV généré · ${plural(branches.length, 'ligne')} · ${formatBytes(csvByteLength(csv))}`,
    );
  }
}

/** Unités françaises : espace insécable avant l'unité, virgule décimale. */
export function formatBytes(byteCount: number): string {
  if (byteCount < bytesPerKilobyte) {
    return `${byteCount} o`;
  }

  const kilobytes = (byteCount / bytesPerKilobyte).toFixed(1).replace('.', ',');
  return `${kilobytes} ko`;
}

function slug(value: string): string {
  const normalized = value
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, slugSeparator)
    .replace(/^-+|-+$/g, '');
  return normalized.length > 0 ? normalized : 'githealth';
}
