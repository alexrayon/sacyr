# Tareas de implementación - Utilidad de cálculo de edad

## 1. Preparar estructura del ejercicio
- Orden: 1
- Dependencia: ninguna
- Acción: crear carpeta del ejercicio y proyecto .NET para la utilidad.
- Comprobación: el proyecto compila vacío.

## 2. Definir contrato del componente
- Orden: 2
- Dependencia: tarea 1
- Acción: definir componente utilitario y métodos de cálculo (fechas tipadas + entrada textual).
- Comprobación: firma disponible y accesible desde el programa principal.

## 3. Implementar validaciones de entrada
- Orden: 3
- Dependencia: tarea 2
- Acción: validar nulos lógicos, texto vacío, parseo inválido y rango cronológico inválido.
- Comprobación: casos de error lanzan excepciones explícitas con mensaje claro.

## 4. Implementar regla de cálculo de años completos
- Orden: 4
- Dependencia: tarea 3
- Acción: calcular diferencia anual y ajustar por cumpleaños no alcanzado.
- Comprobación: casos normales devuelven edad esperada.

## 5. Añadir comprobaciones de casos principales
- Orden: 5
- Dependencia: tarea 4
- Acción: incorporar validaciones sencillas en ejecución para casos normales, límite y error.
- Comprobación: se muestran resultados correctos y fallos controlados.

## 6. Ejecutar validación final del ejercicio
- Orden: 6
- Dependencia: tareas 1 a 5
- Acción: compilar y ejecutar.
- Comprobación: compilación exitosa y salida alineada con la definición funcional.
