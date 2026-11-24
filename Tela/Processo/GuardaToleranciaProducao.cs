using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// A TRAVA DE TOLERÂNCIA DAS TELAS DE PRODUÇÃO, escrita UMA VEZ e usada pelas duas (incremento 071).
/// <para>
/// Consumo de Material e Produto Semi-Acabado compartilham esta classe de propósito. Em 24/11/2026
/// uma trava de tolerância existia num caminho de pesagem e não em outro, e por isso não valia nada:
/// o defeito nasce de haver duas implementações, não de haver duas telas. Aqui há uma só, e ligar
/// uma tela nova é chamá-la, não copiá-la.
/// </para>
/// <para>
/// <b>Só o TETO é cobrado aqui</b>, nunca o piso. Nenhuma das duas telas tem um momento que declare
/// a ordem concluída — produção e consumo parciais contra uma ordem aberta são normais —, e todo
/// componente começa em zero, que está abaixo de qualquer piso. Cobrar o piso durante a pesagem
/// barraria a PRIMEIRA pesagem de toda ordem. É a mesma razão pela qual o Recebimento só cobra o
/// piso na finalização, que é o momento em que faltar vira fato.
/// </para>
/// </summary>
internal sealed class GuardaToleranciaProducao
{
    private readonly string _processo;
    private readonly ToleranciaProcessoProducaoRepositorio _repositorio;
    private readonly Func<AutorizacaoToleranciaProducaoServico> _autorizacao;

    /// <summary>Cache por material: a trava roda a cada pesagem e não pode ir ao banco toda vez.</summary>
    private readonly Dictionary<string, ToleranciaProcessoProducao?> _cache = new(StringComparer.Ordinal);

    public GuardaToleranciaProducao(
        string processo,
        ToleranciaProcessoProducaoRepositorio? repositorio = null,
        Func<AutorizacaoToleranciaProducaoServico>? autorizacao = null)
    {
        if (!ProcessoToleranciaProducao.EhValido(processo))
        {
            throw new ArgumentException($"Processo desconhecido: {processo}", nameof(processo));
        }

        _processo = processo;
        _repositorio = repositorio
            ?? FabricaControladoresCadastro.CriarToleranciaProcessoProducaoRepositorio();
        _autorizacao = autorizacao
            ?? FabricaControladoresCadastro.CriarAutorizacaoToleranciaProducaoServico;
    }

    /// <summary>Esquece o que foi lido. Chamar ao trocar de ordem, para não carregar cadastro velho.</summary>
    public void Limpar() => _cache.Clear();

    /// <summary>
    /// Esta pesagem pode entrar?
    /// <para>
    /// Devolve <c>true</c> quando está dentro do limite, ou quando um gerente autorizou o estouro.
    /// Qualquer outro desfecho — inclusive dúvida — devolve <c>false</c>.
    /// </para>
    /// </summary>
    /// <param name="totalProjetado">
    /// O total do produto DEPOIS desta pesagem, incluindo o que o SAP já registrou. Não é a pesagem
    /// sozinha: um componente de 100 recebido em 5 pesagens de 20 seria barrado nas cinco se a conta
    /// fosse por pesagem.
    /// </param>
    public async Task<bool> PermitePesagemAsync(
        IWin32Window dono,
        ContextoPesagemProducao contexto,
        decimal totalProjetado,
        Action<string> publicarStatus)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(publicarStatus);

        ToleranciaProcessoProducao? tolerancia;
        try
        {
            tolerancia = await ObterToleranciaAsync(contexto.Centro, contexto.Material);
        }
        catch (Exception erro)
        {
            // Cadastro INDISPONÍVEL não é o mesmo que cadastro AUSENTE. Não saber se existe
            // tolerância não pode parar a produção por causa de uma falha de banco — é a mesma
            // decisão tomada no Recebimento, e a razão é a válvula de saída: guarda fail-closed por
            // indisponibilidade prende quem está trabalhando por um problema que não é dele.
            System.Diagnostics.Trace.TraceWarning(
                $"[Tolerancia] Cadastro indisponivel ({erro.GetType().Name}). Pesagem segue sem o limite.");
            return true;
        }

        VereditoTolerancia veredito = RegraTolerancia.AvaliarDurantePesagem(
            contexto.QuantidadePrevista, totalProjetado, tolerancia);

        if (!veredito.Bloqueia)
        {
            return true;
        }

        string mensagem = RegraTolerancia.Mensagem(contexto.Material, veredito);

        if (veredito.Resultado != ResultadoTolerancia.AcimaDoTeto)
        {
            // FALTA DE CADASTRO não abre o diálogo — decisão do usuário. Cadastrar leva segundos na
            // tela de Tolerância de Produção; deixar o gerente furar esse bloqueio faria o cadastro
            // nunca ser feito, esvaziando a regra que ele mesmo escolheu.
            publicarStatus(mensagem);
            MessageBox.Show(dono, mensagem, TituloDaCaixa, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return await PedirLiberacaoAsync(dono, contexto, veredito, mensagem, publicarStatus);
    }

    private string TituloDaCaixa => $"Tolerância — {ProcessoToleranciaProducao.Descrever(_processo)}";

    private async Task<ToleranciaProcessoProducao?> ObterToleranciaAsync(string centro, string material)
    {
        if (_cache.TryGetValue(material, out ToleranciaProcessoProducao? emCache))
        {
            return emCache;
        }

        ToleranciaProcessoProducao? lida = await _repositorio.ObterAsync(centro, _processo, material);
        _cache[material] = lida;
        return lida;
    }

    private async Task<bool> PedirLiberacaoAsync(
        IWin32Window dono,
        ContextoPesagemProducao contexto,
        VereditoTolerancia veredito,
        string mensagemDoBloqueio,
        Action<string> publicarStatus)
    {
        static string N(decimal? v) => v is null
            ? "-"
            : v.Value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

        ContextoEstouroProducao estouro = new()
        {
            Processo = _processo,
            NumeroOrdem = contexto.NumeroOrdem,
            Operacao = string.IsNullOrWhiteSpace(contexto.Operacao) ? null : contexto.Operacao,
            Material = contexto.Material,
            QuantidadePrevista = veredito.QuantidadePedida,
            QuantidadeApurada = veredito.QuantidadeRecebida,
            LimiteAplicado = veredito.LimiteSuperior,
            PercentualTolerancia = veredito.Percentual,
            TipoEstouro = ContextoEstouroProducao.TipoDoResultado(veredito.Resultado),
            LoginOperador = EstadoSessaoUsuarioAtual.SessaoAtual?.Login,
            CodigoUsuarioOperador = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario,
            Terminal = contexto.Terminal,
        };

        string detalhe = LiberacaoToleranciaDialog.DescreverEstouro(
            contexto.Material,
            new VereditoToleranciaTexto(
                N(veredito.QuantidadePedida), N(veredito.QuantidadeRecebida),
                N(veredito.LimiteSuperior), $"{N(veredito.Percentual)} %"));

        using LiberacaoToleranciaDialog dialogo = new(detalhe);

        // Dono = a janela que está NA FRENTE. A pesagem pode vir de uma janela modal, e um dono atrás
        // dela faria o diálogo nascer inacessível — trocar uma trava por uma armadilha.
        IWin32Window donoReal = Form.ActiveForm ?? dono;

        while (dialogo.ShowDialog(donoReal) == DialogResult.OK)
        {
            ResultadoAutorizacaoTolerancia resultado;
            try
            {
                resultado = await _autorizacao()
                    .AutorizarAsync(dialogo.Usuario, dialogo.Senha, estouro, CancellationToken.None);
            }
            catch (Exception erro)
            {
                // Qualquer falha inesperada NÃO libera: a dúvida mantém o bloqueio.
                System.Diagnostics.Trace.TraceWarning(
                    $"[Tolerancia] Falha ao autorizar: {erro.GetType().Name}");
                resultado = ResultadoAutorizacaoTolerancia.Nao(
                    "Não foi possível validar a credencial agora. A pesagem continua bloqueada.");
            }

            if (resultado.Autorizada)
            {
                publicarStatus(resultado.Mensagem);
                return true;
            }

            dialogo.MostrarErro(resultado.Mensagem);
        }

        publicarStatus(mensagemDoBloqueio + " Liberação não autorizada.");
        return false;
    }
}

/// <summary>
/// Quem está sendo pesado, do ponto de vista da tolerância. As duas telas montam este contexto a
/// partir do que cada uma chama de "previsto" — no Consumo a quantidade prevista do componente, no
/// Semi-Acabado a planejada da ordem.
/// </summary>
internal sealed record ContextoPesagemProducao
{
    public required string Centro { get; init; }
    public required string Material { get; init; }
    public required string NumeroOrdem { get; init; }
    public string? Operacao { get; init; }

    /// <summary>A referência contra a qual o total é comparado. Nulo ou zero = sem o que comparar.</summary>
    public decimal? QuantidadePrevista { get; init; }

    public string? Terminal { get; init; }
}
