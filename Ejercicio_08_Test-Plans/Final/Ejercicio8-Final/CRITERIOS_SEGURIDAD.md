# CRITERIOS DE ACEPTACION DE SEGURIDAD

## 1. Objetivo
Definir criterios de aceptacion funcionales y no funcionales para un sistema de Geofencing de seguridad en obras de Sacyr, orientado a detectar entrada y salida de operarios respecto a una Zona de Peligro circular.

## 2. Definicion del escenario
- Entidad protegida: Operario con dispositivo movil con GPS activo.
- Zona de Peligro: Circunferencia con centro fijo en coordenadas geograficas validas y radio de 50.0 metros.
- Estado del operario respecto a la zona:
  - Dentro: distancia al centro menor o igual a 50.0 m.
  - Fuera: distancia al centro mayor a 50.0 m.
- Precision objetivo para evitar falsas alarmas en borde: no debe generarse alerta de entrada si la distancia real se mantiene en 51.0 m de forma estable.

## 3. Definiciones tecnicas obligatorias
- Distancia geodesica: debe calcularse con formula de Haversine o equivalente geodesicamente correcta para distancias cortas.
- Unidad de medida: metros.
- Frecuencia de evaluacion: cada nueva muestra GPS recibida.
- Marca temporal: toda alerta debe incluir timestamp UTC en formato ISO 8601.
- Identificadores requeridos en cada evento: idOperario, idZona, tipoEvento, distanciaMetros, timestampUtc.

## 4. Criterios de aceptacion funcionales (Given-When-Then)

### 4.1 Alerta positiva (entrada en radio de 50 m)
Given
- Un operario inicialmente en estado Fuera (distancia mayor a 50.0 m).
- Una Zona de Peligro activa con radio 50.0 m.
When
- Se procesa una muestra GPS valida del operario con distancia menor o igual a 50.0 m.
Then
- El sistema genera exactamente un evento AlertaEntrada por transicion Fuera -> Dentro.
- El evento se emite en menos de 2 segundos desde la recepcion de la muestra causante.
- El evento contiene idOperario, idZona, distanciaMetros y timestampUtc.
- Mientras el operario permanezca Dentro, no se duplican alertas de entrada para la misma transicion.

### 4.2 Falsa alarma (operario a 51 m)
Given
- Un operario estable a distancia real de 51.0 m del centro.
- La zona mantiene radio de 50.0 m.
When
- Se reciben y procesan muestras GPS validas dentro de la ventana de observacion definida para pruebas.
Then
- El sistema no debe generar AlertaEntrada.
- El estado del operario permanece Fuera.
- No debe existir ningun evento de severidad critica asociado a entrada en zona para ese intervalo.

### 4.3 Recuperacion (salida de la zona de peligro)
Given
- Un operario en estado Dentro con alerta de entrada previamente emitida.
When
- Se procesa una muestra GPS valida con distancia mayor a 50.0 m.
Then
- El sistema genera exactamente un evento AlertaSalida por transicion Dentro -> Fuera.
- El evento se emite en menos de 2 segundos desde la recepcion de la muestra causante.
- El estado final del operario queda en Fuera.
- Tras la salida, el sistema queda armado para detectar una nueva entrada posterior.

## 5. Requisitos no funcionales de seguridad

### 5.1 Tiempo maximo de deteccion
- Requisito: latencia extremo a extremo menor a 2 segundos para eventos AlertaEntrada y AlertaSalida.
- Medicion: tiempo desde recepcion de muestra GPS valida hasta publicacion del evento.
- Criterio de aceptacion: p95 < 2.0 s durante pruebas de carga nominal de la obra.
- Incumplimiento: cualquier valor p95 mayor o igual a 2.0 s implica no conformidad.

### 5.2 Comportamiento ante perdida de senal GPS
- Deteccion de perdida: ausencia de muestras durante un periodo continuo configurable (por defecto 5 s).
- Accion obligatoria:
  - Marcar estado de posicion como Desconocido.
  - Generar evento GPSPerdido con severidad de seguridad media.
  - Congelar evaluacion de entrada/salida hasta recibir nueva muestra valida.
- Recuperacion tras senal:
  - La primera muestra valida reanuda evaluacion normal.
  - Si la posicion recuperada cae Dentro, debe evaluarse y emitirse AlertaEntrada segun reglas de transicion.

## 6. Matriz de casos de borde

| Caso de borde | Precondicion | Estimulo | Comportamiento esperado | Severidad | Resultado esperado |
|---|---|---|---|---|---|
| Coordenadas nulas | Operario registrado y zona activa | Muestra con latitud o longitud nula/no informada | La muestra se descarta, se registra evento DatoInvalido, no cambia estado Dentro/Fuera | Alta | Sin alerta de entrada/salida y con trazabilidad de error |
| Coordenadas fuera de rango | Operario registrado y zona activa | Latitud fuera de [-90, 90] o longitud fuera de [-180, 180] | Rechazo de muestra como invalida, log de validacion, continuidad operativa del motor | Alta | Sin cambio de estado y sin alerta falsa |
| Salto brusco de posicion (teletransporte) | Operario con trayectoria estable | Cambio imposible por velocidad fisica (umbral configurable, p. ej. > 25 m/s en obra peatonal) | Marcar muestra como sospechosa, no confirmar transicion en una sola muestra aislada, solicitar confirmacion con siguiente muestra valida | Critica | Sin alerta inmediata por muestra anomala; solo alertar si se confirma en muestras consecutivas |
| Bateria baja del dispositivo | Dispositivo operativo | Nivel de bateria por debajo de umbral (p. ej. <= 10%) | Generar evento BateriaBaja, elevar prioridad de supervision, mantener geofencing mientras exista senal | Media | Continuidad del servicio con alerta preventiva |
| Perdida temporal de GPS | Operario en cualquier estado | Interrupcion de muestras por encima del timeout | Estado pasa a Desconocido y se emite GPSPerdido | Alta | Sin transiciones Dentro/Fuera hasta recuperar senal |

## 7. Reglas de no ambiguedad para implementacion y QA
- Regla de inclusion de borde: distancia menor o igual a 50.0 m se considera Dentro.
- Regla de salida: distancia mayor a 50.0 m se considera Fuera.
- Regla de unicidad: por cada transicion de estado solo se permite un evento.
- Regla de idempotencia: reprocesar la misma muestra no debe duplicar eventos.
- Regla de trazabilidad: todos los rechazos de muestra deben quedar auditados con causa tecnica.
- Regla de continuidad segura: ante datos invalidos o ausencia de senal, prevalece estado seguro (no afirmar entrada sin evidencia valida).

## 8. Evidencias minimas para cierre de aceptacion
- Evidencia de pruebas funcionales Given-When-Then para los 3 escenarios obligatorios.
- Evidencia de latencia con medicion p95 menor a 2.0 s.
- Evidencia de pruebas de perdida de GPS y recuperacion.
- Evidencia de ejecucion de matriz de casos de borde con resultado esperado cumplido.
- Registro de auditoria de eventos y errores con identificadores completos.
