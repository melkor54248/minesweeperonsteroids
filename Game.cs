using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MinesweeperOnSteroids;

internal enum GameMode { Classic, Steroids, Daily, Arcade }
internal enum GameState { Ready, Playing, Won, Lost }

internal sealed record Difficulty(string Name, int Width, int Height, int Mines)
{
    public static readonly Difficulty[] Presets =
    [
        new("Rookie", 9, 9, 10),
        new("Veteran", 16, 16, 40),
        new("Maniac", 30, 16, 99)
    ];
}

internal sealed class Cell
{
    public bool IsMine { get; internal set; }
    public bool IsRevealed { get; internal set; }
    public bool IsFlagged { get; internal set; }
    public int AdjacentMines { get; internal set; }
}

internal readonly record struct ActionResult(bool Changed, string Message);

internal sealed class Game
{
    private readonly Cell[,] cells;
    private readonly int seed;
    private TimeSpan? lastScoringMove;
    private bool generated;

    public Difficulty Difficulty { get; }
    public GameMode Mode { get; }
    public GameState State { get; private set; } = GameState.Ready;
    public int Width => Difficulty.Width;
    public int Height => Difficulty.Height;
    public int FlagCount { get; private set; }
    public int SafeRemaining { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; } = 1;
    public int Shields { get; private set; }
    public int Scans { get; private set; }
    public (int X, int Y)? ExplodedCell { get; private set; }
    public bool IsFinished => State is GameState.Won or GameState.Lost;

    public Game(Difficulty difficulty, GameMode mode, int seed)
    {
        ArgumentNullException.ThrowIfNull(difficulty);
        if (difficulty.Width is < 4 or > 100 || difficulty.Height is < 4 or > 100 ||
            difficulty.Mines < 1 || difficulty.Mines > difficulty.Width * difficulty.Height - 9)
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty),
                "Boards must be 4-100 cells per side with room for a mine-free 3x3 opening.");
        }

        Difficulty = difficulty;
        Mode = mode;
        this.seed = seed;
        cells = new Cell[Width, Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            cells[x, y] = new Cell();

        SafeRemaining = Width * Height - difficulty.Mines;
        Shields = mode == GameMode.Classic ? 0 : 1;
        Scans = mode == GameMode.Classic ? 0 : 3;
    }

    public Cell this[int x, int y] => cells[x, y];

    internal void AddArcadeShield()
    {
        if (Mode != GameMode.Arcade || IsFinished || Shields >= 3)
            throw new InvalidOperationException("Arcade shields require an active field with fewer than three shields.");
        Shields++;
    }

    public static int DailySeed(DateOnly date, Difficulty difficulty)
    {
        string key = $"minesweeperonsteroids:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}:" +
                     $"{difficulty.Width}x{difficulty.Height}:{difficulty.Mines}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(hash);
    }

    public IEnumerable<(int X, int Y)> Neighbors(int x, int y)
    {
        for (int ny = Math.Max(0, y - 1); ny <= Math.Min(Height - 1, y + 1); ny++)
        for (int nx = Math.Max(0, x - 1); nx <= Math.Min(Width - 1, x + 1); nx++)
            if (nx != x || ny != y)
                yield return (nx, ny);
    }

    public ActionResult ToggleFlag(int x, int y)
    {
        ValidatePosition(x, y);
        if (IsFinished)
            return new(false, "Round over. Choose New game for another round.");
        Cell cell = cells[x, y];
        if (cell.IsRevealed)
            return new(false, "An open cell cannot be flagged.");
        cell.IsFlagged = !cell.IsFlagged;
        FlagCount += cell.IsFlagged ? 1 : -1;
        return new(true, cell.IsFlagged ? "Flag placed. F again to remove." : "Flag removed.");
    }

    public ActionResult Reveal(int x, int y, TimeSpan elapsed)
    {
        ValidatePosition(x, y);
        if (IsFinished)
            return new(false, "Round over. Choose New game for another round.");
        if (cells[x, y].IsFlagged)
            return new(false, "Flagged cell: remove its flag before revealing.");
        if (cells[x, y].IsRevealed)
            return new(false, "Already open. Click a numbered cell to chord its neighbors.");
        if (!generated)
            Generate(x, y);
        return RevealTargets([(x, y)], elapsed);
    }

    public ActionResult Chord(int x, int y, TimeSpan elapsed)
    {
        ValidatePosition(x, y);
        if (IsFinished)
            return new(false, "Round over. Choose New game for another round.");
        Cell cell = cells[x, y];
        if (!cell.IsRevealed || cell.IsMine || cell.AdjacentMines == 0)
            return new(false, "Chord an open numbered cell after flagging its neighboring mines.");

        var neighbors = Neighbors(x, y).ToArray();
        int flags = neighbors.Count(p => cells[p.X, p.Y].IsFlagged);
        if (flags != cell.AdjacentMines)
            return new(false, $"Needs {cell.AdjacentMines} adjacent flags; currently {flags}.");

        return RevealTargets(neighbors.Where(p => !cells[p.X, p.Y].IsFlagged &&
            !cells[p.X, p.Y].IsRevealed), elapsed);
    }

    public ActionResult Scan(int x, int y)
    {
        ValidatePosition(x, y);
        if (IsFinished)
            return new(false, "Round over. Choose New game for another round.");
        if (!generated)
            return new(false, "Reveal your opening cell before using radar.");
        if (Scans == 0)
            return new(false, "No radar charges. Trust the numbers!");
        Scans--;
        return new(true, "RADAR: the 3x3 area glows for 4 seconds. ! = mine, - = safe.");
    }

    private void Generate(int safeX, int safeY)
    {
        var candidates = new List<(int X, int Y)>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            if (Math.Abs(x - safeX) > 1 || Math.Abs(y - safeY) > 1)
                candidates.Add((x, y));

        var random = new Random(seed);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var (x, y) in candidates.Take(Difficulty.Mines))
            cells[x, y].IsMine = true;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            cells[x, y].AdjacentMines = Neighbors(x, y).Count(p => cells[p.X, p.Y].IsMine);

        generated = true;
        State = GameState.Playing;
    }

    private ActionResult RevealTargets(IEnumerable<(int X, int Y)> targets, TimeSpan elapsed)
    {
        int before = SafeRemaining;
        bool shieldUsed = false;
        foreach (var (x, y) in targets)
        {
            Cell cell = cells[x, y];
            if (cell.IsRevealed || cell.IsFlagged)
                continue;
            if (cell.IsMine)
            {
                if (Shields > 0)
                {
                    Shields--;
                    shieldUsed = true;
                    // The mine stays in place, so every existing clue remains true.
                    cell.IsFlagged = true;
                    FlagCount++;
                }
                else
                {
                    cell.IsRevealed = true;
                    ExplodedCell = (x, y);
                    State = GameState.Lost;
                    break;
                }
            }
            else
            {
                FloodReveal(x, y);
            }
        }

        int revealed = before - SafeRemaining;
        if (revealed > 0)
        {
            Combo = Mode != GameMode.Classic && lastScoringMove is { } last &&
                    elapsed >= last && elapsed - last <= TimeSpan.FromSeconds(4)
                ? Math.Min(5, Combo + 1) : 1;
            Score += revealed * 10 * Combo;
            lastScoringMove = elapsed;
        }

        if (State == GameState.Lost)
            return new(true, "BOOM! The minefield wins. Choose New game for revenge.");
        if (SafeRemaining == 0)
        {
            State = GameState.Won;
            Score += 1000 + (int)Math.Max(0, 600 - Math.Floor(elapsed.TotalSeconds)) * 2;
            return new(true, "MINEFIELD CONQUERED! All safe cells cleared. Choose New game to play again.");
        }
        if (shieldUsed)
            return new(true, "SHIELD SHATTERED! Mine blocked and auto-flagged. Next hit is fatal.");
        return new(revealed > 0, revealed > 0
            ? $"Cleared {revealed} safe cell{(revealed == 1 ? "" : "s")}. Keep sweeping!"
            : "No covered, unflagged neighbors to reveal.");
    }

    private void FloodReveal(int x, int y)
    {
        var pending = new Queue<(int X, int Y)>();
        RevealSafe(x, y);
        while (pending.TryDequeue(out var position))
        {
            if (cells[position.X, position.Y].AdjacentMines != 0)
                continue;
            foreach (var (nx, ny) in Neighbors(position.X, position.Y))
                if (!cells[nx, ny].IsRevealed && !cells[nx, ny].IsFlagged && !cells[nx, ny].IsMine)
                    RevealSafe(nx, ny);
        }

        void RevealSafe(int nx, int ny)
        {
            cells[nx, ny].IsRevealed = true;
            SafeRemaining--;
            pending.Enqueue((nx, ny));
        }
    }

    private void ValidatePosition(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Cell coordinates must be inside the board.");
    }
}
