using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Modo operacional repetitivo HML (decisão Hera): Produto Acabado HU permitido SEM gate por OP, porém
/// EXCLUSIVAMENTE para o centro PET 3007. Prova a regra CENTRAL, a defesa em profundidade (Controller na
/// consulta, Form na elegibilidade/ação, Service antes do claim) e que nenhuma OP está hardcoded no código
/// produtivo. Testes de fonte + comportamento puro (não instanciam a Form, não tocam banco/SAP).
/// </summary>
public sealed class ProdutoAcabadoCentroPet3007Tests
{
    private static string RaizProjeto()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return dir;
    }

    private static string LerFonte(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    // A regra central existe e concentra o centro PET (constante única, sem "3007" espalhado).
    [Fact]
    public void RegraCentral_ExisteEConcentraOCentroPet()
    {
        Assert.Equal("3007", RegraCentroPetProdutoAcabado.CentroPetHuPermitido);
        Assert.True(RegraCentroPetProdutoAcabado.CentroPermitido("3007"));
        Assert.False(RegraCentroPetProdutoAcabado.CentroPermitido("3009"));
        Assert.False(string.IsNullOrWhiteSpace(RegraCentroPetProdutoAcabado.MensagemCentroNaoPermitido));
    }

    // Defesa 1: o Controller bloqueia a consulta de OP fora do centro PET, sem escrita.
    [Fact]
    public void Controller_ConsultaOP_BloqueiaCentroDiferente()
    {
        string controller = LerFonte("Controle", "Processo", "ProdutoAcabadoController.cs");
        Assert.Contains("RegraCentroPetProdutoAcabado.CentroPermitido(ordem.Centro)", controller, StringComparison.Ordinal);
        Assert.Contains("RegraCentroPetProdutoAcabado.MensagemCentroNaoPermitido", controller, StringComparison.Ordinal);
        // Não usa literal "3007" espalhado no Controller.
        Assert.DoesNotContain("\"3007\"", controller, StringComparison.Ordinal);
    }

    // Defesa 2: a elegibilidade do botão exige centro 3007 (nunca por linha selecionada).
    [Fact]
    public void Form_Elegibilidade_ExigeCentroPet()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("private void AtualizarEstadoEnvioCaixaSap()", StringComparison.Ordinal);
        int fim = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string atualizar = form[inicio..fim];
        Assert.Contains("RegraCentroPetProdutoAcabado.CentroPermitido(caixa.Centro)", atualizar, StringComparison.Ordinal);
        // A elegibilidade continua derivada da caixa ATIVA (LastOrDefault), não da seleção da grid.
        Assert.Contains("_caixasPesadas.LastOrDefault()", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("productionDataGridView.SelectedRows", atualizar, StringComparison.Ordinal);
    }

    // Defesa 3: antes de autorizar/claim/POST, o handler revalida o centro (precede AutorizarEnvioCaixaAsync).
    [Fact]
    public void Form_SolicitarEnvio_RevalidaCentroAntesDaAcao()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        int fim = form.IndexOf("private void SubstituirCaixaNoCache(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string envio = form[inicio..fim];

        int posCentro = envio.IndexOf("RegraCentroPetProdutoAcabado.CentroPermitido(caixa.Centro)", StringComparison.Ordinal);
        int posAutorizar = envio.IndexOf("await _controller.AutorizarEnvioCaixaAsync(", StringComparison.Ordinal);
        int posEnviar = envio.IndexOf("await _controller.EnviarCaixaHandlingUnitAsync(", StringComparison.Ordinal);
        Assert.True(posCentro >= 0);
        Assert.True(posCentro < posAutorizar, "revalidação de centro precede a autorização local");
        Assert.True(posCentro < posEnviar, "revalidação de centro precede o envio/POST");
    }

    // Defesa 4: o Service valida o centro do snapshot ANTES do claim.
    [Fact]
    public void Service_EnviarAsync_ValidaCentroAntesDoClaim()
    {
        string service = LerFonte("Servicos", "Operacao", "ProdutoAcabadoHuService.cs");
        int posCentro = service.IndexOf("RegraCentroPetProdutoAcabado.CentroPermitido(", StringComparison.Ordinal);
        int posClaim = service.IndexOf("_repositorio.ClaimEnvioAsync(", StringComparison.Ordinal);
        Assert.True(posCentro >= 0);
        Assert.True(posClaim >= 0);
        Assert.True(posCentro < posClaim, "a validação de centro precede o claim atômico");
        Assert.DoesNotContain("\"3007\"", service, StringComparison.Ordinal); // usa a regra central, não literal
    }

    // Nenhuma OP hardcoded / condição especial no código PRODUTIVO (1001951 e 1002024 só existem em testes/dados).
    [Fact]
    public void CodigoProdutivo_SemOpHardcoded()
    {
        foreach (string[] arquivo in new[]
        {
            new[] { "Controle", "Processo", "ProdutoAcabadoController.cs" },
            new[] { "Servicos", "Operacao", "ProdutoAcabadoHuService.cs" },
            new[] { "Tela", "Processo", "ProcessoProdutoAcabadoForm.cs" },
            new[] { "Modelo", "Processo", "RegraCentroPetProdutoAcabado.cs" },
        })
        {
            string fonte = LerFonte(arquivo);
            Assert.DoesNotContain("1001951", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("1002024", fonte, StringComparison.Ordinal);
        }
    }
}
