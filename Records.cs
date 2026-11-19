using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinesweeperOnSteroids;

internal sealed record ScoreRecord(string Mode, string Difficulty, int Score, double Seconds, string Date);

internal sealed class PlayerData
{
    public string PreferredMode { get; set; } = nameof(GameMode.Steroids);
    public int PreferredDifficulty { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool AnimationsEnabled { get; set; } = true;
    public List<ScoreRecord> Wins { get; set; } = [];
}

internal sealed class RecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict
    };

    private readonly string path;
    public PlayerData Data { get; private set; } = new();
    public string? Warning { get; private set; }

    public RecordStore(string? path = null)
    {
        this.path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "minesweeperonsteroids", "records.json");
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(path))
                return;
            var data = JsonSerializer.Deserialize<PlayerData>(File.ReadAllText(path), JsonOptions);
            if (data is null || data.Wins is null ||
                !Enum.TryParse<GameMode>(data.PreferredMode, out var mode) || !Enum.IsDefined(mode) ||
                data.PreferredDifficulty < 0 || data.PreferredDifficulty >= Difficulty.Presets.Length ||
                data.Wins.Any(w => w is null || !double.IsFinite(w.Seconds) || w.Seconds < 0 ||
                    w.Score < 0 || !Enum.TryParse<GameMode>(w.Mode, out var m) || !Enum.IsDefined(m) ||
                    !Difficulty.Presets.Any(d => d.Name == w.Difficulty) ||
                    !DateOnly.TryParseExact(w.Date, "yyyy-MM-dd", out _)))
            {
                Warning = "Local records are invalid; using defaults. The original file is untouched.";
                CanSave = false;
                return;
            }
            Data = data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Warning = $"Could not load local records: {ex.Message}";
            CanSave = false;
        }
    }

    public bool CanSave { get; private set; } = true;

    public void AddWin(Game game, TimeSpan elapsed, DateOnly date)
    {
        if (game.State != GameState.Won)
            throw new InvalidOperationException("Only a won game may be recorded.");
        Data.Wins.Add(new(game.Mode.ToString(), game.Difficulty.Name, game.Score,
            Math.Round(elapsed.TotalSeconds, 1), date.ToString("yyyy-MM-dd")));
        Data.Wins = Data.Wins.OrderByDescending(w => w.Score).ThenBy(w => w.Seconds).Take(100).ToList();
        Save();
    }

    public void Save()
    {
        if (!CanSave)
            return;
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(Data, JsonOptions));
            File.Move(temporary, path, overwrite: true);
            Warning = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warning = $"Could not save local records: {ex.Message}";
        }
    }
}
