using System.Diagnostics;

namespace MinesweeperOnSteroids;

internal sealed class MainForm : Form
{
    private readonly RecordStore records;
    private readonly SoundEffects sounds = new();
    private readonly Icon gameIcon = LoadGameIcon();
    private readonly Func<string, bool>? confirmDiscard;
    private readonly Stopwatch clock = new();
    private readonly System.Windows.Forms.Timer ticker = new() { Interval = 100 };
    private readonly MinefieldControl board = new() { Name = "Minefield" };
    private readonly ComboBox mode = new() { Name = "Mode", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox difficulty = new() { Name = "Difficulty", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly GameHud counters = new();
    private readonly MissionPanel fieldPanel = new();
    private readonly Label powers = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Label warning = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 180, 95), AutoEllipsis = true };
    private readonly Button radarButton;
    private readonly Button soundButton;
    private readonly Button motionButton;
    private readonly Button droneButton;
    private readonly Button empButton;
    private readonly Button aegisButton;
    private readonly Button nextSectorButton;
    private readonly ComboBox upgrade = new() { Name = "Upgrade", DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Label overdrive = new() { AutoSize = true, Margin = new(8, 10, 0, 0), ForeColor = NeonTheme.Gold };
    private readonly OverdriveMeter energyMeter = new();
    private readonly FlowLayoutPanel arcadeBar = new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
    private ArcadeRun? run;
    private Game game = null!;
    private DateOnly date;
    private TimeSpan radarUntil;
    private bool recorded;
    private long lastRevealSound;

    internal Game CurrentGame => game;
    internal MinefieldControl Board => board;
    internal ArcadeRun? CurrentRun => run;

    public MainForm(RecordStore? store = null, Func<string, bool>? confirmDiscard = null)
    {
        records = store ?? new RecordStore();
        this.confirmDiscard = confirmDiscard;
        records.Load();
        sounds.Enabled = records.Data.SoundEnabled;
        board.SetAnimations(records.Data.AnimationsEnabled);
        Text = "minesweeperonsteroids";
        Icon = gameIcon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new(1080, 860);
        MinimumSize = new(720, 600);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(16, 23, 33);
        ForeColor = Color.FromArgb(223, 232, 241);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new(22), ColumnCount = 1, RowCount = 10
        };
        foreach (float height in new float[] { 60, 26, 54, 84, 32 })
            layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 52));
        layout.RowStyles.Add(new(SizeType.Absolute, 30));
        layout.RowStyles.Add(new(SizeType.Absolute, 28));
        layout.RowStyles.Add(new(SizeType.Absolute, 0));
        layout.Controls.Add(new GameBrand(gameIcon) { Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "CLASSIC DEDUCTION  /  EXTRA FIREPOWER  /  ZERO MERCY",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(138, 161, 181)
        }, 0, 1);

        mode.Items.AddRange(Enum.GetNames<GameMode>());
        difficulty.Items.AddRange(Difficulty.Presets.Select(d => (object)d.Name).ToArray());
        mode.SelectedIndex = (int)Enum.Parse<GameMode>(records.Data.PreferredMode);
        difficulty.SelectedIndex = records.Data.PreferredDifficulty;
        mode.AccessibleName = "Mode for next game";
        difficulty.AccessibleName = "Difficulty for next game";
        foreach (ComboBox selector in new[] { mode, difficulty })
        {
            selector.FlatStyle = FlatStyle.Flat;
            selector.BackColor = Color.FromArgb(33, 48, 66);
            selector.ForeColor = ForeColor;
        }
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new(0, 7, 0, 0) };
        toolbar.Controls.AddRange([
            mode, difficulty,
            MakeButton("New game", "NewGame", (_, _) => NewGame()),
            MakeButton("Records", "Records", (_, _) => ShowRecords()),
            MakeButton("How to play", "Help", (_, _) => ShowHelp())
        ]);
        layout.Controls.Add(toolbar, 0, 2);
        layout.Controls.Add(counters, 0, 3);
        powers.ForeColor = Color.FromArgb(138, 161, 181);
        layout.Controls.Add(powers, 0, 4);
        fieldPanel.Controls.Add(board);
        layout.Controls.Add(fieldPanel, 0, 5);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new(0, 5, 0, 0) };
        radarButton = MakeButton("Radar [R]", "Radar", (_, _) => Act(board.Selected.X, board.Selected.Y, FieldAction.Radar));
        soundButton = MakeButton("", "Sound", (_, _) => ToggleSound());
        motionButton = MakeButton("", "Motion", (_, _) => ToggleMotion());
        actions.Controls.Add(radarButton);
        actions.Controls.Add(soundButton);
        actions.Controls.Add(motionButton);
        actions.Controls.Add(new Label
        {
            Text = "Left: reveal / chord   Right: flag",
            AutoSize = true, Margin = new(12, 9, 0, 0)
        });
        layout.Controls.Add(actions, 0, 6);
        layout.Controls.Add(status, 0, 7);
        layout.Controls.Add(warning, 0, 8);
        droneButton = MakeButton("Drone [1]", "Drone", (_, _) => Act(board.Selected.X, board.Selected.Y, FieldAction.Drone));
        empButton = MakeButton("EMP [2]", "Emp", (_, _) => Act(board.Selected.X, board.Selected.Y, FieldAction.Emp));
        aegisButton = MakeButton("Aegis [3]", "Aegis", (_, _) => Act(board.Selected.X, board.Selected.Y, FieldAction.Aegis));
        upgrade.Items.AddRange([
            "Reactor / +1 energy per safe tile",
            "Armor / +1 starting shield (max 3)",
            "Efficiency / abilities cost 5 less"
        ]);
        upgrade.SelectedIndex = 0;
        upgrade.SelectedIndexChanged += (_, _) => { if (game is not null) RefreshGame(); };
        upgrade.AccessibleName = "Upgrade for next sector";
        upgrade.BackColor = mode.BackColor;
        upgrade.ForeColor = ForeColor;
        nextSectorButton = MakeButton("Next sector", "NextSector", (_, _) => AdvanceSector());
        arcadeBar.Controls.AddRange([droneButton, empButton, aegisButton, overdrive, energyMeter, upgrade, nextSectorButton]);
        layout.Controls.Add(arcadeBar, 0, 9);
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

    private static Icon LoadGameIcon()
    {
        using Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("MinesweeperOnSteroids.GameIcon")
            ?? throw new InvalidOperationException("The embedded game icon is missing.");
        using var icon = new Icon(stream, new Size(32, 32));
        return (Icon)icon.Clone();
    }

    private static Button MakeButton(string text, string name, EventHandler click)
    {
        var button = new ShinyButton
        {
            Text = text, Name = name, AutoSize = true, Height = 34,
            Padding = new(9, 2, 9, 2), Cursor = Cursors.Hand
        };
        button.Click += click;
        return button;
    }

    private void ToggleSound()
    {
        records.Data.SoundEnabled = !records.Data.SoundEnabled;
        sounds.Enabled = records.Data.SoundEnabled;
        records.Save();
        if (sounds.Enabled)
            sounds.Play(SoundCue.Flag);
        RefreshGame();
        board.Focus();
    }

    private void ToggleMotion()
    {
        records.Data.AnimationsEnabled = !records.Data.AnimationsEnabled;
        board.SetAnimations(records.Data.AnimationsEnabled);
        records.Save();
        RefreshGame();
        board.Focus();
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
        run = selectedMode == GameMode.Arcade ? new ArcadeRun(selectedDifficulty, Random.Shared.Next()) : null;
        game = run?.Game ?? new(selectedDifficulty, selectedMode,
            selectedMode == GameMode.Daily ? Game.DailySeed(date, selectedDifficulty) : Random.Shared.Next());
        PrepareSector();
    }

    private void AdvanceSector()
    {
        if (run is null || !run.CanAdvance)
            return;
        run.Advance((RunUpgrade)upgrade.SelectedIndex);
        game = run.Game;
        PrepareSector();
        board.Focus();
    }

    private void PrepareSector()
    {
        clock.Reset();
        recorded = false;
        radarUntil = TimeSpan.Zero;
        board.SetGame(game);
        sounds.Stop();
        status.Text = run is null ? "Choose your opening. The first reveal and its neighbors are always safe." :
            $"SECTOR {run.Sector}/{ArcadeRun.FinalSector}: {game.Difficulty.Mines} mines. Sweep to charge Overdrive!";
        if (game.Mode == GameMode.Daily)
        {
            clock.Start();
            Act(game.Width / 2, game.Height / 2, FieldAction.Reveal);
        }
        sounds.Play(SoundCue.Deploy);
        RefreshGame();
    }

    private void Act(int x, int y, FieldAction action)
    {
        int previousScore = game.Score;
        int previousShields = game.Shields;
        int previousCombo = game.Combo;
        int previousSafe = game.SafeRemaining;
        GameState previousState = game.State;
        if (action == FieldAction.Reveal && game.State == GameState.Ready && !game[x, y].IsFlagged)
            clock.Start();
        ActionResult result = action switch
        {
            FieldAction.Reveal => game.Reveal(x, y, clock.Elapsed),
            FieldAction.Flag => game.ToggleFlag(x, y),
            FieldAction.Chord => game.Chord(x, y, clock.Elapsed),
            FieldAction.Radar => game.Scan(x, y),
            FieldAction.Drone => UseAbility(OverdriveAbility.Drone, x, y),
            FieldAction.Emp => UseAbility(OverdriveAbility.Emp, x, y),
            FieldAction.Aegis => UseAbility(OverdriveAbility.Aegis, x, y),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        status.Text = result.Message;
        if (run is not null && action is FieldAction.Reveal or FieldAction.Chord)
            run.EarnEnergy(previousSafe - game.SafeRemaining);
        if (action == FieldAction.Radar && result.Changed)
        {
            board.RadarCenter = new(x, y);
            board.RadarRow = null;
            radarUntil = clock.Elapsed + TimeSpan.FromSeconds(4);
        }
        if (action == FieldAction.Emp && result.Changed)
        {
            board.RadarCenter = new(x, y);
            board.RadarRow = y;
            radarUntil = clock.Elapsed + TimeSpan.FromSeconds(4);
        }
        if (game.IsFinished)
        {
            clock.Stop();
            board.RadarCenter = null;
            board.RadarRow = null;
            if (game.State == GameState.Won && !recorded)
            {
                records.AddWin(game, clock.Elapsed, date, run);
                recorded = true;
            }
            if (run is not null)
                status.Text = game.State == GameState.Lost
                    ? $"RUN ENDED / Sector {run.Sector} / {run.TotalScore:N0} points. New game to redeploy."
                    : run.IsComplete ? $"ARCADE CONQUERED! All five sectors cleared / {run.TotalScore:N0} points!"
                    : $"SECTOR {run.Sector} CLEAR / {run.TotalScore:N0} points. Choose an upgrade, then Next sector.";
        }
        if (result.Changed)
        {
            FieldEffect effect = game.State == GameState.Lost ? FieldEffect.Explosion :
                game.State == GameState.Won ? FieldEffect.Victory :
                game.Shields != previousShields ? FieldEffect.Shield :
                action is FieldAction.Radar or FieldAction.Emp ? FieldEffect.Radar :
                action == FieldAction.Flag ? FieldEffect.Flag : FieldEffect.Reveal;
            Point effectOrigin = game.ExplodedCell is { } exploded ? new(exploded.X, exploded.Y) : new(x, y);
            board.AnimateAction(effectOrigin, effect, game.Score - previousScore);
            SoundCue cue = effect switch
            {
                FieldEffect.Explosion => SoundCue.Explosion,
                FieldEffect.Victory => SoundCue.Victory,
                FieldEffect.Shield => SoundCue.Shield,
                FieldEffect.Radar => SoundCue.Radar,
                FieldEffect.Flag => game[x, y].IsFlagged ? SoundCue.Flag : SoundCue.Unflag,
                _ => game.Combo > previousCombo ? SoundCue.Combo : SoundCue.Reveal
            };
            if (cue != SoundCue.Reveal || previousState == GameState.Ready ||
                Stopwatch.GetElapsedTime(lastRevealSound).TotalMilliseconds > 75)
            {
                sounds.Play(cue);
                lastRevealSound = Stopwatch.GetTimestamp();
            }
        }
        RefreshGame();
    }

    private ActionResult UseAbility(OverdriveAbility ability, int x, int y) =>
        run?.Use(ability, x, y, clock.Elapsed) ?? new(false, "Overdrive abilities are available in Arcade mode only.");

    private void RefreshGame()
    {
        if (board.RadarCenter is not null && clock.Elapsed >= radarUntil)
        {
            board.RadarCenter = null;
            board.RadarRow = null;
        }
        counters.UpdateValues(game, clock.Elapsed, run);
        fieldPanel.UpdateValues(game, clock.Elapsed, run);
        int total = game.Width * game.Height - game.Difficulty.Mines;
        powers.Text = $"{game.Mode} / {game.Difficulty.Name}   |   SHIELD {game.Shields}   RADAR {game.Scans}" +
                      $"   |   CLEARED {(total - game.SafeRemaining) * 100 / total}%" +
                      (game.Mode == GameMode.Daily ? $"   |   {date:yyyy-MM-dd}" : "");
        if (run is not null)
            powers.Text = $"ARCADE {run.Sector}/{ArcadeRun.FinalSector} / {game.Difficulty.Name}   |   RUN {run.TotalScore:N0}" +
                $"   |   SHIELD {game.Shields}   RADAR {game.Scans}   |   ENERGY {run.Energy}/100";
        arcadeBar.Visible = run is not null;
        if (arcadeBar.Parent is TableLayoutPanel layout)
            layout.RowStyles[9].Height = run is null ? 0 : 50 * DeviceDpi / 96f;
        bool advancing = run?.CanAdvance == true;
        nextSectorButton.Enabled = advancing && run!.CanUpgrade((RunUpgrade)upgrade.SelectedIndex);
        upgrade.Visible = nextSectorButton.Visible = advancing;
        droneButton.Visible = empButton.Visible = aegisButton.Visible = !advancing;
        overdrive.Text = run is null ? "" : advancing
            ? run.CanUpgrade((RunUpgrade)upgrade.SelectedIndex) ? "UPGRADE:" : "MAX LEVEL"
            : $"OVERDRIVE {run.Energy}/100";
        energyMeter.Visible = !advancing;
        energyMeter.UpdateValue(run?.Energy ?? 0);
        if (run is not null)
        {
            droneButton.Text = $"Drone [1] / {run.Cost(OverdriveAbility.Drone)}";
            empButton.Text = $"EMP [2] / {run.Cost(OverdriveAbility.Emp)}";
            aegisButton.Text = $"Aegis [3] / {run.Cost(OverdriveAbility.Aegis)}";
            droneButton.Enabled = game.State == GameState.Playing && run.Energy >= run.Cost(OverdriveAbility.Drone);
            empButton.Enabled = game.State == GameState.Playing && run.Energy >= run.Cost(OverdriveAbility.Emp);
            aegisButton.Enabled = game.State == GameState.Playing && game.Shields < 3 && run.Energy >= run.Cost(OverdriveAbility.Aegis);
        }
        radarButton.Enabled = game.State == GameState.Playing && game.Scans > 0;
        soundButton.Text = sounds.Enabled ? "Sound: ON" : "Sound: OFF";
        motionButton.Text = board.AnimationsEnabled ? "Motion: FULL" : "Motion: LOW";
        warning.Text = records.Warning is { } message ? $"Storage warning: {message}" :
            sounds.Warning is { } audioWarning && sounds.Enabled ? audioWarning :
            "Mode and difficulty selections apply when you choose New game.";
        status.ForeColor = game.IsFinished ? Color.FromArgb(255, 201, 88) : ForeColor;
        Text = $"minesweeperonsteroids - {game.Mode} / {game.Difficulty.Name} - {game.State}";
    }

    private bool DiscardAllowed(string prompt)
    {
        if (game.State != GameState.Playing && !(run is not null && !run.IsComplete && game.State == GameState.Won))
            return true;
        return PauseWhile(() => confirmDiscard?.Invoke(prompt) ??
            MessageBox.Show(this, prompt, "Minefield in progress",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
    }

    private T PauseWhile<T>(Func<T> action)
    {
        bool running = clock.IsRunning;
        clock.Stop();
        board.PauseEffects();
        sounds.Stop();
        try { return action(); }
        finally
        {
            if (running)
                clock.Start();
            board.ResumeEffects();
        }
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
        Arcade: conquer five increasingly dense sectors. Safe reveals earn energy
        (2 per tile, max 20 per action; cap 100). Flags and abilities never recharge it.
        1: Drone (35) opens a safe tile near selection. 2: EMP (25) scans the row.
        3: Aegis (60) adds a shield (max 3). Reveal your opening before abilities.
        Clear a sector, select Reactor, Armor or Efficiency, then Next sector.
        Energy and upgrades carry forward; shields/radar reset each sector.

        Keyboard (minefield focused): Arrows/WASD move; Space reveal; F flag;
        Enter chord; R radar. F2 new game; F1 help. Tab moves between controls.
        Help, records, and confirmation dialogs pause the timer.
        Sound toggles all synthesized effects. Motion: LOW disables moving effects.
        Both preferences are saved. Visual clues remain available when muted.
        Scores and preferences stay on this PC. No account or internet required.
        """, "Field manual - game paused", MessageBoxButtons.OK, MessageBoxIcon.Information));

    private void ShowRecords()
    {
        string text = records.Data.Wins.Count == 0 ? "No victories yet. The minefield is waiting." :
            string.Join(Environment.NewLine, records.Data.Wins.Take(15).Select(w =>
                $"{w.Score:N0} pts / {w.Seconds:0.0}s / {w.Mode}" +
                (w.Sector > 0 ? $" sector {w.Sector}/5 (run score, sector time)" : "") +
                $" / {w.Difficulty} / {w.Date}"));
        PauseWhile(() => MessageBox.Show(this, text + "\n\nTop 15 wins shown; best 100 retained.\n" +
            "Arcade records show cumulative run points and individual sector times.\n" +
            "Arcade, assisted and Classic scores are not equivalent.", "Hall of survivors - game paused",
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
        {
            ticker.Dispose();
            sounds.Dispose();
        }
        base.Dispose(disposing);
        if (disposing)
            gameIcon.Dispose();
    }
}
