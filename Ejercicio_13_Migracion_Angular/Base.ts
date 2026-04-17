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

class CommonModule {}

interface Observable<T> {
  readonly current: T;
}

class BehaviorSubject<T> {
  private value: T;

  constructor(initialValue: T) {
    this.value = initialValue;
  }

  next(newValue: T): void {
    this.value = newValue;
  }

  asObservable(): Observable<T> {
    return { current: this.value };
  }
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
  imports: [CommonModule],
  template: `
    <div class="dashboard-container">
      <h2>Panel de Control - Middleware Sacyr</h2>
      
      <div *ngIf="loading" class="status-message loading">
        <span class="spinner"></span> Conectando con los sensores de obra...
      </div>

      <div *ngIf="error" class="status-message error">
        ⚠️ Error de conexión: No se pudo establecer enlace con el Middleware.
      </div>

      <div *ngIf="!loading && !error">
        <div class="stats-summary">
          Total Dispositivos: {{ (items$ | async)?.length }}
        </div>
        
        <ul class="sensor-list">
          <li *ngFor="let item of items$ | async" class="sensor-item">
            <div class="sensor-info">
              <span class="sensor-name">{{ item.name }}</span>
              <span class="sensor-detail">ID: {{ item.id }} | Lectura: {{ item.lastReading }}{{ item.unit }}</span>
            </div>
            <div class="sensor-status" [class.active]="item.active">
              {{ item.active ? '🟢 OPERATIVO' : '🔴 FUERA DE LÍNEA' }}
            </div>
          </li>
        </ul>
        
        <button (click)="refreshData()" class="btn-refresh">Actualizar Datos</button>
      </div>
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
  loading = true;
  error = false;
  
  // Usamos el tipo definido en la interfaz
  private itemsSubject = new BehaviorSubject<SensorData[]>([]);
  items$ = this.itemsSubject.asObservable();

  ngOnInit(): void {
    this.refreshData();
  }

  /**
   * Simula la petición a la API del Middleware
   */
  refreshData(): void {
    this.loading = true;
    this.error = false;

    // Simulación de latencia de red (1.5 segundos)
    setTimeout(() => {
      try {
        const mockData: SensorData[] = [
          { id: 101, name: 'Sensor Tuneladora A1', active: true, lastReading: 45.5, unit: 'bar' },
          { id: 102, name: 'Anemómetro Grúa Torre', active: false, lastReading: 0, unit: 'km/h' },
          { id: 103, name: 'Célula de Carga Puente', active: true, lastReading: 1200, unit: 'kg' },
          { id: 104, name: 'Sensor Vibración Talud', active: true, lastReading: 0.02, unit: 'mm/s' }
        ];

        this.itemsSubject.next(mockData);
        this.loading = false;
      } catch {
        this.error = true;
        this.loading = false;
      }
    }, 1500);
  }
}
