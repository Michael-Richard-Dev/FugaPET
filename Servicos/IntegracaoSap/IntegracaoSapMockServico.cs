using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Implementacao MOCK do contrato de integracao SAP. NAO conversa com SAP real:
/// devolve um historico simulado, resolve produto por OP a partir desse historico e
/// "finge" um envio bem-sucedido (com protocolo simulado). Serve para destravar o
/// desenvolvimento de telas/servicos com os tipos definitivos. <see cref="EhSimulado"/> = true.
/// </summary>
public sealed class IntegracaoSapMockServico : IIntegracaoSapServico
{
    public IntegracaoSapMockServico()
    {
        global::FugaPET_Dev.AcessoDados.Banco.EstadoIntegracaoBanco.GarantirDadosSimuladosPermitidos();
    }

    internal IntegracaoSapMockServico(bool usoAutorizado)
    {
        if (!usoAutorizado)
        {
            throw new InvalidOperationException(
                "Mock SAP proibido fora de ambiente demonstrativo com banco desabilitado.");
        }
    }

    public bool EhSimulado => true;

    public Task<IReadOnlyList<RegistroIntegracaoSap>> ConsultarHistoricoAsync(
        FiltroConsultaIntegracaoSap filtro,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<RegistroIntegracaoSap> consulta = ObterHistoricoSimulado();

        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            string termo = filtro.Termo.Trim();
            consulta = consulta.Where(r =>
                r.OrdemProducao.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                r.Produto.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                (r.Lote?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (filtro.DataInicial.HasValue)
            consulta = consulta.Where(r => r.DataHora.DateTime >= filtro.DataInicial.Value);

        if (filtro.DataFinal.HasValue)
            consulta = consulta.Where(r => r.DataHora.DateTime <= filtro.DataFinal.Value);

        int limite = filtro.Limite > 0 ? filtro.Limite : 200;
        IReadOnlyList<RegistroIntegracaoSap> resultado = consulta
            .OrderByDescending(r => r.DataHora)
            .Take(limite)
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<ProdutoSap?> ConsultarProdutoPorOrdemAsync(
        string ordemProducao,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ordemProducao))
            return Task.FromResult<ProdutoSap?>(null);

        string op = ordemProducao.Trim();
        RegistroIntegracaoSap? registro = ObterHistoricoSimulado()
            .FirstOrDefault(r => string.Equals(r.OrdemProducao, op, StringComparison.OrdinalIgnoreCase));

        if (registro is null)
            return Task.FromResult<ProdutoSap?>(null);

        ProdutoSap produto = new()
        {
            OrdemProducao = registro.OrdemProducao,
            CodigoProduto = $"SKU-{registro.OrdemProducao}",
            DescricaoProduto = registro.Produto,
            Unidade = registro.Unidade,
            Lote = registro.Lote,
            QuantidadePlanejada = registro.Quantidade,
            Ativo = true
        };

        return Task.FromResult<ProdutoSap?>(produto);
    }

    public Task<ResultadoEnvioSap> EnviarApontamentoAsync(
        ApontamentoSap apontamento,
        CancellationToken cancellationToken = default)
    {
        // Mesmo no mock, valida o contrato para o consumidor exercitar o fluxo de erro.
        if (string.IsNullOrWhiteSpace(apontamento.OrdemProducao))
            return Task.FromResult(ResultadoEnvioSap.Falha("Ordem de producao e obrigatoria para o envio ao SAP."));

        if (apontamento.Quantidade <= 0)
            return Task.FromResult(ResultadoEnvioSap.Falha("Quantidade do apontamento deve ser maior que zero."));

        // MOCK: gera um protocolo simulado, sem nenhuma chamada externa.
        string protocolo = $"SIM-{DateTime.Now:yyyyMMddHHmmss}-{apontamento.OrdemProducao}";
        return Task.FromResult(
            ResultadoEnvioSap.Ok(protocolo, "Apontamento simulado registrado (sem SAP real)."));
    }

    /// <summary>
    /// Historico simulado, ancorado em "hoje" para nao envelhecer. Espelha as colunas
    /// usadas pela tela de consulta de integracao SAP.
    /// </summary>
    private static IReadOnlyList<RegistroIntegracaoSap> ObterHistoricoSimulado()
    {
        DateTime hoje = DateTime.Today;

        return
        [
            Criar(hoje, 13, 36, TipoMovimentoSap.Envio, "58895", "PE - TURMA DA MONICA CARNE E LEITE", "131 262F", "7891234567890", 16.000m, "OPERADOR01", "Balanca F12", SituacaoIntegracaoSap.Sucesso),
            Criar(hoje, 13, 32, TipoMovimentoSap.Envio, "58894", "MP PULMAO CONG BOVINO NT CONGELADO", null, null, 16.000m, "OPERADOR01", "Manual", SituacaoIntegracaoSap.Sucesso),
            Criar(hoje, 13, 28, TipoMovimentoSap.Retorno, "58893", "MP C.M.S DE FRANGO NT CONGELADO", null, null, 4.000m, "OPERADOR02", "Balanca F8", SituacaoIntegracaoSap.Confirmado),
            Criar(hoje, 13, 21, TipoMovimentoSap.Envio, "58892", "PEITO DE FRANGO S/OSSO CONGELADO", null, null, 8.000m, "SISTEMA", "Automatico", SituacaoIntegracaoSap.Enviado),
            Criar(hoje, 13, 15, TipoMovimentoSap.Retorno, "58891", "MP FIGADO DE FRANGO CONGELADO", null, "7899876543210", 4.000m, "OPERADOR01", "Balanca F12", SituacaoIntegracaoSap.Confirmado),
            Criar(hoje, 13, 9, TipoMovimentoSap.Envio, "58890", "COXA E SOBRECOXA CONGELADA", null, null, 12.000m, "OPERADOR02", "Manual", SituacaoIntegracaoSap.Sucesso),
            Criar(hoje, 12, 55, TipoMovimentoSap.Retorno, "58889", "ASA DE FRANGO CONGELADA", null, "7891112223334", 10.000m, "OPERADOR01", "Balanca F12", SituacaoIntegracaoSap.Confirmado),
            Criar(hoje, 12, 42, TipoMovimentoSap.Erro, "58888", "LINGUICA DE FRANGO CONGELADA", null, null, 6.000m, "SISTEMA", "Automatico", SituacaoIntegracaoSap.Erro)
        ];
    }

    private static RegistroIntegracaoSap Criar(
        DateTime dia,
        int hora,
        int minuto,
        TipoMovimentoSap tipo,
        string ordemProducao,
        string produto,
        string? lote,
        string? codigoBarras,
        decimal quantidade,
        string usuario,
        string origem,
        SituacaoIntegracaoSap situacao)
        => new()
        {
            DataHora = new DateTimeOffset(dia.AddHours(hora).AddMinutes(minuto)),
            Tipo = tipo,
            OrdemProducao = ordemProducao,
            Produto = produto,
            Lote = lote,
            CodigoBarras = codigoBarras,
            Quantidade = quantidade,
            Unidade = "KG",
            Usuario = usuario,
            Origem = origem,
            Situacao = situacao
        };
}
