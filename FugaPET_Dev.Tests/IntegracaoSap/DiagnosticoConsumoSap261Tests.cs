using FugaPET_Dev.Modelo.Diagnostico;
using FugaPET_Dev.Servicos.Diagnostico;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class DiagnosticoConsumoSap261Tests
{
    [Fact]
    public async Task BancoIndisponivel_DeveRetornarBloqueio()
    {
        DiagnosticoConsumoSap261Servico servico = CriarServico(new FakeProbe { Falhar = true }, SapValido(escrita: false));

        DiagnosticoConsumoSap261Resultado resultado = await servico.ExecutarAsync();

        Assert.False(resultado.ProntoParaTesteLocal);
        Assert.Contains(resultado.Itens, item => item.Codigo == "BANCO_CONEXAO" && item.Status == "BLOQUEIO");
    }

    [Fact]
    public async Task TabelaLancamentoAusente_DeveRetornarBloqueio()
    {
        DiagnosticoConsumoSap261BancoSnapshot snapshot = SnapshotValido();
        snapshot.Tabelas.Remove("consumo_material_lancamento");
        DiagnosticoConsumoSap261Servico servico = CriarServico(new FakeProbe { Snapshot = snapshot }, SapValido(escrita: false));

        DiagnosticoConsumoSap261Resultado resultado = await servico.ExecutarAsync();

        Assert.False(resultado.ProntoParaTesteLocal);
        Assert.Contains(resultado.Itens, item => item.Codigo == "TABELA_consumo_material_lancamento" && item.Status == "BLOQUEIO");
    }

    [Fact]
    public async Task StatusEnviandoAusente_DeveRetornarBloqueio()
    {
        DiagnosticoConsumoSap261BancoSnapshot snapshot = SnapshotValido(
            "PENDENTE_SAP CONFIRMADO_SAP FALHA_SAP CANCELADO_LOCAL REGISTRADA_LOCALMENTE");
        DiagnosticoConsumoSap261Servico servico = CriarServico(new FakeProbe { Snapshot = snapshot }, SapValido(escrita: false));

        DiagnosticoConsumoSap261Resultado resultado = await servico.ExecutarAsync();

        Assert.False(resultado.ProntoParaTesteLocal);
        Assert.Contains(resultado.Itens, item => item.Codigo == "STATUS_ENVIANDO_SAP" && item.Status == "BLOQUEIO");
    }

    [Fact]
    public async Task StatusPesagemRegistradaAusente_DeveRetornarBloqueio()
    {
        DiagnosticoConsumoSap261BancoSnapshot snapshot = SnapshotValido(
            "PENDENTE_SAP ENVIANDO_SAP CONFIRMADO_SAP FALHA_SAP CANCELADO_LOCAL");
        DiagnosticoConsumoSap261Servico servico = CriarServico(new FakeProbe { Snapshot = snapshot }, SapValido(escrita: false));

        DiagnosticoConsumoSap261Resultado resultado = await servico.ExecutarAsync();

        Assert.False(resultado.ProntoParaTesteLocal);
        Assert.Contains(resultado.Itens, item => item.Codigo == "STATUS_PESAGEM_REGISTRADA" && item.Status == "BLOQUEIO");
    }

    [Fact]
    public async Task EstruturaEssencialCompleta_DevePermitirTesteLocalEPreview()
    {
        DiagnosticoConsumoSap261Servico servico = CriarServico(new FakeProbe { Snapshot = SnapshotValido() }, SapValido(escrita: false));

        DiagnosticoConsumoSap261Resultado resultado = await servico.ExecutarAsync();

        Assert.True(resultado.ProntoParaTesteLocal);
        Assert.True(resultado.ProntoParaPreview);
        Assert.False(resultado.ProntoParaEnvioSap);
        Assert.Contains(resultado.Itens, item => item.Codigo == "SAP_WRITE_ENABLED" && item.Status == "ALERTA");
    }

    [Fact]
    public async Task WriteEnabledTrue_ComSapValido_DevePermitirEnvioSapComAlerta()
    {
        DiagnosticoConsumoSap261Servico servico = CriarServico(new FakeProbe { Snapshot = SnapshotValido() }, SapValido(escrita: true));

        DiagnosticoConsumoSap261Resultado resultado = await servico.ExecutarAsync();

        Assert.True(resultado.ProntoParaEnvioSap);
        Assert.Contains(resultado.Itens, item => item.Codigo == "SAP_WRITE_ENABLED" && item.Mensagem.Contains("massa autorizada", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(resultado.Itens, item => item.Codigo == "RESUMO" && item.Mensagem.Contains("confirmação manual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UrlSemHttpsOuHostForaAllowlist_DeveBloquearEnvio()
    {
        DiagnosticoConsumoSap261Servico semHttps = CriarServico(
            new FakeProbe { Snapshot = SnapshotValido() },
            SapValido(escrita: true, materialDocumentBaseUrl: "http://sap.example.com/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/"));
        DiagnosticoConsumoSap261Servico hostFora = CriarServico(
            new FakeProbe { Snapshot = SnapshotValido() },
            SapValido(escrita: true, materialDocumentBaseUrl: "https://outro.example.com/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/"));

        DiagnosticoConsumoSap261Resultado r1 = await semHttps.ExecutarAsync();
        DiagnosticoConsumoSap261Resultado r2 = await hostFora.ExecutarAsync();

        Assert.False(r1.ProntoParaEnvioSap);
        Assert.Contains(r1.Itens, item => item.Codigo == "SAP_HTTPS" && item.Status == "BLOQUEIO");
        Assert.False(r2.ProntoParaEnvioSap);
        Assert.Contains(r2.Itens, item => item.Codigo == "SAP_ALLOWLIST" && item.Status == "BLOQUEIO");
    }

    [Fact]
    public void Diagnostico_NaoDeveFazerPostCsrfPatchOuDml()
    {
        string servico = LerArquivoProjeto("Servicos", "Diagnostico", "DiagnosticoConsumoSap261Servico.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "DiagnosticoConsumoSap261Form.cs");

        Assert.DoesNotContain("HttpClient", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CSRF", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PostAsync", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PATCH", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnviarConsumoSap261Async", form, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarReservarEnvioSapAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarFalhaSapAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarConsumoConfirmadoSapAsync", form, StringComparison.Ordinal);
    }

    [Fact]
    public void EntradaProduto_DevePermanecerIntacta()
    {
        string entradaController = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string entradaForm = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("GoodsMovementRefDocType = \"B\"", entradaController, StringComparison.Ordinal);
        Assert.DoesNotContain("PATCH", entradaController, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PATCH", entradaForm, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sql030_DeveDeclararEValidarStatusPesagemRegistradaLocalmente()
    {
        string pastaSql = Path.Combine(
            RaizProjeto(),
            "BancoDados",
            "001_incrementais",
            "030_consumo_material_persistencia_local_GAIA");
        string proposta = File.ReadAllText(Path.Combine(pastaSql, "030_consumo_material_persistencia_local_PROPOSTA_GAIA.sql"));
        string validacao = File.ReadAllText(Path.Combine(pastaSql, "030_consumo_material_persistencia_local_VALIDACAO_GAIA.sql"));

        Assert.Contains("CONSTRAINT ck_consumo_pesagem_status", proposta, StringComparison.Ordinal);
        Assert.Contains("status_pesagem IN ('REGISTRADA_LOCALMENTE')", proposta, StringComparison.Ordinal);
        Assert.Contains("ck_consumo_pesagem_status", validacao, StringComparison.Ordinal);
        Assert.Contains("REGISTRADA_LOCALMENTE", validacao, StringComparison.Ordinal);
    }

    private static DiagnosticoConsumoSap261Servico CriarServico(FakeProbe probe, ConfiguracaoSap sap)
        => new(probe, () => sap);

    private static ConfiguracaoSap SapValido(
        bool escrita,
        string materialDocumentBaseUrl = "https://sap.example.com/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/")
        => new()
        {
            MaterialDocumentBaseUrl = materialDocumentBaseUrl,
            Usuario = "usuario",
            Senha = "senha",
            SapClient = "110",
            HostsPermitidos = ["sap.example.com"],
            EscritaHabilitada = escrita
        };

    private static DiagnosticoConsumoSap261BancoSnapshot SnapshotValido(
        string definicoesConstraints = "PENDENTE_SAP ENVIANDO_SAP CONFIRMADO_SAP FALHA_SAP CANCELADO_LOCAL REGISTRADA_LOCALMENTE")
        => new()
        {
            Tabelas =
            [
                "consumo_material_lancamento",
                "consumo_material_item",
                "consumo_material_pesagem"
            ],
            ColunasPorTabela = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["consumo_material_lancamento"] =
                [
                    "codigo_consumo_material_lancamento", "numero_ordem", "status_lancamento",
                    "documento_material_sap", "exercicio_documento_material_sap", "enviado_sap_em",
                    "consumo_material_lancamento_criado_em", "consumo_material_lancamento_atualizado_em"
                ],
                ["consumo_material_item"] =
                [
                    "codigo_consumo_material_item", "codigo_consumo_material_lancamento", "codigo_material",
                    "numero_reserva", "item_reserva", "quantidade_consumida_local", "tipo_movimento_sap", "status_item"
                ],
                ["consumo_material_pesagem"] =
                [
                    "codigo_consumo_material_pesagem", "codigo_consumo_material_item", "peso_bruto_kg",
                    "peso_tara_kg", "peso_liquido_kg", "origem", "pesado_em", "status_pesagem"
                ]
            },
            DefinicoesConstraints = definicoesConstraints,
            Triggers =
            [
                "consumo_material_lancamento:trg_consumo_material_lancamento_atualizado_em",
                "consumo_material_item:trg_consumo_material_item_atualizado_em"
            ],
            Indices =
            [
                "idx_consumo_numero_ordem",
                "idx_consumo_status",
                "idx_consumo_lancamento",
                "idx_consumo_item",
                "idx_consumo_reserva_item_reserva",
                "idx_consumo_documento_material_sap_ano"
            ]
        };

    private sealed class FakeProbe : IDiagnosticoConsumoSap261BancoProbe
    {
        public bool Falhar { get; init; }
        public DiagnosticoConsumoSap261BancoSnapshot Snapshot { get; init; } = SnapshotValido();

        public Task<DiagnosticoConsumoSap261BancoSnapshot> ObterSnapshotAsync(CancellationToken cancellationToken = default)
            => Falhar
                ? Task.FromException<DiagnosticoConsumoSap261BancoSnapshot>(new InvalidOperationException("sem VPN"))
                : Task.FromResult(Snapshot);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

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
}
