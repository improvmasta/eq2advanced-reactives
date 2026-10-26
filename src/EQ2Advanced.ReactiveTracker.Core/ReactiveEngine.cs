using System.Text.RegularExpressions;

namespace EQ2Advanced.ReactiveTracker.Core;

/// <summary>Live evidence for one local healer. Names and counts come from explicit log lines;
/// a proc without a known cast never invents a remaining count.</summary>
public sealed class ReactiveEngine
{
    private const int DurationSeconds = 30;
    private readonly Dictionary<string, Recipient> _recipients = new(StringComparer.OrdinalIgnoreCase);
    private string _groupSpell = "";
    private long _groupCastAt;

    private static readonly Regex InquisitorSingle = new(@"^You demand retribution for any wrongs done to (?<target>.+?)\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex TemplarSingle = new(@"^You pray for (?<target>.+?)(?:'s|') body and soul\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex GroupRecipient = new(@"^(?:(?<target>.+?)'(?:s)?|Your) group is protected by (?:a |an )?(?<spell>Malevolent Diatribe|Holy Intercession)!?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex GroupLost = new(@"^(?:(?<target>.+?)'(?:s)?|Your) group is no longer protected by (?:a |an )?(?<spell>Malevolent Diatribe|Holy Intercession)\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex Proc = new(@"^YOUR (?<spell>Vengeful Faith|Supplicant's Prayer|Atoning Faith|Divine Prayer) heals (?<target>.+?) for (?:a critical of )?[\d,.KMBTQ]+ hit points?\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex OtherSingleExpired = new(@"^(?<target>.+?) appears less sure of (?:himself|herself|themself)\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex GroupJoin = new(@"^(?<target>.+?) has joined the group\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex GroupLeave = new(@"^(?<target>.+?) left the group\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex Death = new(@"^(?:Alas, )?(?<target>.+?) (?:has died(?: from .+)?|was slain by .+)\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    public ReactiveEngine(string character) => Character = character;

    public string Character { get; }
    public string ClassName { get; private set; } = "";
    public long LastAt { get; private set; }
    public string LastObservation { get; private set; } = "";
    public HealerOptions Options { get; set; } = new();

    public bool Accept(string text, long at)
    {
        if (string.IsNullOrWhiteSpace(text) || at <= 0) return false;
        text = text.Trim();
        if (text.Length > 512 || !(text.Contains("retribution for", StringComparison.OrdinalIgnoreCase)
            || text.Contains("body and soul", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Malevolent Diatribe", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Holy Intercession", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Vengeful Faith", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Supplicant's Prayer", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Atoning Faith", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Divine Prayer", StringComparison.OrdinalIgnoreCase)
            || text.Contains("less sure of", StringComparison.OrdinalIgnoreCase)
            || text.Contains("strong prayerful protection", StringComparison.OrdinalIgnoreCase)
            || text.Contains("joined the group", StringComparison.OrdinalIgnoreCase)
            || text.Contains("left the group", StringComparison.OrdinalIgnoreCase)
            || text.Contains("has died", StringComparison.OrdinalIgnoreCase)
            || text.Contains("was slain by", StringComparison.OrdinalIgnoreCase))) return false;
        Match match;
        if (text.Equals("You have left the group.", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var target in _recipients.Keys.Where(name => !name.Equals(Character, StringComparison.OrdinalIgnoreCase)).ToList())
                _recipients.Remove(target);
            _groupSpell = "";
            _groupCastAt = 0;
            LastAt = at;
            LastObservation = "You left the group; former group members were removed.";
            return true;
        }
        if ((match = GroupLeave.Match(text)).Success)
        {
            var target = Target(match.Groups["target"].Value);
            _recipients.Remove(target);
            LastAt = at;
            LastObservation = $"{target} left the group.";
            return true;
        }
        if ((match = GroupJoin.Match(text)).Success)
        {
            var target = Target(match.Groups["target"].Value);
            RecipientFor(target).Listed = true;
            LastAt = at;
            LastObservation = $"{target} joined the group.";
            return true;
        }
        if ((match = Death.Match(text)).Success)
        {
            var target = Target(match.Groups["target"].Value);
            if (!_recipients.TryGetValue(target, out var fallen)) return false;
            fallen.Single = null;
            fallen.Group = null;
            LastAt = at;
            LastObservation = $"{target} died; reactives cleared.";
            return true;
        }
        if ((match = InquisitorSingle.Match(text)).Success)
        {
            ClassName = "Inquisitor";
            StartSingle(Target(match.Groups["target"].Value), at);
            return true;
        }
        if ((match = TemplarSingle.Match(text)).Success)
        {
            ClassName = "Templar";
            StartSingle(Target(match.Groups["target"].Value), at);
            return true;
        }
        if (text.Equals("You raise your voice in a Malevolent Diatribe.", StringComparison.OrdinalIgnoreCase)
            || text.Equals("You pray devoutly for Holy Intercession.", StringComparison.OrdinalIgnoreCase))
        {
            ClassName = text.Contains("Malevolent", StringComparison.OrdinalIgnoreCase) ? "Inquisitor" : "Templar";
            _groupSpell = ClassName == "Inquisitor" ? "Malevolent Diatribe" : "Holy Intercession";
            _groupCastAt = at;
            // The protection lines that follow are the fresh roster. Old group rows
            // must not linger just because a leave message was missed.
            foreach (var recipient in _recipients.Values) { recipient.Listed = false; recipient.Group = null; }
            LastObservation = "Group cast seen; adding recipients as their protection appears in the log.";
            LastAt = at;
            return true;
        }
        // EQ2 writes a protection line for each recipient. Accept only those immediately
        // following this character's own cast; an observer's log cannot identify the caster.
        if ((match = GroupRecipient.Match(text)).Success && _groupCastAt > 0 && at >= _groupCastAt
            && at <= _groupCastAt + 3 && match.Groups["spell"].Value.Equals(_groupSpell, StringComparison.OrdinalIgnoreCase))
        {
            var target = match.Groups["target"].Success ? Target(match.Groups["target"].Value) : Character;
            StartGroup(target, _groupCastAt);
            LastObservation = $"Group reactive active on {target}.";
            return true;
        }
        if ((match = GroupLost.Match(text)).Success)
        {
            var target = match.Groups["target"].Success ? Target(match.Groups["target"].Value) : Character;
            if (!_recipients.TryGetValue(target, out var recipient) || recipient.Group is null) return false;
            recipient.Group = null;
            LastAt = at;
            LastObservation = $"Group reactive dropped from {target}.";
            return true;
        }
        if ((match = Proc.Match(text)).Success)
        {
            var spell = match.Groups["spell"].Value;
            var target = Target(match.Groups["target"].Value);
            var single = spell.Equals("Vengeful Faith", StringComparison.OrdinalIgnoreCase)
                || spell.Equals("Supplicant's Prayer", StringComparison.OrdinalIgnoreCase);
            ClassName = spell.Equals("Vengeful Faith", StringComparison.OrdinalIgnoreCase)
                || spell.Equals("Atoning Faith", StringComparison.OrdinalIgnoreCase) ? "Inquisitor" : "Templar";
            if (_recipients.TryGetValue(target, out var recipient))
            {
                var effect = single ? recipient.Single : recipient.Group;
                if (effect is { Remaining: > 0 } && effect.ExpiresAt > at)
                {
                    effect.Remaining--;
                    LastAt = at;
                    LastObservation = $"{spell} triggered on {target}.";
                    return true;
                }
            }
            LastAt = at;
            LastObservation = $"{spell} healed {target}; its cast was not seen, so remaining triggers are unknown.";
            return true;
        }
        if (text.Equals("You feel the strong prayerful protection dissipate.", StringComparison.OrdinalIgnoreCase))
            return ExpireSingle(Character, at);
        if ((match = OtherSingleExpired.Match(text)).Success)
            return ExpireSingle(Target(match.Groups["target"].Value), at);
        return false;
    }

    public void Clear(string reason = "Tracking cleared.")
    {
        _recipients.Clear();
        _groupSpell = "";
        _groupCastAt = 0;
        LastObservation = reason;
    }

    public ReactiveSnapshot Snapshot(long now)
    {
        foreach (var name in _recipients.Where(pair => !pair.Value.Listed
            && Active(pair.Value.Single, now) is null && Active(pair.Value.Group, now) is null)
            .Select(pair => pair.Key).ToList()) _recipients.Remove(name);
        var rows = _recipients.Values.Select(recipient => new RecipientSnapshot(recipient.Name,
            Active(recipient.Single, now), Active(recipient.Group, now), recipient.Listed))
            .Where(row => row.Listed || row.Single is not null || row.Group is not null)
            .OrderBy(row => row.Name.Equals(Character, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return new ReactiveSnapshot(Character, ClassName, LastAt, LastObservation, Options, rows);
    }

    private static EffectSnapshot? Active(Effect? effect, long now) => effect is { Remaining: > 0 } && effect.ExpiresAt > now
        ? new EffectSnapshot(effect.Remaining, effect.ExpiresAt, (int)(effect.ExpiresAt - now)) : null;

    private string Target(string name)
    {
        name = name.Trim().TrimEnd('.', '!');
        return name.Equals("YOU", StringComparison.OrdinalIgnoreCase) || name.Equals("yourself", StringComparison.OrdinalIgnoreCase)
            ? Character : name;
    }

    private Recipient RecipientFor(string target)
    {
        if (!_recipients.TryGetValue(target, out var recipient))
            _recipients[target] = recipient = new Recipient(target);
        return recipient;
    }

    private void StartSingle(string target, long at)
    {
        RecipientFor(target).Single = new Effect(5 + (Options.CoerciveHealing ? 1 : 0)
            + (ClassName == "Templar" && Options.TemplarEofTwoSet ? 4 : 0), at + DurationSeconds);
        LastAt = at;
        LastObservation = $"Single reactive cast on {target}.";
    }

    private void StartGroup(string target, long at)
    {
        var recipient = RecipientFor(target);
        recipient.Listed = true;
        recipient.Group = new Effect(3 + (Options.CoerciveHealing ? 1 : 0), at + DurationSeconds);
        LastAt = at;
    }

    private bool ExpireSingle(string target, long at)
    {
        if (!_recipients.TryGetValue(target, out var recipient) || recipient.Single is null) return false;
        recipient.Single = null;
        LastAt = at;
        LastObservation = $"Single reactive faded on {target}.";
        return true;
    }

    private sealed class Recipient(string name)
    {
        public string Name { get; } = name;
        public bool Listed { get; set; }
        public Effect? Single { get; set; }
        public Effect? Group { get; set; }
    }

    private sealed class Effect(int remaining, long expiresAt)
    {
        public int Remaining { get; set; } = remaining;
        public long ExpiresAt { get; } = expiresAt;
    }
}

public sealed class HealerOptions
{
    public bool CoerciveHealing { get; set; }
    public bool TemplarEofTwoSet { get; set; }
    public int WarningCount { get; set; } = 1;
    public int WarningSeconds { get; set; } = 5;
}

public sealed record EffectSnapshot(int Remaining, long ExpiresAt, int Seconds);
public sealed record RecipientSnapshot(string Name, EffectSnapshot? Single, EffectSnapshot? Group, bool Listed);
public sealed record ReactiveSnapshot(string Character, string ClassName, long LastAt, string LastObservation,
    HealerOptions Options, IReadOnlyList<RecipientSnapshot> Recipients);
