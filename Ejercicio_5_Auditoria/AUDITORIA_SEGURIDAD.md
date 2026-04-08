# Auditoría de Seguridad - MaquinariaController

## Resumen ejecutivo
Este informe analiza `MaquinariaController` localizado en `Ejercicio_5_Auditoria\Ejercicio5\Program.cs` frente a la taxonomía de riesgos corporativos Sacyr.
Se detecta una falta de seguridad por defecto en todos los endpoints, la prohibición de hardcoding de identidad violada en el método de desmantelamiento, y la autorización declarativa inexistente en la lógica de negocio.

## Matriz de riesgos
| Método analizado | Nivel de Riesgo | Regla incumplida | Impacto potencial |
|---|---|---|---|
| `GetFullFleet()` | Alto | REGLA-SEC-01 | Divulgación de inventario y estado de tuneladoras sin control. Un atacante podría mapear activos, identificar máquinas operativas y preparar ataques dirigidos a las unidades más críticas de la obra. |
| `StopMachine(int id)` | Crítico | REGLA-SEC-01 | Control de parada de emergencia expuesto públicamente. Un atacante que invoque este endpoint puede detener remotamente una tuneladora en operación, generando paradas de obra, riesgo de colapso y posibles daños a personal y equipo. |
| `Decommission(int id)` | Crítico | REGLA-SEC-01, REGLA-SEC-02, REGLA-SEC-03 | Desmantelamiento irreversible protegido solo por lógica en el cuerpo del método. El uso de `User.Identity?.Name == "admin_sacyr_central"` rompe la regla de no hardcodear identidades y el control de acceso incrustado facilita la elevación de privilegios. Un atacante puede eliminar activos del inventario y sabotear la operación de la infraestrutura. |

## Observaciones generales
- Ningún método del controlador usa `[Authorize]`, lo que significa que la seguridad por defecto está ausente en todos los endpoints.
- La validación de permisos en `Decommission(int id)` se realiza dentro del método y no mediante middleware/atributos, lo que contradice el principio de autorización declarativa.
- El valor fijo `admin_sacyr_central` constituye un punto de falla crítico: si se conoce o se deduce, un atacante puede eludir la protección por identidad.
- La capacidad de detener o desmantelar maquinaria con llamados HTTP sin una capa de autorización robusta representa un riesgo alto para cualquier infraestructura crítica de excavación y tunelado.

## Recomendaciones de auditoría
- Reforzar todos los endpoints con seguridad por defecto mediante `[Authorize]` o equivalente.
- Eliminar cualquier identificación codificada en el código y depender de claims / roles definidos externamente.
- Delegar la autorización a middleware y políticas declarativas, evitando lógica de acceso dentro de los métodos de acción.
- Revisar la exposición de comandos de control de maquinaria y aplicar controles adicionales de seguridad operacional.
