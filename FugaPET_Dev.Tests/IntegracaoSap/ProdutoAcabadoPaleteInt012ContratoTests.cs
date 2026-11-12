using FugaPET_Dev.Modelo.Ambiente;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 046-K: contrato INT012 — HandlingUnitExternalID é o literal "$1" (nunca o CodigoPaleteLocal); os itens
/// (_HandlingUnitItem) carregam exclusivamente a HU SAP individual da caixa; e a autenticação CPI usa credencial
/// DEDICADA (fail-closed sem fallback para as credenciais SAP genéricas). Nenhum POST/HTTP é executado aqui.
/// </summary>
public sealed class ProdutoAcabadoPaleteInt012ContratoTests
{
    private const string CodigoLocal = "PLT-1002024-0002-0003";

    private static ProdutoAcabadoPalete PaleteValido() => new()
    {
        CodigoPaleteLocal = CodigoLocal,
        PrimeiraCaixa = 2,
        UltimaCaixa = 3,
        PesoBrutoKg = 20m,
        PesoLiquidoKg = 18m,
        TaraKg = 2m,
        Plant = "3007",
        StorageLocation = "PP01",
        PackagingMaterial = "PACK_TESTE_046K", // valor de teste — nunca PALLET01/4000108; não há POST
        Caixas =
        [
            Caixa(2, "HU-CX-A"),
            Caixa(3, "HU-CX-B"),
        ]
    };

    private static ProdutoAcabadoCaixa Caixa(int numero, string huSap) => new()
    {
        CodigoProdutoAcabadoCaixa = numero,
        NumeroCaixa = numero,
        CodigoCaixaLocal = $"CX-LOCAL-{numero:0000}",
        StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
        HandlingUnitExternalId = huSap,
        PesoBrutoKg = 10m,
        PesoLiquidoKg = 9m,
        TaraKg = 1m
    };

    /// <summary>GATE 126V/126X: tara fisica JA RESOLVIDA, injetada no builder (que permanece puro).</summary>
    private static ResultadoTaraFisicaEmbalagem TaraFisica(decimal pesoKg)
        => ResultadoTaraFisicaEmbalagem.Resolvida(IdentidadeQ(), pesoKg);

    /// <summary>
    /// GATE 126X: identidade QAS110 obtida pelo VALIDADOR DE PRODUCAO, a partir de uma configuracao
    /// Q-valida. Nao existe fabrica de identidade falsa: o teste exercita o mesmo caminho do runtime.
    /// </summary>
    private static IdentidadeAmbienteQas110 IdentidadeQ()
    {
        ResultadoIdentidadeQas110 r = ValidadorIdentidadeQas110.Resolver(ConfigQValida());
        Assert.True(r.Confirmada, r.Mensagem);
        return r.Identidade!;
    }

    /// <summary>Configuracao que satisfaz o contrato Q REAL: perfil Q + host/porta Q + allowlist Q.</summary>
    private static ConfiguracaoSap ConfigQValida(
        IReadOnlyList<TaraFisicaEmbalagemConfigurada>? taras = null) => new()
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_PURCHASEORDER_SRV"),
            MaterialDocumentBaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = MandanteQ,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado],
            Usuario = "u",
            Senha = "p",
            TimeoutSegundos = 30,
            TarasFisicasEmbalagemPalete = taras ?? []
        };

    /// <summary>Mesma configuracao, porem apontada para o host DS — com o MESMO mandante 110.</summary>
    private static ConfiguracaoSap ConfigDsMandante110(
        IReadOnlyList<TaraFisicaEmbalagemConfigurada>? taras = null) => new()
        {
            PerfilLocalQ = true, // perfil declarado Q de proposito: provar que perfil nao basta
            BaseUrl = UrlDs("API_PURCHASEORDER_SRV"),
            MaterialDocumentBaseUrl = UrlDs("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = MandanteQ, // 110, identico ao do Q
            HostsPermitidos = [ValidadorAmbienteQ.SapHostDesenvolvimento],
            Usuario = "u",
            Senha = "p",
            TimeoutSegundos = 30,
            TarasFisicasEmbalagemPalete = taras ?? []
        };

    private static string UrlQ(string servico)
        => $"https://{ValidadorAmbienteQ.SapHostEsperado}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/opu/odata/sap/{servico}/";

    private static string UrlDs(string servico)
        => $"https://{ValidadorAmbienteQ.SapHostDesenvolvimento}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/opu/odata/sap/{servico}/";

    [Fact] // §3/§4: HandlingUnitExternalID == "$1" e != CodigoPaleteLocal
    public void HandlingUnitExternalId_EhLiteralDolar1_NaoOCodigoLocal()
    {
        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaleteValido(), TaraFisica(1m));

        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.NotNull(preview.Payload);
        Assert.Equal("$1", preview.Payload!.HandlingUnitExternalID);
        Assert.NotEqual(CodigoLocal, preview.Payload.HandlingUnitExternalID);
        Assert.Contains("\"HandlingUnitExternalID\": \"$1\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(CodigoLocal, preview.PayloadJson, StringComparison.Ordinal); // código local NÃO viaja no payload
    }

    [Fact] // §4: _HandlingUnitItem[].HandlingUnit = HU SAP individual da caixa, sem fallback para código local
    public void ItemHandlingUnit_UsaHuSapDaCaixa_SemFallbackLocal()
    {
        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaleteValido(), TaraFisica(1m));

        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.Equal(["HU-CX-A", "HU-CX-B"], preview.Payload!.HandlingUnitItems.Select(i => i.HandlingUnit));
        Assert.DoesNotContain("CX-LOCAL-0002", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CX-LOCAL-0003", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact] // GATE 046-K REV2 §9: PALLET01 é o PackagingMaterial oficial do MVP (INT012 v1.00) — ACEITO e serializado.
    public void PackagingMaterial_Pallet01_Aceito_ESerializado()
    {
        ProdutoAcabadoPalete palete = PaleteValido();
        ProdutoAcabadoPalete comPallet01 = new()
        {
            CodigoPaleteLocal = palete.CodigoPaleteLocal,
            PrimeiraCaixa = palete.PrimeiraCaixa,
            UltimaCaixa = palete.UltimaCaixa,
            PesoBrutoKg = palete.PesoBrutoKg,
            PesoLiquidoKg = palete.PesoLiquidoKg,
            TaraKg = palete.TaraKg,
            Plant = palete.Plant,
            StorageLocation = palete.StorageLocation,
            PackagingMaterial = "PALLET01",
            Caixas = palete.Caixas
        };

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(comPallet01, TaraFisica(1m));

        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.Equal("PALLET01", preview.Payload!.PackagingMaterial);
        Assert.Contains("\"PackagingMaterial\": \"PALLET01\"", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Theory] // fail-closed preservado: vazio/branco/null seguem bloqueados
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void PackagingMaterial_AusenteVazioBranco_Bloqueado(string? material)
    {
        ProdutoAcabadoPalete p = PaleteValido();
        ProdutoAcabadoPalete semMaterial = new()
        {
            CodigoPaleteLocal = p.CodigoPaleteLocal,
            PrimeiraCaixa = p.PrimeiraCaixa,
            UltimaCaixa = p.UltimaCaixa,
            PesoBrutoKg = p.PesoBrutoKg,
            PesoLiquidoKg = p.PesoLiquidoKg,
            TaraKg = p.TaraKg,
            Plant = p.Plant,
            StorageLocation = p.StorageLocation,
            PackagingMaterial = material ?? string.Empty,
            Caixas = p.Caixas
        };

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(semMaterial, TaraFisica(1m));
        Assert.False(preview.Sucesso);
    }

    [Fact] // §5/§6: credencial CPI DEDICADA ausente ⇒ gateway NÃO autorizado, mesmo com credencial SAP presente (sem fallback)
    public void Gateway_SemCredencialCpiDedicada_FailClosed_SemFallbackSap()
    {
        ConfiguracaoSap config = ConfigCpi(comCredencialCpi: false, writeHabilitado: true);
        IProdutoAcabadoPaleteInt012Gateway gateway = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(config);
        Assert.False(gateway.EnvioAutorizado);
    }

    [Fact] // §6: com credencial CPI dedicada + write + url + host ⇒ autorizado
    public void Gateway_ComCredencialCpiDedicada_Autoriza()
    {
        ConfiguracaoSap config = ConfigCpi(comCredencialCpi: true, writeHabilitado: true);
        IProdutoAcabadoPaleteInt012Gateway gateway = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(config);
        Assert.True(gateway.EnvioAutorizado);
    }

    [Fact] // §9: write desabilitado ⇒ fail-closed mesmo com credencial CPI presente
    public void Gateway_SemWrite_FailClosed()
    {
        ConfiguracaoSap config = ConfigCpi(comCredencialCpi: true, writeHabilitado: false);
        IProdutoAcabadoPaleteInt012Gateway gateway = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(config);
        Assert.False(gateway.EnvioAutorizado);
    }

    // ========================================================================================
    // GATE 126V: composicao da tara FISICA da embalagem do palete EXCLUSIVAMENTE no outbound.
    // Cenario aprovado: PLANT=3007, PACKAGING_MATERIAL=PALLET01, PHYSICAL_TARE=1.000 KG,
    // local 28.800 / 28.700 / 0.100 ⇒ outbound 29.800 / 28.700 / 1.100.
    // ========================================================================================

    private const string MandanteQ = "110";
    private const string CentroQ = "3007";
    private const string MaterialPalete = "PALLET01";

    /// <summary>Palete 1 do piloto: pesos LOCAIS persistidos, sem qualquer tara de embalagem fisica.</summary>
    private static ProdutoAcabadoPalete PaletePiloto1() => new()
    {
        CodigoPaleteLocal = "1",
        PrimeiraCaixa = 1,
        UltimaCaixa = 2,
        PesoBrutoKg = 28.800m,
        PesoLiquidoKg = 28.700m,
        TaraKg = 0.100m,
        Plant = CentroQ,
        StorageLocation = "PA01",
        PackagingMaterial = MaterialPalete,
        Caixas =
        [
            CaixaPiloto(1, "300000100"),
            CaixaPiloto(2, "300000101"),
        ]
    };

    private static ProdutoAcabadoCaixa CaixaPiloto(int numero, string huSap) => new()
    {
        CodigoProdutoAcabadoCaixa = numero,
        NumeroCaixa = numero,
        CodigoCaixaLocal = $"CX-LOCAL-{numero:0000}",
        StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
        HandlingUnitExternalId = huSap,
        PesoBrutoKg = 14.400m,
        PesoLiquidoKg = 14.350m,
        TaraKg = 0.050m
    };

    /// <summary>Mapeamento APROVADO: mandante 110 + centro 3007 + PALLET01 ⇒ 1.000 KG.</summary>
    private static IReadOnlyList<TaraFisicaEmbalagemConfigurada> MapeamentoAprovado() =>
    [
        new()
        {
            SapClient = MandanteQ,
            Centro = CentroQ,
            MaterialEmbalagem = MaterialPalete,
            PesoKg = 1.000m,
            Unidade = "KG"
        }
    ];

    /// <summary>
    /// GATE 126X: a resolucao exige IDENTIDADE QAS110 comprovada. Por padrao usa a identidade valida
    /// emitida pelo validador de producao; os testes de bloqueio passam identidade nula ou de outro
    /// mandante.
    /// </summary>
    private static ResultadoTaraFisicaEmbalagem ResolverPiloto(
        IReadOnlyList<TaraFisicaEmbalagemConfigurada>? mapeamentos,
        IdentidadeAmbienteQas110? identidade = null,
        string centro = CentroQ,
        string material = MaterialPalete)
        => ResolvedorTaraFisicaEmbalagemPalete.Resolver(
            mapeamentos, identidade ?? IdentidadeQ(), centro, material);

    /// <summary>Identidade valida porem de OUTRO mandante (destino Q, sap-client diferente).</summary>
    private static IdentidadeAmbienteQas110 IdentidadeComMandante(string mandante)
    {
        ResultadoIdentidadeQas110 r = ValidadorIdentidadeQas110.Resolver(new ConfiguracaoSap
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_PURCHASEORDER_SRV"),
            MaterialDocumentBaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = mandante,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado],
            Usuario = "u",
            Senha = "p",
            TimeoutSegundos = 30
        });
        Assert.True(r.Confirmada, r.Mensagem);
        return r.Identidade!;
    }

    [Fact] // T1 + T12: palete 1 produz 29.800 / 28.700 / 1.100 no payload e no JSON serializado, em KG
    public void T1_Palete1_ProduzPesosCompostos_ComUnidadeKg()
    {
        ProdutoAcabadoPalete palete = PaletePiloto1();

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder()
            .MontarPreview(palete, ResolverPiloto(MapeamentoAprovado()));

        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.Equal(29.800m, preview.Payload!.GrossWeight);
        Assert.Equal(28.700m, preview.Payload.NetWeight);
        Assert.Equal(1.100m, preview.Payload.TareWeight);
        Assert.Equal("KG", preview.Payload.WeightUnit);
        // T12: os pesos corretos chegam ao corpo serializado que o gateway envia.
        Assert.Contains("\"GrossWeight\": 29.800", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"NetWeight\": 28.700", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"TareWeight\": 1.100", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"WeightUnit\": \"KG\"", preview.PayloadJson, StringComparison.Ordinal);
        // A tara das caixas (0.100) NAO foi perdida: 1.100 = 0.100 + 1.000.
        Assert.Equal(palete.TaraKg + 1.000m, preview.Payload.TareWeight);
    }

    [Fact] // T2 + T11: tara ausente (nenhum mapeamento) bloqueia o outbound ANTES de qualquer payload
    public void T2_TaraAusente_BloqueiaOutbound_SemPayload()
    {
        ResultadoTaraFisicaEmbalagem tara = ResolverPiloto(mapeamentos: []);
        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada, tara.Cenario);
        Assert.Null(tara.PesoKg); // ausencia NAO e zero

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder()
            .MontarPreview(PaletePiloto1(), tara);

        Assert.False(preview.Sucesso);
        Assert.Null(preview.Payload);
        // Nenhum fallback silencioso para os pesos locais.
        Assert.DoesNotContain("28.800", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact] // T3: tara negativa e INVALIDA e bloqueia o outbound
    public void T3_TaraNegativa_BloqueiaOutbound()
    {
        ResultadoTaraFisicaEmbalagem tara = ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = -1.000m, Unidade = "KG" }
        ]);

        Assert.Equal(CenarioTaraFisicaEmbalagem.Invalida, tara.Cenario);
        Assert.Null(tara.PesoKg);

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder()
            .MontarPreview(PaletePiloto1(), tara);

        Assert.False(preview.Sucesso);
        Assert.Null(preview.Payload);
    }

    [Fact] // T4: zero EXPLICITO e valido e distinto de desconhecido — e nao substitui o valor de PALLET01/3007
    public void T4_TaraZeroExplicita_EhDistintaDeDesconhecida_ENaoSubstituiOValorAprovado()
    {
        // Zero explicito: cenario Disponivel, com peso materialmente 0m.
        ResultadoTaraFisicaEmbalagem zero = ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = "EMB_SEM_PESO", PesoKg = 0m, Unidade = "KG" }
        ], material: "EMB_SEM_PESO");

        Assert.Equal(CenarioTaraFisicaEmbalagem.Disponivel, zero.Cenario);
        Assert.True(zero.Disponivel);
        Assert.Equal(0m, zero.PesoKg);

        // Desconhecida: cenario diferente e peso null — jamais confundidos.
        ResultadoTaraFisicaEmbalagem desconhecida = ResolverPiloto(mapeamentos: []);
        Assert.NotEqual(zero.Cenario, desconhecida.Cenario);
        Assert.False(desconhecida.Disponivel);
        Assert.Null(desconhecida.PesoKg);

        // O zero de OUTRO material nao vale para PALLET01/3007: lá continua valendo 1.000.
        ResultadoTaraFisicaEmbalagem aprovada = ResolverPiloto(MapeamentoAprovado());
        Assert.Equal(1.000m, aprovada.PesoKg);
    }

    [Fact] // T5: NetWeight permanece invariavel — o liquido das caixas nunca e alterado
    public void T5_NetWeight_PermaneceInvariavel()
    {
        ProdutoAcabadoPalete palete = PaletePiloto1();

        foreach (decimal taraFisica in new[] { 0m, 1.000m, 7.500m })
        {
            ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder()
                .MontarPreview(palete, TaraFisica(taraFisica));

            Assert.True(preview.Sucesso, preview.Mensagem);
            Assert.Equal(palete.PesoLiquidoKg, preview.Payload!.NetWeight);
            Assert.Equal(28.700m, preview.Payload.NetWeight);
        }
    }

    [Fact] // T6: dois previews consecutivos produzem pesos IDENTICOS (composicao exatamente uma vez)
    public void T6_DoisPreviews_ProduzemPesosIdenticos()
    {
        ProdutoAcabadoPalete palete = PaletePiloto1();
        ProdutoAcabadoPaletePayloadBuilder builder = new();

        ResultadoPreviewProdutoAcabadoPalete primeiro = builder.MontarPreview(palete, ResolverPiloto(MapeamentoAprovado()));
        ResultadoPreviewProdutoAcabadoPalete segundo = builder.MontarPreview(palete, ResolverPiloto(MapeamentoAprovado()));

        Assert.True(primeiro.Sucesso, primeiro.Mensagem);
        Assert.True(segundo.Sucesso, segundo.Mensagem);
        Assert.Equal(primeiro.Payload!.GrossWeight, segundo.Payload!.GrossWeight);
        Assert.Equal(primeiro.Payload.NetWeight, segundo.Payload.NetWeight);
        Assert.Equal(primeiro.Payload.TareWeight, segundo.Payload.TareWeight);
        Assert.Equal(29.800m, segundo.Payload.GrossWeight); // nao acumulou para 30.800
        Assert.Equal(1.100m, segundo.Payload.TareWeight);   // nao acumulou para 2.100
    }

    [Fact] // T7: o objeto de dominio recebido NAO e mutado — o rascunho local permanece intacto
    public void T7_PaleteRecebido_NaoEhMutado()
    {
        ProdutoAcabadoPalete palete = PaletePiloto1();

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder()
            .MontarPreview(palete, ResolverPiloto(MapeamentoAprovado()));

        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.Equal(28.800m, palete.PesoBrutoKg);
        Assert.Equal(28.700m, palete.PesoLiquidoKg);
        Assert.Equal(0.100m, palete.TaraKg);
        // As caixas tambem permanecem intactas.
        Assert.Equal(14.350m, palete.Caixas[0].PesoLiquidoKg);
        Assert.Equal(0.050m, palete.Caixas[0].TaraKg);
    }

    [Fact] // T9: invariantes GROSS = NET + TARE e GROSS > NET > TARE sao verificadas apos a composicao
    public void T9_Invariantes_DePeso_SaoVerificadas()
    {
        // GROSS = NET + TARE no cenario aprovado.
        ResultadoPreviewProdutoAcabadoPalete ok = new ProdutoAcabadoPaletePayloadBuilder()
            .MontarPreview(PaletePiloto1(), ResolverPiloto(MapeamentoAprovado()));
        Assert.True(ok.Sucesso, ok.Mensagem);
        Assert.Equal(ok.Payload!.GrossWeight, ok.Payload.NetWeight + ok.Payload.TareWeight);
        Assert.True(ok.Payload.GrossWeight > ok.Payload.NetWeight);
        Assert.True(ok.Payload.NetWeight > ok.Payload.TareWeight);

        // Tara fisica tao grande que TARE >= NET viola a ordem exigida ⇒ bloqueia.
        ResultadoPreviewProdutoAcabadoPalete violada = new ProdutoAcabadoPaletePayloadBuilder()
            .MontarPreview(PaletePiloto1(), TaraFisica(100.000m));
        Assert.False(violada.Sucesso);
        Assert.Null(violada.Payload);
    }

    [Fact] // T10: mapeamento de OUTRO centro/material/mandante nao autoriza PALLET01/3007
    public void T10_MapeamentoDeOutraChave_NaoAutorizaPallet01_3007()
    {
        // Centro diferente.
        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada, ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = "3008", MaterialEmbalagem = MaterialPalete, PesoKg = 1.000m, Unidade = "KG" }
        ]).Cenario);

        // Material diferente.
        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada, ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = "PALLET02", PesoKg = 1.000m, Unidade = "KG" }
        ]).Cenario);

        // Mandante diferente na IDENTIDADE VALIDADA: o mapeamento de 110 nao e selecionado.
        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada, ResolverPiloto(
            MapeamentoAprovado(), IdentidadeComMandante("100")).Cenario);

        // GATE 126X: sem identidade comprovada ⇒ fail-closed, independentemente do mapeamento.
        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada,
            ResolvedorTaraFisicaEmbalagemPalete.Resolver(
                MapeamentoAprovado(), null, CentroQ, MaterialPalete).Cenario);
    }

    [Fact] // T11: configuracao invalida ou ambigua bloqueia ANTES de qualquer POST
    public void T11_ConfiguracaoInvalidaOuAmbigua_Bloqueia()
    {
        // Unidade diferente de KG ⇒ INVALIDA (nunca convertida).
        Assert.Equal(CenarioTaraFisicaEmbalagem.Invalida, ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = 1000m, Unidade = "G" }
        ]).Cenario);

        // Unidade ausente ⇒ INVALIDA.
        Assert.Equal(CenarioTaraFisicaEmbalagem.Invalida, ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = 1.000m, Unidade = string.Empty }
        ]).Cenario);

        // Precisao acima de 3 casas ⇒ INVALIDA.
        Assert.Equal(CenarioTaraFisicaEmbalagem.Invalida, ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = 1.00049m, Unidade = "KG" }
        ]).Cenario);

        // Duas entradas para a MESMA chave logica ⇒ AMBIGUA, nunca resolvida por escolha.
        Assert.Equal(CenarioTaraFisicaEmbalagem.Ambigua, ResolverPiloto(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = 1.000m, Unidade = "KG" },
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = 1.500m, Unidade = "KG" }
        ]).Cenario);

        // Todos os cenarios acima resultam em bloqueio no builder, sem payload.
        foreach (ResultadoTaraFisicaEmbalagem tara in new[]
        {
            ResultadoTaraFisicaEmbalagem.Invalida("teste"),
            ResultadoTaraFisicaEmbalagem.Ambigua("teste"),
            ResultadoTaraFisicaEmbalagem.NaoConfigurada("teste")
        })
        {
            ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder()
                .MontarPreview(PaletePiloto1(), tara);
            Assert.False(preview.Sucesso);
            Assert.Null(preview.Payload);
        }
    }

    [Fact] // 126V: match tolerante a espacos e caixa, sem afrouxar a chave logica
    public void Resolucao_ToleraEspacosECaixa_SemAfrouxarChave()
    {
        ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
        [
            new() { SapClient = " 110 ", Centro = " 3007 ", MaterialEmbalagem = " pallet01 ", PesoKg = 1.000m, Unidade = " kg " }
        ], IdentidadeQ(), " 3007 ", " PALLET01 ");

        Assert.True(tara.Disponivel);
        Assert.Equal(1.000m, tara.PesoKg);
    }

    // ========================================================================================
    // GATE 126X: a tara fisica so resolve com IDENTIDADE QAS110 comprovada (perfil Q da
    // configuracao carregada + destino SAP Q validado). Mandante 110, isolado, NAO autoriza.
    // ========================================================================================

    [Fact] // 126X-T1: QAS110 valido + 110 + 3007 + PALLET01 + 1.000 KG => 29.800 / 28.700 / 1.100
    public void X1_Qas110Valido_ProduzOutboundAprovado()
    {
        ConfiguracaoSap cfg = ConfigQValida(MapeamentoAprovado());
        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
        Assert.True(ident.Confirmada, ident.Mensagem);
        Assert.Equal(MandanteQ, ident.Identidade!.SapClient);
        Assert.Equal(ValidadorAmbienteQ.SapHostEsperado, ident.Identidade.HostSap);
        Assert.Equal(ValidadorAmbienteQ.SapPortaEsperada, ident.Identidade.PortaSap);

        ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
            cfg.TarasFisicasEmbalagemPalete, ident.Identidade, CentroQ, MaterialPalete);
        Assert.True(tara.Disponivel);
        Assert.Equal(1.000m, tara.PesoKg);

        ResultadoPreviewProdutoAcabadoPalete preview =
            new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaletePiloto1(), tara);
        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.Equal(29.800m, preview.Payload!.GrossWeight);
        Assert.Equal(28.700m, preview.Payload.NetWeight);
        Assert.Equal(1.100m, preview.Payload.TareWeight);
        Assert.Equal("KG", preview.Payload.WeightUnit);
    }

    [Fact] // 126X-T2: DS com mandante 110 e o MESMO bloco de tara => BLOQUEADO (defeito do 126W)
    public void X2_DsComMandante110_EMesmoBlocoDeTara_Bloqueado()
    {
        ConfiguracaoSap ds = ConfigDsMandante110(MapeamentoAprovado());

        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(ds);
        Assert.False(ident.Confirmada);
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ident.Cenario);
        Assert.Null(ident.Identidade);

        // Mesmo mandante (110) e mesmo mapeamento: a tara NAO resolve.
        ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
            ds.TarasFisicasEmbalagemPalete, ident.Identidade, CentroQ, MaterialPalete);
        Assert.False(tara.Disponivel);
        Assert.Null(tara.PesoKg);

        ResultadoPreviewProdutoAcabadoPalete preview =
            new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaletePiloto1(), tara);
        Assert.False(preview.Sucesso);
        Assert.Null(preview.Payload);
    }

    [Fact] // 126X-T3: destino nao validado (URL/allowlist/sap-client fora do contrato) => BLOQUEADO
    public void X3_DestinoNaoValidado_Bloqueado()
    {
        // allowlist vazia
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ValidadorIdentidadeQas110.Resolver(new ConfiguracaoSap
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = MandanteQ,
            HostsPermitidos = []
        }).Cenario);

        // porta divergente
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ValidadorIdentidadeQas110.Resolver(new ConfiguracaoSap
        {
            PerfilLocalQ = true,
            BaseUrl = $"https://{ValidadorAmbienteQ.SapHostEsperado}:8443/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            SapClient = MandanteQ,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado]
        }).Cenario);

        // sap-client invalido
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ValidadorIdentidadeQas110.Resolver(new ConfiguracaoSap
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = "11",
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado]
        }).Cenario);

        // configuracao ausente
        Assert.Equal(CenarioIdentidadeQas110.ConfiguracaoAusente,
            ValidadorIdentidadeQas110.Resolver(null).Cenario);
    }

    [Fact] // 126X-T4: perfil Q declarado + destino SAP DS => BLOQUEADO (perfil nao basta)
    public void X4_PerfilQComDestinoDs_Bloqueado()
    {
        ConfiguracaoSap cfg = ConfigDsMandante110();
        Assert.True(cfg.PerfilLocalQ); // o perfil ESTA declarado Q
        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
        Assert.False(ident.Confirmada);
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ident.Cenario);
    }

    [Fact] // 126X-T5: destino Q porem SEM perfil reconhecido pelo contrato real => BLOQUEADO
    public void X5_DestinoQSemPerfilComprovado_Bloqueado()
    {
        ConfiguracaoSap cfg = new()
        {
            PerfilLocalQ = false, // nao reconhecido na carga
            BaseUrl = UrlQ("API_PURCHASEORDER_SRV"),
            MaterialDocumentBaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = MandanteQ,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado],
            TarasFisicasEmbalagemPalete = MapeamentoAprovado()
        };

        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
        Assert.False(ident.Confirmada);
        Assert.Equal(CenarioIdentidadeQas110.PerfilNaoQ, ident.Cenario);

        ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
            cfg.TarasFisicasEmbalagemPalete, ident.Identidade, CentroQ, MaterialPalete);
        Assert.False(tara.Disponivel);
    }

    [Fact] // 126X-T5b: perfil Q NAO vem de texto livre — o leitor o deriva do arquivo real
    public void X5b_PerfilLocalQ_NaoEhDeclaracaoLivre_EhDerivadoDoArquivo()
    {
        string arquivo = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            // Sem a secao "ambiente": o leitor NAO concede perfil Q.
            File.WriteAllText(arquivo, "{ \"sap\": { \"sap_client\": \"110\" } }");
            Assert.False(LeitorConfiguracaoSap.Carregar(arquivo, _ => null).PerfilLocalQ);

            // Com a secao "ambiente" reconhecida: concede.
            File.WriteAllText(arquivo, "{ \"ambiente\": { \"perfil\": \"Q\" }, \"sap\": { \"sap_client\": \"110\" } }");
            ConfiguracaoSap comPerfil = LeitorConfiguracaoSap.Carregar(
                arquivo,
                _ => null,
                null,
                PerfilAmbienteLocal.CarregarDoArquivo(arquivo));
            Assert.True(comPerfil.PerfilLocalQ);
        }
        finally
        {
            if (File.Exists(arquivo)) { File.Delete(arquivo); }
        }
    }

    [Fact] // 126X-T6: configuracao de tara ausente/invalida => BLOQUEADO mesmo com identidade valida
    public void X6_TaraAusenteOuInvalida_BloqueiaMesmoComIdentidadeValida()
    {
        IdentidadeAmbienteQas110 ident = IdentidadeQ();

        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada,
            ResolvedorTaraFisicaEmbalagemPalete.Resolver([], ident, CentroQ, MaterialPalete).Cenario);
        Assert.Equal(CenarioTaraFisicaEmbalagem.NaoConfigurada,
            ResolvedorTaraFisicaEmbalagemPalete.Resolver(null, ident, CentroQ, MaterialPalete).Cenario);
        Assert.Equal(CenarioTaraFisicaEmbalagem.Invalida, ResolvedorTaraFisicaEmbalagemPalete.Resolver(
        [
            new() { SapClient = MandanteQ, Centro = CentroQ, MaterialEmbalagem = MaterialPalete, PesoKg = -1m, Unidade = "KG" }
        ], ident, CentroQ, MaterialPalete).Cenario);
    }

    [Fact] // 126X-T7/T8: repeticao do preview nao acumula e os pesos locais nao sofrem mutacao
    public void X7_PreviewRepetido_SemDuplaContagem_ESemMutacaoLocal()
    {
        ProdutoAcabadoPalete palete = PaletePiloto1();
        ProdutoAcabadoPaletePayloadBuilder builder = new();
        ConfiguracaoSap cfg = ConfigQValida(MapeamentoAprovado());

        for (int i = 0; i < 3; i++)
        {
            ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
            ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
                cfg.TarasFisicasEmbalagemPalete, ident.Identidade, CentroQ, MaterialPalete);
            ResultadoPreviewProdutoAcabadoPalete preview = builder.MontarPreview(palete, tara);

            Assert.True(preview.Sucesso, preview.Mensagem);
            Assert.Equal(29.800m, preview.Payload!.GrossWeight); // nunca 30.800
            Assert.Equal(1.100m, preview.Payload.TareWeight);    // nunca 2.100
            Assert.Equal(28.700m, preview.Payload.NetWeight);
        }

        Assert.Equal(28.800m, palete.PesoBrutoKg);
        Assert.Equal(28.700m, palete.PesoLiquidoKg);
        Assert.Equal(0.100m, palete.TaraKg);
    }

    [Fact] // 126X-T10: nao ha caminho que produza tara aplicavel sem identidade comprovada
    public void X10_SemIdentidade_NaoExisteCaminhoParaTaraAplicavel()
    {
        // A fabrica do resultado POSITIVO exige a identidade; nula ⇒ Invalida, nunca Disponivel.
        ResultadoTaraFisicaEmbalagem forjada = ResultadoTaraFisicaEmbalagem.Resolvida(null, 1.000m);
        Assert.False(forjada.Disponivel);
        Assert.Equal(CenarioTaraFisicaEmbalagem.Invalida, forjada.Cenario);
        Assert.Null(forjada.PesoKg);

        // E o builder recusa qualquer resultado nao-disponivel, sem payload.
        ResultadoPreviewProdutoAcabadoPalete preview =
            new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaletePiloto1(), forjada);
        Assert.False(preview.Sucesso);
        Assert.Null(preview.Payload);

        // O tipo de identidade nao possui construtor nem fabrica PUBLICA.
        Assert.Empty(typeof(IdentidadeAmbienteQas110).GetConstructors());
        Assert.Empty(typeof(IdentidadeAmbienteQas110)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(IdentidadeAmbienteQas110)));
    }

    [Fact] // 126X: rota CPI presente porem fora da propria allowlist => BLOQUEADO (consistencia 046-K)
    public void X_RotaCpiForaDaAllowlist_Bloqueado()
    {
        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(new ConfiguracaoSap
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = MandanteQ,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado],
            PalletInt012BaseUrl = "https://cpi.intruso.test/http/QS4_110/x",
            PalletInt012HostsPermitidos = ["cpi.autorizado.test"]
        });

        Assert.False(ident.Confirmada);
        Assert.Equal(CenarioIdentidadeQas110.RotaCpiInconsistente, ident.Cenario);
    }

    // ========================================================================================
    // GATE 126AA: o contrato de DESTINO exige (a) ao menos UMA URL SAP efetiva e (b) que TODAS as
    // URLs efetivas — inclusive PurchaseOrder — sejam Q. Uma unica URL DS bloqueia o conjunto.
    // ========================================================================================

    /// <summary>Configuracao minima para isolar o contrato de destino (perfil Q + allowlist + mandante).</summary>
    private static ConfiguracaoSap ConfigDestino(
        string? baseUrl = null,
        string? purchaseOrderBaseUrl = null,
        string? materialDocumentBaseUrl = null,
        IReadOnlyList<string>? allowlist = null,
        string sapClient = MandanteQ) => new()
        {
            PerfilLocalQ = true,
            BaseUrl = baseUrl ?? string.Empty,
            PurchaseOrderBaseUrl = purchaseOrderBaseUrl ?? string.Empty,
            MaterialDocumentBaseUrl = materialDocumentBaseUrl ?? string.Empty,
            SapClient = sapClient,
            HostsPermitidos = allowlist ?? [ValidadorAmbienteQ.SapHostEsperado]
        };

    [Fact] // 126AA-T1/T6: nenhuma URL SAP efetiva => BLOQUEADO, mesmo com allowlist Q e mandante 110
    public void AA1_SemNenhumaUrlSapEfetiva_Bloqueado()
    {
        ConfiguracaoSap cfg = ConfigDestino();

        ResultadoValidacaoAmbienteQ destino = ValidadorAmbienteQ.ValidarDestinoSapQ(cfg);
        Assert.False(destino.Valido);
        Assert.Contains("nenhuma URL SAP efetiva", destino.Mensagem, StringComparison.OrdinalIgnoreCase);

        // E a identidade QAS110 tambem nao e emitida (o guard de startup nao e paralelo ao da tara).
        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
        Assert.False(ident.Confirmada);
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ident.Cenario);

        // O guard de STARTUP recusa o mesmo cenario (nao foi afrouxado).
        Assert.False(ValidadorAmbienteQ.ValidarSap(cfg).Valido);
    }

    [Fact] // 126AA-T2: URL principal Q e PurchaseOrder DS => BLOQUEADO (defeito do 126Y)
    public void AA2_BaseQComPurchaseOrderDs_Bloqueado()
    {
        ConfiguracaoSap cfg = ConfigDestino(
            baseUrl: UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            purchaseOrderBaseUrl: UrlDs("API_PURCHASEORDER_SRV"));

        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(cfg).Valido);
        Assert.False(ValidadorIdentidadeQas110.Resolver(cfg).Confirmada);
        Assert.False(ValidadorAmbienteQ.ValidarSap(cfg).Valido);
    }

    [Fact] // 126AA-T3: URL principal DS e PurchaseOrder Q => BLOQUEADO
    public void AA3_BaseDsComPurchaseOrderQ_Bloqueado()
    {
        ConfiguracaoSap cfg = ConfigDestino(
            baseUrl: UrlDs("API_MATERIAL_DOCUMENT_SRV"),
            purchaseOrderBaseUrl: UrlQ("API_PURCHASEORDER_SRV"));

        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(cfg).Valido);
        Assert.False(ValidadorIdentidadeQas110.Resolver(cfg).Confirmada);
    }

    [Fact] // 126AA-T2b: PurchaseOrder AUSENTE deriva de BaseUrl — Q continua valido, DS continua bloqueado
    public void AA2b_PurchaseOrderAusente_DerivaDaBaseUrl()
    {
        // Ausente + base Q ⇒ efetiva Q ⇒ aprova (ausencia nao e invalidez).
        Assert.True(ValidadorAmbienteQ.ValidarDestinoSapQ(
            ConfigDestino(baseUrl: UrlQ("API_MATERIAL_DOCUMENT_SRV"))).Valido);

        // Ausente + base DS ⇒ efetiva DS ⇒ bloqueia.
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(
            ConfigDestino(baseUrl: UrlDs("API_MATERIAL_DOCUMENT_SRV"))).Valido);
    }

    [Fact] // 126AA-T4: todas as URLs SAP efetivas Q validas => PASS
    public void AA4_TodasAsUrlsEfetivasQ_Aprova()
    {
        ConfiguracaoSap cfg = ConfigDestino(
            baseUrl: UrlQ("API_PURCHASEORDER_SRV"),
            purchaseOrderBaseUrl: UrlQ("API_PURCHASEORDER_SRV"),
            materialDocumentBaseUrl: UrlQ("API_MATERIAL_DOCUMENT_SRV"));

        Assert.True(ValidadorAmbienteQ.ValidarDestinoSapQ(cfg).Valido);
        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
        Assert.True(ident.Confirmada, ident.Mensagem);
    }

    [Fact] // 126AA-T5: scheme, host ou porta divergentes => BLOQUEADO
    public void AA5_SchemeHostOuPortaDivergente_Bloqueado()
    {
        // http em vez de https
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(
            baseUrl: $"http://{ValidadorAmbienteQ.SapHostEsperado}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/")).Valido);

        // host divergente (nem Q nem DS)
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(
            baseUrl: $"https://outro.sap.fugacouros.com.br:{ValidadorAmbienteQ.SapPortaEsperada}/sap/")).Valido);

        // porta divergente
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(
            baseUrl: $"https://{ValidadorAmbienteQ.SapHostEsperado}:8443/sap/")).Valido);

        // host que CONTEM o host Q como sufixo/prefixo nao e aceito (sem comparacao parcial)
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(
            baseUrl: $"https://x{ValidadorAmbienteQ.SapHostEsperado}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/")).Valido);
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(
            baseUrl: $"https://{ValidadorAmbienteQ.SapHostEsperado}.intruso.test:{ValidadorAmbienteQ.SapPortaEsperada}/sap/")).Valido);

        // URL relativa/malformada
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(baseUrl: "/sap/opu/odata")).Valido);
    }

    [Fact] // 126AA-T7: DS com mandante 110 e perfil Q => BLOQUEADO (reconfirma o 126X apos o 126AA)
    public void AA7_DsComMandante110EPerfilQ_Bloqueado()
    {
        ConfiguracaoSap ds = ConfigDsMandante110(MapeamentoAprovado());
        Assert.True(ds.PerfilLocalQ);
        Assert.Equal(MandanteQ, ds.SapClient);

        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ds).Valido);
        Assert.Equal(CenarioIdentidadeQas110.DestinoNaoQ, ValidadorIdentidadeQas110.Resolver(ds).Cenario);
    }

    [Fact] // 126AA-T8: identidade nao validada => tara indisponivel e payload bloqueado
    public void AA8_IdentidadeNaoValidada_TaraIndisponivel_SemPayload()
    {
        foreach (ConfiguracaoSap cfg in new[]
        {
            ConfigDestino(), // sem URL
            ConfigDestino(baseUrl: UrlQ("API_MATERIAL_DOCUMENT_SRV"), purchaseOrderBaseUrl: UrlDs("API_PURCHASEORDER_SRV")),
            ConfigDsMandante110()
        })
        {
            ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
            Assert.False(ident.Confirmada);

            ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
                MapeamentoAprovado(), ident.Identidade, CentroQ, MaterialPalete);
            Assert.False(tara.Disponivel);
            Assert.Null(tara.PesoKg);

            ResultadoPreviewProdutoAcabadoPalete preview =
                new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaletePiloto1(), tara);
            Assert.False(preview.Sucesso);
            Assert.Null(preview.Payload);
        }
    }

    [Fact] // 126AA-T9: identidade Q valida (todas as URLs Q) + PALLET01 => 29.800 / 28.700 / 1.100
    public void AA9_IdentidadeQValidaComTodasAsUrls_ProduzOutboundAprovado()
    {
        ConfiguracaoSap cfg = new()
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_PURCHASEORDER_SRV"),
            PurchaseOrderBaseUrl = UrlQ("API_PURCHASEORDER_SRV"),
            MaterialDocumentBaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            ProductionOrderBaseUrl = UrlQ("API_PRODUCTION_ORDER_2_SRV"),
            ProductBaseUrl = UrlQ("API_PRODUCT_SRV"),
            HandlingUnitBaseUrl = UrlQ("API_HANDLINGUNIT_SRV"),
            SapClient = MandanteQ,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado],
            Usuario = "u",
            Senha = "p",
            TarasFisicasEmbalagemPalete = MapeamentoAprovado()
        };

        ResultadoIdentidadeQas110 ident = ValidadorIdentidadeQas110.Resolver(cfg);
        Assert.True(ident.Confirmada, ident.Mensagem);

        ResultadoTaraFisicaEmbalagem tara = ResolvedorTaraFisicaEmbalagemPalete.Resolver(
            cfg.TarasFisicasEmbalagemPalete, ident.Identidade, CentroQ, MaterialPalete);
        ResultadoPreviewProdutoAcabadoPalete preview =
            new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(PaletePiloto1(), tara);

        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.Equal(29.800m, preview.Payload!.GrossWeight);
        Assert.Equal(28.700m, preview.Payload.NetWeight);
        Assert.Equal(1.100m, preview.Payload.TareWeight);
        Assert.Equal("KG", preview.Payload.WeightUnit);
    }

    [Fact] // 126AA: a enumeracao de destino cobre PurchaseOrder e NAO inclui as rotas CPI
    public void AA_EnumeracaoDeDestino_CobrePurchaseOrder_ENaoIncluiCpi()
    {
        // PurchaseOrder participa: sozinha, uma URL DS nesse campo derruba o conjunto.
        Assert.False(ValidadorAmbienteQ.ValidarDestinoSapQ(ConfigDestino(
            baseUrl: UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            purchaseOrderBaseUrl: UrlDs("API_PURCHASEORDER_SRV"))).Valido);

        // Rotas do tenant CPI NAO sao validadas contra o host SAP on-premise (contrato proprio 046-K).
        ConfiguracaoSap comCpi = new()
        {
            PerfilLocalQ = true,
            BaseUrl = UrlQ("API_MATERIAL_DOCUMENT_SRV"),
            SapClient = MandanteQ,
            HostsPermitidos = [ValidadorAmbienteQ.SapHostEsperado],
            PackagingBaseUrl = "https://tenant.cfapps.br10.hana.ondemand.com/http/packaging",
            PalletInt012BaseUrl = "https://tenant.cfapps.br10.hana.ondemand.com/http/QS4_110/x",
            PalletInt012HostsPermitidos = ["tenant.cfapps.br10.hana.ondemand.com"]
        };
        Assert.True(ValidadorAmbienteQ.ValidarDestinoSapQ(comCpi).Valido);
        Assert.True(ValidadorIdentidadeQas110.Resolver(comCpi).Confirmada);
    }

    private static ConfiguracaoSap ConfigCpi(bool comCredencialCpi, bool writeHabilitado) => new()
    {
        // Credenciais SAP genéricas PREENCHIDAS de propósito, para provar que NÃO há fallback para elas.
        Usuario = "sap-generico-fake",
        Senha = "sap-generico-fake",
        PalletWriteHabilitado = writeHabilitado,
        PalletInt012BaseUrl = "https://cpi.exemplo.test",
        PalletInt012HostsPermitidos = ["cpi.exemplo.test"],
        PalletInt012Usuario = comCredencialCpi ? "cpi-usuario-fake" : string.Empty,
        PalletInt012Senha = comCredencialCpi ? "cpi-senha-fake" : string.Empty,
        TimeoutSegundos = 30
    };
}
