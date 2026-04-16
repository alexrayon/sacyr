# Diseño técnico - Utilidad de cálculo de edad en C# (.NET)

## 1. Responsabilidad del componente
Crear un componente utilitario que reciba fechas de nacimiento y referencia, valide entradas y devuelva la edad en años completos de forma determinista.

## 2. Firma del método
- Método principal orientado a dominio: recibe fecha de nacimiento y fecha de referencia como fechas.
- Método de conveniencia para entrada textual: recibe dos textos de fecha, los valida y delega en el método principal.

## 3. Validaciones previas
- Verificar que las fechas no sean valores no inicializados.
- Si la entrada es textual, verificar que no esté vacía.
- Si la entrada es textual, validar formato y parseo de fecha.
- Verificar que fecha de nacimiento no sea posterior a fecha de referencia.

## 4. Regla de cálculo
- Calcular diferencia inicial entre años de referencia y nacimiento.
- Construir el aniversario de nacimiento en el año de referencia.
- Si la fecha de referencia es anterior al aniversario, decrementar en uno.
- Devolver la edad resultante.

## 5. Comportamiento ante errores
- Error explícito de argumento cuando una fecha no es válida o no se puede interpretar.
- Error explícito de rango cuando la fecha de nacimiento es posterior a la fecha de referencia.
- Mensajes de error claros y accionables para diagnóstico funcional.

## 6. Criterios técnicos de calidad
- Cálculo legible y sin ambigüedad.
- Sin uso de aproximaciones por días o meses.
- Salida inmutable y sin efectos laterales.
