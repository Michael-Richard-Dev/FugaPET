using System.ComponentModel;
using System.Drawing.Drawing2D;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela;

/// <summary>
/// Botao com quinas arredondadas de verdade (owner-draw com anti-aliasing), sem usar
/// Region/clip do GDI — que deixava "tracinhos" brancos nas quinas e a borda cortada.
/// As quinas externas ao arredondamento sao pintadas com a cor do container pai
/// (FillColor do RoundedPanel, quando aplicavel), de forma deterministica e sem residuo.
/// O preenchimento vem de <see cref="CorPreenchimento"/> e, quando
/// <c>FlatAppearance.BorderSize &gt; 0</c>, a borda arredondada usa <c>FlatAppearance.BorderColor</c>.
/// Mantem todo o comportamento de <see cref="Button"/> (Click, DialogResult, mnemonicos).
/// </summary>
internal sealed class RoundedButton : Button
{
    private int _raio = 7;
    private Color _corPreenchimento = Color.White;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    /// <summary>Raio das quinas, em pixels.</summary>
    [DefaultValue(7)]
    public int Raio
    {
        get => _raio;
        set
        {
            _raio = Math.Max(0, value);
            Invalidate();
        }
    }

    /// <summary>Cor de preenchimento do botao.</summary>
    [DefaultValue(typeof(Color), "White")]
    public Color CorPreenchimento
    {
        get => _corPreenchimento;
        set
        {
            _corPreenchimento = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Pinta as quinas com a cor real do container pai (deterministico, sem residuo).
        e.Graphics.Clear(ResolverCorFundoPai());

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = BuildRoundedPath(rect, _raio);

        using (SolidBrush preenchimento = new(CorPreenchimento))
        {
            e.Graphics.FillPath(preenchimento, path);
        }

        if (FlatAppearance.BorderSize > 0)
        {
            using Pen borda = new(FlatAppearance.BorderColor, FlatAppearance.BorderSize);
            e.Graphics.DrawPath(borda, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private Color ResolverCorFundoPai()
    {
        if (Parent is RoundedPanel painel)
        {
            return painel.FillColor;
        }

        if (Parent is not null && Parent.BackColor.A == 255)
        {
            return Parent.BackColor;
        }

        return CorPreenchimento.A == 255 ? CorPreenchimento : Color.White;
    }

    private static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
    {
        GraphicsPath path = new();

        if (radius <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
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
