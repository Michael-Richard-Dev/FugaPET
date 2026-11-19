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
        string codigoLegivel = string.IsNullOrWhiteSpace(etiqueta.CodigoCaixaLocal)
            ? "SEM-CODIGO"
            : etiqueta.CodigoCaixaLocal;

        // A BARRA CARREGA A HU DO SAP. Pedido do usuario em 19/11/2026: bipar a etiqueta tem de
        // devolver a Handling Unit, que e como a caixa existe no SAP — era o codigo local que saia, e
        // ele nao serve para quem le a etiqueta do lado de la.
        //
        // Isso so e possivel porque a etiqueta passou a ser impressa DEPOIS da confirmacao: a
        // elegibilidade exige HU nao vazia, entao aqui ela sempre existe. Se um dia alguem imprimir
        // antes, a barra sairia com "PENDENTE" — e por isso o fallback abaixo NAO codifica: sem HU
        // nao ha barra, porque uma barra que devolve a palavra PENDENTE e pior que nenhuma.
        string codigoBarras = string.IsNullOrWhiteSpace(etiqueta.HandlingUnitSap)
            ? string.Empty
            : etiqueta.HandlingUnitSap.Trim();

        // Geometria convertida de Relatorio/EtiquetaProduto8x5.rdlc, caixa por caixa. O RDLC esta em
        // polegadas; aqui tudo vira dots a 203 dpi (8x5 cm = 3,15 x 1,97 pol = 640 x 400 dots).
        const int Dpi = 203;
        static int P(double polegadas) => (int)Math.Round(polegadas * Dpi);

        int largura = P(3.15), altura = P(1.97);

        // Caixa com borda e texto centralizado em duas linhas (rotulo em cima, valor embaixo),
        // como no RDLC — ali cada Textbox traz "ROTULO\r\nVALOR".
        static string CaixaDupla(int x, int y, int w, int h, string rotulo, string valor)
            => $"^FO{x},{y}^GB{w},{h},2^FS\n"
             + $"^FO{x},{y + 8}^A0N,20,20^FB{w},1,0,C^FD{SanitizarParaZpl(rotulo)}^FS\n"
             + $"^FO{x},{y + 32}^A0N,26,26^FB{w},1,0,C^FD{SanitizarParaZpl(valor)}^FS\n";

        // GATE 130B: a etiqueta passa a ter duas regioes — a GRADE de dados, a esquerda, e a FAIXA do
        // codigo de barras, vertical, a direita. Tudo o que e texto vive dentro da grade; a faixa e
        // exclusiva da barra, para ela poder correr pela altura inteira sem esbarrar em nada.
        int faixaBarraLargura = AlturaBarrasCaixa + P(0.04);
        int faixaBarraX = largura - faixaBarraLargura;

        int colEsqX = P(0.03);
        int colDirW = P(1.06);
        int colDirX = faixaBarraX - P(0.03) - colDirW;
        int colEsqW = colDirX - P(0.02) - colEsqX;
        int larguraGrade = (colDirX + colDirW) - colEsqX;

        var zpl = new StringBuilder();
        zpl.Append("^XA\n^CI28\n")
           .Append($"^PW{largura}\n^LL{altura}\n^FWN\n^LH0,0\n");

        // --- Faixa superior: aviso do EAN a esquerda, lote a direita ---
        // O EAN do produto nao existe no sistema. Nao se imprime barra sem origem: um EAN errado
        // e lido pelo cliente como outro produto. Fica o aviso no lugar reservado.
        zpl.Append($"^FO{colEsqX + P(0.02)},{P(0.06)}^A0N,18,18^FB{colEsqW},2,0,L^FDEAN DO PRODUTO\\&NAO CADASTRADO^FS\n");

        zpl.Append($"^FO{colDirX},{P(0.05)}^GB{colDirW},{P(0.19)},2^FS\n")
           .Append($"^FO{colDirX},{P(0.08)}^A0N,22,22^FB{colDirW},1,0,C^FDLote: {Z(etiqueta.Lote)}^FS\n");

        // --- Descricao do produto, faixa inteira da GRADE ---
        zpl.Append($"^FO{colEsqX},{P(0.26)}^GB{larguraGrade},{P(0.18)},2^FS\n")
           .Append($"^FO{colEsqX},{P(0.29)}^A0N,24,24^FB{larguraGrade},1,0,C^FD{Z(etiqueta.DescricaoMaterial)}^FS\n");

        // --- Grade: esquerda com identificacao e datas, direita com pesos e HU ---
        // GATE 130B: as linhas voltaram a crescer (0,33 pol) porque a barra saiu do rodape; o que
        // elas perderam foi LARGURA, para abrir a faixa vertical da direita.
        zpl.Append(CaixaDupla(colEsqX, P(0.45), colEsqW, P(0.33), "CODIGO DO PRODUTO", etiqueta.Material))
           .Append(CaixaDupla(colDirX, P(0.45), colDirW, P(0.33), "PESO BRUTO", etiqueta.PesoBruto))
           .Append(CaixaDupla(colEsqX, P(0.78), colEsqW, P(0.33), "DATA DE FABRICACAO", SemOrigem))
           .Append(CaixaDupla(colDirX, P(0.78), colDirW, P(0.33), "PESO LIQUIDO", etiqueta.PesoLiquido))
           .Append(CaixaDupla(colEsqX, P(1.11), colEsqW, P(0.33), "DATA DE VALIDADE", SemOrigem))
           .Append(CaixaDupla(colDirX, P(1.11), colDirW, P(0.33), "HU SAP", hu));

        // --- Quantidade de pacotes, faixa inteira da grade ---
        zpl.Append($"^FO{colEsqX},{P(1.45)}^GB{larguraGrade},{P(0.20)},2^FS\n")
           .Append($"^FO{colEsqX},{P(1.48)}^A0N,22,22^FB{larguraGrade},1,0,C^FDQtd. Pacotes: {Z(etiqueta.Quantidade)}^FS\n");

        // --- Codigo de barras VERTICAL na faixa da direita, ancorado no TOPO ---
        // GATE 130C, com DUAS correcoes medidas numa etiqueta de calibracao impressa em 18/11/2026.
        //
        // 1. EM ^BCB O SIMBOLO CRESCE PARA BAIXO, no sentido do Y crescente — nao para cima, como o
        //    comentario original afirmava. A calibracao nasceu em Y=540 e saiu de 540 para baixo.
        //    Era por isso que a barra caia fora do rotulo e ia parar na etiqueta SEGUINTE: a origem
        //    estava no rodape. Logo, para encostar no topo, a origem E o topo.
        //
        // 2. O MODO AUTOMATICO ('A') NAO COMPRIME nesta impressora. Medidas com regua: a barra com
        //    modo e a sem modo sairam do MESMO tamanho, 5,0 cm — os 200 modulos do subconjunto B.
        //    Por isso o parametro saiu daqui e a estimativa voltou a ser a conservadora.
        //
        // Consequencia das duas: 15 caracteres na vertical medem 5,0 cm numa etiqueta de 5,0 cm de
        // altura. Nao ha posicionamento que resolva — faltaria ate a zona de silencio das pontas.
        //
        // Com a HU no lugar do codigo local o simbolo ENCURTOU: as HUs reais do SAP tem 9 digitos
        // (ex.: 300015994) e as simuladas 10 caracteres, contra os 12 do codigo local sem prefixo.
        // A guarda de estreitamento continua, porque o tamanho da HU e definido pelo SAP e nao por
        // nos — se um dia vier maior, a barra aperta em vez de sair cortada.
        // Primeiro o MODULO, com a folga minima de cada ponta reservada; depois a posicao, que sai da
        // centralizacao. A ordem importa: a margem nao e dado de entrada, e consequencia do tamanho
        // que o simbolo acabou tendo.
        int cursoDisponivel = altura - (2 * MargemMinimaFaixaBarra);
        int moduloBarra = EscolherModuloBarraCaixa(codigoBarras, cursoDisponivel);
        int cursoBarra = EstimarLarguraCode128Dots(codigoBarras, moduloBarra);
        int topoBarra = TopoCentralizadoBarraCaixa(altura, cursoBarra);

        // Sem HU nao se imprime barra nenhuma. Ver o comentario de codigoBarras: codificar a palavra
        // "PENDENTE" faria o leitor devolver um texto que nao identifica caixa alguma, e o operador
        // descobriria isso so no destino.
        if (codigoBarras.Length > 0)
        {
            zpl.Append($"^FO{faixaBarraX},{topoBarra}^BY{moduloBarra}^BCB,{AlturaBarrasCaixa},N,N,N^FD{Z(codigoBarras)}^FS\n");
        }

        // Leitura humana no rodape da grade: SO o codigo local.
        //
        // A HU chegou a vir junto aqui, pela convencao de que o numero impresso sob um codigo de
        // barras e o conteudo dele. A convencao NAO se aplica a este layout: a barra e vertical, na
        // faixa da direita, e este rodape fica embaixo da grade, a esquerda — nao ha nada "sob a
        // barra". O que sobrou foi a HU aparecendo duas vezes na mesma etiqueta, aqui e na celula
        // "HU SAP" da grade. O usuario apontou a repeticao na etiqueta impressa em 19/11/2026.
        //
        // Cada identificador tem UM lugar: a HU na celula rotulada, o codigo local aqui.
        zpl.Append($"^FO{colEsqX},{P(1.70)}^A0N,20,20^FB{larguraGrade},1,0,C^FD{Z(codigoLegivel)}^FS\n")
           .Append("^XZ");

        return zpl.ToString();
    }

    /// <summary>Texto de campo sem origem no sistema — nunca preenchido por suposicao.</summary>
    private const string SemOrigem = "NAO INFORMADA";

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

    public ResultadoEnvioZebra ImprimirEtiquetaPaleteProdutoAcabado(
        string nomeImpressora, DadosEtiquetaPaleteProdutoAcabado etiqueta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeImpressora);
        RawPrinterHelper.EnsurePrinterReady(nomeImpressora);
        string zpl = ConstruirZplEtiquetaPaleteProdutoAcabado(etiqueta);
        return RawPrinterHelper.SendStringToPrinter(nomeImpressora, zpl);
    }

    /// <summary>
    /// Etiqueta do palete: identificacao, totais e o bloco de lotes com a quantidade de caixas de
    /// cada um. Code128 do codigo de barras do palete para leitura na expedicao.
    /// <para>
    /// O bloco de lotes cresce para baixo, uma linha por lote, e a altura da etiqueta acompanha a
    /// quantidade de lotes para que nenhuma linha seja cortada — um lote que nao aparece no papel
    /// e um lote que some do inventario.
    /// </para>
    /// </summary>
    public string ConstruirZplEtiquetaPaleteProdutoAcabado(DadosEtiquetaPaleteProdutoAcabado etiqueta)
    {
        ArgumentNullException.ThrowIfNull(etiqueta);

        string codigoBarras = string.IsNullOrWhiteSpace(etiqueta.CodigoBarras) ? "SEM-CODIGO" : etiqueta.CodigoBarras;
        // A descricao e a linha mais visivel do modelo. Vazia, o material ocupa o lugar — melhor um
        // codigo legivel do que um espaco em branco no meio da etiqueta.
        string titulo = string.IsNullOrWhiteSpace(etiqueta.DescricaoMaterial)
            ? etiqueta.Material
            : etiqueta.DescricaoMaterial;

        const int Margem = 30;
        const int Util = LabelWidthDots - (2 * Margem);   // largura util entre as margens
        const int AlturaLinhaLote = 30;

        // A moldura do codigo de barras e as linhas de lote definem a altura final.
        const int TopoMoldura = 300;
        const int AlturaMoldura = 210;
        int topoTotais = TopoMoldura + AlturaMoldura + 28;
        int topoLotes = topoTotais + 54;
        int alturaLotes = Math.Max(1, etiqueta.Lotes.Count) * AlturaLinhaLote;
        int altura = Math.Max(LabelHeightDots, topoLotes + alturaLotes + 40);

        var zpl = new StringBuilder();
        zpl.Append("^XA\n^CI28\n")
           .Append($"^PW{LabelWidthDots}\n")
           .Append($"^LL{altura}\n")
           .Append("^FWN\n^LH0,0\n")
           // Cabecalho: data/hora de impressao no alto, a direita.
           .Append($"^FO{Margem},22^A0N,20,20^FB{Util},1,0,R^FDData/Hora de Impressao:   "
                 + $"{Z(etiqueta.DataHoraImpressao)}^FS\n")
           // Titulo centralizado.
           .Append($"^FO{Margem},70^A0N,48,48^FB{Util},1,0,C^FDFUGA PET^FS\n")
           // Numero a esquerda, data a direita, na mesma linha.
           .Append($"^FO{Margem},146^A0N,38,38^FDNumero: {Z(etiqueta.Numero)}^FS\n")
           .Append($"^FO{Margem},146^A0N,38,38^FB{Util},1,0,R^FDData: {Z(etiqueta.Data)}^FS\n")
           // Descricao do produto, centralizada e grande, com regua embaixo.
           .Append($"^FO{Margem},214^A0N,34,34^FB{Util},2,4,C^FD{Z(titulo)}^FS\n")
           .Append($"^FO{Margem},{TopoMoldura - 14}^GB{Util},0,3^FS\n")
           // Moldura do codigo de barras, com a leitura humana abaixo das barras.
           .Append($"^FO{Margem + 140},{TopoMoldura}^GB{Util - 280},{AlturaMoldura},3^FS\n")
           // GATE 126D: o campo de barras NAO leva ^FB. ^FB e bloco de TEXTO; aplicado a um ^BC, o
           // dado e composto como texto e NENHUMA barra e gerada -- foi o que saiu impresso em
           // 16/11/2026: moldura vazia com o numero em letra miuda. A etiqueta de CAIXA, que sempre
           // funcionou, usa ^BCB sem ^FB. Como ZPL nao centraliza codigo de barras, a posicao e
           // calculada a partir da largura estimada do Code128.
           .Append($"^FO{CentralizarBarras(codigoBarras, ModuloBarrasPalete, Margem + 140, Util - 280)},"
                 + $"{TopoMoldura + 26}^BY{ModuloBarrasPalete}^BCN,120,Y,N,N^FD{Z(codigoBarras)}^FS\n")
           // Totais, com os mesmos rotulos do modelo.
           .Append($"^FO{Margem},{topoTotais}^A0N,32,32^FDQt Cx./Sacos: {Z(etiqueta.QuantidadeCaixas)}"
                 + $"    Qt Pct/PC: {Z(etiqueta.QuantidadeProdutos)}"
                 + $"    Peso B.: {Z(etiqueta.PesoBruto)}"
                 + $"    Peso L.: {Z(etiqueta.PesoLiquido)}^FS\n");

        if (etiqueta.Lotes.Count == 0)
        {
            zpl.Append($"^FO{Margem},{topoLotes}^A0N,20,20^FDNENHUM LOTE IDENTIFICADO NAS CAIXAS^FS\n");
        }
        else
        {
            // Colunas em posicoes fixas: a leitura por lote precisa alinhar verticalmente.
            int y = topoLotes;
            foreach (LinhaLoteEtiquetaPalete lote in etiqueta.Lotes)
            {
                zpl.Append($"^FO{Margem},{y}^A0N,20,20^FDLOTE: {Z(lote.Lote)}^FS\n")
                   .Append($"^FO{Margem + 190},{y}^A0N,20,20^FDDT. FABRI.: {Z(lote.DataFabricacao)}^FS\n")
                   .Append($"^FO{Margem + 450},{y}^A0N,20,20^FDDT. VCTO.: {Z(lote.DataVencimento)}^FS\n")
                   .Append($"^FO{Margem + 700},{y}^A0N,20,20^FDQTDE. CX.: "
                         + $"{lote.QuantidadeCaixas.ToString(System.Globalization.CultureInfo.InvariantCulture)}^FS\n");
                y += AlturaLinhaLote;
            }
        }

        zpl.Append("^XZ");

        return zpl.ToString();
    }

    /// <summary>Largura do modulo (^BY) do codigo de barras do palete, em pontos.</summary>
    internal const int ModuloBarrasPalete = 3;

    /// <summary>
    /// Largura do modulo (^BY) do codigo de barras da CAIXA, em pontos.
    /// <para>
    /// Dois pontos a 203 dpi dao 0,25 mm de modulo, que e o PISO para Code128 ser lido com folga por
    /// leitor de mao. Era 1 ate 18/11/2026 — 0,125 mm — e o leitor do chao de fabrica simplesmente
    /// nao lia. Menor que o do palete (3) porque a 8x5 tem 639 pontos de largura e o codigo local
    /// chega a 15 caracteres, que ja gastam 400 deles.
    /// </para>
    /// </summary>
    internal const int ModuloBarrasCaixa = 2;

    /// <summary>
    /// Modulo de recuo, usado SO quando o codigo e longo demais para caber com o modulo cheio.
    /// Barra apertada ainda e lida de perto; barra cortada pela borda nao e lida nunca.
    /// </summary>
    internal const int ModuloBarrasCaixaEstreito = 1;

    /// <summary>
    /// Teto do modulo da barra da CAIXA. Quatro pontos sao 0,5 mm de modulo — barra bem larga, lida
    /// de longe. Nao sobe mais porque acima disso o simbolo passa do curso da faixa com qualquer HU,
    /// e porque modulo grande demais exige distancia MINIMA de leitura que o operador nao tem na linha.
    /// </summary>
    internal const int ModuloBarrasCaixaMaximo = 4;

    /// <summary>
    /// Altura das barras da CAIXA, em pontos (~13 mm a 203 dpi).
    /// <para>
    /// Com a barra VERTICAL esta e a medida horizontal da faixa. E a tolerancia de mira do operador:
    /// a referencia da norma e 15% do comprimento do simbolo (4,4 cm aqui, logo 6,6 mm) ou 6,35 mm, o
    /// que for maior — 13 mm fica com folga sobre os dois. Na versao original eram 75 pontos (9 mm)
    /// numa faixa espremida; agora a largura da etiqueta sobra, porque a grade cedeu espaco lateral.
    /// </para>
    /// </summary>
    internal const int AlturaBarrasCaixa = 104;

    /// <summary>
    /// Folga MINIMA em cada ponta da barra, em pontos. Cobre a zona de silencio que o Code128 pede
    /// (10 modulos, 20 pontos no modulo 2) com uma sobra pequena.
    /// <para>
    /// NAO e a margem impressa: e so o piso usado para ESCOLHER O MODULO. A folga real sai da
    /// centralizacao — ver <see cref="TopoCentralizadoBarraCaixa"/> —, e por isso costuma ser maior.
    /// </para>
    /// </summary>
    internal const int MargemMinimaFaixaBarra = 24;

    /// <summary>
    /// Escolhe a largura de modulo da barra da CAIXA: a MAIOR que ainda cabe no curso disponivel.
    /// <para>
    /// Ate 19/11/2026 o modulo era fixo em <see cref="ModuloBarrasCaixa"/>, com um unico recuo para
    /// <see cref="ModuloBarrasCaixaEstreito"/> quando o dado nao cabia. Fazia sentido enquanto a barra
    /// carregava o codigo local, de 12 caracteres, que ocupava quase toda a faixa. Com a HU do SAP o
    /// dado encurtou: 9 caracteres sao 134 modulos, contra os 167 do codigo local. Escolher o modulo pelo
    /// dado mantem as barras tao largas quanto a faixa permite, qualquer que seja o tamanho da HU.
    /// </para>
    /// <para>
    /// Modulo maior significa barra mais LARGA, que e o que o leitor de mao precisa — foi exatamente a
    /// queixa de 18/11/2026, quando o modulo era 1 e o leitor nao lia. Preencher a faixa e consequencia,
    /// nao objetivo: o objetivo e a barra mais grossa que cabe.
    /// </para>
    /// <para>
    /// O piso continua sendo <see cref="ModuloBarrasCaixaEstreito"/>, usado so quando nem o modulo
    /// minimo legivel cabe: barra apertada ainda e lida de perto; barra cortada nao e lida nunca.
    /// </para>
    /// </summary>
    /// <summary>
    /// Origem Y que deixa a barra CENTRALIZADA na altura da etiqueta: folga igual em cima e embaixo.
    /// <para>
    /// Em ^BCB o simbolo cresce para BAIXO a partir da origem, entao centralizar e simplesmente
    /// comecar na metade do que sobra. Pedido do usuario em 19/11/2026, com a etiqueta impressa na
    /// mao: o 1 cm fixo no topo deixava a barra visivelmente mais perto de uma borda que da outra.
    /// </para>
    /// <para>
    /// A margem deixou de ser constante de propósito: ela agora DEPENDE do comprimento do simbolo,
    /// que depende do tamanho da HU, que quem define e o SAP. Fixar a margem de novo faria a barra
    /// sair descentralizada no dia em que a HU mudasse de tamanho.
    /// </para>
    /// <para>
    /// Nunca devolve menos que <see cref="MargemMinimaFaixaBarra"/>: se o simbolo for maior que o
    /// previsto, a zona de silencio e preservada e o excesso vai para baixo, onde ha a grade — e nao
    /// para fora do topo, onde nao seria impresso.
    /// </para>
    /// </summary>
    internal static int TopoCentralizadoBarraCaixa(int alturaEtiqueta, int comprimentoBarra)
    {
        int folga = (alturaEtiqueta - comprimentoBarra) / 2;
        return folga < MargemMinimaFaixaBarra ? MargemMinimaFaixaBarra : folga;
    }

    internal static int EscolherModuloBarraCaixa(string dados, int cursoDisponivel)
    {
        if (string.IsNullOrEmpty(dados))
        {
            return ModuloBarrasCaixa;
        }

        for (int modulo = ModuloBarrasCaixaMaximo; modulo >= ModuloBarrasCaixa; modulo--)
        {
            if (EstimarLarguraCode128Dots(dados, modulo) <= cursoDisponivel)
            {
                return modulo;
            }
        }

        return ModuloBarrasCaixaEstreito;
    }

    /// <summary>
    /// GATE 126D: largura ESTIMADA de um Code128 em pontos. Serve para dimensionar e centralizar o
    /// codigo, ja que ZPL nao tem centralizacao para campo de barras (^FB vale so para texto).
    /// <para>
    /// A conta segue a codificacao do Code128: start + simbolos de dado + digito verificador valem
    /// 11 modulos cada, e o stop vale 13. UM SIMBOLO POR CARACTERE — ver o corpo para o porque de a
    /// compressao de digitos nao entrar nesta conta.
    /// </para>
    /// <para>
    /// E ESTIMATIVA, nao medida. Ela erra para MAIS quando a impressora comprime, e esse e o erro
    /// seguro: a barra sai menor do que o previsto e cabe. Errar para menos foi o que imprimiu uma
    /// barra de 6,6 cm numa etiqueta de 5,0 cm em 19/11/2026.
    /// </para>
    /// </summary>
    internal static int EstimarLarguraCode128Dots(string dados, int larguraModulo)
    {
        if (string.IsNullOrEmpty(dados) || larguraModulo <= 0)
        {
            return 0;
        }

        // UM SIMBOLO POR CARACTERE, SEMPRE — inclusive quando o dado e so digito.
        //
        // O Code128 PODE comprimir digitos no subconjunto C, dois por simbolo, e esta conta chegou a
        // supor isso. Mas a calibracao impressa em 18/11/2026 mediu com regua que ESTA IMPRESSORA NAO
        // COMPRIME: a barra com modo automatico e a sem modo sairam do mesmo tamanho, 5,0 cm, que e a
        // conta do subconjunto B puro para 15 caracteres (200 modulos a modulo 2).
        //
        // A suposicao de compressao ficou dormindo no codigo porque o dado de entao — o codigo local —
        // tinha hifen e caia no ramo "demais". Quando a barra passou a carregar a HU, que e so digito,
        // a estimativa passou a prever METADE do simbolo real: 9 digitos foram estimados em 90 modulos
        // e sairam com 134. No modulo 4 isso virou 6,6 cm numa etiqueta de 5,0 cm, e a barra saiu da
        // etiqueta — relatado pelo usuario em 19/11/2026.
        //
        // A conta volta a ser a CONSERVADORA. Se um dia a impressora passar a comprimir, a barra sai
        // menor que o previsto, que e o erro seguro: cabe na etiqueta.
        int simbolosDados = dados.Length;

        // start + dados + verificador, a 11 modulos cada, mais o stop de 13 modulos.
        int modulos = (11 * (1 + simbolosDados + 1)) + 13;
        return modulos * larguraModulo;
    }

    /// <summary>
    /// Posicao X que centraliza o codigo de barras dentro da moldura. Quando a estimativa excede a
    /// moldura, devolve a borda esquerda dela: melhor o codigo comecar alinhado e eventualmente
    /// invadir a direita do que comecar fora da etiqueta, onde nao seria impresso.
    /// </summary>
    internal static int CentralizarBarras(string dados, int larguraModulo, int xMoldura, int larguraMoldura)
    {
        int larguraBarras = EstimarLarguraCode128Dots(dados, larguraModulo);
        if (larguraBarras <= 0 || larguraBarras >= larguraMoldura)
        {
            return xMoldura;
        }

        return xMoldura + ((larguraMoldura - larguraBarras) / 2);
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












