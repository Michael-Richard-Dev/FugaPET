using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FugaPET_Dev.Tela;

internal sealed class ActionPillButton : Control
{
    private string _primaryText = string.Empty;
    private string _keyHint = string.Empty;
    private string _iconGlyph = string.Empty;
    private string _iconFontFamily = "Segoe MDL2 Assets";
    private Image? _iconImage;
    private Color _baseBackColor = Color.White;
    private Color _baseForeColor = Color.FromArgb(17, 24, 39);

    [DefaultValue("")]
    public string PrimaryText
    {
        get => _primaryText;
        set
        {
            _primaryText = value;
            Invalidate();
        }
    }

    [DefaultValue("")]
    public string KeyHint
    {
        get => _keyHint;
        set
        {
            _keyHint = value;
            Invalidate();
        }
    }

    [DefaultValue("")]
    public string IconGlyph
    {
        get => _iconGlyph;
        set
        {
            _iconGlyph = value;
            Invalidate();
        }
    }

    [DefaultValue("Segoe MDL2 Assets")]
    public string IconFontFamily
    {
        get => _iconFontFamily;
        set
        {
            _iconFontFamily = value;
            Invalidate();
        }
    }

    [DefaultValue(null)]
    public Image? IconImage
    {
        get => _iconImage;
        set
        {
            _iconImage = value;
            Invalidate();
        }
    }

    [DefaultValue(typeof(Color), "White")]
    public Color BaseBackColor
    {
        get => _baseBackColor;
        set
        {
            _baseBackColor = value;
            Invalidate();
        }
    }

    [DefaultValue(typeof(Color), "17, 24, 39")]
    public Color BaseForeColor
    {
        get => _baseForeColor;
        set
        {
            _baseForeColor = value;
            Invalidate();
        }
    }

    public ActionPillButton()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using GraphicsPath path = BuildRoundedPath(rect, 5);
        using SolidBrush backgroundBrush = new(BaseBackColor);
        e.Graphics.FillPath(backgroundBrush, path);

        if (BaseBackColor.ToArgb() == Color.White.ToArgb())
        {
            using Pen borderPen = new(Color.FromArgb(226, 231, 238));
            e.Graphics.DrawPath(borderPen, path);
        }

        Rectangle iconRectangle = new(10, 0, 24, Height);
        if (IconImage is not null)
        {
            Rectangle imageRectangle = new(
                iconRectangle.Left + 4,
                iconRectangle.Top + Math.Max(0, (iconRectangle.Height - 16) / 2),
                16,
                16);
            e.Graphics.DrawImage(IconImage, imageRectangle);
        }
        else
        {
            using Font iconFont = new(IconFontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            TextRenderer.DrawText(
                e.Graphics,
                IconGlyph,
                iconFont,
                iconRectangle,
                BaseForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        Rectangle keyRectangle = new(Math.Max(0, Width - 42), 0, 34, Height);
        TextRenderer.DrawText(
            e.Graphics,
            KeyHint,
            Font,
            keyRectangle,
            BaseForeColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        Rectangle textRectangle = new(36, 0, Math.Max(10, Width - 80), Height);
        TextRenderer.DrawText(
            e.Graphics,
            PrimaryText,
            Font,
            textRectangle,
            BaseForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(1, radius * 2);
        GraphicsPath path = new();
        Rectangle arc = new(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}


