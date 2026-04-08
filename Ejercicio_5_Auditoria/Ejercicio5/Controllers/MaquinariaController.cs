using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ejercicio5_Base.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaquinariaController : ControllerBase
{
    [HttpGet("listado")]
    [Authorize(Policy = "FleetViewer")]
    public IActionResult GetFullFleet() => Ok(Inventario.Maquinas);

    [HttpPost("emergencia/parar/{id:int}")]
    [Authorize(Policy = "CriticalAssetAdmin")]
    public IActionResult StopMachine(int id)
    {
        var machine = Inventario.Maquinas.FirstOrDefault(m => m.Id == id);
        if (machine is null)
        {
            return NotFound($"No se encontro la unidad {id}.");
        }

        machine.Estado = "PARADA_EMERGENCIA";
        return Ok($"Comando de parada enviado a la unidad {id}");
    }

    [HttpDelete("decommission/{id:int}")]
    [Authorize(Policy = "CriticalAssetAdmin")]
    public IActionResult Decommission(int id)
    {
        var machine = Inventario.Maquinas.FirstOrDefault(m => m.Id == id);
        if (machine is null)
        {
            return NotFound($"No se encontro la unidad {id}.");
        }

        Inventario.Maquinas.RemoveAll(m => m.Id == id);
        return Ok("Activo eliminado permanentemente del inventario.");
    }
}

public static class Inventario
{
    public static List<Maquina> Maquinas { get; } =
        new()
        {
            new Maquina { Id = 1, Estado = "OPERATIVA" },
            new Maquina { Id = 2, Estado = "OPERATIVA" },
            new Maquina { Id = 3, Estado = "MANTENIMIENTO" }
        };
}

public class Maquina
{
    public int Id { get; set; }
    public string Estado { get; set; } = "OPERATIVA";
}
