namespace MinesweeperOnSteroids;

internal static class ArcadeTests
{
    public static void Energy()
    {
        var run = Open();
        Check(run.Energy == 40, "Run must start with 40 energy.");
        run.EarnEnergy(1);
        Check(run.Energy == 42, "Safe tile must earn 2 energy.");
        run.EarnEnergy(100);
        Check(run.Energy == 62, "Opening flood must be capped at 20 energy per action.");
        for (int i = 0; i < 10; i++)
            run.EarnEnergy(100);
        Check(run.Energy == 100, "Energy must cap at 100.");
        run.EarnEnergy(int.MaxValue);
        Check(run.Energy == 100, "Large flood counts must not overflow the meter.");
        var ready = new ArcadeRun(Difficulty.Presets[0], 42);
        Check(!ready.Use(OverdriveAbility.Emp, 0, 0, TimeSpan.Zero).Changed && ready.Energy == 40,
            "Cannot spend energy before the opening.");
    }

    public static void Drone()
    {
        var run = Open();
        int before = run.Game.SafeRemaining;
        Check(run.Use(OverdriveAbility.Drone, 0, 0, TimeSpan.FromSeconds(1)).Changed,
            "Drone must deploy.");
        Check(run.Energy == 5 && run.Game.SafeRemaining < before && run.Game.State != GameState.Lost,
            "Drone must reveal safely and spend exactly 35 without refunding energy.");
        Check(!run.Use(OverdriveAbility.Drone, 0, 0, TimeSpan.FromSeconds(2)).Changed && run.Energy == 5,
            "Insufficient energy must not spend more.");

        run = Open();
        foreach (var p in Cells(run.Game).Where(p => !run.Game[p.X, p.Y].IsMine && !run.Game[p.X, p.Y].IsRevealed))
            run.Game.ToggleFlag(p.X, p.Y);
        Check(!run.Use(OverdriveAbility.Drone, 0, 0, TimeSpan.Zero).Changed && run.Energy == 40,
            "Drone must respect safe flags and not charge when no target exists.");
    }

    public static void EmpAndShield()
    {
        var run = Open();
        string layout = Layout(run.Game);
        int remaining = run.Game.SafeRemaining;
        Check(run.Use(OverdriveAbility.Emp, 2, 3, TimeSpan.Zero).Changed &&
            run.Energy == 15 && run.Game.SafeRemaining == remaining && Layout(run.Game) == layout,
            "EMP must spend 25, never reveal, and preserve clues.");
        run.EarnEnergy(100);
        run.EarnEnergy(100);
        run.EarnEnergy(100);
        Check(run.Use(OverdriveAbility.Aegis, 0, 0, TimeSpan.Zero).Changed && run.Game.Shields == 2 &&
            run.Energy == 15, "Aegis must add one shield for 60 energy.");
        run.EarnEnergy(100);
        run.EarnEnergy(100);
        run.EarnEnergy(100);
        run.Use(OverdriveAbility.Aegis, 0, 0, TimeSpan.Zero);
        run.EarnEnergy(100);
        run.EarnEnergy(100);
        run.EarnEnergy(100);
        int energy = run.Energy;
        Check(!run.Use(OverdriveAbility.Aegis, 0, 0, TimeSpan.Zero).Changed && run.Game.Shields == 3 &&
            run.Energy == energy && Layout(run.Game) == layout, "Max shields must not cost energy or move mines.");
    }

    public static void Progression()
    {
        var run = Open();
        int cumulative = 0;
        int previousMines = 0;
        foreach (RunUpgrade upgrade in new[] { RunUpgrade.Reactor, RunUpgrade.Efficiency, RunUpgrade.Armor, RunUpgrade.Armor })
        {
            Check(run.Game.Difficulty.Mines > previousMines, "Mine count must rise each sector.");
            previousMines = run.Game.Difficulty.Mines;
            Win(run.Game);
            cumulative += run.Game.Score;
            Check(run.TotalScore == cumulative && run.CanAdvance, "Cumulative score or intermission incorrect.");
            int energy = run.Energy;
            run.Advance(upgrade);
            Check(run.Game.State == GameState.Ready && run.Energy == energy && run.Game.Scans == 3,
                "Next sector must reset field/radar but keep energy.");
            run.Game.Reveal(4, 4, TimeSpan.Zero);
        }
        Check(run.Sector == 5 && run.Game.Shields == 3 && run.Cost(OverdriveAbility.Drone) == 30 &&
            run.Cost(OverdriveAbility.Emp) == 20 && run.Cost(OverdriveAbility.Aegis) == 55,
            "Upgrades must carry into later sectors.");
        int before = run.Energy;
        run.EarnEnergy(1);
        Check(run.Energy == before + 3, "Reactor upgrade must earn extra energy.");
        Win(run.Game);
        Check(run.IsComplete && !run.CanAdvance, "Five cleared sectors must finish the run.");
        Throws<InvalidOperationException>(() => run.Advance(RunUpgrade.Reactor));
        Check(!run.Use(OverdriveAbility.Emp, 0, 0, TimeSpan.Zero).Changed, "No abilities after completion.");
    }

    public static void Guards()
    {
        var run = Open();
        Throws<InvalidOperationException>(() => run.Advance(RunUpgrade.Reactor));
        Throws<ArgumentOutOfRangeException>(() => run.Use((OverdriveAbility)99, 0, 0, TimeSpan.Zero));
        Throws<ArgumentOutOfRangeException>(() => run.Use(OverdriveAbility.Emp, -1, 0, TimeSpan.Zero));
        Throws<ArgumentOutOfRangeException>(() => run.EarnEnergy(-1));
        var mines = Cells(run.Game).Where(p => run.Game[p.X, p.Y].IsMine).Take(2).ToArray();
        foreach (var p in mines)
            run.Game.Reveal(p.X, p.Y, TimeSpan.Zero);
        Check(run.Game.State == GameState.Lost, "Fixture must end the run.");
        int energy = run.Energy;
        run.EarnEnergy(100);
        Check(run.Energy == energy && !run.Use(OverdriveAbility.Drone, 0, 0, TimeSpan.Zero).Changed,
            "Loss cannot earn or spend energy.");
        Throws<InvalidOperationException>(() => run.Advance(RunUpgrade.Armor));
    }

    public static void UpgradeCaps()
    {
        var armor = Open();
        for (int i = 0; i < 2; i++)
        {
            Win(armor.Game);
            armor.Advance(RunUpgrade.Armor);
            armor.Game.Reveal(4, 4, TimeSpan.Zero);
        }
        Win(armor.Game);
        int score = armor.TotalScore;
        Check(!armor.CanUpgrade(RunUpgrade.Armor), "Armor must cap at two upgrades.");
        Throws<InvalidOperationException>(() => armor.Advance(RunUpgrade.Armor));
        Check(armor.Sector == 3 && armor.TotalScore == score && armor.Game.State == GameState.Won,
            "Rejected max upgrade must not advance or duplicate score.");
        armor.Advance(RunUpgrade.Reactor);
        Check(armor.Game.Shields == 3, "Capped armor must keep three starting shields.");

        foreach (Difficulty difficulty in Difficulty.Presets)
        {
            var run = new ArcadeRun(difficulty, 74);
            for (int sector = 1; sector <= 5; sector++)
            {
                int expected = Math.Min(difficulty.Width * difficulty.Height / 3,
                    difficulty.Mines + (sector - 1) * Math.Max(2, difficulty.Mines / 5));
                Check(run.Game.Difficulty.Mines == expected && run.Game.Width == difficulty.Width &&
                    run.Game.Height == difficulty.Height, "Sector density must scale without resizing the field.");
                run.Game.Reveal(4, 4, TimeSpan.Zero);
                Check(run.Game[4, 4].AdjacentMines == 0, "Late sector opening must still be safe.");
                Win(run.Game);
                if (sector < 5)
                    run.Advance(RunUpgrade.Efficiency);
            }
            Check(run.Cost(OverdriveAbility.Emp) == 10 && run.Cost(OverdriveAbility.Drone) == 15 &&
                run.Cost(OverdriveAbility.Aegis) == 40, "Repeated efficiency upgrades must respect cost floor.");
        }
    }

    public static void Records()
    {
        string path = Path.Combine(Path.GetTempPath(), $"arcade-records-{Guid.NewGuid():N}.json");
        try
        {
            var run = Open();
            Win(run.Game);
            int firstScore = run.TotalScore;
            run.Advance(RunUpgrade.Efficiency);
            run.Game.Reveal(4, 4, TimeSpan.Zero);
            Win(run.Game);
            var store = new RecordStore(path);
            store.AddWin(run.Game, TimeSpan.FromSeconds(10), new(2026, 11, 19), run);
            var loaded = new RecordStore(path);
            loaded.Load();
            Check(loaded.Warning is null && loaded.Data.Wins.Single().Sector == 2 &&
                loaded.Data.Wins[0].Score == run.TotalScore && run.TotalScore > firstScore,
                "Arcade records must preserve sector and cumulative score.");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    private static ArcadeRun Open()
    {
        var run = new ArcadeRun(Difficulty.Presets[0], 42);
        run.Game.Reveal(4, 4, TimeSpan.Zero);
        return run;
    }

    private static void Win(Game game)
    {
        foreach (var p in Cells(game).Where(p => !game[p.X, p.Y].IsMine && !game[p.X, p.Y].IsRevealed))
        {
            if (game[p.X, p.Y].IsFlagged)
                game.ToggleFlag(p.X, p.Y);
            game.Reveal(p.X, p.Y, TimeSpan.FromSeconds(10));
        }
        Check(game.State == GameState.Won, "Fixture must win.");
    }

    private static IEnumerable<(int X, int Y)> Cells(Game game)
    {
        for (int y = 0; y < game.Height; y++)
        for (int x = 0; x < game.Width; x++)
            yield return (x, y);
    }

    private static string Layout(Game game) => string.Concat(Cells(game).Select(p =>
        game[p.X, p.Y].IsMine ? "*" : game[p.X, p.Y].AdjacentMines.ToString()));

    private static void Check(bool success, string message)
    {
        if (!success)
            throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
