using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Tarefa Consumo 22.9.2: OP sem componentes compatíveis com o modo da tela é BLOQUEADA visualmente
/// (alerta + estado inicial), em vez de abrir a tela operacional com grid vazia.
/// </summary>
public sealed class ProcessoConsumoMaterialBloqueio2292Tests
{
    // ---- Ajuste 3: mensagem do modo Químico ----

    [Fact]
    public void Mensagem_Quimico_DeveTitularEOrientarParaMateriaPrima()
    {
        (string titulo, string mensagem) = ProcessoConsumoMaterialForm.MontarMensagemOpIncompativel(
            ModoConsumoMaterial.Quimico, "1001180", "2000091", totalComponentes: 4);

        Assert.Equal("OP não pertence ao Consumo Químico", titulo);
        Assert.Contains("Esta OP não possui componentes classificados para Consumo Químico.", mensagem, StringComparison.Ordinal);
        Assert.Contains("OP: 1001180", mensagem, StringComparison.Ordinal);
        Assert.Contains("Produto da OP: 2000091", mensagem, StringComparison.Ordinal);
        // Ajuste 3: contagem quando há componentes (mas nenhum químico).
        Assert.Contains("Componentes encontrados: 4", mensagem, StringComparison.Ordinal);
        Assert.Contains("Componentes químicos encontrados: 0", mensagem, StringComparison.Ordinal);
        // Ajuste 6 da mensagem: orientar a usar a outra tela / verificar SAP.
        Assert.Contains("Use a tela de Consumo de Matéria-Prima ou verifique a classificação dos componentes no SAP.", mensagem, StringComparison.Ordinal);
    }

    // ---- Ajuste 4: mensagem do modo Matéria-Prima ----

    [Fact]
    public void Mensagem_MateriaPrima_DeveTitularEOrientarParaQuimico()
    {
        (string titulo, string mensagem) = ProcessoConsumoMaterialForm.MontarMensagemOpIncompativel(
            ModoConsumoMaterial.MateriaPrima, "1002000", "3000123", totalComponentes: 2);

        Assert.Equal("OP não pertence ao Consumo de Matéria-Prima", titulo);
        Assert.Contains("Esta OP não possui componentes classificados para Consumo de Matéria-Prima.", mensagem, StringComparison.Ordinal);
        Assert.Contains("OP: 1002000", mensagem, StringComparison.Ordinal);
        Assert.Contains("Produto da OP: 3000123", mensagem, StringComparison.Ordinal);
        Assert.Contains("Componentes encontrados: 2", mensagem, StringComparison.Ordinal);
        Assert.Contains("Componentes de matéria-prima encontrados: 0", mensagem, StringComparison.Ordinal);
        Assert.Contains("Use a tela de Consumo Químico ou verifique a classificação dos componentes no SAP.", mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Mensagem_SemComponentes_NaoExibeContagem()
    {
        (_, string mensagem) = ProcessoConsumoMaterialForm.MontarMensagemOpIncompativel(
            ModoConsumoMaterial.Quimico, "1003000", "4000999", totalComponentes: 0);

        Assert.DoesNotContain("Componentes encontrados:", mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("encontrados: 0", mensagem, StringComparison.Ordinal);
        Assert.Contains("OP: 1003000", mensagem, StringComparison.Ordinal);
        Assert.Contains("Produto da OP: 4000999", mensagem, StringComparison.Ordinal);
    }

    // ---- Ajustes 1/2/5: wiring do bloqueio em ConsultarOrdemProducaoAsync ----

    [Fact]
    public void Consultar_DeveBloquearQuandoNenhumComponenteDoModo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string consultar = ExtrairMetodo(form, "private async Task ConsultarOrdemProducaoAsync");

        // Ajuste 1: enriquece/filtra e valida lista vazia ANTES de carregar operacional.
        Assert.Contains("EnriquecerEClassificarComponentesDoModo(resultado.Ordem)", consultar, StringComparison.Ordinal);
        Assert.Contains("if (componentesModo.Count == 0)", consultar, StringComparison.Ordinal);
        Assert.Contains("BloquearOrdemIncompativelComModo(resultado.Ordem, resultado.NumeroOrdem)", consultar, StringComparison.Ordinal);

        // Ajuste 5: o bloqueio retorna ANTES de PreencherOrdemCarregada (não carrega grid vazia como sucesso).
        int bloqueio = consultar.IndexOf("BloquearOrdemIncompativelComModo", StringComparison.Ordinal);
        int preencher = consultar.IndexOf("PreencherOrdemCarregada(resultado.Ordem, componentesModo)", StringComparison.Ordinal);
        int retornoBloqueio = consultar.IndexOf("return;", bloqueio, StringComparison.Ordinal);
        Assert.True(bloqueio >= 0 && preencher > bloqueio);
        Assert.True(retornoBloqueio > bloqueio && retornoBloqueio < preencher);
    }

    // ---- Ajustes 2/3/4/5/6/7: comportamento do método de bloqueio ----

    [Fact]
    public void Bloqueio_DeveVoltarEstadoInicialAlertarEFocarCampoOrdem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string bloqueio = ExtrairMetodo(form, "private void BloquearOrdemIncompativelComModo");

        // Estado inicial (limpa operacional, desabilita ações) mantendo só o número da OP.
        Assert.Contains("LimparDadosOrdem(limparNumeroOrdem: false)", bloqueio, StringComparison.Ordinal);
        Assert.Contains("DefinirTextoCampoOrdem(numeroOrdem)", bloqueio, StringComparison.Ordinal);
        // Painel de orientação + status SAP de bloqueio.
        Assert.Contains("AtualizarApontamentoOpIncompativel()", bloqueio, StringComparison.Ordinal);
        // Alerta por modo + foco de volta no campo OP + diagnóstico.
        Assert.Contains("MessageBox.Show(mensagem, titulo", bloqueio, StringComparison.Ordinal);
        Assert.Contains("DevolverFocoParaCampoOrdem()", bloqueio, StringComparison.Ordinal);
        Assert.Contains("RegistrarDiagnosticoOpIncompativel(ordem)", bloqueio, StringComparison.Ordinal);
    }

    [Fact]
    public void Bloqueio_EstadoVisual_DeveUsarBloqueadoEOrientacaoIncompativel()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string apontamento = ExtrairMetodo(form, "private void AtualizarApontamentoOpIncompativel");

        Assert.Contains("EstadoVisualIntegracaoSapConsumo.BloqueadoOpIncompativel", apontamento, StringComparison.Ordinal);
        Assert.Contains("OP incompatível com esta tela.", apontamento, StringComparison.Ordinal);
        // Estado visual mapeado para "SAP HML: BLOQUEADO".
        Assert.Contains("BloqueadoOpIncompativel => (\"SAP HML: BLOQUEADO\"", form, StringComparison.Ordinal);
    }

    // ---- Ajuste 7: diagnóstico com total de componentes e total compatível ----

    [Fact]
    public void Diagnostico_DeveRegistrarTotaisEClassificacoes()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string diagnostico = ExtrairMetodo(form, "private void RegistrarDiagnosticoOpIncompativel");

        Assert.Contains("[Consumo][OpIncompativel]", diagnostico, StringComparison.Ordinal);
        Assert.Contains("Total de componentes da OP:", diagnostico, StringComparison.Ordinal);
        Assert.Contains("Total de componentes compativeis com o modo: 0", diagnostico, StringComparison.Ordinal);
        Assert.Contains("Indefinido:", diagnostico, StringComparison.Ordinal);
        Assert.Contains("Resultado: OP bloqueada para o modo atual", diagnostico, StringComparison.Ordinal);
    }

    // ---- Ajuste 8: OP com componente compatível continua carregando normalmente ----

    [Fact]
    public void Consultar_ComComponenteCompativel_CarregaNormalmente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string consultar = ExtrairMetodo(form, "private async Task ConsultarOrdemProducaoAsync");

        // Caminho normal preservado: quando NÃO está vazia, preenche a OP e registra recente.
        Assert.Contains("PreencherOrdemCarregada(resultado.Ordem, componentesModo)", consultar, StringComparison.Ordinal);
        Assert.Contains("RegistrarOrdemRecenteSePermitida(resultado.Ordem)", consultar, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximoMetodo > inicio, $"Fim do método não encontrado: {assinatura}");

        return fonte[inicio..proximoMetodo];
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
