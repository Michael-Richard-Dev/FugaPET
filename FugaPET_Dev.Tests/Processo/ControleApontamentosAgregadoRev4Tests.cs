using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 048-E REV4 — conclusão AGREGADA 1:N. Um único processo terminal NÃO conclui a operação sozinho:
/// só CONCLUIDA quando TODOS os requisitos SAP (por Reservation+ReservationItem) estão satisfeitos.
/// Os valores 0050/8/63/1001961 aparecem apenas como MASSA de teste (não são regra hardcoded).
/// </summary>
public sealed class ControleApontamentosAgregadoRev4Tests
{
    private static ComponenteOrdemProducaoSap Req(
        string reserva, string item, decimal necessaria, decimal retirada, bool finalIssue, string operacao = "0050", string material = "MAT")
        => new()
        {
            Operacao = operacao,
            Reserva = reserva,
            ItemReserva = item,
            Material = material,
            QuantidadeNecessaria = necessaria,
            QuantidadeRetirada = retirada,
            ReservaFinalizada = finalIssue
        };

    private static ApontamentoProcesso ProcessoTerminal(
        string reserva,
        string item,
        long registro = 63,
        string tipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima)
        => new()
        {
            CodigoApontamento = 8,
            TipoProcesso = tipoProcesso,
            CodigoRegistroProcesso = registro,
            StatusLancamento = "CONFIRMADO_SAP",
            DocumentoMaterialSap = "4900005594",
            ExercicioMaterialSap = "2026",
            Reservation = reserva,
            ReservationItem = item
        };

    private static ComponenteConsumoDecisaoOperacional Zero(
        string apontamento,
        string reserva,
        string item,
        string material)
        => new()
        {
            CodigoApontamento = long.Parse(apontamento, System.Globalization.CultureInfo.InvariantCulture),
            NumeroReserva = reserva,
            ItemReserva = item,
            CodigoMaterial = material,
            DecisaoOperacional = ComponenteConsumoDecisaoOperacional.DecisaoZeroIntencional,
            Quantidade = 0m,
            Unidade = "KG"
        };

    // ---------- Terminalidade (§5) ----------

    [Fact] // 9: processo terminal (CONFIRMADO_SAP + documento/exercício) — não elegível para repost.
    public void ProcessoTerminal_ConsumoConfirmadoComDocumento_EhTerminal()
        => Assert.True(AvaliadorConclusaoAgregada.EhProcessoTerminal(ProcessoTerminal("R1", "0010")));

    [Fact] // sem documento/exercício NÃO é terminal (não pode preservar conclusão indevidamente).
    public void ProcessoSemDocumento_NaoEhTerminal()
        => Assert.False(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("R1", "0010") with { DocumentoMaterialSap = "", ExercicioMaterialSap = "" }));

    [Fact] // status diferente de CONFIRMADO_SAP não é terminal.
    public void ProcessoNaoConfirmado_NaoEhTerminal()
        => Assert.False(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("R1", "0010") with { StatusLancamento = "REGISTRADO_LOCALMENTE" }));

    [Fact] // 050/0060 Q1: CONSUMO_QUIMICOS confirmado com documento/exercício também é terminal.
    public void ProcessoTerminal_ConsumoQuimicosConfirmadoComDocumento_EhTerminal()
        => Assert.True(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("14391", "1", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos)));

    [Theory] // 050/0060 Q2: estados não terminais de químicos não cobrem requisito.
    [InlineData("PENDENTE_SAP")]
    [InlineData("ENVIANDO_SAP")]
    [InlineData("FALHA_SAP")]
    public void ProcessoTerminal_ConsumoQuimicosStatusNaoTerminal_NaoEhTerminal(string status)
        => Assert.False(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("14391", "1", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos) with
            {
                StatusLancamento = status
            }));

    [Fact] // 050/0060 Q3: documento é obrigatório para terminalidade química.
    public void ProcessoTerminal_ConsumoQuimicosSemDocumento_NaoEhTerminal()
        => Assert.False(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("14391", "1", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos) with
            {
                DocumentoMaterialSap = ""
            }));

    [Fact] // 050/0060 Q4: exercício é obrigatório para terminalidade química.
    public void ProcessoTerminal_ConsumoQuimicosSemExercicio_NaoEhTerminal()
        => Assert.False(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("14391", "1", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos) with
            {
                ExercicioMaterialSap = ""
            }));

    // GATE FUGAPET-Q-TEST-SNAPSHOT-ALIGNMENT-07: ConsumoSemiAcabado nao integra o contrato produtivo materialmente comprovado deste snapshot Q; nao reativar sem source/contrato autoritativo e novo gate.
    [Theory] // 050/0060 Q6: tipos não autorizados não ganham terminalidade por esta corretiva.
    [InlineData(TipoProcessoOperacao.ResultadoApontamento)]
    [InlineData(TipoProcessoOperacao.SemiAcabado)]
    [InlineData("TIPO_FUTURO")]
    public void ProcessoTerminal_TipoNaoAutorizado_NaoEhTerminal(string tipoProcesso)
        => Assert.False(AvaliadorConclusaoAgregada.EhProcessoTerminal(
            ProcessoTerminal("14391", "1", tipoProcesso: tipoProcesso)));

    // ---------- Avaliação agregada (§4) ----------

    [Fact] // 7: todos os requisitos concluídos (FinalIssue) → CONCLUIDA.
    public void Avaliar_TodosRequisitosConcluidos_Concluida()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("R1", "0010", 10m, 10m, finalIssue: true),
            Req("R2", "0020", 5m, 5m, finalIssue: true)
        ];

        Assert.Equal(EstadoConclusaoAgregada.Concluida, AvaliadorConclusaoAgregada.Avaliar(requisitos, []));
    }

    [Fact] // 6: um processo CONFIRMADO_SAP (terminal, cobre R1) + outro requisito SAP pendente (R2) → PENDENTE.
    public void Avaliar_UmConfirmadoOutroPendente_Pendente()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("R1", "0010", 10m, 10m, finalIssue: false),
            Req("R2", "0020", 10m, 0m, finalIssue: false)
        ];
        IReadOnlyList<ApontamentoProcesso> vinculos = [ProcessoTerminal("R1", "0010")];

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 8: universo SAP incompleto/ausente → INDETERMINADA (fail-closed).
    public void Avaliar_UniversoSapVazio_Indeterminada()
        => Assert.Equal(EstadoConclusaoAgregada.Indeterminada, AvaliadorConclusaoAgregada.Avaliar([], []));

    [Fact] // 8b: requisito sem identidade Reservation/ReservationItem → INDETERMINADA (não presume por material).
    public void Avaliar_RequisitoSemIdentidade_Indeterminada()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos = [Req("", "", 10m, 0m, finalIssue: false)];
        Assert.Equal(EstadoConclusaoAgregada.Indeterminada, AvaliadorConclusaoAgregada.Avaliar(requisitos, []));
    }

    [Fact] // 10: SAP reporta requisito pendente (stale), mas processo local terminal o cobre → não reabre → CONCLUIDA.
    public void Avaliar_ProcessoTerminalCobreRequisitoStale_Concluida()
    {
        // FinalIssue=false e Required(10) > Withdrawn(0): SAP "pendente" (stale, como o processo 63).
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos = [Req("R1", "0010", 10m, 0m, finalIssue: false)];
        IReadOnlyList<ApontamentoProcesso> vinculos = [ProcessoTerminal("R1", "0010")];

        Assert.Equal(EstadoConclusaoAgregada.Concluida, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 050/0060 Q7: nove requisitos químicos cobertos por processos CONFIRMADO_SAP concluem mesmo com Withdrawn=0.
    public void Avaliar_ConsumoQuimicosNoveRequisitosCobertos_Concluida()
    {
        string[] itens = ["1", "2", "3", "6", "7", "8", "9", "10", "11"];
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos = itens
            .Select(item => Req("14391", item, 1m, 0m, finalIssue: false, operacao: "0060"))
            .ToList();
        IReadOnlyList<ApontamentoProcesso> vinculos = itens
            .Select((item, indice) => ProcessoTerminal(
                "14391",
                item,
                100 + indice,
                TipoProcessoOperacao.ConsumoQuimicos))
            .ToList();

        Assert.Equal(EstadoConclusaoAgregada.Concluida, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 050/0060 Q8: oito de nove requisitos químicos não concluem a ocorrência.
    public void Avaliar_ConsumoQuimicosOitoDeNoveCobertos_Pendente()
    {
        string[] itens = ["1", "2", "3", "6", "7", "8", "9", "10", "11"];
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos = itens
            .Select(item => Req("14391", item, 1m, 0m, finalIssue: false, operacao: "0060"))
            .ToList();
        IReadOnlyList<ApontamentoProcesso> vinculos = itens
            .Take(8)
            .Select((item, indice) => ProcessoTerminal(
                "14391",
                item,
                100 + indice,
                TipoProcessoOperacao.ConsumoQuimicos))
            .ToList();

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 050/0060 Q9: ReservationItem diferente não cobre o requisito.
    public void Avaliar_ConsumoQuimicosReservationItemDiferente_NaoCobre()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "1", 1m, 0m, finalIssue: false, operacao: "0060")
        ];
        IReadOnlyList<ApontamentoProcesso> vinculos =
        [
            ProcessoTerminal("14391", "2", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos)
        ];

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 050/0060 V4: vínculo químico sem Reservation não cobre requisito.
    public void Avaliar_ConsumoQuimicosReservationAusente_NaoCobre()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "1", 1m, 0m, finalIssue: false, operacao: "0060")
        ];
        IReadOnlyList<ApontamentoProcesso> vinculos =
        [
            ProcessoTerminal("", "1", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos)
        ];

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 050/0060 V5: vínculo químico sem ReservationItem não cobre requisito.
    public void Avaliar_ConsumoQuimicosReservationItemAusente_NaoCobre()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "1", 1m, 0m, finalIssue: false, operacao: "0060")
        ];
        IReadOnlyList<ApontamentoProcesso> vinculos =
        [
            ProcessoTerminal("14391", "", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos)
        ];

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos));
    }

    [Fact] // 050/0060 Z1/Z3: ausência de pesagem e de decisão explícita permanece pendente.
    public void Avaliar_ConsumoQuimicosSemPesagemESemZero_Pendente()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "2", 1m, 0m, finalIssue: false, operacao: "0060", material: "3500024")
        ];

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, []));
    }

    [Fact] // 050/0060 Z2: ZERO_INTENCIONAL explícito cobre localmente o requisito exato.
    public void Avaliar_ConsumoQuimicosZeroIntencional_CobreRequisito()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "2", 1m, 0m, finalIssue: false, operacao: "0060", material: "3500024")
        ];
        IReadOnlyList<ComponenteConsumoDecisaoOperacional> zeros =
        [
            Zero("11", "14391", "2", "3500024")
        ];

        Assert.Equal(EstadoConclusaoAgregada.Concluida, AvaliadorConclusaoAgregada.Avaliar(requisitos, [], zeros));
    }

    [Fact] // 050/0060 Z8: cinco positivos confirmados + quatro zeros explícitos resolvem 9/9.
    public void Avaliar_ConsumoQuimicosCincoPositivosQuatroZeros_Concluida()
    {
        string[] positivos = ["1", "7", "8", "9", "11"];
        string[] zeros = ["2", "3", "6", "10"];
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos = positivos.Concat(zeros)
            .Select(item => Req("14391", item, 1m, 0m, finalIssue: false, operacao: "0060", material: $"MAT-{item}"))
            .ToList();
        IReadOnlyList<ApontamentoProcesso> vinculos = positivos
            .Select((item, indice) => ProcessoTerminal("14391", item, 100 + indice, TipoProcessoOperacao.ConsumoQuimicos))
            .ToList();
        IReadOnlyList<ComponenteConsumoDecisaoOperacional> decisoes = zeros
            .Select(item => Zero("11", "14391", item, $"MAT-{item}"))
            .ToList();

        Assert.Equal(EstadoConclusaoAgregada.Concluida, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos, decisoes));
    }

    [Fact] // 050/0060 Z9: cinco positivos + três zeros + um ausente não concluem.
    public void Avaliar_ConsumoQuimicosCincoPositivosTresZerosUmAusente_Pendente()
    {
        string[] positivos = ["1", "7", "8", "9", "11"];
        string[] zeros = ["2", "3", "6"];
        string ausente = "10";
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos = positivos.Concat(zeros).Append(ausente)
            .Select(item => Req("14391", item, 1m, 0m, finalIssue: false, operacao: "0060", material: $"MAT-{item}"))
            .ToList();
        IReadOnlyList<ApontamentoProcesso> vinculos = positivos
            .Select((item, indice) => ProcessoTerminal("14391", item, 100 + indice, TipoProcessoOperacao.ConsumoQuimicos))
            .ToList();
        IReadOnlyList<ComponenteConsumoDecisaoOperacional> decisoes = zeros
            .Select(item => Zero("11", "14391", item, $"MAT-{item}"))
            .ToList();

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, vinculos, decisoes));
    }

    [Fact] // 050/0060 Z10/Z11/Z12: identidade divergente não cobre.
    public void Avaliar_ZeroIntencionalIdentidadeDivergente_NaoCobre()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "2", 1m, 0m, finalIssue: false, operacao: "0060", material: "3500024")
        ];

        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, [], [Zero("11", "OUTRA", "2", "3500024")]));
        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, [], [Zero("11", "14391", "3", "3500024")]));
        Assert.Equal(EstadoConclusaoAgregada.Pendente, AvaliadorConclusaoAgregada.Avaliar(requisitos, [], [Zero("11", "14391", "2", "OUTRO")]));
    }

    [Fact] // 050/0060 Z14: positivo confirmado + zero no mesmo requisito fica fail-closed.
    public void Avaliar_PositivoEZeroMesmoRequisito_Indeterminada()
    {
        IReadOnlyList<ComponenteOrdemProducaoSap> requisitos =
        [
            Req("14391", "2", 1m, 0m, finalIssue: false, operacao: "0060", material: "3500024")
        ];

        Assert.Equal(
            EstadoConclusaoAgregada.Indeterminada,
            AvaliadorConclusaoAgregada.Avaliar(
                requisitos,
                [ProcessoTerminal("14391", "2", tipoProcesso: TipoProcessoOperacao.ConsumoQuimicos)],
                [Zero("11", "14391", "2", "3500024")]));
    }

    [Fact] // 050/0060 Q10: outro apontamento/ocorrência não chega ao avaliador porque o serviço lista por codigo_apontamento.
    public void Repositorio_ProcessosVinculados_SaoListadosPorCodigoApontamento()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("WHERE v.codigo_apontamento = @codigo", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("MAX(", fonte, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // 050/0060 Q11: avaliador agregado permanece puro, sem SAP/repository/HTTP.
    public void AvaliadorAgregado_NaoExecutaSapBancoOuHttp()
    {
        string fonte = LerProjeto("Servicos", "Processo", "AvaliadorConclusaoAgregada.cs");

        Assert.DoesNotContain("ConsultarOrdemAsync", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", fonte, StringComparison.Ordinal);
    }

    [Fact] // 050/0060 Q12: ConfirmadoSap continua sendo resultado terminal para o caminho AGUARDANDO_FINALIZACAO.
    public void ResultadoExecucaoProcesso_ConfirmadoSap_EhAtividadeConcluida()
    {
        ResultadoExecucaoProcesso resultado = new(
            ResultadoExecucaoProcessoApontamento.ConfirmadoSap,
            71,
            "Confirmado SAP",
            indicadorConfirmadoSap: true);

        Assert.True(resultado.AtividadeConcluida);
    }

    // ---------- Vínculo 1:N — semântica de registro (§2) ----------
    // Store de referência EM MEMÓRIA modelando o contrato SQL (tripla idempotente + legado write-once)
    // exigido do repositório real (ver também o source-scan abaixo).
    private sealed class VinculoStoreReferencia
    {
        private readonly HashSet<(long, string, long)> _triplas = [];
        private readonly List<ApontamentoProcesso> _vinculos = [];
        public long? LegadoRegistro { get; private set; }

        public void Registrar(long apontamento, string tipo, long registro)
        {
            if (!_triplas.Add((apontamento, tipo, registro)))
            {
                return; // mesma tripla não duplica
            }

            _vinculos.Add(new ApontamentoProcesso
            {
                CodigoApontamento = apontamento,
                TipoProcesso = tipo,
                CodigoRegistroProcesso = registro
            });
            LegadoRegistro ??= registro; // write-once
        }

        public IReadOnlyList<ApontamentoProcesso> Listar() => _vinculos;
    }

    [Fact] // 1
    public void Vinculo_RegistrarPrimeiro_CriaUm()
    {
        VinculoStoreReferencia store = new();
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 63);
        Assert.Single(store.Listar());
    }

    [Fact] // 2
    public void Vinculo_RegistrarSegundoDiferente_CriaDois()
    {
        VinculoStoreReferencia store = new();
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 63);
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 64);
        Assert.Equal(2, store.Listar().Count);
    }

    [Fact] // 3
    public void Vinculo_MesmaTripla_Idempotente()
    {
        VinculoStoreReferencia store = new();
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 63);
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 63);
        Assert.Single(store.Listar());
    }

    [Fact] // 4 e 5: legado preenchido só uma vez; segundo vínculo não sobrescreve.
    public void Vinculo_Legado_WriteOnce()
    {
        VinculoStoreReferencia store = new();
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 63);
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 64);
        Assert.Equal(63, store.LegadoRegistro);
    }

    [Fact] // 14: reload/listagem recupera todos os vínculos.
    public void Vinculo_Listagem_RecuperaTodos()
    {
        VinculoStoreReferencia store = new();
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 63);
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 64);
        store.Registrar(8, TipoProcessoOperacao.ConsumoMateriaPrima, 65);
        Assert.Equal(new long[] { 63, 64, 65 }, store.Listar().Select(v => v.CodigoRegistroProcesso).ToArray());
    }

    [Fact] // O repositório REAL implementa a tripla idempotente + o legado write-once no SQL.
    public void Repositorio_Sql_TemIdempotenciaELegadoWriteOnce()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("operacao_producao_apontamento_processo", fonte, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (codigo_apontamento, tipo_processo, codigo_registro_processo) DO NOTHING", fonte, StringComparison.Ordinal);
        Assert.Contains("codigo_registro_processo IS NULL", fonte, StringComparison.Ordinal); // legado write-once
    }

    [Fact] // REV4A §8: SQL do repositório estruturalmente compatível com o DDL congelado da tabela 1:N.
    public void Repositorio_Sql_CompativelComDdlCongelado_Rev4A()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        // A) INSERT usa SOMENTE colunas existentes no DDL congelado (+ criado_em/atualizado_em).
        Assert.Contains("INSERT INTO operacao_producao_apontamento_processo", fonte, StringComparison.Ordinal);
        Assert.Contains("(codigo_apontamento, tipo_processo, codigo_registro_processo, criado_em, atualizado_em)", fonte, StringComparison.Ordinal);

        // B) O SELECT NÃO referencia reservation/reservation_item como colunas da tabela 1:N (v.*).
        Assert.DoesNotContain("v.reservation", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("v.reservation_item", fonte, StringComparison.Ordinal);

        // C) status/documento/exercício e reserva/item vêm por JOIN nas entidades TIPADAS.
        Assert.Contains("LEFT JOIN consumo_material_lancamento cml", fonte, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN consumo_material_item cmi", fonte, StringComparison.Ordinal);
        Assert.Contains("cml.exercicio_documento_material_sap", fonte, StringComparison.Ordinal);
        Assert.Contains("cmi.numero_reserva", fonte, StringComparison.Ordinal);
        Assert.Contains("cmi.item_reserva", fonte, StringComparison.Ordinal);

        // D) NÃO usa o nome físico errado do exercício, nem colunas SAP stale no vínculo.
        Assert.DoesNotContain("exercicio_material_sap", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("v.status_lancamento", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("v.quantidade", fonte, StringComparison.Ordinal);
    }

    [Fact] // 050/0060 V1/V2: repository enriquece MP e Químicos pelo mesmo JOIN tipado.
    public void Repositorio_Sql_EnriqueceMateriaPrimaEQuimicos()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("v.tipo_processo = ANY(@tipos_consumo)", fonte, StringComparison.Ordinal);
        Assert.Contains("TipoProcessoOperacao.ConsumoMateriaPrima", fonte, StringComparison.Ordinal);
        Assert.Contains("TipoProcessoOperacao.ConsumoQuimicos", fonte, StringComparison.Ordinal);
        Assert.Contains("cml.status_lancamento", fonte, StringComparison.Ordinal);
        Assert.Contains("cml.documento_material_sap", fonte, StringComparison.Ordinal);
        Assert.Contains("cml.exercicio_documento_material_sap", fonte, StringComparison.Ordinal);
        Assert.Contains("cmi.numero_reserva", fonte, StringComparison.Ordinal);
        Assert.Contains("cmi.item_reserva", fonte, StringComparison.Ordinal);
    }

    [Fact] // 050/0060 V8: tipos fora de MP/Químicos não entram no enriquecimento de consumo.
    public void Repositorio_Sql_NaoGeneralizaEnriquecimentoParaOutrosTipos()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.DoesNotContain("v.tipo_processo IS NOT NULL", fonte, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LEFT JOIN consumo_material_lancamento cml\r\n                    ON cml.codigo_consumo_material_lancamento", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoProcessoOperacao.ResultadoApontamento", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoProcessoOperacao.SemiAcabado", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoProcessoOperacao.ConsumoSemiAcabado", fonte, StringComparison.Ordinal);
    }

    [Fact] // 050/0060 Z4/Z5/Z15/Z16: decisão zero usa tabela própria, insert-only e reload.
    public void Repositorio_ZeroIntencional_InsertOnlyReloadEIdempotencia()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("INSERT INTO consumo_material_componente_decisao", fonte, StringComparison.Ordinal);
        Assert.Contains("'ZERO_INTENCIONAL'", fonte, StringComparison.Ordinal);
        Assert.Contains("quantidade, unidade, usuario, estacao", fonte, StringComparison.Ordinal);
        Assert.Contains("ListarDecisoesZeroIntencionalAsync", fonte, StringComparison.Ordinal);
        Assert.Contains("WHERE codigo_apontamento = @codigo_apontamento", fonte, StringComparison.Ordinal);
        Assert.Contains("SqlStateUniqueViolation", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE consumo_material_componente_decisao", fonte, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM consumo_material_componente_decisao", fonte, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // 050/0060 Z7/Z20: payload 261 positivo permanece separado da decisão zero.
    public void Payload261_NaoConheceTabelaDeZeroIntencional()
    {
        string payload = LerProjeto("Servicos", "IntegracaoSap", "ConsumoMaterialSapPayloadBuilder.cs");
        string repo = LerProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("if (item.QuantidadeConsumidaLocal <= 0m)", payload, StringComparison.Ordinal);
        Assert.Contains("continue;", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("consumo_material_componente_decisao", payload, StringComparison.Ordinal);
        Assert.Contains("consumo_material_componente_decisao", repo, StringComparison.Ordinal);
    }

    [Fact] // 050/0060 Z2/Z3/Z13: Form exige ação explícita e não infere por Peso Utilizado = 0.
    public void Form_ZeroIntencional_AcaoExplicitaENaoInferidaPorPesoZero()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("CriarBotaoNaoConsumido", form, StringComparison.Ordinal);
        Assert.Contains("Confirmar que este componente será registrado como NÃO CONSUMIDO", form, StringComparison.Ordinal);
        Assert.Contains("PossuiPesagemLocal(componente)", form, StringComparison.Ordinal);
        Assert.Contains("ListarDecisoesZeroIntencionalAsync", form, StringComparison.Ordinal);
        Assert.Contains("codigoApontamento", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Peso Utilizado = 0", form, StringComparison.Ordinal);
    }

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}
