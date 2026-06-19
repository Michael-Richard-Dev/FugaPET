namespace FugaPET_Dev.Tests.Repositorio;

public sealed class RepositorioContratosSegurancaTests
{
    [Fact]
    public void UsuarioRepositorio_AtualizarComVinculos_DeveSerTransacionalEManterPerfilUnico()
    {
        string fonte = LerFonte("AcessoDados", "Repositorio", "UsuarioRepositorio.cs");
        string metodo = ExtrairMetodo(fonte, "AtualizarComVinculosAsync");

        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", metodo);
        Assert.Contains("UPDATE usuario", metodo);
        Assert.Contains("UPDATE usuario_perfil", metodo);
        Assert.Contains("SET situacao_usuario_perfil = false", metodo);
        Assert.Contains("AND situacao_usuario_perfil = true", metodo);
        Assert.Contains("codigo_perfil_acesso = @codigo_perfil_acesso", metodo);
        Assert.Contains("INSERT INTO usuario_perfil", metodo);
        Assert.Contains("UPDATE usuario_setor", metodo);
        Assert.Contains("INSERT INTO usuario_setor", metodo);
    }

    [Fact]
    public void UsuarioPerfilRepositorio_SincronizarPerfilUnico_DeveInativarAtivosAntesDoSelecionado()
    {
        string fonte = LerFonte("AcessoDados", "Repositorio", "UsuarioPerfilRepositorio.cs");
        string metodo = ExtrairMetodo(fonte, "SincronizarPerfilUnicoAsync");

        int inativar = metodo.IndexOf("SET situacao_usuario_perfil = false", StringComparison.Ordinal);
        int reativar = metodo.IndexOf("SET situacao_usuario_perfil = true", StringComparison.Ordinal);
        int inserir = metodo.IndexOf("INSERT INTO usuario_perfil", StringComparison.Ordinal);

        Assert.True(inativar >= 0, "O metodo deve inativar todos os perfis ativos do usuario.");
        Assert.True(reativar > inativar, "O perfil selecionado deve ser reativado depois da inativacao geral.");
        Assert.True(inserir > reativar, "Se nao existir vinculo anterior, o perfil selecionado deve ser inserido por ultimo.");
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", metodo);
    }

    [Fact]
    public void PerfilPermissaoRepositorio_DeveBloquearRemocaoDePermissoesCriticasDoUltimoAdministrador()
    {
        string fonte = LerFonte("AcessoDados", "Repositorio", "PerfilPermissaoRepositorio.cs");
        string metodo = ExtrairMetodo(fonte, "SincronizarPermissoesAsync");
        string protecao = fonte;

        Assert.Contains("MensagemProtecaoPerfilAdministrador", fonte);
        Assert.Contains("MantemPerfilComPermissoesAdministrativasEssenciaisAsync", metodo);
        Assert.Contains("throw new RemocaoPermissaoAdministrativaEssencialException", metodo);

        // O conjunto essencial deve referenciar PermissoesSistema (constantes), nao strings soltas.
        // Assim este teste tambem reforca a adocao do PermissoesSistema neste repositorio critico.
        Assert.Contains("PermissoesSistema.Modulos.Seguranca", protecao);
        Assert.Contains("PermissoesSistema.Rotinas.PerfilAcesso", protecao);
        Assert.Contains("PermissoesSistema.Rotinas.Permissao", protecao);
        Assert.Contains("PermissoesSistema.Rotinas.Usuario", protecao);
        Assert.Contains("PermissoesSistema.Acoes.Gerenciar", protecao);
        Assert.Contains("PermissoesSistema.Acoes.Consultar", protecao);
        Assert.Contains("PermissoesSistema.Acoes.Editar", protecao);
    }

    private static string LerFonte(params string[] partes)
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            string candidatoProjeto = Path.Combine(diretorio, "FugaPET_Dev.csproj");
            if (File.Exists(candidatoProjeto))
            {
                return File.ReadAllText(Path.Combine(new[] { diretorio }.Concat(partes).ToArray()));
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada para teste de contrato.");
    }

    private static string ExtrairMetodo(string fonte, string nomeMetodo)
    {
        int inicio = fonte.IndexOf(nomeMetodo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Metodo {nomeMetodo} nao encontrado.");

        int abreChave = fonte.IndexOf('{', inicio);
        Assert.True(abreChave >= 0, $"Metodo {nomeMetodo} sem corpo localizado.");

        int profundidade = 0;
        for (int i = abreChave; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') profundidade++;
            if (fonte[i] == '}') profundidade--;
            if (profundidade == 0) return fonte[inicio..(i + 1)];
        }

        throw new InvalidOperationException($"Nao foi possivel extrair o corpo do metodo {nomeMetodo}.");
    }
}
