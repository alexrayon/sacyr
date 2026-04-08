# ADR-006: Refactorizacion SRP con Interfaces e Inyeccion de Dependencias

## Estado
Aceptado

## Fecha
2026-04-08

## Contexto
El analisis en ESTRATEGIA_SRP identifica que el cierre de obras concentra multiples responsabilidades en un unico flujo: acceso a datos, logica financiera, notificacion, reporte y orquestacion. El codigo original acopla el caso de uso a infraestructura concreta (consulta SQL directa, correo SMTP, archivo local), generando fragilidad operativa y baja testabilidad.

La fragilidad observada se manifiesta en:
- Fallos no deterministas por dependencia de red, disco y estado global.
- Pruebas no aisladas, lentas y sensibles al entorno.
- Alto costo de cambio: cualquier variacion tecnica impacta el metodo de negocio.

## Decision
Adoptar una arquitectura por puertos y adaptadores basada en:
- Contratos explicitos de salida: IProjectRepository, INotificationService, IReportGenerator.
- Servicio de aplicacion ProjectClosingService como orquestador del caso de uso.
- Inyeccion de Dependencias (DI) por constructor (Primary Constructor en C#) para suministrar colaboraciones.
- Transferencia de datos a reporte mediante DTO dedicado (ClosingSummary), evitando exponer entidades de persistencia.

## Justificacion tecnica
Se considera la unica solucion valida para eliminar la fragilidad del codigo original porque:

1. Solo las interfaces rompen el acoplamiento estructural
Sin contratos abstractos, el caso de uso conoce detalles de tecnologia. Con interfaces, el servicio depende de capacidades, no de implementaciones.

2. Solo DI elimina el acoplamiento temporal y de construccion
No basta con "envolver" llamadas estaticas. Mientras el servicio cree o invoque dependencias concretas, no hay sustitucion real en pruebas. DI permite inyectar dobles de prueba y controlar cada escenario.

3. Solo esta combinacion permite pruebas unitarias deterministas
Unit testing exige aislamiento completo de IO. Interfaces + DI permiten validar reglas y orquestacion sin SMTP, sin base de datos y sin filesystem reales.

4. Solo asi se habilita evolucion independiente
Cambiar proveedor de correo, motor de datos o formato de reporte pasa a ser un cambio de adaptador, no del caso de uso.

## Alternativas consideradas
1. Mantener clase monolitica y aumentar pruebas de integracion
- Rechazada: no elimina fragilidad ni reduce costo de cambio.

2. Introducir wrappers estaticos sin DI
- Rechazada: mantiene acoplamiento a estado global y no habilita sustitucion limpia en tests.

3. Service Locator
- Rechazada: oculta dependencias, reduce claridad y dificulta testing predecible.

## Consecuencias
Positivas:
- Mayor testabilidad y velocidad de feedback.
- Menor impacto de cambios tecnicos.
- Mejor separacion de responsabilidades y mantenibilidad.

Compromisos:
- Aumento inicial de tipos (interfaces, DTOs, adaptadores).
- Necesidad de composicion explicita en el bootstrap.

## Criterios de verificacion
- ProjectClosingService no contiene SQL, SMTP ni operaciones de archivo.
- Sus dependencias son unicamente contratos inyectados por constructor.
- CloseProject queda como orquestacion de alto nivel (que hacer), sin detalles tecnicos (como hacerlo).
- El reporte se genera desde ClosingSummary y no desde entidades de persistencia.
