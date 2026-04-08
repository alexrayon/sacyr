# VALIDACION FUNCIONAL - Sistema Integral de Validacion de Certificaciones de Obra

## 1. Proposito del documento
Este documento define la especificacion funcional para un sistema de validacion de certificaciones de obra basado en Spec-Driven Development (SDD). Su objetivo es establecer, de manera verificable y no ambigua, las reglas de negocio, los criterios de aceptacion y el comportamiento esperado ante situaciones normales y de borde.

El documento no incluye implementacion tecnica ni codigo fuente.

## 2. Objetivo del sistema
Disenar un motor de reglas inmutable que determine si una propuesta de certificacion de obra es:
- Apta: puede continuar al circuito de cobro.
- Rechazada: no puede avanzar y debe registrarse con motivo de rechazo.

### 2.1 Resultado esperado por cada validacion
Cada ejecucion de validacion debe devolver:
- Estado final: Apta o Rechazada.
- Lista de reglas evaluadas con su resultado individual.
- Motivo(s) de rechazo en lenguaje funcional comprensible para negocio.
- Evidencias de calculo (valores de entrada, umbrales aplicados y resultado numerico).
- Trazabilidad temporal (fecha y hora de validacion) y version de reglas aplicada.

### 2.2 Principios funcionales clave
- Inmutabilidad: una vez evaluada una certificacion con un set de datos y una version de reglas, el resultado no debe alterarse.
- Determinismo: mismas entradas + misma version de reglas = mismo resultado.
- Auditabilidad: toda decision debe poder explicarse y reconstruirse para auditoria tecnica y financiera.
- Transparencia: las causas de rechazo deben ser claras para el Interventor Tecnico.

## 3. Alcance funcional

### 3.1 Incluye
- Validacion de aptitud para cobro de certificaciones de obra.
- Evaluacion de reglas contractuales de presupuesto, temporalidad y estado de partida.
- Emision de dictamen final (Apta/Rechazada) con motivos.
- Registro de evidencia funcional para auditoria.

### 3.2 No incluye
- Integracion con sistemas contables o de pagos.
- Firma digital de documentos.
- Gestion documental del expediente.
- Modulo de facturacion.

## 4. Perfil de usuario principal
Interventor Tecnico, responsable de supervisar la ejecucion presupuestaria.

### 4.1 Necesidades funcionales del usuario
- Confirmar rapidamente si una certificacion cumple condiciones contractuales.
- Entender por que una certificacion es rechazada.
- Ver los datos criticos que influyen en la decision.
- Conservar trazabilidad para revisiones internas y externas.

### 4.2 Responsabilidades operativas
- Revisar certificaciones propuestas.
- Validar coherencia temporal y economica de la partida.
- Aceptar decisiones del motor o derivar incidencias para subsanacion.

## 5. Definiciones de negocio
- Partida proyectada: importe presupuestado para una partida de obra.
- Acumulado historico: suma de importes certificados previamente para la misma partida.
- Certificacion actual: importe propuesto en la solicitud en curso.
- Margen legal de variabilidad: tolerancia contractual del 5% sobre partida proyectada.
- Acta de replanteo: hito formal que habilita el inicio valido de trabajos.
- Fecha de trabajos: fecha efectiva asociada a los trabajos certificados.
- Fecha de emision de certificacion: fecha de formalizacion de la certificacion.
- Estado de partida: estado administrativo de la partida (activa, finalizada, liquidada, etc.).

## 6. Entradas funcionales minimas
Para evaluar una certificacion se requieren, como minimo:
- Identificador de certificacion.
- Identificador de partida.
- Importe de partida proyectada.
- Importe acumulado historico.
- Importe de certificacion actual.
- Fecha de firma del acta de replanteo.
- Fecha de trabajos.
- Fecha de emision de certificacion.
- Estado de partida.

## 7. Salidas funcionales

### 7.1 Estructura de decision funcional
- Decision global: Apta o Rechazada.
- Resultado por regla:
  - Regla del Techo de Gasto: Cumple/No cumple.
  - Regla de Temporalidad: Cumple/No cumple.
  - Regla de Estado de Partida: Cumple/No cumple.
- Motivos de rechazo acumulables (si aplica).
- Observaciones funcionales para auditoria.

### 7.2 Politica de decision
- La certificacion es Apta solo si todas las reglas son Cumple.
- Si una o mas reglas no cumplen, la certificacion es Rechazada.
- El sistema debe informar todas las reglas fallidas, no solo la primera.

## 8. Matriz de reglas contractuales

### 8.1 Regla R1 - Techo de Gasto
Descripcion:
La suma del acumulado historico mas la certificacion actual no debe exceder el 105% de la partida proyectada.

Formula funcional:
- Total certificable = Acumulado historico + Certificacion actual
- Techo permitido = Partida proyectada x 1.05
- Cumple si: Total certificable <= Techo permitido
- No cumple si: Total certificable > Techo permitido

Resultado de rechazo sugerido:
- "Rechazo R1: el total certificable excede el 105% de la partida proyectada."

Observaciones:
- Debe conservarse evidencia del calculo con al menos 2 decimales en presentacion.
- El umbral del 105% se considera inclusivo en el limite exacto.

### 8.2 Regla R2 - Temporalidad
Descripcion:
La fecha de los trabajos debe ser posterior a la firma del acta de replanteo y anterior a la fecha de emision de la certificacion.

Condicion funcional:
- Cumple si: Fecha trabajos > Fecha acta replanteo y Fecha trabajos < Fecha emision certificacion
- No cumple en cualquier otro caso

Resultado de rechazo sugerido:
- "Rechazo R2: incoherencia temporal entre acta de replanteo, fecha de trabajos y emision de certificacion."

Observaciones:
- La comparacion es estricta (no se admiten igualdades en limites).
- Debe registrarse la terna de fechas para auditoria.

### 8.3 Regla R3 - Estado de Partida
Descripcion:
Bloqueo automatico si la partida esta marcada como Finalizada o Liquidada.

Condicion funcional:
- Cumple si: Estado partida no es Finalizada y no es Liquidada
- No cumple si: Estado partida es Finalizada o Liquidada

Resultado de rechazo sugerido:
- "Rechazo R3: la partida se encuentra cerrada para nuevas certificaciones (Finalizada/Liquidada)."

Observaciones:
- Esta regla tiene caracter de bloqueo administrativo.
- El sistema debe impedir la aptitud incluso si R1 y R2 cumplen.

## 9. Prioridad y combinacion de reglas
- Todas las reglas se evaluan en la misma corrida de validacion.
- La decision final es por conjuncion logica (AND) entre R1, R2 y R3.
- Si hay multiples incumplimientos, se emiten todos los motivos en un unico resultado.

## 10. Criterios de aceptacion (Gherkin)

Escenario: Validacion exitosa
Dado una partida proyectada de 100000.00
Y un acumulado historico de 95000.00
Y una certificacion actual de 8000.00
Y una fecha de acta de replanteo 2026-01-10
Y una fecha de trabajos 2026-02-15
Y una fecha de emision de certificacion 2026-02-28
Y una partida en estado Activa
Cuando se ejecuta la validacion de certificacion
Entonces el resultado global debe ser Apta
Y la regla R1 debe Cumplir
Y la regla R2 debe Cumplir
Y la regla R3 debe Cumplir

Escenario: Rechazo por exceso de presupuesto
Dado una partida proyectada de 100000.00
Y un acumulado historico de 102000.00
Y una certificacion actual de 4000.00
Y una fecha de acta de replanteo 2026-01-10
Y una fecha de trabajos 2026-02-15
Y una fecha de emision de certificacion 2026-02-28
Y una partida en estado Activa
Cuando se ejecuta la validacion de certificacion
Entonces el resultado global debe ser Rechazada
Y la regla R1 debe No cumplir
Y el motivo debe indicar exceso sobre el 105% de la partida proyectada

Escenario: Rechazo por incoherencia de fechas
Dado una partida proyectada de 100000.00
Y un acumulado historico de 90000.00
Y una certificacion actual de 5000.00
Y una fecha de acta de replanteo 2026-03-01
Y una fecha de trabajos 2026-02-15
Y una fecha de emision de certificacion 2026-03-20
Y una partida en estado Activa
Cuando se ejecuta la validacion de certificacion
Entonces el resultado global debe ser Rechazada
Y la regla R2 debe No cumplir
Y el motivo debe indicar incoherencia temporal

## 11. Analisis de casos de borde (Edge Cases)

### 11.1 Certificacion de importe cero
Regla funcional:
- Una certificacion con importe actual igual a 0.00 no genera incremento economico.
- Debe evaluarse igualmente contra R2 y R3.
- En R1, el total certificable coincide con el acumulado historico.

Comportamiento esperado:
- Si R1, R2 y R3 cumplen, el resultado puede ser Apta.
- Si alguna regla falla, el resultado debe ser Rechazada con motivo correspondiente.

Riesgo controlado:
- Evitar rechazos automaticos por el mero hecho de ser importe cero cuando formalmente cumple.

### 11.2 Ajustes de centimos y redondeo financiero
Regla funcional:
- Los importes deben tratarse con precision monetaria de 2 decimales para presentacion.
- Para comparaciones de techo (R1), la logica debe evitar errores por redondeo binario.
- En caso de diferencia minima por redondeo (ejemplo: 0.01), prevalece la regla contractual exacta sobre el valor monetario normalizado.

Comportamiento esperado:
- Si el total certificable supera el techo permitido en 0.01 tras normalizacion monetaria valida, debe ser Rechazada.
- Si el total coincide exactamente con el techo permitido, debe ser Apta en R1.

Riesgo controlado:
- Prevenir aprobaciones o rechazos inconsistentes por precision numerica.

## 12. Requisitos de trazabilidad y auditoria
- Registrar para cada validacion:
  - Identificador unico de ejecucion.
  - Fecha/hora de evaluacion.
  - Version de reglas aplicada.
  - Datos de entrada utilizados.
  - Resultado por regla y dictamen final.
  - Motivos de rechazo y evidencias numericas/temporales.
- Los registros deben ser inmutables y consultables para auditoria tecnica.

## 13. Reglas de calidad funcional
- Claridad: cada rechazo debe ser accionable para subsanacion.
- Coherencia: no debe existir contradiccion entre resultado global y resultados por regla.
- Repetibilidad: la misma certificacion evaluada con la misma version produce el mismo dictamen.
- Completitud: no debe emitirse dictamen si faltan datos obligatorios.

## 14. Supuestos y restricciones
- Se asume que las fechas de entrada son validas en formato calendario.
- Se asume que los importes monetarios llegan en moneda homogenea.
- Si faltan datos obligatorios, la evaluacion funcional debe considerarse invalida y no emitir aptitud.
- Cualquier cambio futuro en margen legal (hoy 5%) debera versionarse explicitamente en reglas.

## 15. Criterio de exito de esta especificacion
La especificacion se considera exitosa cuando permite:
- Traducir reglas de negocio a pruebas funcionales sin ambiguedad.
- Auditar decisiones de aceptacion/rechazo con evidencia suficiente.
- Implementar el motor de validacion sin reinterpretaciones contractuales.
