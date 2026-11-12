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

public sealed class EntradaMpP5R8CAbiTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly IdentidadeItemProdutoSapR8B Identidade = new(new(new("000123", "10"), 900, "SYNTHETIC"), 41, 71);

    [Fact]
    public void IngestaoEmiteQuinzeParametrosDaAssinaturaRealSemConexao()
    {
        using NpgsqlCommand comando = ComandosEvidenciaProdutoSapR8C.Registrar(Pedido());
        Assert.Equal("SELECT * FROM homologacao.fn_p5_r8b_ingestar_producttype($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", comando.CommandText);
        string[] nomes = ["p_pedido", "p_item", "p_lancamento", "p_item_local", "p_fk_sap_item", "p_material", "p_product", "p_producttype", "p_lastchange_datetime_raw", "p_lastchange_datetime_utc", "p_offset_comprovado", "p_lastchange_date", "p_lastchanged_by_user", "p_referencia_coleta", "p_correlation_coleta"];
        NpgsqlDbType[] tipos = [NpgsqlDbType.Text,NpgsqlDbType.Text,NpgsqlDbType.Bigint,NpgsqlDbType.Bigint,NpgsqlDbType.Bigint,NpgsqlDbType.Text,NpgsqlDbType.Text,NpgsqlDbType.Text,NpgsqlDbType.Text,NpgsqlDbType.TimestampTz,NpgsqlDbType.Boolean,NpgsqlDbType.Date,NpgsqlDbType.Text,NpgsqlDbType.Text,NpgsqlDbType.Uuid];
        Assert.Equal(nomes, comando.Parameters.Select(parametro => parametro.ParameterName));
        Assert.Equal(tipos, comando.Parameters.Select(parametro => parametro.NpgsqlDbType));
        Assert.Equal("000123", comando.Parameters[0].Value);
        Assert.Equal("00010", comando.Parameters[1].Value);
        Assert.Equal(41L, comando.Parameters[3].Value);
        Assert.Equal(71L, comando.Parameters[4].Value);
        Assert.Equal("COLETA-OPACA-SIMULADA", comando.Parameters[13].Value);
        Assert.Equal(Pedido().Evidencia.Contexto.CorrelacaoColetaBackend, comando.Parameters[14].Value);
        Assert.Null(comando.Connection);
        Assert.Null(comando.Transaction);
    }

    [Fact]
    public void NulosOpcionaisPermanecemDbNullTipadoSemOffsetInventado()
    {
        PedidoRegistroEvidenciaSqlR8C pedido = Pedido();
        pedido = pedido with { Evidencia = pedido.Evidencia with { Campos = pedido.Evidencia.Campos with
        {
            LastChangeDateTimeOriginal = null, OffsetLastChangeDateTimeOriginal = null,
            LastChangeDateOriginal = null, LastChangedByUser = null
        }}, OffsetComprovadoPeloBackend = false };
        using NpgsqlCommand comando = ComandosEvidenciaProdutoSapR8C.Registrar(pedido);
        foreach (int indice in new[] { 8,9,11,12 }) Assert.Equal(DBNull.Value, comando.Parameters[indice].Value);
        Assert.Equal(false, comando.Parameters[10].Value);
    }

    [Theory]
    [InlineData("2026-10-20T09:00:00-03:00", "-03:00", true, true)]
    [InlineData("2026-10-20T12:00:00Z", "Z", true, true)]
    [InlineData("2026-10-20T12:00:00", null, false, false)]
    [InlineData("/Date(1792497600000+0300)/", "+0300", false, false)]
    [InlineData("2026-10-20T09:00:00-03:00", "-03:00", false, false)]
    public void DatasPreservamRawEExigemOffsetAtestadoParaUtc(string raw, string? offset, bool atestado, bool temUtc)
    {
        CamposProdutoSapR8B campos = Pedido().Evidencia.Campos with { LastChangeDateTimeOriginal = raw, OffsetLastChangeDateTimeOriginal = offset };
        DatasProdutoSqlR8C datas = Assert.IsType<DatasProdutoSqlR8C>(MapeadorDatasProdutoSapR8C.Mapear(campos, atestado));
        Assert.Equal(raw, datas.LastChangeDateTimeOriginal);
        Assert.Equal(temUtc, datas.LastChangeDateTimeUtc.HasValue);
        Assert.Equal(atestado, datas.OffsetComprovado);
        Assert.Equal(new DateOnly(2026,10,20), datas.LastChangeDate);
        if (temUtc) Assert.Equal(Agora.UtcDateTime, datas.LastChangeDateTimeUtc);
    }

    [Theory]
    [InlineData("2026-10-20T12:00:00", null)]
    [InlineData("/Date(1792497600000+0300)/", "+0300")]
    [InlineData("2026-10-20T12:00:00Z", "-03:00")]
    [InlineData("2026-13-20T12:00:00Z", "Z")]
    public void ConversaoUtcAmbiguaOuDivergenteBloqueia(string raw, string? offset)
    {
        CamposProdutoSapR8B campos = Pedido().Evidencia.Campos with { LastChangeDateTimeOriginal = raw, OffsetLastChangeDateTimeOriginal = offset };
        Assert.Null(MapeadorDatasProdutoSapR8C.Mapear(campos, true));
    }

    [Theory]
    [InlineData("20/10/2026")]
    [InlineData("2026-02-30")]
    [InlineData("/Date(1792454400000)/")]
    [InlineData("2026-10-20T00:00:00")]
    public void DataCivilNaoContratadaNaoEConvertidaPorSuposicao(string raw)
        => Assert.Null(MapeadorDatasProdutoSapR8C.Mapear(Pedido().Evidencia.Campos with { LastChangeDateOriginal = raw }, true));

    [Theory]
    [InlineData("PRODUTO")]
    [InlineData("TIPO")]
    [InlineData("FK")]
    [InlineData("PK")]
    [InlineData("REFERENCIA")]
    [InlineData("COLETA")]
    public void RegistroNaoPreencheIdentidadesOuReferenciaAusentes(string cenario)
    {
        PedidoRegistroEvidenciaSqlR8C pedido = Pedido();
        pedido = pedido with
        {
            Identidade = Identidade with { CodigoItemLocal = cenario == "PK" ? 0 : 41, CodigoSapPedidoCompraItem = cenario == "FK" ? 0 : 71 },
            ReferenciaColetaBackend = cenario == "REFERENCIA" ? "" : pedido.ReferenciaColetaBackend,
            Evidencia = pedido.Evidencia with
            {
                Campos = pedido.Evidencia.Campos with { Product = cenario == "PRODUTO" ? "OUTRO" : "SYNTHETIC", ProductType = cenario == "TIPO" ? "" : "ROH" },
                Contexto = pedido.Evidencia.Contexto with { CorrelacaoColetaBackend = cenario == "COLETA" ? Guid.Empty : pedido.Evidencia.Contexto.CorrelacaoColetaBackend }
            }
        };
        Assert.Throws<InvalidOperationException>(() => ComandosEvidenciaProdutoSapR8C.Registrar(pedido));
    }

    [Fact]
    public void ResolverEVincularClassificacaoUsamAssinaturasQuatroEOito()
    {
        using NpgsqlCommand resolver = ComandosEvidenciaProdutoSapR8C.Resolver(Identidade.Escopo);
        Assert.Equal("SELECT * FROM homologacao.fn_p5_r8b_resolver_classificacao($1,$2,$3,$4)", resolver.CommandText);
        Assert.Equal(new[] { "p_pedido", "p_item", "p_lancamento", "p_material" }, resolver.Parameters.Select(parametro => parametro.ParameterName));
        using NpgsqlCommand vincular = ComandosEvidenciaProdutoSapR8C.VincularClassificacao(Identidade, 101, 0);
        Assert.Equal("SELECT * FROM homologacao.fn_p5_r8b_vincular_classificacao($1,$2,$3,$4,$5,$6,$7,$8)", vincular.CommandText);
        Assert.Equal(101L, vincular.Parameters[6].Value);
        Assert.Equal(0L, vincular.Parameters[7].Value);
        Assert.Equal(NpgsqlDbType.Bigint, vincular.Parameters[7].NpgsqlDbType);
    }

    [Fact]
    public void VinculoHistoricoDeOitoParametrosNaoConstituiAlternativaExecutavelRoh()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ComandosEvidenciaProdutoSapR8C.VincularTentativa(Identidade, Claim(), Projecao()));
    }

    [Theory]
    [InlineData("HIBE")]
    [InlineData("FK")]
    [InlineData("LANCAMENTO")]
    [InlineData("GUARDA")]
    public void TentativaNaoAceitaTipoOuIdentidadeDivergentes(string cenario)
    {
        ProjecaoClassificacaoSqlR8C projecao = Projecao() with
        {
            ProductType = cenario == "HIBE" ? "HIBE" : "ROH",
            CodigoSapItem = cenario == "FK" ? 72 : 71
        };
        ClaimP5 claim = cenario == "LANCAMENTO"
            ? new(Identidade.Escopo with { Lancamento = 901 }, 1, Guid.NewGuid(), Guid.NewGuid(), versaoGuarda: 1)
            : new(Identidade.Escopo, 1, Guid.NewGuid(), Guid.NewGuid(), versaoGuarda: cenario == "GUARDA" ? 0 : 1);
        Assert.Throws<InvalidOperationException>(() => ComandosEvidenciaProdutoSapR8C.VincularTentativa(Identidade, claim, projecao));
    }

    [Theory]
    [InlineData("ROH")]
    [InlineData("HIBE")]
    public void ResolverMapeiaSeteColunasComoProjecaoParcialSemFabricarRecibo(string tipo)
    {
        using DataTable tabela = Tabela(tipo);
        using DbDataReader reader = tabela.CreateDataReader();
        ProjecaoClassificacaoSqlR8C resultado = Assert.IsType<ProjecaoClassificacaoSqlR8C>(
            LeitorResultadosEvidenciaProdutoSapR8C.Classificacao(reader, Identidade, Agora));
        Assert.Equal(101L, resultado.EvidenciaId);
        Assert.Equal(201L, resultado.VinculoId);
        Assert.Equal(1L, resultado.VersaoVinculo);
        Assert.Equal(tipo, resultado.ProductType);
        Assert.Equal(41L, resultado.CodigoItemLocal);
        Assert.Equal(71L, resultado.CodigoSapItem);
    }

    [Theory]
    [InlineData("COLUNA")]
    [InlineData("NULO")]
    [InlineData("PK")]
    [InlineData("FK")]
    [InlineData("VERSAO")]
    [InlineData("EVIDENCIA")]
    [InlineData("VINCULO")]
    [InlineData("TIPO")]
    [InlineData("VENCIDA")]
    [InlineData("DUAS_LINHAS")]
    [InlineData("SEM_LINHA")]
    public void ProjecaoIncompletaOuDivergenteBloqueia(string cenario)
    {
        using DataTable tabela = Tabela();
        if (cenario == "COLUNA") tabela.Columns.Remove("evidencia_id");
        if (cenario == "NULO") tabela.Rows[0]["validade_ate"] = DBNull.Value;
        if (cenario == "PK") tabela.Rows[0]["codigo_item_local"] = 42L;
        if (cenario == "FK") tabela.Rows[0]["codigo_sap_item"] = 72L;
        if (cenario == "VERSAO") tabela.Rows[0]["versao_vinculo"] = 0L;
        if (cenario == "EVIDENCIA") tabela.Rows[0]["evidencia_id"] = 0L;
        if (cenario == "VINCULO") tabela.Rows[0]["vinculo_id"] = 0L;
        if (cenario == "TIPO") tabela.Rows[0]["producttype"] = "UNKNOWN";
        if (cenario == "VENCIDA") tabela.Rows[0]["validade_ate"] = Agora.UtcDateTime;
        if (cenario == "DUAS_LINHAS") tabela.Rows.Add(tabela.Rows[0].ItemArray);
        if (cenario == "SEM_LINHA") tabela.Rows.Clear();
        using DbDataReader reader = tabela.CreateDataReader();
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.Classificacao(reader, Identidade, Agora));
    }

    [Fact]
    public void EscalarRegistroNaoEquivaleAReciboPersistidoCompleto()
    {
        Assert.Equal(101L, LeitorResultadosEvidenciaProdutoSapR8C.CodigoRegistro(101L));
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.CodigoRegistro("101"));
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.CodigoRegistro(0L));
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.CodigoRegistro(null));
    }

    [Fact]
    public void TextoGenericoNuncaConfirmaVinculoDuravelDaTentativa()
    {
        Assert.Equal(RespostaVinculoSqlR8C.TextoSemReciboDuravel,
            LeitorResultadosEvidenciaProdutoSapR8C.VinculoTentativa("CLASSIFICACAO_VINCULADA_SEM_AUTORIZAR_POST"));
        Assert.Equal(RespostaVinculoSqlR8C.Invalida, LeitorResultadosEvidenciaProdutoSapR8C.VinculoTentativa("OK"));
        Assert.Equal(RespostaVinculoSqlR8C.Invalida, LeitorResultadosEvidenciaProdutoSapR8C.VinculoTentativa(null));
    }

    [Theory]
    [InlineData("registro")]
    [InlineData("resolver")]
    [InlineData("classificacao")]
    [InlineData("tentativa")]
    public void ComandoConfereComAssinaturaDoZipGaiaImutavel(string operacao)
    {
        if (operacao == "tentativa")
        {
            Assert.Throws<InvalidOperationException>(() =>
                ComandosEvidenciaProdutoSapR8C.VincularTentativa(Identidade, Claim(), Projecao()));
            return;
        }
        string path = Environment.GetEnvironmentVariable("FUGAPET_R8C_SQL_PACKAGE")
            ?? throw new InvalidOperationException("Informe o ZIP SQL candidato offline para os testes ABI.");
        using FileStream arquivo = File.OpenRead(path);
        Assert.Equal("9D12BCD1000D7487582717B08B401BAC8966D8B779D9E3B995EF9B2DD5FB1CC8",
            Convert.ToHexString(SHA256.HashData(arquivo)));
        arquivo.Position = 0;
        using ZipArchive zip = new(arquivo, ZipArchiveMode.Read);
        using StreamReader texto = new(zip.GetEntry("02_R8B_INCREMENTAL_ATOMICO_NAO_APLICAR.sql")!.Open());
        string sql = texto.ReadToEnd();
        using NpgsqlCommand comando = operacao switch
        {
            "registro" => ComandosEvidenciaProdutoSapR8C.Registrar(Pedido()),
            "resolver" => ComandosEvidenciaProdutoSapR8C.Resolver(Identidade.Escopo),
            "classificacao" => ComandosEvidenciaProdutoSapR8C.VincularClassificacao(Identidade, 101, 0),
            _ => ComandosEvidenciaProdutoSapR8C.VincularTentativa(Identidade, Claim(), Projecao())
        };
        string funcao = Regex.Match(comando.CommandText, @"homologacao\.(?<nome>[a-z0-9_]+)\(").Groups["nome"].Value;
        Match assinatura = Regex.Match(sql, @"CREATE FUNCTION homologacao\." + Regex.Escape(funcao) + @"\((?<parametros>[^)]+)\)\s*RETURNS", RegexOptions.Singleline);
        Assert.True(assinatura.Success);
        string[] parametros = assinatura.Groups["parametros"].Value.Split(',');
        Assert.Equal(parametros.Length, comando.Parameters.Count);
        Dictionary<string,NpgsqlDbType> tipos = new()
        {
            ["text"] = NpgsqlDbType.Text, ["bigint"] = NpgsqlDbType.Bigint, ["uuid"] = NpgsqlDbType.Uuid,
            ["timestamptz"] = NpgsqlDbType.TimestampTz, ["boolean"] = NpgsqlDbType.Boolean, ["date"] = NpgsqlDbType.Date
        };
        for (int indice = 0; indice < parametros.Length; indice++)
        {
            string[] declaracao = parametros[indice].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(declaracao[0], comando.Parameters[indice].ParameterName);
            Assert.Equal(tipos[declaracao[1]], comando.Parameters[indice].NpgsqlDbType);
        }
    }

    [Theory]
    [InlineData("ROH")]
    [InlineData("HIBE")]
    public void VinculoClassificacaoLeTresCamposSemAfirmarCommit(string tipo)
    {
        using DataTable tabela = TabelaVinculo(tipo);
        using DbDataReader reader = tabela.CreateDataReader();
        ProjecaoVinculoClassificacaoSqlR8C resultado = Assert.IsType<ProjecaoVinculoClassificacaoSqlR8C>(
            LeitorResultadosEvidenciaProdutoSapR8C.VinculoClassificacao(reader, 0, tipo));
        Assert.Equal(201L, resultado.VinculoId);
        Assert.Equal(1L, resultado.Versao);
        Assert.Equal(tipo, resultado.ProductType);
    }

    [Theory]
    [InlineData("VERSAO")]
    [InlineData("TIPO")]
    [InlineData("COLUNA")]
    [InlineData("NULO")]
    [InlineData("DUPLICADO")]
    public void VinculoClassificacaoIncompletoOuCasDivergenteBloqueia(string cenario)
    {
        using DataTable tabela = TabelaVinculo("ROH");
        if (cenario == "VERSAO") tabela.Rows[0]["versao"] = 2L;
        if (cenario == "TIPO") tabela.Rows[0]["producttype"] = "HIBE";
        if (cenario == "COLUNA") tabela.Columns.Remove("vinculo_id");
        if (cenario == "NULO") tabela.Rows[0]["vinculo_id"] = DBNull.Value;
        if (cenario == "DUPLICADO") tabela.Rows.Add(tabela.Rows[0].ItemArray);
        using DbDataReader reader = tabela.CreateDataReader();
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.VinculoClassificacao(reader, 0, "ROH"));
    }

    [Fact]
    public void ValidadeSqlSemUtcNaoGanhaFusoPorSuposicao()
    {
        using DataTable tabela = Tabela(semUtc: true);
        using DbDataReader reader = tabela.CreateDataReader();
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.Classificacao(reader, Identidade, Agora));
    }

    [Fact]
    public void TipoSqlIncorretoNaoECoagidoParaBigint()
    {
        using DataTable tabela = Tabela();
        tabela.Columns.Remove("codigo_sap_item");
        tabela.Columns.Add("codigo_sap_item", typeof(string));
        tabela.Rows[0]["codigo_sap_item"] = "71";
        using DbDataReader reader = tabela.CreateDataReader();
        Assert.Null(LeitorResultadosEvidenciaProdutoSapR8C.Classificacao(reader, Identidade, Agora));
    }

    private static PedidoRegistroEvidenciaSqlR8C Pedido() => new(Identidade, new(new("SYNTHETIC", "ROH",
        "2026-10-20T09:00:00-03:00", "-03:00", "2026-10-20", null),
        new("QAS110", "110", "API_PRODUCT_SRV/A_Product", "COLETOR_SIMULADO",
            Guid.Parse("e408f566-2e9c-491d-9f63-e65b9dfb6b62"), Agora, Agora.AddSeconds(1))), "COLETA-OPACA-SIMULADA", true);

    private static ClaimP5 Claim() => new(Identidade.Escopo, 1, Guid.NewGuid(), Guid.NewGuid(), versaoGuarda: 3);
    private static ProjecaoClassificacaoSqlR8C Projecao() => new(Identidade.Escopo, 41, 71, "ROH", 101, 201, 1, Agora.AddHours(1));

    private static DataTable Tabela(string tipo = "ROH", bool semUtc = false)
    {
        DataTable tabela = new();
        tabela.Columns.Add("codigo_item_local", typeof(long));
        tabela.Columns.Add("codigo_sap_item", typeof(long));
        tabela.Columns.Add("producttype", typeof(string));
        tabela.Columns.Add("evidencia_id", typeof(long));
        tabela.Columns.Add("vinculo_id", typeof(long));
        tabela.Columns.Add("versao_vinculo", typeof(long));
        tabela.Columns.Add(new DataColumn("validade_ate", typeof(DateTime)) { DateTimeMode = semUtc ? DataSetDateTime.Unspecified : DataSetDateTime.Utc });
        tabela.Rows.Add(41L,71L,tipo,101L,201L,1L,Agora.AddHours(1).UtcDateTime);
        return tabela;
    }

    private static DataTable TabelaVinculo(string tipo)
    {
        DataTable tabela = new();
        tabela.Columns.Add("vinculo_id", typeof(long));
        tabela.Columns.Add("versao", typeof(long));
        tabela.Columns.Add("producttype", typeof(string));
        tabela.Rows.Add(201L,1L,tipo);
        return tabela;
    }
}
