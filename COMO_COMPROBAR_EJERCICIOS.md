# Cómo Comprobar los Ejercicios de Sacyr

Este documento explica cómo verificar que cada ejercicio del proyecto está funcionando correctamente. Todos los ejercicios están ubicados en subcarpetas dentro de la raíz:

`C:\Users\alvar\Desktop\Brain&Code\Sacyr`

## Requisitos Generales
- .NET 10.0 instalado
- PowerShell para ejecutar comandos
- Navegar a la raíz del proyecto antes de ejecutar los comandos.
- Atención: la ruta contiene `&`, por lo que debe ir entre comillas.
  ```powershell
  cd 'C:\Users\alvar\Desktop\Brain&Code\Sacyr'
  ```

## Ejercicio_0_bis_Detccion_de_anomalias

**Descripción:** Detector de anomalías en datos de obras.

**Pasos para comprobar:**
1. Compilar:
   ```powershell
   cd Ejercicio_0_bis_Detccion_de_anomalias\Final\AnomaliaDetector; dotnet build
   ```
2. Ejecutar usando los archivos de ejemplo existentes:
   ```powershell
   dotnet run -- obras_validas.txt reporte_validas.txt txt
   ```
   - El directorio actual contiene ejemplos como `obras_validas.txt`, `obras_con_anomalias.txt`, `reporte_validas.txt` y `reporte_anomalias.txt`.
   - Usa `obras_validas.txt` como entrada y crea un reporte de salida como `reporte_validas.txt`.
   - El último parámetro puede ser `txt` o `json` según el formato deseado.
3. Verificar salida: Debe procesar los datos y generar el archivo de reporte en la misma carpeta.

## Ejercicio_0_Calculo_Edad

**Descripción:** Calculadora de edad basada en fechas.

**Pasos para comprobar:**
1. Compilar:
   ```powershell
   cd Ejercicio_0_Calculo_Edad\Final; dotnet build
   ```
2. Ejecutar: `dotnet run`
3. Verificar: Introduce fechas y confirma cálculos correctos de edad.

## Ejercicio_01_Certificaciones

**Descripción:** Motor de validación de certificaciones de obra basado en reglas contractuales (presupuesto, temporalidad y estado de partida).

**Pasos para comprobar:**
1. Compilar solución y proyectos:
   ```powershell
   cd Ejercicio_01_Certificaciones\Final\ValidacionCertificaciones
   dotnet build
   ```
2. Ejecutar tests automáticos:
   ```powershell
   dotnet test
   ```
3. (Opcional) Ejecutar consola de demostración:
   ```powershell
   dotnet run --project src\ValidacionCertificaciones.Console\ValidacionCertificaciones.Console.csproj
   ```
4. Verificar: Los tests deben validar reglas R1 (techo de gasto), R2 (temporalidad) y R3 (estado de partida), incluyendo escenarios de aprobación y rechazo.

## Ejercicio_2_Arqueologia_de_Software

**Descripción:** Refactorización de lógica de resistencia estructural.

**Pasos para comprobar:**
1. Compilar:
   ```powershell
   cd Ejercicio_2_Arqueologia_de_Software\Base\Ejercicio2_base
   dotnet build
   ```
2. Ejecutar tests:
   ```powershell
   dotnet test
   ```
   (si existe un proyecto de tests)
3. Verificar: Código refactorizado con mejor mantenibilidad.

## Ejercicio_3_Ingenieria_Robustez

**Descripción:** Sistema de telemetría robusta con idempotencia.

**Pasos para comprobar:**
1. Compilar:
   ```powershell
   cd Ejercicio_3_Ingenieria_Robustez\Ejercicio3
   dotnet build
   ```
2. Ejecutar:
   ```powershell
   dotnet run
   ```
3. Verificar: Manejo de errores y telemetría idempotente.

## Ejercicio_4_Evaluacion_Calidad

**Descripción:** Cálculo de margen de riesgo con refactorización.

**Pasos para comprobar:**
1. Compilar:
   ```powershell
   cd Ejercicio_4_Evaluacion_Calidad\Ejercicio4
   dotnet build
   ```
2. Ejecutar tests:
   ```powershell
   cd Ejercicio_4_Evaluacion_Calidad\Ejercicio4.Tests
   dotnet test
   ```
3. Verificar: Tests pasan y lógica aplanada.

## Ejercicio_5_Auditoria

**Descripción:** Controlador de maquinaria con autorización segura.

**Pasos para comprobar:**
1. **Compilación:**
   ```powershell
   cd Ejercicio_5_Auditoria\Ejercicio5
   dotnet build
   ```
   - Esperado: "Compilación correcto" sin errores.

2. **Tests Automáticos:**
   ```powershell
   cd Ejercicio_5_Auditoria\Ejercicio5.Tests
   dotnet test
   ```
   - Esperado: "Resumen de pruebas: total: 3; con errores: 0; correcto: 3".

**Resultado:** Si todos pasan, el controlador está blindado y funcional.

## Ejercicio_06_Refactorizacion

**Descripción:** Refactorización SRP del cierre de proyectos, separando orquestación, persistencia, notificación y generación de reportes mediante contratos.

**Pasos para comprobar:**
1. Compilar proyecto principal:
   ```powershell
   cd Ejercicio_06_Refactorizacion\Final\Ejercicio6-Final
   dotnet build
   ```
2. Ejecutar tests del servicio de cierre:
   ```powershell
   cd ..\Ejercicio6-Final.Tests
   dotnet test
   ```
3. Ejecutar demo de cierre:
   ```powershell
   cd ..\Ejercicio6-Final
   dotnet run
   ```
4. Verificar: El cierre se ejecuta sin errores, se muestra el balance final por consola y los tests confirman el comportamiento del servicio desacoplado.

## Ejercicio_7_Desarrollo

**Descripción:** Módulo de Telemetría TBM con Clean Architecture, mitigación de errores de punto flotante (IEEE-754) para validación de umbrales exactos y casos de borde.

**Pasos para comprobar:**
1. **Compilar la solución:**
   ```powershell
   cd Ejercicio_7_Desarrollo\TbmTelemetry
   dotnet build
   ```
2. **Ejecutar Tests Automáticos (Comprobación de Escenarios y Limites):**
   ```powershell
   cd Ejercicio_7_Desarrollo\TbmTelemetry
   dotnet test
   ```
   - Esperado: Todos los tests deben figurar en verde (`Superado: 7`), validando los escenarios `En Ruta`, `Precaución`, `Crítico`, límite preciso de 5.0cm y las sentencias de guarda sobre valores imposibles del sensor.

## Ejercicio_08_Test-Plans

**Descripción:** Plan y automatización de pruebas para motor de geofencing de seguridad (frontera 50m, alertas y robustez ante jitter GPS).

**Pasos para comprobar:**
1. Compilar el proyecto de pruebas:
   ```powershell
   cd Ejercicio_08_Test-Plans\Final\Ejercicio8-Final
   dotnet build
   ```
2. Ejecutar test suite:
   ```powershell
   dotnet test
   ```
3. Verificar: Deben pasar pruebas de borde (49.9/50.0/50.1m), no duplicación de alertas de entrada y rechazo de coordenadas nulas sin mutar estado.

## Ejercicio_9_Estimacion_Funcionalidad

**Descripción:** Auditoría de rendimiento, análisis de impacto arquitectónico, ADR y codificación del repositorio C# para migración a Azure Blob Storage.

**Pasos para comprobar:**
1. Al ser una libreria con hacer el comando dotnet build y ver que no hay errores ya deberia estar lista para su uso
2. Compilar y ejecutar tests:
   ```powershell
   cd Ejercicio_9_Estimacion_Funcionalidad\Tests
   dotnet test
   ```
3. Verificar: Éxito de los tests de integración simulando infraestructura en Azure.

## Ejercicio_10_Optimizacion

**Descripción:** Optimización de consulta SQL Server para reporte de consumo de maquinaria, eliminando subconsultas correlacionadas y añadiendo índices de cobertura.

## Ejercicio_11_Ciencia_Datos

**Descripción:** Flujo de mantenimiento predictivo con Machine Learning para detección de fallo en maquinaria a partir de sensores.

**Pasos para comprobar:**
1. Ir a la carpeta del ejercicio:
   ```powershell
   cd Ejercicio_11_Ciencia_Datos
   ```
2. Instalar dependencias necesarias (si no están instaladas):
   ```powershell
   pip install pandas matplotlib seaborn scikit-learn
   ```
3. Ejecutar el script principal:
   ```powershell
   python predictivo.py
   ```
4. Verificar:
   - Debe localizar `sensores_maquinaria.csv` sin error.
   - Debe mostrar métricas de evaluación (Recall, Classification Report y matriz de confusión).
   - Debe generar/actualizar la imagen `correlacion_fallos.png` en la misma carpeta.

## Ejercicio_12_APIs

**Descripción:** Monitor de viento robusto para operación de grúas, con consumo de API, validación de contrato y degradación segura.

**Pasos para comprobar:**
1. Ir a la carpeta del ejercicio:
   ```powershell
   cd Ejercicio_12_APIs
   ```
2. Instalar dependencia HTTP (si no está instalada):
   ```powershell
   pip install requests
   ```
3. (Opcional) Crear archivo de entorno desde plantilla:
   ```powershell
   copy .env.example .env
   ```
   - Si no se configura `WEATHER_API_BASE_URL`, el monitor usa simulador local automáticamente.
4. Ejecutar el monitor:
   ```powershell
   python monitor_viento.py
   ```
5. Verificar:
   - Debe refrescar la salida cada 10 segundos.
   - Debe mostrar `ESTADO NORMAL`, `ALERTA AMBAR` o `ALERTA ROJA` según velocidad del viento.
   - Ante fallo de API/timeout debe mostrar `ALERTA DE SISTEMA`.

## Ejercicio_13_Migracion_Angular

**Descripción:** Migración de componente middleware a Angular 21 con enfoque moderno (signals y control flow) y preparación zoneless.

**Pasos para comprobar:**
1. Ir al proyecto Angular:
   ```powershell
   cd Ejercicio_13_Migracion_Angular\visor-middleware-v21
   ```
2. Instalar dependencias:
   ```powershell
   npm install
   ```
3. Compilar proyecto:
   ```powershell
   npm run build
   ```
4. Ejecutar tests:
   ```powershell
   npm test
   ```
5. Levantar la aplicación para validación visual:
   ```powershell
   npm start
   ```
6. Verificar:
   - La aplicación arranca sin errores de compilación.
   - El componente migrado mantiene el comportamiento funcional esperado.
   - No aparecen regresiones en renderizado ni en flujo de carga en la vista.

