namespace WindowsToolbox.Core.Interfaces;

/// <summary>所有工具模块必须实现的统一契约。</summary>
public interface IToolModule
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }
    string IconKey { get; }
    int SortOrder { get; }
    bool IsAvailable { get; }
    IReadOnlyList<string> Keywords { get; }
    string? ResourceDictionaryPath { get; }
    object CreateViewModel();
}
