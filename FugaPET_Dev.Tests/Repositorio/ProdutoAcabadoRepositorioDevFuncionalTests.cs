using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Npgsql;

namespace FugaPET_Dev.Tests.Repositorio;

/// <summary>
/// Testes FUNCIONAIS (§25) do <see cref="ProdutoAcabadoRepositorio"/> contra o banco DEV
/// (192.168.4.160/fuga_jales_local_desenvolvimento, schema desenvolvimento), incremental 044 REV11.
/// As operações do app rodam sob <c>SET ROLE fugapet_dev_app</c> (via <see cref="FabricaConexaoAppRole"/>),
/// exercitando a superfície REAL de privilégios: INSERT por colunas, funções fn_hu_caixa_*, sem UPDATE
/// direto e sem DML no log. Nenhuma DDL/GRANT/SQL administrativo; nenhuma HU SAP real; dados marcados como
/// teste (OP "T044-*"). Requer o banco DEV acessível (§2). Sem POST/CPI real.
/// </summary>
[Collection("dev-db")]
public sealed class ProdutoAcabadoRepositorioDevFuncionalTests
{
    private const long Usuario = 1; // usuario ativo confirmado no DEV

    // Config lida DIRETO do arquivo (não do env): outros testes mutam FUGAPET_DEV_CONEXAO_POSTGRES no
    // processo, o que apontaria para um banco inexistente. Pooling DESLIGADO: conexões físicas fechadas ao
    // dispor, para que o SET ROLE do app NUNCA vaze para uma conexão reutilizada. Não altera a config do projeto.
    private static readonly ConfiguracaoBancoPostgreSql Config = CarregarDoArquivoSemPooling();

    private static ConfiguracaoBancoPostgreSql CarregarDoArquivoSemPooling()
    {
        string caminho = Path.Combine(AppContext.BaseDirectory, "configuracao.banco.json");
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(caminho));
        System.Text.Json.JsonElement banco = doc.RootElement.GetProperty("banco");
        string Texto(string p, string padrao) => banco.TryGetProperty(p, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString()! : padrao;
        int Inteiro(string p, int padrao) => banco.TryGetProperty(p, out var v) && v.TryGetInt32(out int i) ? i : padrao;
        bool Booleano(string p) => banco.TryGetProperty(p, out var v) && v.ValueKind is System.Text.Json.JsonValueKind.True;

        return new ConfiguracaoBancoPostgreSql
        {
            Habilitado = Booleano("habilitado"),
            Servidor = Texto("servidor", "127.0.0.1"),
            Porta = Inteiro("porta", 5432),
            NomeBanco = Texto("nome_banco", "fuga_jales_local_desenvolvimento"),
            Schema = Texto("schema", "desenvolvimento"),
            Usuario = Texto("usuario", "postgres"),
            Senha = Environment.GetEnvironmentVariable("FUGAPET_DEV_POSTGRES_SENHA") is { Length: > 0 } s ? s : Texto("senha", string.Empty),
            TimeoutSegundos = Inteiro("timeout_segundos", 15),
            Pooling = false,
            SslMode = Texto("ssl_mode", "Prefer")
        };
    }

    private static ProdutoAcabadoRepositorio Repo()
        => new(new FabricaConexaoAppRole(new FabricaConexaoPostgreSql(Config)));

    private static NpgsqlConnection AbrirComoPostgres()
        => new FabricaConexaoPostgreSql(Config).CriarConexao();

    private static (string Op, string Terminal, string Lote, string Hu) Identificadores()
    {
        string id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return ($"T044-{id}", $"TERM044-{id}", $"L{id}", $"HU{id}");
    }

    private static ProdutoAcabadoCaixa NovaCaixa(string op, string terminal, string lote) => new()
    {
        NumeroOrdemProducao = op,
        ItemOrdemProducao = "0001",
        Material = "4000108",
        Lote = lote,
        Centro = "3007",
        Deposito = "PP02",
        MaterialEmbalagem = "3000009",
        OrigemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.Sap,
        PesoBrutoKg = 10.5m,
        TaraKg = 0.5m,
        PesoLiquidoKg = 10.0m,
        UnidadePeso = "KG",
        QuantidadeProdutos = 60,
        UnidadeQuantidade = "UN",
        OrigemPesagem = "MANUAL",
        CorrelationId = Guid.NewGuid(),
        Terminal = terminal,
        CodigoUsuario = Usuario
    };

    private const string RequestJson =
        """{"HandlingUnitExternalID":"$1","GrossWeight":10.5,"NetWeight":10.0,"WeightUnit":"KG","Warehouse":"","Plant":"3007"}""";

    // ================= §25: registro + numeração gerada + pesagem + finalizar/preview/aguardar/autorizar =================
    [Fact]
    public async Task Fluxo_RegistroAteAutorizacao_ComIdentidadeGeradaEAuditoria()
    {
        (string op, string terminal, string lote, _) = Identificadores();
        ProdutoAcabadoRepositorio repo = Repo();

        ProdutoAcabadoCaixa caixa = NovaCaixa(op, terminal, lote);
        Guid correlation = caixa.CorrelationId;
        ProdutoAcabadoCaixa persistida = await repo.RegistrarCaixaAsync(caixa);

        long codigo = persistida.CodigoProdutoAcabadoCaixa!.Value;
        Assert.Equal(1, persistida.NumeroCaixa);                        // banco gerou (primeira caixa da OP)
        Assert.Equal($"CX-{op}-0001", persistida.CodigoCaixaLocal);     // padrão CX-<OP>-<4 dígitos>
        Assert.Equal(correlation, persistida.CorrelationId);           // correlation_id preservado
        Assert.Equal(StatusIntegracaoCaixa.EmPesagem, persistida.StatusIntegracao);

        await repo.RegistrarPesagemAsync(new RegistroPesagemHuCaixa
        {
            CodigoHuCaixa = codigo, CodigoBalanca = null, OrigemPesagem = "MANUAL",
            PesoLido = 10.5m, PesoBruto = 10.5m, PesoLiquido = 10.0m, PesoTara = 0.5m,
            UnidadePeso = "KG", CodigoUsuario = Usuario
        });

        Assert.True(await repo.FinalizarLocalAsync(codigo, Usuario, terminal));
        Assert.True(await repo.SalvarPreviewAsync(codigo, RequestJson, ProdutoAcabadoHandlingUnitCaixaRequestBuilder.EndpointRelativo));
        Assert.True(await repo.AguardarAutorizacaoAsync(codigo));
        Assert.True(await repo.AutorizarEnvioAsync(codigo, Usuario, terminal));

        ProdutoAcabadoCaixa? snap = await repo.ObterPorCodigoAsync(codigo);
        Assert.Equal(StatusIntegracaoCaixa.ProntaParaEnvio, snap!.StatusIntegracao);

        // §26: auditoria RUNTIME — o INSERT do app disparou o trigger dedicado e gravou no log.
        Assert.True(await ContarAuditoria(codigo) >= 1);

        // Limpeza pelo contrato (cancelamento), sem DELETE direto.
        Assert.True(await repo.CancelarAsync(codigo, Usuario, terminal, "limpeza teste 044"));
    }

    // ================= §9/§11/§12: claim (token+tentativa) → timeout → reconciliação confirmada =================
    [Fact]
    public async Task Claim_Timeout_ReconciliacaoConfirmada()
    {
        (string op, string terminal, string lote, string hu) = Identificadores();
        ProdutoAcabadoRepositorio repo = Repo();
        long codigo = await DriveAtePronta(repo, op, terminal, lote);

        ProdutoAcabadoCaixa? snapshot = await repo.ClaimEnvioAsync(codigo, Usuario, terminal);
        Assert.NotNull(snapshot);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, snapshot!.StatusIntegracao);
        Assert.Equal(1, snapshot.Tentativas);
        Assert.NotNull(snapshot.ClaimToken);

        int tentativa = snapshot.Tentativas;
        Guid token = snapshot.ClaimToken!.Value;

        Assert.True(await repo.RegistrarTimeoutAsync(codigo, tentativa, token, null, "timeout teste 044"));
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);

        Assert.True(await repo.ConfirmarReconciliacaoAsync(
            codigo, hu, string.Empty, 200, null, null, null, "TESTE", DateTimeOffset.UtcNow, comparacaoAprovada: true));
        ProdutoAcabadoCaixa? conf = await repo.ObterPorCodigoAsync(codigo);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, conf!.StatusIntegracao);
        Assert.Equal(hu, conf.HandlingUnitExternalId);
    }

    // ================= §11/§12: timeout → reconciliação 404 mantém INDETERMINADO_TIMEOUT =================
    [Fact]
    public async Task Claim_Timeout_Reconciliacao404_MantemIndeterminado()
    {
        (string op, string terminal, string lote, string hu) = Identificadores();
        ProdutoAcabadoRepositorio repo = Repo();
        long codigo = await DriveAtePronta(repo, op, terminal, lote);
        ProdutoAcabadoCaixa snap = (await repo.ClaimEnvioAsync(codigo, Usuario, terminal))!;
        await repo.RegistrarTimeoutAsync(codigo, snap.Tentativas, snap.ClaimToken!.Value, null, "timeout teste 044");

        Assert.True(await repo.RegistrarReconciliacaoNaoEncontradaAsync(codigo, hu, string.Empty, 404, null, null, "nao encontrada teste 044"));
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    // ================= §13: erro 401 NAO_AUTORIZADO (não reprocessável) =================
    [Fact]
    public async Task Claim_Erro401_NaoAutorizado_NaoReprocessavel()
    {
        (string op, string terminal, string lote, _) = Identificadores();
        ProdutoAcabadoRepositorio repo = Repo();
        long codigo = await DriveAtePronta(repo, op, terminal, lote);
        ProdutoAcabadoCaixa snap = (await repo.ClaimEnvioAsync(codigo, Usuario, terminal))!;

        Assert.True(await repo.RegistrarErroAsync(
            codigo, snap.Tentativas, snap.ClaimToken!.Value, 401, null, null,
            "nao autorizado teste 044", ResultadoErroHu.NaoAutorizado, podeReprocessar: false));
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);

        // NAO_AUTORIZADO nunca libera reprocessamento.
        Assert.False(await repo.LiberarReprocessamentoAsync(codigo, Usuario, terminal, "tentativa reproc"));

        Assert.True(await repo.CancelarAsync(codigo, Usuario, terminal, "limpeza teste 044"));
    }

    // ================= §13: erro 500 ERRO_DEFINITIVO reprocessável → liberar → PRONTA =================
    [Fact]
    public async Task Claim_Erro500_Definitivo_Reprocessavel_Libera()
    {
        (string op, string terminal, string lote, _) = Identificadores();
        ProdutoAcabadoRepositorio repo = Repo();
        long codigo = await DriveAtePronta(repo, op, terminal, lote);
        ProdutoAcabadoCaixa snap = (await repo.ClaimEnvioAsync(codigo, Usuario, terminal))!;

        Assert.True(await repo.RegistrarErroAsync(
            codigo, snap.Tentativas, snap.ClaimToken!.Value, 500, null, null,
            "erro definitivo teste 044", ResultadoErroHu.ErroDefinitivo, podeReprocessar: true));
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);

        Assert.True(await repo.LiberarReprocessamentoAsync(codigo, Usuario, terminal, "reproc teste 044"));
        Assert.Equal(StatusIntegracaoCaixa.ProntaParaEnvio, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);

        Assert.True(await repo.CancelarAsync(codigo, Usuario, terminal, "limpeza teste 044"));
    }

    // ================= §26: auditoria runtime existe + DML direto no log é BLOQUEADO por contrato =================
    [Fact]
    public async Task Auditoria_Runtime_E_DmlDiretoNoLog_Bloqueado()
    {
        (string op, string terminal, string lote, _) = Identificadores();
        ProdutoAcabadoRepositorio repo = Repo();
        ProdutoAcabadoCaixa persistida = await repo.RegistrarCaixaAsync(NovaCaixa(op, terminal, lote));
        long codigo = persistida.CodigoProdutoAcabadoCaixa!.Value;

        Assert.True(await ContarAuditoria(codigo) >= 1); // trigger gravou automaticamente

        // Sob a role do app, INSERT direto no log deve falhar (insufficient_privilege / 42501).
        await using NpgsqlConnection con = AbrirComoPostgres();
        await con.OpenAsync();
        await using (var setRole = new NpgsqlCommand("SET ROLE fugapet_dev_app", con))
        {
            await setRole.ExecuteNonQueryAsync();
        }

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO desenvolvimento.log_alteracao_cadastral(tabela,codigo_registro,operacao,situacao_log_alteracao_cadastral) VALUES('forjado_teste',-999044,'INSERT',true)", con);
            await insert.ExecuteNonQueryAsync();
        });
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege

        await using (var reset = new NpgsqlCommand("RESET ROLE", con))
        {
            await reset.ExecuteNonQueryAsync();
        }

        Assert.True(await repo.CancelarAsync(codigo, Usuario, terminal, "limpeza teste 044"));
    }

    private static async Task<long> DriveAtePronta(ProdutoAcabadoRepositorio repo, string op, string terminal, string lote)
    {
        ProdutoAcabadoCaixa persistida = await repo.RegistrarCaixaAsync(NovaCaixa(op, terminal, lote));
        long codigo = persistida.CodigoProdutoAcabadoCaixa!.Value;
        await repo.RegistrarPesagemAsync(new RegistroPesagemHuCaixa
        {
            CodigoHuCaixa = codigo, OrigemPesagem = "MANUAL", PesoLido = 10.5m,
            PesoBruto = 10.5m, PesoLiquido = 10.0m, PesoTara = 0.5m, UnidadePeso = "KG", CodigoUsuario = Usuario
        });
        await repo.FinalizarLocalAsync(codigo, Usuario, terminal);
        await repo.SalvarPreviewAsync(codigo, RequestJson, ProdutoAcabadoHandlingUnitCaixaRequestBuilder.EndpointRelativo);
        await repo.AguardarAutorizacaoAsync(codigo);
        await repo.AutorizarEnvioAsync(codigo, Usuario, terminal);
        return codigo;
    }

    private static async Task<long> ContarAuditoria(long codigo)
    {
        await using NpgsqlConnection con = AbrirComoPostgres();
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "select count(*) from desenvolvimento.log_alteracao_cadastral where tabela='hu_caixa' and codigo_registro=@c and operacao='INSERT'", con);
        cmd.Parameters.AddWithValue("c", codigo);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
}

/// <summary>
/// Fábrica de conexão de TESTE que aplica <c>SET ROLE fugapet_dev_app</c> em cada conexão aberta, para
/// exercitar a superfície REAL de privilégios do app (a conexão configurada do projeto é postgres). Não
/// altera roles/senhas/grants; o role é da sessão e é resetado ao devolver a conexão ao pool.
/// </summary>
internal sealed class FabricaConexaoAppRole(IFabricaConexaoBanco inner) : IFabricaConexaoBanco
{
    public NpgsqlConnection CriarConexao() => inner.CriarConexao();

    public string ObterConnectionString() => inner.ObterConnectionString();

    public async Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
    {
        NpgsqlConnection con = await inner.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new("SET ROLE fugapet_dev_app", con);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return con;
    }
}
