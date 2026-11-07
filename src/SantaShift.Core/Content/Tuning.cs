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

    /// <summary>The bench minigame: time limit and how the part order gets mixed up as rounds go on.</summary>
    public AssemblyTuning Assembly { get; init; } = new();

    // Orders (GDD section 9)
    public double[] SpawnTimes { get; init; } =
        [0, 18, 36, 54, 72, 90, 108, 126, 144, 162, 180, 198, 216, 234, 252];
    public int MaxLiveOrders { get; init; } = 3;
    public string[] ToySequence { get; init; } = ["train", "bear", "robot"];
    public double DeadlineSeconds { get; init; } = 55;
    public double RibbonDeadlineSeconds { get; init; } = 65;
    public double RushDeadlineSeconds { get; init; } = 45;
    public double RelaxedDeadlineMultiplier { get; init; } = 1.5;

    /// <summary>How many children Santa puts on the naughty list at the start of each run. Their orders want coal.</summary>
    public int NaughtyPerRun { get; init; } = 3;

    /// <summary>
    /// Kits in one supply crate. With a supply sled in the room, each kit bin only holds the kits Santa sent for the
    /// letters so far (one per toy asked for); a wasted kit means fetching a crate from the sled.
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

    public int QuotaFor(GameMode mode, int round = 1) =>
        (mode == GameMode.Relaxed ? RelaxedQuota : Quota) + Rounds.QuotaIncrease * Math.Max(0, round - 1);

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
    /// <summary>Extra deliveries needed for each round after the first.</summary>
    public int QuotaIncrease { get; init; } = 1;

    /// <summary>Deadlines are multiplied by this once per round after the first (0.95 = 5% shorter each round).</summary>
    public double DeadlineMultiplierPerRound { get; init; } = 0.95;

    /// <summary>Deadlines never shrink below this fraction of their round 1 length.</summary>
    public double MinDeadlineMultiplier { get; init; } = 0.7;
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

    /// <summary>From this round the parts must go on in a random order each build (before it: their natural order).</summary>
    public int ShuffleOrderFromRound { get; init; } = 2;

    /// <summary>From this round the parts also move to different number keys each build.</summary>
    public int ShuffleKeysFromRound { get; init; } = 4;

    public int RelaxedRoundOffset { get; init; } = 1;
    public double RelaxedTimeMultiplier { get; init; } = 1.5;

    /// <summary>The round the difficulty is based on (relaxed mode lags behind).</summary>
    public int DifficultyRound(GameMode mode, int round) =>
        Math.Max(1, round - (mode == GameMode.Relaxed ? RelaxedRoundOffset : 0));

    public double TimeLimitFor(GameMode mode, int round)
    {
        double seconds = Math.Max(MinTimeLimitSeconds, TimeLimitSeconds - TimeLimitDecreasePerRound * (DifficultyRound(mode, round) - 1));
        return mode == GameMode.Relaxed ? seconds * RelaxedTimeMultiplier : seconds;
    }

    public bool ShuffleOrder(GameMode mode, int round) => DifficultyRound(mode, round) >= ShuffleOrderFromRound;
    public bool ShuffleKeys(GameMode mode, int round) => DifficultyRound(mode, round) >= ShuffleKeysFromRound;
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
