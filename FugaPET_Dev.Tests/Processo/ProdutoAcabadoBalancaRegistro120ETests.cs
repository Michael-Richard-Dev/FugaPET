using System.Text.Json;
using System.Reflection;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Diagnostico;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Npgsql;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ProdutoAcabadoBalancaRegistro120ETests
{
    private static ProdutoAcabadoOrdem Ordem() => new()
    {
        NumeroOrdem = "1000242", ItemOrdem = "1", MaterialProduzido = "4000174",
        Lote = "169 26", Centro = "3007", DepositoDestino = "PA01"
    };

    private static ProdutoAcabadoNormaEmbalagem Norma() => new()
    {
        QuantidadeProdutosPorCaixa = 8, Unidade = "UN", MaterialCaixa = "3000046"
    };

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void Balanca_MontagemPreservaCodigoRealEPesos(long codigo)
    {
        var controller = new ProdutoAcabadoController();
        var leitura = ResultadoLeituraPeso.Ok("14.400") with { CodigoBalanca = codigo };
        ProdutoAcabadoCaixa caixa = controller.MontarCaixaSemIdentidadeSequencial(
            Ordem(), Norma(), 14.400m, .050m, "BALANCA", codigoUsuario: 1,
            codigoBalanca: leitura.CodigoBalanca);
        Assert.Equal(codigo, caixa.CodigoBalanca);
        Assert.Equal(14.400m, caixa.PesoBrutoKg);
        Assert.Equal(.050m, caixa.TaraKg);
        Assert.Equal(14.350m, caixa.PesoLiquidoKg);
        Assert.Equal(8, caixa.QuantidadeProdutos);
        Assert.Equal("BALANCA", caixa.OrigemPesagem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public async Task Balanca_SemCodigo_BloqueiaAntesDoServico(long? codigo)
    {
        var etapas = new List<EtapaRegistroCaixa120E>();
        var controller = new ProdutoAcabadoController();
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.FinalizarCaixaLocalAsync(Ordem(), Norma(), 14.400m, .050m,
                "BALANCA", "TERMINAL_TESTE", codigoUsuario: 1, codigoBalanca: codigo,
                diagnostico: etapas.Add));
        Assert.Contains("codigo_balanca", erro.Message);
        Assert.Equal(new[] { EtapaRegistroCaixa120E.MONTAR_CAIXA }, etapas);
    }

    [Fact]
    public void Manual_PreservaAusenciaDeBalancaEPesos()
    {
        var controller = new ProdutoAcabadoController();
        var caixa = controller.MontarCaixaSemIdentidadeSequencial(
            Ordem(), Norma(), 14.400m, .050m, "MANUAL", codigoBalanca: 42);
        Assert.Null(caixa.CodigoBalanca);
        Assert.Equal(14.350m, caixa.PesoLiquidoKg);
        Assert.Equal(8, caixa.QuantidadeProdutos);
    }

    [Theory]
    [InlineData("BALANCA", 1L)]
    [InlineData("MANUAL", null)]
    public async Task Service_TransportaIdentidadeParaPesagemEIdentificaEtapa(string origem, long? codigo)
    {
        var repositorio = DispatchProxy.Create<IProdutoAcabadoRepositorio, RepositorioProxy>();
        var proxy = (RepositorioProxy)repositorio;
        var gateway = DispatchProxy.Create<IProdutoAcabadoHandlingUnitSapServico, RepositorioProxy>();
        var service = new ProdutoAcabadoHuService(repositorio, gateway);
        var caixa = new ProdutoAcabadoController().MontarCaixaSemIdentidadeSequencial(
            Ordem(), Norma(), 14.400m, .050m, origem, codigoUsuario: 1, codigoBalanca: codigo);
        var etapas = new List<EtapaRegistroCaixa120E>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegistrarEFinalizarCaixaAsync(caixa, 1, "TESTE", diagnostico: etapas.Add));

        Assert.Equal(codigo, proxy.Pesagem!.CodigoBalanca);
        Assert.Equal(origem, proxy.Pesagem.OrigemPesagem);
        Assert.Equal(14.400m, proxy.Pesagem.PesoBruto);
        Assert.Equal(.050m, proxy.Pesagem.PesoTara);
        Assert.Equal(14.350m, proxy.Pesagem.PesoLiquido);
        Assert.Equal(EtapaRegistroCaixa120E.INSERIR_PESAGEM, etapas.Last());
        Assert.Contains(EtapaRegistroCaixa120E.INSERIR_CAIXA, etapas);
    }

    public class RepositorioProxy : DispatchProxy
    {
        public RegistroPesagemHuCaixa? Pesagem { get; private set; }

        protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos)
        {
            switch (metodo!.Name)
            {
                case "ObterAtivaPorTerminalAsync":
                // 068: o registro passou a perguntar pela caixa que BLOQUEIA, dado o processo. Sem este
                // caso o proxy caía no default e abortava antes da pesagem, que é o que este teste mede.
                case "ObterBloqueadoraPorTerminalAsync":
                    return Task.FromResult<ProdutoAcabadoCaixa?>(null);
                case "RegistrarCaixaAsync":
                    var caixa = (ProdutoAcabadoCaixa)argumentos![0]!;
                    caixa.CodigoProdutoAcabadoCaixa = 1;
                    return Task.FromResult(caixa);
                case "RegistrarPesagemAsync":
                    Pesagem = (RegistroPesagemHuCaixa)argumentos![0]!;
                    return Task.FromException<long>(new InvalidOperationException("FALHA_SINTETICA_PESAGEM"));
                default:
                    throw new InvalidOperationException("Chamada inesperada no fake: " + metodo.Name);
            }
        }
    }

    [Fact]
    public void Diagnostico_PreservaStageEPostgresMetadataIncluindoInner()
    {
        var postgres = new PostgresException("falha de integridade", "ERROR", "ERROR", "23514",
            tableName: "hu_caixa", columnName: "peso_bruto", constraintName: "ck_hu_caixa_peso_bruto");
        string linha = RegistroCaixaDiag120E.Formatar(EtapaRegistroCaixa120E.INSERIR_CAIXA,
            new NpgsqlException("falha externa", postgres));
        using var json = JsonDocument.Parse(linha);
        Assert.Equal("INSERIR_CAIXA", json.RootElement.GetProperty("Stage").GetString());
        var inner = json.RootElement.GetProperty("Exceptions")[1];
        Assert.Equal("Npgsql.PostgresException", inner.GetProperty("ExceptionType").GetString());
        Assert.Equal("23514", inner.GetProperty("SqlState").GetString());
        Assert.Equal("ck_hu_caixa_peso_bruto", inner.GetProperty("ConstraintName").GetString());
        Assert.Equal("hu_caixa", inner.GetProperty("TableName").GetString());
        Assert.Equal("peso_bruto", inner.GetProperty("ColumnName").GetString());
    }

    [Theory]
    [InlineData("Password=supersegredo; falha")]
    [InlineData("Host=db;Username=app;Password=supersegredo")]
    [InlineData("Authorization: Bearer supersegredo")]
    public void Diagnostico_NaoPersisteSegredos(string mensagem)
    {
        string linha = RegistroCaixaDiag120E.Formatar(EtapaRegistroCaixa120E.INSERIR_PESAGEM,
            new InvalidOperationException(mensagem));
        Assert.DoesNotContain("supersegredo", linha);
    }
}
