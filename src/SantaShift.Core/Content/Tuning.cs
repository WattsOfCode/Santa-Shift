using SantaShift.Core.Models;

namespace SantaShift.Core.Content;

/// <summary>
/// Every gameplay number in one place, loaded from tuning.json (GDD section 1: tuning values live in configuration).
/// Times are in seconds and distances in logical pixels; rules convert seconds to ticks with <see cref="Ticks"/>.
/// The defaults below match the GDD and are used for any value missing from the file.
/// </summary>
public sealed class Tuning
{
    public int TicksPerSecond { get; init; } = 60;
    public double ShiftSeconds { get; init; } = 300;

    // Player and floor (GDD section 13)
    public double PlayerSpeed { get; init; } = 200;
    public double InteractRange { get; init; } = 54;
    public Point2 PlayerStart { get; init; } = new(470, 410);
    public double PlayerColliderWidth { get; init; } = 22;
    public double PlayerColliderHeight { get; init; } = 18;
    public FloorBounds Floor { get; init; } = new();

    // Crafting (GDD section 8)
    public double RibbonSeconds { get; init; } = 1.0;

    /// <summary>Time to shovel a lump of coal out of the scuttle by the fireplace.</summary>
    public double ShovelSeconds { get; init; } = 0.8;

    /// <summary>The bench minigame: time limit, when toy variants start and when the combinations stop being listed.</summary>
    public AssemblyTuning Assembly { get; init; } = new();

    // Orders (GDD section 9)
    public double[] SpawnTimes { get; init; } =
        [0, 18, 36, 54, 72, 90, 108, 126, 144, 162, 180, 198, 216, 234, 252];
    public int MaxLiveOrders { get; init; } = 3;

    /// <summary>From the second round: a steady stream of letters on a bigger board (see <see cref="FlowTuning"/>).</summary>
    public FlowTuning Flow { get; init; } = new();
    public string[] ToySequence { get; init; } = ["train", "bear", "robot"];
    public double DeadlineSeconds { get; init; } = 55;
    public double RibbonDeadlineSeconds { get; init; } = 65;
    public double RushDeadlineSeconds { get; init; } = 45;
    public double RelaxedDeadlineMultiplier { get; init; } = 1.5;

    /// <summary>How many children Santa puts on the naughty list at the start of each run. Their orders want coal.</summary>
    public int NaughtyPerRun { get; init; } = 3;

    /// <summary>
    /// Kits in one supply crate. With a supply sled in the room, each kit bin is filled once when a shift starts (one kit
    /// per toy the shift's letters will ask for) and never topped up; a wasted kit means fetching a crate from the sled.
    /// </summary>
    public int ResupplyKits { get; init; } = 2;

    /// <summary>
    /// Ribbon colours at the ribbon station. When there are any, ribbon orders ask for one of them and the elf picks a
    /// spool before tying (spool n on part key n). Empty: any ribbon will do and tying starts at once.
    /// </summary>
    public RibbonColor[] RibbonColors { get; init; } = [];

    /// <summary>Perk cards Santa offers after a passed round; one more after a round well over quota.</summary>
    public int PerkOffers { get; init; } = 3;

    /// <summary>Delivering this many over the quota earns an extra perk card to choose from.</summary>
    public int BonusPerkMargin { get; init; } = 2;

    public SpiritTuning Spirit { get; init; } = new();

    public TutorialTuning Tutorial { get; init; } = new();

    /// <summary>The toy cart: built one part per passed round, then it carries finished toys (see <see cref="CartTuning"/>).</summary>
    public CartTuning Cart { get; init; } = new();

    public RibbonColor? FindRibbon(string? id) => id == null ? null : RibbonColors.FirstOrDefault(r => r.Id == id);

    // Scoring and review (GDD section 12)
    public ScoreTuning Score { get; init; } = new();
    public int Quota { get; init; } = 9;
    public int RelaxedQuota { get; init; } = 6;
    public int SurvivorDelivered { get; init; } = 9;
    public int LegendDelivered { get; init; } = 12;

    // Rounds: passing a round (meeting the quota) starts a harder one; missing it ends the run.
    public RoundTuning Rounds { get; init; } = new();

    // World scale: rules work in logical pixels; the 3D world and Blender work in metres.
    public double PixelsPerMetre { get; init; } = 40;
    public Point2 WorldOrigin { get; init; } = new(480, 300);

    public long Ticks(double seconds) => (long)Math.Round(seconds * TicksPerSecond);
    public long ShiftTicks => Ticks(ShiftSeconds);

    /// <summary>
    /// Deliveries needed in a round: the round 1 quota, plus each later round's increase from
    /// <see cref="RoundTuning.QuotaIncreases"/>, then <see cref="RoundTuning.QuotaIncrease"/> a round once that list runs out.
    /// </summary>
    public int QuotaFor(GameMode mode, int round = 1)
    {
        bool relaxed = mode == GameMode.Relaxed;
        int quota = relaxed ? RelaxedQuota : Quota;
        int[] steps = relaxed ? Rounds.RelaxedQuotaIncreases : Rounds.QuotaIncreases;
        int after = relaxed ? Rounds.RelaxedQuotaIncrease : Rounds.QuotaIncrease;
        for (int r = 2; r <= round; r++) quota += r - 2 < steps.Length ? steps[r - 2] : after;
        return quota;
    }

    /// <summary>How many orders can wait on the board at once in this round.</summary>
    public int MaxLiveFor(int round) => Flow.On(round) ? Math.Max(MaxLiveOrders, Flow.MaxLiveOrders) : MaxLiveOrders;

    /// <summary>Below this many waiting orders, the next letter comes early (0: never).</summary>
    public int MinLiveFor(int round) => Flow.On(round) ? Flow.MinLiveOrders : 0;

    /// <summary>
    /// When each letter of the shift arrives, in ticks. Round 1 uses <see cref="SpawnTimes"/>. Later rounds start with
    /// <see cref="FlowTuning.MinLiveOrders"/> letters at once, then one every interval (shorter each round), plus a few
    /// reserve letters (tick <see cref="long.MaxValue"/>) that only arrive early, to keep the board from running dry.
    /// </summary>
    public long[] SpawnTicksFor(GameMode mode, int round)
    {
        if (!Flow.On(round)) return SpawnTimes.Select(Ticks).ToArray();
        double interval = Math.Max(Flow.MinIntervalSeconds,
            Flow.IntervalSeconds - Flow.IntervalDecreasePerRound * (round - Flow.FromRound));
        if (mode == GameMode.Relaxed) interval *= Flow.RelaxedIntervalMultiplier;
        interval = Math.Max(1, interval);
        var ticks = new List<long>();
        for (int i = 0; i < Math.Max(1, Flow.MinLiveOrders); i++) ticks.Add(0);
        for (double s = interval; s <= Flow.LastLetterSeconds; s += interval) ticks.Add(Ticks(s));
        for (int i = 0; i < Flow.ReserveLetters; i++) ticks.Add(long.MaxValue);
        return ticks.ToArray();
    }

    /// <summary>Multiplies every order deadline: relaxed mode lengthens them, later rounds shorten them.</summary>
    public double DeadlineMultiplier(GameMode mode, int round = 1)
    {
        double roundFactor = Math.Max(Rounds.MinDeadlineMultiplier, Math.Pow(Rounds.DeadlineMultiplierPerRound, Math.Max(0, round - 1)));
        return (mode == GameMode.Relaxed ? RelaxedDeadlineMultiplier : 1.0) * roundFactor;
    }

    /// <summary>Converts a point in game-world metres (x right, z towards the player's start) to logical pixels.</summary>
    public Point2 MetresToPixels(double x, double z) => new(WorldOrigin.X + x * PixelsPerMetre, WorldOrigin.Y + z * PixelsPerMetre);
}

public sealed class RoundTuning
{
    /// <summary>
    /// Extra deliveries needed in round 2, round 3 and so on (added up). Round 2 opens the floodgates with only a
    /// small rise; from round 3 the demands climb.
    /// </summary>
    public int[] QuotaIncreases { get; init; } = [1, 3];

    /// <summary>Extra deliveries needed for each round after the <see cref="QuotaIncreases"/> list runs out.</summary>
    public int QuotaIncrease { get; init; } = 2;

    public int[] RelaxedQuotaIncreases { get; init; } = [1, 2];
    public int RelaxedQuotaIncrease { get; init; } = 1;

    /// <summary>
    /// Rush shifts: from round <see cref="RushShiftFrom"/>, every <see cref="RushShiftEvery"/> rounds, every order of
    /// the shift is RUSH (0 turns them off).
    /// </summary>
    public int RushShiftFrom { get; init; } = 4;
    public int RushShiftEvery { get; init; } = 3;

    public bool IsRushShift(int round) =>
        RushShiftFrom > 0 && round >= RushShiftFrom && (round - RushShiftFrom) % Math.Max(1, RushShiftEvery) == 0;

    /// <summary>Deadlines are multiplied by this once per round after the first (0.95 = 5% shorter each round).</summary>
    public double DeadlineMultiplierPerRound { get; init; } = 0.95;

    /// <summary>Deadlines never shrink below this fraction of their round 1 length.</summary>
    public double MinDeadlineMultiplier { get; init; } = 0.7;
}

/// <summary>
/// The toy cart. Each passed round of a run adds the next of its <see cref="Parts"/>; once all are on (with three parts,
/// from round 4) it holds up to <see cref="Capacity"/> finished toys and can be pushed at <see cref="SpeedMultiplier"/>
/// times walking speed. Every run builds it again.
/// </summary>
public sealed class CartTuning
{
    /// <summary>The cart's parts, in the order they are earned (one per passed round). Empty = no cart.</summary>
    public string[] Parts { get; init; } = ["Wheels", "Toy tray", "Push handle"];

    public int Capacity { get; init; } = 3;

    /// <summary>The elf's speed while pushing it, as a share of walking speed.</summary>
    public double SpeedMultiplier { get; init; } = 0.75;

    /// <summary>Where the cart stands at the start of every shift (logical pixels): between the tree and the robot bin.</summary>
    public Point2 Home { get; init; } = new(780, 172);

    /// <summary>How far ahead of the elf the cart rolls while pushed, centre to centre (logical pixels).</summary>
    public double PushDistance { get; init; } = 30;

    /// <summary>How quickly a pushed cart swings round to where the elf walks (share of the gap closed each step, 0..1).</summary>
    public double TurnRate { get; init; } = 0.12;

    /// <summary>Whether the cart works with this many parts built.</summary>
    public bool BuiltWith(int parts) => Parts.Length > 0 && parts >= Parts.Length;
}

/// <summary>
/// Difficulty of the bench minigame by round. Relaxed mode plays as if it were <see cref="RelaxedRoundOffset"/>
/// rounds earlier and gets <see cref="RelaxedTimeMultiplier"/> times as long.
/// </summary>
public sealed class AssemblyTuning
{
    /// <summary>Time to add every part in round 1.</summary>
    public double TimeLimitSeconds { get; init; } = 6.0;

    /// <summary>Taken off the time limit for each round after the first.</summary>
    public double TimeLimitDecreasePerRound { get; init; } = 0.5;

    public double MinTimeLimitSeconds { get; init; } = 2.5;

    /// <summary>The time limits above are for a toy of this many parts; a longer combination gets more time in proportion.</summary>
    public int BaseParts { get; init; } = 3;

    /// <summary>From this round some letters ask for a toy variant (big wheels, extra stuffing, a rocket pack). 0 = never.</summary>
    public int VariantsFromRound { get; init; } = 2;

    /// <summary>The share of a shift's letters that ask for a variant in the round variants start (0 to 1)...</summary>
    public double VariantShare { get; init; } = 0.25;

    /// <summary>...plus this much for each round after it...</summary>
    public double VariantSharePerRound { get; init; } = 0.1;

    /// <summary>...up to this share.</summary>
    public double MaxVariantShare { get; init; } = 0.5;

    /// <summary>
    /// From this round the bench stops listing the build combinations: the elf works from memory (the parts still
    /// show their keys). 0 = always list them.
    /// </summary>
    public int HideRecipesFromRound { get; init; } = 5;

    public int RelaxedRoundOffset { get; init; } = 1;
    public double RelaxedTimeMultiplier { get; init; } = 1.5;

    /// <summary>The round the difficulty is based on (relaxed mode lags behind).</summary>
    public int DifficultyRound(GameMode mode, int round) =>
        Math.Max(1, round - (mode == GameMode.Relaxed ? RelaxedRoundOffset : 0));

    /// <summary>Time to put on <see cref="BaseParts"/> parts.</summary>
    public double TimeLimitFor(GameMode mode, int round)
    {
        double seconds = Math.Max(MinTimeLimitSeconds, TimeLimitSeconds - TimeLimitDecreasePerRound * (DifficultyRound(mode, round) - 1));
        return mode == GameMode.Relaxed ? seconds * RelaxedTimeMultiplier : seconds;
    }

    /// <summary>Time to put on a combination of <paramref name="parts"/> parts.</summary>
    public double TimeLimitFor(GameMode mode, int round, int parts) =>
        TimeLimitFor(mode, round) * Math.Max(1, parts) / Math.Max(1, BaseParts);

    /// <summary>Whether letters can ask for toy variants in this round.</summary>
    public bool Variants(GameMode mode, int round) =>
        VariantsFromRound > 0 && DifficultyRound(mode, round) >= VariantsFromRound;

    /// <summary>
    /// Whether letters can ask for this variant (and the bench builds it) in this round: once variants have started and
    /// the variant's own first round has come. Never in the guided lesson.
    /// </summary>
    public bool VariantOpen(ToyVariant variant, GameMode mode, int round, bool tutorial) =>
        !tutorial && Variants(mode, round) && DifficultyRound(mode, round) >= variant.FromRound;

    /// <summary>The share of this round's letters that ask for a variant (0 before variants start).</summary>
    public double VariantShareFor(GameMode mode, int round) => !Variants(mode, round) ? 0
        : Math.Clamp(VariantShare + VariantSharePerRound * (DifficultyRound(mode, round) - VariantsFromRound), 0, Math.Max(0, MaxVariantShare));

    /// <summary>Whether the bench lists the build combinations in this round.</summary>
    public bool ShowRecipes(GameMode mode, int round) =>
        HideRecipesFromRound <= 0 || DifficultyRound(mode, round) < HideRecipesFromRound;

    /// <summary>
    /// How the bench changes in this round compared with the one before: "assembly_variants" (letters start asking for
    /// toy variants, each with its own combination), "assembly_memory" (the bench stops listing the combinations), or
    /// null (no change). The ids are dialogue cue names.
    /// </summary>
    public string? PolicyChange(GameMode mode, int round)
    {
        if (round <= 1) return null;
        if (!ShowRecipes(mode, round) && ShowRecipes(mode, round - 1)) return "assembly_memory";
        if (Variants(mode, round) && !Variants(mode, round - 1)) return "assembly_variants";
        return null;
    }
}

/// <summary>
/// The steady flow of letters from the second round on (the training wheels come off): more letters, a board that holds
/// up to <see cref="MaxLiveOrders"/>, and never fewer than <see cref="MinLiveOrders"/> waiting, so there is no idle time.
/// </summary>
public sealed class FlowTuning
{
    /// <summary>The first round with the steady flow (0 turns it off).</summary>
    public int FromRound { get; init; } = 2;

    public int MaxLiveOrders { get; init; } = 10;

    /// <summary>When fewer orders than this are waiting, the next letter arrives early.</summary>
    public int MinLiveOrders { get; init; } = 2;

    /// <summary>The shortest gap between two letters when one comes early.</summary>
    public double RefillSeconds { get; init; } = 2;

    /// <summary>The gap between scheduled letters in round <see cref="FromRound"/>, shrinking each round after it.</summary>
    public double IntervalSeconds { get; init; } = 16;
    public double IntervalDecreasePerRound { get; init; } = 1.5;
    public double MinIntervalSeconds { get; init; } = 10;
    public double RelaxedIntervalMultiplier { get; init; } = 1.25;

    /// <summary>No scheduled letter after this time (early letters still come to keep the board stocked).</summary>
    public double LastLetterSeconds { get; init; } = 260;

    /// <summary>Extra letters in Santa's sack that only arrive early, when the board runs low.</summary>
    public int ReserveLetters { get; init; } = 6;

    public bool On(int round) => FromRound > 0 && round >= FromRound;
}

public sealed class FloorBounds
{
    public double Left { get; init; } = 20;
    public double Top { get; init; } = 120;
    public double Right { get; init; } = 940;
    public double Bottom { get; init; } = 480;
}

public sealed class ScoreTuning
{
    public int Base { get; init; } = 100;
    public int RibbonBonus { get; init; } = 25;
    public int RushBonus { get; init; } = 25;
    public int TimeBonusMax { get; init; } = 20;
    public int StreakStep { get; init; } = 5;
    public int StreakCap { get; init; } = 4;
}

/// <summary>
/// The Christmas Spirit meter: it lasts the whole run. Deliveries raise it, missed orders and botched toys lower it.
/// High spirit ("jolly") earns bonus points; if it runs out, the shift ends there as a failure.
/// Off unless tuning.json turns it on, so rule tests that don't mention it are unaffected.
/// </summary>
public sealed class SpiritTuning
{
    public bool Enabled { get; init; }
    public double Start { get; init; } = 100;
    public double Max { get; init; } = 100;
    public double Delivered { get; init; } = 4;
    public double CoalDelivered { get; init; } = 4;
    public double Missed { get; init; } = 20;
    public double Botched { get; init; } = 8;

    /// <summary>Spirit regained between rounds (a night's sleep).</summary>
    public double BetweenRounds { get; init; } = 15;

    /// <summary>At or above this, deliveries earn <see cref="JollyBonus"/> more points.</summary>
    public double JollyAt { get; init; } = 75;
    public double JollyBonus { get; init; } = 0.1;
}

/// <summary>
/// The guided first round: arrows on the floor lead the elf through the first few orders, which are arranged to show
/// every job once (one is coal for a naughty child, one needs a ribbon).
/// </summary>
public sealed class TutorialTuning
{
    /// <summary>How many orders are guided.</summary>
    public int Orders { get; init; } = 6;

    /// <summary>Which of them (1-based) is coal for a naughty child, and which needs a ribbon.</summary>
    public int CoalOrder { get; init; } = 3;
    public int RibbonOrder { get; init; } = 5;

    /// <summary>
    /// With a supply sled: Santa's sack for the lesson round is one kit short, for the last lesson order's toy. That toy
    /// is asked for nowhere else that round, so its bin is empty just when the guide can teach the sled.
    /// </summary>
    public bool ShortKit { get; init; } = true;
}
