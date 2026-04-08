# Estimación de Esfuerzo y Tareas Técnicas Granulares: Migración a Azure Blob Storage

El presente documento provee el desglose en el nivel más bajo (Story breakdown) para el plan de ejecución iterativo respecto a la migración transaccional de Base de Datos relacional hacia el Blob Storage.

---

## 1. Infraestructura Cloud (DevOps)
*Fase donde se aprovisionan los recursos fundamentales aplicando Infraestructura como Código (IaC).*

| ID | Tarea | Descripción Técnica | Perfil | Esfuerzo Estimado |
|:---:|---|---|:---:|:---:|
| **1.1** | Creación de Entornos Storage | Provisionar recursos `Storage Account` en los entornos de dev, staging y produccción vía Terraform/Bicep. | DevOps | 4 h |
| **1.2** | Configuración de Contenedores | Crear y blindar las jerarquías de contenedores desactivando categóricamente el acceso público accidental en bloque. | DevOps | 1 h |
| **1.3** | Configuración VNet & Endpoint | Desplegar Azure Private Endpoint para la cuenta de almacenamiento aislando el canal de comunicación. | DevOps | 3 h |
| **1.4** | Managed Identities / RBAC | Asignar perfil a las identidades (VM / Pod de AKS / App Service) el rol estricto `Storage Blob Data Contributor`. | DevOps | 3 h |

---

## 2. Capa de Abstracción
*Desacoplamiento agresivo de la lógica actual fuertemente ligada a EF/Dapper en persistencia binaria.*

| ID | Tarea | Descripción Técnica | Perfil | Esfuerzo Estimado |
|:---:|---|---|:---:|:---:|
| **2.1** | Diseño de `IBlueprintStorage` | Extraer la lógica pura de la DB creando contratos agnósticos para `Upload`, `Download`, `Delete` y `GenerateSas`. | Dev | 3 h |
| **2.2** | Refactorización de Dominio | Desacoplar el campo original `VARBINARY(MAX)` (Data) del Entity Framework, sustituyéndolo por un metadato `BlobUri`. | Dev | 4 h |
| **2.3** | Adaptación del Old Repository | Conformar la clase histórica en uso (ej. `SqlBlueprintRepository`) para que también implemente la nueva interfaz en transición. | Dev | 2 h |

---

## 3. Implementación Azure SDK
*Implementación del core funcional haciendo uso de las librerías oficiales contra los recursos Cloud.*

| ID | Tarea | Descripción Técnica | Perfil | Esfuerzo Estimado |
|:---:|---|---|:---:|:---:|
| **3.1** | `AzureBlobBlueprintRepository` | Escribir código core con `Azure.Storage.Blobs` implementando Multipart Uploads (Block Blobs) en archivos gruesos. | Dev | 6 h |
| **3.2** | Implementación Polly (Http) | Inyectar directivas *Exponential Backoff* y *Circuit Breaker* en el HttpClient que habla con la API de Azure. | Dev | 5 h |
| **3.3** | Generador de Tokens SAS | Crear el método lógico utilizando credenciales de usuario delegadas (User Delegation Key) limitados a expiración corta. | Dev | 4 h |
| **3.4** | Modificación capa Controller | Refactor de los Action Results (Endpoints) para devolver `Http 302` o `Http 200 JSON` del SAS URI al Frontend. | Dev | 5 h |
| **3.5** | Inyección y Toggling | Configurar un *Feature Flag* local en IoC que instancie la Infraestructura nueva en vez de SQL a voluntad. | Dev | 2 h |

---

## 4. Lógica de Migración
*Elaboración de las herramientas en segundo plano que consolidarán el histórico y operarán en dual.*

| ID | Tarea | Descripción Técnica | Perfil | Esfuerzo Estimado |
|:---:|---|---|:---:|:---:|
| **4.1** | Dual Write Publisher | Interceptar el comando de guardado de los controladores para emitir el volcado asíncrono hacia SQL y Azure en paralelo. | Dev | 4 h |
| **4.2** | Backfill Worker Service | Construir el HostService maestro (Background Job) que lee en lotes desde SQL y sube en batches contra Azure Blob. | Dev | 8 h |
| **4.3** | Hash Corroboration Engine | Añadir al Job un validador estricto MD5 que certifique la clonación sin perder cabeceras o un solo byte del archivo CAD. | Dev | 3 h |

---

## 5. Verificación y Pruebas
*Certificación de calidad del software para asegurar la correcta tolerancia técnica y fiabilidad.*

| ID | Tarea | Descripción Técnica | Perfil | Esfuerzo Estimado |
|:---:|---|---|:---:|:---:|
| **5.1** | Test de Integración con Azurite | Escribir las pruebas transaccionales completas utilizando el contenedor local/simulador de Azure `Azurite`. | QA/SDET | 6 h |
| **5.2** | Validación E2E SAS Tickets | Casos de uso de Selenium/cypress que soliciten descarga, validando la expiración intencionada de la firma del fichero. | QA | 4 h |
| **5.3** | Prueba de Estrés y Throttling | Inyectar concurrencia masiva localmente contra Azurite visualizando la resistencia inyectada de Polly mitigando peticiones 429. | QA/DevOps | 5 h |
| **5.4** | Pruebas Exploratorias de Failover | Comprobar desconexión simulada (caída de Azure) activando el Toggle hacia SQL Server. | QA | 2 h |

---

## Resumen del Esfuerzo Requerido

*   **Esfuerzo del Rol DevOps:** 11 Horas
*   **Esfuerzo del Rol de Desarrollo (Backend):** 44 Horas
*   **Esfuerzo del Rol de Aseguramiento Calidad (QA/SDET):** 17 Horas
*   **Estimación Total Acumulada del Sprint:** **72 Horas** (Aprox. un Sprint estándar ágil para el equipo consolidado).
