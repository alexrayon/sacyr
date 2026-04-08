# PLAN TECNICO DE PRUEBAS - GEOFENCING DE SEGURIDAD

## 1. Alcance
Este plan define como verificar el motor de geofencing en base a CRITERIOS_SEGURIDAD.md, separando pruebas puras de geometria de las pruebas de flujo de alertas con eventos y estado.

## 2. Estrategia de verificacion

### 2.1 Capas de prueba

#### Capa A: Tests de Logica Geometrica
Objetivo:
- Validar el calculo de distancia y la regla de pertenencia al perimetro (Dentro/Fuera) sin dependencias de tiempo, hardware ni transporte de eventos.

Unidad bajo prueba:
- Componente puro de evaluacion geometrica, por ejemplo GeofenceMath o GeofenceEvaluator.

Entradas:
- Coordenadas centro de zona.
- Coordenadas operario.
- Radio de seguridad.

Salidas:
- Distancia en metros.
- Clasificacion booleana estaDentro.

Criterios cubiertos:
- Inclusion de borde: distancia <= 50.0 m es Dentro.
- Falsa alarma: a 51.0 m se mantiene Fuera.

Reglas de diseno:
- Sin mocks de infraestructura.
- Determinismo total (mismo input, mismo output).
- Pruebas data-driven para radios y distancias de frontera.

#### Capa B: Tests de Flujo de Alerta
Objetivo:
- Validar transiciones de estado y publicacion de eventos AlertaEntrada, AlertaSalida y GPSPerdido.

Unidad bajo prueba:
- Orquestador de flujo, por ejemplo GeofencingEngine.

Entradas:
- Flujo temporal de muestras GPS.
- Configuracion de timeout de perdida de GPS.

Salidas:
- Eventos emitidos.
- Estado final del operario (Dentro/Fuera/Desconocido).
- Latencia de deteccion.

Criterios cubiertos:
- Alerta positiva por transicion Fuera -> Dentro.
- Recuperacion por transicion Dentro -> Fuera.
- No duplicacion de alertas por permanencia.
- Manejo de perdida y recuperacion de GPS.

Reglas de diseno:
- Uso de mocks/fakes para sensor GPS, reloj y publicador de eventos.
- Verificacion de secuencia de eventos y unicidad.
- Pruebas de estres con rafagas.

### 2.2 Matriz de responsabilidad por capa

| Criterio | Capa A Geometria | Capa B Flujo |
|---|---|---|
| Distancia correcta | SI | NO |
| Regla <= 50 m Dentro | SI | SI (integracion de regla) |
| AlertaEntrada unica | NO | SI |
| AlertaSalida unica | NO | SI |
| No alerta a 51 m estable | SI | SI |
| GPS perdido/recuperado | NO | SI |
| Latencia p95 < 2 s | NO | SI |

## 3. Diseno de mocks de hardware

### 3.1 Contrato recomendado para sensor GPS
Se recomienda desacoplar hardware con interfaz:

- IGpsSensor:
  - IAsyncEnumerable<GpsSample> StreamAsync(CancellationToken ct)

Modelo de muestra:
- GpsSample(latitud, longitud, timestampUtc, batteryLevel, isValid)

### 3.2 Fake principal para pruebas
Componente:
- BurstGpsSensorFake

Capacidades:
- Reproducir secuencias predefinidas de muestras.
- Emitir rafagas de alta frecuencia (por ejemplo 50, 100 o 200 muestras/segundo).
- Inyectar anomalias controladas:
  - coordenadas nulas
  - coordenadas fuera de rango
  - teletransporte (saltos imposibles)
  - perdida de senal (silencio de muestras)
  - bateria baja

Configuracion:
- EscenarioScript con pasos ordenados por tiempo relativo.
- Cada paso define muestra y delayMs antes del siguiente envio.
- Modo acelerado para pruebas de carga sin esperar tiempo real (reloj virtual).

### 3.3 Dependencias de tiempo y observabilidad
Para pruebas estables bajo estres:
- IClock o TimeProvider inyectable para medir latencia sin depender de reloj de sistema.
- IAlertPublisher fake/sp y para capturar eventos emitidos.
- Buffer de eventos in-memory para aserciones de orden, unicidad y contenido.

### 3.4 Criterios de aceptacion del mock
El mock de hardware se considera valido si:
- Puede generar al menos 10,000 muestras en menos de 10 s de ejecucion de test en modo acelerado.
- Permite reproducibilidad total con semilla fija.
- Permite guionar secuencias exactas para Given-When-Then.

## 4. Estructura del test plan (clases)

### 4.1 Clase base comun
Clase base recomendada:
- GeofencingTestBase

Responsabilidades:
- Definir perimetro por defecto de obra (centro y radio).
- Construir sujetos bajo prueba con dependencias fake.
- Exponer utilidades:
  - BuildSampleAtDistance(metros, bearing)
  - EmitBurst(samples)
  - AssertSingleEvent(tipo)
  - AssertNoCriticalAlerts()

Datos comunes del setup:
- Centro de zona fijo para pruebas.
- Radio por defecto 50.0 m.
- idOperario e idZona estandar para trazabilidad.

### 4.2 Clases derivadas por escenario

1. GeometricLogicTests
- Verifica Haversine y frontera (49.9, 50.0, 50.1, 51.0).
- Tipo de test: unitario puro, data-driven.

2. PositiveAlertRiskScenarioTests
- Verifica entrada Fuera -> Dentro y evento unico AlertaEntrada.
- Tipo de test: flujo.

3. FalseAlarmRiskScenarioTests
- Verifica estabilidad a 51.0 m sin AlertaEntrada.
- Tipo de test: flujo + precision.

4. RecoveryRiskScenarioTests
- Verifica salida Dentro -> Fuera y rearme para reentrada.
- Tipo de test: flujo.

5. GpsSignalLossRiskScenarioTests
- Verifica timeout de perdida de GPS, estado Desconocido y recuperacion.
- Tipo de test: resiliencia.

6. EdgeCasesRiskScenarioTests
- Verifica coordenadas nulas, teletransporte y bateria baja.
- Tipo de test: robustez.

7. PerformanceLatencyScenarioTests
- Verifica p95 de deteccion < 2 s bajo carga nominal y rafagas.
- Tipo de test: no funcional.

### 4.3 Esquema de carpetas recomendado
- tests/
  - Common/
    - GeofencingTestBase.cs
    - TestDataBuilders.cs
    - BurstGpsSensorFake.cs
    - InMemoryAlertPublisher.cs
    - FakeClock.cs
  - Geometry/
    - GeometricLogicTests.cs
  - AlertFlow/
    - PositiveAlertRiskScenarioTests.cs
    - FalseAlarmRiskScenarioTests.cs
    - RecoveryRiskScenarioTests.cs
    - GpsSignalLossRiskScenarioTests.cs
    - EdgeCasesRiskScenarioTests.cs
  - NonFunctional/
    - PerformanceLatencyScenarioTests.cs

## 5. Trazabilidad criterio -> clase de prueba

| Criterio de CRITERIOS_SEGURIDAD | Clase primaria | Clase secundaria |
|---|---|---|
| Alerta positiva | PositiveAlertRiskScenarioTests | GeometricLogicTests |
| Falsa alarma a 51 m | FalseAlarmRiskScenarioTests | GeometricLogicTests |
| Recuperacion | RecoveryRiskScenarioTests | GeometricLogicTests |
| GPS perdido | GpsSignalLossRiskScenarioTests | EdgeCasesRiskScenarioTests |
| Latencia < 2 s | PerformanceLatencyScenarioTests | PositiveAlertRiskScenarioTests |
| Casos de borde | EdgeCasesRiskScenarioTests | GpsSignalLossRiskScenarioTests |

## 6. Criterios de salida del plan
- 100 por ciento de escenarios obligatorios con test automatizado.
- Cero ambiguedades en reglas de frontera y transicion.
- Ejecucion estable en pipeline CI con resultados reproducibles.
- Reporte de cobertura de ramas para decisiones de estado del motor.
