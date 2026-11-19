using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace MinesweeperOnSteroids;

internal enum FieldAction { Reveal, Flag, Chord, Radar }
internal enum FieldEffect { Reveal, Flag, Radar, Shield, Explosion, Victory }

internal sealed class MinefieldControl : Control
{
    private static readonly Color[] ClueColors =
    [
        Color.DimGray, Color.FromArgb(68, 190, 255), Color.FromArgb(99, 224, 152),
        Color.FromArgb(255, 116, 116), Color.FromArgb(162, 155, 255),
        Color.FromArgb(255, 172, 91), Color.Turquoise, Color.HotPink, Color.WhiteSmoke
    ];
    private Game? game;
    private readonly Stopwatch animationClock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer animator = new() { Interval = 16 };
    private readonly Dictionary<Point, double> reveals = [];
    private readonly Dictionary<Point, double> flagPops = [];
    private readonly List<Ring> rings = [];
    private readonly List<Particle> particles = [];
    private readonly List<ScorePopup> popups = [];
    private readonly Random random = new();
    private bool[,] knownRevealed = new bool[0, 0];
    private bool[,] knownFlags = new bool[0, 0];
    private bool animationsEnabled = true;
    private Point? radarCenter;
    private Point? hovered;
    private double impactStarted = -10;
    private FieldEffect impactEffect;
    private bool paused;
    private int frameCount;
    private sealed record Ring(Point Origin, double Start, double Duration, float Radius, Color Color);
    private sealed record Particle(PointF Origin, PointF Velocity, double Start, double Duration, Color Color, bool Confetti);
    private sealed record ScorePopup(Point Origin, double Start, string Text, Color Color);

    public int CellSize => (int)Math.Round(34 * DeviceDpi / 96d);
    public Point Selected { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Point? RadarCenter
    {
        get => radarCenter;
        set
        {
            radarCenter = value;
            WakeAnimator();
            Invalidate();
        }
    }
    internal int ActiveAnimationCount => reveals.Count + flagPops.Count + rings.Count + particles.Count + popups.Count;
    internal int AnimationFrameCount => frameCount;
    internal bool AnimationsEnabled => animationsEnabled;
    internal FieldEffect? LastEffect { get; private set; }
    public event Action<int, int, FieldAction>? CellAction;

    public MinefieldControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        BackColor = Color.FromArgb(16, 23, 33);
        AccessibleName = "Minefield";
        AccessibleDescription = "Arrow keys move, Space reveals, F flags, Enter chords, R scans.";
        animator.Tick += (_, _) =>
        {
            double now = animationClock.Elapsed.TotalSeconds;
            foreach (Point point in reveals.Where(pair => now - pair.Value > .4).Select(pair => pair.Key).ToArray())
                reveals.Remove(point);
            foreach (Point point in flagPops.Where(pair => now - pair.Value > .3).Select(pair => pair.Key).ToArray())
                flagPops.Remove(point);
            rings.RemoveAll(ring => now - ring.Start > ring.Duration);
            particles.RemoveAll(particle => now - particle.Start > particle.Duration);
            popups.RemoveAll(popup => now - popup.Start > 1.15);
            frameCount++;
            Invalidate();
            if (ActiveAnimationCount == 0 && radarCenter is null && now - impactStarted > .6)
                animator.Stop();
        };
    }

    public void SetGame(Game value)
    {
        game = value;
        ClearEffects();
        animationClock.Restart();
        knownRevealed = new bool[value.Width, value.Height];
        knownFlags = new bool[value.Width, value.Height];
        RadarCenter = null;
        hovered = null;
        Selected = new(value.Width / 2, value.Height / 2);
        UpdateBoardSize();
        Invalidate();
    }

    public void SetAnimations(bool enabled)
    {
        animationsEnabled = enabled;
        if (!enabled)
            ClearEffects();
        else
            WakeAnimator();
        Invalidate();
    }

    public void PauseEffects()
    {
        paused = true;
        animationClock.Stop();
        animator.Stop();
    }

    public void ResumeEffects()
    {
        paused = false;
        animationClock.Start();
        WakeAnimator();
    }

    private void ClearEffects()
    {
        animator.Stop();
        reveals.Clear();
        flagPops.Clear();
        rings.Clear();
        particles.Clear();
        popups.Clear();
        impactStarted = -10;
        LastEffect = null;
    }

    private void WakeAnimator()
    {
        if (animationsEnabled && !paused && (ActiveAnimationCount > 0 || radarCenter is not null))
            animator.Start();
    }

    public void AnimateAction(Point origin, FieldEffect effect, int scoreDelta)
    {
        if (game is null)
            return;
        double now = animationClock.Elapsed.TotalSeconds;
        LastEffect = effect;
        for (int y = 0; y < game.Height; y++)
        for (int x = 0; x < game.Width; x++)
        {
            if (animationsEnabled && game[x, y].IsRevealed && !knownRevealed[x, y])
                reveals[new(x, y)] = now + Math.Min(.28, Math.Sqrt(Math.Pow(x - origin.X, 2) + Math.Pow(y - origin.Y, 2)) * .025);
            if (animationsEnabled && game[x, y].IsFlagged != knownFlags[x, y])
                flagPops[new(x, y)] = now;
            knownRevealed[x, y] = game[x, y].IsRevealed;
            knownFlags[x, y] = game[x, y].IsFlagged;
        }
        if (!animationsEnabled)
        {
            Invalidate();
            return;
        }
        Color accent = effect switch
        {
            FieldEffect.Explosion => Color.FromArgb(255, 104, 79),
            FieldEffect.Shield => Color.FromArgb(114, 181, 255),
            FieldEffect.Victory or FieldEffect.Flag => NeonTheme.Gold,
            _ => NeonTheme.Cyan
        };
        int count = effect switch
        {
            FieldEffect.Explosion => 80,
            FieldEffect.Shield => 48,
            FieldEffect.Victory => 110,
            FieldEffect.Flag => 8,
            FieldEffect.Reveal => 6,
            _ => 0
        };
        if (effect is FieldEffect.Shield or FieldEffect.Explosion or FieldEffect.Victory)
        {
            impactStarted = now;
            impactEffect = effect;
        }
        rings.Add(new(origin, now, effect == FieldEffect.Radar ? 1.1 : .65,
            effect is FieldEffect.Explosion or FieldEffect.Victory ? 7 : 3, accent));
        if (rings.Count > 12)
            rings.RemoveAt(0);
        for (int i = 0; i < count; i++)
        {
            double angle = random.NextDouble() * Math.Tau;
            float speed = (float)(1 + random.NextDouble() * (count > 10 ? 7 : 2));
            bool confetti = effect == FieldEffect.Victory;
            Color color = confetti ? ClueColors[random.Next(1, ClueColors.Length)] : accent;
            particles.Add(new(new(origin.X + .5f, origin.Y + .5f),
                new((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed - (confetti ? 5 : 0)),
                now, confetti ? 2.4 : .8 + random.NextDouble() * .5, color, confetti));
        }
        if (particles.Count > 220)
            particles.RemoveRange(0, particles.Count - 220);
        if (scoreDelta > 0)
        {
            popups.Add(new(origin, now, $"+{scoreDelta:N0}" + (game.Combo > 1 ? $"  x{game.Combo}" : ""), accent));
            if (popups.Count > 5)
                popups.RemoveAt(0);
        }
        WakeAnimator();
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateBoardSize();
    }

    private void UpdateBoardSize()
    {
        if (game is not null)
            Size = new(game.Width * CellSize + 1, game.Height * CellSize + 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (game is null)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        double now = animationClock.Elapsed.TotalSeconds;
        using var font = new Font("Segoe UI", 12, FontStyle.Bold);
        for (int y = 0; y < game.Height; y++)
        for (int x = 0; x < game.Width; x++)
        {
            var rectangle = new Rectangle(x * CellSize + 2, y * CellSize + 2, CellSize - 5, CellSize - 5);
            if (!e.ClipRectangle.IntersectsWith(rectangle))
                continue;
            Cell cell = game[x, y];
            bool scanned = RadarCenter is { } radar && Math.Abs(radar.X - x) <= 1 &&
                           Math.Abs(radar.Y - y) <= 1 && !cell.IsRevealed && !game.IsFinished;
            Point position = new(x, y);
            Color fill = cell.IsRevealed ? Color.FromArgb(18, 30, 45) : Color.FromArgb(56, 83, 109);
            Color ink = Color.FromArgb(255, 201, 88);
            string text = "";
            if (game.IsFinished && cell.IsMine)
            {
                text = cell.IsFlagged ? "F" : "*";
                ink = Color.FromArgb(255, 116, 116);
                fill = Color.FromArgb(66, 30, 43);
            }
            else if (game.IsFinished && cell.IsFlagged && !cell.IsMine)
            {
                text = "X";
                ink = Color.FromArgb(255, 116, 116);
            }
            else if (cell.IsRevealed)
            {
                text = cell.AdjacentMines == 0 ? "" : cell.AdjacentMines.ToString();
                ink = ClueColors[cell.AdjacentMines];
            }
            else if (scanned)
            {
                text = cell.IsMine ? "!" : "-";
                ink = cell.IsMine ? Color.FromArgb(255, 116, 116) : Color.Aquamarine;
                fill = Color.FromArgb(25, 68, 68);
            }
            else if (cell.IsFlagged)
                text = "F";

            bool popping = animationsEnabled && reveals.ContainsKey(position);
            double revealProgress = popping ? Math.Clamp((now - reveals[position]) / .4, 0, 1) : 1;
            if (popping)
                fill = Blend(NeonTheme.Cyan, fill, revealProgress);
            if (hovered == position && !cell.IsRevealed)
                fill = Blend(fill, Color.FromArgb(102, 154, 185), .42);
            if (game.ExplodedCell == (x, y))
                fill = Color.FromArgb(170, 45, 59);
            using var tilePath = NeonTheme.Rounded(rectangle, 5);
            using var shadow = new SolidBrush(Color.FromArgb(7, 13, 23));
            e.Graphics.FillRectangle(shadow, rectangle.X + 1, rectangle.Y + 3, rectangle.Width, rectangle.Height);
            using var brush = new LinearGradientBrush(rectangle, fill,
                Blend(fill, Color.Black, cell.IsRevealed ? .15 : .45), 90);
            e.Graphics.FillPath(brush, tilePath);
            using var tileBorder = new Pen(cell.IsRevealed ? Color.FromArgb(28, 44, 62) : Color.FromArgb(78, 111, 139));
            e.Graphics.DrawPath(tileBorder, tilePath);
            if (!cell.IsRevealed || popping)
            {
                var sheen = new Rectangle(rectangle.Left + 2, rectangle.Top + 1, rectangle.Width - 4, rectangle.Height / 2);
                using var gloss = new LinearGradientBrush(sheen, Color.FromArgb(38, Color.White), Color.Transparent, 90);
                e.Graphics.FillRectangle(gloss, sheen);
            }
            if (text == "F")
            {
                float pop = flagPops.TryGetValue(position, out double flagStart)
                    ? (float)(Math.Sin(Math.Clamp((now - flagStart) / .3, 0, 1) * Math.PI) * .16) : 0;
                DrawFlag(e.Graphics, rectangle, pop);
            }
            else if (text == "*")
                DrawMine(e.Graphics, rectangle);
            else
                TextRenderer.DrawText(e.Graphics, text, font, rectangle, ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Selected == position || hovered == position)
            {
                using var glow = new Pen(Color.FromArgb(50, NeonTheme.Cyan), 5);
                e.Graphics.DrawPath(glow, tilePath);
                using var border = new Pen(Selected == position ? NeonTheme.Cyan : Color.FromArgb(149, 215, 227), 1.5f);
                e.Graphics.DrawPath(border, tilePath);
            }
        }
        DrawEffects(e.Graphics, now);
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static void DrawFlag(Graphics graphics, Rectangle rectangle, float pop)
    {
        float cx = rectangle.Left + rectangle.Width * .45f;
        float top = rectangle.Top + rectangle.Height * (.21f - pop);
        float bottom = rectangle.Bottom - rectangle.Height * .2f;
        using var pole = new Pen(Color.FromArgb(217, 231, 242), 2);
        graphics.DrawLine(pole, cx, top, cx, bottom);
        graphics.DrawLine(pole, cx - 4, bottom, cx + 5, bottom);
        using var flag = new SolidBrush(NeonTheme.Gold);
        PointF[] points = [new(cx + 1, top), new(cx + rectangle.Width * .34f, top + 5),
            new(cx + 1, top + rectangle.Height * .35f)];
        graphics.FillPolygon(flag, points);
    }

    private static void DrawMine(Graphics graphics, Rectangle rectangle)
    {
        float cx = rectangle.Left + rectangle.Width / 2f;
        float cy = rectangle.Top + rectangle.Height / 2f;
        float radius = rectangle.Width * .23f;
        using var spike = new Pen(Color.FromArgb(255, 132, 117), 2);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            graphics.DrawLine(spike, cx, cy, cx + (float)Math.Cos(angle) * radius * 1.5f,
                cy + (float)Math.Sin(angle) * radius * 1.5f);
        }
        using var body = new SolidBrush(Color.FromArgb(255, 116, 100));
        graphics.FillEllipse(body, cx - radius, cy - radius, radius * 2, radius * 2);
        using var highlight = new SolidBrush(Color.FromArgb(255, 224, 192));
        graphics.FillEllipse(highlight, cx - radius * .55f, cy - radius * .55f, radius * .55f, radius * .55f);
    }

    private void DrawEffects(Graphics graphics, double now)
    {
        if (animationsEnabled)
        {
            foreach (Ring ring in rings)
            {
                double progress = Math.Clamp((now - ring.Start) / ring.Duration, 0, 1);
                float radius = (float)(.25 + progress * ring.Radius) * CellSize;
                using var pen = new Pen(Color.FromArgb((int)(160 * (1 - progress)), ring.Color), 2.5f);
                graphics.DrawEllipse(pen, (ring.Origin.X + .5f) * CellSize - radius,
                    (ring.Origin.Y + .5f) * CellSize - radius, radius * 2, radius * 2);
            }
            foreach (Particle particle in particles)
            {
                float age = (float)(now - particle.Start);
                float fade = Math.Clamp(1 - age / (float)particle.Duration, 0, 1);
                float x = (particle.Origin.X + particle.Velocity.X * age) * CellSize;
                float y = (particle.Origin.Y + particle.Velocity.Y * age + age * age * (particle.Confetti ? 4 : 2)) * CellSize;
                using var brush = new SolidBrush(Color.FromArgb((int)(220 * fade), particle.Color));
                float size = (particle.Confetti ? .16f : .10f) * CellSize * fade + 1;
                graphics.FillRectangle(brush, x, y, size, particle.Confetti ? size * .5f : size);
            }
            using var popupFont = new Font("Segoe UI", 12, FontStyle.Bold);
            foreach (ScorePopup popup in popups)
            {
                float age = (float)(now - popup.Start);
                var origin = new PointF(Math.Clamp(popup.Origin.X * CellSize - 15, 2, Math.Max(2, Width - 155)),
                    Math.Max(5, popup.Origin.Y * CellSize - age * CellSize));
                using var shadow = new SolidBrush(Color.FromArgb((int)(220 * Math.Max(0, 1 - age / 1.15)), Color.Black));
                using var brush = new SolidBrush(Color.FromArgb((int)(255 * Math.Max(0, 1 - age / 1.15)), popup.Color));
                graphics.DrawString(popup.Text, popupFont, shadow, origin.X + 1, origin.Y + 2);
                graphics.DrawString(popup.Text, popupFont, brush, origin);
            }
            if (now - impactStarted is >= 0 and < .55)
            {
                Color impact = impactEffect == FieldEffect.Explosion ? Color.OrangeRed :
                    impactEffect == FieldEffect.Victory ? NeonTheme.Gold : Color.DodgerBlue;
                using var flash = new SolidBrush(Color.FromArgb((int)(45 * (1 - (now - impactStarted) / .55)), impact));
                graphics.FillRectangle(flash, ClientRectangle);
            }
        }
        if (radarCenter is { } radar)
        {
            float pulse = animationsEnabled ? (float)((now * 1.3) % 1) : .6f;
            float radius = (1 + pulse * .6f) * CellSize;
            using var pen = new Pen(Color.FromArgb(animationsEnabled ? (int)(180 * (1 - pulse)) : 130, NeonTheme.Cyan), 2);
            graphics.DrawEllipse(pen, (radar.X + .5f) * CellSize - radius, (radar.Y + .5f) * CellSize - radius, radius * 2, radius * 2);
        }
        if (game?.IsFinished == true)
        {
            int visibleX = Parent is Panel panel ? -panel.AutoScrollPosition.X : 0;
            int visibleY = Parent is Panel container ? -container.AutoScrollPosition.Y : 0;
            int width = Math.Min(Width - visibleX - 12, Math.Min(Parent?.ClientSize.Width - 24 ?? Width, 420));
            if (width > 160)
            {
                var area = new Rectangle(visibleX + 6, visibleY + 8, width, 54);
                using var path = NeonTheme.Rounded(area, 10);
                using var background = new SolidBrush(Color.FromArgb(235, 13, 23, 38));
                graphics.FillPath(background, path);
                Color color = game.State == GameState.Won ? NeonTheme.Gold : Color.FromArgb(255, 135, 120);
                using var border = new Pen(color, 1.5f);
                graphics.DrawPath(border, path);
                using var font = new Font("Segoe UI", width < 280 ? 11 : 16, FontStyle.Bold);
                TextRenderer.DrawText(graphics, game.State == GameState.Won ? "MINEFIELD CONQUERED" : "DETONATION",
                    font, area, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point? next = game is not null && e.X >= 0 && e.Y >= 0 &&
                      e.X < game.Width * CellSize && e.Y < game.Height * CellSize
            ? new Point(e.X / CellSize, e.Y / CellSize) : null;
        if (next != hovered)
        {
            hovered = next;
            Invalidate();
        }
        Cursor = hovered is null ? Cursors.Default : Cursors.Hand;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = null;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            animator.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (game is null || e.X < 0 || e.Y < 0 || e.X >= game.Width * CellSize || e.Y >= game.Height * CellSize)
            return;
        Focus();
        SelectCell(e.X / CellSize, e.Y / CellSize);
        FieldAction action;
        if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Shift))
            action = FieldAction.Flag;
        else if (e.Button == MouseButtons.Middle)
            action = FieldAction.Chord;
        else if (e.Button == MouseButtons.Left)
            action = game[Selected.X, Selected.Y].IsRevealed ? FieldAction.Chord : FieldAction.Reveal;
        else
            return;
        CellAction?.Invoke(Selected.X, Selected.Y, action);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (game is null)
            return base.ProcessCmdKey(ref msg, keyData);
        var (dx, dy) = keyData switch
        {
            Keys.Left or Keys.A => (-1, 0),
            Keys.Right or Keys.D => (1, 0),
            Keys.Up or Keys.W => (0, -1),
            Keys.Down or Keys.S => (0, 1),
            _ => (0, 0)
        };
        if (dx != 0 || dy != 0)
        {
            SelectCell(Math.Clamp(Selected.X + dx, 0, game.Width - 1),
                Math.Clamp(Selected.Y + dy, 0, game.Height - 1));
            // Scroll the selected tile, not the entire (potentially wider) board.
            if (Parent is Panel scroller)
            {
                int left = -scroller.AutoScrollPosition.X;
                int top = -scroller.AutoScrollPosition.Y;
                int px = Selected.X * CellSize;
                int py = Selected.Y * CellSize;
                left = px < left ? px : Math.Max(left, px + CellSize - scroller.ClientSize.Width + SystemInformation.VerticalScrollBarWidth);
                top = py < top ? py : Math.Max(top, py + CellSize - scroller.ClientSize.Height + SystemInformation.HorizontalScrollBarHeight);
                scroller.AutoScrollPosition = new(left, top);
            }
            return true;
        }
        FieldAction? action = keyData switch
        {
            Keys.Space => FieldAction.Reveal,
            Keys.F => FieldAction.Flag,
            Keys.Enter => FieldAction.Chord,
            Keys.R => FieldAction.Radar,
            _ => null
        };
        if (action is { } value)
        {
            CellAction?.Invoke(Selected.X, Selected.Y, value);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SelectCell(int x, int y)
    {
        Selected = new(x, y);
        Invalidate();
    }
}
