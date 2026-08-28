import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-home',
  styleUrl: './home.scss',
  templateUrl: './home.html',
})
export class Home {
  protected readonly foundations = [
    { label: 'Hôte', value: '.NET 10' },
    { label: 'Interface', value: 'Angular 22' },
    { label: 'Contrat', value: 'OpenAPI' },
  ] as const;
}
