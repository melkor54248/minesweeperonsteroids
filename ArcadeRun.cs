namespace MinesweeperOnSteroids;

internal enum OverdriveAbility { Drone, Emp, Aegis }
internal enum RunUpgrade { Reactor, Armor, Efficiency }

internal sealed class ArcadeRun
{
    public const int FinalSector = 5;
    private readonly int seed;
    private int completedScore;
    public Difficulty BaseDifficulty { get; }
    public Game Game { get; private set; }
    public int Sector { get; private set; } = 1;
    public int Energy { get; private set; } = 40;
    public int ReactorLevel { get; private set; }
    public int ArmorLevel { get; private set; }
    public int EfficiencyLevel { get; private set; }
    public int TotalScore => completedScore + Game.Score;
    public bool CanAdvance => Game.State == GameState.Won && Sector < FinalSector;
    public bool IsComplete => Game.State == GameState.Won && Sector == FinalSector;
    public bool CanUpgrade(RunUpgrade upgrade) => upgrade switch
    {
        RunUpgrade.Reactor => ReactorLevel < 4,
        RunUpgrade.Armor => ArmorLevel < 2,
        RunUpgrade.Efficiency => EfficiencyLevel < 4,
        _ => false
    };

    public ArcadeRun(Difficulty difficulty, int seed)
    {
        BaseDifficulty = difficulty;
        this.seed = seed;
        Game = CreateSector();
    }

    public int Cost(OverdriveAbility ability) => Math.Max(10, (ability switch
    {
        OverdriveAbility.Drone => 35,
        OverdriveAbility.Emp => 25,
        OverdriveAbility.Aegis => 60,
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    }) - EfficiencyLevel * 5);

    public void EarnEnergy(int safeCellsRevealed)
    {
        if (safeCellsRevealed < 0)
            throw new ArgumentOutOfRangeException(nameof(safeCellsRevealed));
        if (Game.State == GameState.Lost)
            return;
        // Per-action cap prevents a single opening flood from filling the entire meter.
        Energy = Math.Min(100, Energy + Math.Min(20, Math.Min(20, safeCellsRevealed) * (2 + ReactorLevel)));
    }

    public ActionResult Use(OverdriveAbility ability, int x, int y, TimeSpan elapsed)
    {
        int cost = Cost(ability);
        if (x < 0 || y < 0 || x >= Game.Width || y >= Game.Height)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (Game.State != GameState.Playing)
            return new(false, "Overdrive needs an active field. Reveal your opening first.");
        if (Energy < cost)
            return new(false, $"Need {cost} energy. Reveal safe tiles to recharge Overdrive.");
        switch (ability)
        {
            case OverdriveAbility.Drone:
                var candidates = new List<(int X, int Y)>();
                for (int ny = 0; ny < Game.Height; ny++)
                for (int nx = 0; nx < Game.Width; nx++)
                    if (!Game[nx, ny].IsMine && !Game[nx, ny].IsFlagged && !Game[nx, ny].IsRevealed)
                        candidates.Add((nx, ny));
                if (candidates.Count == 0)
                    return new(false, "Drone cannot find an unflagged safe tile. Check your flags.");
                var target = candidates.MinBy(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y));
                ActionResult result = Game.Reveal(target.X, target.Y, elapsed);
                Energy -= cost;
                return new(true, $"DRONE cleared safe tile ({target.X + 1},{target.Y + 1}). {result.Message}");
            case OverdriveAbility.Emp:
                Energy -= cost;
                return new(true, $"EMP swept row {y + 1} for 4 seconds: ! mine / - safe.");
            case OverdriveAbility.Aegis:
                if (Game.Shields >= 3)
                    return new(false, "Aegis is at maximum strength: three shields.");
                Game.AddArcadeShield();
                Energy -= cost;
                return new(true, "AEGIS ONLINE! One additional mine hit blocked. Mines stay in place.");
            default:
                throw new ArgumentOutOfRangeException(nameof(ability));
        }
    }

    public void Advance(RunUpgrade upgrade)
    {
        if (!Enum.IsDefined(upgrade))
            throw new ArgumentOutOfRangeException(nameof(upgrade));
        if (!CanAdvance)
            throw new InvalidOperationException("Clear the current sector before advancing; sector five ends the run.");
        if (!CanUpgrade(upgrade))
            throw new InvalidOperationException("This upgrade is already at maximum level.");
        completedScore += Game.Score;
        switch (upgrade)
        {
            case RunUpgrade.Reactor: ReactorLevel++; break;
            case RunUpgrade.Armor: ArmorLevel++; break;
            case RunUpgrade.Efficiency: EfficiencyLevel++; break;
        }
        Sector++;
        Game = CreateSector();
    }

    private Game CreateSector()
    {
        int extraMines = (Sector - 1) * Math.Max(2, BaseDifficulty.Mines / 5);
        Difficulty field = BaseDifficulty with
        {
            Mines = Math.Min(BaseDifficulty.Width * BaseDifficulty.Height / 3, BaseDifficulty.Mines + extraMines)
        };
        var game = new Game(field, GameMode.Arcade, unchecked(seed + Sector * 7919));
        for (int i = 0; i < Math.Min(2, ArmorLevel); i++)
            game.AddArcadeShield();
        return game;
    }
}
