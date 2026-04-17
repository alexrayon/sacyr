import { Component } from '@angular/core';
import { LegacyMiddlewareStatusComponent } from './legacy-middleware-status.component';
import { MiddlewareStatusComponent } from './middleware-status.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [LegacyMiddlewareStatusComponent, MiddlewareStatusComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly migrationHighlights = [
    {
      title: 'Arquitectura standalone-first',
      before: 'El componente dependía del ensamblado clásico y del cableado global para arrancar.',
      after: 'La funcionalidad se empaqueta como componente standalone y se monta sin NgModule.'
    },
    {
      title: 'Signals frente a BehaviorSubject',
      before: 'El estado local se gestionaba con streams, async pipe y más boilerplate.',
      after: 'Signals y computed reducen ruido y actualizan solo lo que consume el dato.'
    },
    {
      title: 'Templates modernos',
      before: 'La plantilla se fragmentaba con *ngIf, *ngFor y plantillas auxiliares.',
      after: 'Los bloques @if, @for y @empty hacen el flujo visible y más fácil de mantener.'
    },
    {
      title: 'Preparado para zoneless',
      before: 'La detección de cambios dependía del ciclo global de Zone.js.',
      after: 'La app arranca con provideZonelessChangeDetection y actualizaciones reactivas finas.'
    }
  ];

  protected readonly improvements = [
    'Se corrige el refresco inicial para que la carga de sensores se lance al abrir la pantalla.',
    'La normalización del payload detecta estructuras inválidas, ids duplicados y unidades vacías.',
    'El estado loading, error y datos se mantiene coherente sin subscripciones manuales.',
    'La demo deja visible el contraste entre la versión anterior y la migrada en una sola pantalla.'
  ];
}
