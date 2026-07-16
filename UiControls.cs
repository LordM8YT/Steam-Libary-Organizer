using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using SteamBibliotekRydder.Models;

namespace SteamBibliotekRydder;

internal static class AppPalette
{
    public static readonly Color BackgroundTop = Color.FromArgb(15, 23, 42);
    public static readonly Color BackgroundBottom = Color.FromArgb(2, 6, 23);
    public static readonly Color Surface = Color.FromArgb(15, 28, 50);
    public static readonly Color SurfaceRaised = Color.FromArgb(20, 36, 61);
    public static readonly Color SurfaceSoft = Color.FromArgb(24, 43, 70);
    public static readonly Color Border = Color.FromArgb(43, 63, 89);
    public static readonly Color BorderSoft = Color.FromArgb(33, 49, 72);
    public static readonly Color Accent = Color.FromArgb(56, 189, 248);
    public static readonly Color AccentLight = Color.FromArgb(125, 211, 252);
    public static readonly Color AccentDark = Color.FromArgb(8, 47, 73);
    public static readonly Color Text = Color.FromArgb(241, 245, 249);
    public static readonly Color Muted = Color.FromArgb(148, 163, 184);
    public static readonly Color Dim = Color.FromArgb(100, 116, 139);
    public static readonly Color Success = Color.FromArgb(52, 211, 153);
}

internal static class UiGeometry
{
    public static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(rectangle);
            return path;
        }

        var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GradientBackgroundPanel : Panel
{
    public GradientBackgroundPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        using var brush = new LinearGradientBrush(
            ClientRectangle,
            AppPalette.BackgroundTop,
            AppPalette.BackgroundBottom,
            LinearGradientMode.Vertical);
        eventArgs.Graphics.FillRectangle(brush, ClientRectangle);

        using var glowPath = new GraphicsPath();
        glowPath.AddEllipse(
            ClientRectangle.Width / 5,
            -ClientRectangle.Height / 2,
            Math.Max(500, ClientRectangle.Width * 3 / 5),
            Math.Max(400, ClientRectangle.Height));
        using var glow = new PathGradientBrush(glowPath)
        {
            CenterColor = Color.FromArgb(35, AppPalette.Accent),
            SurroundColors = [Color.FromArgb(0, AppPalette.Accent)]
        };
        eventArgs.Graphics.FillPath(glow, glowPath);
    }
}

internal class RoundedPanel : Panel
{
    private Color _fillColor = AppPalette.Surface;
    private Color _gradientColor = AppPalette.Surface;
    private Color _borderColor = AppPalette.BorderSoft;
    private int _radius = 20;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            Invalidate();
        }
    }

    public Color GradientColor
    {
        get => _gradientColor;
        set
        {
            _gradientColor = value;
            Invalidate();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    public int Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            UpdateRegion();
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var path = UiGeometry.RoundedRectangle(bounds, Radius);
        using var brush = new LinearGradientBrush(
            bounds,
            FillColor,
            GradientColor,
            LinearGradientMode.Vertical);
        eventArgs.Graphics.FillPath(brush, path);
        using var pen = new Pen(BorderColor);
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, Width, Height),
            Radius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }
}

internal sealed class ModernButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public ModernButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = AppPalette.SurfaceRaised;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 9.5f);
        Height = 42;
    }

    public bool Primary { get; set; }

    public int Radius { get; set; } = 14;

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, Width, Height),
            Radius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(eventArgs);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(eventArgs);
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(eventArgs);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var path = UiGeometry.RoundedRectangle(bounds, Radius);

        Color top;
        Color bottom;
        Color border;
        Color text;
        if (!Enabled)
        {
            top = AppPalette.SurfaceSoft;
            bottom = AppPalette.SurfaceRaised;
            border = AppPalette.BorderSoft;
            text = AppPalette.Dim;
        }
        else if (Primary)
        {
            top = _hovered ? Color.FromArgb(186, 230, 253) : AppPalette.AccentLight;
            bottom = _pressed ? Color.FromArgb(14, 165, 233) : AppPalette.Accent;
            border = AppPalette.AccentLight;
            text = AppPalette.AccentDark;
        }
        else
        {
            top = _hovered ? Color.FromArgb(31, 54, 84) : AppPalette.SurfaceSoft;
            bottom = _pressed ? AppPalette.Surface : AppPalette.SurfaceRaised;
            border = _hovered ? Color.FromArgb(63, 92, 124) : AppPalette.Border;
            text = Enabled ? AppPalette.Text : AppPalette.Dim;
        }

        using var brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
        eventArgs.Graphics.FillPath(brush, path);
        using var pen = new Pen(border);
        eventArgs.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(
            eventArgs.Graphics,
            Text,
            Font,
            bounds,
            text,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        AutoSize = false;
        Size = new Size(48, 26);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new Rectangle(0, 2, Width - 1, Height - 5);
        using var trackPath = UiGeometry.RoundedRectangle(track, track.Height / 2);
        using var trackBrush = new SolidBrush(
            Checked ? AppPalette.Accent : AppPalette.SurfaceSoft);
        eventArgs.Graphics.FillPath(trackBrush, trackPath);
        using var border = new Pen(Checked ? AppPalette.AccentLight : AppPalette.Border);
        eventArgs.Graphics.DrawPath(border, trackPath);

        var diameter = track.Height - 6;
        var x = Checked
            ? track.Right - diameter - 3
            : track.Left + 3;
        var thumb = new Rectangle(x, track.Top + 3, diameter, diameter);
        using var thumbBrush = new SolidBrush(
            Checked ? AppPalette.AccentDark : AppPalette.Muted);
        eventArgs.Graphics.FillEllipse(thumbBrush, thumb);
    }
}

internal sealed class ModernCheckedListBox : CheckedListBox
{
    public ModernCheckedListBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 44;
        BorderStyle = BorderStyle.None;
        BackColor = AppPalette.Surface;
        ForeColor = AppPalette.Text;
        CheckOnClick = true;
        IntegralHeight = false;
        Font = new Font("Segoe UI", 9.5f);
    }

    protected override void OnItemCheck(ItemCheckEventArgs ice)
    {
        base.OnItemCheck(ice);
        BeginInvoke(Invalidate);
    }

    protected override void OnDrawItem(DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= Items.Count)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var selected = (eventArgs.State & DrawItemState.Selected) != 0;
        var row = new Rectangle(
            eventArgs.Bounds.Left + 2,
            eventArgs.Bounds.Top + 2,
            eventArgs.Bounds.Width - 5,
            eventArgs.Bounds.Height - 4);
        using var rowPath = UiGeometry.RoundedRectangle(row, 11);
        using var rowBrush = new SolidBrush(
            selected ? Color.FromArgb(28, 64, 91) : AppPalette.SurfaceRaised);
        eventArgs.Graphics.FillPath(rowBrush, rowPath);
        using var rowPen = new Pen(
            selected ? Color.FromArgb(49, 107, 142) : AppPalette.BorderSoft);
        eventArgs.Graphics.DrawPath(rowPen, rowPath);

        var checkedItem = GetItemChecked(eventArgs.Index);
        var checkBounds = new Rectangle(row.Left + 12, row.Top + 10, 19, 19);
        using var checkPath = UiGeometry.RoundedRectangle(checkBounds, 5);
        using var checkBrush = new SolidBrush(
            checkedItem ? AppPalette.Accent : AppPalette.Surface);
        eventArgs.Graphics.FillPath(checkBrush, checkPath);
        using var checkPen = new Pen(
            checkedItem ? AppPalette.AccentLight : AppPalette.Border);
        eventArgs.Graphics.DrawPath(checkPen, checkPath);
        if (checkedItem)
        {
            using var tickPen = new Pen(AppPalette.AccentDark, 2.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            eventArgs.Graphics.DrawLines(
                tickPen,
                new Point[]
                {
                    new Point(checkBounds.Left + 5, checkBounds.Top + 10),
                    new Point(checkBounds.Left + 8, checkBounds.Top + 13),
                    new Point(checkBounds.Left + 14, checkBounds.Top + 6)
                });
        }

        var item = Items[eventArgs.Index];
        var title = item is CategorySuggestion suggestion
            ? suggestion.Name.Replace("Auto • ", string.Empty, StringComparison.Ordinal)
            : item?.ToString() ?? string.Empty;
        var count = item is CategorySuggestion category
            ? category.AppIds.Count.ToString()
            : string.Empty;

        var titleBounds = new Rectangle(
            checkBounds.Right + 11,
            row.Top,
            Math.Max(0, row.Width - 92),
            row.Height);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            title,
            Font,
            titleBounds,
            AppPalette.Text,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);

        var countBounds = new Rectangle(row.Right - 48, row.Top, 34, row.Height);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            count,
            new Font("Segoe UI Semibold", 9f),
            countBounds,
            selected ? AppPalette.AccentLight : AppPalette.Muted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class SteamMark : Control
{
    public SteamMark()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Size = new Size(58, 58);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = UiGeometry.RoundedRectangle(bounds, 17);
        using var brush = new LinearGradientBrush(
            bounds,
            AppPalette.AccentLight,
            AppPalette.Accent,
            135f);
        eventArgs.Graphics.FillPath(brush, path);

        using var linkPen = new Pen(AppPalette.AccentDark, 4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        eventArgs.Graphics.DrawLine(
            linkPen,
            new Point(bounds.Left + 16, bounds.Bottom - 17),
            new Point(bounds.Right - 17, bounds.Top + 17));
        using var nodeBrush = new SolidBrush(AppPalette.AccentDark);
        eventArgs.Graphics.FillEllipse(
            nodeBrush,
            bounds.Right - 27,
            bounds.Top + 9,
            18,
            18);
        eventArgs.Graphics.FillEllipse(
            nodeBrush,
            bounds.Left + 9,
            bounds.Bottom - 27,
            18,
            18);
        using var innerBrush = new SolidBrush(AppPalette.AccentLight);
        eventArgs.Graphics.FillEllipse(
            innerBrush,
            bounds.Right - 22,
            bounds.Top + 14,
            8,
            8);
    }
}

internal sealed class SmoothDataGridView : DataGridView
{
    public SmoothDataGridView()
    {
        DoubleBuffered = true;
    }
}

internal static class WindowsTheme
{
    private const int UseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr window,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr window,
        string? subAppName,
        string? subIdList);

    public static void ApplyDarkTitleBar(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(
            form.Handle,
            UseImmersiveDarkMode,
            ref enabled,
            sizeof(int));
    }

    public static void ApplyDarkExplorerTheme(Control control)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return;
        }

        _ = SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
    }
}

internal enum DialogKind
{
    Information,
    Success,
    Error
}

internal static class ModernDialog
{
    public static void Show(
        IWin32Window owner,
        string title,
        string message,
        string primaryText,
        DialogKind kind = DialogKind.Information)
    {
        using var dialog = new ModernDialogForm(
            title,
            message,
            primaryText,
            null,
            kind);
        dialog.ShowDialog(owner);
    }

    public static bool Confirm(
        IWin32Window owner,
        string title,
        string message,
        string primaryText,
        string secondaryText,
        DialogKind kind = DialogKind.Information)
    {
        using var dialog = new ModernDialogForm(
            title,
            message,
            primaryText,
            secondaryText,
            kind);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    private sealed class ModernDialogForm : Form
    {
        public ModernDialogForm(
            string title,
            string message,
            string primaryText,
            string? secondaryText,
            DialogKind kind)
        {
            Text = title;
            BackColor = AppPalette.BackgroundBottom;
            ForeColor = AppPalette.Text;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(
                590,
                Math.Clamp(
                    268 + message.Count(character => character == '\n') * 17,
                    320,
                    470));

            var background = new GradientBackgroundPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22)
            };
            Controls.Add(background);

            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppPalette.SurfaceRaised,
                GradientColor = AppPalette.Surface,
                BorderColor = AppPalette.Border,
                Radius = 22,
                Padding = new Padding(20)
            };
            background.Controls.Add(card);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            card.Controls.Add(root);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var iconColors = kind switch
            {
                DialogKind.Success => (
                    Color.FromArgb(10, 54, 48),
                    Color.FromArgb(24, 94, 76),
                    AppPalette.Success,
                    "✓"),
                DialogKind.Error => (
                    Color.FromArgb(62, 25, 39),
                    Color.FromArgb(103, 41, 56),
                    Color.FromArgb(251, 113, 133),
                    "!"),
                _ => (
                    Color.FromArgb(10, 48, 68),
                    Color.FromArgb(29, 84, 108),
                    AppPalette.AccentLight,
                    "i")
            };
            var iconCard = new RoundedPanel
            {
                Size = new Size(46, 46),
                Anchor = AnchorStyles.Left,
                FillColor = iconColors.Item1,
                GradientColor = iconColors.Item1,
                BorderColor = iconColors.Item2,
                Radius = 15,
                Margin = Padding.Empty
            };
            iconCard.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = iconColors.Item4,
                ForeColor = iconColors.Item3,
                Font = new Font("Segoe UI Semibold", 16f),
                TextAlign = ContentAlignment.MiddleCenter
            });
            header.Controls.Add(iconCard, 0, 0);
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = title,
                ForeColor = AppPalette.Text,
                Font = new Font("Segoe UI Semibold", 16f),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 1, 0);
            root.Controls.Add(header, 0, 0);

            var messageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = message,
                ForeColor = AppPalette.Muted,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(2, 8, 4, 4)
            };
            root.Controls.Add(messageLabel, 0, 1);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0)
            };

            var primary = new ModernButton
            {
                Text = primaryText,
                Primary = true,
                Size = new Size(150, 42),
                DialogResult = DialogResult.OK,
                Margin = Padding.Empty
            };
            actions.Controls.Add(primary);
            AcceptButton = primary;

            if (!string.IsNullOrWhiteSpace(secondaryText))
            {
                var secondary = new ModernButton
                {
                    Text = secondaryText,
                    Size = new Size(125, 42),
                    DialogResult = DialogResult.Cancel,
                    Margin = new Padding(0, 0, 10, 0)
                };
                actions.Controls.Add(secondary);
                CancelButton = secondary;
            }

            root.Controls.Add(actions, 0, 2);
        }
    }
}
