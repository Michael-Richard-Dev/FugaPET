using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Percorre as caixas pendentes de um envio em lote (processo ENVIO_UNICO), uma a uma.
/// <para>
/// NAO sabe falar com o SAP, NAO abre caixa de dialogo e NAO toca em tela: recebe a lista e um
/// delegado que envia UMA caixa e devolve o status PERSISTIDO dela. E isso que torna o lote testavel
/// sem banco, sem rede e sem WinForms — a regra de parada e o relato nascem aqui, nao no Form.
/// </para>
/// <para>
/// O delegado devolve <c>StatusIntegracaoCaixa?</c>, e nao um booleano, de proposito: "nao confirmou"
/// tem motivos diferentes (erro comprovado, indeterminado, bloqueada) e o operador precisa saber qual.
/// Nulo significa que nem o estado final foi possivel determinar — o pior caso, e o que mais justifica
/// parar.
/// </para>
/// </summary>
public sealed class ExecutorEnvioLoteCaixas
{
    /// <summary>
    /// Caixa que pode ser enviada. E a MESMA condicao do envio individual, escrita uma vez so: o lote
    /// nao pode ser mais permissivo que o botao de uma caixa, senao vira uma porta lateral para o SAP.
    /// </summary>
    public static bool Elegivel(ProdutoAcabadoCaixa? caixa)
        => caixa?.CodigoProdutoAcabadoCaixa is long
            && caixa.StatusIntegracao
                is StatusIntegracaoCaixa.AguardandoAutorizacaoSap
                or StatusIntegracaoCaixa.ProntaParaEnvio;

    /// <summary>
    /// Envia as caixas na ORDEM RECEBIDA e para na primeira que nao confirmar.
    /// <para>
    /// Parar nao e timidez. Um resultado indeterminado deixa a caixa num estado que so sai por
    /// reconciliacao; empilhar mais POSTs em cima enterraria o problema sob dez linhas novas, e o
    /// operador descobriria tarde. Parando, ele ve exatamente onde travou e o que nao foi tentado.
    /// </para>
    /// <para>
    /// Caixa ja CONFIRMADA_SAP e pulada em silencio — e o que torna o lote RETOMAVEL sem nenhum
    /// controle extra: resolver a que travou e acionar de novo continua de onde parou.
    /// </para>
    /// </summary>
    public async Task<ResultadoEnvioLoteCaixas> ExecutarAsync(
        IReadOnlyList<ProdutoAcabadoCaixa> caixas,
        Func<ProdutoAcabadoCaixa, CancellationToken, Task<StatusIntegracaoCaixa?>> enviarUma,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caixas);
        ArgumentNullException.ThrowIfNull(enviarUma);

        List<ProdutoAcabadoCaixa> enviadas = [];
        List<ProdutoAcabadoCaixa> puladas = [];
        List<ProdutoAcabadoCaixa> foraDoLote = [];

        for (int i = 0; i < caixas.Count; i++)
        {
            ProdutoAcabadoCaixa caixa = caixas[i];

            if (caixa.StatusIntegracao == StatusIntegracaoCaixa.ConfirmadaSap)
            {
                puladas.Add(caixa);
                continue;
            }

            if (!Elegivel(caixa))
            {
                // Fora do lote, mas NAO interrompe: uma caixa bloqueada no meio nao e motivo para
                // deixar as outras sem enviar. Ela entra no relato para tratamento individual.
                foraDoLote.Add(caixa);
                continue;
            }

            StatusIntegracaoCaixa? status = await enviarUma(caixa, cancellationToken);

            if (status == StatusIntegracaoCaixa.ConfirmadaSap)
            {
                enviadas.Add(caixa);
                continue;
            }

            // Daqui em diante ninguem mais e tentado. O que sobrou da lista e reportado como nao
            // tentado, inclusive o que seria pulado ou inelegivel: nao foram avaliados, e afirmar o
            // contrario seria inventar.
            return ResultadoEnvioLoteCaixas.Interrompido(
                enviadas, puladas, foraDoLote, caixa, status, [.. caixas.Skip(i + 1)]);
        }

        return ResultadoEnvioLoteCaixas.Completo(enviadas, puladas, foraDoLote);
    }
}

/// <summary>O que aconteceu no lote, em termos que o operador entende.</summary>
public sealed class ResultadoEnvioLoteCaixas
{
    private ResultadoEnvioLoteCaixas(
        IReadOnlyList<ProdutoAcabadoCaixa> enviadas,
        IReadOnlyList<ProdutoAcabadoCaixa> puladas,
        IReadOnlyList<ProdutoAcabadoCaixa> foraDoLote,
        ProdutoAcabadoCaixa? parouEm,
        StatusIntegracaoCaixa? statusDoParado,
        IReadOnlyList<ProdutoAcabadoCaixa> naoTentadas)
    {
        Enviadas = enviadas;
        Puladas = puladas;
        ForaDoLote = foraDoLote;
        ParouEm = parouEm;
        StatusDoParado = statusDoParado;
        NaoTentadas = naoTentadas;
    }

    /// <summary>Caixas que o SAP confirmou nesta rodada.</summary>
    public IReadOnlyList<ProdutoAcabadoCaixa> Enviadas { get; }

    /// <summary>Ja estavam CONFIRMADA_SAP antes de comecar.</summary>
    public IReadOnlyList<ProdutoAcabadoCaixa> Puladas { get; }

    /// <summary>Nao podiam ser enviadas (bloqueada, indeterminada, erro, cancelada).</summary>
    public IReadOnlyList<ProdutoAcabadoCaixa> ForaDoLote { get; }

    /// <summary>A caixa que interrompeu o lote, quando houve interrupcao.</summary>
    public ProdutoAcabadoCaixa? ParouEm { get; }

    /// <summary>Status persistido da caixa que interrompeu; nulo = estado final indeterminado.</summary>
    public StatusIntegracaoCaixa? StatusDoParado { get; }

    /// <summary>Caixas que vinham depois da interrupcao e nao chegaram a ser avaliadas.</summary>
    public IReadOnlyList<ProdutoAcabadoCaixa> NaoTentadas { get; }

    public bool Interrompeu => ParouEm is not null;

    /// <summary>Nenhum POST foi executado nesta rodada.</summary>
    public bool NadaTentado => Enviadas.Count == 0 && !Interrompeu;

    internal static ResultadoEnvioLoteCaixas Completo(
        IReadOnlyList<ProdutoAcabadoCaixa> enviadas,
        IReadOnlyList<ProdutoAcabadoCaixa> puladas,
        IReadOnlyList<ProdutoAcabadoCaixa> foraDoLote)
        => new(enviadas, puladas, foraDoLote, null, null, []);

    internal static ResultadoEnvioLoteCaixas Interrompido(
        IReadOnlyList<ProdutoAcabadoCaixa> enviadas,
        IReadOnlyList<ProdutoAcabadoCaixa> puladas,
        IReadOnlyList<ProdutoAcabadoCaixa> foraDoLote,
        ProdutoAcabadoCaixa parouEm,
        StatusIntegracaoCaixa? statusDoParado,
        IReadOnlyList<ProdutoAcabadoCaixa> naoTentadas)
        => new(enviadas, puladas, foraDoLote, parouEm, statusDoParado, naoTentadas);
}
