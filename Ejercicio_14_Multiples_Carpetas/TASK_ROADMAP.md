# TASK_ROADMAP - Backlog para @developer-agent

## Secuencia de implementacion (determinista)
Orden obligatorio para evitar bloqueos:
1. Definir contratos (ports) y objetos de configuracion.
2. Refactor del servicio para DI.
3. Adaptadores de infraestructura actuales (in-memory/archivo).
4. Orquestacion en main.py (composition root).
5. Tests unitarios con mocks/fakes.
6. Preparacion de adapter SQL (esqueleto y contrato validado).

## Tareas atomicas con Definition of Done

### T1. Crear puertos de aplicacion
Accion:
- Definir interfaces FlotaRepositoryPort, FiscalPolicyPort, FuelPricingPort y ClockPort.

DoD:
- No hay dependencia de infrastructure en los puertos.
- Interfaces documentan contrato de entrada/salida.
- Sin imports circulares.

### T2. Crear configuracion tipada de reglas de eficiencia
Accion:
- Definir objeto de configuracion para umbral de consumo y antiguedad.

DoD:
- Valores por defecto no hardcodeados dentro del servicio.
- Validaciones de rango implementadas.

### T3. Refactorizar constructor de ServicioCostesFinancieros
Accion:
- Modificar constructor para recibir repository, fiscal_policy, fuel_pricing, clock e inefficiency_rules.
- Eliminar import directo de data.flota_repository.

DoD:
- Servicio compila y no importa infraestructura concreta.
- Metodo de calculo usa solo dependencias inyectadas.
- Linter sin errores en archivo de servicio.

### T4. Parametrizar logica fiscal y de combustible
Accion:
- Reemplazar IVA_GENERAL y PRECIO_GASOIL_LITRO por lectura desde proveedores inyectados.
- Mantener tasa de amortizacion dentro de politica/configuracion externa.

DoD:
- No quedan constantes fiscales/combustible hardcodeadas en el servicio.
- Se soporta contexto por pais/proyecto (ejemplo: Chile/Australia).

### T5. Parametrizar ano actual en antiguedad
Accion:
- Usar ClockPort en lugar de valor por defecto fijo para ano actual.

DoD:
- Calculo de antiguedad determinista en tests.
- Sin dependencia de fecha hardcodeada en flujo principal.

### T6. Implementar adaptador actual de flota
Accion:
- Crear InMemoryFlotaRepository (o FileFlotaRepository) que encapsule la fuente existente.

DoD:
- El servicio obtiene maquinas via puerto.
- Main puede cambiar implementacion sin tocar servicio.

### T7. Actualizar main.py como orquestador e inyector
Accion:
- main.py debe crear implementaciones concretas, construir configuracion y pasar todo al servicio.

DoD:
- main.py actua como composition root.
- Servicio se instancia con constructor completo.
- Flujo funcional actual se mantiene.

### T8. Crear suite de tests unitarios del servicio con mocks/fakes
Accion:
- Tests para calculo base, alquilada vs propia, politicas fiscales distintas, ineficiencia, valores limite.

DoD:
- Tests sin acceso a archivos ni DB.
- Cobertura minima de ramas criticas definida por el equipo (sugerido > 80% en servicio).
- Escenarios Chile y Australia validados con politicas fake.

### T9. Preparar adapter SQL (sin migracion completa)
Accion:
- Crear esqueleto SqlFlotaRepository implementando puerto y contrato de mapeo de entidad.

DoD:
- Servicio no requiere cambios para usar SQL.
- Existe prueba de contrato del repositorio.
- Conexion y credenciales via variables de entorno (sin hardcode).

### T10. Verificacion arquitectonica final
Accion:
- Ejecutar revision de dependencias y checklist de Clean Architecture.

DoD:
- Anti-acoplamiento: aprobado.
- Seguridad por diseno: sin secretos hardcodeados.
- Agnosticismo tecnologico: logica de negocio pura.
- Evidencia documental actualizada en SPECIFICATION.md y ARCHITECTURE_PLAN.md.

## Riesgos y mitigaciones
- Riesgo: sobre-ingenieria inicial de interfaces.
- Mitigacion: mantener contratos minimos y evolucionables.

- Riesgo: regresion en formulas durante refactor.
- Mitigacion: tests caracterizacion antes y despues del cambio.

- Riesgo: configuracion pais/proyecto incompleta.
- Mitigacion: validacion de configuracion al inicio y fallback controlado.
