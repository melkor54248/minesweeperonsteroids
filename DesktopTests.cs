using System.Drawing.Imaging;
using System.Reflection;

namespace MinesweeperOnSteroids;

internal static class DesktopTests
{
    public static int Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var report = File.CreateText(Path.Combine(outputDirectory, "desktop-tests.txt"));
        string recordsPath = Path.Combine(outputDirectory, $"test-records-{Guid.NewGuid():N}.json");
        try
        {
            bool allowDiscard = false;
            var testStore = new RecordStore(recordsPath);
            testStore.Data.SoundEnabled = false;
            testStore.Save();
            using var form = new MainForm(testStore, _ => allowDiscard);
            form.Show();
            Application.DoEvents();
            Check(form.Visible && form.Board.Visible && form.CurrentGame.State == GameState.Ready,
                "Native window opens without a console.");
            using (var icon = (form.Icon ?? throw new InvalidOperationException("No window icon.")).ToBitmap())
            {
                Color center = icon.GetPixel(icon.Width / 2, icon.Height / 2);
                Check(center.R > 180 && center.G > 130 && center.B < 160,
                    "Window displays the original gold-lightning game icon.");
            }
            Point selected = form.Board.Selected;
            PressKey(form.Board, Keys.Right);
            Check(form.Board.Selected == new Point(selected.X + 1, selected.Y),
                "Arrow keys move the selected cell.");
            PressKey(form.Board, Keys.F);
            Check(form.CurrentGame[form.Board.Selected.X, form.Board.Selected.Y].IsFlagged,
                "F key flags the selected cell.");
            Check(form.Board.ActiveAnimationCount > 0 && form.Board.LastEffect == FieldEffect.Flag,
                "Flag action starts a pop animation and spark particles.");
            PressKey(form.Board, Keys.F);
            PressKey(form.Board, Keys.Left);
            var mode = Find<ComboBox>(form, "Mode");
            var difficulty = Find<ComboBox>(form, "Difficulty");
            var newGame = Find<Button>(form, "NewGame");
            var radar = Find<Button>(form, "Radar");
            var sound = Find<Button>(form, "Sound");
            var motion = Find<Button>(form, "Motion");
            Check(sound.Text == "Sound: OFF", "Mute preference is reflected in the toolbar.");
            sound.PerformClick();
            Check(sound.Text == "Sound: ON", "Sound toggle enables effects.");
            sound.PerformClick();
            motion.PerformClick();
            Check(!form.Board.AnimationsEnabled && form.Board.ActiveAnimationCount == 0,
                "Reduced motion clears running effects immediately.");
            PressKey(form.Board, Keys.F);
            Check(form.Board.ActiveAnimationCount == 0, "Reduced motion keeps flag actions instantaneous.");
            PressKey(form.Board, Keys.F);
            var preferences = new RecordStore(recordsPath);
            preferences.Load();
            Check(!preferences.Data.SoundEnabled && !preferences.Data.AnimationsEnabled,
                "Sound and reduced-motion choices are saved.");
            motion.PerformClick();
            Check(form.Board.AnimationsEnabled, "Full motion can be restored without restarting.");
            Check(!radar.Enabled, "Radar disabled before the opening.");
            Click(form.Board, 4, 4, MouseButtons.Right);
            Check(form.CurrentGame[4, 4].IsFlagged && form.CurrentGame.State == GameState.Ready,
                "Right mouse button flags without generating mines.");
            Click(form.Board, 4, 4, MouseButtons.Left);
            Check(!form.CurrentGame[4, 4].IsRevealed, "Left click respects flags.");
            Click(form.Board, 4, 4, MouseButtons.Right);
            Click(form.Board, 4, 4, MouseButtons.Left);
            Check(form.CurrentGame[4, 4].IsRevealed && form.CurrentGame[4, 4].AdjacentMines == 0,
                "Left mouse button reveals a safe opening.");
            Check(form.Board.ActiveAnimationCount > 0 && form.Board.LastEffect == FieldEffect.Reveal,
                "Reveal starts a staggered wave and score popup.");
            int safeDuringAnimation = form.CurrentGame.SafeRemaining;
            string firstFrame = FrameSignature(form.Board);
            Pump(120);
            string secondFrame = FrameSignature(form.Board);
            Check(firstFrame != secondFrame && form.CurrentGame.SafeRemaining == safeDuringAnimation,
                "Animation visibly advances without altering the game state.");
            form.Board.PauseEffects();
            int pausedFrame = form.Board.AnimationFrameCount;
            Pump(120);
            Check(form.Board.AnimationFrameCount == pausedFrame, "Paused effects do not advance frames.");
            form.Board.ResumeEffects();
            Pump(80);
            Check(form.Board.AnimationFrameCount > pausedFrame, "Effects resume after a pause.");
            Check(radar.Enabled, "Radar enabled during Steroids round.");
            radar.PerformClick();
            Check(form.CurrentGame.Scans == 2 && form.Board.RadarCenter is not null,
                "Radar button consumes a charge and displays its overlay.");
            Check(form.Board.LastEffect == FieldEffect.Radar, "Radar starts its expanding scan ring.");
            DateTime expiry = DateTime.UtcNow.AddSeconds(4.3);
            while (DateTime.UtcNow < expiry)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            Check(form.Board.RadarCenter is null, "Radar overlay expires after four seconds.");
            Pump(100);
            Check(form.Board.ActiveAnimationCount == 0, "Transient effects finish and release their state.");
            Game previous = form.CurrentGame;
            newGame.PerformClick();
            Check(ReferenceEquals(previous, form.CurrentGame), "Cancelled restart preserves the round.");
            form.Close();
            Check(form.Visible, "Cancelled exit preserves the window.");
            allowDiscard = true;
            newGame.PerformClick();
            Check(!ReferenceEquals(previous, form.CurrentGame) && form.CurrentGame.State == GameState.Ready,
                "Confirmed restart creates a fresh round.");
            Check(form.Board.ActiveAnimationCount == 0 && form.Board.RadarCenter is null &&
                form.Board.LastEffect is null, "New round resets all visual effects and radar.");

            mode.SelectedIndex = (int)GameMode.Classic;
            difficulty.SelectedIndex = 2;
            newGame.PerformClick();
            Check(form.CurrentGame.Mode == GameMode.Classic && form.CurrentGame.Width == 30,
                "Mode and difficulty selectors apply to New game.");
            Click(form.Board, 15, 8, MouseButtons.Left);
            Check(!radar.Enabled && form.CurrentGame.Shields == 0, "Classic has no power-ups.");
            for (int i = 0; i < 30; i++)
                PressKey(form.Board, Keys.Right);
            Check(form.Board.Selected.X == 29 && form.Board.Parent is Panel horizontal &&
                horizontal.AutoScrollPosition.X < 0, "Keyboard selection scrolls a wide board into view.");
            PressKey(form.Board, Keys.Left);
            Check(form.Board.Selected.X == 28, "Selection remains responsive after scrolling.");
            var clue = Positions(form.CurrentGame).FirstOrDefault(p =>
                form.CurrentGame[p.X, p.Y].IsRevealed && form.CurrentGame[p.X, p.Y].AdjacentMines > 0 &&
                form.CurrentGame.Neighbors(p.X, p.Y).Any(n =>
                    !form.CurrentGame[n.X, n.Y].IsMine && !form.CurrentGame[n.X, n.Y].IsRevealed));
            Check(form.CurrentGame[clue.X, clue.Y].AdjacentMines > 0, "Chord fixture is available.");
            foreach (var mine in form.CurrentGame.Neighbors(clue.X, clue.Y).Where(p => form.CurrentGame[p.X, p.Y].IsMine))
                Click(form.Board, mine.X, mine.Y, MouseButtons.Right);
            int safeBefore = form.CurrentGame.SafeRemaining;
            Click(form.Board, clue.X, clue.Y, MouseButtons.Left);
            Check(form.CurrentGame.SafeRemaining < safeBefore && form.CurrentGame.State != GameState.Lost,
                "Clicking an open clue chords correctly.");
            var fatal = Positions(form.CurrentGame).First(p => form.CurrentGame[p.X, p.Y].IsMine &&
                !form.CurrentGame[p.X, p.Y].IsFlagged);
            Click(form.Board, fatal.X, fatal.Y, MouseButtons.Left);
            Check(form.CurrentGame.State == GameState.Lost, "Mine click ends Classic round.");
            Check(form.Board.LastEffect == FieldEffect.Explosion && form.Board.ActiveAnimationCount > 0,
                "Fatal hit starts explosion particles and shockwave.");
            int finalScore = form.CurrentGame.Score;
            Click(form.Board, 0, 0, MouseButtons.Right);
            Check(form.CurrentGame.Score == finalScore && form.CurrentGame.State == GameState.Lost,
                "Game-over clicks cannot change the result.");

            mode.SelectedIndex = (int)GameMode.Daily;
            difficulty.SelectedIndex = 1;
            newGame.PerformClick();
            string layout = MineLayout(form.CurrentGame);
            newGame.PerformClick();
            Check(layout == MineLayout(form.CurrentGame) && form.CurrentGame[8, 8].IsRevealed,
                "Daily restart has identical mines and fixed opening.");
            PressKey(form.Board, Keys.R);
            Check(form.CurrentGame.Scans == 2, "R key activates radar.");
            foreach (var p in Positions(form.CurrentGame).Where(p => !form.CurrentGame[p.X, p.Y].IsMine))
                if (!form.CurrentGame[p.X, p.Y].IsRevealed)
                    Click(form.Board, p.X, p.Y, MouseButtons.Left);
            Check(form.CurrentGame.State == GameState.Won, "Mouse reveals can win a round.");
            Check(form.Board.LastEffect == FieldEffect.Victory && form.Board.ActiveAnimationCount > 0,
                "Victory starts confetti and a persistent result banner.");
            Pump(140);
            SavePreview(form, outputDirectory, "victory-preview.png");
            var saved = new RecordStore(recordsPath);
            saved.Load();
            Check(saved.Data.Wins.Count == 1, "Victory is recorded exactly once.");
            Click(form.Board, 0, 0, MouseButtons.Left);
            saved = new RecordStore(recordsPath);
            saved.Load();
            Check(saved.Data.Wins.Count == 1, "Post-victory clicks do not duplicate records.");

            mode.SelectedIndex = (int)GameMode.Steroids;
            newGame.PerformClick();
            Click(form.Board, 8, 8, MouseButtons.Left);
            var mines = Positions(form.CurrentGame).Where(p => form.CurrentGame[p.X, p.Y].IsMine)
                .OrderBy(p => Math.Abs(p.X - 8) + Math.Abs(p.Y - 8)).Take(2).ToArray();
            Click(form.Board, mines[0].X, mines[0].Y, MouseButtons.Left);
            Check(form.CurrentGame.Shields == 0 && form.CurrentGame[mines[0].X, mines[0].Y].IsFlagged &&
                form.CurrentGame.State == GameState.Playing, "Mouse mine hit uses shield and auto-flags.");
            Check(form.Board.LastEffect == FieldEffect.Shield && form.Board.ActiveAnimationCount > 0,
                "Shield hit starts a blue energy burst.");
            form.ClientSize = new(720, 600);
            Application.DoEvents();
            Check(form.Board.Parent is Panel panel && panel.VerticalScroll.Visible,
                "Large board scrolls in a smaller window.");
            form.ClientSize = new(1080, 1010);
            Application.DoEvents();
            Pump(110);
            SavePreview(form, outputDirectory, "desktop-preview.png");
            PressKey(form.Board, Keys.R);
            Pump(130);
            SavePreview(form, outputDirectory, "radar-preview.png");
            Click(form.Board, mines[1].X, mines[1].Y, MouseButtons.Left);
            Check(form.CurrentGame.State == GameState.Lost, "Second mine hit loses Steroids round.");
            Pump(130);
            SavePreview(form, outputDirectory, "explosion-preview.png");
            Pump(2600);
            Check(form.Board.ActiveAnimationCount == 0, "Game-over animation settles instead of running forever.");
            mode.SelectedIndex = (int)GameMode.Arcade;
            difficulty.SelectedIndex = 0;
            newGame.PerformClick();
            var drone = Find<Button>(form, "Drone");
            var emp = Find<Button>(form, "Emp");
            var aegis = Find<Button>(form, "Aegis");
            var nextSector = Find<Button>(form, "NextSector");
            var upgrade = Find<ComboBox>(form, "Upgrade");
            Check(form.CurrentRun is { Sector: 1, Energy: 40 } && drone.Visible && !drone.Enabled &&
                !emp.Enabled && !aegis.Enabled, "Arcade opens with visible abilities locked until first reveal.");
            if (form.Board.AnimationsEnabled)
                Find<Button>(form, "Motion").PerformClick();
            Click(form.Board, 4, 4, MouseButtons.Right);
            Click(form.Board, 4, 4, MouseButtons.Right);
            Check(form.CurrentRun!.Energy == 40, "Flag toggles cannot farm Overdrive energy.");
            Click(form.Board, 4, 4, MouseButtons.Left);
            Check(form.CurrentRun.Energy is > 40 and <= 60 && drone.Enabled && emp.Enabled,
                "Safe opening earns capped energy and enables affordable abilities.");
            while (form.CurrentRun.Energy < 60)
            {
                var safe = Positions(form.CurrentGame).First(p => !form.CurrentGame[p.X, p.Y].IsMine &&
                    !form.CurrentGame[p.X, p.Y].IsRevealed);
                Click(form.Board, safe.X, safe.Y, MouseButtons.Left);
            }
            int scanRow = form.Board.Selected.Y;
            int beforeEmp = form.CurrentRun.Energy;
            emp.PerformClick();
            Check(form.Board.RadarRow == scanRow && form.CurrentRun.Energy == beforeEmp - 25,
                "EMP button scans the selected row for the exact energy cost.");
            int beforeDrone = form.CurrentRun.Energy;
            int safeBeforeDrone = form.CurrentGame.SafeRemaining;
            PressKey(form.Board, Keys.D1);
            Check(form.CurrentRun.Energy == beforeDrone - 35 && form.CurrentGame.SafeRemaining < safeBeforeDrone,
                "Drone key opens safe tiles without earning its energy back.");
            Pump(4300);
            Check(form.Board.RadarRow is null, "EMP row intel expires after four seconds.");
            int completedScore = 0;
            for (int sector = 1; sector <= ArcadeRun.FinalSector; sector++)
            {
                if (form.CurrentGame.State == GameState.Ready)
                    Click(form.Board, 4, 4, MouseButtons.Left);
                foreach (var p in Positions(form.CurrentGame).Where(p => !form.CurrentGame[p.X, p.Y].IsMine &&
                    !form.CurrentGame[p.X, p.Y].IsRevealed))
                    Click(form.Board, p.X, p.Y, MouseButtons.Left);
                Check(form.CurrentGame.State == GameState.Won && form.CurrentRun.Sector == sector,
                    $"Arcade sector {sector} can be cleared through mouse handlers.");
                completedScore += form.CurrentGame.Score;
                Check(form.CurrentRun.TotalScore == completedScore, $"Sector {sector} score is accumulated exactly once.");
                if (sector == ArcadeRun.FinalSector)
                    break;
                Check(nextSector.Visible && upgrade.Visible && !drone.Visible,
                    $"Sector {sector} exposes the upgrade / next-sector controls.");
                Game cleared = form.CurrentGame;
                allowDiscard = false;
                newGame.PerformClick();
                Check(ReferenceEquals(form.CurrentGame, cleared),
                    "Cancelling a new game during intermission preserves the Arcade Run.");
                allowDiscard = true;
                upgrade.SelectedIndex = sector == 1 ? (int)RunUpgrade.Armor : (int)RunUpgrade.Efficiency;
                if (sector == 1)
                    SavePreview(form, outputDirectory, "arcade-upgrade-preview.png");
                int energyAtExit = form.CurrentRun.Energy;
                int minesAtExit = form.CurrentGame.Difficulty.Mines;
                nextSector.PerformClick();
                Check(form.CurrentRun.Sector == sector + 1 && form.CurrentRun.Energy == energyAtExit &&
                    form.CurrentGame.Difficulty.Mines > minesAtExit && form.CurrentGame.Shields == 2 &&
                    form.CurrentGame.State == GameState.Ready && form.Board.RadarRow is null,
                    $"Next sector {sector + 1} applies upgrades, raises mines and resets overlays.");
            }
            Check(form.CurrentRun!.IsComplete && !nextSector.Visible &&
                !drone.Enabled && !emp.Enabled && !aegis.Enabled, "Fifth sector completes the run and locks abilities.");
            saved = new RecordStore(recordsPath);
            saved.Load();
            Check(saved.Warning is null && saved.Data.Wins.Count(w => w.Mode == nameof(GameMode.Arcade)) == 5 &&
                saved.Data.Wins.Any(w => w.Sector == 5 && w.Score == form.CurrentRun.TotalScore),
                "Arcade saves one labeled cumulative-score record per cleared sector.");
            SavePreview(form, outputDirectory, "arcade-victory-preview.png");
            newGame.PerformClick();
            Click(form.Board, 4, 4, MouseButtons.Left);
            while (form.CurrentRun!.Energy < 60)
            {
                var safe = Positions(form.CurrentGame).First(p => !form.CurrentGame[p.X, p.Y].IsMine &&
                    !form.CurrentGame[p.X, p.Y].IsRevealed);
                Click(form.Board, safe.X, safe.Y, MouseButtons.Left);
            }
            int energyBeforeAegis = form.CurrentRun!.Energy;
            aegis.PerformClick();
            Check(form.CurrentGame.Shields == 2 && form.CurrentRun.Energy == energyBeforeAegis - 60,
                "Aegis button converts energy into protection.");
            for (int i = 0; i < 3; i++)
            {
                var target = Positions(form.CurrentGame).First(p => form.CurrentGame[p.X, p.Y].IsMine &&
                    !form.CurrentGame[p.X, p.Y].IsFlagged);
                Click(form.Board, target.X, target.Y, MouseButtons.Left);
            }
            Check(form.CurrentGame.State == GameState.Lost && !nextSector.Visible,
                "Arcade loss ends the run without offering a next sector.");
            newGame.PerformClick();
            Check(form.CurrentRun is { Sector: 1, Energy: 40, ArmorLevel: 0, EfficiencyLevel: 0 } &&
                form.CurrentRun.TotalScore == 0, "New game resets the full Arcade Run.");
            Click(form.Board, 4, 4, MouseButtons.Left);
            SavePreview(form, outputDirectory, "arcade-preview.png");
            mode.SelectedIndex = (int)GameMode.Classic;
            newGame.PerformClick();
            Check(form.CurrentRun is null && !drone.Visible && form.CurrentGame.Shields == 0,
                "Switching back to Classic removes Arcade systems and preserves original rules.");
            int classicRemaining = form.CurrentGame.SafeRemaining;
            PressKey(form.Board, Keys.D1);
            Check(form.CurrentGame.State == GameState.Ready && form.CurrentGame.SafeRemaining == classicRemaining,
                "Arcade hotkeys cannot reveal or alter a Classic game.");
            form.Close();
            Check(!form.Visible, "Window closes cleanly.");
            report.WriteLine("\nAll desktop UI checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            report.WriteLine($"FAIL: {ex}");
            return 1;
        }
        finally
        {
            File.Delete(recordsPath);
            File.Delete(recordsPath + ".tmp");
        }

        void Check(bool success, string message)
        {
            if (!success)
                throw new InvalidOperationException(message);
            report.WriteLine($"PASS: {message}");
            report.Flush();
        }
    }

    private static T Find<T>(Control parent, string name) where T : Control =>
        parent.Controls.Find(name, searchAllChildren: true).OfType<T>().Single();

    private static void Click(MinefieldControl board, int x, int y, MouseButtons button)
    {
        typeof(MinefieldControl).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(board, [new MouseEventArgs(button, 1, x * board.CellSize + board.CellSize / 2,
                y * board.CellSize + board.CellSize / 2, 0)]);
        Application.DoEvents();
    }

    private static void PressKey(MinefieldControl board, Keys key)
    {
        object[] arguments = [Message.Create(board.Handle, 0x100, (IntPtr)(int)key, IntPtr.Zero), key];
        object? handled = typeof(MinefieldControl).GetMethod("ProcessCmdKey",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(board, arguments);
        if (handled is not true)
            throw new InvalidOperationException($"Key {key} was not handled by the board.");
        Application.DoEvents();
    }

    private static IEnumerable<(int X, int Y)> Positions(Game game)
    {
        for (int y = 0; y < game.Height; y++)
        for (int x = 0; x < game.Width; x++)
            yield return (x, y);
    }

    private static string MineLayout(Game game) =>
        string.Concat(Positions(game).Select(p => game[p.X, p.Y].IsMine ? "*" : "."));

    private static void Pump(int milliseconds)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static string FrameSignature(Control control)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new(Point.Empty, control.Size));
        using var data = new MemoryStream();
        bitmap.Save(data, ImageFormat.Png);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data.ToArray()));
    }

    private static void SavePreview(Form form, string directory, string name)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
    }
}
