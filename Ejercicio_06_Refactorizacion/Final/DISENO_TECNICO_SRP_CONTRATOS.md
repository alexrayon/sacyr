# DISENO TECNICO SRP: Contratos, Inyeccion y DTO

## Alcance
Este diseno deriva exclusivamente de ESTRATEGIA_SRP y define fronteras tecnicas para desacoplar el cierre de obras.

## 1. Definicion de contratos

Firmas de referencia (diseno de contrato):

```csharp
public interface IProjectRepository
{
	ProjectData GetById(int projectId);
	void SaveClosure(int projectId, decimal finalBalance, DateTime closedAtUtc, string finalStatus);
}

public interface INotificationService
{
	void SendProjectClosed(string recipientEmail, string subject, string body);
}

public interface IReportGenerator
{
	void GenerateClosingReport(ClosingSummary summary);
}
```

### 1.1 IProjectRepository
Responsabilidad:
- Proveer y persistir estado de proyectos para el caso de uso de cierre.

Operaciones minimas:
- Obtener proyecto por identificador.
- Marcar proyecto como cerrado con balance final.

Semantica recomendada:
- Si el proyecto no existe, retorna resultado de dominio controlado o lanza excepcion de aplicacion tipada.
- Debe garantizar consistencia del cierre (estado + balance).

### 1.2 INotificationService
Responsabilidad:
- Notificar al responsable de la obra sobre el resultado del cierre.

Operaciones minimas:
- Enviar notificacion de cierre con destinatario, asunto y contenido.

Semantica recomendada:
- Exponer resultado de envio (exito/fallo) o excepcion de infraestructura traducible.
- No mezclar logica de plantillas con logica de negocio.

### 1.3 IReportGenerator
Responsabilidad:
- Generar el artefacto documental de cierre a partir de datos de resumen.

Operaciones minimas:
- Generar reporte desde ClosingSummary.

Semantica recomendada:
- No depender de entidades de base de datos.
- Permitir implementaciones intercambiables (archivo local, blob, API documental).

## 2. Esquema de inyeccion (Primary Constructor en C#)

### Objetivo
ProjectClosingService debe declarar sus dependencias como parametros del constructor primario, de forma explicita y obligatoria.

### Modelo de composicion
- Composition Root: registra adaptadores concretos para cada interfaz.
- ProjectClosingService: recibe IProjectRepository, INotificationService, IReportGenerator en el constructor.
- Metodo CloseProject: coordina pasos sin conocer detalles de SQL, SMTP o filesystem.

Definicion esperada del servicio (forma, no implementacion):

```csharp
public class ProjectClosingService(
	IProjectRepository projectRepository,
	INotificationService notificationService,
	IReportGenerator reportGenerator)
{
	public void CloseProject(int projectId)
	{
		// Orquesta el flujo de cierre sin detalles de infraestructura.
	}
}
```

### Propiedades arquitectonicas obtenidas
- Transparencia de dependencias (no ocultas).
- Sustitucion directa por dobles de prueba.
- Orquestacion limpia y verificable.

## 3. Diseno de modelo DTO

### ClosingSummary
Proposito:
- Transferir informacion del cierre desde el servicio de negocio al generador de reportes sin exponer modelo de persistencia.

Forma sugerida:

```csharp
public sealed record ClosingSummary(
	int ProjectId,
	string OwnerEmail,
	decimal Budget,
	decimal Expenses,
	decimal FinalBalance,
	DateTime ClosedAtUtc,
	string FinalStatus);
```

Campos recomendados:
- ProjectId: identificador de la obra.
- OwnerEmail: destinatario principal del cierre.
- Budget: presupuesto inicial.
- Expenses: gastos acumulados.
- FinalBalance: resultado de liquidacion.
- ClosedAtUtc: fecha/hora de cierre en UTC.
- FinalStatus: estado final de negocio (Closed, ClosedWithWarnings, etc.).

Reglas de diseno:
- Inmutable o de mutacion controlada.
- Sin comportamiento de acceso a datos.
- Sin referencias a entidades ORM o estructuras de infraestructura.

## 4. Flujo objetivo de CloseProject (orquestador)

1. Recuperar datos de proyecto mediante IProjectRepository.
2. Calcular balance y estado final (politica de dominio definida en ESTRATEGIA_SRP).
3. Persistir cierre mediante IProjectRepository.
4. Notificar resultado mediante INotificationService.
5. Construir ClosingSummary y delegar generacion documental a IReportGenerator.

Resultado:
- CloseProject mantiene responsabilidad unica de coordinacion.
- Cada detalle tecnico queda aislado en su adaptador especializado.
