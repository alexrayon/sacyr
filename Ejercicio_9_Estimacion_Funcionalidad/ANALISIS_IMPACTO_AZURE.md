# Informe de Análisis de Impacto: Migración a Azure Blob Storage

**Fecha:** 8 de abril de 2026
**Autor:** Consultor de Arquitectura Cloud (Azure)
**Proyecto:** Migración de Almacenamiento de Planos Técnicos
**Origen:** SQL Server
**Destino:** Azure Blob Storage

---

## 1. Análisis de Capas Afectadas

La migración del almacenamiento físico y binario desde una base de datos relacional (SQL Server) hacia un almacenamiento de objetos estáticos (Azure Blob Storage) introduce cambios significativos en el diseño de las capas de la aplicación.

### 1.1. Capa de Configuración (Secrets)
*   **Impacto Tecnológico:** Sustitución o ampliación de las cadenas de conexión. Se requerirá integrar de manera segura las credenciales y Endpoints de resiliencia regionales de Azure.
*   **Gestión de Secretos:** Implementación de **Azure Key Vault** u otros gestores corporativos para almacenar de forma segura la cadena de conexión de la cuenta de almacenamiento (`Storage Account Connection String`), identificadores de cliente (Client ID) y certificados/secretos (Client Secrets) en caso de usar Service Principals o Managed Identities en las VMs/App Services.
*   **Rotación de Credenciales:** Definición de políticas de rotación de claves nativas en Azure y su propagación automática en el clúster sin necesidad de reciclar el pool de aplicaciones (evitando downtime por cambio de claves).

### 1.2. Capa de Persistencia (Repositorios)
*   **Impacto Tecnológico:** Los repositorios de código encargados de gestionar la entidad "Planos Técnicos" deberán bifurcar su lógica de lectura/escritura (patrón de diseño *Repository / Adapter*).
*   **Desacoplamiento Estructural:** SQL Server pasará a guardar únicamente el modelo relacional o de negocio (metadatos del plano: ID, Nombre, Versión, ID_Proyecto, CreadoPor, y la **URI de referencia lógica en el Blob**). Por el contrario, Azure Blob Storage almacenará exclusivamente el *payload* o BLOB (el archivo físico PDF, DWG, BIM, etc.).
*   **Optimización del ORM/DB:** Se abandonará o modificará el patrón de mapeo sobre campos de tipo `VARBINARY(MAX)`. Esto reducirá drásticamente los cuellos de botella de red, los bloqueos en tablas y la demanda de memoria en el propio motor SQL Server (el buffer pool quedará liberado).
*   **Transaccionalidad (ACID):** Se pierde la integridad atómica innata de un solo commit de SQL Server. La guarda estructurada y física será distribuida, requiriendo el manejo explícito de compensaciones o implementación del Patrón _Saga_: si falla el guardado de metadatos tras subir el archivo, el archivo quedará huérfano y el sistema debe purgarlo o reintentarlo.

### 1.3. Capa de Presentación (APIs de Descarga)
*   **Impacto Tecnológico:** Las llamadas de lectura de la API dejarán de recibir peticiones intensivas donde los servidores de aplicación serializan y empujan secuencias binarias masivas a los clientes.
*   **Redirección de la Carga (Offloading):** Se modificará la lógica de la API para que opere mediante redirecciones estáticas o resoluciones indirectas: el servidor proporcionará un enlace pre-firmado (**SAS URI**) mediante código HTTP (ej. `302 Found` o una respuesta en JSON) para que el navegador del usuario descargue el elemento *directamente* desde los nodos CDN de Azure Blob Storage.
*   **Rendimiento:** Disminución radical del consumo de ancho de banda y latencia en los nodos de cómputo transaccionales, lo que facilitará escalas más sostenibles y baratas.

---

## 2. Matriz de Riesgos Técnicos

La transición del monolito persistente a un entorno de almacenamiento fragmentado y en la nube necesita mapear los riesgos a priorizar.

| Riesgo Técnico | Descripción del Escenario | Impacto (1-5) | Probabilidad (1-5) | Estrategia de Mitigación |
|---|---|:---:|:---:|---|
| **Latencia de Red / Resolución DNS** | Aumento de los tiempos de respuesta debido a múltiples saltos en Internet para alcanzar o escribir contenidos pesados en zonas de Azure externas a otras infraestructuras. | 3 | 4 | Uso de **Azure Private Links** (Endponits privados en la red VNet). Interposición de **Azure CDN/Front Door** en la capa de presentación para cacheo en el borde y mejora en los tiempos de lectura repetitiva de un plano. |
| **Consistencia Eventual / Orphaned Blobs** | Discordancia inter-bases: un plano borrado en SQL Server pero persistido en Blob (gasto innecesario de storage); o un registro en DB apuntando a un archivo que ha fallado al subir por TimeOut. | 4 | 3 | Implementar resiliencia (Wait and Retry). Establecer políticas de *LifeCycle Management* en Azure Blob y crear un *Worker / Function App* auxiliar encargado de la reconciliación y limpieza periódica de blobs o metadatos inconsistentes. |
| **Seguridad de la Firma y Fuga de SAS** | Generación de firmas Shared Access Signatures asociadas a contextos muy amplios (un contenedor en vez de un solo documento) y/o plazos de caducidad gigantes, abriendo la puerta a explotación o filtrado. | 5 | 2 | Aplicar "Principio de Menor Privilegio". Firmar usando **User Delegation Keys** vinculadas a Azure Active Directory. Acortar el tiempo de validez de las SAS al mínimo esencial (ej. 5 min), limitando exhaustivamente la política a `Read-Only` por un solo ID de Blob. |
| **Throttling y Límites de Entrada/Salida (IOPS)** | Superar el número máximo de operaciones permitidas por nodo en el Standard o Premium Blob, provocando la penalización y eventual encolamiento forzado de recursos. | 3 | 2 | Establecimiento de particionamiento lógico de las cuentas de Storage o uso de la versión "Premium Block Blobs". Aplicación en código del patrón *Exponential Backoff* para readaptar la velocidad en respuestas de exceso de uso. |

---

## 3. Definición de Requisitos de Migración ('Zero Downtime')

Para ejecutar un proyecto de cambio central sin afectar la productividad y el tiempo de respuesta del personal que maneja de forma continua Planos Técnicos, se propone emplear el enfoque **Parallel Run Workflow** o Dark Launching:

1.  **Activación de Escritura Dual Asíncrona:** A partir del despliegue inicial, el Core de la aplicación guardará el binario transaccionalmente de la forma convencional y, en un hilo secundario (Background Task o Service Bus), enviará una réplica a Azure Blob.
2.  **Backfilling e Ingreso Masivo:** Uso de herramientas de movimiento masivo o scripts ad-hoc (e.g., *Azure Data Factory, AzCopy*) para migrar por bloques el almacenamiento histórico desde SQL Server (extracción binaria a disco y posterior subida al bucket). Durante este proceso se validarán los Hashes (ej. MD5) para constatar la integridad absoluta.
3.  **Provisión mediante Feature Toggling (Canary Read):** A nivel de la capa API, se desplegará una bandera de característica o *"Toggle"*. Para un grupo de usuarios y/u oficinas de prueba (User Acceptance Testing), el API redirigirá sus descargas exclusivamente al Azure Blob Storage. El resto seguirá consumiendo SQL Server.
4.  **Monitoreo del Fallback:** La lectura de un plano mediante el Toggle de nueva arquitectura incluirá un manejador de caída (Fallback); si no es encontrado o existe latencia severa (TimeOut Azure), revertirá de forma transparente leyendo temporalmente de la DB original mientras se loguea la incidencia en telemetría.
5.  **Depreciación / Cut-over Definitivo:** Posterior a la migración histórica 100% veraz y habiendo superado los KPIs de estrés, el *Feature Toggle* se propaga globalmente a toda la capa, cesando las escrituras en SQL Server para el Binario de Archivo y permitiendo una posterior depuración que recuperará espacio en disco del modelo SQL.

---

## 4. Casos de Uso de Fallo 

En un sistema puramente en memoria y red, debe implementarse software preparado para fallar, asumiendo lo siguiente:

### Escenario A: Expiración Prematura de Tokens (SAS Timeout en Baja Cobertura)
*   **Fallo:** Durante una descarga por WiFi móvil irregular (típico en obra o inspección del terreno por partes para la constructora), una bajada transitoria de red ralentiza la bajada del plano. El tiempo transcurre, la ventana de 5 minutos del token SAS expira, devolviendo la nube de Azure un rotundo `403 Forbidden` interrumpiendo un plano del cual ya se había bajado el 85%.
*   **Soporte Técnico en Código:** El cliente Web/Desktop debe identificar rápidamente el tipo de pérdida. Tras recibir el código 403 o una rotura del *Stream*, el cliente ha de re-solicitar a su API central un token *nuevo* válido sin intervención del operador, reanudando la descarga invocando *HTTP 206 Partial Content* pasando a la nube los Range Headers especificando que continuará obteniendo bytes donde había quedado sin tener que arrancar desde el 0%.

### Escenario B: Throttling de Componentes Cloud (Limitación de Rendimiento)
*   **Fallo:** Lanzamiento en la madrugada de un proceso de Inteligencia de Negocio que lee cientos de miles de planos en paralelo, ocasionando que Blob Storage rechace parte de los flujos de lectura devolviendo recurrentemente estados HTTP `429 Too Many Requests` o `503 Server Busy`.
*   **Soporte Técnico en Código:** El sistema orquestador deberá interceptar el código 429 y abstenerse de enviar más saturación pura. Hará uso imperativo de políticas como **Jitter and Exponential Backoff**. Por medio de bibliotecas de resiliencia (Polly en base .NET), se obligará a detener al nodo cliente un intervalo pseudo-aleatorio que incrementará progresivamente (1 seg, 3 seg, 8 seg...), espaciando en el tiempo el flujo de solicitudes y facilitando de esta forma la recuperación elástica de Azure.

### Escenario C: Fallos de Conectividad Temporal (Micro-Cortes WAN durante Subida)
*   **Fallo:** Un problema intermedio de BGP, saturación de la fibra óptica o ExpressRoute induce una caída total de conexión a la nube durante unos segundos en medio del 'Upload' pesado de un documento técnico BIM/DWG.
*   **Soporte Técnico en Código:** Desistimiento de subidas estáticas unificadas (Single-Put/Monolithic upload) y obligatoriedad en el uso nativo de las capacidades **Block Blobs Upload (REST API / SDK)**. Al realizar la segmentación subiendo bloque por bloque individual de pocos Megabytes e ir validando su confirmación a nivel red, si ocurre el micro-corte se detecta el Chunk dañado o no enviado. El sistema repetirá únicamente las partes del puzzle que fracasaron garantizando el *Eventually Success*.
