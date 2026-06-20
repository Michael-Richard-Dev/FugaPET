using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

public sealed class PermissoesEntradaProdutoH1Tests : IDisposable
{
    public PermissoesEntradaProdutoH1Tests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    [Fact]
    public void Constantes_DeveExporTodasAsPermissoesDaEntrada()
    {
        Assert.Equal("ENTRADA_PRODUTO", PermissoesSistema.Rotinas.EntradaProduto);
        Assert.Equal("PESO_MANUAL", PermissoesSistema.Acoes.PesoManual);
        Assert.Equal("SINCRONIZAR_CACHE", PermissoesSistema.Acoes.SincronizarCache);
        Assert.Equal("ENVIAR_SAP", PermissoesSistema.Acoes.EnviarSap);
        Assert.Equal("IMPRIMIR", PermissoesSistema.Acoes.Imprimir);
        Assert.Equal("REIMPRIMIR", PermissoesSistema.Acoes.Reimprimir);
    }

    [Fact]
    public void PermissaoEspecifica_DeveSerPrioritaria()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessao(
            Permissao(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.EntradaProduto,
                PermissoesSistema.Acoes.PesoManual)));

        Assert.True(AutorizacaoEntradaProdutoServico.PossuiPermissao(
            PermissoesSistema.Acoes.PesoManual));
    }

    [Fact]
    public void PesoManual_NaoDeveSerLiberadoSomentePorNomeDePerfil()
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 7,
            Login = "usuario",
            Nome = "Usuario",
            PerfisCodigo = ["ADMINISTRADOR", "PCP_CHAVE"],
            Permissoes = [],
            IntegracaoBancoHabilitada = true
        });

        Assert.False(AutorizacaoEntradaProdutoServico.PossuiPermissao(
            PermissoesSistema.Acoes.PesoManual));
    }

    [Fact]
    public void PesoManual_LegadoExigeFinalizarESincronizarCache()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessao(
            Permissao(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.LeituraProducao,
                PermissoesSistema.Acoes.Finalizar),
            Permissao(
                PermissoesSistema.Modulos.IntegracaoSap,
                PermissoesSistema.Rotinas.CacheSap,
                PermissoesSistema.Acoes.Sincronizar)));

        Assert.True(AutorizacaoEntradaProdutoServico.PossuiPermissao(
            PermissoesSistema.Acoes.PesoManual));
    }

    // Compatibilidade do fluxo LEGADO (PesagemEntradaServico): garante que o portao antigo segue
    // bloqueando. Fluxo oficial e EntradaProdutoServico + EntradaProdutoRepositorio. CS0618 esperado.
#pragma warning disable CS0618
    [Fact]
    public async Task PesagemServico_SemFinalizar_DeveBloquearAntesDoRepositorio()
    {
        PesagemEntradaServico servico = new(null!);

        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.SalvarPesagensAsync(
            [
                new PesagemEntradaItem
                {
                    CodigoSapPedidoCompraItem = 1,
                    PesoKg = 1m,
                    OrigemPeso = "LIDO"
                }
            ]));
    }
#pragma warning restore CS0618

    [Fact]
    public void View_NaoDeveAutorizarPesoManualPorPerfil()
    {
        string conteudo = File.ReadAllText(LocalizarArquivo(
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs"));

        Assert.DoesNotContain("PerfisCodigo", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("PCP_CHAVE", conteudo, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.PesoManual", conteudo, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Reimprimir", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Services_DevemRevalidarPermissoesEspecificas()
    {
        string pesagem = File.ReadAllText(LocalizarArquivo(
            "Servicos",
            "Operacao",
            "PesagemEntradaServico.cs"));
        string impressao = File.ReadAllText(LocalizarArquivo(
            "Servicos",
            "Operacao",
            "ImpressoraEtiquetaServico.cs"));
        string sap = File.ReadAllText(LocalizarArquivo(
            "Servicos",
            "IntegracaoSap",
            "SincronizacaoPedidoCompraSapServico.cs"));

        Assert.Contains("PermissoesSistema.Acoes.Finalizar", pesagem, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Imprimir", impressao, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Reimprimir", impressao, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.EnviarSap", sap, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptBanco_NaoDeveSelecionarPerfilPorNome()
    {
        string caminhoIncremental = LocalizarArquivoOpcional(
            "BancoDados",
            "001_incrementais",
            "020_criar_permissoes_especificas_entrada_produto_v1_0.sql");
        string caminho = File.Exists(caminhoIncremental)
            ? caminhoIncremental
            : LocalizarArquivo(
                "BancoDados",
                "000_baseline",
                "Banco_Desenvolvimento_V1_1_Geral",
                "000_execucao_completa_desenvolvimento_v1_1.sql");
        string conteudoCompleto = File.ReadAllText(caminho);
        string conteudo = ExtrairSecao(
            conteudoCompleto,
            "020 - Permiss",
            "021 - Campos");

        Assert.Contains("'ENTRADA_PRODUTO'", conteudo, StringComparison.Ordinal);
        Assert.Contains("'PESO_MANUAL'", conteudo, StringComparison.Ordinal);
        Assert.Contains("'SINCRONIZAR_CACHE'", conteudo, StringComparison.Ordinal);
        Assert.Contains("'ENVIAR_SAP'", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("nome_perfil_acesso", conteudo, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        EstadoSessaoUsuarioAtual.Limpar();
    }

    private static SessaoUsuarioAplicacao CriarSessao(
        params PermissaoSessaoAplicacao[] permissoes)
        => new()
        {
            IdUsuario = 1,
            Login = "usuario",
            Nome = "Usuario",
            PerfisCodigo = [],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        };

    private static PermissaoSessaoAplicacao Permissao(
        string modulo,
        string rotina,
        string acao)
        => new()
        {
            Modulo = modulo,
            Rotina = rotina,
            Acao = acao
        };

    private static string LocalizarArquivo(params string[] partes)
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            string candidato = Path.Combine([diretorio.FullName, .. partes]);
            if (File.Exists(candidato))
            {
                return candidato;
            }

            diretorio = diretorio.Parent;
        }

        throw new FileNotFoundException(Path.Combine(partes));
    }

    private static string LocalizarArquivoOpcional(params string[] partes)
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj")))
            {
                return Path.Combine([diretorio.FullName, .. partes]);
            }

            diretorio = diretorio.Parent;
        }

        return Path.Combine(partes);
    }

    private static string ExtrairSecao(string conteudo, string inicio, string fim)
    {
        int indiceInicio = conteudo.IndexOf(inicio, StringComparison.Ordinal);
        if (indiceInicio < 0)
        {
            return conteudo;
        }

        int indiceFim = conteudo.IndexOf(fim, indiceInicio, StringComparison.Ordinal);
        return indiceFim > indiceInicio
            ? conteudo[indiceInicio..indiceFim]
            : conteudo[indiceInicio..];
    }
}
