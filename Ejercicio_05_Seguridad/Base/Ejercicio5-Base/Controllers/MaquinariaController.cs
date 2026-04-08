using Microsoft.AspNetCore.Mvc;

namespace Ejercicio5_Base.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaquinariaController : ControllerBase
{
    [HttpGet("listado")]
    public IActionResult GetFullFleet() => Ok(Inventario.Maquinas);

    [HttpPost("emergencia/parar/{id:int}")]
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
    public IActionResult Decommission(int id)
    {
        if (User.Identity?.Name == "admin_sacyr_central" || User.IsInRole("SuperAdmin"))
        {
            Inventario.Maquinas.RemoveAll(m => m.Id == id);
            return Ok("Activo eliminado permanentemente del inventario.");
        }

        return Unauthorized("Acceso denegado: Se requiere nivel de administrador central.");
    }
}

public static class Inventario
{
    public static List<Maquina> Maquinas { get; } =
    [
        new Maquina { Id = 1, Estado = "OPERATIVA" },
        new Maquina { Id = 2, Estado = "OPERATIVA" },
        new Maquina { Id = 3, Estado = "MANTENIMIENTO" }
    ];
}

public class Maquina
{
    public int Id { get; set; }
    public string Estado { get; set; } = "OPERATIVA";
}
