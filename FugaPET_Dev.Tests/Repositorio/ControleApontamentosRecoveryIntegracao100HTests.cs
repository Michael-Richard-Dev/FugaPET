using System.Collections.Concurrent;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;
using Xunit;

namespace FugaPET_Dev.Tests.Repositorio;

/// <summary>
/// GATE 100H-R2 — cobertura COMPORTAMENTAL real (PostgreSQL isolado fuga_balanca_teste_recovery_100h) do recovery
/// de consumo confirmado. Exercita diretamente ControleApontamentosRepositorio.TentarRecuperarConsumoConfirmadoAsync
/// (o núcleo transacional dos cenários 01–19), a atomicidade (rollback integral por falha pós-início/pré-commit) e
/// a concorrência real (FOR UPDATE, conexões independentes, sincronização por Barrier — sem Thread.Sleep).
///
/// Segurança: opt-in explícito (FUGAPET_HML_TESTE_CONNECTION_STRING + FUGAPET_HML_PERMITIR_TESTE_DESTRUTIVO=true) e
/// guard de prefixo fuga_balanca_teste_ do harness ⇒ jamais toca Q/homologação real. Fixtures 100% sintéticas
/// (OP prefixada "9990", nunca OP1000170/PK reais), limpeza rigorosa por prefixo. NENHUMA escrita SAP/consumo.
/// </summary>
[Collection("RecoveryIntegracao100H")]
public sealed class ControleApontamentosRecoveryIntegracao100HTests : IAsyncLifetime
{
    private const string Schema = "homologacao";
    private const string PrefixoOrdem = "9990"; // 4 chars; + 8 dígitos = 12 (varchar(12) das tabelas de consumo)
    private static int _sequencia;

    private FabricaConexaoBancoTeste? _fabrica;
    private string? _motivo;

    public async Task InitializeAsync()
    {
        if (!BancoTesteIntegracao.TentarCriar(out FabricaConexaoBancoTeste fabricaBase, out string motivo))
        {
            _motivo = motivo;
            return;
        }

        // 100H-R3: guard EXATO fail-closed ANTES de qualquer conexão — só o alvo autorizado (host/porta/database/
        // role/TLS) prossegue; qualquer divergência aborta sem tocar o banco.
        Recovery100HAlvo.Resultado alvo = Recovery100HAlvo.Validar(
            fabricaBase.ObterConnectionString(), BancoTesteIntegracao.DestrutivoAutorizado());
        if (!alvo.Aceito)
        {
            _motivo = alvo.Motivo;
            return;
        }

        // Espelha a produção (FabricaConexaoPostgreSql define SearchPath = schema): garante que as conexões do
        // repositório resolvam as tabelas NÃO-qualificadas no schema homologacao. Sem alterar código produtivo.
        NpgsqlConnectionStringBuilder builder = new(fabricaBase.ObterConnectionString());
        if (string.IsNullOrWhiteSpace(builder.SearchPath))
        {
            builder.SearchPath = Schema;
        }

        _fabrica = new FabricaConexaoBancoTeste(builder.ConnectionString);

        try
        {
            await ValidarSchemaAsync();
            await LimparResiduoAsync();
        }
        catch (Exception ex)
        {
            _motivo = ex.Message;
            _fabrica = null;
            return;
        }

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "it100h",
            Nome = "Integracao 100H",
            IntegracaoBancoHabilitada = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_fabrica is not null)
        {
            try { await LimparResiduoAsync(); } catch { /* limpeza best-effort */ }
        }

        EstadoSessaoUsuarioAtual.Limpar();
    }

    // ============================ 01–15: FAIL-CLOSED (false, zero mutação) ============================

    [Recovery100HIntegrationFact]
    public async Task Cenario01_CodigoLancamentoInexistente_RetornaFalseSemMutacao()
    {
        // 100H-R3 FIX01: apontamento e contexto VÁLIDOS; a PK de LANÇAMENTO passada é sinteticamente inexistente
        // (namespace controlado pelo teste, ≥ 8e9 — sem MAX(id)+1 vulnerável a corrida). Nenhum lançamento é criado
        // para essa PK. Prova o guard de PK/codigo_lancamento explícito inexistente (não "apontamento inexistente").
        Cenario c = await MontarHappyPathAsync();
        long lancamentoInexistente = 8_000_000_000L + Interlocked.Increment(ref _sequencia);

        int vinculoAntes = await ContarVinculosAsync(c.CodigoApontamento);
        int eventoAntes = await ContarEventosAsync(c.CodigoApontamento);

        bool ok = await ExecutarRecuperacaoAsync(c with { CodigoLancamento = lancamentoInexistente });

        Assert.False(ok);
        (string status, string? resultado, long? registro, bool concluidoNotNull) = await LerApontamentoAsync(c.CodigoApontamento);
        Assert.Equal("EM_ANDAMENTO", status);           // status inalterado
        Assert.Null(resultado);                          // resultado_operacional inalterado
        Assert.Null(registro);                           // codigo_registro_processo inalterado
        Assert.False(concluidoNotNull);                  // concluido_operacional_em inalterado
        Assert.Equal(vinculoAntes, await ContarVinculosAsync(c.CodigoApontamento)); // vinculo_count_delta = 0
        Assert.Equal(eventoAntes, await ContarEventosAsync(c.CodigoApontamento));   // evento_count_delta = 0
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario02_LancamentoPendenteSap_RetornaFalse()
        => await CenarioLancamentoNaoElegivelAsync(status: "PENDENTE_SAP", documento: "5000000001", exercicio: "2026");

    [Recovery100HIntegrationFact]
    public async Task Cenario03_ConfirmadoSemDocumento_RetornaFalse()
        => await CenarioLancamentoNaoElegivelAsync(status: "CONFIRMADO_SAP", documento: null, exercicio: "2026");

    [Recovery100HIntegrationFact]
    public async Task Cenario04_ConfirmadoSemExercicio_RetornaFalse()
        => await CenarioLancamentoNaoElegivelAsync(status: "CONFIRMADO_SAP", documento: "5000000001", exercicio: null);

    [Recovery100HIntegrationFact]
    public async Task Cenario05_OpDivergente_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync();
        // lançamento pertence a outra OP: o SELECT do lançamento filtra por numero_ordem do contexto.
        string outraOrdem = NovaOrdem();
        long lancamentoOutraOrdem = await InserirLancamentoAsync(outraOrdem, "CONFIRMADO_SAP", "5000000009", "2026");
        await InserirItemAsync(lancamentoOutraOrdem, outraOrdem, c.Material, c.Reserva, c.ItemReserva, c.Lote);

        bool ok = await ExecutarRecuperacaoAsync(c with { CodigoLancamento = lancamentoOutraOrdem });

        Assert.False(ok);
        await AssertApontamentoIntactoAsync(c);
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario06_MaterialDivergente_RetornaFalse()
        => await CenarioItemDivergenteAsync(material: "MATERIAL_OUTRO");

    [Recovery100HIntegrationFact]
    public async Task Cenario07_ReservaDivergente_RetornaFalse()
        => await CenarioItemDivergenteAsync(reserva: "9999999999");

    [Recovery100HIntegrationFact]
    public async Task Cenario08_ItemReservaDivergente_RetornaFalse()
        => await CenarioItemDivergenteAsync(itemReserva: "9999");

    [Recovery100HIntegrationFact]
    public async Task Cenario09_LoteDivergente_RetornaFalse()
        => await CenarioItemDivergenteAsync(lote: "LOTE_OUTRO");

    [Recovery100HIntegrationFact]
    public async Task Cenario10_MultiplosItens_CountDiferenteDeUm_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync();
        // Segundo item no MESMO lançamento ⇒ COUNT(*)=2 ≠ 1 ⇒ fail-closed.
        await InserirItemAsync(c.CodigoLancamento, c.NumeroOrdem, "MATERIAL_EXTRA", "1234567890", "0002", "LOTE-EXTRA");

        bool ok = await ExecutarRecuperacaoAsync(c);

        Assert.False(ok);
        await AssertApontamentoIntactoAsync(c);
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario11_ApontamentoNaoEmAndamento_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync(statusApontamento: "AGUARDANDO_FINALIZACAO",
            resultadoOperacional: "ConfirmadoSap", concluidoOperacional: true);

        bool ok = await ExecutarRecuperacaoAsync(c);

        Assert.False(ok);
        Assert.Equal("AGUARDANDO_FINALIZACAO", await LerStatusApontamentoAsync(c.CodigoApontamento));
        Assert.Equal(0, await ContarVinculosAsync(c.CodigoApontamento));
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario12_ResultadoOperacionalPreenchido_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync(resultadoOperacional: "ConfirmadoSap");
        bool ok = await ExecutarRecuperacaoAsync(c);
        Assert.False(ok);
        Assert.Equal(0, await ContarVinculosAsync(c.CodigoApontamento));
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario13_CodigoRegistroProcessoPreenchido_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync(codigoRegistroProcesso: 12345);
        bool ok = await ExecutarRecuperacaoAsync(c);
        Assert.False(ok);
        Assert.Equal(0, await ContarVinculosAsync(c.CodigoApontamento));
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario14_ConcluidoOperacionalEmPreenchido_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync(concluidoOperacional: true);
        bool ok = await ExecutarRecuperacaoAsync(c);
        Assert.False(ok);
        Assert.Equal(0, await ContarVinculosAsync(c.CodigoApontamento));
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario15_VinculoPreexistente_RetornaFalse()
    {
        Cenario c = await MontarHappyPathAsync();
        await InserirVinculoAsync(c.CodigoApontamento, "CONSUMO_MATERIA_PRIMA", c.CodigoLancamento);

        bool ok = await ExecutarRecuperacaoAsync(c);

        Assert.False(ok);
        Assert.Equal(1, await ContarVinculosAsync(c.CodigoApontamento)); // continua sendo o pré-existente
        Assert.Equal("EM_ANDAMENTO", await LerStatusApontamentoAsync(c.CodigoApontamento));
    }

    // ============================ 16: HAPPY PATH ============================

    [Recovery100HIntegrationFact]
    public async Task Cenario16_HappyPath_CriaVinculoConcluiOperacionalERegistraEvento()
    {
        Cenario c = await MontarHappyPathAsync();

        int lancAntes = await ContarConsumoLancamentosAsync();
        int itemAntes = await ContarConsumoItensAsync();
        int pesagemAntes = await ContarConsumoPesagensAsync();
        int eventoAntes = await ContarEventosAsync(c.CodigoApontamento);

        bool ok = await ExecutarRecuperacaoAsync(c);

        Assert.True(ok);
        Assert.Equal(1, await ContarVinculosAsync(c.CodigoApontamento));

        (string status, string? resultado, long? registro, bool concluidoNotNull) = await LerApontamentoAsync(c.CodigoApontamento);
        Assert.Equal("AGUARDANDO_FINALIZACAO", status);
        Assert.Equal("ConfirmadoSap", resultado);
        Assert.Equal(c.CodigoLancamento, registro);
        Assert.True(concluidoNotNull);

        Assert.Equal(eventoAntes + 1, await ContarEventosAsync(c.CodigoApontamento)); // +1 evento
        // zero novo lançamento/item/pesagem de consumo + zero SAP (o repositório não possui gateway SAP).
        Assert.Equal(lancAntes, await ContarConsumoLancamentosAsync());
        Assert.Equal(itemAntes, await ContarConsumoItensAsync());
        Assert.Equal(pesagemAntes, await ContarConsumoPesagensAsync());
    }

    // ============================ 17: SEGUNDA EXECUÇÃO ============================

    [Recovery100HIntegrationFact]
    public async Task Cenario17_SegundaExecucaoMesmaPk_FailSafeSemDuplicacao()
    {
        Cenario c = await MontarHappyPathAsync();

        Assert.True(await ExecutarRecuperacaoAsync(c));
        int eventosApos1 = await ContarEventosAsync(c.CodigoApontamento);

        bool segunda = await ExecutarRecuperacaoAsync(c);

        Assert.False(segunda);
        Assert.Equal(1, await ContarVinculosAsync(c.CodigoApontamento));        // sem duplicação
        Assert.Equal(eventosApos1, await ContarEventosAsync(c.CodigoApontamento)); // nenhuma nova conclusão
        Assert.Equal("AGUARDANDO_FINALIZACAO", await LerStatusApontamentoAsync(c.CodigoApontamento));
    }

    // ============================ 18/19: CONCORRÊNCIA ============================

    [Recovery100HIntegrationFact]
    public async Task Cenario18_ConcorrenciaMesmaPk_ExatamenteUmVencedor()
    {
        Cenario c = await MontarHappyPathAsync();

        bool[] resultados = await ExecutarConcorrenteAsync(c.CodigoApontamento, [c.CodigoLancamento, c.CodigoLancamento], c);

        Assert.Equal(1, resultados.Count(r => r));                 // exatamente 1 vencedor
        Assert.Equal(1, await ContarVinculosAsync(c.CodigoApontamento));
        Assert.Equal("AGUARDANDO_FINALIZACAO", await LerStatusApontamentoAsync(c.CodigoApontamento));
    }

    [Recovery100HIntegrationFact]
    public async Task Cenario19_ConcorrenciaPksDiferentes_MesmoApontamento_ExatamenteUmVencedor()
    {
        Cenario c = await MontarHappyPathAsync();
        // Segundo lançamento CONFIRMADO_SAP, mesma OP/identidade de item, para o MESMO apontamento.
        long lancamento2 = await InserirLancamentoAsync(c.NumeroOrdem, "CONFIRMADO_SAP", "5000000002", "2026");
        await InserirItemAsync(lancamento2, c.NumeroOrdem, c.Material, c.Reserva, c.ItemReserva, c.Lote);

        bool[] resultados = await ExecutarConcorrenteAsync(c.CodigoApontamento, [c.CodigoLancamento, lancamento2], c);

        Assert.Equal(1, resultados.Count(r => r));                 // exatamente 1 vencedor
        Assert.Equal(1, await ContarVinculosAsync(c.CodigoApontamento));
        Assert.Equal("AGUARDANDO_FINALIZACAO", await LerStatusApontamentoAsync(c.CodigoApontamento));
    }

    // ============================ ATOMICIDADE ============================

    [Recovery100HIntegrationFact]
    public async Task Atomicidade_FalhaAposInicioAntesCommit_RollbackIntegral()
    {
        Cenario c = await MontarHappyPathAsync();

        // 100H-R3 FIX02: snapshot COMPORTAMENTAL do consumo (chave + campos mutáveis + atualizado_em), não só COUNT,
        // para provar INSERT=0/UPDATE=0/DELETE=0 no consumo associado à fixture após o rollback.
        string consumoAntes = await SnapshotConsumoAsync(c.NumeroOrdem);

        await InstalarGatilhoFalhaEventoAsync();
        try
        {
            // O repositório insere vínculo + atualiza o apontamento e SÓ ENTÃO insere o evento (último passo):
            // o gatilho força RAISE no INSERT do evento ⇒ exceção ⇒ rollback de TODA a transação.
            await Assert.ThrowsAnyAsync<PostgresException>(() => ExecutarRecuperacaoBrutaAsync(c));
        }
        finally
        {
            await RemoverGatilhoFalhaEventoAsync();
        }

        // Nenhum estado parcial: vínculo ausente, resultado inalterado, registro NULL, status EM_ANDAMENTO, sem evento.
        Assert.Equal(0, await ContarVinculosAsync(c.CodigoApontamento));
        (string status, string? resultado, long? registro, bool concluidoNotNull) = await LerApontamentoAsync(c.CodigoApontamento);
        Assert.Equal("EM_ANDAMENTO", status);
        Assert.Null(resultado);
        Assert.Null(registro);
        Assert.False(concluidoNotNull);
        Assert.Equal(0, await ContarEventosAsync(c.CodigoApontamento));

        // Consumo (lançamento/item/pesagem) idêntico byte a byte ⇒ nenhum INSERT/UPDATE/DELETE no consumo da fixture.
        string consumoDepois = await SnapshotConsumoAsync(c.NumeroOrdem);
        Assert.Equal(consumoAntes, consumoDepois);
    }

    /// <summary>
    /// Assinatura determinística do estado de consumo da fixture (por OP): lançamento + item + pesagem com chave e
    /// TODOS os campos mutáveis relevantes (incl. *_atualizado_em). Igualdade antes/depois prova zero INSERT/UPDATE/DELETE.
    /// </summary>
    private async Task<string> SnapshotConsumoAsync(string numeroOrdem)
    {
        System.Text.StringBuilder sb = new();
        await using NpgsqlConnection conexao = await AbrirAsync();

        await AnexarLinhasAsync(conexao, sb, "L", $"""
            SELECT codigo_consumo_material_lancamento, status_lancamento,
                   coalesce(documento_material_sap,''), coalesce(exercicio_documento_material_sap,''),
                   consumo_material_lancamento_atualizado_em
              FROM {Schema}.consumo_material_lancamento
             WHERE numero_ordem = @ordem
             ORDER BY codigo_consumo_material_lancamento
            """, numeroOrdem);

        await AnexarLinhasAsync(conexao, sb, "I", $"""
            SELECT codigo_consumo_material_item, codigo_material, coalesce(numero_reserva,''),
                   coalesce(item_reserva,''), coalesce(lote,''), status_item, quantidade_consumida_local,
                   consumo_material_item_atualizado_em
              FROM {Schema}.consumo_material_item
             WHERE numero_ordem = @ordem
             ORDER BY codigo_consumo_material_item
            """, numeroOrdem);

        await AnexarLinhasAsync(conexao, sb, "P", $"""
            SELECT p.codigo_consumo_material_pesagem, p.status_pesagem, p.peso_liquido_kg, p.peso_bruto_kg,
                   p.peso_tara_kg, p.consumo_material_pesagem_criado_em
              FROM {Schema}.consumo_material_pesagem p
              JOIN {Schema}.consumo_material_item i
                ON i.codigo_consumo_material_item = p.codigo_consumo_material_item
             WHERE i.numero_ordem = @ordem
             ORDER BY p.codigo_consumo_material_pesagem
            """, numeroOrdem);

        return sb.ToString();
    }

    private static async Task AnexarLinhasAsync(
        NpgsqlConnection conexao, System.Text.StringBuilder sb, string prefixo, string sql, string numeroOrdem)
    {
        await using NpgsqlCommand cmd = new(sql, conexao);
        cmd.Parameters.AddWithValue("@ordem", numeroOrdem);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            sb.Append(prefixo).Append('{');
            for (int i = 0; i < leitor.FieldCount; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(leitor.IsDBNull(i) ? "<null>" : Convert.ToString(leitor.GetValue(i), System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.Append("}\n");
        }
    }

    // ============================ Cenários auxiliares ============================

    private async Task CenarioLancamentoNaoElegivelAsync(string status, string? documento, string? exercicio)
    {
        Cenario c = await MontarHappyPathAsync(criarLancamento: false);
        long lancamento = await InserirLancamentoAsync(c.NumeroOrdem, status, documento, exercicio);
        await InserirItemAsync(lancamento, c.NumeroOrdem, c.Material, c.Reserva, c.ItemReserva, c.Lote);

        bool ok = await ExecutarRecuperacaoAsync(c with { CodigoLancamento = lancamento });

        Assert.False(ok);
        await AssertApontamentoIntactoAsync(c);
    }

    private async Task CenarioItemDivergenteAsync(
        string? material = null, string? reserva = null, string? itemReserva = null, string? lote = null)
    {
        Cenario c = await MontarHappyPathAsync(criarItem: false);
        await InserirItemAsync(
            c.CodigoLancamento, c.NumeroOrdem,
            material ?? c.Material, reserva ?? c.Reserva, itemReserva ?? c.ItemReserva, lote ?? c.Lote);

        bool ok = await ExecutarRecuperacaoAsync(c);

        Assert.False(ok);
        await AssertApontamentoIntactoAsync(c);
    }

    // ============================ Montagem de fixture ============================

    private sealed record Cenario(
        long CodigoApontamento,
        string NumeroOrdem,
        string Operacao,
        string Sequencia,
        string Suboperacao,
        long CodigoLancamento,
        string Material,
        string Reserva,
        string ItemReserva,
        string Lote,
        string CodigoBarras);

    private async Task<Cenario> MontarHappyPathAsync(
        string statusApontamento = "EM_ANDAMENTO",
        string? resultadoOperacional = null,
        long? codigoRegistroProcesso = null,
        bool concluidoOperacional = false,
        bool criarLancamento = true,
        bool criarItem = true)
    {
        string ordem = NovaOrdem();
        const string operacao = "0010";
        const string sequencia = "000000";
        const string suboperacao = "";
        string material = "MAT" + ordem;
        string reserva = "R" + ordem[^9..];
        string itemReserva = "0001";
        string lote = "LOTE-" + ordem;
        string barcode = ordem + operacao + "01"; // 12 + 4 + 2 = 18

        long codigoApontamento = await InserirApontamentoAsync(
            ordem, sequencia, operacao, suboperacao, statusApontamento, "CONSUMO_MATERIA_PRIMA",
            resultadoOperacional, codigoRegistroProcesso, concluidoOperacional, barcode);

        long codigoLancamento = 0;
        if (criarLancamento)
        {
            codigoLancamento = await InserirLancamentoAsync(ordem, "CONFIRMADO_SAP", "5000000000", "2026");
            if (criarItem)
            {
                await InserirItemAsync(codigoLancamento, ordem, material, reserva, itemReserva, lote);
            }
        }

        return new Cenario(codigoApontamento, ordem, operacao, sequencia, suboperacao,
            codigoLancamento, material, reserva, itemReserva, lote, barcode);
    }

    private async Task<bool> ExecutarRecuperacaoAsync(Cenario c)
    {
        var repo = new ControleApontamentosRepositorio(_fabrica!);
        return await repo.TentarRecuperarConsumoConfirmadoAsync(
            Contexto(c), c.CodigoLancamento, c.Material, c.Reserva, c.ItemReserva, c.Lote,
            Resultado(c.CodigoLancamento), "it-user", "it-est", Barcode(c.CodigoBarras));
    }

    // Variante que NÃO é engolida por catch — usada só na prova de atomicidade (o repositório relança).
    private Task<bool> ExecutarRecuperacaoBrutaAsync(Cenario c) => ExecutarRecuperacaoAsync(c);

    private async Task<bool[]> ExecutarConcorrenteAsync(long codigoApontamento, long[] lancamentos, Cenario baseCenario)
    {
        using Barrier barreira = new(lancamentos.Length);
        ConcurrentBag<bool> resultados = [];

        async Task Rodar(long lancamento)
        {
            var repo = new ControleApontamentosRepositorio(_fabrica!);
            Cenario c = baseCenario with { CodigoLancamento = lancamento };
            barreira.SignalAndWait();
            bool r = await repo.TentarRecuperarConsumoConfirmadoAsync(
                Contexto(c), lancamento, c.Material, c.Reserva, c.ItemReserva, c.Lote,
                Resultado(lancamento), "it-user", "it-est", Barcode(c.CodigoBarras));
            resultados.Add(r);
        }

        await Task.WhenAll(lancamentos.Select(l => Task.Run(() => Rodar(l))));
        return [.. resultados];
    }

    private ContextoApontamentoProcesso Contexto(Cenario c) => new()
    {
        CodigoApontamento = c.CodigoApontamento,
        NumeroOrdem = c.NumeroOrdem,
        Sequencia = c.Sequencia,
        Operacao = c.Operacao,
        Suboperacao = c.Suboperacao,
        TipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima
    };

    private static ResultadoExecucaoProcesso Resultado(long codigoLancamento) => new(
        ResultadoExecucaoProcessoApontamento.ConfirmadoSap, codigoLancamento,
        "Consumo já confirmado no SAP recuperado sem novo envio.", true);

    private static CodigoBarrasOperacao Barcode(string codigo)
        => new CodigoBarrasOperacaoServico().Interpretar(codigo);

    private static string NovaOrdem()
        => PrefixoOrdem + (Interlocked.Increment(ref _sequencia) % 100_000_000).ToString("D8");

    // ============================ Asserções compostas ============================

    private async Task AssertApontamentoIntactoAsync(Cenario c)
    {
        Assert.Equal(0, await ContarVinculosAsync(c.CodigoApontamento));
        Assert.Equal("EM_ANDAMENTO", await LerStatusApontamentoAsync(c.CodigoApontamento));
        Assert.Equal(0, await ContarEventosAsync(c.CodigoApontamento));
    }

    // ============================ Acesso ao banco (fixtures/asserts) ============================

    private async Task<NpgsqlConnection> AbrirAsync()
    {
        NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using (NpgsqlCommand cmd = new($"SET search_path TO {Schema}, pg_catalog;", conexao))
        {
            await cmd.ExecuteNonQueryAsync();
        }
        return conexao;
    }

    private async Task<long> InserirApontamentoAsync(
        string numeroOrdem, string sequencia, string operacao, string suboperacao, string status,
        string tipoProcesso, string? resultadoOperacional, long? codigoRegistroProcesso, bool concluido, string barcode)
    {
        const string sql = $"""
            INSERT INTO {Schema}.operacao_producao_apontamento
                (numero_ordem, sequencia, operacao, suboperacao, tipo_processo, status,
                 usuario_inicio, estacao_inicio, codigo_barras_inicio, idempotency_key,
                 resultado_operacional, codigo_registro_processo, concluido_operacional_em)
            VALUES
                (@numero_ordem, @sequencia, @operacao, @suboperacao, @tipo_processo, @status,
                 'it-user', 'it-est', @barcode, @idem,
                 @resultado, @registro, @concluido)
            RETURNING codigo_apontamento;
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        cmd.Parameters.AddWithValue("@numero_ordem", numeroOrdem);
        cmd.Parameters.AddWithValue("@sequencia", sequencia);
        cmd.Parameters.AddWithValue("@operacao", operacao);
        cmd.Parameters.AddWithValue("@suboperacao", suboperacao);
        cmd.Parameters.AddWithValue("@tipo_processo", tipoProcesso);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@barcode", barcode);
        cmd.Parameters.AddWithValue("@idem", "IDEM-" + numeroOrdem);
        cmd.Parameters.AddWithValue("@resultado", (object?)resultadoOperacional ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@registro", (object?)codigoRegistroProcesso ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@concluido", concluido ? DateTimeOffset.UtcNow : (object)DBNull.Value);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<long> InserirLancamentoAsync(string numeroOrdem, string status, string? documento, string? exercicio)
    {
        const string sql = $"""
            INSERT INTO {Schema}.consumo_material_lancamento
                (numero_ordem, status_lancamento, documento_material_sap, exercicio_documento_material_sap)
            VALUES (@numero_ordem, @status, @documento, @exercicio)
            RETURNING codigo_consumo_material_lancamento;
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        cmd.Parameters.AddWithValue("@numero_ordem", numeroOrdem);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@documento", (object?)documento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@exercicio", (object?)exercicio ?? DBNull.Value);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task InserirItemAsync(
        long codigoLancamento, string numeroOrdem, string material, string reserva, string itemReserva, string lote)
    {
        const string sql = $"""
            INSERT INTO {Schema}.consumo_material_item
                (codigo_consumo_material_lancamento, numero_ordem, codigo_material,
                 numero_reserva, item_reserva, lote, unidade)
            VALUES (@lancamento, @numero_ordem, @material, @reserva, @item, @lote, 'KG');
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        cmd.Parameters.AddWithValue("@lancamento", codigoLancamento);
        cmd.Parameters.AddWithValue("@numero_ordem", numeroOrdem);
        cmd.Parameters.AddWithValue("@material", material);
        cmd.Parameters.AddWithValue("@reserva", reserva);
        cmd.Parameters.AddWithValue("@item", itemReserva);
        cmd.Parameters.AddWithValue("@lote", lote);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InserirVinculoAsync(long codigoApontamento, string tipoProcesso, long codigoRegistroProcesso)
    {
        const string sql = $"""
            INSERT INTO {Schema}.operacao_producao_apontamento_processo
                (codigo_apontamento, tipo_processo, codigo_registro_processo)
            VALUES (@apontamento, @tipo, @registro);
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        cmd.Parameters.AddWithValue("@apontamento", codigoApontamento);
        cmd.Parameters.AddWithValue("@tipo", tipoProcesso);
        cmd.Parameters.AddWithValue("@registro", codigoRegistroProcesso);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int> ContarVinculosAsync(long codigoApontamento)
        => await ContarAsync($"SELECT COUNT(*) FROM {Schema}.operacao_producao_apontamento_processo WHERE codigo_apontamento = @p", codigoApontamento);

    private async Task<int> ContarEventosAsync(long codigoApontamento)
        => await ContarAsync($"SELECT COUNT(*) FROM {Schema}.operacao_producao_evento WHERE codigo_apontamento = @p", codigoApontamento);

    private async Task<int> ContarConsumoLancamentosAsync()
        => await ContarAsync($"SELECT COUNT(*) FROM {Schema}.consumo_material_lancamento WHERE numero_ordem LIKE '{PrefixoOrdem}%'", null);

    private async Task<int> ContarConsumoItensAsync()
        => await ContarAsync($"SELECT COUNT(*) FROM {Schema}.consumo_material_item WHERE numero_ordem LIKE '{PrefixoOrdem}%'", null);

    private async Task<int> ContarConsumoPesagensAsync()
        => await ContarAsync($"""
            SELECT COUNT(*) FROM {Schema}.consumo_material_pesagem p
             JOIN {Schema}.consumo_material_item i
               ON i.codigo_consumo_material_item = p.codigo_consumo_material_item
            WHERE i.numero_ordem LIKE '{PrefixoOrdem}%'
            """, null);

    private async Task<int> ContarAsync(string sql, long? parametro)
    {
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        if (parametro is not null)
        {
            cmd.Parameters.AddWithValue("@p", parametro.Value);
        }
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<string> LerStatusApontamentoAsync(long codigoApontamento)
    {
        (string status, _, _, _) = await LerApontamentoAsync(codigoApontamento);
        return status;
    }

    private async Task<(string Status, string? Resultado, long? Registro, bool ConcluidoNotNull)> LerApontamentoAsync(long codigoApontamento)
    {
        const string sql = $"""
            SELECT status, resultado_operacional, codigo_registro_processo, concluido_operacional_em
              FROM {Schema}.operacao_producao_apontamento
             WHERE codigo_apontamento = @p;
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        cmd.Parameters.AddWithValue("@p", codigoApontamento);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync();
        Assert.True(await leitor.ReadAsync(), $"Apontamento {codigoApontamento} não encontrado.");
        string status = leitor.GetString(0);
        string? resultado = leitor.IsDBNull(1) ? null : leitor.GetString(1);
        long? registro = leitor.IsDBNull(2) ? null : leitor.GetInt64(2);
        bool concluido = !leitor.IsDBNull(3);
        return (status, resultado, registro, concluido);
    }

    private async Task InstalarGatilhoFalhaEventoAsync()
    {
        const string sql = $"""
            CREATE OR REPLACE FUNCTION {Schema}.fn_it100h_falha_evento() RETURNS trigger
            LANGUAGE plpgsql AS $fn$ BEGIN RAISE EXCEPTION 'IT100H_FORCED_ROLLBACK'; END; $fn$;
            DROP TRIGGER IF EXISTS trg_it100h_falha_evento ON {Schema}.operacao_producao_evento;
            CREATE TRIGGER trg_it100h_falha_evento BEFORE INSERT ON {Schema}.operacao_producao_evento
                FOR EACH ROW EXECUTE FUNCTION {Schema}.fn_it100h_falha_evento();
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RemoverGatilhoFalhaEventoAsync()
    {
        const string sql = $"""
            DROP TRIGGER IF EXISTS trg_it100h_falha_evento ON {Schema}.operacao_producao_evento;
            DROP FUNCTION IF EXISTS {Schema}.fn_it100h_falha_evento();
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ValidarSchemaAsync()
    {
        const string sql = $"""
            SELECT to_regclass('{Schema}.operacao_producao_apontamento') IS NOT NULL
               AND to_regclass('{Schema}.operacao_producao_apontamento_processo') IS NOT NULL
               AND to_regclass('{Schema}.consumo_material_lancamento') IS NOT NULL
               AND to_regclass('{Schema}.consumo_material_item') IS NOT NULL
               AND to_regclass('{Schema}.operacao_producao_evento') IS NOT NULL;
            """;
        await using NpgsqlConnection conexao = await AbrirAsync();
        await using NpgsqlCommand cmd = new(sql, conexao);
        object? ok = await cmd.ExecuteScalarAsync();
        if (ok is not bool existe || !existe)
        {
            throw new InvalidOperationException("Schema de recovery 100H ausente no banco de teste (base+delta não aplicados).");
        }
    }

    private async Task LimparResiduoAsync()
    {
        // Ordem FK-safe: evento → vínculo → apontamento; item (CASCADE) e lançamento por prefixo sintético.
        string[] comandos =
        [
            $"DELETE FROM {Schema}.operacao_producao_evento e USING {Schema}.operacao_producao_apontamento a WHERE e.codigo_apontamento = a.codigo_apontamento AND a.numero_ordem LIKE '{PrefixoOrdem}%'",
            $"DELETE FROM {Schema}.operacao_producao_apontamento_processo p USING {Schema}.operacao_producao_apontamento a WHERE p.codigo_apontamento = a.codigo_apontamento AND a.numero_ordem LIKE '{PrefixoOrdem}%'",
            $"DELETE FROM {Schema}.operacao_producao_apontamento WHERE numero_ordem LIKE '{PrefixoOrdem}%'",
            $"DELETE FROM {Schema}.consumo_material_lancamento WHERE numero_ordem LIKE '{PrefixoOrdem}%'",
        ];
        await using NpgsqlConnection conexao = await AbrirAsync();
        foreach (string sql in comandos)
        {
            await using NpgsqlCommand cmd = new(sql, conexao);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// Guard EXATO e fail-closed do alvo de teste 100H (100H-R3). Valida SEMANTICAMENTE (via
/// NpgsqlConnectionStringBuilder — nunca busca textual frágil) que a connection string aponta EXATAMENTE para o
/// banco de teste isolado autorizado, além do opt-in destrutivo e do prefixo de segurança já existentes. Não abre
/// conexão. NÃO altera/enfraquece BancoTesteIntegracao (guard aditivo, específico do 100H).
/// </summary>
internal static class Recovery100HAlvo
{
    public const string Host = "192.168.3.226";
    public const int Port = 5432;
    public const string Database = "fuga_balanca_teste_recovery_100h";
    public const string Username = "fugapet_test_recovery_100h";

    public sealed record Resultado(bool Aceito, string Motivo);

    public static Resultado Validar(string? connectionString, bool destrutivoAutorizado)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(false, $"{BancoTesteIntegracao.VariavelConnectionString} não configurada.");
        }

        if (!destrutivoAutorizado)
        {
            return new(false, $"Defina {BancoTesteIntegracao.VariavelPermitirDestrutivo}=true para autorizar testes destrutivos.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            return new(false, $"Connection string inválida: {ex.GetType().Name}.");
        }

        string database = builder.Database ?? string.Empty;
        if (!database.StartsWith(BancoTesteIntegracao.PrefixoBancoSeguro, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, $"database '{database}' não começa com '{BancoTesteIntegracao.PrefixoBancoSeguro}'.");
        }

        if (!string.Equals(builder.Host, Host, StringComparison.Ordinal))
        {
            return new(false, "host divergente do alvo 100H (localhost/127.0.0.1/outro rejeitado).");
        }

        if (builder.Port != Port)
        {
            return new(false, "porta divergente do alvo 100H.");
        }

        if (!string.Equals(database, Database, StringComparison.Ordinal))
        {
            return new(false, "database divergente do alvo 100H (mesmo com prefixo fuga_balanca_teste_).");
        }

        if (!string.Equals(builder.Username, Username, StringComparison.Ordinal))
        {
            return new(false, "role divergente do alvo 100H.");
        }

        if (builder.SslMode != SslMode.Require)
        {
            return new(false, "TLS deve ser Require (SSL desabilitado/prefer/allow rejeitado).");
        }

        return new(true, string.Empty);
    }
}

/// <summary>Fact que só executa quando o alvo é EXATAMENTE o banco de teste 100H autorizado (senão SKIP objetivo, sem tocar banco).</summary>
internal sealed class Recovery100HIntegrationFactAttribute : FactAttribute
{
    public Recovery100HIntegrationFactAttribute()
    {
        string? conn = Environment.GetEnvironmentVariable(BancoTesteIntegracao.VariavelConnectionString);
        Recovery100HAlvo.Resultado resultado = Recovery100HAlvo.Validar(conn, BancoTesteIntegracao.DestrutivoAutorizado());
        if (!resultado.Aceito)
        {
            Skip = $"Integração recovery 100H ignorada: {resultado.Motivo}";
        }
    }
}

/// <summary>
/// 100H-R3 — testes DB-FREE do guard exato do alvo (nenhuma conexão PostgreSQL). Provam fail-closed:
/// alvo exato aceita; host/porta/database/role divergentes rejeitam; TLS ≠ Require rejeita; opt-in ausente rejeita.
/// </summary>
public sealed class Recovery100HAlvoGuardTests
{
    private const string Alvo =
        "Host=192.168.3.226;Port=5432;Database=fuga_balanca_teste_recovery_100h;Username=fugapet_test_recovery_100h;Password=x;SSL Mode=Require";

    [Fact]
    public void AlvoExatoComOptIn_Aceita()
        => Assert.True(Recovery100HAlvo.Validar(Alvo, destrutivoAutorizado: true).Aceito);

    [Fact]
    public void OptInDestrutivoAusente_Rejeita()
    {
        Recovery100HAlvo.Resultado r = Recovery100HAlvo.Validar(Alvo, destrutivoAutorizado: false);
        Assert.False(r.Aceito);
    }

    [Fact]
    public void ConnectionStringVazia_Rejeita()
        => Assert.False(Recovery100HAlvo.Validar(null, destrutivoAutorizado: true).Aceito);

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=fuga_balanca_teste_recovery_100h;Username=fugapet_test_recovery_100h;SSL Mode=Require")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=fuga_balanca_teste_recovery_100h;Username=fugapet_test_recovery_100h;SSL Mode=Require")]
    [InlineData("Host=192.168.3.999;Port=5432;Database=fuga_balanca_teste_recovery_100h;Username=fugapet_test_recovery_100h;SSL Mode=Require")]
    public void HostDivergente_Rejeita(string conn)
        => Assert.False(Recovery100HAlvo.Validar(conn, destrutivoAutorizado: true).Aceito);

    [Fact]
    public void PortaDivergente_Rejeita()
        => Assert.False(Recovery100HAlvo.Validar(Alvo.Replace("Port=5432", "Port=5433", StringComparison.Ordinal), true).Aceito);

    [Fact]
    public void DatabaseDivergenteMasComPrefixoSeguro_Rejeita()
        => Assert.False(Recovery100HAlvo.Validar(
            Alvo.Replace("Database=fuga_balanca_teste_recovery_100h", "Database=fuga_balanca_teste_outro", StringComparison.Ordinal), true).Aceito);

    [Fact]
    public void RoleDivergente_Rejeita()
        => Assert.False(Recovery100HAlvo.Validar(
            Alvo.Replace("Username=fugapet_test_recovery_100h", "Username=postgres", StringComparison.Ordinal), true).Aceito);

    [Theory]
    [InlineData("SSL Mode=Disable")]
    [InlineData("SSL Mode=Prefer")]
    [InlineData("SSL Mode=Allow")]
    public void TlsDiferenteDeRequire_Rejeita(string ssl)
        => Assert.False(Recovery100HAlvo.Validar(Alvo.Replace("SSL Mode=Require", ssl, StringComparison.Ordinal), true).Aceito);
}
