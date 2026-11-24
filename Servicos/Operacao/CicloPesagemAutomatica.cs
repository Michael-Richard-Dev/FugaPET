namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Onde o ciclo automático está, em relação à caixa que está na balança.</summary>
public enum EstadoPesagemAutomatica
{
    /// <summary>Balança vazia, esperando a próxima caixa.</summary>
    AguardandoCaixa,

    /// <summary>Há peso, mas ele ainda não ficou parado o bastante.</summary>
    Estabilizando,

    /// <summary>Esta caixa já foi registrada. Só rearma quando a balança voltar ao zero.</summary>
    AguardandoRetirada,
}

/// <summary>
/// Os números que definem o comportamento do ciclo. Ficam juntos num lugar só para poderem ser
/// ajustados sem mexer na lógica.
/// </summary>
public sealed record ConfiguracaoPesagemAutomatica
{
    /// <summary>
    /// Quanto tempo o peso precisa ficar parado para virar pesagem. Decisão do usuário em
    /// 24/11/2026: 2 segundos.
    /// </summary>
    public TimeSpan TempoParado { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Abaixo disto a balança é considerada VAZIA. Não é zero exato de propósito: balança
    /// descarregada oscila em torno do zero, e exigir zero absoluto deixaria o ciclo preso em
    /// "aguardando retirada" para sempre.
    /// </summary>
    public decimal LimiteBalancaVaziaKg { get; init; } = 0.050m;

    /// <summary>
    /// Peso mínimo para valer como caixa. Protege contra registrar a mão do operador encostando na
    /// plataforma, ou um resíduo que ficou em cima dela.
    /// </summary>
    public decimal PesoMinimoKg { get; init; } = 0.200m;

    /// <summary>
    /// Quanto o valor pode variar DENTRO da janela e ainda contar como parado.
    /// <para>
    /// Medida sempre contra a PRIMEIRA leitura da janela, nunca contra a anterior: contra a anterior,
    /// um valor subindo de grama em grama passaria por parado para sempre, somando uma deriva
    /// enorme sem nunca quebrar a janela.
    /// </para>
    /// </summary>
    public decimal VariacaoMaximaKg { get; init; } = 0.005m;
}

/// <summary>O que a tela deve fazer depois de uma leitura.</summary>
/// <param name="Estado">Onde o ciclo ficou.</param>
/// <param name="PesoParaRegistrar">Preenchido SOMENTE na leitura em que a pesagem deve ser gerada.</param>
public sealed record DecisaoPesagemAutomatica(
    EstadoPesagemAutomatica Estado,
    decimal? PesoParaRegistrar,
    string Mensagem)
{
    public bool DeveRegistrar => PesoParaRegistrar.HasValue;
}

/// <summary>
/// O CICLO DA PESAGEM AUTOMÁTICA: observa leituras sucessivas da balança e diz quando uma caixa
/// virou pesagem (pedido do usuário em 24/11/2026).
/// <para>
/// A regra, nas palavras dele: "quando o peso estabilizar sem ser zero gera um peso, quando ir a 0 e
/// aparecer outro peso já gera o peso e assim sucessivamente".
/// </para>
/// <para>
/// <b>Por que uma classe sem balança, sem tela e sem relógio próprio:</b> o momento de cada leitura
/// entra por parâmetro. Assim o comportamento inteiro — inclusive os casos que seriam raros e caros
/// de reproduzir com uma caixa na mão, como a caixa que oscila ou a que é retirada no meio — se
/// prova em teste, em milissegundos. Uma lógica dessas embutida na tela só poderia ser verificada
/// com a balança ligada, que é como defeito de captura passa despercebido.
/// </para>
/// <para>
/// <b>REARMAR É OBRIGATÓRIO E É O CORAÇÃO DA SEGURANÇA.</b> Depois de registrar, o ciclo não aceita
/// outra pesagem enquanto a balança não voltar a ficar vazia. Sem isso, uma caixa parada na
/// plataforma seria registrada repetidamente, a cada janela — e o operador só descobriria pelo
/// estoque errado, depois.
/// </para>
/// </summary>
public sealed class CicloPesagemAutomatica
{
    private readonly ConfiguracaoPesagemAutomatica _configuracao;

    private EstadoPesagemAutomatica _estado = EstadoPesagemAutomatica.AguardandoCaixa;
    private decimal _referenciaDaJanela;
    private DateTimeOffset _inicioDaJanela;
    private bool _janelaAberta;

    public CicloPesagemAutomatica(ConfiguracaoPesagemAutomatica? configuracao = null)
        => _configuracao = configuracao ?? new ConfiguracaoPesagemAutomatica();

    public EstadoPesagemAutomatica Estado => _estado;

    /// <summary>Volta ao início. Usar ao ligar o modo, ao trocar de OP e depois de erro de leitura.</summary>
    public void Reiniciar()
    {
        _estado = EstadoPesagemAutomatica.AguardandoCaixa;
        _janelaAberta = false;
    }

    /// <summary>
    /// Processa UMA leitura da balança e devolve o que fazer.
    /// </summary>
    /// <param name="pesoKg">Peso lido, já convertido para kg.</param>
    /// <param name="momento">Quando a leitura aconteceu. Entra por parâmetro para o ciclo ser testável.</param>
    public DecisaoPesagemAutomatica Observar(decimal pesoKg, DateTimeOffset momento)
    {
        bool balancaVazia = pesoKg < _configuracao.LimiteBalancaVaziaKg;

        if (_estado == EstadoPesagemAutomatica.AguardandoRetirada)
        {
            if (!balancaVazia)
            {
                return Ficar("Retire a caixa para a próxima pesagem.");
            }

            _estado = EstadoPesagemAutomatica.AguardandoCaixa;
            _janelaAberta = false;
            return Ficar("Balança livre. Pode colocar a próxima caixa.");
        }

        if (balancaVazia)
        {
            // Voltar ao vazio cancela qualquer janela em andamento: a caixa saiu antes de parar.
            _janelaAberta = false;
            _estado = EstadoPesagemAutomatica.AguardandoCaixa;
            return Ficar("Aguardando caixa.");
        }

        // Há peso, mas pequeno demais para ser caixa: não abre janela nem registra.
        if (pesoKg < _configuracao.PesoMinimoKg)
        {
            _janelaAberta = false;
            _estado = EstadoPesagemAutomatica.AguardandoCaixa;
            return Ficar(
                $"Peso abaixo do mínimo de {Formatar(_configuracao.PesoMinimoKg)} kg. Aguardando caixa.");
        }

        _estado = EstadoPesagemAutomatica.Estabilizando;

        if (!_janelaAberta || Math.Abs(pesoKg - _referenciaDaJanela) > _configuracao.VariacaoMaximaKg)
        {
            // Primeira leitura com peso, ou o valor saiu da faixa: a janela recomeça AGORA.
            _janelaAberta = true;
            _referenciaDaJanela = pesoKg;
            _inicioDaJanela = momento;
            return Ficar("Estabilizando...");
        }

        if (momento - _inicioDaJanela < _configuracao.TempoParado)
        {
            return Ficar("Estabilizando...");
        }

        // Parou pelo tempo exigido: esta é a pesagem.
        //
        // Registra a leitura ATUAL, e nao a referencia da janela: dentro da faixa as duas sao
        // equivalentes, e a atual e a que o operador ve no visor da balanca neste instante.
        _estado = EstadoPesagemAutomatica.AguardandoRetirada;
        _janelaAberta = false;

        return new DecisaoPesagemAutomatica(
            _estado,
            pesoKg,
            $"Pesagem registrada: {Formatar(pesoKg)} kg. Retire a caixa.");
    }

    private DecisaoPesagemAutomatica Ficar(string mensagem) => new(_estado, null, mensagem);

    private static string Formatar(decimal valor)
        => valor.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
