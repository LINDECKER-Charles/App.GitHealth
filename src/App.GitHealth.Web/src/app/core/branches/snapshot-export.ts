import { Injectable, inject } from '@angular/core';
import { BranchSnapshotResponse } from '../api/api.models';
import { FileDownloader } from '../workspace/file-download';
import { ToastService } from '../workspace/toast';
import { pluralMessage } from '../i18n/plural-message';
import { sourceLocale } from '../i18n/locale';
import { csvByteLength, toSnapshotCsv } from './snapshot-csv';

const csvMimeType = 'text/csv;charset=utf-8';
const bytesPerKilobyte = 1024;
const slugSeparator = '-';

const kilobyteFormatter = new Intl.NumberFormat(sourceLocale, {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

/** Writes the CSV from the facts already loaded: no request, so the export follows the view. */
@Injectable({ providedIn: 'root' })
export class SnapshotExporter {
  private readonly downloader = inject(FileDownloader);
  private readonly toast = inject(ToastService);

  export(projectName: string, branches: readonly BranchSnapshotResponse[]): void {
    const csv = toSnapshotCsv(branches);
    const size = formatBytes(csvByteLength(csv));
    this.downloader.download(`${slug(projectName)}-branches.csv`, csv, csvMimeType);
    this.toast.show(exportedToast(branches.length, size));
  }
}

/** The whole sentence is translated per plural category: word order is not universal. */
function exportedToast(count: number, size: string): string {
  return pluralMessage(count, {
    one: $localize`:@@export.csv.one:CSV export generated · ${count}:count: line · ${size}:size:`,
    other: $localize`:@@export.csv.many:CSV export generated · ${count}:count: lines · ${size}:size:`,
  });
}

/** Byte units formatted for the application locale. */
export function formatBytes(byteCount: number): string {
  if (byteCount < bytesPerKilobyte) {
    return `${byteCount} B`;
  }

  return `${kilobyteFormatter.format(byteCount / bytesPerKilobyte)} kB`;
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
