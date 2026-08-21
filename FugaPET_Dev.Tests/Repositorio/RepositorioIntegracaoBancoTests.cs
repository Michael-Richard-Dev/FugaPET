using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Tests.Repositorio;

public sealed class RepositorioIntegracaoBancoTests : IAsyncLifetime
{
    private readonly string _prefixo = "IT_" + Guid.NewGuid().ToString("N")[..12];
    private FabricaConexaoBancoTeste? _fabrica;
    private string? _motivoIgnorado;

    public async Task InitializeAsync()
    {
        if (!BancoTesteIntegracao.TentarCriar(out FabricaConexaoBancoTeste fabrica, out string motivo))
        {
            _motivoIgnorado = motivo;
            return;
        }

        if (!await BancoTesteIntegracao.SchemaDesenvolvimentoDisponivelAsync(fabrica))
        {
            _motivoIgnorado = "Schema desenvolvimento nao encontrado no banco de teste.";
            return;
        }

        _fabrica = fabrica;
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "teste.integracao",
            Nome = "Teste Integracao",
            IntegracaoBancoHabilitada = true
        });
    }

    public async Task DisposeAsync()
    {
        EstadoSessaoUsuarioAtual.Limpar();

        if (_fabrica is null)
        {
            return;
        }

        await LimparMassaAsync();
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task UsuarioPerfilRepositorio_SincronizarPerfilUnicoAsync_DeveManterExatamenteUmPerfilAtivo()
    {
        BancoConfigurado();

        long codigoPerfilAntigo = await InserirPerfilAsync("Perfil Antigo");
        long codigoPerfilNovo = await InserirPerfilAsync("Perfil Novo");
        long codigoUsuario = await InserirUsuarioAsync("usuario.perfil");
        await InserirUsuarioPerfilAsync(codigoUsuario, codigoPerfilAntigo, ativo: true);
        await InserirUsuarioPerfilAsync(codigoUsuario, codigoPerfilNovo, ativo: true);

        UsuarioPerfilRepositorio repositorio = new(_fabrica!);
        await repositorio.SincronizarPerfilUnicoAsync(codigoUsuario, codigoPerfilNovo);

        (int ativos, long? perfilAtivo) = await ObterResumoPerfisAtivosAsync(codigoUsuario);

        Assert.Equal(1, ativos);
        Assert.Equal(codigoPerfilNovo, perfilAtivo);
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task UsuarioRepositorio_AtualizarComVinculosAsync_DeveAtualizarUsuarioPerfilUnicoESetorPadrao()
    {
        BancoConfigurado();

        long codigoPerfilAntigo = await InserirPerfilAsync("Perfil Usuario Antigo");
        long codigoPerfilNovo = await InserirPerfilAsync("Perfil Usuario Novo");
        long codigoSetor = await InserirSetorAsync("Setor Padrao");
        long codigoUsuario = await InserirUsuarioAsync("usuario.vinculos");
        await InserirUsuarioPerfilAsync(codigoUsuario, codigoPerfilAntigo, ativo: true);

        UsuarioRepositorio repositorio = new(_fabrica!);
        int atualizados = await repositorio.AtualizarComVinculosAsync(
            new UsuarioCadastro
            {
                IdUsuario = codigoUsuario,
                NomeUsuario = $"{_prefixo} Usuario Atualizado",
                LoginUsuario = $"{_prefixo}_usuario_vinculos",
                EmailUsuario = string.Empty,
                TelefoneUsuario = string.Empty,
                SituacaoUsuario = true,
                DeveTrocarSenha = false,
                BloqueadoUsuario = false
            },
            codigoPerfilNovo,
            codigoSetor);

        (int ativos, long? perfilAtivo) = await ObterResumoPerfisAtivosAsync(codigoUsuario);
        long? setorPadrao = await ObterSetorPadraoUsuarioAsync(codigoUsuario);

        Assert.Equal(1, atualizados);
        Assert.Equal(1, ativos);
        Assert.Equal(codigoPerfilNovo, perfilAtivo);
        Assert.Equal(codigoSetor, setorPadrao);
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task PerfilPermissaoRepositorio_SincronizarPermissoesAsync_DeveBloquearUltimoAdministradorSemPermissoesEssenciais()
    {
        BancoConfigurado();

        IReadOnlyList<long> permissoesEssenciais = await ObterPermissoesAdministrativasEssenciaisAsync();
        if (permissoesEssenciais.Count != 4)
        {
            throw new InvalidOperationException(
                $"Teste de integracao indisponivel: esperadas 4 permissoes administrativas essenciais, encontradas {permissoesEssenciais.Count}.");
        }

        long codigoPerfil = await InserirPerfilAsync("Perfil Administrador Essencial");
        foreach (long codigoPermissao in permissoesEssenciais)
        {
            await InserirPerfilPermissaoAsync(codigoPerfil, codigoPermissao, ativo: true);
        }

        PerfilPermissaoRepositorio repositorio = new(_fabrica!);

        RemocaoPermissaoAdministrativaEssencialException excecao = await Assert.ThrowsAsync<RemocaoPermissaoAdministrativaEssencialException>(
            () => repositorio.SincronizarPermissoesAsync(codigoPerfil, Array.Empty<long>()));

        int ativos = await ContarPermissoesAtivasDoPerfilAsync(codigoPerfil);

        Assert.Equal(PerfilPermissaoRepositorio.MensagemProtecaoPerfilAdministrador, excecao.Message);
        Assert.Equal(4, ativos);
    }

    private bool BancoConfigurado()
    {
        if (_fabrica is not null)
        {
            return true;
        }

        throw new InvalidOperationException(
            $"Teste de integracao indisponivel: {_motivoIgnorado ?? "banco de teste nao configurado."}");
    }

    private async Task<long> InserirPerfilAsync(string nome)
    {
        const string sql = """
            INSERT INTO desenvolvimento.perfil_acesso
                   (nome_perfil_acesso, descricao_perfil_acesso, perfil_sistema, situacao_perfil_acesso)
            VALUES (@nome, @descricao, false, true)
            RETURNING codigo_perfil_acesso;
            """;

        return await ExecutarScalarLongAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@nome", $"{_prefixo} {nome}");
            comando.Parameters.AddWithValue("@descricao", "Massa de teste de integracao.");
        });
    }

    private async Task<long> InserirUsuarioAsync(string login)
    {
        const string sql = """
            INSERT INTO desenvolvimento.usuario
                   (nome_usuario, login_usuario, senha_hash, deve_trocar_senha, bloqueado_usuario, situacao_usuario)
            VALUES (@nome, @login, @senha_hash, false, false, true)
            RETURNING codigo_usuario;
            """;

        return await ExecutarScalarLongAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@nome", $"{_prefixo} Usuario");
            comando.Parameters.AddWithValue("@login", $"{_prefixo}_{login}");
            comando.Parameters.AddWithValue("@senha_hash", "hash_teste");
        });
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task TaraRepositorio_ListarAtivasPorSetorAsync_DeveRetornarTaraDoMesmoSetorEAtiva()
    {
        BancoConfigurado();

        long codigoSetor = await InserirSetorAsync("Setor Tara A");
        long codigoTipo = await InserirTipoTaraAsync("Tipo Tara A");
        long codigoTara = await InserirTaraAsync(codigoTipo, codigoSetor, "Tara Ativa A", ativo: true);

        TaraRepositorio repositorio = new(_fabrica!);
        IReadOnlyList<TaraCadastro> taras = await repositorio.ListarAtivasPorSetorAsync(codigoSetor);

        Assert.Contains(taras, tara => tara.CodigoTara == codigoTara);
        Assert.All(taras, tara => Assert.Equal(codigoSetor, tara.CodigoSetor));
        Assert.All(taras, tara => Assert.True(tara.SituacaoTara));
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task TaraRepositorio_ListarAtivasPorSetorAsync_NaoDeveRetornarTaraDeOutroSetor()
    {
        BancoConfigurado();

        long setorAlvo = await InserirSetorAsync("Setor Tara Alvo");
        long setorOutro = await InserirSetorAsync("Setor Tara Outro");
        long codigoTipo = await InserirTipoTaraAsync("Tipo Tara B");
        long taraOutroSetor = await InserirTaraAsync(codigoTipo, setorOutro, "Tara Outro Setor", ativo: true);

        TaraRepositorio repositorio = new(_fabrica!);
        IReadOnlyList<TaraCadastro> taras = await repositorio.ListarAtivasPorSetorAsync(setorAlvo);

        Assert.DoesNotContain(taras, tara => tara.CodigoTara == taraOutroSetor);
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task TaraRepositorio_ListarAtivasPorSetorAsync_NaoDeveRetornarTaraInativa()
    {
        BancoConfigurado();

        long codigoSetor = await InserirSetorAsync("Setor Tara Inativa");
        long codigoTipo = await InserirTipoTaraAsync("Tipo Tara C");
        long taraInativa = await InserirTaraAsync(codigoTipo, codigoSetor, "Tara Inativa", ativo: false);

        TaraRepositorio repositorio = new(_fabrica!);
        IReadOnlyList<TaraCadastro> taras = await repositorio.ListarAtivasPorSetorAsync(codigoSetor);

        Assert.DoesNotContain(taras, tara => tara.CodigoTara == taraInativa);
    }

    [IntegrationFact]
    [Trait("Categoria", "Integracao")]
    public async Task TaraRepositorio_ListarAtivasPorSetorAsync_DeveRetornarVazioQuandoSetorSemTara()
    {
        BancoConfigurado();

        long codigoSetor = await InserirSetorAsync("Setor Sem Tara");

        TaraRepositorio repositorio = new(_fabrica!);
        IReadOnlyList<TaraCadastro> taras = await repositorio.ListarAtivasPorSetorAsync(codigoSetor);

        Assert.Empty(taras);
    }

    private async Task<long> InserirTipoTaraAsync(string nome)
    {
        const string sql = """
            INSERT INTO desenvolvimento.tipo_tara
                   (nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara)
            VALUES (@nome, @descricao, true)
            RETURNING codigo_tipo_tara;
            """;

        return await ExecutarScalarLongAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@nome", $"{_prefixo} {nome}");
            comando.Parameters.AddWithValue("@descricao", "Massa de teste de integracao.");
        });
    }

    private async Task<long> InserirTaraAsync(long codigoTipo, long codigoSetor, string nome, bool ativo)
    {
        const string sql = """
            INSERT INTO desenvolvimento.tara
                   (codigo_tipo_tara, codigo_setor, nome_tara, tamanho, peso_kg, situacao_tara)
            VALUES (@tipo, @setor, @nome, 'M', 1.500, @ativo)
            RETURNING codigo_tara;
            """;

        return await ExecutarScalarLongAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@tipo", codigoTipo);
            comando.Parameters.AddWithValue("@setor", codigoSetor);
            comando.Parameters.AddWithValue("@nome", $"{_prefixo} {nome}");
            comando.Parameters.AddWithValue("@ativo", ativo);
        });
    }

    private async Task<long> InserirSetorAsync(string nome)
    {
        const string sql = """
            INSERT INTO desenvolvimento.setor
                   (nome_setor, descricao_setor, situacao_setor)
            VALUES (@nome, @descricao, true)
            RETURNING codigo_setor;
            """;

        return await ExecutarScalarLongAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@nome", $"{_prefixo} {nome}");
            comando.Parameters.AddWithValue("@descricao", "Massa de teste de integracao.");
        });
    }

    private async Task InserirUsuarioPerfilAsync(long codigoUsuario, long codigoPerfil, bool ativo)
    {
        const string sql = """
            INSERT INTO desenvolvimento.usuario_perfil
                   (codigo_usuario, codigo_perfil_acesso, situacao_usuario_perfil)
            VALUES (@codigo_usuario, @codigo_perfil_acesso, @ativo);
            """;

        await ExecutarAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@codigo_usuario", codigoUsuario);
            comando.Parameters.AddWithValue("@codigo_perfil_acesso", codigoPerfil);
            comando.Parameters.AddWithValue("@ativo", ativo);
        });
    }

    private async Task InserirPerfilPermissaoAsync(long codigoPerfil, long codigoPermissao, bool ativo)
    {
        const string sql = """
            INSERT INTO desenvolvimento.perfil_permissao
                   (codigo_perfil_acesso, codigo_permissao, situacao_perfil_permissao)
            VALUES (@codigo_perfil_acesso, @codigo_permissao, @ativo);
            """;

        await ExecutarAsync(sql, comando =>
        {
            comando.Parameters.AddWithValue("@codigo_perfil_acesso", codigoPerfil);
            comando.Parameters.AddWithValue("@codigo_permissao", codigoPermissao);
            comando.Parameters.AddWithValue("@ativo", ativo);
        });
    }

    private async Task<(int Ativos, long? PerfilAtivo)> ObterResumoPerfisAtivosAsync(long codigoUsuario)
    {
        const string sql = """
            SELECT count(*)::int, max(codigo_perfil_acesso)
              FROM desenvolvimento.usuario_perfil
             WHERE codigo_usuario = @codigo_usuario
               AND situacao_usuario_perfil = true;
            """;

        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo_usuario", codigoUsuario);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        await leitor.ReadAsync();
        int ativos = leitor.GetInt32(0);
        long? perfilAtivo = leitor.IsDBNull(1) ? null : leitor.GetInt64(1);
        return (ativos, perfilAtivo);
    }

    private async Task<long?> ObterSetorPadraoUsuarioAsync(long codigoUsuario)
    {
        const string sql = "SELECT codigo_setor_padrao FROM desenvolvimento.usuario WHERE codigo_usuario = @codigo_usuario;";
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo_usuario", codigoUsuario);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is long codigo ? codigo : null;
    }

    private async Task<IReadOnlyList<long>> ObterPermissoesAdministrativasEssenciaisAsync()
    {
        const string sql = """
            SELECT codigo_permissao
              FROM desenvolvimento.permissao
             WHERE situacao_permissao = true
               AND (
                    (modulo_permissao = 'SEGURANCA' AND rotina_permissao = 'PERFIL_ACESSO' AND acao_permissao = 'GERENCIAR')
                 OR (modulo_permissao = 'SEGURANCA' AND rotina_permissao = 'PERMISSAO' AND acao_permissao = 'GERENCIAR')
                 OR (modulo_permissao = 'SEGURANCA' AND rotina_permissao = 'USUARIO' AND acao_permissao = 'CONSULTAR')
                 OR (modulo_permissao = 'SEGURANCA' AND rotina_permissao = 'USUARIO' AND acao_permissao = 'EDITAR')
               );
            """;

        List<long> codigos = [];
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            codigos.Add(leitor.GetInt64(0));
        }

        return codigos;
    }

    private async Task<int> ContarPermissoesAtivasDoPerfilAsync(long codigoPerfil)
    {
        const string sql = """
            SELECT count(*)::int
              FROM desenvolvimento.perfil_permissao
             WHERE codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_perfil_permissao = true;
            """;

        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo_perfil_acesso", codigoPerfil);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is int total ? total : 0;
    }

    private async Task<long> ExecutarScalarLongAsync(string sql, Action<NpgsqlCommand> configurar)
    {
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        configurar(comando);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is long codigo ? codigo : 0L;
    }

    private async Task ExecutarAsync(string sql, Action<NpgsqlCommand> configurar)
    {
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        configurar(comando);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task LimparMassaAsync()
    {
        string like = _prefixo + "%";
        await ExecutarAsync(
            """
            DELETE FROM desenvolvimento.perfil_permissao
             WHERE codigo_perfil_acesso IN (
                   SELECT codigo_perfil_acesso FROM desenvolvimento.perfil_acesso WHERE nome_perfil_acesso LIKE @like
             );

            DELETE FROM desenvolvimento.usuario_setor
             WHERE codigo_usuario IN (
                   SELECT codigo_usuario FROM desenvolvimento.usuario WHERE login_usuario LIKE @like
             );

            DELETE FROM desenvolvimento.usuario_perfil
             WHERE codigo_usuario IN (
                   SELECT codigo_usuario FROM desenvolvimento.usuario WHERE login_usuario LIKE @like
             )
                OR codigo_perfil_acesso IN (
                   SELECT codigo_perfil_acesso FROM desenvolvimento.perfil_acesso WHERE nome_perfil_acesso LIKE @like
             );

            DELETE FROM desenvolvimento.tara WHERE nome_tara LIKE @like;
            DELETE FROM desenvolvimento.tipo_tara WHERE nome_tipo_tara LIKE @like;

            DELETE FROM desenvolvimento.usuario WHERE login_usuario LIKE @like;
            DELETE FROM desenvolvimento.setor WHERE nome_setor LIKE @like;
            DELETE FROM desenvolvimento.perfil_acesso WHERE nome_perfil_acesso LIKE @like;
            """,
            comando => comando.Parameters.AddWithValue("@like", like));
    }
}

internal sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BancoTesteIntegracao.VariavelConnectionString)))
        {
            Skip =
                $"Teste de integracao ignorado explicitamente: variavel {BancoTesteIntegracao.VariavelConnectionString} nao configurada.";
        }
        else if (!BancoTesteIntegracao.DestrutivoAutorizado())
        {
            // Sem autorizacao destrutiva explicita, PULA (nao falha) - a limpeza usa DELETE.
            Skip =
                $"Teste destrutivo ignorado: defina {BancoTesteIntegracao.VariavelPermitirDestrutivo}=true para autorizar.";
        }
    }
}
