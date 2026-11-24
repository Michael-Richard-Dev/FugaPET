namespace FugaPET_Dev.Tests.Tela;

public sealed class PainelInicialSidebarHarmoniaTests
{
    [Fact]
    public void Sidebar_ReservaColunaUniformeParaIconesEAlinhaTextos()
    {
        string source = LerPainelInicial();

        Assert.Contains("const int larguraIcone = 44;", source, StringComparison.Ordinal);
        Assert.Contains("icone.SetBounds(10, 0, larguraIcone, altura);", source, StringComparison.Ordinal);
        Assert.Contains("texto.Left = 64;", source, StringComparison.Ordinal);
        Assert.Contains("texto.Top = Math.Max(4, (altura - alturaTexto) / 2);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_BlocoUsuarioTemLayoutProprioSemSobreporStatusDeCarga()
    {
        string source = LerPainelInicial();

        Assert.Contains("AjustarRodapeSidebar();", source, StringComparison.Ordinal);
        Assert.Contains("sidebarUserPanel.Height = alturaUsuario;", source, StringComparison.Ordinal);
        Assert.Contains("sidebarUserAvatarLabel.SetBounds(18, 20, 44, 44);", source, StringComparison.Ordinal);
        Assert.Contains("sidebarUserNameLabel.SetBounds(74, 18, larguraTexto, 26);", source, StringComparison.Ordinal);
        Assert.Contains("sidebarUserStatusLabel.SetBounds(74, 44, larguraTexto, 22);", source, StringComparison.Ordinal);

        // 24/11/2026: este teste cobrava o literal "_preCarregamentoStatusLabel.Top =
        // sidebarUserPanel.Top - 24;" — e era JUSTAMENTE esse numero de pixels cravado que fazia o
        // aviso cobrir o nome do usuario. O teste pinava o defeito. Agora cobra a propriedade: a
        // caixa do aviso e calculada, e a conta e exercitada em
        // PainelInicialAvisoPreCarregamentoLayoutTests.
        Assert.Contains("PosicionarStatusPreCarregamentoPedidos();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_preCarregamentoStatusLabel.Top = sidebarUserPanel.Top", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDeCarga_PertenceASidebarETemReticencias()
    {
        string source = LerPainelInicial();

        Assert.Contains("AutoEllipsis = true", source, StringComparison.Ordinal);
        Assert.Contains("sidebarPanel.Controls.Add(_preCarregamentoStatusLabel);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Controls.Add(_preCarregamentoStatusLabel);", source.Replace("sidebarPanel.Controls.Add(_preCarregamentoStatusLabel);", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_RecalculaRegiaoDepoisDeRedimensionarCadaItem()
    {
        string source = LerPainelInicial();

        int redimensionamento = source.IndexOf("item.Height = altura;", StringComparison.Ordinal);
        int regiaoAtualizada = source.IndexOf("ApplyRoundedRegion(item, 6);", StringComparison.Ordinal);

        Assert.True(redimensionamento >= 0);
        Assert.True(regiaoAtualizada > redimensionamento);
        Assert.DoesNotContain("ReaplicarEstadoVisualSidebar", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_activeSidebarMenuItem", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_UsaLarguraUniformeBaseadaNoMaiorRotulo()
    {
        string source = LerPainelInicial();

        Assert.Contains("int larguraTextoMaxima = itens.Max(item => item.texto.GetPreferredSize(Size.Empty).Width);", source, StringComparison.Ordinal);
        Assert.Contains("int larguraItem = 64 + larguraTextoMaxima + 16;", source, StringComparison.Ordinal);
        Assert.Contains("item.Width = larguraItem;", source, StringComparison.Ordinal);
        // A coluna da sidebar abraça o item + margem (com teto de 420), em vez de a largura do item ser clampada.
        Assert.Contains("bodyLayout.ColumnStyles[0].Width = Math.Min(420, larguraItem + 18);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Cabecalho_RemoveBadgeBancoEPreservaStatusNoRodape()
    {
        string source = LerPainelInicial();
        string designer = LerPainelInicialDesigner();

        Assert.DoesNotContain("sapStatusPanel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusLabel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusDotLabel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusPanel", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusLabel", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusDotLabel", designer, StringComparison.Ordinal);
        Assert.Contains("Shown += async (_, _) => await AtualizarStatusIndustrialAsync();", source, StringComparison.Ordinal);
        Assert.Contains("cellBancoText.Text = $\"Banco de Dados:  {nomeBanco} | {statusBanco.Ambiente} | {statusBanco.Situacao}\";", source, StringComparison.Ordinal);
    }

    private static string LerPainelInicial()
        => LerArquivoPainelInicial("PainelInicialForm.cs");

    private static string LerPainelInicialDesigner()
        => LerArquivoPainelInicial("PainelInicialForm.Designer.cs");

    private static string LerArquivoPainelInicial(string nomeArquivo)
    {
        string diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            string arquivo = Path.Combine(diretorio, "Tela", nomeArquivo);
            if (File.Exists(arquivo))
            {
                return File.ReadAllText(arquivo);
            }

            diretorio = Directory.GetParent(diretorio)?.FullName ?? string.Empty;
        }

        throw new FileNotFoundException($"Tela/{nomeArquivo}");
    }
}
