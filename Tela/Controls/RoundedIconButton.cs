using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FugaPET_Dev.Tela.Controls;

public class RoundedIconButton : Control
{
    private static readonly Color CorBordaPadrao = Color.FromArgb(203, 213, 225);

    private Image? _image;
    private int _borderRadius = 6;
    private int _borderThickness;
    private Color _borderColor = CorBordaPadrao;

    [DefaultValue(6)]
    public int BorderRadius
    {
        get => _borderRadius;
        set
        {
            _borderRadius = Math.Max(0, value);
            Invalidate();
        }
    }

    [DefaultValue(0)]
    public int BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = Math.Max(0, value);
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

    public bool ShouldSerializeBorderColor() => BorderColor != CorBordaPadrao;

    public void ResetBorderColor()
    {
        BorderColor = CorBordaPadrao;
    }

    [DefaultValue(null)]
    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            Invalidate();
        }
    }

    public RoundedIconButton()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(30, 41, 59);

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using SolidBrush backgroundBrush = new(ObterCorFundoPai());
        pevent.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = CriarCaminhoArredondado(bounds, BorderRadius);
        using SolidBrush backgroundBrush = new(BackColor);
        pevent.Graphics.FillPath(backgroundBrush, path);

        if (BorderThickness > 0)
        {
            using Pen borderPen = new(BorderColor, BorderThickness)
            {
                Alignment = PenAlignment.Inset
            };
            pevent.Graphics.DrawPath(borderPen, path);
        }

        int iconLeft = 14;
        int iconSize = Image is null ? 0 : 16;
        if (Image is not null)
        {
            Rectangle imageBounds = new(iconLeft, Math.Max(0, (Height - iconSize) / 2), iconSize, iconSize);
            pevent.Graphics.DrawImage(Image, imageBounds);
        }

        int textLeft = Image is null ? 0 : iconLeft + iconSize + 6;
        Rectangle textBounds = new(textLeft, 0, Math.Max(1, Width - textLeft - 8), Height);
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            textBounds,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
        {
            OnClick(EventArgs.Empty);
        }
    }

    private Color ObterCorFundoPai()
    {
        if (Parent is RoundedPanel roundedPanel)
        {
            return roundedPanel.FillColor;
        }

        return Parent?.BackColor ?? SystemColors.Control;
    }

    private static GraphicsPath CriarCaminhoArredondado(Rectangle rectangle, int radius)
    {
        int diameter = Math.Max(1, radius * 2);
        GraphicsPath path = new();
        Rectangle arc = new(rectangle.Location, new Size(diameter, diameter));

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
