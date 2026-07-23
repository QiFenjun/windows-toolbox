namespace WindowsToolbox.Core.Interfaces;

public interface IModuleRegistry
{
    IReadOnlyList<IToolModule> Modules { get; }
    void Register(IToolModule module);
    IToolModule? Find(string id);
    IReadOnlyList<IToolModule> Search(string query);
}
