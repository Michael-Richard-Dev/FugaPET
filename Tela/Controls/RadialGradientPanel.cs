using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace FugaPET_Dev.Tela.Controls;

public class RadialGradientPanel : Panel
{
    [DefaultValue(typeof(Color), "31, 47, 68")]
    public Color CenterColor { get; set; } = Color.FromArgb(31, 47, 68);

    [DefaultValue(typeof(Color), "7, 15, 28")]
    public Color EdgeColor { get; set; } = Color.FromArgb(7, 15, 28);

    public RadialGradientPanel()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using GraphicsPath path = new();
        path.AddRectangle(bounds);

        using PathGradientBrush brush = new(path)
        {
            CenterColor = CenterColor,
            CenterPoint = new PointF(bounds.Width / 2F, bounds.Height / 2F),
            SurroundColors = new[] { EdgeColor },
            FocusScales = new PointF(0.42F, 0.38F)
        };

        e.Graphics.FillRectangle(brush, bounds);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Invalidate();
    }
}



