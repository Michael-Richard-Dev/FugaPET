using System.Globalization;
using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>Por que a tolerancia aprovou ou barrou. O motivo e parte do resultado, nao um detalhe de log.</summary>
public enum ResultadoTolerancia
{
    /// <summary>Dentro dos limites (ou sem o que comparar).</summary>
    Dentro,

    /// <summary>Passou do teto. E o unico caso verificavel DURANTE a pesagem.</summary>
    AcimaDoTeto,

    /// <summary>Ficou abaixo do piso. So faz sentido na FINALIZACAO — ver o comentario da classe.</summary>
    AbaixoDoPiso,

    /// <summary>Material sem tolerancia ativa cadastrada.</summary>
    SemCadastro,
}

/// <summary>O veredito, com os numeros que o justificam — para a mensagem poder ser especifica.</summary>
public sealed record VereditoTolerancia(
    ResultadoTolerancia Resultado,
    decimal QuantidadePedida,
    decimal QuantidadeRecebida,
    decimal? LimiteInferior,
    decimal? LimiteSuperior,
    decimal? Percentual)
{
    public bool Bloqueia => Resultado != ResultadoTolerancia.Dentro;
}

/// <summary>
/// A matematica da tolerancia de recebimento, num lugar so e sem banco, rede ou tela.
///
/// <para><b>A regra</b>, como o usuario a definiu em 23/11/2026: o total pesado de um item precisa
/// ficar entre <c>pedido - X%</c> e <c>pedido + X%</c>. Exemplo dele: pedido 100 com 10% aceita de 90 a
/// 110; 112 e bloqueio.</para>
///
/// <para><b>Contra o TOTAL do item, nao contra a pesagem.</b> Um item e recebido em varias pesagens —
/// a Entrada acumula <c>PesoLiquidoTotalKg</c> somando-as. Se a tolerancia valesse por pesagem isolada,
/// um item de 100 recebido em 5 pesagens de 20 seria barrado nas cinco, porque nenhuma delas chega
/// perto de 100.</para>
///
/// <para><b>Peso LIQUIDO</b>, escolha do usuario: e o que o sistema grava como <c>QuantidadeRecebida</c>
/// e envia ao SAP. O pedido e de material, nao de embalagem — 100 kg pedidos com 2 kg de tara dariam
/// 102 no bruto e pareceriam excesso que nao existe.</para>
///
/// <para><b>Os dois lados sao verificados em MOMENTOS diferentes, e isso nao e simetria quebrada por
/// descuido.</b> O teto pode ser checado a cada pesagem, porque ultrapassa-lo e definitivo: peso
/// registrado nao volta atras. O piso NAO pode: todo item comeca em zero, que esta abaixo de qualquer
/// piso, entao checa-lo durante a pesagem barraria a PRIMEIRA pesagem de todo recebimento. Falta
/// quantidade so e um fato quando o usuario declara que terminou. Por isso
/// <see cref="AvaliarDurantePesagem"/> ignora o piso e <see cref="AvaliarNaFinalizacao"/> cobra os dois.</para>
///
/// <para><b>Sem cadastro = bloqueio</b> (decisao do usuario em 23/11/2026, contra a recomendacao de
/// seguir liberado). Tolerancia INATIVA conta como nao cadastrada, e nao como "sem limite": desativar
/// uma linha nao pode ser um jeito silencioso de liberar o material. Quem bloqueia por aqui tem o dever
/// de dizer QUAL material falta — ver [[guarda-sem-valvula-de-saida]]: trava sem caminho de liberacao
/// ja prendeu a operacao antes.</para>
/// </summary>
public static class RegraTolerancia
{
    /// <summary>
    /// Avalia o que acontece com o item DURANTE a pesagem. So o teto e cobrado aqui — ver a classe.
    /// </summary>
    public static VereditoTolerancia AvaliarDurantePesagem(
        decimal? quantidadePedida,
        decimal quantidadeRecebida,
        IToleranciaPercentual? tolerancia)
        => Avaliar(quantidadePedida, quantidadeRecebida, tolerancia, cobrarPiso: false);

    /// <summary>
    /// Avalia o item na FINALIZACAO do recebimento, quando falta quantidade ja e um fato. Cobra os dois
    /// lados.
    /// </summary>
    public static VereditoTolerancia AvaliarNaFinalizacao(
        decimal? quantidadePedida,
        decimal quantidadeRecebida,
        IToleranciaPercentual? tolerancia)
        => Avaliar(quantidadePedida, quantidadeRecebida, tolerancia, cobrarPiso: true);

    private static VereditoTolerancia Avaliar(
        decimal? quantidadePedida,
        decimal quantidadeRecebida,
        IToleranciaPercentual? tolerancia,
        bool cobrarPiso)
    {
        decimal pedida = quantidadePedida ?? 0m;

        if (tolerancia is null || !tolerancia.Situacao)
        {
            return new VereditoTolerancia(
                ResultadoTolerancia.SemCadastro, pedida, quantidadeRecebida, null, null, null);
        }

        // Pedido sem quantidade nao da o que comparar: sem o 100 do exemplo, nao existe 90 nem 110.
        // Inventar um limite aqui seria barrar por um numero que ninguem informou.
        if (quantidadePedida is null || quantidadePedida.Value <= 0m)
        {
            return new VereditoTolerancia(
                ResultadoTolerancia.Dentro, pedida, quantidadeRecebida, null, null, tolerancia.PercentualTolerancia);
        }

        decimal fator = tolerancia.PercentualTolerancia / 100m;
        decimal folga = pedida * fator;
        decimal piso = pedida - folga;
        decimal teto = pedida + folga;

        if (quantidadeRecebida > teto)
        {
            return new VereditoTolerancia(
                ResultadoTolerancia.AcimaDoTeto, pedida, quantidadeRecebida, piso, teto, tolerancia.PercentualTolerancia);
        }

        if (cobrarPiso && quantidadeRecebida < piso)
        {
            return new VereditoTolerancia(
                ResultadoTolerancia.AbaixoDoPiso, pedida, quantidadeRecebida, piso, teto, tolerancia.PercentualTolerancia);
        }

        return new VereditoTolerancia(
            ResultadoTolerancia.Dentro, pedida, quantidadeRecebida, piso, teto, tolerancia.PercentualTolerancia);
    }

    /// <summary>
    /// Mensagem para o operador. Sempre com OS NUMEROS: um bloqueio que so diz "fora da tolerancia"
    /// obriga quem esta no chao de fabrica a adivinhar de quanto, e a reacao vira tentativa e erro na
    /// balanca.
    /// </summary>
    public static string Mensagem(string material, VereditoTolerancia veredito)
    {
        ArgumentNullException.ThrowIfNull(veredito);
        string item = string.IsNullOrWhiteSpace(material) ? "do item" : $"do material {material.Trim()}";

        return veredito.Resultado switch
        {
            ResultadoTolerancia.SemCadastro =>
                $"O material {material.Trim()} não tem tolerância de recebimento cadastrada. "
                + "Cadastre em Cadastros › Tolerância de Recebimento para liberar a pesagem.",

            ResultadoTolerancia.AcimaDoTeto =>
                $"Pesagem {item} bloqueada: {N(veredito.QuantidadeRecebida)} excede o limite de "
                + $"{N(veredito.LimiteSuperior)} (pedido {N(veredito.QuantidadePedida)} com tolerância de "
                + $"{N(veredito.Percentual)} %).",

            ResultadoTolerancia.AbaixoDoPiso =>
                $"Recebimento {item} bloqueado: {N(veredito.QuantidadeRecebida)} está abaixo do mínimo de "
                + $"{N(veredito.LimiteInferior)} (pedido {N(veredito.QuantidadePedida)} com tolerância de "
                + $"{N(veredito.Percentual)} %).",

            _ => string.Empty,
        };
    }

    private static string N(decimal? valor)
        => valor is null ? "-" : valor.Value.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
}
