using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Parser do código de barras da operação (formato V1: 12 OP + 4 operação + 2 evento).
/// Os códigos aqui são DADOS DE TESTE do processo analisado, não regra hardcoded de produção.
/// </summary>
public sealed class CodigoBarrasOperacaoServicoTests
{
    private static CodigoBarrasOperacaoServico Servico() => new();

    // ---------- Exemplos reais do processo analisado ----------

    [Fact]
    public void Interpretar_CodigoDeInicio_DeveDevolverOpOperacaoEEventoInicio()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("000001001710005001");

        Assert.True(r.Valido);
        Assert.Equal("1001710", r.OrdemProducao);
        Assert.Equal("000001001710", r.OrdemNormalizada);
        Assert.Equal("0050", r.Operacao);
        Assert.Equal("01", r.CodigoEvento);
        Assert.Equal(TipoEventoOperacao.Inicio, r.TipoEvento);
        Assert.Equal(ConfiguracaoFormatoCodigoOperacao.VersaoV1, r.FormatoVersao);
        Assert.Empty(r.MensagemValidacao);
    }

    [Fact]
    public void Interpretar_CodigoDeTermino_DeveDevolverEventoTermino()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("000001001710005002");

        Assert.True(r.Valido);
        Assert.Equal("1001710", r.OrdemProducao);
        Assert.Equal("0050", r.Operacao);
        Assert.Equal("02", r.CodigoEvento);
        Assert.Equal(TipoEventoOperacao.Termino, r.TipoEvento);
    }

    [Fact]
    public void Interpretar_DevePreservarCodigoOriginalExatamenteComoLido()
    {
        const string lido = "000001001710005001";
        CodigoBarrasOperacao r = Servico().Interpretar(lido);

        Assert.Equal(lido, r.CodigoOriginal);
        // Original preservado mesmo quando inválido.
        Assert.Equal("123", Servico().Interpretar("123").CodigoOriginal);
    }

    // ---------- Rejeições ----------

    [Theory]
    [InlineData("", "não informado")]
    [InlineData("00000100171000500", "18 caracteres")]      // 17: curto
    [InlineData("0000010017100050011", "18 caracteres")]    // 19: longo
    public void Interpretar_TamanhoInvalido_DeveRejeitar(string codigo, string trechoEsperado)
    {
        CodigoBarrasOperacao r = Servico().Interpretar(codigo);

        Assert.False(r.Valido);
        Assert.Contains(trechoEsperado, r.MensagemValidacao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpretar_CaractereNaoNumerico_DeveRejeitar()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("00000100171000A001");

        Assert.False(r.Valido);
        Assert.Contains("somente números", r.MensagemValidacao, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("03")]
    [InlineData("99")]
    public void Interpretar_EventoDesconhecido_DeveRejeitar(string evento)
    {
        CodigoBarrasOperacao r = Servico().Interpretar($"0000010017100050{evento}");

        Assert.False(r.Valido);
        Assert.Contains("não é reconhecido", r.MensagemValidacao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpretar_OrdemTodaZerada_DeveRejeitar()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("000000000000005001");

        Assert.False(r.Valido);
        Assert.Contains("vazia após a normalização", r.MensagemValidacao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpretar_OperacaoZerada_DeveRejeitar()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("000001001710000001");

        Assert.False(r.Valido);
        Assert.Contains("Operação vazia", r.MensagemValidacao, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Zeros / normalização ----------

    [Fact]
    public void Interpretar_OpComZeros_NormalizaSemPerderAFormaOriginal()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("000000000042005001");

        Assert.True(r.Valido);
        Assert.Equal("42", r.OrdemProducao);          // chave SAP (sem zeros)
        Assert.Equal("000000000042", r.OrdemNormalizada); // forma do código (com zeros)
    }

    [Fact]
    public void Interpretar_OperacaoMantemZerosAEsquerda()
    {
        CodigoBarrasOperacao r = Servico().Interpretar("000001001710000501");

        Assert.True(r.Valido);
        Assert.Equal("0005", r.Operacao); // não vira "5": o código preserva a forma impressa
    }

    // ---------- Gerador / round-trip ----------

    [Fact]
    public void Gerar_Inicio_ETermino_DeveProduzirOsCodigosDoProcessoAnalisado()
    {
        CodigoBarrasOperacaoServico servico = Servico();

        Assert.Equal("000001001710005001", servico.Gerar("1001710", "0050", TipoEventoOperacao.Inicio));
        Assert.Equal("000001001710005002", servico.Gerar("1001710", "0050", TipoEventoOperacao.Termino));
    }

    [Theory]
    [InlineData(TipoEventoOperacao.Inicio)]
    [InlineData(TipoEventoOperacao.Termino)]
    public void RoundTrip_GerarEInterpretar_DevePreservarOsDados(TipoEventoOperacao evento)
    {
        CodigoBarrasOperacaoServico servico = Servico();

        string gerado = servico.Gerar("1001710", "0050", evento);
        CodigoBarrasOperacao r = servico.Interpretar(gerado);

        Assert.True(r.Valido);
        Assert.Equal("1001710", r.OrdemProducao);
        Assert.Equal("0050", r.Operacao);
        Assert.Equal(evento, r.TipoEvento);
    }

    [Fact]
    public void Gerar_OpAcimaDoTamanho_DeveLancar()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => Servico().Gerar("1234567890123", "0050", TipoEventoOperacao.Inicio));

    [Fact]
    public void Gerar_OperacaoAcimaDoTamanho_DeveLancar()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => Servico().Gerar("1001710", "00500", TipoEventoOperacao.Inicio));

    [Theory]
    [InlineData("", "0050")]
    [InlineData("   ", "0050")]
    [InlineData("1001710", "")]
    [InlineData("1001710", "   ")]
    public void Gerar_ComOpOuOperacaoVazia_DeveLancar(string ordem, string operacao)
        => Assert.Throws<ArgumentException>(() => Servico().Gerar(ordem, operacao, TipoEventoOperacao.Inicio));

    [Theory]
    [InlineData("100A710", "0050")]
    [InlineData("1001710", "00A0")]
    public void Gerar_ComCaractereNaoNumerico_DeveLancar(string ordem, string operacao)
        => Assert.Throws<ArgumentException>(() => Servico().Gerar(ordem, operacao, TipoEventoOperacao.Inicio));

    [Fact]
    public void Gerar_ComEnumDesconhecido_DeveLancarEmVezDeTratarComoTermino()
    {
        // Qualquer valor diferente de Inicio NÃO pode virar Termino por padrão.
        const TipoEventoOperacao invalido = (TipoEventoOperacao)99;

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Servico().Gerar("1001710", "0050", invalido));
        Assert.Contains("desconhecido", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Formato centralizado ----------

    [Fact]
    public void Formato_V1_DeveCentralizarAsDefinicoes()
    {
        ConfiguracaoFormatoCodigoOperacao f = ConfiguracaoFormatoCodigoOperacao.V1;

        Assert.Equal("OP_OPERACAO_EVENTO_V1", f.CodigoFormato);
        Assert.Equal(12, f.TamanhoOrdem);
        Assert.Equal(4, f.TamanhoOperacao);
        Assert.Equal(2, f.TamanhoEvento);
        Assert.Equal(18, f.TamanhoTotal);
        Assert.Equal("01", f.CodigoInicio);
        Assert.Equal("02", f.CodigoTermino);
        Assert.Equal(TipoEventoOperacao.Inicio, f.ResolverEvento("01"));
        Assert.Equal(TipoEventoOperacao.Termino, f.ResolverEvento("02"));
        Assert.Null(f.ResolverEvento("03"));
    }

    [Fact]
    public void IdempotencyKey_DeveCombinarFormatoECodigoLido()
        => Assert.Equal(
            "OP_OPERACAO_EVENTO_V1|000001001710005001",
            CodigoBarrasOperacaoServico.MontarIdempotencyKey(
                "000001001710005001", ConfiguracaoFormatoCodigoOperacao.VersaoV1));

    [Fact]
    public void Parser_NaoDeveExibirMessageBox()
    {
        string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Servicos", "Processo", "CodigoBarrasOperacaoServico.cs"));

        // Nenhuma CHAMADA de MessageBox (a palavra pode aparecer em comentário explicando a regra).
        Assert.DoesNotContain("MessageBox.Show", fonte, StringComparison.Ordinal);
        // O código completo nunca é convertido para número.
        Assert.DoesNotContain("long.Parse", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("int.Parse", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Convert.ToInt", fonte, StringComparison.Ordinal);
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
