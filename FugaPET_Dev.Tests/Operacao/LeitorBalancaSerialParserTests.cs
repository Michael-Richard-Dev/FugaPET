using System.Globalization;
using FugaPET_Dev.Servicos;

namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// Correção da escala decimal da leitura serial: o divisor passa a depender do PROTOCOLO da balança.
/// P03 divide por 100 (000165 → 1,65). Protocolos vazios/diferentes mantêm o divisor legado 10 (compatibilidade
/// temporária). A conversão é única (ConverterValorBrutoParaKg) e usada pelos dois caminhos do leitor.
/// </summary>
public sealed class LeitorBalancaSerialParserTests
{
    // ---- P03: divisor 100 ----

    [Theory]
    [InlineData(165, 1.65)]
    [InlineData(50, 0.50)]
    [InlineData(1000, 10.00)]
    [InlineData(1650, 16.50)]
    [InlineData(10000, 100.00)]
    public void ConverterValorBrutoParaKg_P03_DividePor100(int bruto, double esperado)
        => Assert.Equal((decimal)esperado, LeitorBalancaSerialServico.ConverterValorBrutoParaKg(bruto, "P03"));

    [Theory]
    [InlineData("P03")]
    [InlineData("p03")]
    [InlineData("  P03  ")]
    public void ConverterValorBrutoParaKg_P03_NormalizaProtocolo(string protocolo)
        => Assert.Equal(1.65m, LeitorBalancaSerialServico.ConverterValorBrutoParaKg(165, protocolo));

    // ---- Fallback legado: divisor 10 (compatibilidade temporária) ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("P02")]
    [InlineData("OUTRO")]
    public void ConverterValorBrutoParaKg_ProtocoloVazioOuDiferente_MantemDivisor10(string? protocolo)
        => Assert.Equal(16.5m, LeitorBalancaSerialServico.ConverterValorBrutoParaKg(165, protocolo!));

    // ---- FormatarPeso (string pt-BR) ----

    [Theory]
    [InlineData(165, "P03", "1,65")]
    [InlineData(50, "P03", "0,50")]
    [InlineData(10000, "P03", "100,00")]
    [InlineData(165, "", "16,50")] // fallback legado
    public void FormatarPeso_UsaEscalaDoProtocolo(int bruto, string protocolo, string esperado)
        => Assert.Equal(esperado, LeitorBalancaSerialServico.FormatarPeso(bruto, protocolo));

    // ---- Quadro (12 dígitos; 6 primeiros = valor bruto) ----

    [Fact]
    public void TentarExtrairValorBrutoEstavel_QuadroDe12Digitos_UsaSeisPrimeiros()
    {
        int? bruto = LeitorBalancaSerialServico.TentarExtrairValorBrutoEstavel("000165000000\r\n");
        Assert.Equal(165, bruto);
    }

    [Fact]
    public void TentarExtrairPeso_QuadroComP03_Retorna165()
    {
        string? peso = LeitorBalancaSerialServico.TentarExtrairPeso("000165000000\r\n", "P03");
        Assert.Equal("1,65", peso);
    }

    [Fact]
    public void TentarExtrairPeso_MesmoQuadroDuasVezes_ContinuaEstavel()
    {
        // Duas leituras iguais no buffer continuam produzindo o valor estável (000165 -> 1,65 no P03).
        string? peso = LeitorBalancaSerialServico.TentarExtrairPeso("000165000000\r\n000165000000\r\n", "P03");
        Assert.Equal("1,65", peso);
    }

    [Fact]
    public void TentarExtrairPeso_CaminhoFinalDeBuffer_UsaMesmaEscalaP03()
    {
        // O caminho final de buffer (timeout) usa a mesma conversão por protocolo: 001650 -> 16,50 no P03.
        string? peso = LeitorBalancaSerialServico.TentarExtrairPeso("001650000000\r\n", "P03");
        Assert.Equal("16,50", peso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem digitos aqui")]
    [InlineData("12345")]        // menos de 12 dígitos
    [InlineData("abcdefghijkl")] // não numérico
    public void TentarExtrairPeso_QuadroInvalido_RetornaNull(string texto)
        => Assert.Null(LeitorBalancaSerialServico.TentarExtrairPeso(texto, "P03"));

    [Fact]
    public void TentarExtrairValorBrutoEstavel_QuadroInvalido_RetornaNull()
        => Assert.Null(LeitorBalancaSerialServico.TentarExtrairValorBrutoEstavel("nao ha 12 digitos"));

    [Fact]
    public void Conversao_EhCentavosParaP03_Independente_DeCultura()
    {
        // Garante que o divisor é 100 (valor decimal), não dependente de formatação.
        decimal peso = LeitorBalancaSerialServico.ConverterValorBrutoParaKg(165, "P03");
        Assert.Equal(1.65m, peso);
        Assert.Equal("1.65", peso.ToString(CultureInfo.InvariantCulture));
    }
}
