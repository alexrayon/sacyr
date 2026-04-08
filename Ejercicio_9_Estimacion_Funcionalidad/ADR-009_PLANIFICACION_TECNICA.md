# PLANIFICACIÓN TÉCNICA Y ADR-009: Migración a Azure Blob Storage

En base a las directrices establecidas en el documento de `ANALISIS_IMPACTO_AZURE.md`, a continuación se detalla la hoja de ruta técnica y arquitectónica para la migración del almacenamiento de planos.

---

## 1. Diseño del Nuevo Repositorio

Para cumplir con el desacoplamiento estructural y el patrón *Repository / Adapter*, la lógica de persistencia binaria se abstraerá a través de la interfaz `IBlueprintStorage`.

### Estructura Propuesta

```csharp
// Interfaz base en la capa de uso (Dominio / Casos de Uso)
public interface IBlueprintStorage
{
    Task<string> UploadBlueprintAsync(string blueprintId, Stream fileStream, CancellationToken cancellationToken);
    Task<Stream> DownloadBlueprintAsync(string blobUri, CancellationToken cancellationToken);
    Task<string> GenerateDownloadSasUriAsync(string blobUri, TimeSpan expirationTime);
    Task DeleteBlueprintAsync(string blobUri, CancellationToken cancellationToken);
}

// Implementación en la capa de Infraestructura
public class AzureBlobBlueprintRepository : IBlueprintStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    
    // Inyección del cliente de Azure configurado mediante DI
    public AzureBlobBlueprintRepository(BlobServiceClient blobServiceClient, IOptions<BlobStorageSettings> settings)
    {
        _blobServiceClient = blobServiceClient;
        _containerClient = _blobServiceClient.GetBlobContainerClient(settings.Value.ContainerName);
    }

    public async Task<string> UploadBlueprintAsync(string blueprintId, Stream fileStream, CancellationToken cancellationToken)
    {
        // Uso obligatorio de soporte para segmentación de grandes archivos (Block Blobs)
        var blobClient = _containerClient.GetBlobClient($"{blueprintId}.pdf");
        var options = new BlobUploadOptions { TransferOptions = new StorageTransferOptions { MaximumTransferSize = 4 * 1024 * 1024 } }; // 4MB chunks
        await blobClient.UploadAsync(fileStream, options, cancellationToken);
        
        return blobClient.Uri.ToString();
    }
    
    // ... (Implementación del resto de la interfaz)
}
```

---

## 2. Architecture Decision Record (ADR-009)

**Título:** ADR-009 - Autenticación Nativa e Inyección de Dependencias para Azure Storage
**Estado:** Aprobado
**Fecha:** 8 de Abril de 2026

**Contexto:**
Se requiere conectar de manera segura y flexible los ecosistemas .NET de Sacyr con Azure Blob Storage para almacenar un alto volumen de binarios transaccionales, prescindiendo del bloqueador que suponía `VARBINARY(MAX)` en SQL Server.

**Decisión:**
1.  **Identidades Administradas (Managed Identities):** Se prescindirá de `Storage Account Connection Strings` estáticos y Secretos de Cliente en favor de Identidades Administradas por el sistema asociadas al App Service/AKS donde resida la API. 
2.  **Inyección de Dependencias (DI):** Se inyectará el `IBlueprintStorage` mediante el contenedor IoC de .NET, permitiendo en un inicio inicializar el antiguo servicio `SqlBlueprintRepository` y posteriormente registrar `AzureBlobBlueprintRepository`.

**Justificación:**
*   La Identidad Administrada elimina el riesgo de fuga documentada en el informe de impacto (Gestión de Secretos) ya que Microsoft Entra ID (Azure AD) rota por debajo los certificados sin requerir reinicios o configuraciones en el *App Service*. Cumplimos de primera mano las políticas de "Cero Downtime" por caducidad de credenciales.
*   El uso del patrón DI obedece a la necesidad del flujo de *Parallel Run Workflow*. Simplemente añadiendo una bandera al `appsettings.json` o servicio de *Feature Toggles*, el contenedor inyectará un repositorio u otro, sin tocar una sola pieza del código de lógica de negocio.

---

## 3. Estrategia de Migración de Datos ('Zero Downtime' Data Sync)

Mover múltiples Terabytes de planos sin interrumpir el servicio requiere una estrategia de conciliación en segundo plano y transicional. 

**Plan Técnico Background Workers (.NET Hosted Services):**

1.  **Fase 1 - Dual Write Inmediato:** El caso de uso de "Guardar Plano" escribirá activamente el archivo en el `SqlBlueprintRepository` **y** notificará de forma asíncrona (ej. publicando un evento de dominio o usando Azure Service Bus) a un Worker que lo persistirá en `AzureBlobBlueprintRepository`.
2.  **Fase 2 - Backfill Job:** Un componente estructurado como `BackgroundService` en .NET ejecutará de forma recurrente durante las horas valle un proceso de drenado de la DB.
    *   *Lectura Pagina (Batches):* Descargará lotes de 100 planos antiguos mediante Dapper/EF.
    *   *Volcado:* Hará el Upload al Storage usando Azure Blob SDK.
    *   *Checksum Check:* Se validará la simetría binaria comparando el Content-MD5 (Checksum Hash) de Azure contra la firma local para certificar tolerancia nula a fallos o corrupción.
    *   *Ack:* Actualizará la tabla SQL asignando la nueva `AzureURI`.
3.  **Fase 3 - Canary & Reconciliación:** Un Worker secundario o *Garbage Collector* re-buscará discrepancias ocasionales y limpiará *Orphaned Blobs*. Activado el Toggle en la capa Web, el front end comenzará a resolver descargas mediante SAS URIs pre-firmadas directamente a la nube.

---

## 4. Diseño de Resiliencia en Red (.NET y Polly)

Tratándose de una arquitectura distribuida expuesta a micro-cortes de ExpressRoute y limitaciones estipuladas de los clústeres externos (Azure Throttling), estableceremos resiliencia activa en el código mediante patrones tácticos:

### 4.1. Retry con Exponential Backoff y Jitter 
Aplicado para combatir y protegerse ante escenarios HTTP 429 y 503 limitando el Throttling agresivo.

```csharp
// Configuración Polly en inyección de dependencias HTTP
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError() // Intercepta fallos de red 5xx y 408
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // 429 Azure Throttling
    .WaitAndRetryAsync(retryCount: 5, sleepDurationProvider: retryAttempt => 
        // Backoff Exponencial con Jitter (aleatoriedad) para que todos los workers no reintenten en masa.
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
        + TimeSpan.FromMilliseconds(new Random().Next(0, 1000)));
```

### 4.2. Circuit Breaker (Cortocircuito en caso de Colapso WAN)
Aplicaremos *Circuit Breaker* si se detecta un corte sostenido del enlace del enrutador o fallo regional mayor en el Cloud.
*   Si fallan más de *N* subidas consecutivas (ej. 15), el interruptor "se abre".
*   Las cargas subsiguientes fallarán rápida y explícitamente (Fail-Fast) deteniendo el flujo para que el cliente no se quede bloqueado, guardándose previsoramente estas peticiones y archivos en una cola local temporal (Fallback / Outbox Local) para procesarse y ser enviados cuando el circuito efectúe el auto-arreglo.
*   Asímismo, para el *Upload* a nivel código (como visto en el punto 1), todo uso pasa por "Block Blobs" asumiendo los micro-cortes sin requerir subir todo desde un principio.
