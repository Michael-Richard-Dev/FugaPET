using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace FugaPET_Dev.Tela.Controls;

public class RoundedPanel : Panel
{
    private Color _borderColor = Color.FromArgb(226, 231, 238);
    private Color _fillColor = Color.White;

    [DefaultValue(8)]
    public int BorderRadius { get; set; } = 8;

    [DefaultValue(1)]
    public int BorderThickness { get; set; } = 1;

    [DefaultValue(typeof(Color), "226, 231, 238")]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor == value)
            {
                return;
            }

            _borderColor = value;
            Invalidate();
        }
    }

    [DefaultValue(typeof(Color), "White")]
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value)
            {
                return;
            }

            _fillColor = value;
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool ShadowEnabled { get; set; }

    [DefaultValue(typeof(Color), "30, 15, 23, 42")]
    public Color ShadowColor { get; set; } = Color.FromArgb(30, 15, 23, 42);

    [DefaultValue(0)]
    public int ShadowOffsetX { get; set; } = 0;

    [DefaultValue(3)]
    public int ShadowOffsetY { get; set; } = 3;

    [DefaultValue(6)]
    public int ShadowBlur { get; set; } = 6;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle contentRectangle = ClientRectangle;
        contentRectangle.Width -= 1;
        contentRectangle.Height -= 1;

        if (ShadowEnabled)
        {
            Rectangle shadowRectangle = new(
                contentRectangle.X + ShadowOffsetX + 1,
                contentRectangle.Y + ShadowOffsetY + 1,
                Math.Max(1, contentRectangle.Width - ShadowBlur),
                Math.Max(1, contentRectangle.Height - ShadowBlur));

            using GraphicsPath shadowPath = CreateRoundedPath(shadowRectangle, BorderRadius);
            using SolidBrush shadowBrush = new(ShadowColor);
            e.Graphics.FillPath(shadowBrush, shadowPath);

            contentRectangle.Width -= ShadowBlur;
            contentRectangle.Height -= ShadowBlur;
        }
        else
        {
            ShadowBlur = 0;
            ShadowOffsetX = 0;
            ShadowOffsetY = 0;
        }

        using GraphicsPath fillPath = CreateRoundedPath(contentRectangle, BorderRadius);
        using SolidBrush fillBrush = new(FillColor);
        e.Graphics.FillPath(fillBrush, fillPath);

        if (BorderThickness > 0)
        {
            using Pen borderPen = new(BorderColor, BorderThickness);
            e.Graphics.DrawPath(borderPen, fillPath);
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Invalidate();
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rectangle, int radius)
    {
        GraphicsPath path = new();
        int diameter = Math.Max(1, radius * 2);
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



