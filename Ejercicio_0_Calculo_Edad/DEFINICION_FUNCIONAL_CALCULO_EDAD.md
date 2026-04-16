# Definición funcional - Cálculo de edad

## 1. Objetivo
Definir una funcionalidad que calcule la edad de una persona en años completos a partir de su fecha de nacimiento y una fecha de referencia.

## 2. Entradas
- Fecha de nacimiento.
- Fecha de referencia sobre la que se quiere calcular la edad.

## 3. Salidas
- Edad en años completos (entero no negativo).

## 4. Reglas de negocio
- La edad se calcula como años completos transcurridos.
- Si en la fecha de referencia aún no se alcanzó el cumpleaños del año en curso, se resta un año al cálculo base.
- La fecha de nacimiento debe ser menor o igual que la fecha de referencia.
- Ambas fechas deben ser válidas a nivel calendario.

## 5. Casos normales
- Nacimiento anterior a la fecha de referencia y cumpleaños ya cumplido en el año de referencia.
- Nacimiento anterior a la fecha de referencia y cumpleaños aún no cumplido en el año de referencia.

## 6. Casos límite
- Fecha de nacimiento igual a fecha de referencia (edad 0).
- Cálculo el día exacto del cumpleaños.
- Nacidos el 29 de febrero y cálculo en año no bisiesto.
- Personas con edad muy alta pero válida.

## 7. Errores
- Fecha de nacimiento inválida.
- Fecha de referencia inválida.
- Fecha de nacimiento posterior a la fecha de referencia.
- Datos vacíos o no informados cuando se recibe entrada textual.

## 8. Ejemplos de uso
- Nacimiento: 2000-04-10, Referencia: 2026-04-16, salida esperada: 26.
- Nacimiento: 2000-12-20, Referencia: 2026-04-16, salida esperada: 25.
- Nacimiento: 2026-04-16, Referencia: 2026-04-16, salida esperada: 0.
- Nacimiento: 2027-01-01, Referencia: 2026-12-31, resultado esperado: error por incoherencia de fechas.
