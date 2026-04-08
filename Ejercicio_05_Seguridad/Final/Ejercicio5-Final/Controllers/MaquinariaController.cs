using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ejercicio5_Final.Services;

namespace Sacyr.Industrial.FleetSecurity;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MaquinariaController : ControllerBase
{
    private readonly IMachineService _service;

    public MaquinariaController(IMachineService service)
    {
        _service = service;
    }

    [HttpGet("listado")]
    [Authorize(Policy = "FleetViewer")]
    public IActionResult GetFullFleet()
    {
        return Ok(_service.GetMachines());
    }

    [HttpPost("emergencia/parar/{id:int}")]
    [Authorize(Policy = "CriticalAssetAdmin")]
    public IActionResult StopMachine(int id)
    {
        if (!_service.ExecuteEmergencyStop(id))
        {
            return NotFound($"No se encontro el activo {id}.");
        }

        return Ok($"Comando de parada procesado para activo {id}");
    }

    [HttpDelete("baja/{id:int}")]
    [Authorize(Policy = "CentralAdminOnly")]
    public IActionResult Decommission(int id)
    {
        if (!_service.RemoveAsset(id))
        {
            return NotFound($"No se encontro el activo {id}.");
        }

        return Ok("Activo dado de baja del sistema central.");
    }
}
