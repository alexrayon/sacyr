# ESTRATEGIA_SRP

## Contexto
La clase `ProjectManager` concentra en un único método (`CloseProject`) decisiones de negocio, persistencia, comunicación externa y generación documental. Este diseño viola el Principio de Responsabilidad Única (SRP) porque mezcla múltiples motivos de cambio dentro de la misma unidad de código.

## 1. Inventario de Responsabilidades

1. Recuperación de datos del proyecto
- Ejecuta consulta de lectura (`Database.Query`) para cargar el estado inicial de la obra.
- Motivo de cambio: cambios en esquema, tecnología de datos o estrategia de acceso.

2. Lógica financiera de cierre
- Calcula `balance = Budget - Expenses`.
- Motivo de cambio: nuevas reglas de liquidación, impuestos, retenciones, redondeos o validaciones contables.

3. Persistencia del cierre
- Ejecuta actualización de estado y balance final (`Database.Execute`).
- Motivo de cambio: reglas transaccionales, versionado de entidades, auditoría, cambios de base de datos.

4. Notificación al responsable de la obra
- Envía mensaje al propietario (`NotificationGateway.Send`, equivalente arquitectónico a usar `SmtpClient` directo).
- Motivo de cambio: plantilla de mensaje, canal de comunicación, proveedor de correo, políticas de reintento.

5. Generación y almacenamiento de reporte de cierre
- Crea carpeta y archivo en disco (`Directory.CreateDirectory`, `File.WriteAllText`).
- Motivo de cambio: formato de reporte, repositorio documental, políticas de nomenclatura, destinos alternativos (blob, API, etc.).

6. Comunicación operativa por consola
- Escribe trazas de éxito (`Console.WriteLine`).
- Motivo de cambio: estandarización de observabilidad, migración a logging estructurado o telemetría centralizada.

7. Coordinación secuencial del proceso
- Decide orden de ejecución de pasos de cierre.
- Motivo de cambio: requisitos de orquestación, manejo de errores, compensaciones o idempotencia.

## 2. Análisis de Acoplamiento

### 2.1 Acoplamiento a infraestructura concreta
Cuando `CloseProject` depende de implementaciones concretas como `SmtpClient` y `Database.Query` (o wrappers estáticos con el mismo acoplamiento efectivo), el método queda ligado a recursos externos reales: red SMTP, motor de base de datos, sistema de archivos y estado global.

Consecuencias técnicas:
- Pruebas lentas y frágiles por dependencia de IO real.
- Fallos no deterministas por condiciones externas (latencia, credenciales, conectividad, estado del servidor).
- Necesidad de preparar entornos integrados para validar reglas de negocio simples.

### 2.2 Imposibilidad práctica de aislamiento unitario
Una prueba unitaria debe validar comportamiento en memoria y de forma determinista. Con dependencias directas:
- No es posible sustituir (`mock/fake/stub`) fácilmente el envío de correo o la consulta SQL.
- El test no puede observar interacciones semánticas (por ejemplo, "se notificó con este asunto") sin ejecutar la infraestructura.
- La lógica financiera y la lógica de orquestación quedan mezcladas con efectos secundarios, impidiendo verificar cada regla de forma independiente.

### 2.3 Acoplamiento por API estática y estado global
El patrón `Database.Query(...)` estático induce acoplamiento temporal y estructural:
- No hay contrato de abstracción inyectable.
- El estado compartido (diccionario estático en el ejercicio) contamina pruebas entre sí.
- El orden de ejecución de tests puede alterar resultados.

Resultado: el código no es estrictamente "imposible" de probar en términos absolutos, pero sí no unit-testable de forma fiable, aislada y mantenible; lo que en práctica equivale a una barrera para testing unitario profesional.

## 3. Definición de Fronteras (Separación de Intereses)

Se propone descomponer el caso de uso de cierre de obra en componentes especializados con contratos explícitos:

1. `ProjectClosureOrchestrator` (Aplicación)
- Responsabilidad: coordinar el flujo de cierre.
- No implementa detalles de datos, correo ni archivos.

2. `IProjectRepository` (Puerto de salida de datos)
- Operaciones: obtener proyecto por id, persistir estado de cierre.
- Implementaciones: SQL, memoria, API externa.

3. `IClosureCalculator` o `ProjectClosurePolicy` (Dominio)
- Responsabilidad: encapsular reglas financieras del cierre.
- Totalmente puro, sin IO.

4. `INotificationService` (Puerto de comunicación)
- Responsabilidad: emitir notificación de cierre.
- Implementaciones: SMTP (`SmtpClient`), proveedor SaaS, cola de eventos.

5. `IClosureReportService` (Puerto documental)
- Responsabilidad: construir/publicar reporte de cierre.
- Implementaciones: archivo local, almacenamiento cloud, gestor documental.

6. `ILogger`/`ITelemetry` (Observabilidad)
- Responsabilidad: trazabilidad operacional sin contaminar reglas de dominio.

### Regla arquitectónica clave
Las dependencias deben apuntar hacia abstracciones y no hacia tecnologías concretas. El caso de uso depende de interfaces; las implementaciones concretas se resuelven en composición (bootstrap/DI).

## 4. Criterios de Éxito para `CloseProject`

El método `CloseProject` se considera correctamente refactorizado cuando cumple todos estos criterios:

1. Es un orquestador declarativo
- Describe el "qué" del proceso, no el "cómo" técnico.

2. No contiene detalles de infraestructura
- No instancia ni usa directamente `SmtpClient`, SQL inline, `File`, `Directory` ni estado global estático.

3. La lógica financiera está externalizada
- El cálculo de balance y reglas de cierre viven en un componente de dominio aislado y testeable.

4. Sus colaboraciones son contratos
- Interactúa con interfaces (`IProjectRepository`, `INotificationService`, `IClosureReportService`, etc.).

5. Es unit-testable en aislamiento
- Puede probarse con dobles de prueba en memoria, verificando orden e intención de llamadas sin IO real.

6. Gestiona errores con semántica de caso de uso
- Traduce fallos técnicos a resultados de negocio (éxito parcial, reintento, error recuperable/no recuperable) según política definida.

7. Mantiene cohesión y legibilidad
- Flujo corto, nombres ubicuos de dominio y ausencia de bloques técnicos extensos.

## Cierre
Aplicar esta estrategia SRP permite reducir acoplamiento, aumentar testabilidad y habilitar evolución independiente de reglas de negocio e infraestructura. El resultado esperado no es solo "código más limpio", sino un proceso de cierre de obras gobernable, verificable y sostenible en el tiempo.