namespace MinesweeperOnSteroids;

internal static class SelfTests
{
    public static int Run()
    {
        (string Name, Action Test)[] tests =
        [
            ("Generation: 240 seeded boards, safe openings, exact clues", Generation),
            ("Flags block reveals and do not start a round", Flags),
            ("Flood reveal preserves flags", FloodPreservesFlags),
            ("Classic mine hit loses immediately", ClassicLoss),
            ("Shield blocks once without changing clues", Shield),
            ("Radar is limited and never reveals or changes mines", Radar),
            ("Correctly flagged chord opens safe neighbors", CorrectChord),
            ("Chord rejects mismatched flag counts", BlockedChord),
            ("Wrong flags make a chord lethal", WrongChord),
            ("One shield cannot stop a multi-mine chord", MultiMineChord),
            ("All safe cells win without flags; end state is immutable", Victory),
            ("Combo caps at 5x, expires, and scores exact cell counts", Combo),
            ("Classic never earns combo multipliers", ClassicScore),
            ("Daily board and opening are reproducible", Daily),
            ("Invalid board sizes and coordinates are rejected", Validation),
            ("Settings and wins survive a storage round trip", Persistence),
            ("Malformed records are warned about and preserved", CorruptRecords),
            ("Invalid record values are rejected", InvalidRecords),
            ("Storage write failures produce warnings", StorageFailure)
        ];
        int failures = 0;
        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL  {name}\n      {ex}");
            }
        }
        Console.WriteLine($"\n{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void Generation()
    {
        foreach (Difficulty d in Difficulty.Presets)
        for (int seed = 0; seed < 80; seed++)
        {
            int sx = seed % d.Width;
            int sy = seed % d.Height;
            var game = new Game(d, GameMode.Classic, seed);
            Check(game.Reveal(sx, sy, TimeSpan.Zero).Changed, "First reveal must change the board.");
            Check(game[sx, sy].IsRevealed && game[sx, sy].AdjacentMines == 0, "Opening must be empty.");
            Check(Positions(game).Count(p => game[p.X, p.Y].IsMine) == d.Mines, "Mine count mismatch.");
            foreach (var (x, y) in Positions(game))
            {
                Cell c = game[x, y];
                Check(c.AdjacentMines == game.Neighbors(x, y).Count(p => game[p.X, p.Y].IsMine),
                    "Clue mismatch.");
                Check(!c.IsRevealed || !c.IsMine, "Opening revealed a mine.");
            }
            Check(game.Neighbors(sx, sy).All(p => !game[p.X, p.Y].IsMine), "Opening neighbor is a mine.");
            Check(game.SafeRemaining == Positions(game).Count(p =>
                !game[p.X, p.Y].IsMine && !game[p.X, p.Y].IsRevealed), "Safe count mismatch.");
        }
    }

    private static void Flags()
    {
        var game = new Game(Difficulty.Presets[0], GameMode.Classic, 42);
        Check(game.ToggleFlag(4, 4).Changed && game.FlagCount == 1, "Flag not set.");
        Check(!game.Reveal(4, 4, TimeSpan.Zero).Changed && game.State == GameState.Ready,
            "Flagged reveal started a round.");
        Check(game.ToggleFlag(4, 4).Changed && game.FlagCount == 0, "Flag not removed.");
        game.Reveal(4, 4, TimeSpan.Zero);
        Check(!game.ToggleFlag(4, 4).Changed, "Revealed cell accepted a flag.");
        foreach (var p in Positions(game).Where(p => !game[p.X, p.Y].IsRevealed))
            game.ToggleFlag(p.X, p.Y);
        Check(game.FlagCount == Positions(game).Count(p => game[p.X, p.Y].IsFlagged), "Flags out of sync.");
    }

    private static void FloodPreservesFlags()
    {
        var game = new Game(Difficulty.Presets[0], GameMode.Classic, 123);
        game.ToggleFlag(3, 3);
        game.Reveal(4, 4, TimeSpan.Zero);
        Check(game[3, 3].IsFlagged && !game[3, 3].IsRevealed && !game[3, 3].IsMine,
            "Flood reveal crossed a flag.");
        game.ToggleFlag(3, 3);
        game.Reveal(3, 3, TimeSpan.FromSeconds(1));
        Check(game[3, 3].IsRevealed, "Unflagged safe cell was not revealed.");
    }

    private static void ClassicLoss()
    {
        Game game = Open(GameMode.Classic);
        var mine = Positions(game).First(p => game[p.X, p.Y].IsMine);
        game.Reveal(mine.X, mine.Y, TimeSpan.FromSeconds(1));
        Check(game.State == GameState.Lost && game.ExplodedCell == mine, "Mine did not end Classic.");
        int score = game.Score;
        Check(!game.Reveal(0, 0, TimeSpan.FromSeconds(2)).Changed &&
            !game.ToggleFlag(0, 0).Changed && !game.Scan(0, 0).Changed &&
            !game.Chord(4, 4, TimeSpan.FromSeconds(2)).Changed && game.Score == score,
            "Lost round mutated.");
    }

    private static void Shield()
    {
        Game game = Open();
        var mines = Positions(game).Where(p => game[p.X, p.Y].IsMine).ToArray();
        string layout = Layout(game);
        int remaining = game.SafeRemaining;
        int score = game.Score;
        game.Reveal(mines[0].X, mines[0].Y, TimeSpan.FromSeconds(1));
        Check(game.State == GameState.Playing && game.Shields == 0, "Shield failed.");
        Check(game[mines[0].X, mines[0].Y].IsFlagged && game.FlagCount == 1, "Hit mine not flagged.");
        Check(game.SafeRemaining == remaining && game.Score == score, "Shield awarded safe-cell credit.");
        Check(Layout(game) == layout, "Shield changed mines or clues.");
        game.Reveal(mines[1].X, mines[1].Y, TimeSpan.FromSeconds(2));
        Check(game.State == GameState.Lost, "Second mine hit should lose.");
    }

    private static void Radar()
    {
        var ready = new Game(Difficulty.Presets[0], GameMode.Steroids, 1);
        Check(!ready.Scan(4, 4).Changed && ready.Scans == 3, "Radar used before generation.");
        Game game = Open();
        string layout = Layout(game);
        int remaining = game.SafeRemaining;
        int score = game.Score;
        for (int i = 0; i < 3; i++)
            Check(game.Scan(0, 0).Changed, "Radar charge not available.");
        Check(!game.Scan(0, 0).Changed && game.Scans == 0, "Unlimited radar.");
        Check(Layout(game) == layout && game.SafeRemaining == remaining && game.Score == score,
            "Radar mutated game state.");
        Check(!Open(GameMode.Classic).Scan(0, 0).Changed, "Classic has radar.");
    }

    private static void CorrectChord()
    {
        Game game = Open(GameMode.Classic);
        var clue = FindClue(game, (g, x, y) => g.Neighbors(x, y).Any(p =>
            !g[p.X, p.Y].IsMine && !g[p.X, p.Y].IsRevealed));
        foreach (var p in game.Neighbors(clue.X, clue.Y).Where(p => game[p.X, p.Y].IsMine))
            game.ToggleFlag(p.X, p.Y);
        int before = game.SafeRemaining;
        Check(game.Chord(clue.X, clue.Y, TimeSpan.FromSeconds(1)).Changed &&
            game.State != GameState.Lost && game.SafeRemaining < before, "Correct chord failed.");
    }

    private static void BlockedChord()
    {
        Game game = Open();
        var clue = FindClue(game, (_, _, _) => true);
        int remaining = game.SafeRemaining;
        Check(!game.Chord(clue.X, clue.Y, TimeSpan.FromSeconds(1)).Changed &&
            remaining == game.SafeRemaining, "Mismatched chord changed board.");
        Check(!game.Chord(4, 4, TimeSpan.FromSeconds(1)).Changed, "Chord accepted empty cell.");
    }

    private static void WrongChord()
    {
        var (game, clue) = ChordTrap(GameMode.Classic, minimumMines: 1);
        game.Chord(clue.X, clue.Y, TimeSpan.FromSeconds(1));
        Check(game.State == GameState.Lost, "Wrong-flag chord should lose.");
    }

    private static void MultiMineChord()
    {
        var (game, clue) = ChordTrap(GameMode.Steroids, minimumMines: 2);
        game.Chord(clue.X, clue.Y, TimeSpan.FromSeconds(1));
        Check(game.Shields == 0 && game.State == GameState.Lost, "One shield survived multiple mines.");
    }

    private static (Game Game, (int X, int Y) Clue) ChordTrap(GameMode mode, int minimumMines)
    {
        for (int seed = 0; seed < 200; seed++)
        {
            Game game = Open(mode, seed);
            foreach (var p in Positions(game).Where(p =>
                         game[p.X, p.Y].IsRevealed && game[p.X, p.Y].AdjacentMines >= minimumMines))
            {
                var safe = game.Neighbors(p.X, p.Y).Where(n =>
                    !game[n.X, n.Y].IsRevealed && !game[n.X, n.Y].IsMine).ToArray();
                int count = game[p.X, p.Y].AdjacentMines;
                if (safe.Length < count)
                    continue;
                foreach (var flag in safe.Take(count))
                    game.ToggleFlag(flag.X, flag.Y);
                return (game, p);
            }
        }
        throw new InvalidOperationException("No chord trap fixture found.");
    }

    private static void Victory()
    {
        foreach (GameMode mode in Enum.GetValues<GameMode>())
        {
            Game game = Open(mode);
            foreach (var (x, y) in Positions(game).Where(p => !game[p.X, p.Y].IsMine))
                game.Reveal(x, y, TimeSpan.FromSeconds(10));
            Check(game.State == GameState.Won && game.SafeRemaining == 0 && game.FlagCount == 0,
                "Safe-cell victory failed.");
            int score = game.Score;
            Check(!game.Reveal(0, 0, TimeSpan.Zero).Changed && !game.ToggleFlag(0, 0).Changed &&
                !game.Scan(0, 0).Changed && !game.Chord(4, 4, TimeSpan.Zero).Changed &&
                game.Score == score, "Won round mutated.");
        }
    }

    private static void Combo()
    {
        Game game = Open(GameMode.Steroids, 42, Difficulty.Presets[2]);
        for (int i = 1; i <= 7; i++)
        {
            var p = CoveredNumber(game);
            int before = game.SafeRemaining;
            int score = game.Score;
            game.Reveal(p.X, p.Y, TimeSpan.FromSeconds(i));
            Check(game.Combo == Math.Min(5, i + 1), "Combo progression mismatch.");
            Check(game.Score - score == (before - game.SafeRemaining) * 10 * game.Combo,
                "Combo score mismatch.");
        }
        var next = CoveredNumber(game);
        game.Reveal(next.X, next.Y, TimeSpan.FromSeconds(11));
        Check(game.Combo == 5, "Exactly four seconds must keep combo alive.");
        next = CoveredNumber(game);
        game.Reveal(next.X, next.Y, TimeSpan.FromSeconds(15.001));
        Check(game.Combo == 1, "Expired combo did not reset.");
    }

    private static void ClassicScore()
    {
        Game game = Open(GameMode.Classic);
        foreach (var (x, y) in Positions(game).Where(p => !game[p.X, p.Y].IsMine))
            game.Reveal(x, y, TimeSpan.FromSeconds(10));
        int expected = (game.Width * game.Height - game.Difficulty.Mines) * 10 + 1000 + 590 * 2;
        Check(game.Score == expected && game.Combo == 1, $"Expected {expected}, got {game.Score}.");
    }

    private static void Daily()
    {
        var date = new DateOnly(2026, 11, 19);
        Difficulty difficulty = Difficulty.Presets[1];
        int seed = Game.DailySeed(date, difficulty);
        Check(seed == Game.DailySeed(date, difficulty), "Unstable seed.");
        Check(seed != Game.DailySeed(date.AddDays(1), difficulty), "Dates share a seed.");
        Check(seed != Game.DailySeed(date, Difficulty.Presets[0]), "Difficulties share a seed.");
        Game a = Open(GameMode.Daily, seed, difficulty);
        Game b = Open(GameMode.Daily, seed, difficulty);
        Check(Layout(a) == Layout(b) && a.SafeRemaining == b.SafeRemaining, "Daily board changed.");
    }

    private static void Validation()
    {
        Throws<ArgumentOutOfRangeException>(() => new Game(new("Bad", 3, 9, 1), GameMode.Classic, 1));
        Throws<ArgumentOutOfRangeException>(() => new Game(new("Bad", 9, 9, 73), GameMode.Classic, 1));
        Throws<ArgumentOutOfRangeException>(() => new Game(new("Bad", 9, 9, 0), GameMode.Classic, 1));
        Game game = Open();
        Throws<ArgumentOutOfRangeException>(() => game.Reveal(-1, 0, TimeSpan.Zero));
        Throws<ArgumentOutOfRangeException>(() => game.ToggleFlag(9, 0));
        Throws<ArgumentOutOfRangeException>(() => game.Scan(0, 9));
        Throws<ArgumentOutOfRangeException>(() => game.Chord(0, -1, TimeSpan.Zero));
    }

    private static void Persistence() => WithTempPath(path =>
    {
        var store = new RecordStore(path);
        store.Load();
        Check(store.Warning is null && store.Data.Wins.Count == 0, "Missing file must be normal.");
        store.Data.PreferredMode = nameof(GameMode.Daily);
        store.Data.PreferredDifficulty = 2;
        Game game = Open();
        Throws<InvalidOperationException>(() => store.AddWin(game, TimeSpan.Zero, new(2026, 11, 19)));
        foreach (var (x, y) in Positions(game).Where(p => !game[p.X, p.Y].IsMine))
            game.Reveal(x, y, TimeSpan.FromSeconds(12));
        store.AddWin(game, TimeSpan.FromSeconds(12), new(2026, 11, 19));
        var loaded = new RecordStore(path);
        loaded.Load();
        Check(loaded.Warning is null && loaded.Data.PreferredMode == nameof(GameMode.Daily) &&
            loaded.Data.PreferredDifficulty == 2 && loaded.Data.Wins.Count == 1 &&
            loaded.Data.Wins[0].Score == game.Score && loaded.Data.Wins[0].Seconds == 12,
            "Records did not round-trip.");
    });

    private static void CorruptRecords() => WithTempPath(path =>
    {
        const string broken = "{ definitely not json";
        File.WriteAllText(path, broken);
        var store = new RecordStore(path);
        store.Load();
        Check(store.Warning is not null && !store.CanSave, "Corrupt file not reported.");
        store.Save();
        Check(File.ReadAllText(path) == broken, "Corrupt file overwritten.");
    });

    private static void InvalidRecords() => WithTempPath(path =>
    {
        string[] invalid =
        [
            "null",
            """{"PreferredMode":"Bogus"}""",
            """{"PreferredMode":"99"}""",
            """{"PreferredDifficulty":-1}""",
            """{"Wins":null}""",
            """{"Wins":[null]}""",
            """{"Wins":[{"Mode":"Classic","Difficulty":"Rookie","Score":1,"Seconds":-1,"Date":"2026-11-19"}]}"""
        ];
        foreach (string json in invalid)
        {
            File.WriteAllText(path, json);
            var store = new RecordStore(path);
            store.Load();
            Check(store.Warning is not null && !store.CanSave, $"Invalid data accepted: {json}");
        }
    });

    private static void StorageFailure() => WithTempPath(path =>
    {
        File.WriteAllText(path, "blocks a directory");
        var store = new RecordStore(Path.Combine(path, "records.json"));
        store.Save();
        Check(store.Warning is not null, "Save failure not reported.");
    });

    private static Game Open(GameMode mode = GameMode.Steroids, int seed = 42, Difficulty? difficulty = null)
    {
        var game = new Game(difficulty ?? Difficulty.Presets[0], mode, seed);
        game.Reveal(game.Width / 2, game.Height / 2, TimeSpan.Zero);
        return game;
    }

    private static (int X, int Y) CoveredNumber(Game game) => Positions(game).First(p =>
        !game[p.X, p.Y].IsMine && !game[p.X, p.Y].IsRevealed && game[p.X, p.Y].AdjacentMines > 0);

    private static (int X, int Y) FindClue(Game game, Func<Game, int, int, bool> predicate) =>
        Positions(game).First(p => game[p.X, p.Y].IsRevealed &&
            game[p.X, p.Y].AdjacentMines > 0 && predicate(game, p.X, p.Y));

    private static string Layout(Game game) =>
        string.Concat(Positions(game).Select(p => game[p.X, p.Y].IsMine ? "*" :
            game[p.X, p.Y].AdjacentMines.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static IEnumerable<(int X, int Y)> Positions(Game game)
    {
        for (int y = 0; y < game.Height; y++)
        for (int x = 0; x < game.Width; x++)
            yield return (x, y);
    }

    private static void WithTempPath(Action<string> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"minesweeper-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(Path.Combine(directory, "records.json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
