using FugaPET_Dev.Servicos.Operacao;
using Xunit;

namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// O CICLO DA PESAGEM AUTOMÁTICA (pedido do usuário em 24/11/2026): "quando o peso estabilizar sem
/// ser zero gera um peso, quando ir a 0 e aparecer outro peso já gera o peso e assim sucessivamente".
/// <para>
/// Tudo aqui roda sem balança e sem tela, porque o momento de cada leitura entra por parâmetro. É o
/// que permite provar os casos que seriam caros de reproduzir com uma caixa na mão — a que oscila, a
/// que é retirada no meio, a que fica parada na plataforma.
/// </para>
/// </summary>
public sealed class CicloPesagemAutomaticaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 14, 0, 0, TimeSpan.Zero);

    private static ConfiguracaoPesagemAutomatica Config(double segundosParado = 2) => new()
    {
        TempoParado = TimeSpan.FromSeconds(segundosParado),
        LimiteBalancaVaziaKg = 0.050m,
        PesoMinimoKg = 0.200m,
        VariacaoMaximaKg = 0.005m,
    };

    private static DateTimeOffset Em(double segundos) => T0.AddSeconds(segundos);

    // ---------------------------------------------------------------- caminho feliz

    /// <summary>Caixa entra, para, e vira pesagem quando completa o tempo exigido.</summary>
    [Fact]
    public void CaixaParada_PeloTempoExigido_ViraPesagem()
    {
        CicloPesagemAutomatica ciclo = new(Config());

        Assert.False(ciclo.Observar(12.500m, Em(0)).DeveRegistrar);     // abre a janela
        Assert.False(ciclo.Observar(12.500m, Em(1)).DeveRegistrar);     // ainda nao deu 2s
        Assert.False(ciclo.Observar(12.500m, Em(1.999)).DeveRegistrar); // um triz antes

        DecisaoPesagemAutomatica decisao = ciclo.Observar(12.500m, Em(2));

        Assert.True(decisao.DeveRegistrar);
        Assert.Equal(12.500m, decisao.PesoParaRegistrar);
        Assert.Equal(EstadoPesagemAutomatica.AguardandoRetirada, decisao.Estado);
    }

    /// <summary>Caixa após caixa, que é o pedido: retira, coloca outra, gera de novo.</summary>
    [Fact]
    public void CaixaAposCaixa_GeraUmaPesagemParaCada()
    {
        CicloPesagemAutomatica ciclo = new(Config());
        List<decimal> registradas = [];

        void Passar(decimal peso, double de, double ate)
        {
            for (double s = de; s <= ate; s += 0.5)
            {
                DecisaoPesagemAutomatica d = ciclo.Observar(peso, Em(s));
                if (d.PesoParaRegistrar is decimal p) { registradas.Add(p); }
            }
        }

        Passar(10.000m, 0, 3);      // primeira caixa
        Passar(0.010m, 3.5, 4);     // retirada
        Passar(11.000m, 4.5, 7.5);  // segunda caixa
        Passar(0.010m, 8, 8.5);     // retirada
        Passar(12.000m, 9, 12);     // terceira caixa

        Assert.Equal([10.000m, 11.000m, 12.000m], registradas);
    }

    // ---------------------------------------------------------------- o coração da segurança

    /// <summary>
    /// O PONTO MAIS IMPORTANTE. Caixa esquecida na plataforma NÃO vira pesagem atrás de pesagem. Sem
    /// o rearme, cada janela de 2 segundos geraria uma caixa nova, e o operador só descobriria pelo
    /// estoque errado, depois.
    /// </summary>
    [Fact]
    public void CaixaEsquecidaNaPlataforma_RegistraUmaVezSo()
    {
        CicloPesagemAutomatica ciclo = new(Config());
        int registros = 0;

        for (double s = 0; s <= 60; s += 0.5)
        {
            if (ciclo.Observar(15.000m, Em(s)).DeveRegistrar) { registros++; }
        }

        Assert.Equal(1, registros);
    }

    /// <summary>Só volta a aceitar depois que a balança fica vazia de verdade.</summary>
    [Fact]
    public void DepoisDeRegistrar_SoRearmaComABalancaVazia()
    {
        CicloPesagemAutomatica ciclo = new(Config());
        for (double s = 0; s <= 2; s += 0.5) { ciclo.Observar(9.000m, Em(s)); }
        Assert.Equal(EstadoPesagemAutomatica.AguardandoRetirada, ciclo.Estado);

        // Peso cai, mas nao o bastante: continua travado.
        ciclo.Observar(0.300m, Em(3));
        Assert.Equal(EstadoPesagemAutomatica.AguardandoRetirada, ciclo.Estado);

        ciclo.Observar(0.010m, Em(4));
        Assert.Equal(EstadoPesagemAutomatica.AguardandoCaixa, ciclo.Estado);
    }

    // ---------------------------------------------------------------- o que NÃO pode virar pesagem

    /// <summary>
    /// Caixa descendo passa por valores repetidos por acaso. Com a janela de 2 segundos medida
    /// contra o tempo, isso não registra — era o risco de usar o critério de hoje do leitor, que se
    /// contenta com dois quadros iguais a 30 ms de distância.
    /// </summary>
    [Fact]
    public void PesoEmMovimento_NaoViraPesagem()
    {
        CicloPesagemAutomatica ciclo = new(Config());
        decimal[] descendo = [3.000m, 3.000m, 7.400m, 7.400m, 11.900m, 11.900m, 12.480m];

        for (int i = 0; i < descendo.Length; i++)
        {
            Assert.False(ciclo.Observar(descendo[i], Em(i * 0.2)).DeveRegistrar);
        }
    }

    /// <summary>
    /// Deriva lenta NÃO conta como parado. A faixa é medida contra a PRIMEIRA leitura da janela; se
    /// fosse contra a anterior, um valor subindo de grama em grama passaria por parado para sempre.
    /// </summary>
    [Fact]
    public void DerivaLenta_NaoContaComoParado()
    {
        CicloPesagemAutomatica ciclo = new(Config());
        decimal peso = 10.000m;

        for (double s = 0; s <= 10; s += 0.5)
        {
            peso += 0.004m;   // dentro da faixa em relacao a leitura ANTERIOR, nunca a primeira
            Assert.False(ciclo.Observar(peso, Em(s)).DeveRegistrar);
        }
    }

    /// <summary>Oscilação dentro da faixa é parada de verdade: a balança jamais repete o grama exato.</summary>
    [Fact]
    public void OscilacaoDentroDaFaixa_ContaComoParado()
    {
        CicloPesagemAutomatica ciclo = new(Config());

        ciclo.Observar(20.000m, Em(0));
        ciclo.Observar(20.003m, Em(0.5));
        ciclo.Observar(19.998m, Em(1.0));
        ciclo.Observar(20.002m, Em(1.5));

        DecisaoPesagemAutomatica decisao = ciclo.Observar(20.001m, Em(2.0));

        Assert.True(decisao.DeveRegistrar);
        Assert.Equal(20.001m, decisao.PesoParaRegistrar);   // a leitura atual, a que esta no visor
    }

    /// <summary>Caixa retirada antes de completar o tempo não registra nada.</summary>
    [Fact]
    public void CaixaRetiradaAntesDoTempo_NaoRegistra()
    {
        CicloPesagemAutomatica ciclo = new(Config());

        ciclo.Observar(8.000m, Em(0));
        ciclo.Observar(8.000m, Em(1));
        Assert.False(ciclo.Observar(0.010m, Em(1.5)).DeveRegistrar);
        Assert.Equal(EstadoPesagemAutomatica.AguardandoCaixa, ciclo.Estado);

        // E a janela recomeca do zero quando a proxima caixa entra.
        ciclo.Observar(8.000m, Em(2));
        Assert.False(ciclo.Observar(8.000m, Em(3)).DeveRegistrar);
        Assert.True(ciclo.Observar(8.000m, Em(4)).DeveRegistrar);
    }

    /// <summary>
    /// Peso pequeno — mão encostando na plataforma, resíduo — não vira caixa, por mais tempo que
    /// fique parado.
    /// </summary>
    [Fact]
    public void PesoAbaixoDoMinimo_NuncaViraPesagem()
    {
        CicloPesagemAutomatica ciclo = new(Config());

        for (double s = 0; s <= 30; s += 0.5)
        {
            Assert.False(ciclo.Observar(0.150m, Em(s)).DeveRegistrar);
        }
    }

    /// <summary>Balança vazia o tempo todo não gera nada e não trava.</summary>
    [Fact]
    public void BalancaVazia_NaoGeraNada()
    {
        CicloPesagemAutomatica ciclo = new(Config());

        for (double s = 0; s <= 30; s += 0.5)
        {
            DecisaoPesagemAutomatica d = ciclo.Observar(0.004m, Em(s));
            Assert.False(d.DeveRegistrar);
            Assert.Equal(EstadoPesagemAutomatica.AguardandoCaixa, d.Estado);
        }
    }

    // ---------------------------------------------------------------- reinício

    /// <summary>
    /// Reiniciar limpa a janela. Usado ao ligar o modo, ao trocar de OP e depois de erro de leitura —
    /// casos em que continuar de onde parou misturaria duas situações diferentes.
    /// </summary>
    [Fact]
    public void Reiniciar_DescartaAJanelaEmAndamento()
    {
        CicloPesagemAutomatica ciclo = new(Config());

        ciclo.Observar(10.000m, Em(0));
        ciclo.Observar(10.000m, Em(1.9));
        ciclo.Reiniciar();

        // Sem o reinicio, a leitura em 2s completaria a janela aberta em 0s.
        Assert.False(ciclo.Observar(10.000m, Em(2)).DeveRegistrar);
        Assert.True(ciclo.Observar(10.000m, Em(4)).DeveRegistrar);
    }

    /// <summary>O tempo parado é configurável: quem muda o número não mexe na lógica.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void TempoParado_ERespeitadoComoConfigurado(double segundos)
    {
        CicloPesagemAutomatica ciclo = new(Config(segundos));

        ciclo.Observar(7.000m, Em(0));
        Assert.False(ciclo.Observar(7.000m, Em(segundos - 0.01)).DeveRegistrar);
        Assert.True(ciclo.Observar(7.000m, Em(segundos)).DeveRegistrar);
    }

    /// <summary>O padrão é o que o usuário pediu: 2 segundos.</summary>
    [Fact]
    public void PadraoDeFabrica_EDoisSegundos()
        => Assert.Equal(TimeSpan.FromSeconds(2), new ConfiguracaoPesagemAutomatica().TempoParado);
}
