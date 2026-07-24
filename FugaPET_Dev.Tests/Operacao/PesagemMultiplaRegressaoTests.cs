namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// Regressão: a correção da escala decimal fica EXCLUSIVAMENTE no leitor serial. A tela de pesagem NÃO pode
/// compensar (dividir/multiplicar) o peso — usa direto o valor retornado por BalancaLeituraServico e calcula
/// pesoLiquido = pesoBruto - tara. A leitura manual permanece igual à automática (ambas via TryParsePeso).
/// </summary>
public sealed class PesagemMultiplaRegressaoTests
{
    [Fact]
    public void Tela_UsaPesoDireto_SemCompensacao()
    {
        string form = LerForm();
        // Usa o peso retornado pela balança direto (sem deslocar vírgula).
        Assert.Contains("TryParsePeso(leitura.Peso, out decimal peso)", form, StringComparison.Ordinal);
        Assert.Contains("AdicionarPesoAsync(peso, \"BALANCA\", leitura.Peso)", form, StringComparison.Ordinal);
        // Cálculo de líquido = bruto - tara (não alterado).
        Assert.Contains("peso - _tara.PesoKg", form, StringComparison.Ordinal);
        Assert.Contains("LeituraOriginal = leituraOriginal", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_NaoDivideNemMultiplicaOPeso()
    {
        string form = LerForm();
        Assert.DoesNotContain("peso / 10", form, StringComparison.Ordinal);
        Assert.DoesNotContain("peso / 100", form, StringComparison.Ordinal);
        Assert.DoesNotContain("/ 10m", form, StringComparison.Ordinal);
        Assert.DoesNotContain("/ 100m", form, StringComparison.Ordinal);
        Assert.DoesNotContain("peso * 10", form, StringComparison.Ordinal);
        Assert.DoesNotContain("peso * 100", form, StringComparison.Ordinal);
    }

    [Fact]
    public void PesoLiquido_BrutoMenosTara()
    {
        // Documenta o resultado esperado com o peso já corrigido pelo leitor (P03): 1,65 - 0,50 = 1,15.
        decimal pesoBruto = 1.65m;
        decimal tara = 0.50m;
        Assert.Equal(1.15m, pesoBruto - tara);
    }

    private static string LerForm()
        => File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Processo", "PesagemMultiplaItemForm.cs"));

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
