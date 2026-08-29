import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { DsButton } from '../../ui/core/ds-button';
import { BranchFiche } from '../branch-fiche/branch-fiche';

/** Lien direct vers une fiche de branche, hors du cadre d'un dépôt ouvert. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BranchFiche, DsButton],
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
