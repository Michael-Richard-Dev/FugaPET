    using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing.Printing;
using System.Drawing.Drawing2D;
using QRCoder;
using System.Text;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Servicos;

/// <summary>
/// Driver Zebra/ZPL de baixo nivel. NAO escolhe impressora: TODOS os metodos exigem o
/// nome da impressora por parametro. Nao ha mais fallback automatico para a impressora
/// padrao do Windows (FindZebraOrDefaultPrinter, EnsureDefaultPrinterAvailable,
/// WarmUpDefaultPrinter e a const "Zebra ZT411" foram removidos) — quem resolve a
/// impressora padrao do terminal e a camada ImpressoraEtiquetaServico (Servicos/Operacao).
/// </summary>
public sealed class ServicoImpressoraZebra
{
    private const int LabelWidthDots = 839;
    private const int LabelHeightDots = 959;
    private const int MateriaPrimaLabelWidthDots = 799;
    private const int MateriaPrimaLabelHeightDots = 1118;
    private const int MateriaPrimaLandscapeWidthDots = 1118;
    private const int MateriaPrimaLandscapeHeightDots = MateriaPrimaLabelWidthDots;
    private static readonly string[] MarcadoresNomeZebra = ["ZEBRA", "ZDESIGNER"];

    public ResultadoEnvioZebra ImprimirTexto(string nomeImpressora, string texto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        string zpl = ConstruirZplTexto(texto);
        return RawPrinterHelper.SendStringToPrinter(nomeImpressora, zpl);
    }

    public void GarantirImpressoraDisponivel(string nomeImpressora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        RawPrinterHelper.EnsurePrinterReady(nomeImpressora);
    }

    public bool TryImpressoraPronta(string nomeImpressora, out ErroImpressaoZebraException? erro)
    {
        erro = null;
        if (string.IsNullOrWhiteSpace(nomeImpressora))
        {
            erro = new ErroImpressaoZebraException(
                CategoriaErroImpressaoZebra.ImpressoraNaoConfigurada,
                "Impressora Zebra não configurada para este terminal.");
            return false;
        }

        return RawPrinterHelper.TryEnsurePrinterReady(nomeImpressora, out erro);
    }

    public ResultadoEnvioZebra AquecerImpressora(string nomeImpressora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        return RawPrinterHelper.SendStringToPrinter(nomeImpressora, "^XA^XZ");
    }

    public ResultadoEnvioZebra ImprimirEtiquetaProducao(string nomeImpressora, DadosEtiquetaProducao etiqueta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        RawPrinterHelper.EnsurePrinterReady(nomeImpressora);
        string zpl = ConstruirZplEtiquetaProducao(etiqueta);
        return RawPrinterHelper.SendStringToPrinter(nomeImpressora, zpl);
    }


    public ResultadoEnvioZebra ImprimirEtiquetaMateriaPrima(string nomeImpressora, DadosEtiquetaMateriaPrima etiqueta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        RawPrinterHelper.EnsurePrinterReady(nomeImpressora);
        string zpl = ConstruirZplEtiquetaMateriaPrimaGrafica(etiqueta);
        return RawPrinterHelper.SendStringToPrinter(nomeImpressora, zpl);
    }

    public ResultadoEnvioZebra ImprimirEtiquetaCaixaProdutoAcabado(string nomeImpressora, DadosEtiquetaCaixaProdutoAcabado etiqueta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        RawPrinterHelper.EnsurePrinterReady(nomeImpressora);
        string zpl = ConstruirZplEtiquetaCaixaProdutoAcabado(etiqueta);
        return RawPrinterHelper.SendStringToPrinter(nomeImpressora, zpl);
    }

    public bool ImpressoraInstalada(string nomeImpressora)
        => !string.IsNullOrWhiteSpace(nomeImpressora)
            && PrinterSettings.InstalledPrinters
                .Cast<string>()
                .Any(nome => string.Equals(nome, nomeImpressora.Trim(), StringComparison.OrdinalIgnoreCase));

    public string? ObterImpressoraZebraInstaladaPreferencial()
    {
        string[] zebrasInstaladas = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Where(EhImpressoraZebra)
            .ToArray();

        if (zebrasInstaladas.Length == 0)
        {
            return null;
        }

        string impressoraPadraoWindows = new PrinterSettings().PrinterName;
        string? zebraPadraoWindows = zebrasInstaladas.FirstOrDefault(nome =>
            string.Equals(nome, impressoraPadraoWindows, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(zebraPadraoWindows))
        {
            return zebraPadraoWindows;
        }

        if (zebrasInstaladas.Length == 1)
        {
            return zebrasInstaladas[0];
        }

        throw new ErroOperacionalEsperadoException(
            "Mais de uma impressora Zebra foi encontrada. Defina a Zebra padrao do Windows ou configure a impressora no terminal local.");
    }

    private static bool EhImpressoraZebra(string nomeImpressora)
        => MarcadoresNomeZebra.Any(marcador =>
            nomeImpressora.Contains(marcador, StringComparison.OrdinalIgnoreCase));

    public string ConstruirZplTexto(string texto)
    {
        string textoSanitizado = SanitizarParaZpl(texto);

        return "^XA\n" +
               $"^PW{LabelWidthDots}\n" +
               $"^LL{LabelHeightDots}\n" +
               "^FWN\n" +
               "^LH0,0\n" +
               "^FO120,120\n" +
               "^A0B,45,45\n" +
               $"^FD{textoSanitizado}^FS\n" +
               "^XZ";
    }

    public string ConstruirZplEtiquetaProducao(DadosEtiquetaProducao etiqueta)
    {
        return "^XA\n" +
               "^CI28\n" +
               $"^PW{LabelWidthDots}\n" +
               $"^LL{LabelHeightDots}\n" +
               "^FWB\n" +
               "^LH0,0\n" +
               "^FO35,330^A0B,24,24^FDOP^FS\n" +
               $"^FO65,330^GB50,150,2^FS^FO76,330^A0B,34,34^FB150,1,0,C^FD{Z(etiqueta.OrdemProducao)}^FS\n" +
               "^FO35,500^A0B,24,24^FDLOTE^FS\n" +
               $"^FO65,500^GB50,165,2^FS^FO76,500^A0B,34,34^FB165,1,0,C^FD{Z(etiqueta.Lote)}^FS\n" +
               "^FO140,35^A0B,24,24^FDPRODUTO^FS\n" +
               $"^FO170,35^GB70,160,2^FS^FO188,35^A0B,38,38^FB160,1,0,C^FD{Z(etiqueta.CodigoProduto)}^FS\n" +
               $"^FO170,180^GB70,735,2^FS^FO188,190^A0B,34,30^FB710,1,0,L^FD{Z(etiqueta.DescricaoProduto)}^FS\n" +
               "^FO270,35^GB38,905,38^FS\n" +
               "^FO278,35^A0B,24,24^FR^FB905,1,0,C^FDDATAS^FS\n" +
               "^FO335,35^A0B,21,21^FDSAIDA ESTUFA^FS\n" +
               "^FO335,245^A0B,21,21^FDCLASSIFICACAO^FS\n" +
               "^FO335,490^A0B,21,21^FDFABRICACAO^FS\n" +
               "^FO335,720^A0B,21,21^FDVENCIMENTO^FS\n" +
               $"^FO370,35^GB54,905,2^FS^FO384,45^A0B,28,28^FD{Z(etiqueta.DataSaidaEstufa)}^FS\n" +
               $"^FO384,250^A0B,28,28^FD{Z(etiqueta.DataClassificacao)}^FS\n" +
               $"^FO384,495^A0B,28,28^FD{Z(etiqueta.DataFabricacao)}^FS\n" +
               $"^FO384,725^A0B,28,28^FD{Z(etiqueta.DataVencimento)}^FS\n" +
               "^FO465,35^GB38,905,38^FS\n" +
               "^FO473,35^A0B,24,24^FR^FB905,1,0,C^FDLEITURA / PRODUCAO^FS\n" +
               $"^FO545,35^GB115,180,2^FS^FO560,35^A0B,25,25^FB180,1,0,C^FDCAIXAS^FS^FO605,35^A0B,34,34^FB180,1,0,C^FD{Z(etiqueta.CaixasPrevistas)}^FS\n" +
               $"^FO545,230^GB115,190,2^FS^FO560,230^A0B,25,25^FB190,1,0,C^FDPACOTES^FS^FO605,230^A0B,34,34^FB190,1,0,C^FD{Z(etiqueta.PacotesPrevistos)}^FS\n" +
               $"^FO545,435^GB115,210,2^FS^FO560,435^A0B,25,25^FB210,1,0,C^FDSALDO^FS^FO605,435^A0B,34,34^FB210,1,0,C^FD{Z(etiqueta.Saldo)}^FS\n" +
               $"^FO545,660^GB115,280,2^FS^FO560,660^A0B,25,25^FB280,1,0,C^FDQTDE / PESO^FS^FO605,660^A0B,34,34^FB280,1,0,C^FD{Z(etiqueta.Quantidade)} / {Z(etiqueta.Peso)}^FS\n" +
               $"^FO715,35^GB95,905,3^FS^FO735,35^A0B,60,60^FB905,1,0,C^FD{Z(etiqueta.CodigoProducao)}^FS\n" +
               "^FO825,35^A0B,22,22^FB905,1,0,C^FDCODIGO DA LEITURA / PRODUCAO^FS\n" +
               "^XZ";
    }


    /// <summary>
    /// ZPL específico da CAIXA de Produto Acabado (HU individual). Layout próprio com rótulos corretos
    /// (OP/ITEM/MATERIAL/LOTE/CAIXA/COD.CAIXA/BRUTO-TARA-LIQUIDO/QTDE/HU SAP/DATA-TERMINAL) + código de barras
    /// Code128 do <c>codigo_caixa_local</c> para rastreabilidade. NÃO altera Entrada nem Semi-Acabado.
    /// </summary>
    public string ConstruirZplEtiquetaCaixaProdutoAcabado(DadosEtiquetaCaixaProdutoAcabado etiqueta)
    {
        string hu = string.IsNullOrWhiteSpace(etiqueta.HandlingUnitSap) ? "PENDENTE" : etiqueta.HandlingUnitSap;
        string codigoBarras = string.IsNullOrWhiteSpace(etiqueta.CodigoCaixaLocal)
            ? "SEM-CODIGO"
            : etiqueta.CodigoCaixaLocal;

        return "^XA\n" +
               "^CI28\n" +
               $"^PW{LabelWidthDots}\n" +
               $"^LL{LabelHeightDots}\n" +
               "^FWN\n" +
               "^LH0,0\n" +
               "^FO30,30^A0N,42,42^FDPRODUTO ACABADO - CAIXA^FS\n" +
               "^FO30,84^GB779,0,3^FS\n" +
               $"^FO30,104^A0N,28,28^FDOP: {Z(etiqueta.OrdemProducao)}    ITEM: {Z(etiqueta.ItemOrdem)}^FS\n" +
               $"^FO30,150^A0N,28,28^FDMATERIAL: {Z(etiqueta.Material)}^FS\n" +
               $"^FO30,192^A0N,24,24^FB779,2,0,L^FD{Z(etiqueta.DescricaoMaterial)}^FS\n" +
               $"^FO30,258^A0N,28,28^FDLOTE: {Z(etiqueta.Lote)}^FS\n" +
               $"^FO30,302^A0N,34,34^FDCAIXA No: {Z(etiqueta.NumeroCaixa)}^FS\n" +
               $"^FO30,348^A0N,28,28^FDCOD. CAIXA: {Z(etiqueta.CodigoCaixaLocal)}^FS\n" +
               "^FO30,392^GB779,0,2^FS\n" +
               $"^FO30,404^A0N,28,28^FDBRUTO: {Z(etiqueta.PesoBruto)}   TARA: {Z(etiqueta.Tara)}   LIQ: {Z(etiqueta.PesoLiquido)}^FS\n" +
               $"^FO30,446^A0N,28,28^FDQTDE: {Z(etiqueta.Quantidade)}^FS\n" +
               $"^FO30,494^A0N,36,36^FDHU SAP: {Z(hu)}^FS\n" +
               $"^FO30,544^A0N,22,22^FD{Z(etiqueta.DataHora)}    TERMINAL: {Z(etiqueta.Terminal)}^FS\n" +
               // Código de barras Code128 do codigo_caixa_local na VERTICAL (orientação B = bottom-up/270°).
               // A faixa ocupa a banda inferior livre; ^BY3 = módulo, 120 = largura da faixa (perpendicular às barras).
               $"^FO60,600^BY3^BCB,120,Y,N,N^FD{Z(codigoBarras)}^FS\n" +
               "^XZ";
    }

    public string ConstruirZplEtiquetaMateriaPrimaGrafica(DadosEtiquetaMateriaPrima etiqueta)
    {
        using Bitmap etiquetaPaisagem = RenderizarEtiquetaMateriaPrimaPaisagem(etiqueta);
        etiquetaPaisagem.RotateFlip(RotateFlipType.Rotate90FlipNone);
        string graphic = ConverterBitmapParaZplGraphicField(etiquetaPaisagem);

        return "^XA\n" +
               "^CI28\n" +
               $"^PW{MateriaPrimaLabelWidthDots}\n" +
               $"^LL{MateriaPrimaLabelHeightDots}\n" +
               "^FO0,0\n" +
               graphic +
               "^FS\n" +
               "^XZ";
    }

    private static Bitmap RenderizarEtiquetaMateriaPrimaPaisagem(DadosEtiquetaMateriaPrima etiqueta)
    {
        Bitmap bitmap = new(MateriaPrimaLandscapeWidthDots, MateriaPrimaLandscapeHeightDots);
        bitmap.SetResolution(203, 203);

        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

        using Pen borderPen = new(Color.Black, 2);
        using Brush textBrush = Brushes.Black;
        using Font titleFont = CriarFonteEtiqueta(38, FontStyle.Regular);
        using Font labelFont = CriarFonteEtiqueta(24, FontStyle.Regular);
        using Font valueFont = CriarFonteEtiqueta(34, FontStyle.Bold);
        using Font fornecedorFont = CriarFonteEtiqueta(40, FontStyle.Bold);
        using Font pesoFont = CriarFonteEtiqueta(46, FontStyle.Bold);

        string titulo = $"{etiqueta.CodigoProduto} - {etiqueta.DescricaoProduto}".Trim(' ', '-');

        graphics.DrawRectangle(borderPen, 18, 18, 1082, 748);
        DesenharTextoCentralizado(graphics, titulo, titleFont, textBrush, new RectangleF(40, 42, 1038, 50));
        graphics.DrawLine(borderPen, 18, 120, 1100, 120);
        graphics.DrawLine(borderPen, 559, 120, 559, 450);
        graphics.DrawLine(borderPen, 18, 230, 1100, 230);
        graphics.DrawLine(borderPen, 18, 340, 1100, 340);
        graphics.DrawLine(borderPen, 18, 450, 1100, 450);
        graphics.DrawLine(borderPen, 285, 450, 285, 766);
        graphics.DrawLine(borderPen, 559, 450, 559, 766);
        graphics.DrawLine(borderPen, 559, 605, 1100, 605);

        DesenharCampoEtiqueta(graphics, labelFont, valueFont, textBrush, 35, 145, 505, "PEDIDO COMPRA", etiqueta.LoteOrigem);
        DesenharCampoEtiqueta(graphics, labelFont, valueFont, textBrush, 595, 145, 470, "LOTE INTERNO", etiqueta.LoteInterno);
        DesenharCampoEtiqueta(graphics, labelFont, valueFont, textBrush, 35, 255, 505, "DATA FABRICAÇÃO", etiqueta.DataFabricacao);
        DesenharCampoEtiqueta(graphics, labelFont, valueFont, textBrush, 595, 255, 470, "DATA VENCIMENTO", etiqueta.DataVencimento);
        DesenharCampoEtiqueta(graphics, labelFont, valueFont, textBrush, 35, 365, 505, "CERTIFICADO SANITÁRIO", etiqueta.CertificadoSanitario);
        DesenharCampoEtiqueta(graphics, labelFont, valueFont, textBrush, 595, 365, 470, "SIF", etiqueta.Sif);

        using Bitmap qrCode = GerarQrCodeBitmap(string.IsNullOrWhiteSpace(etiqueta.ConteudoQrCode) ? titulo : etiqueta.ConteudoQrCode);
        graphics.DrawImage(qrCode, new Rectangle(42, 490, 220, 220));

        DesenharTextoCentralizado(graphics, "FORNECEDOR", labelFont, textBrush, new RectangleF(310, 485, 220, 34));
        DesenharTextoCentralizado(graphics, etiqueta.Fornecedor, fornecedorFont, textBrush, new RectangleF(300, 535, 240, 115));
        DesenharTextoCentralizado(graphics, "N° NF", labelFont, textBrush, new RectangleF(595, 485, 470, 34));
        DesenharTextoCentralizado(graphics, etiqueta.NumeroNotaFiscal, valueFont, textBrush, new RectangleF(595, 535, 470, 50));
        DesenharTextoCentralizado(graphics, "PESO", labelFont, textBrush, new RectangleF(595, 640, 470, 34));
        DesenharTextoCentralizado(graphics, etiqueta.Peso, pesoFont, textBrush, new RectangleF(595, 690, 470, 62));

        return bitmap;
    }

    private static void DesenharCampoEtiqueta(Graphics graphics, Font labelFont, Font valueFont, Brush brush, int x, int y, int width, string label, string value)
    {
        DesenharTextoCentralizado(graphics, label, labelFont, brush, new RectangleF(x, y, width, 32));
        DesenharTextoCentralizado(graphics, value, valueFont, brush, new RectangleF(x, y + 42, width, 48));
    }

    private static void DesenharTextoCentralizado(Graphics graphics, string value, Font font, Brush brush, RectangleF bounds)
    {
        RectangleF boundsSeguros = NormalizarBoundsTexto(graphics, bounds);
        if (boundsSeguros.Width <= 0 || boundsSeguros.Height <= 0)
        {
            return;
        }

        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.LineLimit
        };

        graphics.DrawString(SanitizarTextoGrafico(value), font, brush, boundsSeguros, format);
    }

    private static RectangleF NormalizarBoundsTexto(Graphics graphics, RectangleF bounds)
    {
        float x = MathF.Max(0, bounds.X);
        float y = MathF.Max(0, bounds.Y);
        float width = MathF.Min(bounds.Width - MathF.Max(0, -bounds.X), graphics.VisibleClipBounds.Width - x);
        float height = MathF.Min(bounds.Height - MathF.Max(0, -bounds.Y), graphics.VisibleClipBounds.Height - y);

        return new RectangleF(x, y, MathF.Max(0, width), MathF.Max(0, height));
    }

    private static string SanitizarTextoGrafico(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string texto = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return texto.Length <= 180 ? texto : texto[..180];
    }

    private static Font CriarFonteEtiqueta(float size, FontStyle style)
        => new("Arial", size, style, GraphicsUnit.Pixel);

    private static Bitmap GerarQrCodeBitmap(string value)
    {
        using QRCodeGenerator generator = new();
        string valorSeguro = SanitizarTextoGrafico(value);
        using QRCodeData data = generator.CreateQrCode(
            string.IsNullOrWhiteSpace(valorSeguro) ? "FugaPET" : valorSeguro,
            QRCodeGenerator.ECCLevel.Q);
        using QRCode qrCode = new(data);
        return qrCode.GetGraphic(4, Color.Black, Color.White, drawQuietZones: true);
    }

    private static string ConverterBitmapParaZplGraphicField(Bitmap bitmap)
    {
        int widthBytes = (bitmap.Width + 7) / 8;
        int totalBytes = widthBytes * bitmap.Height;
        StringBuilder hex = new(totalBytes * 2);

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int byteX = 0; byteX < widthBytes; byteX++)
            {
                byte value = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int x = byteX * 8 + bit;
                    if (x >= bitmap.Width)
                    {
                        continue;
                    }

                    Color pixel = bitmap.GetPixel(x, y);
                    int luminance = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    if (luminance < 160)
                    {
                        value |= (byte)(0x80 >> bit);
                    }
                }

                hex.Append(value.ToString("X2"));
            }
        }

        return $"^GFA,{totalBytes},{totalBytes},{widthBytes},{hex}\n";
    }
    public string ConstruirZplEtiquetaMateriaPrima(DadosEtiquetaMateriaPrima etiqueta)
    {
        string titulo = $"{etiqueta.CodigoProduto} - {etiqueta.DescricaoProduto}".Trim(' ', '-');
        string qr = string.IsNullOrWhiteSpace(etiqueta.ConteudoQrCode) ? titulo : etiqueta.ConteudoQrCode;

        return "^XA\n" +
               "^CI28\n" +
               $"^PW{MateriaPrimaLandscapeWidthDots}\n" +
               $"^LL{MateriaPrimaLandscapeHeightDots}\n" +
               "^FWN\n" +
               "^LH0,0\n" +
               CaixaMateriaPrima(18, 18, 1082, 748, 2) +
               TextoMateriaPrima(40, 42, 1038, 38, 38, 1, "C", titulo) +
               CaixaMateriaPrima(18, 120, 1082, 0, 2) +
               CaixaMateriaPrima(559, 120, 0, 330, 2) +
               CaixaMateriaPrima(18, 230, 1082, 0, 2) +
               CaixaMateriaPrima(18, 340, 1082, 0, 2) +
               CaixaMateriaPrima(18, 450, 1082, 0, 2) +
               CaixaMateriaPrima(285, 450, 0, 316, 2) +
               CaixaMateriaPrima(559, 450, 0, 316, 2) +
               CaixaMateriaPrima(559, 605, 541, 0, 2) +
               CampoMateriaPrima(35, 145, 505, "PEDIDO COMPRA", etiqueta.LoteOrigem) +
               CampoMateriaPrima(595, 145, 470, "LOTE INTERNO", etiqueta.LoteInterno) +
               CampoMateriaPrima(35, 255, 505, "DATA FABRICAÇÃO", etiqueta.DataFabricacao) +
               CampoMateriaPrima(595, 255, 470, "DATA VENCIMENTO", etiqueta.DataVencimento) +
               CampoMateriaPrima(35, 365, 505, "CERTIFICADO SANITÁRIO", etiqueta.CertificadoSanitario) +
               CampoMateriaPrima(595, 365, 470, "SIF", etiqueta.Sif) +
               QrCodeMateriaPrima(42, 490, 180, qr) +
               TextoMateriaPrima(310, 485, 220, 24, 24, 1, "C", "FORNECEDOR") +
               TextoMateriaPrima(300, 535, 240, 40, 36, 3, "C", etiqueta.Fornecedor) +
               TextoMateriaPrima(595, 485, 470, 24, 24, 1, "C", "N° NF") +
               TextoMateriaPrima(595, 535, 470, 34, 34, 1, "C", etiqueta.NumeroNotaFiscal) +
               TextoMateriaPrima(595, 640, 470, 24, 24, 1, "C", "PESO") +
               TextoMateriaPrima(595, 690, 470, 46, 46, 1, "C", etiqueta.Peso) + "KG" +
               "^XZ";
    }

    private static string CaixaMateriaPrima(int x, int y, int width, int height, int thickness)
    {
        return $"^FO{x},{y}^GB{width},{height},{thickness}^FS\n";
    }

    private static string CampoMateriaPrima(int x, int y, int width, string label, string value)
    {
        return TextoMateriaPrima(x, y, width, 24, 24, 1, "C", label) +
               TextoMateriaPrima(x, y + 42, width, 34, 34, 1, "C", value);
    }

    private static string TextoMateriaPrima(int x, int y, int width, int fontHeight, int fontWidth, int lines, string align, string value)
    {
        return $"^FO{x},{y}^A0N,{fontHeight},{fontWidth}^FB{width},{lines},0,{align}^FD{Z(value)}^FS\n";
    }

    private static string QrCodeMateriaPrima(int x, int y, int size, string value)
    {
        return $"^FO{x},{y}^BQN,2,6^FDLA,{Z(value)}^FS\n";
    }

    private static string SanitizarParaZpl(string texto)
    {
        return texto
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace("^", string.Empty, StringComparison.Ordinal)
            .Replace("~", string.Empty, StringComparison.Ordinal);
    }

    private static string Z(string text)
    {
        return SanitizarParaZpl(text);
    }

    private static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private sealed class DocInfo
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string DocumentName = "FugaPET_Dev ZPL";

            [MarshalAs(UnmanagedType.LPStr)]
            public string? OutputFile;

            [MarshalAs(UnmanagedType.LPStr)]
            public string DataType = "RAW";
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr defaultPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr printerHandle);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int StartDocPrinter(IntPtr printerHandle, int level, [In] DocInfo docInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr printerHandle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr printerHandle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr printerHandle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr printerHandle, IntPtr data, int count, out int written);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetPrinter(IntPtr printerHandle, int level, IntPtr printerInfo, int bufferSize, out int needed);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PrinterInfo2
        {
            public IntPtr ServerName;
            public IntPtr PrinterName;
            public IntPtr ShareName;
            public IntPtr PortName;
            public IntPtr DriverName;
            public IntPtr Comment;
            public IntPtr Location;
            public IntPtr DevMode;
            public IntPtr SepFile;
            public IntPtr PrintProcessor;
            public IntPtr Datatype;
            public IntPtr Parameters;
            public IntPtr SecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint Jobs;
            public uint AveragePagesPerMinute;
        }

        private const uint PrinterAttributeWorkOffline = 0x00000400;
        private const uint PrinterStatusPaused = 0x00000001;
        private const uint PrinterStatusError = 0x00000002;
        private const uint PrinterStatusPaperJam = 0x00000008;
        private const uint PrinterStatusPaperOut = 0x00000010;
        private const uint PrinterStatusPaperProblem = 0x00000040;
        private const uint PrinterStatusOffline = 0x00000080;
        private const uint PrinterStatusOutputBinFull = 0x00000800;
        private const uint PrinterStatusNotAvailable = 0x00001000;
        private const uint PrinterStatusNoToner = 0x00040000;
        private const uint PrinterStatusUserIntervention = 0x00100000;
        private const uint PrinterStatusDoorOpen = 0x00400000;

        private const uint BlockingPrinterStatuses =
            PrinterStatusPaused |
            PrinterStatusError |
            PrinterStatusPaperJam |
            PrinterStatusPaperOut |
            PrinterStatusPaperProblem |
            PrinterStatusOffline |
            PrinterStatusOutputBinFull |
            PrinterStatusNotAvailable |
            PrinterStatusNoToner |
            PrinterStatusUserIntervention |
            PrinterStatusDoorOpen;

        public static void EnsurePrinterReady(string printerName)
        {
            if (!OpenPrinter(printerName, out IntPtr printerHandle, IntPtr.Zero))
            {
                ThrowLastWin32Error(
                    CategoriaErroImpressaoZebra.FalhaAbrirImpressora,
                    $"Não foi possível abrir a impressora Zebra '{printerName}'. Verifique se ela está instalada e acessível.");
            }

            try
            {
                PrinterInfo2 printerInfo = GetPrinterInfo(printerHandle);
                bool isWorkOffline = (printerInfo.Attributes & PrinterAttributeWorkOffline) != 0;
                bool hasBlockingStatus = (printerInfo.Status & BlockingPrinterStatuses) != 0;

                if (isWorkOffline || hasBlockingStatus)
                {
                    throw new ErroImpressaoZebraException(
                        CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
                        "A impressora Zebra está offline, pausada ou com erro. Verifique a fila de impressão e a conexão física.");
                }
            }
            finally
            {
                ClosePrinter(printerHandle);
            }
        }

        public static bool TryEnsurePrinterReady(
            string printerName,
            out ErroImpressaoZebraException? erro)
        {
            erro = null;
            if (!OpenPrinter(printerName, out IntPtr printerHandle, IntPtr.Zero))
            {
                erro = CreateLastWin32Error(
                    CategoriaErroImpressaoZebra.FalhaAbrirImpressora,
                    $"Não foi possível abrir a impressora Zebra '{printerName}'. Verifique se ela está instalada e acessível.");
                return false;
            }

            try
            {
                if (!TryGetPrinterInfo(printerHandle, out PrinterInfo2 printerInfo, out erro))
                {
                    return false;
                }

                bool isWorkOffline = (printerInfo.Attributes & PrinterAttributeWorkOffline) != 0;
                bool hasBlockingStatus = (printerInfo.Status & BlockingPrinterStatuses) != 0;

                if (isWorkOffline || hasBlockingStatus)
                {
                    erro = new ErroImpressaoZebraException(
                        CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
                        "A impressora Zebra está offline, pausada ou com erro. Verifique a fila de impressão e a conexão física.");
                    return false;
                }

                return true;
            }
            finally
            {
                ClosePrinter(printerHandle);
            }
        }

        private static PrinterInfo2 GetPrinterInfo(IntPtr printerHandle)
        {
            GetPrinter(printerHandle, 2, IntPtr.Zero, 0, out int needed);
            if (needed <= 0)
            {
                ThrowLastWin32Error(
                    CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
                    "Não foi possível consultar o status da impressora Zebra.");
            }

            IntPtr printerInfoBuffer = Marshal.AllocHGlobal(needed);

            try
            {
                if (!GetPrinter(printerHandle, 2, printerInfoBuffer, needed, out _))
                {
                    ThrowLastWin32Error(
                        CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
                        "Não foi possível consultar o status da impressora Zebra.");
                }

                return Marshal.PtrToStructure<PrinterInfo2>(printerInfoBuffer);
            }
            finally
            {
                Marshal.FreeHGlobal(printerInfoBuffer);
            }
        }

        private static bool TryGetPrinterInfo(
            IntPtr printerHandle,
            out PrinterInfo2 printerInfo,
            out ErroImpressaoZebraException? erro)
        {
            printerInfo = default;
            erro = null;

            GetPrinter(printerHandle, 2, IntPtr.Zero, 0, out int needed);
            if (needed <= 0)
            {
                erro = CreateLastWin32Error(
                    CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
                    "Não foi possível consultar o status da impressora Zebra.");
                return false;
            }

            IntPtr printerInfoBuffer = Marshal.AllocHGlobal(needed);

            try
            {
                if (!GetPrinter(printerHandle, 2, printerInfoBuffer, needed, out _))
                {
                    erro = CreateLastWin32Error(
                        CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
                        "Não foi possível consultar o status da impressora Zebra.");
                    return false;
                }

                printerInfo = Marshal.PtrToStructure<PrinterInfo2>(printerInfoBuffer);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(printerInfoBuffer);
            }
        }

        public static ResultadoEnvioZebra SendStringToPrinter(string printerName, string text)
        {
            EnsurePrinterReady(printerName);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return SendBytesToPrinter(printerName, bytes);
        }

        private static ResultadoEnvioZebra SendBytesToPrinter(string printerName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out IntPtr printerHandle, IntPtr.Zero))
            {
                ThrowLastWin32Error(
                    CategoriaErroImpressaoZebra.FalhaAbrirImpressora,
                    $"Não foi possível abrir a impressora Zebra '{printerName}'. Verifique se ela está instalada e acessível.");
            }

            try
            {
                var docInfo = new DocInfo();

                if (StartDocPrinter(printerHandle, 1, docInfo) <= 0)
                {
                    ThrowLastWin32Error(
                        CategoriaErroImpressaoZebra.FalhaIniciarDocumento,
                        "Não foi possível iniciar o documento de impressão na Zebra.");
                }

                try
                {
                    if (!StartPagePrinter(printerHandle))
                    {
                        ThrowLastWin32Error(
                            CategoriaErroImpressaoZebra.FalhaIniciarDocumento,
                            "Não foi possível iniciar a página de impressão na Zebra.");
                    }

                    try
                    {
                        int blocosEnviados = WritePrinterEmBlocos(printerHandle, bytes);
                        return new ResultadoEnvioZebra(bytes.Length, blocosEnviados);
                    }
                    finally
                    {
                        EndPagePrinter(printerHandle);
                    }
                }
                finally
                {
                    EndDocPrinter(printerHandle);
                }
            }
            finally
            {
                ClosePrinter(printerHandle);
            }
        }

        private static int WritePrinterEmBlocos(IntPtr printerHandle, byte[] bytes)
        {
            const int tamanhoBloco = 16 * 1024;
            IntPtr buffer = Marshal.AllocCoTaskMem(tamanhoBloco);
            int blocosEnviados = 0;

            try
            {
                for (int offset = 0; offset < bytes.Length; offset += tamanhoBloco)
                {
                    int count = Math.Min(tamanhoBloco, bytes.Length - offset);
                    Marshal.Copy(bytes, offset, buffer, count);
                    if (!WritePrinter(printerHandle, buffer, count, out int written))
                    {
                        ThrowLastWin32Error(
                            CategoriaErroImpressaoZebra.FalhaEnviarDados,
                            "Não foi possível enviar dados RAW para a impressora Zebra.");
                    }

                    if (written != count)
                    {
                        throw new ErroImpressaoZebraException(
                            CategoriaErroImpressaoZebra.EscritaParcial,
                            $"Escrita parcial na impressora Zebra. Esperado {count} bytes, enviado {written} bytes.",
                            Marshal.GetLastWin32Error());
                    }

                    blocosEnviados++;
                }

                return blocosEnviados;
            }
            finally
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }

        private static void ThrowLastWin32Error(CategoriaErroImpressaoZebra categoria, string message)
            => throw CreateLastWin32Error(categoria, message);

        private static ErroImpressaoZebraException CreateLastWin32Error(CategoriaErroImpressaoZebra categoria, string message)
        {
            int errorCode = Marshal.GetLastWin32Error();
            return new ErroImpressaoZebraException(categoria, $"{message} Código Windows: {errorCode}.", errorCode);
        }
    }
}

public sealed record ResultadoEnvioZebra(int TamanhoBytes, int BlocosEnviados)
{
    public static ResultadoEnvioZebra Vazio { get; } = new(0, 0);
}

public enum CategoriaErroImpressaoZebra
{
    ImpressoraNaoConfigurada,
    ImpressoraNaoInstalada,
    ImpressoraOfflinePausada,
    FalhaAbrirImpressora,
    FalhaIniciarDocumento,
    FalhaEnviarDados,
    EscritaParcial,
    UsuarioSemPermissao
}

public sealed class ErroImpressaoZebraException : Exception
{
    public ErroImpressaoZebraException(
        CategoriaErroImpressaoZebra categoria,
        string mensagem,
        int? codigoWin32 = null,
        Exception? innerException = null)
        : base(mensagem, innerException)
    {
        Categoria = categoria;
        CodigoWin32 = codigoWin32;
    }

    public CategoriaErroImpressaoZebra Categoria { get; }
    public int? CodigoWin32 { get; }
}












