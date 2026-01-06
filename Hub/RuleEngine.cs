using System.Globalization;
using Common;

public sealed class RuleEngine
{
    public string RulesPath { get; }
    private DateTime _lastLoadTime = DateTime.MinValue;
    private DateTime _lastWriteTime = DateTime.MinValue;

    private readonly List<string> _rules = new();

    // Motion rate kuralı için
    private readonly Queue<DateTime> _motionTimes = new();

    public RuleEngine(string rulesPath)
    {
        RulesPath = rulesPath;
        EnsureRulesFileExistsWithDefaults();
        LoadIfChanged(force: true);
    }

    public void NotifyMotion()
    {
        var now = DateTime.Now;
        _motionTimes.Enqueue(now);

        while (_motionTimes.Count > 0 && (now - _motionTimes.Peek()).TotalSeconds > 60)
            _motionTimes.Dequeue();
    }

    public IEnumerable<RuleAction> Evaluate(HubContext ctx)
    {
        LoadIfChanged(force: false);

        foreach (var raw in _rules)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;

            var idx = line.IndexOf("THEN", StringComparison.OrdinalIgnoreCase);
            if (!line.StartsWith("IF ", StringComparison.OrdinalIgnoreCase) || idx < 0) continue;

            var condPart = line.Substring(3, idx - 3).Trim();
            var actPart = line.Substring(idx + 4).Trim();

            if (!EvalCondition(condPart, ctx)) continue;

            foreach (var act in ParseActions(actPart, ctx))
                yield return act;
        }
    }

    private bool EvalCondition(string cond, HubContext ctx)
    {
        var atoms = cond.Split(new[] { "AND" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var atomRaw in atoms)
        {
            var atom = atomRaw.Trim();

            if (atom.StartsWith("TEMP>", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseDouble(atom.Substring(5), out var x)) return false;
                if (!(ctx.TempValid && ctx.Temp > x)) return false;
                continue;
            }

            if (atom.StartsWith("TEMP<", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseDouble(atom.Substring(5), out var x)) return false;
                if (!(ctx.TempValid && ctx.Temp < x)) return false;
                continue;
            }

            if (atom.StartsWith("MODE=", StringComparison.OrdinalIgnoreCase))
            {
                var m = atom.Substring(5).Trim().ToUpperInvariant();
                if (!string.Equals(ctx.Mode.ToUpperInvariant(), m, StringComparison.OrdinalIgnoreCase)) return false;
                continue;
            }

            if (atom.StartsWith("MOTION=", StringComparison.OrdinalIgnoreCase))
            {
                var v = atom.Substring(7).Trim().ToUpperInvariant();
                var expected = (v == "ON");
                if (ctx.Motion != expected) return false;
                continue;
            }

            if (atom.StartsWith("ROOM=", StringComparison.OrdinalIgnoreCase))
            {
                var r = atom.Substring(5).Trim().ToUpperInvariant();
                if (!string.Equals(ctx.Room.ToUpperInvariant(), r, StringComparison.OrdinalIgnoreCase)) return false;
                continue;
            }

            if (atom.StartsWith("MOTION_COUNT_60S>=", StringComparison.OrdinalIgnoreCase))
            {
                var nStr = atom.Substring("MOTION_COUNT_60S>=".Length).Trim();
                if (!int.TryParse(nStr, out var n)) return false;
                if (_motionTimes.Count < n) return false;
                continue;
            }

            return false;
        }

        return true;
    }

    private IEnumerable<RuleAction> ParseActions(string actPart, HubContext ctx)
    {
        var parts = actPart.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            var a = p.Trim();

            if (a.StartsWith("LAMP=", StringComparison.OrdinalIgnoreCase))
            {
                yield return RuleAction.SetLamp(a.Substring(5).Trim().ToUpperInvariant());
                continue;
            }

            if (a.StartsWith("LOCK=", StringComparison.OrdinalIgnoreCase))
            {
                yield return RuleAction.SetLock(a.Substring(5).Trim().ToUpperInvariant());
                continue;
            }

            if (a.StartsWith("ALERT=", StringComparison.OrdinalIgnoreCase))
            {
                var v = a.Substring(6).Trim();
                var room = v.Equals("ROOM", StringComparison.OrdinalIgnoreCase) ? ctx.Room : v.ToUpperInvariant();
                yield return RuleAction.Alert(room);
                continue;
            }
        }
    }

    private void EnsureRulesFileExistsWithDefaults()
    {
        if (File.Exists(RulesPath)) return;

        var defaults =
@"# OmniConnect RuleEngine rules
# Format: IF <conditions> THEN <actions>
# Conditions: TEMP>28, TEMP<26, MODE=AWAY, MOTION=ON, ROOM=KITCHEN, MOTION_COUNT_60S>=3
# Actions: LAMP=ON/OFF ; LOCK=LOCK/UNLOCK ; ALERT=ROOM

IF TEMP>28 THEN LAMP=ON
IF TEMP<26 THEN LAMP=OFF
IF MOTION=ON AND MODE=AWAY THEN LOCK=LOCK; ALERT=ROOM
IF MOTION_COUNT_60S>=3 AND MODE=AWAY THEN LOCK=LOCK; ALERT=ROOM
";
        File.WriteAllText(RulesPath, defaults);
    }

    private void LoadIfChanged(bool force)
    {
        var now = DateTime.Now;
        if (!force && (now - _lastLoadTime).TotalMilliseconds < 500) return;

        _lastLoadTime = now;

        try
        {
            var fi = new FileInfo(RulesPath);
            var w = fi.Exists ? fi.LastWriteTime : DateTime.MinValue;

            if (!force && w == _lastWriteTime) return;

            _lastWriteTime = w;
            _rules.Clear();
            _rules.AddRange(File.ReadAllLines(RulesPath));
        }
        catch
        {
            _rules.Clear();
        }
    }

    private static bool TryParseDouble(string s, out double v)
        => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}

public readonly record struct HubContext(
    string Mode,
    bool Motion,
    string Room,
    bool TempValid,
    double Temp
);

public readonly record struct RuleAction(string Kind, string Value)
{
    public static RuleAction SetLamp(string v) => new("LAMP", v);
    public static RuleAction SetLock(string v) => new("LOCK", v);
    public static RuleAction Alert(string room) => new("ALERT", room);
}
