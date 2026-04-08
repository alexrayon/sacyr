# TAREAS DE IMPLEMENTACION - Motor de Validacion de Certificaciones de Obra

Referencias: VALIDACION_FUNCIONAL.md, DISENO_TECNICO_VALIDACION.md, ADR-001

---

## FASE 1 - Infraestructura y Modelos

### TAREA 1.1 - Crear la solucion y estructura de proyectos
- Objetivo tecnico:
  Inicializar la solucion .NET con los cuatro proyectos requeridos:
  - ValidacionCertificaciones.Domain (class library)
  - ValidacionCertificaciones.Application (class library)
  - ValidacionCertificaciones.Infrastructure (class library)
  - ValidacionCertificaciones.Console (console app)
  Referencias de proyecto entre capas configuradas (Domain <- Application <- Infrastructure <- Console).

- Criterio de aceptacion:
  La solucion compila sin errores desde la raiz.
  Cada proyecto tiene dependencias solo hacia capas inferiores (sin referencias circulares).

---

### TAREA 1.2 - Definir el enum EstadoPartida en dominio
- Objetivo tecnico:
  Crear el enum EstadoPartida con los valores: Activa, Finalizada, Liquidada.
  Ubicacion: ValidacionCertificaciones.Domain.

- Criterio de aceptacion:
  El enum contiene exactamente los tres valores.
  No existe logica de negocio en el enum (solo definicion de estados).

---

### TAREA 1.3 - Implementar el record SolicitudCertificacion
- Objetivo tecnico:
  Crear el record inmutable SolicitudCertificacion con los campos del contrato de entrada:
  - string IdCertificacion
  - string IdPartida
  - decimal PartidaProyectada
  - decimal AcumuladoHistorico
  - decimal ImporteActual
  - DateOnly FechaActaReplanteo
  - DateOnly FechaTrabajos
  - DateOnly FechaEmision
  - EstadoPartida EstadoPartida

  Todos los campos monetarios deben ser decimal (ADR-001).
  El record debe ser inmutable (sealed record).

- Criterio de aceptacion:
  El compilador rechaza asignacion posterior a la construccion.
  No existen propiedades de tipo float ni double.
  El record es instanciable mediante constructor con posicion.

---

### TAREA 1.4 - Implementar los records de salida: ErrorValidacion y ResultadoRegla
- Objetivo tecnico:
  Crear los records inmutables de resultado parcial:
  - ErrorValidacion: CodigoRegla (string), Mensaje (string), Severidad (string), Evidencia (IReadOnlyDictionary<string, string>)
  - ResultadoRegla: CodigoRegla (string), Cumple (bool), Mensaje (string), Evidencia (IReadOnlyDictionary<string, string>)
  Ubicacion: ValidacionCertificaciones.Domain.

- Criterio de aceptacion:
  Ambos records son sealed.
  Evidencia es de solo lectura y no puede ser nula en construccion.
  Los records se construyen con colecciones vacias por defecto sin romper.

---

### TAREA 1.5 - Implementar el record ResultadoAuditoria
- Objetivo tecnico:
  Crear el record inmutable de salida global:
  - bool Apta
  - IReadOnlyList<ErrorValidacion> Errores
  - IReadOnlyList<ResultadoRegla> ResultadosReglas
  - string VersionReglas
  - DateTimeOffset FechaEvaluacionUtc
  - string IdEjecucion
  Ubicacion: ValidacionCertificaciones.Domain.

- Criterio de aceptacion:
  Apta es derivable de la lista de Errores: si Errores esta vacia, Apta es true.
  FechaEvaluacionUtc usa DateTimeOffset y no DateTime para garantizar zona horaria explicita.
  IdEjecucion no es nullable ni vacio en construccion valida.

---

### TAREA 1.6 - Definir la interfaz IReglaValidacion en dominio
- Objetivo tecnico:
  Crear el contrato de regla de validacion:
  - string Codigo { get; }
  - string Nombre { get; }
  - int Prioridad { get; }
  - ResultadoRegla Evaluar(SolicitudCertificacion solicitud)
  Ubicacion: ValidacionCertificaciones.Domain.

- Criterio de aceptacion:
  La interfaz no tiene dependencias de infraestructura ni de aplicacion.
  Evaluar es una funcion pura: dada la misma entrada produce el mismo resultado.
  La interfaz es publica y accesible desde todas las capas.

---

### TAREA 1.7 - Definir la interfaz IProveedorRuleSet en dominio
- Objetivo tecnico:
  Crear el contrato de seleccion de set de reglas por contexto contractual:
  - IEnumerable<IReglaValidacion> ObtenerReglas(string pais, string tipoContrato)
  Ubicacion: ValidacionCertificaciones.Domain.

- Criterio de aceptacion:
  La interfaz acepta combinaciones de pais y tipoContrato.
  Retorna una coleccion ordenada por prioridad de regla.
  No depende de implementaciones concretas ni de infraestructura.

---

## FASE 2 - Implementacion de Reglas Atomicas

### TAREA 2.1 - Implementar ReglaTechoGasto (R1)
- Objetivo tecnico:
  Clase que implementa IReglaValidacion.
  Codigo = "R1", Nombre = "Techo de Gasto".
  Logica: (AcumuladoHistorico + ImporteActual) <= (PartidaProyectada * 1.05M)
  Todos los calculos en decimal.
  La evidencia debe capturar: TotalCertificable, TechoPermitido, PartidaProyectada, MargenAplicado.

- Criterio de aceptacion:
  R1 devuelve Cumple=true si TotalCertificable <= TechoPermitido exacto (limite inclusivo).
  R1 devuelve Cumple=false si TotalCertificable > TechoPermitido, incluso por 0.01M.
  La comparacion no usa double ni float en ningun punto.
  Para ImporteActual = 0M, TotalCertificable = AcumuladoHistorico.

---

### TAREA 2.2 - Implementar ReglaTemporalidad (R2)
- Objetivo tecnico:
  Clase que implementa IReglaValidacion.
  Codigo = "R2", Nombre = "Temporalidad".
  Logica: FechaTrabajos > FechaActaReplanteo AND FechaTrabajos < FechaEmision
  La evidencia debe capturar la terna de fechas: FechaActaReplanteo, FechaTrabajos, FechaEmision.

- Criterio de aceptacion:
  R2 devuelve Cumple=true solo si ambas condiciones son estrictamente ciertas (sin igualdades en limites).
  R2 devuelve Cumple=false si FechaTrabajos == FechaActaReplanteo.
  R2 devuelve Cumple=false si FechaTrabajos == FechaEmision.
  R2 devuelve Cumple=false si FechaTrabajos es anterior al acta.

---

### TAREA 2.3 - Implementar ReglaEstadoPartida (R3)
- Objetivo tecnico:
  Clase que implementa IReglaValidacion.
  Codigo = "R3", Nombre = "Estado de Partida".
  Logica: EstadoPartida != Finalizada AND EstadoPartida != Liquidada
  La evidencia debe capturar: EstadoPartidaEvaluado.

- Criterio de aceptacion:
  R3 devuelve Cumple=false para EstadoPartida.Finalizada.
  R3 devuelve Cumple=false para EstadoPartida.Liquidada.
  R3 devuelve Cumple=true para EstadoPartida.Activa.
  El comportamiento es independiente de R1 y R2.

---

### TAREA 2.4 - Implementar ProveedorRuleSetDefault
- Objetivo tecnico:
  Implementacion de IProveedorRuleSet que registra y retorna el conjunto de reglas base:
  [R3, R1, R2] ordenadas por prioridad.
  Si pais o tipoContrato no tienen un set especifico, retorna el set por defecto.
  Ubicacion: ValidacionCertificaciones.Infrastructure.

- Criterio de aceptacion:
  Retorna siempre las tres reglas base.
  Las reglas se ordenan por la propiedad Prioridad.
  El proveedor no contiene logica de negocio de validacion.

---

## FASE 3 - Desarrollo del Motor de Auditoria

### TAREA 3.1 - Implementar ValidadorCertificacionService
- Objetivo tecnico:
  Servicio de aplicacion que orquesta la validacion:
  - Recibe SolicitudCertificacion y string VersionReglas.
  - Obtiene el IEnumerable<IReglaValidacion> activo via IProveedorRuleSet.
  - Ejecuta todas las reglas (sin cortar en el primer fallo).
  - Consolida ResultadoAuditoria con estado global y lista completa de errores e incumplimientos.
  Ubicacion: ValidacionCertificaciones.Application.

- Criterio de aceptacion:
  El servicio ejecuta siempre todas las reglas del set, aunque la primera falle.
  Apta = true si y solo si todas las reglas devuelven Cumple=true.
  ResultadoAuditoria.Errores incluye ErrorValidacion por cada regla incumplida.
  ResultadoAuditoria.ResultadosReglas incluye un ResultadoRegla por cada regla ejecutada.
  Si la SolicitudCertificacion tiene ImporteActual negativo, el servicio no emite Apta.

---

### TAREA 3.2 - Implementar validacion de entrada en el servicio
- Objetivo tecnico:
  Antes de ejecutar reglas, verificar que los campos obligatorios de SolicitudCertificacion son validos:
  - IdCertificacion e IdPartida no son nulos ni vacios.
  - PartidaProyectada > 0M.
  - ImporteActual >= 0M.
  - AcumuladoHistorico >= 0M.
  - Las tres fechas son valores validos y distintos de DateOnly.MinValue.
  Si alguna validacion falla, retornar ResultadoAuditoria con Apta=false y ErrorValidacion descriptivo.

- Criterio de aceptacion:
  Ante datos insuficientes no se emite Apta.
  El error de validacion de entrada es distinto de los codigos R1/R2/R3.
  Las reglas de negocio no se ejecutan si la entrada es invalida.

---

### TAREA 3.3 - Implementar generacion de IdEjecucion y FechaEvaluacionUtc
- Objetivo tecnico:
  El servicio debe generar de forma automatica:
  - IdEjecucion: identificador unico por ejecucion (GUID).
  - FechaEvaluacionUtc: momento de evaluacion en UTC.
  Estos datos se incluyen en ResultadoAuditoria y no deben ser sobreescritos por la entrada.

- Criterio de aceptacion:
  Dos evaluaciones consecutivas de la misma solicitud producen IdEjecucion distintos.
  FechaEvaluacionUtc es siempre UTC (Kind = Utc / Offset = +00:00).

---

## FASE 4 - Registro y Trazabilidad

### TAREA 4.1 - Configurar logging estructurado en aplicacion de consola
- Objetivo tecnico:
  Integrar Microsoft.Extensions.Logging en el proyecto Console.
  El LoggerFactory debe configurarse para salida en consola.
  El nivel minimo de log debe ser configurable.

- Criterio de aceptacion:
  Los logs se emiten en consola con categoria, nivel y mensaje.
  El nivel de log es ajustable sin recompilar.

---

### TAREA 4.2 - Registrar inicio y fin de validacion
- Objetivo tecnico:
  El ValidadorCertificacionService debe emitir logs de nivel Information al inicio y al final de cada validacion:
  - Inicio: IdCertificacion, IdPartida, VersionReglas.
  - Fin: IdEjecucion, resultado Apta/Rechazada, numero de reglas evaluadas.

- Criterio de aceptacion:
  Cada validacion genera exactamente dos logs de nivel Information (inicio y fin).
  Los logs no incluyen datos sensibles no relevantes para auditoria.

---

### TAREA 4.3 - Registrar cada rechazo individual con fines de auditoria legal
- Objetivo tecnico:
  Por cada regla incumplida, emitir log de nivel Warning con:
  - CodigoRegla
  - Mensaje de rechazo funcional
  - Evidencia clave-valor completa
  Estos logs son el registro de auditoria legal y deben ser identificables por IdEjecucion.

- Criterio de aceptacion:
  Cada rechazo genera un log de nivel Warning independiente.
  La evidencia del log permite reconstruir el calculo sin necesidad de la entrada original.
  El log incluye IdEjecucion para correlacion de trazas.

---

### TAREA 4.4 - Registrar errores de validacion de entrada
- Objetivo tecnico:
  Si SolicitudCertificacion tiene datos invalidos, emitir log de nivel Error con descripcion del campo fallido.

- Criterio de aceptacion:
  Logs de nivel Error solo se emiten por datos de entrada invalidos, no por rechazos de negocio.
  El log no expone stacktraces de implementacion interna.

---

## FASE 5 - Suite de Validacion

### TAREA 5.1 - Crear proyecto de tests y configurar dependencias
- Objetivo tecnico:
  Crear proyecto ValidacionCertificaciones.Tests con xUnit.
  Referenciar Domain y Application.
  Configurar ejecucion desde dotnet test.

- Criterio de aceptacion:
  dotnet test retorna 0 errores inicialmente (proyecto vacio compilable).

---

### TAREA 5.2 - Tests unitarios para ReglaTechoGasto (R1)
- Objetivo tecnico:
  Cubrir todos los escenarios funcionales y de borde:
  - Certifica dentro del margen: Cumple=true.
  - Certifica exactamente en el limite del 105%: Cumple=true.
  - Certifica 0.01M por encima del limite: Cumple=false.
  - ImporteActual = 0M con acumulado dentro del margen: Cumple=true.
  - ImporteActual = 0M con acumulado ya excedido: Cumple=false.

- Criterio de aceptacion:
  Todos los calculos se realizan con literales decimal (sufijo M).
  Ningun test usa tolerancias o aproximaciones numericas.
  La evidencia del resultado captura los valores correctos en todos los casos.

---

### TAREA 5.3 - Tests unitarios para ReglaTemporalidad (R2)
- Objetivo tecnico:
  Cubrir escenarios temporales:
  - Fecha de trabajos en rango valido: Cumple=true.
  - Fecha de trabajos igual a acta: Cumple=false.
  - Fecha de trabajos igual a emision: Cumple=false.
  - Fecha de trabajos anterior al acta: Cumple=false.
  - Fecha de trabajos posterior a emision: Cumple=false.

- Criterio de aceptacion:
  Cada escenario es un test independiente con nombre descriptivo.
  La evidencia captura la terna de fechas en todos los casos.

---

### TAREA 5.4 - Tests unitarios para ReglaEstadoPartida (R3)
- Objetivo tecnico:
  Cubrir los tres valores del enum:
  - EstadoPartida.Activa: Cumple=true.
  - EstadoPartida.Finalizada: Cumple=false.
  - EstadoPartida.Liquidada: Cumple=false.

- Criterio de aceptacion:
  Tres tests, uno por valor de enum.
  La adicion de un nuevo valor al enum no silencia tests existentes.

---

### TAREA 5.5 - Tests de integracion del ValidadorCertificacionService
- Objetivo tecnico:
  Traducir los tres escenarios Gherkin de VALIDACION_FUNCIONAL.md a tests de integracion:
  - Escenario 1: Validacion exitosa → Apta=true, 0 errores, 3 reglas Cumple.
  - Escenario 2: Rechazo por exceso de presupuesto → Apta=false, error R1.
  - Escenario 3: Rechazo por incoherencia de fechas → Apta=false, error R2.

- Criterio de aceptacion:
  Cada test usa configuraciones de datos exactas al escenario Gherkin (mismos importes y fechas).
  El servicio retorna todos los campos de ResultadoAuditoria sin nulos.
  Los tests pasan con dotnet test sin configuracion adicional de entorno.

---

### TAREA 5.6 - Tests de edge cases financieros
- Objetivo tecnico:
  Cubrir los casos de borde definidos en la seccion 11 de VALIDACION_FUNCIONAL.md:
  - Importe cero con partida dentro del margen y fechas validas: Apta=true.
  - Importe cero con partida excedida: Rechazada por R1.
  - Total certificable exactamente igual al techo: Apta en R1.
  - Total certificable en 0.01 por encima del techo: Rechazada en R1.

- Criterio de aceptacion:
  Los tests demuestran comportamiento determinista para valores limitrofes.
  No existe ninguna tolerancia numerica configurada en los asserts.

---

### TAREA 5.7 - Tests de validacion de entrada invalida
- Objetivo tecnico:
  Verificar que el servicio rechaza sin ejecutar reglas cuando:
  - IdCertificacion es nulo o vacio.
  - PartidaProyectada es cero o negativa.
  - ImporteActual es negativo.
  - Alguna fecha es DateOnly.MinValue.

- Criterio de aceptacion:
  Apta=false en todos los casos.
  Errores contiene un ErrorValidacion con codigo distinto de R1/R2/R3.
  ResultadosReglas esta vacia (reglas no ejecutadas).

---

## Orden de ejecucion recomendado

Fase 1 → Fase 2 (tareas 2.1, 2.2 y 2.3 pueden desarrollarse en paralelo) → Fase 3 → Fase 4 → Fase 5

Las tareas de Fase 5 pueden iniciarse en paralelo con Fase 3 una vez completada Fase 2, siguiendo desarrollo dirigido por tests (TDD).
