# ADR-001: Uso obligatorio de decimal para calculos monetarios

- Estado: Aprobado
- Fecha: 2026-04-08
- Decision owners: Arquitectura de Software / Auditoria Tecnica

## Contexto
El sistema de validacion de certificaciones debe tomar decisiones contractuales con impacto economico directo (aprobacion o rechazo de cobro). Las reglas de techo presupuestario y los casos de borde de centimos requieren exactitud decimal y comportamiento determinista.

Los tipos de coma flotante binaria (float/double) no representan con exactitud muchos valores decimales, lo que puede introducir errores acumulados y decisiones inconsistentes en comparaciones monetarias.

## Decision
Se establece como norma obligatoria:
- Todo dato monetario se modela y calcula con decimal.
- Queda prohibido usar float o double en calculos monetarios del dominio.
- Cualquier conversion externa debe normalizarse a decimal antes de evaluar reglas.

## Justificacion tecnica
1. Exactitud decimal:
   - decimal usa base 10, adecuada para importes financieros.
   - reduce errores de representacion tipicos de coma flotante binaria.

2. Consistencia contractual:
   - comparaciones como "<= 105%" deben ser estables para importes con 2 decimales.
   - evita rechazos/aprobaciones por diferencias espurias de precision.

3. Auditabilidad:
   - facilita explicar calculos a intervencion, auditoria y legal.
   - resultados reproducibles ante la misma entrada y version de reglas.

4. Cumplimiento de edge cases:
   - diferencias de 0.01 deben tratarse de forma predecible.
   - comportamiento de redondeo documentado y uniforme.

## Alternativas consideradas
1. double para rendimiento:
   - Rechazada: prioriza rendimiento sobre precision financiera.
   - Riesgo alto de errores en comparaciones de umbrales monetarios.

2. float para ahorro de memoria:
   - Rechazada: precision insuficiente para dominio financiero.

3. Enteros en centimos (long/int):
   - Parcialmente valida, pero incrementa complejidad de conversiones y expresividad.
   - No adoptada como regla general en esta fase.

## Consecuencias
### Positivas
- Mayor precision y confiabilidad de decisiones.
- Menor riesgo de disputas por desviaciones de calculo.
- Mejor trazabilidad tecnica y funcional.

### Negativas
- Potencial costo de rendimiento frente a double en calculos masivos.
- Necesidad de vigilancia en integraciones para evitar conversiones inseguras.

## Reglas de cumplimiento
- Revisiones de codigo deben bloquear uso de float/double en importes.
- Analizadores estaticos y convenciones de naming para campos monetarios.
- Pruebas unitarias especificas para centimos, limites exactos y redondeo.

## Impacto en el diseno
- Contratos de dominio monetario usan decimal.
- Regla R1 (techo de gasto) se evalua exclusivamente con decimal.
- ResultadoAuditoria conserva evidencias monetarias formateadas a 2 decimales para presentacion, sin perder exactitud de calculo.

## Estado de adopcion
- Aplicable a nuevos desarrollos desde su aprobacion.
- Refactor obligatorio de componentes existentes que incumplan esta norma.
