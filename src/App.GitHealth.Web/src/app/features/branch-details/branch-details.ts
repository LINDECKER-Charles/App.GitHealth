import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { DsButton } from '../../ui/core/ds-button';
import { BranchCard } from '../branch-card/branch-card';

/** Direct link to a branch card, outside the frame of an open repository. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BranchCard, DsButton],
  selector: 'app-branch-details',
  styleUrl: './branch-details.scss',
  templateUrl: './branch-details.html',
})
export class BranchDetails {
  private readonly location = inject(Location);
  private readonly params = toSignal(inject(ActivatedRoute).paramMap, { requireSync: true });

  protected readonly snapshotId = computed(() => this.params().get('snapshotId') ?? '');

  protected goBack(): void {
    this.location.back();
  }
}
