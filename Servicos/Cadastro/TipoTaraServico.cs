using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class TipoTaraServico
{
    private const string Entidade = PermissoesSistema.Rotinas.TipoTara;
    private const string Tela = "TipoTaraForm";

    // Tarefa Tipo de Tara (Ajuste 3): mensagem única de bloqueio de inativação com taras ativas vinculadas.
    internal const string MensagemBloqueioInativacao =
        "Não é possível inativar este tipo de tara porque existem taras ativas vinculadas a ele.";

    // Ajuste 1: nome é chave funcional ÚNICA GLOBAL (independe de ativo/inativo).
    internal const string MensagemDuplicidadeGlobal =
        "Já existe um tipo de tara com este nome, mesmo que esteja inativo. Localize o registro existente e utilize a ação Reativar.";

    // Ajuste 2: situação muda apenas por Inativar/Reativar (não pela edição).
    internal const string MensagemInativarPelaAcao =
        "A inativação do tipo de tara deve ser feita pela ação Inativar, pois exige validação de dependências.";
    internal const string MensagemReativarPelaAcao =
        "A reativação do tipo de tara deve ser feita pela ação Reativar.";

    private readonly TipoTaraRepositorio _repositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public TipoTaraServico(TipoTaraRepositorio repositorio, AuditoriaServico auditoriaServico)
    {
        _repositorio = repositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<TipoTaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _repositorio.ListarAsync(cancellationToken);

    // Tarefa Tipo de Tara (Ajuste 4): referência = contagem de taras ativas por tipo (mapa código → quantidade).
    public Task<IReadOnlyDictionary<long, int>> ContarTarasAtivasPorTipoAsync(CancellationToken cancellationToken = default)
        => _repositorio.ContarTarasAtivasPorTipoAsync(cancellationToken);

    // Tarefa Tipo de Tara (Ajuste 6): diagnóstico de nomes duplicados (somente leitura; nenhuma correção automática).
    public Task<IReadOnlyList<DuplicadoTipoTara>> ListarNomesDuplicadosAsync(CancellationToken cancellationToken = default)
        => _repositorio.ListarNomesDuplicadosAsync(cancellationToken);

    // Ajuste 4: validação/normalização de nome (2..80) e descrição (<=255), padrão Setor/Cargo.
    private static ResultadoOperacao? ValidarENormalizar(TipoTaraCadastro tipo)
    {
        tipo.NomeTipoTara = tipo.NomeTipoTara?.Trim() ?? string.Empty;
        tipo.DescricaoTipoTara = tipo.DescricaoTipoTara?.Trim() ?? string.Empty;

        if (tipo.NomeTipoTara.Length < TipoTaraCadastro.TamanhoMinimoNome
            || tipo.NomeTipoTara.Length > TipoTaraCadastro.TamanhoMaximoNome)
        {
            return ResultadoOperacao.Falha("Nome do tipo de tara deve ter entre 2 e 80 caracteres.");
        }

        if (tipo.DescricaoTipoTara.Length > TipoTaraCadastro.TamanhoMaximoDescricao)
        {
            return ResultadoOperacao.Falha("Descrição do tipo de tara deve ter no máximo 255 caracteres.");
        }

        return null;
    }

    public Task<TipoTaraCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        // Ajuste 4: validação/normalização (nome 2..80, descrição <=255) no padrão Setor/Cargo.
        ResultadoOperacao? validacao = ValidarENormalizar(tipo);
        if (validacao is not null) return validacao;

        try
        {
            // Ajuste 1: duplicidade GLOBAL (ativo OU inativo) — orienta a reativar o existente.
            if (await _repositorio.ExisteNomeAsync(tipo.NomeTipoTara, null, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            long id = await _repositorio.InserirAsync(tipo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o tipo de tara.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Tipo de tara '{tipo.NomeTipoTara}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Tipo de tara cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (tipo.CodigoTipoTara <= 0)
        {
            return ResultadoOperacao.Falha("Id do tipo de tara invalido para edicao.");
        }

        // Ajuste 4: validação/normalização (nome 2..80, descrição <=255).
        ResultadoOperacao? validacao = ValidarENormalizar(tipo);
        if (validacao is not null) return validacao;

        try
        {
            TipoTaraCadastro? anterior = await _repositorio.ObterPorIdAsync(tipo.CodigoTipoTara, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Tipo de tara nao encontrado para edicao.");
            }

            // Ajuste 2: AtualizarAsync edita SOMENTE dados cadastrais (nome/descrição). Situação muda apenas
            // por Inativar (Excluir) / Reativar. Qualquer tentativa de mudar a situação aqui é bloqueada.
            if (anterior.SituacaoTipoTara && !tipo.SituacaoTipoTara)
            {
                return ResultadoOperacao.Falha(MensagemInativarPelaAcao);
            }

            if (!anterior.SituacaoTipoTara && tipo.SituacaoTipoTara)
            {
                return ResultadoOperacao.Falha(MensagemReativarPelaAcao);
            }

            // Ajuste 1: duplicidade GLOBAL contra OUTRO registro (ativo OU inativo).
            if (await _repositorio.ExisteNomeAsync(tipo.NomeTipoTara, tipo.CodigoTipoTara, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            // Preserva a situação atual (não é alterada por edição).
            tipo.SituacaoTipoTara = anterior.SituacaoTipoTara;

            int atualizados = await _repositorio.AtualizarAsync(tipo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Tipo de tara nao encontrado para edicao.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, tipo.CodigoTipoTara, $"Tipo de tara '{tipo.NomeTipoTara}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do tipo de tara invalido.");

        try
        {
            // Ajuste 3/10: verificação prévia (mensagem amigável) via resumo de dependências.
            ResumoDependenciasTipoTara dependencias = await _repositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
            if (dependencias.PossuiDependenciasAtivas)
            {
                return ResultadoOperacao.Falha(dependencias.ObterMensagemBloqueio());
            }

            // Ajuste 9: o UPDATE também protege atomicamente (NOT EXISTS tara ativa). Se 0, reconsulta a causa.
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0)
            {
                ResumoDependenciasTipoTara dependenciasConcorrentes = await _repositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
                if (dependenciasConcorrentes.PossuiDependenciasAtivas)
                {
                    return ResultadoOperacao.Falha(dependenciasConcorrentes.ObterMensagemBloqueio());
                }

                return ResultadoOperacao.Falha("Tipo de tara nao encontrado ou ja estava inativo.");
            }

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tipo de tara inativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do tipo de tara invalido.");

        try
        {
            // Ajuste 1/8: reativar respeitando duplicidade GLOBAL (não pode existir OUTRO registro com o mesmo nome).
            TipoTaraCadastro? tipo = await _repositorio.ObterPorIdAsync(id, cancellationToken);
            if (tipo is null)
            {
                return ResultadoOperacao.Falha("Tipo de tara nao encontrado.");
            }

            if (await _repositorio.ExisteNomeAsync(tipo.NomeTipoTara, id, cancellationToken))
            {
                return ResultadoOperacao.Falha("Já existe outro tipo de tara com este nome. Não é possível reativar este registro.");
            }

            int reativados = await _repositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Tipo de tara nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tipo de tara reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}


