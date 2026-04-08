namespace Ejercicio5_Final.Services;

public interface IMachineService
{
    IReadOnlyList<MachineAsset> GetMachines();
    bool ExecuteEmergencyStop(int id);
    bool RemoveAsset(int id);
}

public sealed class MachineService : IMachineService
{
    private readonly List<MachineAsset> _machines =
    [
        new MachineAsset { Id = 1, Estado = "OPERATIVA" },
        new MachineAsset { Id = 2, Estado = "OPERATIVA" },
        new MachineAsset { Id = 3, Estado = "MANTENIMIENTO" }
    ];

    public IReadOnlyList<MachineAsset> GetMachines()
    {
        return _machines.AsReadOnly();
    }

    public bool ExecuteEmergencyStop(int id)
    {
        var machine = _machines.FirstOrDefault(m => m.Id == id);
        if (machine is null)
        {
            return false;
        }

        machine.Estado = "PARADA_EMERGENCIA";
        return true;
    }

    public bool RemoveAsset(int id)
    {
        return _machines.RemoveAll(m => m.Id == id) > 0;
    }
}

public sealed class MachineAsset
{
    public int Id { get; set; }
    public string Estado { get; set; } = "OPERATIVA";
}
