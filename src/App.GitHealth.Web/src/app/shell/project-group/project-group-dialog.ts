import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ProjectResponse } from '../../core/api/api.models';
import { ProjectOrganizer } from '../../core/organization/project-organizer';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsInput } from '../../ui/forms/ds-input';

/** Doit rester aligné sur `ProjectOrganization.MaximumGroupNameLength` côté API. */
export const maximumGroupNameLength = 60;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsIcon, DsIconButton, DsInput],
  selector: 'app-project-group-dialog',
  styleUrl: './project-group-dialog.scss',
  templateUrl: './project-group-dialog.html',
})
export class ProjectGroupDialog {
  private readonly organizer = inject(ProjectOrganizer);
  private readonly store = inject(ProjectsStore);

  readonly projectId = input.required<string>();
  readonly close = output<void>();

  protected readonly maximumGroupNameLength = maximumGroupNameLength;
  protected readonly newGroup = signal('');

  /** Groupe retenu par la saisie en cours ; `undefined` tant que rien n'a été choisi. */
  private readonly picked = signal<string | null | undefined>(undefined);

  protected readonly project = computed<ProjectResponse | null>(
    () => this.store.projects().find((candidate) => candidate.id === this.projectId()) ?? null,
  );

  protected readonly selected = computed<string | null>(() => {
    const picked = this.picked();
    return picked === undefined ? (this.project()?.groupName ?? null) : picked;
  });

  /** Les groupes connus, plus celui que la saisie vient de créer et qui n'est pas encore écrit. */
  protected readonly groups = computed<readonly string[]>(() => {
    const known = this.organizer.groupNames();
    const selected = this.selected();
    return selected === null || known.includes(selected) ? known : [...known, selected];
  });

  protected readonly canCreate = computed(() => {
    const name = this.newGroup().trim();
    return name.length > 0 && name.length <= maximumGroupNameLength;
  });

  protected readonly isDirty = computed(
    () => this.selected() !== (this.project()?.groupName ?? null),
  );

  protected pick(groupName: string | null): void {
    this.picked.set(groupName);
  }

  protected createGroup(): void {
    if (!this.canCreate()) {
      return;
    }

    this.picked.set(this.newGroup().trim());
    this.newGroup.set('');
  }

  protected countIn(groupName: string): number {
    return this.store.projects().filter((project) => project.groupName === groupName).length;
  }

  protected save(): void {
    const project = this.project();
    if (project === null || !this.isDirty()) {
      return;
    }

    this.organizer.moveToGroup(project, this.selected());
    this.close.emit();
  }
}
