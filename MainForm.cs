using System.Diagnostics;

namespace MinesweeperOnSteroids;

internal sealed class MainForm : Form
{
    private readonly RecordStore records;
    private readonly Func<string, bool>? confirmDiscard;
    private readonly Stopwatch clock = new();
    private readonly System.Windows.Forms.Timer ticker = new() { Interval = 100 };
    private readonly MinefieldControl board = new() { Name = "Minefield" };
    private readonly ComboBox mode = new() { Name = "Mode", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox difficulty = new() { Name = "Difficulty", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Label counters = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label powers = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Label warning = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 180, 95), AutoEllipsis = true };
    private readonly Button radarButton;
    private Game game = null!;
    private DateOnly date;
    private TimeSpan radarUntil;
    private bool recorded;

    internal Game CurrentGame => game;
    internal MinefieldControl Board => board;

    public MainForm(RecordStore? store = null, Func<string, bool>? confirmDiscard = null)
    {
        records = store ?? new RecordStore();
        this.confirmDiscard = confirmDiscard;
        records.Load();
        Text = "minesweeperonsteroids";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new(1080, 860);
        MinimumSize = new(720, 600);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(16, 23, 33);
        ForeColor = Color.FromArgb(223, 232, 241);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new(22), ColumnCount = 1, RowCount = 9
        };
        foreach (float height in new float[] { 48, 28, 54, 36, 30 })
            layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 48));
        layout.RowStyles.Add(new(SizeType.Absolute, 30));
        layout.RowStyles.Add(new(SizeType.Absolute, 28));
        var title = new Label
        {
            Text = "MINESWEEPER / ON STEROIDS", Dock = DockStyle.Fill,
            Font = new("Segoe UI", 21, FontStyle.Bold), ForeColor = Color.FromArgb(71, 223, 213),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "CLASSIC DEDUCTION. EXTRA FIREPOWER. ZERO MERCY.",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(138, 161, 181)
        }, 0, 1);

        mode.Items.AddRange(Enum.GetNames<GameMode>());
        difficulty.Items.AddRange(Difficulty.Presets.Select(d => (object)d.Name).ToArray());
        mode.SelectedIndex = (int)Enum.Parse<GameMode>(records.Data.PreferredMode);
        difficulty.SelectedIndex = records.Data.PreferredDifficulty;
        mode.AccessibleName = "Mode for next game";
        difficulty.AccessibleName = "Difficulty for next game";
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new(0, 7, 0, 0) };
        toolbar.Controls.AddRange([
            mode, difficulty,
            MakeButton("New game", "NewGame", (_, _) => NewGame()),
            MakeButton("Records", "Records", (_, _) => ShowRecords()),
            MakeButton("How to play", "Help", (_, _) => ShowHelp())
        ]);
        layout.Controls.Add(toolbar, 0, 2);
        counters.Font = new("Consolas", 14, FontStyle.Bold);
        counters.ForeColor = Color.FromArgb(255, 201, 88);
        layout.Controls.Add(counters, 0, 3);
        powers.ForeColor = Color.FromArgb(138, 161, 181);
        layout.Controls.Add(powers, 0, 4);
        var fieldPanel = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(12, 18, 27),
            BorderStyle = BorderStyle.FixedSingle
        };
        fieldPanel.Controls.Add(board);
        layout.Controls.Add(fieldPanel, 0, 5);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new(0, 5, 0, 0) };
        radarButton = MakeButton("Radar [R]", "Radar", (_, _) => Act(board.Selected.X, board.Selected.Y, FieldAction.Radar));
        actions.Controls.Add(radarButton);
        actions.Controls.Add(new Label
        {
            Text = "Left click: reveal / chord   |   Right click: flag",
            AutoSize = true, Margin = new(12, 9, 0, 0)
        });
        layout.Controls.Add(actions, 0, 6);
        layout.Controls.Add(status, 0, 7);
        layout.Controls.Add(warning, 0, 8);
        Controls.Add(layout);
        board.CellAction += Act;
        ticker.Tick += (_, _) => RefreshGame();
        FormClosing += (_, e) =>
        {
            if (!DiscardAllowed("Exit the game? Your current round will be lost."))
                e.Cancel = true;
        };
        Shown += (_, _) => board.Focus();
        StartRound();
        ticker.Start();
    }

    private static Button MakeButton(string text, string name, EventHandler click)
    {
        var button = new Button
        {
            Text = text, Name = name, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 57, 73), ForeColor = Color.WhiteSmoke,
            Padding = new(9, 2, 9, 2), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(65, 98, 116);
        button.Click += click;
        return button;
    }

    private void NewGame()
    {
        if (DiscardAllowed("Start a new game? Your current round will be lost."))
            StartRound();
        board.Focus();
    }

    private void StartRound()
    {
        records.Data.PreferredMode = ((GameMode)mode.SelectedIndex).ToString();
        records.Data.PreferredDifficulty = difficulty.SelectedIndex;
        records.Save();
        var selectedMode = (GameMode)mode.SelectedIndex;
        Difficulty selectedDifficulty = Difficulty.Presets[difficulty.SelectedIndex];
        date = DateOnly.FromDateTime(DateTime.Now);
        game = new(selectedDifficulty, selectedMode,
            selectedMode == GameMode.Daily ? Game.DailySeed(date, selectedDifficulty) : Random.Shared.Next());
        clock.Reset();
        recorded = false;
        radarUntil = TimeSpan.Zero;
        board.SetGame(game);
        status.Text = "Choose your opening. The first reveal and its neighbors are always safe.";
        if (selectedMode == GameMode.Daily)
        {
            clock.Start();
            status.Text = game.Reveal(game.Width / 2, game.Height / 2, clock.Elapsed).Message;
        }
        RefreshGame();
    }

    private void Act(int x, int y, FieldAction action)
    {
        if (action == FieldAction.Reveal && game.State == GameState.Ready && !game[x, y].IsFlagged)
            clock.Start();
        ActionResult result = action switch
        {
            FieldAction.Reveal => game.Reveal(x, y, clock.Elapsed),
            FieldAction.Flag => game.ToggleFlag(x, y),
            FieldAction.Chord => game.Chord(x, y, clock.Elapsed),
            FieldAction.Radar => game.Scan(x, y),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        status.Text = result.Message;
        if (action == FieldAction.Radar && result.Changed)
        {
            board.RadarCenter = new(x, y);
            radarUntil = clock.Elapsed + TimeSpan.FromSeconds(4);
        }
        if (game.IsFinished)
        {
            clock.Stop();
            board.RadarCenter = null;
            if (game.State == GameState.Won && !recorded)
            {
                records.AddWin(game, clock.Elapsed, date);
                recorded = true;
            }
        }
        RefreshGame();
    }

    private void RefreshGame()
    {
        if (board.RadarCenter is not null && clock.Elapsed >= radarUntil)
            board.RadarCenter = null;
        counters.Text = $"MINES {game.Difficulty.Mines - game.FlagCount,3}    SAFE {game.SafeRemaining,3}" +
                        $"    TIME {(int)clock.Elapsed.TotalMinutes:00}:{clock.Elapsed.Seconds:00}    SCORE {game.Score:N0}";
        powers.Text = $"{game.Mode} / {game.Difficulty.Name}   |   SHIELD {game.Shields}   RADAR {game.Scans}" +
                      $"   COMBO {(game.Mode == GameMode.Classic ? "OFF" : $"{game.Combo}x")}" +
                      (game.Mode == GameMode.Daily ? $"   |   {date:yyyy-MM-dd}" : "");
        radarButton.Enabled = game.State == GameState.Playing && game.Scans > 0;
        warning.Text = records.Warning is { } message ? $"Storage warning: {message}" :
            "Mode and difficulty selections apply when you choose New game.";
        status.ForeColor = game.IsFinished ? Color.FromArgb(255, 201, 88) : ForeColor;
        Text = $"minesweeperonsteroids - {game.Mode} / {game.Difficulty.Name} - {game.State}";
        board.Invalidate();
    }

    private bool DiscardAllowed(string prompt)
    {
        if (game.State != GameState.Playing)
            return true;
        return PauseWhile(() => confirmDiscard?.Invoke(prompt) ??
            MessageBox.Show(this, prompt, "Minefield in progress",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
    }

    private T PauseWhile<T>(Func<T> action)
    {
        bool running = clock.IsRunning;
        clock.Stop();
        try { return action(); }
        finally { if (running) clock.Start(); }
    }

    private void ShowHelp() => PauseWhile(() => MessageBox.Show(this, """
        Clear every safe cell to win. Numbers count mines in the 8 surrounding cells.
        Your first reveal and all neighboring cells are mine-free. Mines NEVER move.

        Left click: reveal. Right click (or Shift+click): flag.
        Click an open number (or middle click) to chord: opens unflagged neighbors
        when the adjacent flag count matches. Wrong flags can make a chord lethal!

        Steroids / Daily: one shield blocks a mine and auto-flags it without removing it.
        Three radar scans show a 3x3 area for 4 seconds: ! mine / - safe.
        Select a target with a right click or the arrow keys, then press Radar / R.

        Safe reveal actions within 4 seconds chain up to 5x.
        Score: 10 per safe cell x combo. Classic is always 1x.
        Victory: +1000 plus 2 per second under 10 minutes.
        Daily: same local date + difficulty = same board and fixed center opening.

        Keyboard (minefield focused): Arrows/WASD move; Space reveal; F flag;
        Enter chord; R radar. F2 new game; F1 help. Tab moves between controls.
        Help, records, and confirmation dialogs pause the timer.
        Scores and preferences stay on this PC. No account or internet required.
        """, "Field manual - game paused", MessageBoxButtons.OK, MessageBoxIcon.Information));

    private void ShowRecords()
    {
        string text = records.Data.Wins.Count == 0 ? "No victories yet. The minefield is waiting." :
            string.Join(Environment.NewLine, records.Data.Wins.Take(15).Select(w =>
                $"{w.Score:N0} pts / {w.Seconds:0.0}s / {w.Mode} / {w.Difficulty} / {w.Date}"));
        PauseWhile(() => MessageBox.Show(this, text + "\n\nTop 15 wins shown; best 100 retained.\n" +
            "Assisted and Classic scores are not equivalent.", "Hall of survivors - game paused",
            MessageBoxButtons.OK, MessageBoxIcon.Information));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F2)
        {
            NewGame();
            return true;
        }
        if (keyData == Keys.F1)
        {
            ShowHelp();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ticker.Dispose();
        base.Dispose(disposing);
    }
}
