using System.Data;
using System.Data.Common;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R8DAbiTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly IdentidadeItemProdutoSapR8B Identidade = new(new(new("000123", "10"), 900, "SYNTHETIC"), 41, 71);
    private static readonly Guid CorrelacaoColeta = Guid.Parse("6cd2118d-11b2-4e5e-b76a-2324df046cc3");
    private static readonly Guid CorrelacaoTentativa = Guid.Parse("1529b69a-77d0-46f7-87f2-094e92919952");
    private static readonly Lazy<string> Sql = new(LerSqlCandidato);

    [Theory]
    [InlineData("evidencia", 7)]
    [InlineData("vinculo", 9)]
    [InlineData("resolucao", 4)]
    [InlineData("tentativa", 10)]
    public void ComandosCorrespondemAsQuatroAssinaturasDoZipOriginal(string operacao, int quantidade)
    {
        using NpgsqlCommand comando = Comando(operacao);
        Match assinatura = Regex.Match(Sql.Value, @"CREATE FUNCTION homologacao\." + Funcao(operacao) + @"\((?<parametros>[^)]+)\)\s*RETURNS", RegexOptions.Singleline);
        Assert.True(assinatura.Success);
        string[] parametros = assinatura.Groups["parametros"].Value.Split(',');
        Assert.Equal(quantidade, comando.Parameters.Count);
        Assert.Equal(quantidade, parametros.Length);
        Dictionary<string, NpgsqlDbType> tipos = new() { ["text"] = NpgsqlDbType.Text, ["bigint"] = NpgsqlDbType.Bigint, ["uuid"] = NpgsqlDbType.Uuid };
        for (int indice = 0; indice < parametros.Length; indice++)
        {
            string[] partes = parametros[indice].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(partes[0], comando.Parameters[indice].ParameterName);
            Assert.Equal(tipos[partes[1]], comando.Parameters[indice].NpgsqlDbType);
        }
        Assert.Null(comando.Connection);
        Assert.Null(comando.Transaction);
    }

    [Fact]
    public void TentativaEnviaEvidenciaEVersaoDaGuardaSemTrocarPorVersaoDoVinculo()
    {
        ClaimP5 claim = Claim();
        using NpgsqlCommand comando = ComandosRecibosProdutoSapR8D.VincularTentativa(Identidade, claim, Resolucao());
        Assert.Equal(301L, comando.Parameters[3].Value);
        Assert.Equal(claim.Token, comando.Parameters[4].Value);
        Assert.Equal(CorrelacaoTentativa, comando.Parameters[5].Value);
        Assert.Equal(201L, comando.Parameters[6].Value);
        Assert.Equal(7L, comando.Parameters[7].Value);
        Assert.Equal(101L, comando.Parameters[8].Value);
        Assert.Equal(33L, comando.Parameters[9].Value);
        Assert.NotEqual(CorrelacaoColeta, comando.Parameters[5].Value);
    }

    [Fact]
    public void EvidenciaLeOsVinteECincoCamposReaisSemUsarCandidatoDesktop()
    {
        ReciboEvidenciaSqlR8D recibo = Evidencia();
        Assert.Equal(101L, recibo.Dados.EvidenciaId);
        Assert.Equal("000123", recibo.Dados.Identidade.NumeroPedido);
        Assert.Equal("00010", recibo.Dados.Identidade.NumeroItem);
        Assert.Equal(900L, recibo.Dados.Identidade.CodigoLancamento);
        Assert.Equal(41L, recibo.Dados.Identidade.CodigoItemLocal);
        Assert.Equal(71L, recibo.Dados.Identidade.CodigoSapItem);
        Assert.Equal("SYNTHETIC", recibo.Dados.Identidade.Material);
        Assert.Equal("SYNTHETIC", recibo.Dados.Product);
        Assert.Equal("ROH", recibo.Dados.ProductType);
        Assert.Equal("API_PRODUCT_SRV/A_Product", recibo.Dados.OrigemServico);
        Assert.Equal("QAS110", recibo.Dados.SapSistema);
        Assert.Equal("110", recibo.Dados.SapMandante);
        Assert.Equal("2026-10-20T09:00:00-03:00", recibo.Dados.LastChangeDateTimeRaw);
        Assert.Equal(Agora.UtcDateTime, recibo.Dados.LastChangeDateTimeUtc);
        Assert.True(recibo.Dados.OffsetComprovado);
        Assert.Equal(new DateOnly(2026, 10, 20), recibo.Dados.LastChangeDate);
        Assert.Equal("USUARIO_SIMULADO", recibo.Dados.LastChangedByUser);
        Assert.Equal("REF-COLETA-SIMULADA", recibo.Dados.ReferenciaColetaBackend);
        Assert.Equal(CorrelacaoColeta, recibo.Dados.CorrelacaoColetaBackend);
        Assert.Equal(Agora.AddMinutes(-1), recibo.Dados.RegistradoNoPostgresqlEm);
        Assert.Equal(Agora.AddHours(1), recibo.Dados.ValidadeAte);
        Assert.Equal(5L, recibo.Dados.VersaoPolitica);
        Assert.Equal(new string('a', 64), recibo.FingerprintSha256);
        Assert.Equal("LOGIN_SQL_SIMULADO", recibo.ColetorLogin);
        Assert.True(recibo.ElegivelAgora);
    }

    [Fact]
    public void VinculoMapeiaIdentidadeEvidenciaEstadoECorrelacaoDoBanco()
    {
        using DataTable tabela = Tabela("vinculo");
        using DbDataReader reader = tabela.CreateDataReader();
        ReciboVinculoSqlR8D recibo = Assert.IsType<ReciboVinculoSqlR8D>(LeitorRecibosProdutoSapR8D.Vinculo(reader, Identidade, Evidencia(), 201, 7, Agora));
        Assert.Equal(101L, recibo.EvidenciaId);
        Assert.Equal(201L, recibo.VinculoId);
        Assert.Equal(7L, recibo.VersaoVinculo);
        Assert.Equal("VALIDADA", recibo.EstadoVinculo);
        Assert.Equal("REF-COLETA-SIMULADA", recibo.ReferenciaColetaBackend);
        Assert.Equal(CorrelacaoColeta, recibo.CorrelacaoColetaBackend);
        Assert.Equal(Agora.AddSeconds(-30), recibo.VinculadoEm);
        Assert.Null(recibo.RevogadoEm);
        Assert.Equal("ROH", recibo.ProductType);
        Assert.Equal("SYNTHETIC", recibo.Product);
        Assert.Equal(5L, recibo.VersaoPolitica);
        Assert.Equal(Agora.AddHours(1), recibo.ValidadeAte);
        Assert.True(recibo.Identidade.Corresponde(Identidade));
    }

    [Theory]
    [InlineData("ROH")]
    [InlineData("HIBE")]
    public void ResolucaoMapeiaVinteESeisCamposSemPreencherAusencias(string tipo)
    {
        using DataTable tabela = Tabela("resolucao");
        tabela.Rows[0]["producttype"] = tipo;
        using DbDataReader reader = tabela.CreateDataReader();
        ReciboResolucaoSqlR8D recibo = Assert.IsType<ReciboResolucaoSqlR8D>(LeitorRecibosProdutoSapR8D.Resolucao(reader, Identidade, Origem(), Agora));
        Assert.Equal(tipo, recibo.Dados.ProductType);
        Assert.Equal(101L, recibo.Dados.EvidenciaId);
        Assert.Equal(201L, recibo.VinculoId);
        Assert.Equal(7L, recibo.VersaoVinculo);
        Assert.Equal("VALIDADA", recibo.EstadoVinculo);
        Assert.Equal(Agora.AddSeconds(-30), recibo.VinculadoEm);
        Assert.True(recibo.Dados.Identidade.Corresponde(Identidade));
        Assert.Equal(CorrelacaoColeta, recibo.Dados.CorrelacaoColetaBackend);
    }

    [Fact]
    public void TentativaMapeiaQuatorzeCamposSemTokensOuDataDeVinculoInventada()
    {
        using DataTable tabela = Tabela("tentativa");
        using DbDataReader reader = tabela.CreateDataReader();
        ClaimP5 claim = Claim();
        ReciboTentativaSqlR8D recibo = Assert.IsType<ReciboTentativaSqlR8D>(LeitorRecibosProdutoSapR8D.Tentativa(reader, claim, Resolucao()));
        Assert.Equal("000123", recibo.NumeroPedido);
        Assert.Equal("00010", recibo.NumeroItem);
        Assert.Equal(900L, recibo.CodigoLancamento);
        Assert.Equal(41L, recibo.CodigoItemLocal);
        Assert.Equal(71L, recibo.CodigoSapItem);
        Assert.Equal(301L, recibo.TentativaId);
        Assert.Equal(101L, recibo.EvidenciaId);
        Assert.Equal(201L, recibo.VinculoId);
        Assert.Equal(7L, recibo.VersaoVinculo);
        Assert.Equal(33L, recibo.VersaoGuarda);
        Assert.Equal(CorrelacaoTentativa, recibo.CorrelationId);
        Assert.Equal("RESERVADO", recibo.EstadoTentativa);
        Assert.Equal(Agora.AddMinutes(-5), recibo.TentativaIniciadaEm);
        Assert.Equal(2L, recibo.RecoveryGeneration);
        Assert.DoesNotContain(claim.Token.ToString(), recibo.ToString());
        Assert.DoesNotContain(claim.Token.ToString(), System.Text.Json.JsonSerializer.Serialize(recibo));
        Assert.DoesNotContain("USUARIO_SIMULADO", Evidencia().ToString());
    }

    [Theory]
    [InlineData("evidencia", "SEM_LINHA")]
    [InlineData("vinculo", "SEM_LINHA")]
    [InlineData("resolucao", "SEM_LINHA")]
    [InlineData("tentativa", "SEM_LINHA")]
    [InlineData("evidencia", "DUPLICADA")]
    [InlineData("vinculo", "DUPLICADA")]
    [InlineData("resolucao", "DUPLICADA")]
    [InlineData("tentativa", "DUPLICADA")]
    [InlineData("evidencia", "COLUNA_AUSENTE")]
    [InlineData("vinculo", "COLUNA_AUSENTE")]
    [InlineData("resolucao", "COLUNA_AUSENTE")]
    [InlineData("tentativa", "COLUNA_AUSENTE")]
    [InlineData("evidencia", "NULO")]
    [InlineData("vinculo", "NULO")]
    [InlineData("resolucao", "NULO")]
    [InlineData("tentativa", "NULO")]
    public void CardinalidadeEProjecaoIncompletasBloqueiam(string operacao, string cenario)
    {
        using DataTable tabela = Tabela(operacao);
        if (cenario == "SEM_LINHA") tabela.Rows.Clear();
        if (cenario == "DUPLICADA") tabela.Rows.Add(tabela.Rows[0].ItemArray);
        if (cenario == "COLUNA_AUSENTE") tabela.Columns.RemoveAt(tabela.Columns.Count - 1);
        if (cenario == "NULO") tabela.Rows[0]["codigo_sap_item"] = DBNull.Value;
        Assert.Null(Ler(operacao, tabela));
    }

    [Theory]
    [InlineData("numero_pedido")]
    [InlineData("numero_item")]
    [InlineData("codigo_lancamento")]
    [InlineData("codigo_item_local")]
    [InlineData("codigo_sap_item")]
    public void IdentidadesRetornadasNaoSaoSubstituidasPeloRequest(string campo)
    {
        foreach (string operacao in new[] { "evidencia", "vinculo", "resolucao", "tentativa" })
        {
            using DataTable tabela = Tabela(operacao);
            tabela.Rows[0][campo] = tabela.Columns[campo]!.DataType == typeof(string) ? "99999" : 999L;
            Assert.Null(Ler(operacao, tabela));
        }
    }

    [Theory]
    [InlineData("tentativa_id")]
    [InlineData("evidencia_id")]
    [InlineData("vinculo_id")]
    [InlineData("versao_vinculo")]
    [InlineData("versao_guarda")]
    [InlineData("recovery_generation")]
    [InlineData("correlation_id")]
    [InlineData("estado_tentativa")]
    public void ReciboDaTentativaExigeFencingECorrelacaoExatos(string campo)
    {
        using DataTable tabela = Tabela("tentativa");
        tabela.Rows[0][campo] = campo switch { "correlation_id" => Guid.NewGuid(), "estado_tentativa" => "ENVIO_INICIADO", _ => 999L };
        Assert.Null(Ler("tentativa", tabela));
    }

    [Theory]
    [InlineData("material")]
    [InlineData("product")]
    [InlineData("producttype")]
    [InlineData("sap_sistema")]
    [InlineData("sap_mandante")]
    [InlineData("origem_servico")]
    [InlineData("validade_ate")]
    [InlineData("versao_politica")]
    public void ResolucaoRejeitaDivergenciaOuFaltaDeVigencia(string campo)
    {
        using DataTable tabela = Tabela("resolucao");
        tabela.Rows[0][campo] = campo switch { "validade_ate" => Agora.UtcDateTime, "versao_politica" => 0L, _ => "DIVERGENTE" };
        Assert.Null(Ler("resolucao", tabela));
    }

    [Theory]
    [InlineData("estado_vinculo")]
    [InlineData("revogado_em")]
    [InlineData("evidencia_id")]
    [InlineData("versao_vinculo")]
    [InlineData("referencia_coleta_backend")]
    [InlineData("correlation_id_coleta_backend")]
    public void VinculoRevogadoOuDeOutraEvidenciaBloqueia(string campo)
    {
        using DataTable tabela = Tabela("vinculo");
        tabela.Rows[0][campo] = campo switch
        {
            "estado_vinculo" => "REVOGADA",
            "revogado_em" => Agora.UtcDateTime,
            "referencia_coleta_backend" => "OUTRA",
            "correlation_id_coleta_backend" => Guid.NewGuid(),
            _ => 999L
        };
        Assert.Null(Ler("vinculo", tabela));
    }

    [Fact]
    public void EscalarETextoGenericoNaoSaoRecibosDeEvidenciaOuTentativa()
    {
        using DataTable escalar = new();
        escalar.Columns.Add("id", typeof(long)); escalar.Rows.Add(101L);
        Assert.Null(Ler("evidencia", escalar));
        using DataTable texto = new();
        texto.Columns.Add("resultado", typeof(string)); texto.Rows.Add("CLASSIFICACAO_VINCULADA_SEM_AUTORIZAR_POST");
        Assert.Null(Ler("tentativa", texto));
    }

    [Fact]
    public void NulosSapOpcionaisNaoRecebemDadosDoDesktop()
    {
        using DataTable tabela = Tabela("resolucao");
        foreach (string campo in new[] { "last_change_datetime_raw", "last_change_datetime_utc", "last_change_date", "last_changed_by_user" }) tabela.Rows[0][campo] = DBNull.Value;
        tabela.Rows[0]["offset_comprovado"] = false;
        ReciboResolucaoSqlR8D recibo = Assert.IsType<ReciboResolucaoSqlR8D>(Ler("resolucao", tabela));
        Assert.Null(recibo.Dados.LastChangeDateTimeRaw); Assert.Null(recibo.Dados.LastChangeDateTimeUtc);
        Assert.Null(recibo.Dados.LastChangeDate); Assert.Null(recibo.Dados.LastChangedByUser);
    }

    [Fact]
    public async Task ResultadoDaFuncaoSozinhoNaoConfirmaCommit()
    {
        ConfirmacaoCommitProdutoSapR8D<ReciboEvidenciaSqlR8D> controle = new(Evidencia());
        Assert.Equal(EstadoCommitProdutoSapR8D.ResultadoDaFuncao, controle.Estado);
        int commits = 0;
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitConfirmado,
            await controle.ConfirmarUmaVezAsync(_ => { commits++; return Task.CompletedTask; }, default));
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitConfirmado,
            await controle.ConfirmarUmaVezAsync(_ => { commits++; return Task.CompletedTask; }, default));
        Assert.Equal(1, commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitIndeterminadoNuncaRepeteMutacaoOuCommit(bool cancelado)
    {
        ConfirmacaoCommitProdutoSapR8D<ReciboEvidenciaSqlR8D> controle = new(Evidencia());
        int commits = 0;
        Func<CancellationToken, Task> commit = _ => { commits++; return cancelado ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(new IOException("FALHA_SIMULADA")); };
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitIndeterminado, await controle.ConfirmarUmaVezAsync(commit, default));
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitIndeterminado, await controle.ConfirmarUmaVezAsync(commit, default));
        Assert.Equal(1, commits);
    }

    [Fact]
    public void ControleCommitSemResultadoDeFuncaoNaoPodeSerCriado()
        => Assert.Throws<ArgumentNullException>(() => new ConfirmacaoCommitProdutoSapR8D<ReciboEvidenciaSqlR8D>(null!));

    [Fact]
    public async Task CommitConcorrenteNaoExecutaSegundoCallbackNemConfirmaAntesDoRetorno()
    {
        ConfirmacaoCommitProdutoSapR8D<ReciboEvidenciaSqlR8D> controle = new(Evidencia());
        TaskCompletionSource liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int commits = 0;
        Task<EstadoCommitProdutoSapR8D> primeira = controle.ConfirmarUmaVezAsync(async _ => { commits++; await liberar.Task; }, default);
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitIndeterminado, controle.Estado);
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitIndeterminado,
            await controle.ConfirmarUmaVezAsync(_ => { commits++; return Task.CompletedTask; }, default));
        liberar.SetResult();
        Assert.Equal(EstadoCommitProdutoSapR8D.CommitConfirmado, await primeira);
        Assert.Equal(1, commits);
    }

    [Theory]
    [InlineData("evidencia")]
    [InlineData("vinculo")]
    [InlineData("resolucao")]
    [InlineData("tentativa")]
    public void InteiroDeTipoDiferenteNaoECoagidoParaBigint(string operacao)
    {
        using DataTable tabela = Tabela(operacao);
        int ordinal = tabela.Columns["codigo_sap_item"]!.Ordinal;
        tabela.Columns.Remove("codigo_sap_item");
        DataColumn coluna = tabela.Columns.Add("codigo_sap_item", typeof(int));
        coluna.SetOrdinal(ordinal); tabela.Rows[0][coluna] = 71;
        Assert.Null(Ler(operacao, tabela));
    }

    [Theory]
    [InlineData("SEM_OFFSET")]
    [InlineData("UTC_DIVERGENTE")]
    [InlineData("FLAG_FALSE_COM_UTC")]
    public void InstanteSapAmbiguoOuContraditorioNaoEReparado(string cenario)
    {
        using DataTable tabela = Tabela("resolucao");
        if (cenario == "SEM_OFFSET") tabela.Rows[0]["last_change_datetime_raw"] = "2026-10-20T09:00:00";
        if (cenario == "UTC_DIVERGENTE") tabela.Rows[0]["last_change_datetime_utc"] = Agora.AddHours(1).UtcDateTime;
        if (cenario == "FLAG_FALSE_COM_UTC") tabela.Rows[0]["offset_comprovado"] = false;
        Assert.Null(Ler("resolucao", tabela));
    }

    [Fact]
    public void SemOffsetComprovadoRawSapContinuaPreservado()
    {
        using DataTable tabela = Tabela("resolucao");
        tabela.Rows[0]["last_change_datetime_raw"] = "2026-10-20T09:00:00";
        tabela.Rows[0]["last_change_datetime_utc"] = DBNull.Value;
        tabela.Rows[0]["offset_comprovado"] = false;
        ReciboResolucaoSqlR8D recibo = Assert.IsType<ReciboResolucaoSqlR8D>(Ler("resolucao", tabela));
        Assert.Equal("2026-10-20T09:00:00", recibo.Dados.LastChangeDateTimeRaw);
        Assert.Null(recibo.Dados.LastChangeDateTimeUtc);
    }

    [Fact]
    public void ElegibilidadeMomentaneaFalsaBloqueiaMesmoComFingerprintELoginSql()
    {
        using DataTable tabela = Tabela("evidencia");
        tabela.Rows[0]["elegivel_agora"] = false;
        Assert.Null(Ler("evidencia", tabela));
    }

    [Theory]
    [InlineData("HIBE")]
    [InlineData("FK")]
    [InlineData("LANCAMENTO")]
    [InlineData("GUARDA_ZERO")]
    [InlineData("TOKEN_VAZIO")]
    [InlineData("VINCULO_ZERO")]
    public void ComandoDaTentativaNaoAdaptaIdentidadeOuClassificacaoInvalida(string cenario)
    {
        ReciboResolucaoSqlR8D resolucao = Resolucao();
        resolucao = resolucao with
        {
            VinculoId = cenario == "VINCULO_ZERO" ? 0 : 201,
            Dados = resolucao.Dados with
            {
                ProductType = cenario == "HIBE" ? "HIBE" : "ROH",
                Identidade = resolucao.Dados.Identidade with { CodigoSapItem = cenario == "FK" ? 72 : 71 }
            }
        };
        ClaimP5 claim = new(Identidade.Escopo with { Lancamento = cenario == "LANCAMENTO" ? 901 : 900 }, 301,
            cenario == "TOKEN_VAZIO" ? Guid.Empty : Guid.NewGuid(), CorrelacaoTentativa, 2, cenario == "GUARDA_ZERO" ? 0 : 33);
        Assert.Throws<InvalidOperationException>(() => ComandosRecibosProdutoSapR8D.VincularTentativa(Identidade, claim, resolucao));
    }

    private static object? Ler(string operacao, DataTable tabela)
    {
        using DbDataReader reader = tabela.CreateDataReader();
        return operacao switch
        {
            "evidencia" => LeitorRecibosProdutoSapR8D.Evidencia(reader, Identidade, 101, Origem(), Agora),
            "vinculo" => LeitorRecibosProdutoSapR8D.Vinculo(reader, Identidade, Evidencia(), 201, 7, Agora),
            "resolucao" => LeitorRecibosProdutoSapR8D.Resolucao(reader, Identidade, Origem(), Agora),
            _ => LeitorRecibosProdutoSapR8D.Tentativa(reader, Claim(), Resolucao())
        };
    }

    private static ReciboEvidenciaSqlR8D Evidencia()
    {
        using DataTable tabela = Tabela("evidencia"); using DbDataReader reader = tabela.CreateDataReader();
        return LeitorRecibosProdutoSapR8D.Evidencia(reader, Identidade, 101, Origem(), Agora)!;
    }
    private static ReciboResolucaoSqlR8D Resolucao()
    {
        using DataTable tabela = Tabela("resolucao"); using DbDataReader reader = tabela.CreateDataReader();
        return LeitorRecibosProdutoSapR8D.Resolucao(reader, Identidade, Origem(), Agora)!;
    }
    private static ClaimP5 Claim() => new(Identidade.Escopo, 301, Guid.NewGuid(), CorrelacaoTentativa, 2, 33);
    private static ContextoBackendProdutoSapR8B Origem() => new("QAS110", "110", "API_PRODUCT_SRV/A_Product", "BACKEND_SIMULADO", CorrelacaoColeta, Agora, Agora);
    private static NpgsqlCommand Comando(string operacao) => operacao switch
    {
        "evidencia" => ComandosRecibosProdutoSapR8D.Evidencia(Identidade, 101),
        "vinculo" => ComandosRecibosProdutoSapR8D.Vinculo(Identidade, 201, 101, 7),
        "resolucao" => ComandosRecibosProdutoSapR8D.Resolver(Identidade.Escopo),
        _ => ComandosRecibosProdutoSapR8D.VincularTentativa(Identidade, Claim(), Resolucao())
    };
    private static string Funcao(string operacao) => operacao switch
    {
        "evidencia" => "fn_p5_r8c_obter_evidencia_recibo",
        "vinculo" => "fn_p5_r8c_obter_vinculo_recibo",
        "resolucao" => "fn_p5_r8c_resolver_recibo",
        _ => "fn_p5_r8c_vincular_tentativa_recibo"
    };
    private static DataTable Tabela(string operacao)
    {
        Match retorno = Regex.Match(Sql.Value, @"CREATE FUNCTION homologacao\." + Funcao(operacao) + @"\([^)]+\)\s*RETURNS TABLE\((?<colunas>[^)]+)\)", RegexOptions.Singleline);
        Assert.True(retorno.Success);
        Dictionary<string, object> valores = new()
        {
            ["numero_pedido"] = "000123",
            ["numero_item"] = "00010",
            ["codigo_lancamento"] = 900L,
            ["codigo_item_local"] = 41L,
            ["codigo_sap_item"] = 71L,
            ["material"] = "SYNTHETIC",
            ["product"] = "SYNTHETIC",
            ["producttype"] = "ROH",
            ["evidencia_id"] = 101L,
            ["vinculo_id"] = 201L,
            ["versao_vinculo"] = 7L,
            ["estado_vinculo"] = "VALIDADA",
            ["origem_servico"] = "API_PRODUCT_SRV/A_Product",
            ["sap_sistema"] = "QAS110",
            ["sap_mandante"] = "110",
            ["last_change_datetime_raw"] = "2026-10-20T09:00:00-03:00",
            ["last_change_datetime_utc"] = Agora.UtcDateTime,
            ["offset_comprovado"] = true,
            ["last_change_date"] = new DateOnly(2026, 10, 20),
            ["last_changed_by_user"] = "USUARIO_SIMULADO",
            ["referencia_coleta_backend"] = "REF-COLETA-SIMULADA",
            ["correlation_id_coleta_backend"] = CorrelacaoColeta,
            ["registrado_no_postgresql_em"] = Agora.AddMinutes(-1).UtcDateTime,
            ["validade_ate"] = Agora.AddHours(1).UtcDateTime,
            ["versao_politica"] = 5L,
            ["fingerprint_sha256"] = new string('a', 64),
            ["coletor_login"] = "LOGIN_SQL_SIMULADO",
            ["elegivel_agora"] = true,
            ["vinculado_em"] = Agora.AddSeconds(-30).UtcDateTime,
            ["revogado_em"] = DBNull.Value,
            ["tentativa_id"] = 301L,
            ["versao_guarda"] = 33L,
            ["correlation_id"] = CorrelacaoTentativa,
            ["estado_tentativa"] = "RESERVADO",
            ["tentativa_iniciada_em"] = Agora.AddMinutes(-5).UtcDateTime,
            ["recovery_generation"] = 2L
        };
        DataTable tabela = new();
        foreach (string declaracao in retorno.Groups["colunas"].Value.Split(','))
        {
            string[] partes = declaracao.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Type tipo = partes[1] switch { "bigint" => typeof(long), "uuid" => typeof(Guid), "boolean" => typeof(bool), "timestamptz" => typeof(DateTime), "date" => typeof(DateOnly), _ => typeof(string) };
            DataColumn coluna = new(partes[0], tipo);
            if (tipo == typeof(DateTime)) coluna.DateTimeMode = DataSetDateTime.Utc;
            tabela.Columns.Add(coluna);
        }
        tabela.Rows.Add(tabela.Columns.Cast<DataColumn>().Select(coluna => valores[coluna.ColumnName]).ToArray());
        return tabela;
    }
    private static string LerSqlCandidato()
    {
        string path = Environment.GetEnvironmentVariable("FUGAPET_R8D_SQL_PACKAGE") ?? throw new InvalidOperationException("Informe o ZIP SQL R8C offline.");
        using FileStream arquivo = File.OpenRead(path);
        Assert.Equal("099ABCE0F27298CEAE9BE24F4D0B305D74C6A9D2E2D8B94453F8177C92F7FB0B", Convert.ToHexString(SHA256.HashData(arquivo)));
        arquivo.Position = 0; using ZipArchive zip = new(arquivo, ZipArchiveMode.Read);
        using StreamReader reader = new(zip.GetEntry("01_R8C_ABI_DELTA_ADITIVO_NAO_APLICAR.sql")!.Open());
        return reader.ReadToEnd();
    }
}
