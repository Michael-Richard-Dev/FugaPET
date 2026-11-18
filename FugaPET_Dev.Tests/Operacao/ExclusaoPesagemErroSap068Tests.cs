using Xunit;

namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// Saida para o lancamento preso em ERRO_SAP (18/11/2026, pedido 4500000027 no QAS130).
/// <para>
/// Uma pesagem com quantidade absurda foi recusada pelo SAP; o lancamento, o item e a pesagem
/// ficaram em ERRO_SAP e a exclusao local passou a responder "estado de integracao SAP que impede
/// a exclusao" — para um lancamento que NUNCA chegou ao SAP. Reenviar ja era permitido a partir de
/// ERRO_SAP, entao dava para insistir no erro mas nao para abandona-lo.
/// </para>
/// <para>
/// Estes testes travam a forma da correcao: ERRO_SAP vira estado de SAIDA, e a prova de que nada
/// foi lancado continua sendo as colunas de rastro SAP nulas — nunca o status sozinho.
/// </para>
/// </summary>
public sealed class ExclusaoPesagemErroSap068Tests
{
    [Fact]
    public void Lancamento_AceitaErroSap_MasExigeRastroSapNulo()
    {
        string corpo = Corpo("BloquearLancamentoAsync(");

        Assert.Contains("status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')", corpo, StringComparison.Ordinal);
        Assert.Contains("documento_material_sap IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("exercicio_documento_material_sap IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("enviado_sap_em IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("situacao_entrada_produto_lancamento = true", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void Item_AceitaErroSap_MasExigeDocumentoNulo()
    {
        string corpo = Corpo("BloquearItemElegivelAsync(");

        Assert.Contains("status_item IN ('FINALIZADO_LOCAL', 'ERRO_SAP')", corpo, StringComparison.Ordinal);
        Assert.Contains("documento_material_item IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("situacao_entrada_produto_item = true", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// No lote, admitir ERRO_SAP obrigou a exigir o rastro SAP nulo: antes o 'FINALIZADO_LOCAL'
    /// sozinho excluia os estados SAP, e agora nao exclui mais. Fecha tambem o buraco anterior, em
    /// que um lote FINALIZADO_LOCAL com documento gravado era aceito.
    /// </summary>
    [Fact]
    public void Lote_AceitaErroSap_ComRastroSapNuloObrigatorio()
    {
        string corpo = Corpo("BloquearLoteElegivelAsync(");

        Assert.Contains("status_lote IN ('FINALIZADO_LOCAL', 'ERRO_SAP')", corpo, StringComparison.Ordinal);
        Assert.Contains("documento_material_sap IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("exercicio_documento_material_sap IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("enviado_sap_em IS NULL", corpo, StringComparison.Ordinal);
        Assert.Contains("confirmado_sap_em IS NULL", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O que NAO pode afrouxar: estado ja lancado no SAP continua barrado. CONFIRMADO_SAP e
    /// ENVIADO_SAP nao entram em nenhuma lista de elegibilidade.
    /// </summary>
    [Theory]
    [InlineData("CONFIRMADO_SAP")]
    [InlineData("ENVIADO_SAP")]
    public void Estados_JaLancadosNoSap_NaoViramElegiveis(string status)
    {
        string fonte = Fonte();

        foreach (string metodo in new[]
        {
            "BloquearLancamentoAsync(", "BloquearItemElegivelAsync(", "BloquearLoteElegivelAsync(",
        })
        {
            string corpo = Corpo(metodo);
            string[] linhasDeStatus = [.. corpo.Split('\n').Where(l => l.Contains("IN (", StringComparison.Ordinal))];
            foreach (string linha in linhasDeStatus)
            {
                Assert.DoesNotContain(status, linha, StringComparison.Ordinal);
            }
        }

        Assert.NotEmpty(fonte);
    }

    /// <summary>O guard de outbox/tentativa segue intacto: ele e independente do status.</summary>
    [Fact]
    public void GuardDeOutboxETentativa_ContinuaBloqueandoEmQualquerEstado()
    {
        string fonte = Fonte();
        Assert.Contains("fn_entrada_produto_sap_guard_counts", fonte, StringComparison.Ordinal);
        Assert.Contains("outbox != 0 || tentativa != 0", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// Corpo do metodo, a partir da DEFINICAO.
    /// <para>
    /// Usa <c>LastIndexOf</c> porque o nome aparece antes nas CHAMADAS, dentro de
    /// ExcluirPesagemLocalAsync; procurar a primeira ocorrencia recortaria o trecho errado — foi o
    /// que fez a primeira versao destes testes falhar sem haver defeito no codigo.
    /// </para>
    /// </summary>
    private static string Corpo(string assinatura)
    {
        string fonte = Fonte();
        int inicio = fonte.LastIndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"metodo {assinatura} nao encontrado");
        int fim = fonte.IndexOf("private ", inicio + assinatura.Length, StringComparison.Ordinal);
        return fim < 0 ? fonte[inicio..] : fonte[inicio..fim];
    }

    private static string Fonte()
        => File.ReadAllText(Path.Combine(
            RaizProjeto(), "AcessoDados", "Repositorio", "ExclusaoPesagemRepositorio.cs"));

    private static string RaizProjeto()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return dir;
    }
}
