namespace CoupleRewards.Core;

/// <param name="AutoDone">If true, every step is finished and its points awarded automatically each day.</param>
public record TaskDef(string Key, string Icon, string Name, int[] Steps, bool AutoDone = false)
{
    /// <summary>
    /// The description to show when <paramref name="stepsDone"/> steps are finished today (the step up next; the last
    /// step once the chain is complete). The name can change per step in two ways:
    /// - "{n}" is replaced by the step number:   "Drink x{n} water bottles"  ->  "Drink x1 ...", "Drink x2 ...", ...
    /// - "|" separates one description per step: "Walk 15 min|Walk 30 min|Walk 1 hour"
    ///   (if there are fewer descriptions than steps, the last one is reused).
    /// </summary>
    public string NameFor(int stepsDone)
    {
        var i = Math.Clamp(stepsDone, 0, Steps.Length - 1);
        var parts = Name.Split('|');
        var text = parts[Math.Min(i, parts.Length - 1)].Trim();
        return text.Replace("{n}", (i + 1).ToString());
    }
}
public record RewardDef(string Key, string Icon, string Name, int Cost);

/// <summary>
/// Placeholder tasks and rewards for each person. Edit these freely.
/// - Key must be unique within a person (it is how history is matched, so don't reuse a key for something else).
/// - A task with several point values is a chain: x1, x2, x3 ... each step can be done once per day.
/// - A chain's name can change with each step: use "{n}" for the step number, or "|" between per-step names
///   (see TaskDef.NameFor).
/// - Write A(...) instead of T(...) for a task that is done by default: every day it starts already finished,
///   with all of its steps' points added automatically (including days the app wasn't opened).
/// - Make the same edits on both phones (install the same build on both) so the lists match.
/// </summary>
public static class Defs
{
    public static readonly string[] Names = { "Nick", "Whitney" };

    static TaskDef T(string key, string icon, string name, params int[] steps) => new(key, icon, name, steps);
    static TaskDef A(string key, string icon, string name, params int[] steps) => new(key, icon, name, steps, AutoDone: true);
    static RewardDef R(string key, string icon, string name, int cost) => new(key, icon, name, cost);

    public static readonly TaskDef[][] Tasks =
    {
        new[]   // Nick
        {
            T("water",    "💧", "Drink x{n} water bottles", 20, 50, 100),           
            T("workout",  "🏋️", "Workout",          100),
            T("steps",    "🚶", "Walk 10k steps",   60, 120),
            T("bed",     "🛏️", "Make the bed",     30),
            T("dishes",   "🍽️", "Do the dishes",    40),
            T("read",     "📖", "Read 20 minutes",  30, 60),
            T("tidy",     "🧹", "Tidy a room",      50),
            T("dinner",   "🍳", "Cook dinner",      80),
            T("vitamins", "💊", "Take vitamins",    10),
            T("laundry",  "🧺", "Do laundry",       60),
        },
        new[]   // Whitney
        {
            T("water",    "💧", "Drink x{n} water bottles", 20, 50, 100),
            T("walk",    "🚶‍", "Big walkies (15 min) | Big walkies (30 min) |  Big walkies (1 hour)", 50, 150, 400),
            T("write",     "📕", "Write Something",  60),
            T("cook",     "🍳", "Cook a meal",      50),
            T("craft",  "📓", "Work on a craft",     30),
            T("coffee", "☕", "Make coffee | Make coffee for Nick 🤗",        50, 100),
            T("read",     "📖", "Read 20 minutes x{n}",  30, 60, 120),
            T("dishes", "🍽️", "Do the dishes",    40),
            T("bedtime",  "🌙", "Early bedtime",    50),
            A("mom",     "📞", "Be the best mom",   50)
        },
    };

    public static readonly RewardDef[][] Rewards =
    {
        new[]   // Nick
        {
            R("movie",     "🍿", "Pick the movie tonight", 1500),
            R("breakfast", "🥞", "Breakfast in bed",       2000),
            R("skipchore", "🙅", "Skip a chore",           2500),
            R("coffee",    "☕", "Coffee date",            3000),
            R("backrub",   "💆", "20-minute back rub",     4000),
            R("dinnerout", "🍝", "Nice dinner out",        5000),
            R("sleepin",   "😴", "Sleep-in morning",       6500),
            R("daytrip",   "🚗", "Day trip adventure",     8000),
            R("getaway",   "🏨", "Weekend getaway",       10000),
            R("dream",     "🎆", "Dream date night",      15000),
        },
        new[]   // Whitney
        {
            R("coffee",   "☕", "Get a coffee out",      1500),
            R("dinner",   "🍲", "Get food out",            5000),
            R("books", "📖", "Bookstore visit (1 book)",          7500),
            R("movie",   "🍿", "Movie out",      7500),
            R("target",  "🎯", "$50 Target trip",      10000),
            R("dress",   "🧍‍♀️", "New outfit",       15000),  
            R("head",  "🙂‍↔️", "Head spa",          20000),  
            R("ralph",    "🐻", "Ralph Lauren Polo Sweater",          850000),
            R("getaway",   "🛩️", "3 Day Trip (within continental USA)",       100000),
            R("brunch",    "🚢", "Cruise",     200000),
        },
    };
}
