using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// A TRAVA DE TOLERÂNCIA DA PRODUÇÃO ESTÁ NO CAMINHO VIVO DAS DUAS TELAS (incremento 071).
/// <para>
/// Estes testes existem por causa de 24/11/2026, quando a trava do Recebimento passou um dia inteiro
/// publicada sem bloquear nada: morava num método que ninguém mais chamava, e depois faltava num
/// terceiro caminho de pesagem. A lição virou regra — não se verifica que "a trava existe", e sim
/// que TODO caminho de pesagem passa por ela.
/// </para>
/// </summary>
public sealed class GuardaToleranciaProducaoTests
{
    private static string Consumo() => Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
    private static string SemiAcabado() => Fonte("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
    private static string Guarda() => Fonte("Tela", "Processo", "GuardaToleranciaProducao.cs");

    /// <summary>
    /// UMA implementação para as duas telas. Duas cópias divergem no dia em que uma for corrigida e
    /// a outra não — que é exatamente como o defeito do Recebimento nasceu.
    /// </summary>
    [Fact]
    public void AsDuasTelas_UsamAMesmaGuarda()
    {
        Assert.Contains("new(Modelo.Cadastro.ProcessoToleranciaProducao.Consumo)", Consumo(), StringComparison.Ordinal);
        Assert.Contains("new(Modelo.Cadastro.ProcessoToleranciaProducao.SemiAcabado)", SemiAcabado(), StringComparison.Ordinal);

        Assert.Contains("_guardaTolerancia.PermitePesagemAsync(", Consumo(), StringComparison.Ordinal);
        Assert.Contains("_guardaTolerancia.PermitePesagemAsync(", SemiAcabado(), StringComparison.Ordinal);
    }

    /// <summary>
    /// TODA chamada de registro de pesagem tem a trava antes dela. É o teste que teria pego o defeito
    /// do Recebimento, aplicado preventivamente aqui.
    /// </summary>
    [Theory]
    [InlineData("Consumo", "_controller.RegistrarPesagemConsumo(")]
    [InlineData("SemiAcabado", "pesagens.Add(pesagem);")]
    public void TodoRegistroDePesagem_TemATravaAntes(string tela, string ancoraDoRegistro)
    {
        string fonte = tela == "Consumo" ? Consumo() : SemiAcabado();
        const string trava = "_guardaTolerancia.PermitePesagemAsync(";

        int ondeRegistra = fonte.IndexOf(ancoraDoRegistro, StringComparison.Ordinal);
        Assert.True(ondeRegistra >= 0, $"ancora de registro nao encontrada em {tela}");

        int ondeTrava = fonte.LastIndexOf(trava, ondeRegistra, StringComparison.Ordinal);
        Assert.True(ondeTrava >= 0, $"a trava nao aparece antes do registro em {tela}");
        Assert.True(
            ondeRegistra - ondeTrava < 3000,
            $"a trava esta longe demais do registro em {tela} — pode ser de outro trecho");
    }

    /// <summary>
    /// O teto absoluto do Semi-Acabado foi REMOVIDO, não deixado sem uso. Guarda sem chamador engana
    /// quem lê: parece proteção e não é.
    /// </summary>
    [Fact]
    public void TetoAbsolutoDoSemiAcabado_FoiRemovido_NaoApenasDesligado()
    {
        string fonte = SemiAcabado();

        Assert.DoesNotContain(
            "private static bool ValidarSaldoSemiAcabado(", fonte, StringComparison.Ordinal);
        Assert.Contains("REMOVIDO em 24/11/2026", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// Só o TETO é cobrado durante a pesagem. Cobrar o piso aqui barraria a PRIMEIRA pesagem de toda
    /// ordem, porque todo componente começa em zero — e nenhuma das duas telas declara a ordem
    /// concluída, que é o momento em que faltar viraria fato.
    /// </summary>
    [Fact]
    public void DuranteAPesagem_SoOTetoECobrado()
    {
        string guarda = Guarda();

        Assert.Contains("RegraTolerancia.AvaliarDurantePesagem(", guarda, StringComparison.Ordinal);
        Assert.DoesNotContain("AvaliarNaFinalizacao", guarda, StringComparison.Ordinal);
    }

    /// <summary>
    /// Falta de cadastro NÃO abre o diálogo do gerente — decisão do usuário, para a liberação não
    /// virar caminho permanente de não cadastrar. Só estouro de teto é liberável.
    /// </summary>
    [Fact]
    public void FaltaDeCadastro_NaoAbreODialogo()
    {
        string guarda = Guarda();

        int ondeChecaTeto = guarda.IndexOf(
            "if (veredito.Resultado != ResultadoTolerancia.AcimaDoTeto)", StringComparison.Ordinal);
        Assert.True(ondeChecaTeto >= 0, "o desvio que separa estouro de falta de cadastro sumiu");

        int ondePedeLiberacao = guarda.IndexOf("PedirLiberacaoAsync(", ondeChecaTeto, StringComparison.Ordinal);
        Assert.True(ondePedeLiberacao > ondeChecaTeto, "a liberacao deixou de ser exclusiva do estouro de teto");
    }

    /// <summary>
    /// Cadastro INDISPONÍVEL não é cadastro AUSENTE: falha de banco não pode parar a produção. É a
    /// válvula de saída que faltou no incidente 063 e que quase repetimos hoje.
    /// </summary>
    [Fact]
    public void CadastroIndisponivel_NaoParaAProducao()
    {
        string guarda = Guarda();

        Assert.Contains("Pesagem segue sem o limite", guarda, StringComparison.Ordinal);
        Assert.Contains("catch (Exception erro)", guarda, StringComparison.Ordinal);
    }

    private static string Fonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
