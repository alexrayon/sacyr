type ClassDecoratorFn = <TFunction extends Function>(target: TFunction) => TFunction | void;

interface OnInit {
  ngOnInit(): void;
}

function Component(_metadata: {
  selector: string;
  standalone?: boolean;
  imports?: unknown[];
  template?: string;
  styles?: string[];
}): ClassDecoratorFn {
  return () => {
    // Decorador no-op para mantener compatibilidad en entorno sin Angular real.
  };
}

// Tipo para representar una signal invocable con metodos de escritura.
type WritableSignal<T> = {
  (): T;
  set(value: T): void;
  update(updater: (current: T) => T): void;
};

// Implementacion minima de signal para simular API de Angular en este entorno de ejercicio.
function signal<T>(initialValue: T): WritableSignal<T> {
  let currentValue = initialValue;

  const accessor = (() => currentValue) as WritableSignal<T>;
  accessor.set = (value: T) => {
    currentValue = value;
  };
  accessor.update = (updater: (current: T) => T) => {
    currentValue = updater(currentValue);
  };

  return accessor;
}

// Interfaz de datos para preservar estructura de lecturas del middleware.
interface SensorData {
  id: number;
  name: string;
  active: boolean;
  lastReading: number;
  unit: string;
}

@Component({
  selector: 'app-middleware-status-v21',
  standalone: true,
  imports: [],
  template: `
    <div class="dashboard-container">
      <h2>Panel de Control - Middleware Sacyr</h2>

      @if (loading()) {
        <div class="status-message loading">
          <span class="spinner"></span> Conectando con los sensores de obra...
        </div>
      }

      @if (error()) {
        <div class="status-message error">
          ⚠️ Error de conexión: No se pudo establecer enlace con el Middleware.
        </div>
      }

      @if (!loading() && !error()) {
        <div class="stats-summary">
          Total Dispositivos: {{ items().length }}
        </div>

        <ul class="sensor-list">
          @for (item of items(); track item.id) {
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
            <li class="sensor-item">
              <div class="sensor-info">
                <span class="sensor-name">Sin conectores disponibles</span>
                <span class="sensor-detail">No se recibieron lecturas del middleware</span>
              </div>
            </li>
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
export class MiddlewareV21Component implements OnInit {
  // Estado de carga migrado a signal segun especificacion.
  loading = signal(true);

  // Estado de error migrado a signal para mantener reactividad local uniforme.
  error = signal(false);

  // Listado de items migrado a signal<any[]> segun requerimiento de la migracion.
  items = signal<any[]>([]);

  // Inicializa el flujo de carga y deja el componente listo para ejecucion zoneless.
  ngOnInit(): void {
    this.loading.set(true);
    this.error.set(false);
    this.refreshData();
  }

  /**
   * Simula la peticion a la API del Middleware sin modificar la logica de negocio.
   */
  refreshData(): void {
    this.loading.set(true);
    this.error.set(false);

    // Se conserva la misma latencia de red de la implementacion base.
    setTimeout(() => {
      try {
        const mockData: SensorData[] = [
          { id: 101, name: 'Sensor Tuneladora A1', active: true, lastReading: 45.5, unit: 'bar' },
          { id: 102, name: 'Anemómetro Grúa Torre', active: false, lastReading: 0, unit: 'km/h' },
          { id: 103, name: 'Célula de Carga Puente', active: true, lastReading: 1200, unit: 'kg' },
          { id: 104, name: 'Sensor Vibración Talud', active: true, lastReading: 0.02, unit: 'mm/s' }
        ];

        this.items.set(mockData);
        this.loading.set(false);
      } catch (_err) {
        this.error.set(true);
        this.loading.set(false);
      }
    }, 1500);
  }
}
