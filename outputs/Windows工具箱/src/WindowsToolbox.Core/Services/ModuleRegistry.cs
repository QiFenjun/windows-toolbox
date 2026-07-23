using WindowsToolbox.Core.Interfaces;

namespace WindowsToolbox.Core.Services;

public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly List<IToolModule> _modules = [];

    public IReadOnlyList<IToolModule> Modules =>
        _modules.OrderBy(module => module.SortOrder).ToArray();

    public void Register(IToolModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_modules.Any(item => string.Equals(item.Id, module.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"模块 ID“{module.Id}”已经注册。");

        _modules.Add(module);
    }

    public IToolModule? Find(string id) =>
        _modules.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<IToolModule> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Modules;

        string value = query.Trim();
        return _modules
            .Where(module =>
                module.IsAvailable &&
                (module.DisplayName.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
                 module.Description.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
                 module.Category.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
                 module.Keywords.Any(keyword => keyword.Contains(value, StringComparison.CurrentCultureIgnoreCase))))
            .OrderBy(module => module.SortOrder)
            .ToArray();
    }
}
