using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using System.Globalization;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Envio CONTROLADO da Entrada ao SAP via Material Document (movimento 101), separado da finalizacao
/// local. Garante que o Finalizar nao chama SAP, que o envio NAO usa PATCH no Pedido de Compra, que
/// cria um unico documento de material por lancamento com os itens elegiveis, e que as travas
/// (HOMOLOGACAO + permissao + escrita + Material Document configurado + dados obrigatorios) sao
/// reaplicadas antes do POST.
/// </summary>
public sealed class EnvioControladoSapEntradaC12Tests : IDisposable
{
    public EnvioControladoSapEntradaC12Tests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    [Fact]
    public async Task FinalizarLeitura_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico pedido = new();
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller =
            CriarController(pedido, materialDoc, ehHomologacao: true, itens: [Item()]);

        await controller.FinalizarLeituraAsync(Lancamento());

        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Equal(0, pedido.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_ComTodasAsTravas_DeveCriarUmUnicoDocumentoMaterialSemPatch()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico pedido = new() { EscritaHabilitada = true };
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller =
            CriarController(pedido, materialDoc, ehHomologacao: true, itens: [Item("10"), Item("20")]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas); // um unico documento por lancamento
        Assert.Equal(0, pedido.PatchChamadas); // Entrada nao usa PATCH no Pedido de Compra
        Assert.Equal(2, resultado.Total);
        Assert.True(resultado.StatusLocalAtualizado);
        Assert.NotNull(materialDoc.UltimaRequisicao);
        Assert.Equal(2, materialDoc.UltimaRequisicao!.Itens.Count);
    }

    [Fact]
    public async Task Enviar_DeveMontarPayloadComMovimento101EItemNormalizado()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10")]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        MaterialDocumentSapRequest req = materialDoc.UltimaRequisicao!;
        Assert.Equal("01", req.GoodsMovementCode);
        MaterialDocumentSapItemRequest item = Assert.Single(req.Itens);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("B", item.GoodsMovementRefDocType);
        Assert.Equal("4500000010", item.PurchaseOrder);
        Assert.Equal("00010", item.PurchaseOrderItem); // 5 digitos
        Assert.Equal("KG", item.EntryUnit);
        Assert.Equal("3500027", item.Material);
        Assert.Equal("3007", item.Plant);
        Assert.Equal("PP01", item.StorageLocation);
        // QuantityInEntryUnit = peso liquido em kg
        Assert.Equal(
            8m,
            decimal.Parse(item.QuantityInEntryUnit, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Enviar_DeveMontarTextoCabecalhoCurtoMax25EAscii()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10")]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        string texto = materialDoc.UltimaRequisicao!.MaterialDocumentHeaderText;
        Assert.True(texto.Length <= 25, $"texto cabecalho excede 25: '{texto}' ({texto.Length})");
        Assert.Contains("4500000010", texto);                 // numero do pedido
        Assert.Contains("99", texto);                          // codigo do lancamento
        Assert.Matches("^[A-Za-z0-9 ]+$", texto);              // ASCII, sem acentos/caracteres perigosos
        Assert.DoesNotContain("Entrada", texto);               // nao envia mais o texto longo antigo
    }

    [Fact]
    public async Task Enviar_TextoCabecalho_DeveRemoverAcentosECaracteresEspeciais()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap itemEstranho = Item("10") with { NumeroPedido = "45000-ção/01" };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [itemEstranho]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(77);

        string texto = materialDoc.UltimaRequisicao!.MaterialDocumentHeaderText;
        Assert.Matches("^[A-Za-z0-9 ]+$", texto);   // sem 'ç'/'ã' nem '-' '/'
        Assert.True(texto.Length <= 25);
        Assert.Contains("77", texto);
    }

    [Theory]
    [InlineData("KG")]
    [InlineData("KGM")]
    [InlineData("UN")]
    [InlineData("PC")]
    [InlineData("ST")]
    [InlineData("PAL")]
    [InlineData("CX")]
    public async Task Enviar_UnidadeComercialDoPedido_DeveEnviarPesoLiquidoSempreEmKg(string unidadePedido)
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10", unidade: unidadePedido)]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas);
        MaterialDocumentSapItemRequest item = Assert.Single(materialDoc.UltimaRequisicao!.Itens);
        Assert.Equal("KG", item.EntryUnit);
        Assert.Equal(
            8m,
            decimal.Parse(item.QuantityInEntryUnit, CultureInfo.InvariantCulture));
        Assert.Equal("B", item.GoodsMovementRefDocType);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("4500000010", item.PurchaseOrder);
        Assert.Equal("00010", item.PurchaseOrderItem);
    }

    [Fact]
    public async Task Enviar_ItemComPesoLiquidoZero_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap semPeso = Item("10", unidade: "UN") with { PesoLiquidoKg = 0m };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [semPeso]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Contains("peso", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enviar_FalhaSap_DeveExibirMensagemSanitizadaDoMaterialDocument()
    {
        DefinirSessao(comEnviarSap: true);
        string mensagemSap =
            "Etapa POST_DOCUMENTO_MATERIAL: HTTP 400 Bad Request. "
            + "O SAP rejeitou a entrada em KG para este item do pedido.";
        FakeMaterialDocumentSapServico materialDoc = new()
        {
            Sucesso = false,
            MensagemFalha = mensagemSap
        };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10", unidade: "UN")]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Falha, resultado.Cenario);
        Assert.Equal(mensagemSap, resultado.Mensagem);
        Assert.Equal(mensagemSap, resultado.MensagemCritica);
        Assert.Contains(mensagemSap, resultado.Itens.Single().Mensagem);
        Assert.Equal(1, materialDoc.Chamadas);
    }


    [Fact]
    public async Task Enviar_ItemSemDadosObrigatorios_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap semCentro = Item("10") with { Centro = null };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [semCentro]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_MaterialDocumentNaoConfigurado_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            materialDocumentConfigurado: false);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.MaterialDocumentNaoConfigurado, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_LancamentoJaConfirmadoSap_DeveBloquearSemChamarCriarDocumento()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            statusLancamento: "CONFIRMADO_SAP");

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoJaConfirmadoSap, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas); // nao chamou CriarDocumentoMaterialEntradaAsync
        Assert.Contains("confirmado", resultado.Mensagem!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enviar_LancamentoCancelado_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            statusLancamento: "CANCELADO");

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoCancelado, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_ReservaNaoObtida_DeveAbortarSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            reservaObtida: false);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.EnvioEmProcessamento, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas); // nao chamou o POST sem a reserva
        Assert.Contains("processamento", resultado.Mensagem!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enviar_ReservaObtida_DeveCriarDocumento()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            reservaObtida: true);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas);
    }

    [Fact]
    public void TentarReservar_DeveSerUpdateAtomicoCondicionalAoStatus()
    {
        // Claim atomico: UPDATE ... -> ENVIADO_SAP WHERE status IN ('FINALIZADO_LOCAL','ERRO_SAP').
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "AcessoDados",
            "Repositorio",
            "EntradaProdutoRepositorio.cs"));

        Assert.Contains("status_lancamento = 'ENVIADO_SAP'", repositorio, StringComparison.Ordinal);
        Assert.Contains(
            "AND status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')",
            repositorio,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ListarItensParaEnvioSap_DevePossuirFiltroDeStatusContraReenvio()
    {
        // Itens CONFIRMADO_SAP/CANCELADO nao podem entrar no payload (defesa SQL de duplicacao).
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "AcessoDados",
            "Repositorio",
            "EntradaProdutoRepositorio.cs"));

        Assert.Contains(
            "lancamento.status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')",
            repositorio,
            StringComparison.Ordinal);
        Assert.Contains(
            "item.status_item IN ('FINALIZADO_LOCAL', 'ERRO_SAP')",
            repositorio,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AtualizarStatusAposEnvioSap_DevePersistirRastreabilidadeDoDocumentoMaterial()
    {
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(), "AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"));

        Assert.Contains("documento_material_sap = @documento_material_sap", repositorio, StringComparison.Ordinal);
        Assert.Contains("exercicio_documento_material_sap = @exercicio_documento_material_sap", repositorio, StringComparison.Ordinal);
        Assert.Contains("enviado_sap_em = now()", repositorio, StringComparison.Ordinal);
        Assert.Contains("documento_material_item = @documento_material_item", repositorio, StringComparison.Ordinal);
    }

    [Fact]
    public void LogDePayload_DeveUsarSituacaoParcialNaoPayload()
    {
        string servico = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Servicos", "IntegracaoSap", "MaterialDocumentSapServico.cs"));

        Assert.Contains("\"PARCIAL\"", servico, StringComparison.Ordinal);
        Assert.Contains("Etapa PAYLOAD: ", servico, StringComparison.Ordinal);
        // A situacao "PAYLOAD" nao pode ser usada (constraint do log_integracao_sap pode recusar).
        Assert.DoesNotContain("\"PAYLOAD\"", servico, StringComparison.Ordinal);
    }

    // Regex que cobre as formas de habilitar a escrita SAP em .cmd/.bat/.ps1 (com/sem aspas, $env:,
    // SetEnvironmentVariable). Espelha Testar-ScriptHabilitaEscritaSap do GerarPacoteLimpo.ps1.
    private const string PadraoHabilitaEscritaSap =
        @"(FUGAPET_SAP_WRITE_ENABLED\s*=\s*[""']?\s*true)"
        + @"|(SetEnvironmentVariable\s*\(\s*[""']FUGAPET_SAP_WRITE_ENABLED[""']\s*,\s*[""']?\s*true)";

    [Fact]
    public void Projeto_NaoDeveConterScriptQueHabilitaEscritaSap()
    {
        string raiz = RaizProjeto();
        string[] dirsIgnoradas = ["bin", "obj", "pacotes_limpos", ".git", ".vs", "_backup"];
        string[] extensoes = [".cmd", ".bat", ".ps1"];

        List<string> scriptsPerigosos = Directory
            .EnumerateFiles(raiz, "*.*", SearchOption.AllDirectories)
            .Where(arquivo => extensoes.Any(ext =>
                arquivo.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Where(arquivo => !string.Equals(
                Path.GetFileName(arquivo), "GerarPacoteLimpo.ps1", StringComparison.OrdinalIgnoreCase))
            .Where(arquivo => dirsIgnoradas.All(dir =>
                !arquivo.Contains($"{Path.DirectorySeparatorChar}{dir}{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)))
            .Where(arquivo => System.Text.RegularExpressions.Regex.IsMatch(
                File.ReadAllText(arquivo),
                PadraoHabilitaEscritaSap,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .ToList();

        Assert.True(
            scriptsPerigosos.Count == 0,
            "Script(s) (.cmd/.bat/.ps1) habilitando escrita SAP: "
                + string.Join("; ", scriptsPerigosos.Select(Path.GetFileName)));
    }

    [Fact]
    public void GerarPacoteLimpo_DeveBloquearScriptsQueHabilitamEscritaSap()
    {
        string script = File.ReadAllText(Path.Combine(RaizProjeto(), "Scripts", "GerarPacoteLimpo.ps1"));

        Assert.Contains("function Testar-ScriptHabilitaEscritaSap", script, StringComparison.Ordinal);
        // Cobre as formas alem do '=' direto: $env: e SetEnvironmentVariable.
        Assert.Contains("$env:FUGAPET_SAP_WRITE_ENABLED", script, StringComparison.Ordinal);
        Assert.Contains("SetEnvironmentVariable", script, StringComparison.Ordinal);
        // Aplica a .cmd, .bat e .ps1.
        Assert.Contains("'.cmd', '.bat', '.ps1'", script, StringComparison.Ordinal);

        // A regex de deteccao realmente casa as variacoes pedidas.
        string[] exemplosBloqueados =
        [
            "set FUGAPET_SAP_WRITE_ENABLED=true",
            "$env:FUGAPET_SAP_WRITE_ENABLED = \"true\"",
            "$env:FUGAPET_SAP_WRITE_ENABLED='true'",
            "[Environment]::SetEnvironmentVariable(\"FUGAPET_SAP_WRITE_ENABLED\", \"true\", \"User\")"
        ];
        foreach (string exemplo in exemplosBloqueados)
        {
            Assert.True(
                System.Text.RegularExpressions.Regex.IsMatch(
                    exemplo, PadraoHabilitaEscritaSap,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                $"Deveria bloquear: {exemplo}");
        }

        // Conteudo inofensivo nao e bloqueado.
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(
            "Write-Host 'build ok'", PadraoHabilitaEscritaSap,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Controller_DeveDiagnosticarUnidadeOriginalEPesoSapKgSemBloqueioAntigo()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        Assert.Contains("Unidade original do pedido", controller, StringComparison.Ordinal);
        Assert.Contains("Peso l", controller, StringComparison.Ordinal);
        Assert.Contains("Quantidade SAP", controller, StringComparison.Ordinal);
        Assert.Contains("EntryUnit SAP: KG", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Unidade do item n", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EntryUnit = item.Unidade", controller, StringComparison.Ordinal);
    }


    [Fact]
    public void Controller_DeveSetarGoodsMovementRefDocTypeBExplicitoESemPatch()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        // Padronizacao DEV/HML: nao depender apenas do default do modelo.
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
        // Entrada nao usa PATCH no Pedido de Compra.
        Assert.DoesNotContain("AtualizarPesoItemSapAsync", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_NaoDeveConterMensagemAntigaDePendenciaGaia()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        // A rastreabilidade agora e persistida; a mensagem de pendencia esta desatualizada.
        Assert.DoesNotContain("PENDENCIA GAIA", controller, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Envio SAP confirmado: documento material", controller, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enviar_Sap2xxSemDocumento_DeveSerCriticoSemConfirmarSemReenvio()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new() { RespostaSemDocumento = true };
        bool atualizouStatus = false;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, _, _, _) =>
            {
                atualizouStatus = true;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        // Nao confirma; trata como divergencia critica; nao atualiza status (nao marca ERRO_SAP nem
        // CONFIRMADO_SAP) -> lancamento fica ENVIADO_SAP, bloqueando reenvio automatico.
        Assert.Equal(CenarioEnvioSapEntrada.FalhaPersistenciaLocal, resultado.Cenario);
        Assert.False(resultado.StatusLocalAtualizado);
        Assert.False(atualizouStatus);
        Assert.NotNull(resultado.MensagemCritica);
    }

    [Fact]
    public async Task Enviar_SucessoSap_DeveConfirmarStatusLocalEGravarRastreabilidade()
    {
        DefinirSessao(comEnviarSap: true);
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        RastreabilidadeDocumentoMaterialSap? rastreabilidadePersistida = null;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico { Sucesso = true },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, cenario, rastreio, _) =>
            {
                cenarioPersistido = cenario;
                rastreabilidadePersistida = rastreio;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Enviado, cenarioPersistido);
        Assert.True(resultado.StatusLocalAtualizado);
        Assert.Equal("5000000124", resultado.MaterialDocument);
        // Persiste documento/exercicio na mesma transacao do CONFIRMADO_SAP.
        Assert.NotNull(rastreabilidadePersistida);
        Assert.Equal("5000000124", rastreabilidadePersistida!.Documento);
        Assert.Equal("2026", rastreabilidadePersistida.Exercicio);
    }

    [Fact]
    public async Task Enviar_FalhaSap_DeveRegistrarErroLocal()
    {
        DefinirSessao(comEnviarSap: true);
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico { Sucesso = false },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, cenario, _, _) =>
            {
                cenarioPersistido = cenario;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Falha, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Falha, cenarioPersistido);
    }

    [Fact]
    public async Task Enviar_FalhaPersistenciaAposSucessoSap_DeveSinalizarCritico()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico { Sucesso = true },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, _, _, _) => Task.FromResult(
                ResultadoOperacao.Falha("Falha local.")));

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.FalhaPersistenciaLocal, resultado.Cenario);
        Assert.False(resultado.StatusLocalAtualizado);
        Assert.NotNull(resultado.MensagemCritica);
    }

    [Fact]
    public async Task Enviar_SemPermissaoEnviarSap_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: false);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.SemPermissao, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_AmbienteDiferenteDeHomologacao_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: false,
            itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.AmbienteNaoHomologacao, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_EscritaDesabilitada_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = false },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.EscritaDesabilitada, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_LancamentoSemItens_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: []);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoSemItens, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Diagnosticar_ComTodasAsTravas_DeveLiberarEnvio()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico(),
            ehHomologacao: true,
            itens: [Item()]);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.True(diagnostico.PodeEnviar);
        Assert.Null(diagnostico.MotivoBloqueio);
        Assert.True(diagnostico.MaterialDocumentConfigurado);
    }

    [Fact]
    public async Task Diagnosticar_MaterialDocumentNaoConfigurado_DeveBloquear()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico(),
            ehHomologacao: true,
            itens: [Item()],
            materialDocumentConfigurado: false);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.False(diagnostico.PodeEnviar);
        Assert.False(diagnostico.MaterialDocumentConfigurado);
        Assert.Contains("Material Document", diagnostico.MotivoBloqueio);
    }

    private static EntradaProdutoController CriarController(
        FakePedidoCompraSapServico pedido,
        FakeMaterialDocumentSapServico materialDoc,
        bool ehHomologacao,
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens,
        bool integracaoAtiva = true,
        bool materialDocumentConfigurado = true,
        string statusLancamento = "FINALIZADO_LOCAL",
        bool reservaObtida = true,
        Func<
            long,
            IReadOnlyList<ResultadoItemEnvioSap>,
            CenarioEnvioSapEntrada,
            RastreabilidadeDocumentoMaterialSap?,
            CancellationToken,
            Task<ResultadoOperacao>>? atualizarStatus = null)
        => new(
            new IntegracaoEntradaSapServico(pedido, materialDoc),
            new EntradaProdutoServico(null!, null!, null!, null, new AutorizacaoCentroDepositoEntrada([], []), null),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            new AutorizacaoCentroDepositoEntrada([], []),
            FabricaControladoresCadastro.CriarTaraController(),
            ehAmbienteHomologacao: () => ehHomologacao,
            carregarItensParaEnvio: (_, _) => Task.FromResult(itens),
            obterStatusLancamento: (_, _) => Task.FromResult<string?>(statusLancamento),
            reservarLancamentoParaEnvio: (_, _) => Task.FromResult(reservaObtida),
            diagnosticarIntegracaoSap: _ => Task.FromResult(
                new DiagnosticoProntidaoIntegracaoSap(
                    AmbienteOperacional: true,
                    IntegracaoAtiva: integracaoAtiva,
                    SapConfigurado: pedido.SapConfigurado,
                    EscritaSapHabilitada: pedido.EscritaSapHabilitada,
                    MaterialDocumentConfigurado: materialDocumentConfigurado,
                    MotivoBloqueio: integracaoAtiva
                        ? null
                        : "Integração SAP inativa.")),
            atualizarStatusAposEnvioSap: atualizarStatus
                ?? ((_, _, _, _, _) => Task.FromResult(ResultadoOperacao.Ok())));

    private static void DefinirSessao(bool comEnviarSap)
    {
        IReadOnlyList<PermissaoSessaoAplicacao> permissoes = comEnviarSap
            ? [new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.ProcessoProducao,
                Rotina = PermissoesSistema.Rotinas.EntradaProduto,
                Acao = PermissoesSistema.Acoes.EnviarSap
            }]
            : [];

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 7,
            Login = "operador_teste",
            Nome = "Operador Teste",
            PerfisCodigo = ["OPERADOR"],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        });
    }

    private static EntradaProdutoItemEnvioSap Item(string numeroItem = "10", string unidade = "KG")
        => new()
        {
            NumeroPedido = "4500000010",
            NumeroItem = numeroItem,
            PesoLiquidoKg = 8m,
            PesoBrutoKg = 10m,
            Material = "3500027",
            Centro = "3007",
            Deposito = "PP01",
            Unidade = unidade
        };

    private static EntradaProdutoLancamento Lancamento()
        => new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem
            {
                NumeroItem = "10",
                Pesagens = [new EntradaProdutoPesagem
                {
                    PesoBrutoKg = 10m, PesoTaraKg = 2m, PesoLiquidoKg = 8m, Origem = "BALANCA", StatusPesagem = "VALIDA"
                }]
            }]
        };

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }

    private sealed class FakeMaterialDocumentSapServico : IMaterialDocumentSapServico
    {
        public bool Sucesso { get; init; } = true;
        public string MensagemFalha { get; init; } = "O SAP recusou a criacao do documento de material.";

        /// <summary>Simula HTTP 2xx porem sem MaterialDocument/MaterialDocumentYear (etapa PARSE_RESPOSTA).</summary>
        public bool RespostaSemDocumento { get; init; }

        public bool MaterialDocumentConfigurado => true;
        public bool EhSimulado => false;
        public int Chamadas { get; private set; }
        public MaterialDocumentSapRequest? UltimaRequisicao { get; private set; }
        public string? UltimaChave { get; private set; }

        public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
            MaterialDocumentSapRequest requisicao,
            string chaveNegocio,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            UltimaRequisicao = requisicao;
            UltimaChave = chaveNegocio;

            if (RespostaSemDocumento)
            {
                return Task.FromResult(new ResultadoMaterialDocumentSap
                {
                    Sucesso = false,
                    StatusHttp = 201,
                    Etapa = "PARSE_RESPOSTA",
                    MensagemSanitizada =
                        "Etapa PARSE_RESPOSTA: SAP retornou sucesso HTTP 201, mas sem MaterialDocument/MaterialDocumentYear na resposta."
                });
            }

            return Task.FromResult(Sucesso
                ? new ResultadoMaterialDocumentSap
                {
                    Sucesso = true,
                    StatusHttp = 201,
                    MaterialDocument = "5000000124",
                    MaterialDocumentYear = "2026",
                    MensagemSanitizada = "Documento de material 5000000124/2026 criado no SAP."
                }
                : ResultadoMaterialDocumentSap.Falha(400, MensagemFalha));
        }
    }

    private sealed class FakePedidoCompraSapServico : IPedidoCompraSapServico
    {
        public bool Configurado { get; init; } = true;
        public bool EscritaHabilitada { get; init; }
        public int PatchChamadas { get; private set; }

        public bool EhSimulado => false;
        public bool SapConfigurado => Configurado;
        public bool EscritaSapHabilitada => EscritaHabilitada;

        public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
            string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto, CancellationToken cancellationToken = default)
        {
            PatchChamadas++;
            return Task.FromResult(ResultadoOperacao.Ok("Peso atualizado no SAP."));
        }

        public Task<ResultadoOperacao> SincronizarPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoOperacao.Ok());
        public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSapAgregado?>(null);
        public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSap?>(new PedidoCompraSap { Numero = numeroPedido, StatusProcessamentoCompraSap = "05" });
        public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string> ObterFornecedorPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<DateOnly?> ObterDataPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<DateOnly?>(null);
        public Task<string> ObterTipoPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PedidoCompraSapItem>>([]);
    }
}
