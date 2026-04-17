import { Component, computed, inject, Injectable, OnInit, signal } from '@angular/core';

interface RawSensorData {
  id: number;
  name: string;
  active: boolean;
  lastReading: number;
  unit: string;
}

interface SensorData {
  id: number;
  name: string;
  active: boolean;
  lastReading: number;
  unit: string;
}

class MiddlewareNetworkError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'MiddlewareNetworkError';
  }
}

class InvalidSensorPayloadError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'InvalidSensorPayloadError';
  }
}

interface SensorRepository {
  getSensors(): Promise<unknown>;
}

@Injectable({ providedIn: 'root' })
class MockSensorRepository implements SensorRepository {
  async getSensors(): Promise<unknown> {
    return new Promise((resolve) => {
      setTimeout(() => {
        resolve([
          { id: 101, name: 'Sensor Tuneladora A1', active: true, lastReading: 45.5, unit: 'bar' },
          { id: 102, name: 'Anemometro Grua Torre', active: false, lastReading: 0, unit: 'km/h' },
          { id: 103, name: 'Celula de Carga Puente', active: true, lastReading: 1200, unit: 'kg' },
          { id: 104, name: 'Sensor Vibracion Talud', active: true, lastReading: 0.02, unit: 'mm/s' }
        ] satisfies RawSensorData[]);
      }, 900);
    });
  }
}

function isRawSensorData(input: unknown): input is RawSensorData {
  if (typeof input !== 'object' || input === null) {
    return false;
  }

  const candidate = input as RawSensorData;
  return (
    typeof candidate.id === 'number' &&
    typeof candidate.name === 'string' &&
    typeof candidate.active === 'boolean' &&
    typeof candidate.lastReading === 'number' &&
    typeof candidate.unit === 'string'
  );
}

function normalizeSensorPayload(payload: unknown): SensorData[] {
  if (!Array.isArray(payload)) {
    throw new InvalidSensorPayloadError('El payload recibido no tiene formato de lista.');
  }

  const sensors = payload.map((candidate) => {
    if (!isRawSensorData(candidate)) {
      throw new InvalidSensorPayloadError('Se detecto un sensor con estructura invalida.');
    }

    return {
      id: candidate.id,
      name: candidate.name,
      active: candidate.active,
      lastReading: candidate.lastReading,
      unit: candidate.unit
    } satisfies SensorData;
  });

  const knownIds = new Set<number>();

  for (const sensor of sensors) {
    if (knownIds.has(sensor.id)) {
      throw new InvalidSensorPayloadError('El payload contiene sensores con id duplicado.');
    }

    if (sensor.unit.trim().length === 0) {
      throw new InvalidSensorPayloadError('La unidad de un sensor no puede estar vacia.');
    }

    knownIds.add(sensor.id);
  }

  return sensors;
}

function buildUserFacingError(error: unknown): string {
  if (error instanceof InvalidSensorPayloadError) {
    return 'Respuesta invalida del middleware de sensores.';
  }

  if (error instanceof MiddlewareNetworkError) {
    return 'Error de red al contactar con el middleware de sensores.';
  }

  return 'Error inesperado al recuperar el estado de sensores.';
}

function mapRepositoryError(error: unknown): unknown {
  if (error instanceof TypeError) {
    return new MiddlewareNetworkError('Error de transporte al consumir el middleware.');
  }

  return error;
}

@Component({
  selector: 'app-middleware-status',
  standalone: true,
  template: `
    <section class="dashboard-container">
      <div class="dashboard-topbar">
        <div>
          <p class="dashboard-kicker">Panel operativo</p>
          <h3>Estado del middleware Sacyr</h3>
        </div>

        <button type="button" (click)="refreshData()" class="btn-refresh" [disabled]="loading()">
          {{ loading() ? 'Actualizando...' : 'Actualizar datos' }}
        </button>
      </div>

      @if (loading()) {
        <div class="status-message loading">
          <span class="spinner"></span>
          Conectando con los sensores de obra...
        </div>
      } @else if (error() !== null) {
        <div class="status-message error">
          {{ error() }}
        </div>
      } @else {
        <div class="stats-summary">
          <span>Total dispositivos: {{ totalDispositivos() }}</span>
          <span>Operativos: {{ sensoresActivos() }}</span>
          <span>Offline: {{ sensoresInactivos() }}</span>
        </div>

        <ul class="sensor-list">
          @for (item of sensors(); track item.id) {
            <li class="sensor-item">
              <div class="sensor-info">
                <strong>{{ item.name }}</strong>
                <span>ID {{ item.id }} · Lectura {{ item.lastReading }} {{ item.unit }}</span>
              </div>
              <span class="sensor-status" [class.active]="item.active">
                {{ item.active ? 'Operativo' : 'Fuera de linea' }}
              </span>
            </li>
          } @empty {
            <li class="sensor-item empty-state">No hay sensores disponibles en este momento.</li>
          }
        </ul>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .dashboard-container { display: grid; gap: 16px; padding: 20px; border-radius: 22px; background: rgba(240, 248, 244, 0.88); }
    .dashboard-topbar { display: flex; justify-content: space-between; gap: 14px; align-items: start; }
    .dashboard-kicker { margin: 0 0 6px; text-transform: uppercase; letter-spacing: 0.12em; font-size: 0.72rem; color: #1c7c54; font-weight: 700; }
    h3 { margin: 0; font-size: 1.35rem; }
    .btn-refresh { border: 0; border-radius: 999px; background: #1c7c54; color: #f4f7f2; padding: 10px 16px; font-weight: 700; cursor: pointer; }
    .btn-refresh[disabled] { cursor: progress; opacity: 0.7; }
    .status-message { border-radius: 16px; padding: 14px 16px; display: flex; gap: 10px; align-items: center; }
    .status-message.loading { background: rgba(17, 115, 133, 0.12); color: #185d6b; }
    .status-message.error { background: rgba(181, 83, 52, 0.16); color: #8d3724; }
    .spinner { width: 14px; height: 14px; border-radius: 50%; border: 2px solid rgba(24, 93, 107, 0.2); border-top-color: #185d6b; animation: spin 0.8s linear infinite; }
    .stats-summary { display: flex; flex-wrap: wrap; gap: 10px; font-size: 0.92rem; color: #32504f; }
    .sensor-list { list-style: none; padding: 0; margin: 0; display: grid; gap: 10px; }
    .sensor-item { display: flex; justify-content: space-between; gap: 14px; align-items: center; padding: 14px; border-radius: 16px; background: #fdfefe; }
    .sensor-info { display: grid; gap: 5px; }
    .sensor-info span { color: #5b6b6c; font-size: 0.92rem; }
    .sensor-status { border-radius: 999px; padding: 6px 10px; border: 1px solid rgba(141, 55, 36, 0.24); color: #8d3724; font-size: 0.78rem; font-weight: 700; }
    .sensor-status.active { color: #1c7c54; border-color: rgba(28, 124, 84, 0.34); background: rgba(28, 124, 84, 0.1); }
    .empty-state { color: #5b6b6c; justify-content: center; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class MiddlewareStatusComponent implements OnInit {
  private readonly sensorRepository = inject(MockSensorRepository);

  readonly sensors = signal<SensorData[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly totalDispositivos = computed(() => this.sensors().length);
  readonly sensoresActivos = computed(() => this.sensors().filter((sensor) => sensor.active).length);
  readonly sensoresInactivos = computed(() => this.totalDispositivos() - this.sensoresActivos());

  ngOnInit(): void {
    void this.refreshData();
  }

  async refreshData(): Promise<void> {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    try {
      const rawPayload = await this.sensorRepository.getSensors();
      const normalizedSensors = normalizeSensorPayload(rawPayload);
      this.sensors.set(normalizedSensors);
    } catch (error: unknown) {
      const mappedError = mapRepositoryError(error);
      this.error.set(buildUserFacingError(mappedError));
      this.sensors.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}