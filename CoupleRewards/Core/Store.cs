using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoupleRewards.Core;

/// <summary>
/// One permanent ledger entry. (D, N) = device id + that device's running number, which makes every entry unique.
/// P = person (0 Nick, 1 Whitney). K = "t" task step, "r" reward redeemed, "b" bonus, "c" points cleared. I = task/reward key.
/// S = chain step index. V = points (+earned, -spent).
/// </summary>
public record Ev(string D, int N, int P, string K, string I, int S, string Day, int V, long T);

public class Db
{
    public string Dev { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Who { get; set; } = "Nick";
    public string Peer { get; set; } = "";
    /// <summary>Last day (yyyy-MM-dd) that default-done tasks were awarded on this phone.</summary>
    public string AutoThrough { get; set; } = "";
    public List<Ev> Events { get; set; } = new();
}

/// <summary>Sync message. Vv = "what I have" (device id -> highest entry number). Events = entries the other side lacks.</summary>
public record Msg(Dictionary<string, int>? Vv, List<Ev>? Events);

[JsonSerializable(typeof(Db))]
[JsonSerializable(typeof(Msg))]
internal partial class RewardsJson : JsonSerializerContext { }

/// <summary>
/// All data lives in a single JSON file on the phone. Points are never stored as a number: they are the sum of the
/// ledger. That is what makes two phones merge cleanly (each just adds the entries it is missing) and it also gives
/// the daily reset for free (a task is "done today" if there is an entry for it dated today).
/// </summary>
public class Store
{
    readonly object gate = new();
    readonly string path;
    readonly Db db;
    readonly Dictionary<string, int> vv = new();

    /// <summary>Raised after any change, on whatever thread caused it. UI code must hop to the main thread.</summary>
    public event Action? Changed;

    public Store(string dir)
    {
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, "rewards.json");
        db = Load();
        foreach (var e in db.Events) vv[e.D] = Math.Max(vv.GetValueOrDefault(e.D), e.N);
    }

    Db Load()
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize(File.ReadAllText(path), RewardsJson.Default.Db) ?? new Db();
        }
        catch { }
        return new Db();
    }

    void Save() // caller holds the lock
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(db, RewardsJson.Default.Db));
            File.Move(tmp, path, true);
        }
        catch { }
    }

    public static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---- settings kept on this phone only (not synced) ----
    public string Who
    {
        get { lock (gate) return db.Who; }
        set { lock (gate) { db.Who = value; Save(); } Changed?.Invoke(); }
    }
    public int Person => Who == Defs.Names[1] ? 1 : 0;
    public string Peer
    {
        get { lock (gate) return db.Peer; }
        set { lock (gate) { db.Peer = (value ?? "").Trim(); Save(); } Changed?.Invoke(); }
    }

    // ---- reading ----
    // The same task step finished on both phones at once is counted once.
    IEnumerable<Ev> Live(int p) => db.Events.Where(e => e.P == p)
        .GroupBy(e => e.K == "t" ? $"t|{e.Day}|{e.I}|{e.S}" : $"e|{e.D}|{e.N}")
        .Select(g => g.First());

    public int Points(int p) { lock (gate) return Live(p).Sum(e => e.V); }

    /// <summary>How many steps of this task have been finished today.</summary>
    public int Done(int p, string task)
    {
        lock (gate) { var d = Today; return Live(p).Count(e => e.K == "t" && e.I == task && e.Day == d); }
    }

    // ---- writing ----
    void Add(int p, string kind, string item, int step, int value, string? day = null)
    {
        var n = vv.GetValueOrDefault(db.Dev) + 1;
        vv[db.Dev] = n;
        db.Events.Add(new Ev(db.Dev, n, p, kind, item, step, day ?? Today, value, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

    /// <summary>
    /// Finishes every step of every default-done task (TaskDef.AutoDone) for both people, for today and any days
    /// since this last ran (up to 60 days back), so points aren't lost on days the app wasn't opened.
    /// Safe to call often: steps already recorded are skipped, and if both phones award the same step it counts once.
    /// </summary>
    public void AwardAutoDone()
    {
        int added = 0;
        lock (gate)
        {
            var today = DateTime.Today;
            var from = today;
            if (DateTime.TryParseExact(db.AutoThrough, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last)
                && last < today)
                from = last.AddDays(1);
            if (from < today.AddDays(-60)) from = today.AddDays(-60);

            for (var d = from; d <= today; d = d.AddDays(1))
            {
                var day = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                for (int p = 0; p < Defs.Tasks.Length; p++)
                {
                    foreach (var t in Defs.Tasks[p].Where(t => t.AutoDone))
                    {
                        var have = Live(p).Where(e => e.K == "t" && e.I == t.Key && e.Day == day).Select(e => e.S).ToHashSet();
                        for (int s = 0; s < t.Steps.Length; s++)
                        {
                            if (have.Contains(s)) continue;
                            Add(p, "t", t.Key, s, t.Steps[s], day);
                            added++;
                        }
                    }
                }
            }

            var todayText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (added == 0 && db.AutoThrough == todayText) return;
            db.AutoThrough = todayText;
            Save();
        }
        if (added > 0) Changed?.Invoke();
    }

    /// <returns>Points earned, or 0 if the task is already finished for today.</returns>
    public int Complete(int p, TaskDef t)
    {
        int gain;
        lock (gate)
        {
            var k = Done(p, t.Key);
            if (k >= t.Steps.Length) return 0;
            gain = t.Steps[k];
            Add(p, "t", t.Key, k, gain);
            Save();
        }
        Changed?.Invoke();
        return gain;
    }

    public bool Redeem(int p, RewardDef r)
    {
        lock (gate)
        {
            if (Points(p) < r.Cost) return false;
            Add(p, "r", r.Key, 0, -r.Cost);
            Save();
        }
        Changed?.Invoke();
        return true;
    }

    public void Bonus(int p, int amount)
    {
        lock (gate) { Add(p, "b", "bonus", 0, amount); Save(); }
        Changed?.Invoke();
    }

    /// <summary>
    /// Brings a person's balance to zero. Like everything else this is a ledger entry (K = "c", V = minus the current
    /// balance), so it syncs to the partner's phone instead of being undone by the next sync. Today's finished tasks
    /// stay finished.
    /// </summary>
    /// <returns>How many points were cleared (0 if there was nothing to clear).</returns>
    public int Clear(int p)
    {
        int cleared;
        lock (gate)
        {
            cleared = Points(p);
            if (cleared == 0) return 0;
            Add(p, "c", "clear", 0, -cleared);
            Save();
        }
        Changed?.Invoke();
        return cleared;
    }

    // ---- sync support ----
    public Dictionary<string, int> Vector() { lock (gate) return new Dictionary<string, int>(vv); }

    public List<Ev> Since(Dictionary<string, int> theirs)
    {
        lock (gate) return db.Events.Where(e => e.N > theirs.GetValueOrDefault(e.D)).ToList();
    }

    /// <returns>How many new entries were added.</returns>
    public int Merge(IEnumerable<Ev> incoming)
    {
        int added = 0;
        var redeems = new List<Ev>();
        lock (gate)
        {
            var recent = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeMilliseconds();
            foreach (var e in incoming.OrderBy(e => e.D, StringComparer.Ordinal).ThenBy(e => e.N))
            {
                if (e.N != vv.GetValueOrDefault(e.D) + 1) continue; // only accept the next entry in order
                db.Events.Add(e);
                vv[e.D] = e.N;
                added++;
                // A reward redeemed on the other phone. Old history (e.g. the first sync after a reinstall) is skipped.
                if (e.K == "r" && e.D != db.Dev && e.T >= recent) redeems.Add(e);
            }
            if (added > 0) Save();
        }
        if (added > 0) Changed?.Invoke();
        if (redeems.Count > 0) PartnerRedeemed?.Invoke(redeems);
        return added;
    }

    /// <summary>Raised (on the sync thread) with rewards that were just redeemed on the partner's phone.</summary>
    public event Action<IReadOnlyList<Ev>>? PartnerRedeemed;
}
