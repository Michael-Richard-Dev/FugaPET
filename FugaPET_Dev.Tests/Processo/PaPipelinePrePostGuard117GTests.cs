using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 117G — regressão do PRE-POST guard 045.
/// Sem banco e sem HTTP/SAP: usa somente Linha045 e predicates puros.
/// </summary>
public sealed class PaPipelinePrePostGuard117GTests
{
    private static Linha045 Linha(
        string etapa,
        string status,
        int tentativa = 0,
        string? documento = null,
        string? ano = null,
        int? http = null,
        object? lease = null,
        object? iniciado = null,
        object? confirmado = null,
        object? falhou = null,
        object? indeterminado = null,
        object? request = null,
        object? response = null,
        object? erro = null,
        string? statusReconciliacao = "NAO_NECESSARIA",
        object? reconciliado = null,
        bool? podeReprocessar = false)
        => new(new Dictionary<string, object?>
        {
            ["etapa_sap"] = etapa,
            ["status_etapa"] = status,
            // O early binding cria correlation_id mesmo no estado virgem.
            // 117G prova que isso, isoladamente, NÃO significa tentativa/POST.
            ["correlation_id"] = Guid.NewGuid(),
            ["numero_tentativa"] = tentativa,
            ["lease_obtido_em"] = lease,
            ["lease_expira_em"] = null,
            ["recovery_lease_obtido_em"] = null,
            ["recovery_lease_expira_em"] = null,
            ["material_document"] = documento,
            ["material_document_year"] = ano,
            ["http_status"] = http,
            ["iniciado_em"] = iniciado,
            ["confirmado_em"] = confirmado,
            ["falhou_em"] = falhou,
            ["indeterminado_em"] = indeterminado,
            ["request_sanitizado"] = request,
            ["response_sanitizado"] = response,
            ["erro_sanitizado"] = erro,
            ["status_reconciliacao"] = statusReconciliacao,
            ["reconciliado_em"] = reconciliado,
            ["pode_reprocessar"] = podeReprocessar
        });

    private static Linha045 Confirmada(string etapa)
        => Linha(
            etapa,
            "CONFIRMADO",
            tentativa: 1,
            documento: etapa == "261" ? "4900000001" : "5000000001",
            ano: "2026",
            http: 201,
            lease: DateTimeOffset.UtcNow.AddMinutes(-1),
            iniciado: DateTimeOffset.UtcNow.AddMinutes(-1),
            confirmado: DateTimeOffset.UtcNow);

    [Fact] // TEST_1
    public void PendingVirgem_261E101_PermitePrimeiroEnvio()
    {
        Linha045[] linhas = [Linha("261", "PENDENTE"), Linha("101", "PENDENTE")];

        ResultadoBloqueioPipeline045 r = ProdutoAcabadoController.AvaliarBloqueioPipeline045(linhas);

        Assert.False(r.Bloqueado);
        Assert.Equal(string.Empty, r.Mensagem);
    }

    [Fact] // TEST_2
    public void PendingComTentativa_BloqueiaReconciliacao()
    {
        ResultadoBloqueioPipeline045 r =
            ProdutoAcabadoController.AvaliarBloqueioPipeline045([Linha("261", "PENDENTE", tentativa: 1)]);

        Assert.True(r.Bloqueado);
        Assert.Contains("reconcilia", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("261", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // TEST_2 — claim/lease também é evidência material
    public void PendingComClaimAnterior_BloqueiaReconciliacao()
    {
        ResultadoBloqueioPipeline045 r =
            ProdutoAcabadoController.AvaliarBloqueioPipeline045(
                [Linha("261", "PENDENTE", lease: DateTimeOffset.UtcNow.AddMinutes(-2))]);

        Assert.True(r.Bloqueado);
    }

    [Fact] // TEST_2 — HTTP/document/response não podem ser tratados como virgem
    public void PendingComEvidenciaHttp_BloqueiaReconciliacao()
    {
        ResultadoBloqueioPipeline045 r =
            ProdutoAcabadoController.AvaliarBloqueioPipeline045(
                [Linha("261", "PENDENTE", http: 500, response: """{"error":"sanitizado"}""")]);

        Assert.True(r.Bloqueado);
    }

    [Fact] // TEST_3
    public void ResultadoIndeterminado_BloqueiaReconciliacao()
    {
        ResultadoBloqueioPipeline045 r =
            ProdutoAcabadoController.AvaliarBloqueioPipeline045(
                [Linha("261", "INDETERMINADO_TIMEOUT", tentativa: 1, indeterminado: DateTimeOffset.UtcNow)]);

        Assert.True(r.Bloqueado);
    }

    [Fact] // TEST_4
    public void Confirmado261_Pending101Virgem_PermiteContinuacaoLegitima()
    {
        Linha045[] linhas = [Confirmada("261"), Linha("101", "PENDENTE")];

        ResultadoBloqueioPipeline045 r = ProdutoAcabadoController.AvaliarBloqueioPipeline045(linhas);

        Assert.False(r.Bloqueado);
    }

    [Fact] // TEST_5
    public void Confirmado261_Pending101ComTentativa_BloqueiaReconciliacao()
    {
        Linha045[] linhas = [Confirmada("261"), Linha("101", "PENDENTE", tentativa: 1, request: "{}")];

        ResultadoBloqueioPipeline045 r = ProdutoAcabadoController.AvaliarBloqueioPipeline045(linhas);

        Assert.True(r.Bloqueado);
        Assert.Contains("101", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // TEST_6
    public void Confirmados261E101_HuVirgemFicaParaContratoHuExistente()
    {
        ResultadoBloqueioPipeline045 r =
            ProdutoAcabadoController.AvaliarBloqueioPipeline045([Confirmada("261"), Confirmada("101")]);

        Assert.False(r.Bloqueado);

        Assert.True(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
        {
            CodigoProdutoAcabadoCaixa = 11,
            StatusIntegracao = StatusIntegracaoCaixa.ProntaParaEnvio
        }));
    }

    [Fact] // TEST_7
    public void HuIndeterminado_ParentGuardImpedeNovoPipeline()
    {
        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
        {
            CodigoProdutoAcabadoCaixa = 11,
            StatusIntegracao = StatusIntegracaoCaixa.IndeterminadoTimeout
        }));
    }

    [Fact] // TEST_8
    public void CaixaConfirmadaSap_NaoPodeDuplicarEnvio()
    {
        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
        {
            CodigoProdutoAcabadoCaixa = 11,
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap
        }));
    }

    [Fact] // TEST_9 — snapshot material do incidente BOX 0004 / codigo_hu_caixa=11
    public void Incidente0004_PendingVirgem_NaoBloqueiaPrePost()
    {
        Linha045[] linhas =
        [
            Linha("101", "PENDENTE"),
            Linha("261", "PENDENTE")
        ];

        ResultadoBloqueioPipeline045 r = ProdutoAcabadoController.AvaliarBloqueioPipeline045(linhas);

        Assert.False(r.Bloqueado);
        Assert.Equal(string.Empty, r.Mensagem);
    }

    [Fact] // garante que o método produtivo usa exatamente o evaluator testado acima
    public void VerificarBloqueioPipeline045Async_DelegaAoEvaluator117G()
    {
        string raiz = AppContext.BaseDirectory;
        string? controller = null;

        for (int i = 0; i < 10 && raiz is not null; i++)
        {
            string a = Path.Combine(raiz, "Controle", "Processo", "ProdutoAcabadoController.cs");
            string b = Path.Combine(raiz, "FugaPet_HML", "Controle", "Processo", "ProdutoAcabadoController.cs");

            if (File.Exists(a)) { controller = a; break; }
            if (File.Exists(b)) { controller = b; break; }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        Assert.False(string.IsNullOrWhiteSpace(controller));
        string src = File.ReadAllText(controller!);
        Assert.Contains("return AvaliarBloqueioPipeline045(linhas);", src, StringComparison.Ordinal);
    }
}