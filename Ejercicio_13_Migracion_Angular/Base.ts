interface OnInit {
  ngOnInit(): void;
}

type ComponentMetadata = {
  selector: string;
  standalone: boolean;
  imports: unknown[];
  template: string;
  styles: string[];
};

function Component(_metadata: ComponentMetadata): ClassDecorator {
  return () => {
    // No-op decorator para mantener el ejemplo autocontenido en este workspace.
  };
}

type Signal<T> = () => T;

interface WritableSignal<T> extends Signal<T> {
  set(value: T): void;
  update(updater: (current: T) => T): void;
}

function signal<T>(initialValue: T): WritableSignal<T> {
  let currentValue = initialValue;

  const signalReader = (() => currentValue) as WritableSignal<T>;
  signalReader.set = (newValue: T): void => {
    currentValue = newValue;
  };
  signalReader.update = (updater: (current: T) => T): void => {
    currentValue = updater(currentValue);
  };

  return signalReader;
}

function computed<T>(compute: () => T): Signal<T> {
  return () => compute();
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

interface RawSensorData {
  id: number;
  name: string;
  active: boolean;
  lastReading: number;
  unit: string;
}

interface SensorRepository {
  getSensors(): Promise<unknown>;
}

class MockSensorRepository implements SensorRepository {
  async getSensors(): Promise<unknown> {
    return new Promise((resolve) => {
      setTimeout(() => {
        resolve([
          { id: 101, name: 'Sensor Tuneladora A1', active: true, lastReading: 45.5, unit: 'bar' },
          { id: 102, name: 'Anemómetro Grúa Torre', active: false, lastReading: 0, unit: 'km/h' },
          { id: 103, name: 'Célula de Carga Puente', active: true, lastReading: 1200, unit: 'kg' },
          { id: 104, name: 'Sensor Vibración Talud', active: true, lastReading: 0.02, unit: 'mm/s' }
        ]);
      }, 1500);
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
    throw new InvalidSensorPayloadError('El payload de sensores no tiene formato de lista.');
  }

  const normalizedSensors: SensorData[] = [];

  for (const sensorCandidate of payload) {
    if (!isRawSensorData(sensorCandidate)) {
      throw new InvalidSensorPayloadError('Se detectó un sensor con estructura inválida.');
    }

    normalizedSensors.push({
      id: sensorCandidate.id,
      name: sensorCandidate.name,
      active: sensorCandidate.active,
      lastReading: sensorCandidate.lastReading,
      unit: sensorCandidate.unit
    });
  }

  return normalizedSensors;
}

function buildUserFacingError(error: unknown): string {
  if (error instanceof InvalidSensorPayloadError) {
    return 'Respuesta inválida del Middleware de sensores.';
  }

  if (error instanceof MiddlewareNetworkError) {
    return 'Error de red al contactar con el Middleware de sensores.';
  }

  return 'Error inesperado al recuperar el estado de sensores.';
}

function createDefaultRepository(): SensorRepository {
  return new MockSensorRepository();
}

async function loadSensorsFromRepository(repository: SensorRepository): Promise<SensorData[]> {
  const rawPayload = await repository.getSensors();

  return normalizeSensorPayload(rawPayload);
}

function hasDuplicateSensorIds(sensors: SensorData[]): boolean {
  const knownIds = new Set<number>();

  for (const sensor of sensors) {
    if (knownIds.has(sensor.id)) {
      return true;
    }

    knownIds.add(sensor.id);
  }

  return false;
}

function assertDomainInvariants(sensors: SensorData[]): void {
  if (hasDuplicateSensorIds(sensors)) {
    throw new InvalidSensorPayloadError('El payload contiene sensores con id duplicado.');
  }

  for (const sensor of sensors) {
    if (sensor.unit.trim().length === 0) {
      throw new InvalidSensorPayloadError('La unidad de un sensor no puede estar vacía.');
    }
  }
}

function isErrorWithName(error: unknown, name: string): boolean {
  return error instanceof Error && error.name === name;
}

function mapRepositoryError(error: unknown): unknown {
  if (isErrorWithName(error, 'TimeoutError')) {
    return new MiddlewareNetworkError('Timeout de red agotado.');
  }

  if (error instanceof TypeError) {
    return new MiddlewareNetworkError('Error de transporte al consumir el Middleware.');
  }

  return error;
}

function ensureRepository(repository: SensorRepository | null | undefined): SensorRepository {
  if (repository === null || repository === undefined) {
    return createDefaultRepository();
  }

  return repository;
}

// Definimos una interfaz para asegurar la integridad de los datos de Sacyr
interface SensorData {
  id: number;
  name: string;
  active: boolean;
  lastReading: number;
  unit: string;
}

@Component({
  selector: 'app-middleware-status',
  standalone: true,
  imports: [],
  template: `
    <div class="dashboard-container">
      <h2>Panel de Control - Middleware Sacyr</h2>

      @if (loading()) {
        <div class="status-message loading">
          <span class="spinner"></span> Conectando con los sensores de obra...
        </div>
      } @else if (error() !== null) {
        <div class="status-message error">
          ⚠️ {{ error() }}
        </div>
      } @else {
        <div class="stats-summary">
          Total Dispositivos: {{ totalDispositivos() }} | Operativos: {{ sensoresActivos() }}
        </div>

        <ul class="sensor-list">
          @for (item of sensors(); track item.id) {
            <li class="sensor-item">
              <div class="sensor-info">
                <span class="sensor-name">{{ item.name }}</span>
                <span class="sensor-detail">ID: {{ item.id }} | Lectura: {{ item.lastReading }}{{ item.unit }}</span>
              </div>
              <div class="sensor-status" [class.active]="item.active">
                {{ item.active ? '🟢 OPERATIVO' : '🔴 FUERA DE LÍNEA' }}
              </div>
            </li>
          } @empty {
            <li class="sensor-item">No hay sensores disponibles en este momento.</li>
          }
        </ul>

        <button (click)="refreshData()" class="btn-refresh">Actualizar Datos</button>
      }
    </div>
  `,
  styles: [`
    .dashboard-container { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd; border-radius: 8px; background: #f9f9f9; }
    h2 { color: #004a99; border-bottom: 2px solid #004a99; padding-bottom: 10px; }
    .status-message { padding: 15px; border-radius: 4px; margin-bottom: 10px; }
    .loading { background: #e3f2fd; color: #0d47a1; }
    .error { background: #ffebee; color: #b71c1c; }
    .sensor-list { list-style: none; padding: 0; }
    .sensor-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; border-bottom: 1px solid #eee; background: white; margin-bottom: 5px; border-radius: 4px; }
    .sensor-name { font-weight: bold; display: block; }
    .sensor-detail { font-size: 0.85em; color: #666; }
    .sensor-status { font-size: 0.8em; font-weight: bold; padding: 4px 8px; border-radius: 12px; border: 1px solid #ccc; }
    .sensor-status.active { color: #2e7d32; border-color: #2e7d32; background: #e8f5e9; }
    .stats-summary { font-size: 0.9em; margin-bottom: 10px; color: #444; }
    .btn-refresh { margin-top: 15px; padding: 8px 16px; background: #004a99; color: white; border: none; border-radius: 4px; cursor: pointer; }
    .btn-refresh:hover { background: #003366; }
  `]
})
export class MiddlewareStatusComponent implements OnInit {
  private readonly sensorRepository: SensorRepository;

  readonly sensors = signal<SensorData[]>([]);
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  readonly totalDispositivos = computed<number>(() => this.sensors().length);
  readonly sensoresActivos = computed<number>(() => this.sensors().filter((sensor) => sensor.active).length);
  readonly haySensores = computed<boolean>(() => this.totalDispositivos() > 0);

  constructor(sensorRepository?: SensorRepository) {
    this.sensorRepository = ensureRepository(sensorRepository);
  }

  ngOnInit(): void {
    this.refreshData();
  }

  /**
   * Simula la petición a la API del Middleware
   */
  refreshData(): void {
    // Guard clause para evitar refrescos concurrentes.
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    void this.loadSensors();
  }

  private async loadSensors(): Promise<void> {
    try {
      const fetchedSensors = await loadSensorsFromRepository(this.sensorRepository);
      assertDomainInvariants(fetchedSensors);
      this.sensors.set(fetchedSensors);
    } catch (error: unknown) {
      const mappedError = mapRepositoryError(error);
      this.error.set(buildUserFacingError(mappedError));
      this.sensors.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
