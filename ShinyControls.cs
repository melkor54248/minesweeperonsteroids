using System.Drawing.Drawing2D;

namespace MinesweeperOnSteroids;

internal static class NeonTheme
{
    public static readonly Color Cyan = Color.FromArgb(78, 238, 218);
    public static readonly Color Gold = Color.FromArgb(255, 202, 98);

    public static GraphicsPath Rounded(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GameBrand : Control
{
    private readonly Icon icon;

    public GameBrand(Icon icon)
    {
        this.icon = icon;
        DoubleBuffered = true;
        AccessibleName = "Minesweeper on Steroids";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float textLeft = 58 * scale;
        e.Graphics.DrawIcon(icon, new Rectangle(0, (int)(5 * scale), (int)(44 * scale), (int)(44 * scale)));
        using var font = new Font("Segoe UI", Width / scale < 750 ? 23 : 28, FontStyle.Bold);
        using var glow = new SolidBrush(Color.FromArgb(25, NeonTheme.Cyan));
        using var ink = new SolidBrush(Color.FromArgb(219, 255, 249));
        e.Graphics.DrawString("MINESWEEPER", font, glow, textLeft + scale, 7 * scale);
        e.Graphics.DrawString("MINESWEEPER", font, ink, textLeft, 5 * scale);
        float textWidth = e.Graphics.MeasureString("MINESWEEPER", font).Width;
        var badge = new RectangleF(textLeft + textWidth + 13 * scale, 16 * scale, 158 * scale, 29 * scale);
        using var path = NeonTheme.Rounded(badge, 6);
        using var fill = new LinearGradientBrush(badge, Color.FromArgb(255, 220, 135), NeonTheme.Gold, 90);
        e.Graphics.FillPath(fill, path);
        using var badgeFont = new Font("Segoe UI", 10, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, "ON STEROIDS", badgeFont, Rectangle.Round(badge),
            Color.FromArgb(47, 33, 15), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        using var line = new LinearGradientBrush(new RectangleF(0, Height - 3 * scale, Width, 2 * scale),
            NeonTheme.Cyan, Color.FromArgb(16, 23, 33), 0f);
        e.Graphics.FillRectangle(line, 0, Height - 3 * scale, Width, 2 * scale);
    }
}

internal sealed class ShinyButton : Button
{
    private bool hovered;
    private bool pressed;

    public ShinyButton()
    {
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.FromArgb(16, 23, 33);
        ForeColor = Color.WhiteSmoke;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rectangle = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = NeonTheme.Rounded(rectangle, 7);
        Color top = !Enabled ? Color.FromArgb(37, 45, 54) :
            pressed ? Color.FromArgb(18, 83, 91) : hovered ? Color.FromArgb(46, 109, 125) : Color.FromArgb(45, 72, 93);
        using var fill = new LinearGradientBrush(rectangle, top, Color.FromArgb(20, 34, 50), 90);
        e.Graphics.FillPath(fill, path);
        using var edge = new Pen(hovered && Enabled ? NeonTheme.Cyan : Color.FromArgb(63, 94, 114));
        e.Graphics.DrawPath(edge, path);
        using var shine = new Pen(Color.FromArgb(45, Color.White));
        e.Graphics.DrawLine(shine, 9, 3, Width - 10, 3);
        TextRenderer.DrawText(e.Graphics, Text, Font, rectangle,
            Enabled ? ForeColor : Color.FromArgb(115, 129, 140),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(rectangle, -5, -5));
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
}

internal sealed class GameHud : Control
{
    private Game? game;
    private TimeSpan elapsed;

    public GameHud()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        AccessibleName = "Game statistics";
    }

    public void UpdateValues(Game value, TimeSpan time)
    {
        game = value;
        elapsed = time;
        AccessibleDescription = $"Mines {value.Difficulty.Mines - value.FlagCount}, safe cells {value.SafeRemaining}, " +
            $"score {value.Score}, combo {value.Combo}.";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (game is null)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        string[] labels = ["MINES LEFT", "SAFE CELLS", "MISSION TIME", "SCORE", "COMBO"];
        string[] values =
        [
            $"{game.Difficulty.Mines - game.FlagCount:00}", $"{game.SafeRemaining:000}",
            $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}", $"{game.Score:N0}",
            game.Mode == GameMode.Classic ? "--" : $"x{game.Combo}"
        ];
        Color[] colors = [NeonTheme.Gold, NeonTheme.Cyan, Color.FromArgb(124, 191, 255), Color.WhiteSmoke, Color.FromArgb(204, 148, 255)];
        float scale = DeviceDpi / 96f;
        float cardWidth = (Width - 8f * scale * 4) / 5;
        using var labelFont = new Font("Segoe UI", 8, FontStyle.Bold);
        using var valueFont = new Font("Consolas", Width / scale < 750 ? 17 : 22, FontStyle.Bold);
        for (int i = 0; i < 5; i++)
        {
            var rectangle = new RectangleF(i * (cardWidth + 8 * scale), 3 * scale, cardWidth, Height - 8 * scale);
            using var path = NeonTheme.Rounded(rectangle, 9);
            using var fill = new LinearGradientBrush(rectangle, Color.FromArgb(33, 48, 69), Color.FromArgb(16, 26, 41), 90);
            e.Graphics.FillPath(fill, path);
            using var border = new Pen(Color.FromArgb(57, 78, 101));
            e.Graphics.DrawPath(border, path);
            using var accent = new Pen(Color.FromArgb(150, colors[i]), 2);
            e.Graphics.DrawLine(accent, rectangle.Left + 12, rectangle.Top + 1, rectangle.Right - 12, rectangle.Top + 1);
            TextRenderer.DrawText(e.Graphics, labels[i], labelFont,
                new Rectangle((int)(rectangle.Left + 12 * scale), (int)(12 * scale),
                    (int)(cardWidth - 20 * scale), (int)(17 * scale)), Color.FromArgb(151, 174, 196));
            TextRenderer.DrawText(e.Graphics, values[i], valueFont,
                new Rectangle((int)(rectangle.Left + 10 * scale), (int)(30 * scale),
                    (int)(cardWidth - 18 * scale), (int)(36 * scale)), colors[i],
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

    }
}

internal sealed class MissionPanel : Panel
{
    private Game? game;
    private TimeSpan elapsed;

    public MissionPanel()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(12, 18, 27);
        BorderStyle = BorderStyle.FixedSingle;
    }

    public void UpdateValues(Game value, TimeSpan time)
    {
        game = value;
        elapsed = time;
        if (Controls.Count > 0)
        {
            int left = Controls[0].Right + 10;
            if (left < ClientSize.Width)
                Invalidate(new Rectangle(left, 0, ClientSize.Width - left, ClientSize.Height));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (game is null || Controls.Count == 0 || HorizontalScroll.Visible)
            return;
        float scale = DeviceDpi / 96f;
        float left = Controls[0].Right + 26 * scale;
        float width = Math.Min(330 * scale, ClientSize.Width - left - 26 * scale);
        if (width < 235 * scale)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var labelFont = new Font("Segoe UI", 9, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 10);
        using var headingFont = new Font("Segoe UI", 16, FontStyle.Bold);
        var area = new RectangleF(left, 22 * scale, width, 460 * scale);
        using var path = NeonTheme.Rounded(area, 12 * scale);
        using var fill = new LinearGradientBrush(area, Color.FromArgb(24, 38, 56), Color.FromArgb(12, 21, 34), 90);
        e.Graphics.FillPath(fill, path);
        using var edge = new Pen(Color.FromArgb(43, 69, 90));
        e.Graphics.DrawPath(edge, path);
        Draw("MISSION CONTROL", headingFont, NeonTheme.Cyan, 18);
        Draw($"{game.Difficulty.Width} x {game.Difficulty.Height}  /  {game.Difficulty.Mines} MINES", labelFont,
            Color.FromArgb(138, 161, 181), 57);
        int total = game.Width * game.Height - game.Difficulty.Mines;
        double progress = (double)(total - game.SafeRemaining) / total;
        Draw($"FIELD CLEARANCE / {(int)(progress * 100)}%", labelFont, Color.WhiteSmoke, 102);
        var track = new RectangleF(left + 18 * scale, 151 * scale, width - 36 * scale, 7 * scale);
        using var trackBrush = new SolidBrush(Color.FromArgb(39, 57, 76));
        e.Graphics.FillRectangle(trackBrush, track);
        if (progress > 0)
        {
            using var progressBrush = new LinearGradientBrush(track, Color.FromArgb(26, 132, 160), NeonTheme.Cyan, 0f);
            e.Graphics.FillRectangle(progressBrush, track.X, track.Y, (float)(track.Width * progress), track.Height);
        }
        bool classic = game.Mode == GameMode.Classic;
        Draw("SHIELD SYSTEM", labelFont, Color.FromArgb(138, 161, 181), 160);
        Draw(classic ? "OFFLINE / CLASSIC RULES" : game.Shields > 0 ? "ARMED / ONE HIT PROTECTION" : "DEPLETED / STAY SHARP",
            bodyFont, game.Shields > 0 ? Color.FromArgb(124, 191, 255) : NeonTheme.Gold, 183);
        Draw("RADAR ARRAY", labelFont, Color.FromArgb(138, 161, 181), 231);
        for (int i = 0; i < 3; i++)
        {
            var pip = new RectangleF(left + (18 + i * 31) * scale, 282 * scale, 23 * scale, 7 * scale);
            using var pipBrush = new SolidBrush(i < game.Scans ? NeonTheme.Cyan : Color.FromArgb(40, 56, 73));
            e.Graphics.FillRectangle(pipBrush, pip);
        }
        Draw(classic ? "NO ASSISTS. JUST DEDUCTION." : $"{game.Scans} SCANS / SELECT A TILE, PRESS R", bodyFont,
            Color.WhiteSmoke, 282);
        Draw("FIELD INTEL", labelFont, NeonTheme.Gold, 333);
        Draw("Numbers count nearby mines.\nRight click to mark a threat.\nClick an open number to chord.", bodyFont,
            Color.FromArgb(164, 185, 206), 357, 76);
        Draw(game.IsFinished ? game.State == GameState.Won ? "MISSION COMPLETE" : "SIGNAL LOST / REDEPLOY" :
            game.State == GameState.Ready ? "AWAITING YOUR FIRST MOVE" : $"LIVE / {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}",
            labelFont, game.State == GameState.Lost ? Color.Salmon : NeonTheme.Cyan, 432);

        void Draw(string text, Font font, Color color, float top, float height = 28)
        {
            var rectangle = Rectangle.Round(new RectangleF(left + 16 * scale, (22 + top) * scale,
                width - 30 * scale, height * scale));
            TextRenderer.DrawText(e.Graphics, text, font, rectangle, color,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }
    }
}
