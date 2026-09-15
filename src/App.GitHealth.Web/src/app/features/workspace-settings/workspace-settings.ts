import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { LocalApiStore } from '../../core/local-api/local-api-store';
import { relativeTime } from '../../core/workspace/relative-time';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsInput } from '../../ui/forms/ds-input';
import { DsSwitch } from '../../ui/forms/ds-switch';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsCodeBlock } from '../../ui/surfaces/ds-code-block';
import { DsPanel } from '../../ui/surfaces/ds-panel';
import { Tone } from '../../ui/icon-name';

const minimumPort = 1024;
const maximumPort = 65535;
const tokenPlaceholder = '<your-token>';

/** Workspace settings: everything that belongs to the installation, not to a repository. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DsBadge,
    DsButton,
    DsCallout,
    DsCodeBlock,
    DsIcon,
    DsInput,
    DsPanel,
    DsStatusDot,
    DsSwitch,
  ],
  selector: 'app-workspace-settings',
  styleUrl: './workspace-settings.scss',
  templateUrl: './workspace-settings.html',
})
export class WorkspaceSettings {
  protected readonly access = inject(LocalApiStore);

  protected readonly port = signal(String(minimumPort));
  protected readonly minimumPort = minimumPort;

  protected readonly isEnabled = computed(() => this.access.state()?.isEnabled ?? false);

  protected readonly hasInvalidPort = computed(() => {
    const value = Number.parseInt(this.port(), 10);
    return Number.isNaN(value) || value < minimumPort || value > maximumPort;
  });

  /** The port on screen no longer matches the one the listener was opened with. */
  protected readonly isPortDirty = computed(
    () => Number.parseInt(this.port(), 10) !== (this.access.state()?.port ?? 0),
  );

  protected readonly statusTone = computed<Tone>(() => {
    const status = this.access.state()?.status;
    if (status === 'Listening') {
      return 'success';
    }

    return status === 'Failed' ? 'danger' : 'neutral';
  });

  protected readonly statusLabel = computed(() => {
    const status = this.access.state()?.status;
    if (status === 'Listening') {
      return $localize`:@@settings.localApi.status.listening:Open`;
    }

    return status === 'Failed'
      ? $localize`:@@settings.localApi.status.failed:Port refused`
      : $localize`:@@settings.localApi.status.closed:Closed`;
  });

  protected readonly tokenAge = computed(() => {
    const issuedAt = this.access.state()?.tokenIssuedAtUtc;
    return issuedAt === undefined || issuedAt === null ? null : relativeTime(issuedAt);
  });

  /** A line ready to paste, with the real token while it is still on screen. */
  protected readonly exampleCall = computed(() => {
    const address = this.access.address() ?? `http://127.0.0.1:${this.port()}`;
    const token = this.access.issuedToken() ?? tokenPlaceholder;
    return `curl -H "Authorization: Bearer ${token}" ${trimSlash(address)}/v1/projects`;
  });

  constructor() {
    this.access.load();
    effect(() => this.adopt(this.access.state()?.port));
  }

  protected toggle(isEnabled: boolean): void {
    if (this.hasInvalidPort()) {
      return;
    }

    this.access.apply(isEnabled, Number.parseInt(this.port(), 10));
  }

  /** Rebinding is opening again on another port: the listener moves, the token does not. */
  protected rebind(): void {
    if (!this.hasInvalidPort() && this.isPortDirty()) {
      this.access.apply(this.isEnabled(), Number.parseInt(this.port(), 10));
    }
  }

  /** The field follows the stored port: what is on screen is what the listener was told. */
  private adopt(port: number | undefined): void {
    if (port !== undefined) {
      this.port.set(String(port));
    }
  }
}

function trimSlash(address: string): string {
  return address.endsWith('/') ? address.slice(0, -1) : address;
}
