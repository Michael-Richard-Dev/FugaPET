using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// CORREÇÃO DO RESULTADO JÁ REGISTRADO (incremento 072, relato do usuário em 24/11/2026).
/// <para>
/// O sintoma: "na tela de Tempo no consumo só está cadastrando 5, mesmo o usuário colocando outro
/// valor". A causa não era a tela — era o <c>UNIQUE (codigo_apontamento)</c> no banco. Com um
/// resultado por apontamento, a tela reencontrava o gravado, mostrava-o e travava a grade; o
/// primeiro valor virava definitivo.
/// </para>
/// </summary>
public sealed class CorrecaoResultadoApontamentoTests
{
    private static string Servico() => Fonte("Servicos", "Processo", "ResultadoApontamentoServico.cs");
    private static string Repositorio() => Fonte("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");
    private static string Correcao() => Fonte("Tela", "Processo", "ProcessoResultadoApontamentoForm.Correcao.cs");
    private static string Proposta() => Fonte(
        "BancoDados", "001_incrementais", "072_correcao_resultado_apontamento_Q_GAIA",
        "072_correcao_resultado_apontamento_Q_PROPOSTA_GAIA.sql");
    private static string Rollback() => Fonte(
        "BancoDados", "001_incrementais", "072_correcao_resultado_apontamento_Q_GAIA",
        "072_correcao_resultado_apontamento_Q_ROLLBACK_GAIA.sql");

    // ---------------------------------------------------------------- o valor corrente

    /// <summary>
    /// Mais de um resultado deixou de ser erro e passou a ser correção: vale o MAIS RECENTE. Antes,
    /// isso bloqueava o recovery — o que, somado à unicidade no banco, tornava o primeiro valor
    /// eterno.
    /// </summary>
    [Fact]
    public void VariosResultados_ValeOMaisRecente_NaoBloqueiaMais()
    {
        string servico = Servico();

        Assert.Contains("ResultadoPersistidoApontamento resultado = resultados[^1];", servico, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Mais de um resultado persistido para o apontamento; recovery local bloqueado.",
            servico,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// O "mais recente" só é confiável se a ordem for contrato. O repositório ordena por
    /// <c>codigo_resultado</c>, que é identity — e não por data, que pode empatar.
    /// </summary>
    [Fact]
    public void AOrdemDaLeitura_ECrescentePorCodigo_EEstaDocumentada()
    {
        string repositorio = Repositorio();

        Assert.Contains(".OrderBy(par => par.Key)", repositorio, StringComparison.Ordinal);
        Assert.Contains("e isso e CONTRATO", repositorio, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- o que a correção preserva

    /// <summary>
    /// A correção é um INSERT, nunca um UPDATE ou DELETE: a tabela é append-only e o registro
    /// anterior continua lá. Um UPDATE seria mais simples e apagaria a informação que o log existe
    /// para guardar — qual era o valor antes, e que alguém o trocou.
    /// </summary>
    [Fact]
    public void ACorrecao_NaoReescreveNemApagaOAnterior()
    {
        string repositorio = Repositorio();

        Assert.DoesNotContain("UPDATE operacao_resultado_registro", repositorio, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM operacao_resultado_registro", repositorio, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO operacao_resultado_registro", repositorio, StringComparison.Ordinal);
    }

    /// <summary>O usuário é avisado do que a correção faz antes de ela acontecer.</summary>
    [Fact]
    public void ATela_ConfirmaAntesDeDestravar_EExplicaOEfeito()
    {
        string correcao = Correcao();

        Assert.Contains("MessageBox.Show", correcao, StringComparison.Ordinal);
        Assert.Contains("deixa de valer", correcao, StringComparison.Ordinal);
        Assert.Contains("NÃO é apagado", correcao, StringComparison.Ordinal);
        Assert.Contains("MessageBoxDefaultButton.Button2", correcao, StringComparison.Ordinal);
    }

    /// <summary>
    /// A GRADE NÃO TRAVA MAIS com resultado registrado. Travá-la era o que fazia o primeiro valor
    /// virar definitivo — o operador digitava outro e nada acontecia.
    /// </summary>
    [Fact]
    public void ComResultadoRegistrado_AGradeContinuaEditavel()
    {
        string tela = Fonte("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        int ondeAplica = tela.IndexOf(
            "private void AplicarResultadoPersistido", StringComparison.Ordinal);
        int fim = tela.IndexOf("statusValueLabel.Text = \"RESULTADO JÁ REGISTRADO\"", ondeAplica, StringComparison.Ordinal);
        Assert.True(ondeAplica >= 0 && fim > ondeAplica);

        string corpo = tela[ondeAplica..fim];
        Assert.Contains("colResultado.ReadOnly = false;", corpo, StringComparison.Ordinal);
        Assert.Contains("resultadosGridView.ReadOnly = false;", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("colResultado.ReadOnly = true;", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// A correção entra pelo GESTO que o operador já fazia — digitar por cima —, sem controle novo.
    /// Um botão a mais na coluna de ações empurrava o painel para fora do cartão lateral, o que o
    /// teste de layout real pegou, e seria um segundo caminho para o que a célula já sugere.
    /// </summary>
    [Fact]
    public void ACorrecao_NaoAcrescentaBotaoNaColunaDeAcoes()
    {
        string correcao = Correcao();

        Assert.DoesNotContain("acoesTable", correcao, StringComparison.Ordinal);
        Assert.DoesNotContain("new Button()", correcao, StringComparison.Ordinal);
        Assert.Contains("ConfirmarCorrecaoDoResultadoPersistido", correcao, StringComparison.Ordinal);
    }

    /// <summary>Desistir da correção devolve a célula ao valor gravado, sem efeito colateral.</summary>
    [Fact]
    public void DesistirDaCorrecao_CancelaAEdicao()
    {
        string tela = Fonte("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.Contains("if (!ConfirmarCorrecaoDoResultadoPersistido())", tela, StringComparison.Ordinal);
        Assert.Contains("resultadosGridView.CancelEdit();", tela, StringComparison.Ordinal);
    }

    /// <summary>A pergunta é feita UMA vez por correção, não a cada célula editada.</summary>
    [Fact]
    public void APergunta_NaoSeRepeteACadaCelula()
    {
        string correcao = Correcao();

        Assert.Contains("if (_correcaoConfirmada)", correcao, StringComparison.Ordinal);
        Assert.Contains("_correcaoConfirmada = true;", correcao, StringComparison.Ordinal);
    }

    /// <summary>Apontamento novo não herda a confirmação dada no anterior.</summary>
    [Fact]
    public void AoRecarregar_OEstadoDeCorrecaoEZerado()
    {
        Assert.Contains("ReiniciarEstadoCorrecao();", Fonte("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs"), StringComparison.Ordinal);
        Assert.Contains("_correcaoConfirmada = false;", Correcao(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- o incremento

    /// <summary>A unicidade sai, mas o índice de busca entra: a UNIQUE também servia de índice.</summary>
    [Fact]
    public void OIncremento_TiraAUnicidade_EPoeOIndiceDeBusca()
    {
        string proposta = Proposta();

        Assert.Contains("DROP CONSTRAINT IF EXISTS uq_operacao_resultado_registro_apontamento", proposta, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX IF NOT EXISTS ix_resultado_registro_apontamento", proposta, StringComparison.Ordinal);
    }

    /// <summary>Sem as tabelas do 041 o incremento aborta, em vez de criar schema por conta própria.</summary>
    [Fact]
    public void SemO041_OIncrementoAborta()
        => Assert.Contains("Aplique o 041 antes", Proposta(), StringComparison.Ordinal);

    /// <summary>
    /// O ROLLBACK não apaga correção: se algum apontamento já tem duas linhas, ele aborta e lista
    /// quais. Escolher qual registro descartar não é decisão de script.
    /// </summary>
    [Fact]
    public void ORollback_AbortaEmVezDeApagarCorrecao()
    {
        string rollback = Rollback();

        Assert.Contains("ROLLBACK ABORTADO", rollback, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM homologacao.operacao_resultado_registro", rollback, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REVERTA TAMBEM A APLICACAO", rollback, StringComparison.Ordinal);
    }

    private static string Fonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
