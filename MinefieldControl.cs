using System.Drawing.Drawing2D;

namespace MinesweeperOnSteroids;

internal enum FieldAction { Reveal, Flag, Chord, Radar }

internal sealed class MinefieldControl : Control
{
    private static readonly Color[] ClueColors =
    [
        Color.DimGray, Color.FromArgb(68, 190, 255), Color.FromArgb(99, 224, 152),
        Color.FromArgb(255, 116, 116), Color.FromArgb(162, 155, 255),
        Color.FromArgb(255, 172, 91), Color.Turquoise, Color.HotPink, Color.WhiteSmoke
    ];
    private Game? game;
    public int CellSize => (int)Math.Round(34 * DeviceDpi / 96d);
    public Point Selected { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Point? RadarCenter { get; set; }
    public event Action<int, int, FieldAction>? CellAction;

    public MinefieldControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        BackColor = Color.FromArgb(16, 23, 33);
        AccessibleName = "Minefield";
        AccessibleDescription = "Arrow keys move, Space reveals, F flags, Enter chords, R scans.";
    }

    public void SetGame(Game value)
    {
        game = value;
        RadarCenter = null;
        Selected = new(value.Width / 2, value.Height / 2);
        UpdateBoardSize();
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
        using var font = new Font("Segoe UI", 12, FontStyle.Bold);
        for (int y = 0; y < game.Height; y++)
        for (int x = 0; x < game.Width; x++)
        {
            var rectangle = new Rectangle(x * CellSize + 2, y * CellSize + 2, CellSize - 4, CellSize - 4);
            if (!e.ClipRectangle.IntersectsWith(rectangle))
                continue;
            Cell cell = game[x, y];
            bool scanned = RadarCenter is { } radar && Math.Abs(radar.X - x) <= 1 &&
                           Math.Abs(radar.Y - y) <= 1 && !cell.IsRevealed && !game.IsFinished;
            Color fill = cell.IsRevealed ? Color.FromArgb(22, 32, 45) : Color.FromArgb(43, 61, 80);
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

            if (game.ExplodedCell == (x, y))
                fill = Color.FromArgb(170, 45, 59);
            using var brush = new SolidBrush(fill);
            e.Graphics.FillRectangle(brush, rectangle);
            if (!cell.IsRevealed && !scanned)
            {
                using var edge = new Pen(Color.FromArgb(73, 95, 116));
                e.Graphics.DrawLine(edge, rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Top);
            }
            TextRenderer.DrawText(e.Graphics, text, font, rectangle, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Selected == new Point(x, y))
            {
                using var border = new Pen(Color.FromArgb(71, 223, 213), 2);
                e.Graphics.DrawRectangle(border, rectangle);
            }
        }
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
