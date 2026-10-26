using System.Text.Json;
using EQ2Advanced.Plugins;
using EQ2Advanced.ReactiveTracker.Core;

namespace EQ2Advanced.ReactiveTracker;

/// <summary>Independent live-log plugin. The original ACT ReactiveTracker by Avacii (Avacee on GitHub)
/// inspired the feature; this implementation uses the EQ2Advanced plugin contract.</summary>
public sealed class ReactivePlugin : IEq2Plugin, IEq2PluginEvents
{
    private const string SourceUrl = "https://github.com/Avacee/ACT/tree/master/ReactiveTracker";
    private readonly object _gate = new();
    private readonly Dictionary<string, ReactiveEngine> _healers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HealerOptions> _options = new(StringComparer.OrdinalIgnoreCase);
    private IPluginHost? _host;
    private string _selected = "";
    private string _settingsPath = "";

    public void Start(IPluginHost host)
    {
        _host = host;
        _settingsPath = Path.Combine(host.DataDirectory, "settings.json");
        try
        {
            if (File.Exists(_settingsPath))
            {
                var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(_settingsPath));
                if (saved is not null)
                {
                    _selected = saved.Selected ?? "";
                    foreach (var (name, options) in saved.Healers)
                        if (!string.IsNullOrWhiteSpace(name) && options is not null) _options[name] = options;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            host.Log("Could not read reactive settings: " + ex.Message);
        }
    }

    public void OnEvents(IReadOnlyList<PluginEvent> events)
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var item in events)
            {
                if (item.Kind == "zone" && _healers.TryGetValue(item.Character, out var changingZone))
                {
                    changingZone.Clear("Zone changed; tracking resumes on the next cast.");
                    changed = true;
                    continue;
                }
                if (item.Kind != "line" || item.Replay || string.IsNullOrWhiteSpace(item.Character)) continue;
                if (!_healers.TryGetValue(item.Character, out var healer))
                {
                    healer = new ReactiveEngine(item.Character);
                    if (_options.TryGetValue(item.Character, out var options)) healer.Options = options;
                    // A line can belong to a non-healer. Keep a session only after a
                    // reactive cast or proc has actually matched.
                    if (!healer.Accept(item.Text, item.Timestamp) || healer.ClassName.Length == 0) continue;
                    _healers[item.Character] = healer;
                    if (_selected.Length == 0 || !_healers.ContainsKey(_selected)) _selected = item.Character;
                    changed = true;
                }
                else if (healer.Accept(item.Text, item.Timestamp)) changed = true;
            }
        }
        if (changed) _host?.StateChanged();
    }

    public void Command(string command, JsonElement value)
    {
        if (command == "source") { _host?.OpenUrl(SourceUrl); return; }
        var save = false;
        var changed = false;
        lock (_gate)
        {
            if (command == "select")
            {
                var name = Text(value, "value") ?? Text(value, "character");
                if (name is not null && _healers.ContainsKey(name))
                {
                    _selected = name;
                    save = changed = true;
                }
            }
            else if (command == "clear" && _healers.TryGetValue(_selected, out var toClear))
            {
                toClear.Clear();
                changed = true;
            }
            else if (command == "setting" && _healers.TryGetValue(_selected, out var healer))
            {
                var key = Text(value, "key");
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var input))
                {
                    var options = healer.Options;
                    switch (key)
                    {
                        case "coercive": if (input.ValueKind is JsonValueKind.True or JsonValueKind.False) options.CoerciveHealing = input.GetBoolean(); else return; break;
                        case "eofTwoSet": if (input.ValueKind is JsonValueKind.True or JsonValueKind.False) options.TemplarEofTwoSet = input.GetBoolean(); else return; break;
                        case "warningCount": if (input.ValueKind != JsonValueKind.Number || !input.TryGetInt32(out var count)) return; options.WarningCount = Math.Clamp(count, 0, 10); break;
                        case "warningSeconds": if (input.ValueKind != JsonValueKind.Number || !input.TryGetInt32(out var seconds)) return; options.WarningSeconds = Math.Clamp(seconds, 0, 30); break;
                        default: return;
                    }
                    _options[healer.Character] = options;
                    save = changed = true;
                }
            }
        }
        if (save) Save();
        if (changed) _host?.StateChanged();
    }

    public object State()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var healers = _healers.Values.OrderBy(item => item.Character, StringComparer.OrdinalIgnoreCase).ToList();
            var selected = _healers.TryGetValue(_selected, out var current) ? current : healers.FirstOrDefault();
            var snapshot = selected?.Snapshot(now);
            return new
            {
                selected = snapshot?.Character ?? "",
                characters = healers.Select(item => item.Character).ToArray(),
                current = snapshot is null ? null : new
                {
                    character = snapshot.Character,
                    className = snapshot.ClassName,
                    lastAt = snapshot.LastAt,
                    observation = snapshot.LastObservation,
                    options = new
                    {
                        coercive = snapshot.Options.CoerciveHealing,
                        eofTwoSet = snapshot.Options.TemplarEofTwoSet,
                        warningCount = snapshot.Options.WarningCount,
                        warningSeconds = snapshot.Options.WarningSeconds,
                    },
                    rows = snapshot.Recipients.Select(row => new
                    {
                        name = row.Name,
                        single = row.Single is null ? null : new { count = row.Single.Remaining, seconds = row.Single.Seconds },
                        group = row.Group is null ? null : new { count = row.Group.Remaining, seconds = row.Group.Seconds },
                    }).ToArray(),
                },
            };
        }
    }

    public void Dispose() { }

    private void Save()
    {
        try
        {
            SavedSettings saved;
            lock (_gate) saved = new SavedSettings { Selected = _selected, Healers = new(_options, StringComparer.OrdinalIgnoreCase) };
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var temp = _settingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(saved));
            File.Move(temp, _settingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _host?.Log("Could not save reactive settings: " + ex.Message);
        }
    }

    private static string? Text(JsonElement value, string property) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
        ? element.GetString() : null;

    public sealed class SavedSettings
    {
        public string? Selected { get; set; }
        public Dictionary<string, HealerOptions> Healers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
