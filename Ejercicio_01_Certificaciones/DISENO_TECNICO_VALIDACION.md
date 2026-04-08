# DISENO TECNICO - Motor de Validacion de Certificaciones de Obra

## 1. Contexto y objetivo tecnico
Este documento traduce la especificacion funcional en una arquitectura tecnica para una aplicacion de consola en .NET orientada a:
- Precision absoluta en calculos monetarios.
- Mantenibilidad del dominio de validaciones.
- Trazabilidad y auditabilidad de decisiones.
- Evolucion por pais y por tipo de contrato sin romper codigo existente.

El sistema valida una propuesta de certificacion y emite un dictamen final:
- Apta
- Rechazada

## 2. Propuesta arquitectonica (.NET Console)

### 2.1 Estilo arquitectonico
Se adopta una arquitectura modular por capas con separacion explicita entre dominio, aplicacion e infraestructura:
- Capa de Dominio:
  - Entidades y value objects del problema.
  - Contratos de reglas de validacion.
  - Modelos de auditoria.
- Capa de Aplicacion:
  - Caso de uso principal: ValidarCertificacion.
  - Orquestacion de reglas y consolidacion de resultados.
- Capa de Infraestructura:
  - Carga de configuraciones (pais, contrato, version de reglas).
  - Logging y persistencia de auditoria (si aplica en fases posteriores).
- Capa de Presentacion (Console App):
  - Entrada/salida por linea de comandos.
  - Parseo de solicitud y renderizado de resultado.

### 2.2 Estructura propuesta de proyecto
- src/
  - ValidacionCertificaciones.Console/
  - ValidacionCertificaciones.Application/
  - ValidacionCertificaciones.Domain/
  - ValidacionCertificaciones.Infrastructure/
- docs/
  - adr/

### 2.3 Flujo de alto nivel
1. La consola recibe los datos de la certificacion.
2. El caso de uso de aplicacion crea un contexto inmutable de validacion.
3. Se resuelve el set de reglas activo (por pais/contrato) via DI.
4. Se ejecutan todas las reglas del set.
5. Se consolida un ResultadoAuditoria con estado global y errores detallados.
6. Se imprime dictamen y evidencias.

### 2.4 Principios de diseno
- Single Responsibility: cada regla valida una sola condicion contractual.
- Open/Closed: nuevas reglas se agregan sin modificar las existentes.
- Inmutabilidad: entradas y resultados como objetos inmutables.
- Determinismo: sin dependencias de estado mutable global.
- Fail-safe de negocio: ante datos insuficientes, no se marca Apta.

## 3. Modelo de dominio tecnico

### 3.1 Agregados y objetos clave
- SolicitudCertificacion:
  - IdCertificacion
  - IdPartida
  - PartidaProyectada (decimal)
  - AcumuladoHistorico (decimal)
  - ImporteActual (decimal)
  - FechaActaReplanteo (DateOnly)
  - FechaTrabajos (DateOnly)
  - FechaEmision (DateOnly)
  - EstadoPartida (enum)

- ResultadoAuditoria:
  - Apta (bool)
  - Errores (lista detallada)
  - ResultadosReglas (opcional, recomendado)
  - Metadatos de trazabilidad

- ErrorValidacion:
  - CodigoRegla (ejemplo: R1, R2, R3)
  - Mensaje
  - Severidad
  - Evidencia (pares clave-valor)

### 3.2 Reglas de negocio como componentes
Cada regla contractual se implementa como un componente independiente:
- ReglaTechoGasto (R1)
- ReglaTemporalidad (R2)
- ReglaEstadoPartida (R3)

## 4. Diseno de APIs y contratos

### 4.1 Interfaz IReglaValidacion
Contrato propuesto para permitir composicion de reglas:

```csharp
public interface IReglaValidacion
{
    string Codigo { get; }
    string Nombre { get; }
    int Prioridad { get; }

    ResultadoRegla Evaluar(SolicitudCertificacion solicitud);
}
```

Racional tecnico:
- Codigo: asegura trazabilidad contractual y auditoria.
- Nombre: facilita lectura de reportes funcionales.
- Prioridad: permite ordenar presentacion sin acoplar la logica.
- Evaluar: funcion pura sobre una entrada inmutable.

### 4.2 Esquema de ResultadoAuditoria
Contrato de salida requerido por negocio (estado booleano + lista de errores detallada):

```csharp
public sealed record ResultadoAuditoria(
    bool Apta,
    IReadOnlyList<ErrorValidacion> Errores,
    IReadOnlyList<ResultadoRegla> ResultadosReglas,
    string VersionReglas,
    DateTimeOffset FechaEvaluacionUtc,
    string IdEjecucion
);

public sealed record ErrorValidacion(
    string CodigoRegla,
    string Mensaje,
    string Severidad,
    IReadOnlyDictionary<string, string> Evidencia
);

public sealed record ResultadoRegla(
    string CodigoRegla,
    bool Cumple,
    string Mensaje,
    IReadOnlyDictionary<string, string> Evidencia
);
```

Notas de contrato:
- Apta = true solo si Errores esta vacia y todas las reglas cumplen.
- Errores debe incluir todos los incumplimientos, no solo el primero.
- Evidencia debe capturar valores usados en la evaluacion (fechas, importes, umbrales).

## 5. Orquestacion de validaciones

### 5.1 Validador principal
Se propone un servicio de aplicacion (por ejemplo, ValidadorCertificacionService) que:
- Recibe SolicitudCertificacion.
- Obtiene IEnumerable<IReglaValidacion> inyectadas.
- Ejecuta reglas en orden de Prioridad.
- Agrega incumplimientos y arma ResultadoAuditoria.

### 5.2 Politica de evaluacion
- Ejecutar todas las reglas siempre (modo audit completo).
- No cortar en el primer fallo.
- Consolidar dictamen final por AND logico de resultados.

## 6. Consideraciones de escalabilidad con Inyeccion de Dependencias

### 6.1 Problema de variabilidad
Las reglas pueden variar por:
- Pais (normativa local, margenes legales distintos).
- Tipo de contrato (publico, privado, EPC, mantenimiento, etc.).

### 6.2 Estrategia tecnica con DI
Se define un mecanismo de seleccion de "RuleSet" en tiempo de ejecucion:
- IProveedorRuleSet: resuelve el conjunto de IReglaValidacion aplicable.
- Entradas de contexto: Pais, TipoContrato, VersionNormativa.
- Registro en contenedor DI por perfiles.

Ejemplo conceptual:
- RuleSet_ES_ObraPublica_v2026
- RuleSet_CL_ObraPrivada_v2026
- RuleSet_Default

### 6.3 Beneficios de la DI para mantenibilidad
- Bajo acoplamiento: el orquestador no conoce implementaciones concretas.
- Extension limpia: nuevas reglas o paises se agregan via registro DI.
- Testabilidad: facil inyectar mocks o sets de reglas de prueba.
- Versionado: coexistencia de versiones normativas sin ramas complejas de codigo.

### 6.4 Patron recomendado
Combinar:
- Strategy: seleccion de set de reglas.
- Composite: ejecucion agregada de multiples reglas.
- Factory (opcional): construccion del rule set por metadatos de contrato.

## 7. Precision monetaria y consistencia numerica

### 7.1 Norma de implementacion
Todos los campos monetarios deben usar decimal de forma obligatoria en dominio, aplicacion e infraestructura.

### 7.2 Politicas de precision
- Calculo interno en decimal.
- Normalizacion para presentacion a 2 decimales.
- Comparaciones contractuales sobre valores monetarios normalizados segun politica definida.

### 7.3 Regla operativa para redondeo
- Configurar una politica explicita (por ejemplo, MidpointRounding.ToEven o AwayFromZero) y documentarla.
- Aplicar la misma politica en todos los puntos de calculo y salida para evitar divergencias.

## 8. Observabilidad y auditoria tecnica

### 8.1 Datos minimos de auditoria
- Id de ejecucion.
- Fecha/hora UTC.
- Version de reglas.
- Entrada funcional relevante.
- Resultado por regla.
- Errores y evidencias.

### 8.2 Trazabilidad de decisiones
La salida de consola debe ser util para operador y para auditor:
- Resumen global: Apta/Rechazada.
- Detalle por regla con evidencia.
- Lista de errores accionables.

## 9. Riesgos tecnicos y mitigaciones
- Riesgo: uso accidental de double/float.
  - Mitigacion: ADR vinculante + analizadores + revisiones de codigo.
- Riesgo: deriva entre reglas por pais.
  - Mitigacion: rule sets versionados y pruebas de regresion por perfil.
- Riesgo: comportamiento no determinista.
  - Mitigacion: reglas puras e inmutables, sin dependencias temporales ocultas.

## 10. Criterios de listo para implementacion
- Contratos de dominio aprobados (IReglaValidacion, ResultadoAuditoria, ErrorValidacion).
- ADR de precision monetaria aceptado.
- Mecanismo DI de seleccion de rule set definido.
- Escenarios funcionales base y edge cases trazados a pruebas.
