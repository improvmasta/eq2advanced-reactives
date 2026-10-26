using System.Text.Json;

namespace EQ2Advanced.Plugins;

// The small part of the public plugin contract this plugin needs to compile on its own.
// At run time, EQ2Advanced loads its own EQ2Advanced.Plugins.dll (version 1.0.0.0).
public interface IEq2Plugin : IDisposable
{
    void Start(IPluginHost host);
    void Command(string command, JsonElement value);
    object? State();
}

public interface IEq2PluginEvents
{
    void OnEvents(IReadOnlyList<PluginEvent> events);
}

public sealed class PluginEvent
{
    public string Kind { get; init; } = "";
    public long Timestamp { get; init; }
    public string Character { get; init; } = "";
    public string Text { get; init; } = "";
    public bool Replay { get; init; }
}

public interface IPluginHost
{
    string DataDirectory { get; }
    void StateChanged();
    void Log(string text);
    void OpenUrl(string url);
}
