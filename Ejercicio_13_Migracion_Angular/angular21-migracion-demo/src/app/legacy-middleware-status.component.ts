import { AsyncPipe, CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { BehaviorSubject, combineLatest, map } from 'rxjs';

interface SensorData {
  id: number;
  name: string;
  active: boolean;
  lastReading: number;
  unit: string;
}

const LEGACY_SENSORS: SensorData[] = [
  { id: 101, name: 'Sensor Tuneladora A1', active: true, lastReading: 45.5, unit: 'bar' },
  { id: 102, name: 'Anemometro Grua Torre', active: false, lastReading: 0, unit: 'km/h' },
  { id: 103, name: 'Celula de Carga Puente', active: true, lastReading: 1200, unit: 'kg' },
  { id: 104, name: 'Sensor Vibracion Talud', active: true, lastReading: 0.02, unit: 'mm/s' }
];

@Component({
  selector: 'app-legacy-middleware-status',
  standalone: true,
  imports: [CommonModule, AsyncPipe],
  template: `
    <section class="legacy-dashboard">
      <div class="legacy-banner">
        <strong>Patron previo</strong>
        <p>Observable local, async pipe y plantilla basada en *ngIf / *ngFor.</p>
      </div>

      <div class="legacy-summary" *ngIf="vm$ | async as vm">
        <span>Total: {{ vm.total }}</span>
        <span>Operativos: {{ vm.active }}</span>
      </div>

      <div class="legacy-status loading" *ngIf="loading$ | async; else loadedBlock">
        Cargando estado del middleware...
      </div>

      <ng-template #loadedBlock>
        <div class="legacy-status error" *ngIf="error$ | async as errorMessage">
          {{ errorMessage }}
        </div>

        <ul class="legacy-list" *ngIf="!(error$ | async)">
          <li class="legacy-item" *ngFor="let sensor of sensors$ | async">
            <div>
              <strong>{{ sensor.name }}</strong>
              <p>ID {{ sensor.id }} · {{ sensor.lastReading }} {{ sensor.unit }}</p>
            </div>
            <span class="legacy-pill" [class.active]="sensor.active">
              {{ sensor.active ? 'Operativo' : 'Fuera de linea' }}
            </span>
          </li>
        </ul>
      </ng-template>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .legacy-dashboard { display: grid; gap: 14px; padding: 18px; border-radius: 22px; background: rgba(255, 248, 244, 0.88); }
    .legacy-banner { display: grid; gap: 6px; color: #4c4947; }
    .legacy-banner p, .legacy-item p { margin: 0; color: #716760; }
    .legacy-summary { display: flex; gap: 12px; flex-wrap: wrap; font-size: 0.9rem; color: #6f4a3b; }
    .legacy-status { border-radius: 14px; padding: 12px; }
    .legacy-status.loading { background: rgba(196, 140, 86, 0.14); color: #7b4d2a; }
    .legacy-status.error { background: rgba(181, 83, 52, 0.16); color: #8d3724; }
    .legacy-list { list-style: none; padding: 0; margin: 0; display: grid; gap: 10px; }
    .legacy-item { display: flex; justify-content: space-between; gap: 12px; align-items: center; border-radius: 16px; padding: 14px; background: #fffdf9; }
    .legacy-pill { border-radius: 999px; padding: 6px 10px; font-size: 0.78rem; border: 1px solid rgba(117, 103, 96, 0.24); color: #8d3724; }
    .legacy-pill.active { color: #1c7c54; border-color: rgba(28, 124, 84, 0.34); background: rgba(28, 124, 84, 0.1); }
  `]
})
export class LegacyMiddlewareStatusComponent implements OnInit {
  readonly sensors$ = new BehaviorSubject<SensorData[]>([]);
  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly error$ = new BehaviorSubject<string | null>(null);

  readonly vm$ = combineLatest([this.sensors$, this.loading$, this.error$]).pipe(
    map(([sensors]) => ({
      total: sensors.length,
      active: sensors.filter((sensor) => sensor.active).length
    }))
  );

  ngOnInit(): void {
    this.fetchSensors();
  }

  private fetchSensors(): void {
    this.loading$.next(true);
    this.error$.next(null);

    setTimeout(() => {
      this.sensors$.next(LEGACY_SENSORS);
      this.loading$.next(false);
    }, 1200);
  }
}