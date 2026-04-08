# ADR-005: Autorización basada en políticas para MaquinariaController

## Estado
Propuesto

## Contexto
El informe `AUDITORIA_SEGURIDAD.md` identificó fallos críticos en `Ejercicio_5_Auditoria/Ejercicio5/Program.cs` relativos a:
- ausencia de `[Authorize]` en todos los endpoints,
- hardcoding de la identidad administrativa en el método `Decommission`,
- autorización embebida en el cuerpo del método en lugar de delegarse al middleware.

Estos problemas generan deuda técnica de seguridad y contagian el controlador con lógica de acceso, complicando auditoría, pruebas y cumplimiento de políticas de infraestructura crítica.

## Decisión
Adoptar un sistema de autorización basado en políticas de ASP.NET Core y separar completamente la lógica de permisos de la lógica del controlador.

Se definirá un modelo mínimo con dos políticas clave:
- `FleetViewer`: acceso de consulta a inventario y estados de maquinaria.
- `CriticalAssetAdmin`: permiso sobre operaciones de alto riesgo (`StopMachine`, `Decommission`).

Los endpoints quedarán protegidos con atributos declarativos:
- `[Authorize(Policy = "FleetViewer")]` para lectura y monitorización.
- `[Authorize(Policy = "CriticalAssetAdmin")]` para comandos de parada de emergencia y desmantelamiento.

La seguridad quedará gestionada por middleware y handlers en `Program.cs` / `Startup.cs` en lugar de validaciones puntuales dentro de cada acción.

## Justificación
1. Eliminar deuda técnica de seguridad
   - La lógica de autorización ya no está dispersa en los métodos del controlador.
   - Esto reduce el riesgo de omisiones accidentales y hace explícito qué políticas aplican a cada ruta.
   - Evita la acumulación de excepciones y bypasses cuando el código cambia.

2. Cumplimiento normativo
   - Las políticas declarativas facilitan la auditoría y la revisión de cumplimiento interno.
   - Permiten mapear requisitos Sacyr directamente a políticas nombradas y a claims del token.
   - Separar la autorización del negocio respalda la trazabilidad de decisiones de acceso y la segregación de funciones.

3. Robustez y mantenibilidad
   - La autorización basada en middleware se reutiliza en todos los controladores y evita duplicación.
   - Las políticas pueden evolucionar sin tocar la capa de presentación.
   - El análisis de riesgos queda centralizado en un único lugar, lo que es crítico para infraestructuras de tuneladoras.

## Definición de requisitos y handlers
### Requisitos de seguridad
Definir requisitos independientes del controlador, por ejemplo:
- `FleetViewRequirement`:
  - Permite acceso si el usuario posee el claim `permission` con valor `fleet.view` o si pertenece al role `FleetAnalyst`.
- `CriticalAssetAdminRequirement`:
  - Permite acceso si el usuario posee el claim `permission` con valor `fleet.critical.manage` o si pertenece al role `CriticalAdmin`.

Estos requisitos deben construirse sobre un modelo de claims/roles gestionado externamente, sin ninguna referencia a nombres de usuario concretos.

### Authorization handlers
Estructurar los handlers como componentes reutilizables:
- `FleetViewAuthorizationHandler` valida `FleetViewRequirement`
- `CriticalAssetAdminAuthorizationHandler` valida `CriticalAssetAdminRequirement`

Responsabilidades:
- evaluar claims y roles del principal,
- aplicar reglas de negocio de acceso sin acceder a datos de implementación de la acción,
- devolver `context.Succeed(requirement)` o `context.Fail()`.

### Independencia del controlador
El controlador deberá quedarse con un esquema simple:
- atributos `[Authorize(Policy = ...)]` en clase o métodos,
- inyección de servicios de dominio si hace falta,
- ninguna comprobación directa de `User.Identity?.Name`, `User.IsInRole(...)` ni condiciones de acceso dentro de las acciones.

De esta forma, la lógica de "quién puede hacer qué" queda fuera del cuerpo de los métodos y centralizada en el middleware.

## Plan de migración
1. **Definir las políticas y handlers**
   - Registrar `FleetViewer` y `CriticalAssetAdmin` en `Program.cs` o `Startup.cs`.
   - Implementar los requirements y handlers asociados.

2. **Proteger el controlador con atributos**
   - Aplicar `[Authorize(Policy = "FleetViewer")]` a `GetFullFleet()`.
   - Aplicar `[Authorize(Policy = "CriticalAssetAdmin")]` a `StopMachine(int id)` y `Decommission(int id)`.

3. **Mantener el servicio en funcionamiento**
   - Desplegar la configuración en una fase de prueba con validación de tokens existentes.
   - Verificar que los endpoints no autorizados reciben `401/403` esperados.

4. **Eliminar la lógica manual de autorización**
   - Retirar la condición `User.Identity?.Name == "admin_sacyr_central"` y cualquier `User.IsInRole(...)` del método `Decommission`.
   - Transformar el cuerpo del método en una operación de negocio pura: búsqueda, modificación o eliminación de la máquina.

5. **Validación y control de regresiones**
   - Añadir pruebas de integración para políticas y permisos.
   - Ejecutar pruebas de aceptación con escenarios de `FleetViewer` y `CriticalAssetAdmin`.

6. **Revisión de cumplimiento**
   - Confirmar que el acceso se controla mediante middleware y no por código inline.
   - Documentar la correspondencia entre políticas y los requisitos de seguridad Sacyr.

## Consecuencias
- Mejora inmediata de la postura de seguridad de los endpoints de flota.
- Reducción del riesgo de sabotaje y del impacto por exposición de comandos críticos.
- Mejor trazabilidad para auditorías de ciberseguridad en infraestructuras críticas.
- El controlador queda preparado para escalar con nuevas políticas sin introducir deuda técnica adicional.
