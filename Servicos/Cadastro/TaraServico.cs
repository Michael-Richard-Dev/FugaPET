using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class TaraServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Tara;
    private const string Tela = "TaraForm";

    // Ajuste 7: nome é chave funcional ÚNICA por setor+tipo, independente de ativo/inativo.
    internal const string MensagemDuplicidadeGlobal =
        "Já existe uma tara com este nome para o mesmo setor e tipo, mesmo que esteja inativa. Localize o registro existente e utilize a ação Reativar.";

    // Ajuste 5: situação muda apenas por Inativar/Reativar (não pela edição).
    internal const string MensagemInativarPelaAcao =
        "A inativação da tara deve ser feita pela ação Inativar.";
    internal const string MensagemReativarPelaAcao =
        "A reativação da tara deve ser feita pela ação Reativar.";

    internal const string MensagemDuplicidadeReativacao =
        "Já existe outra tara com este nome no mesmo setor e tipo. Não é possível reativar este registro.";

    // Ajuste 3: mensagens de vínculo inativo na reativação.
    internal const string MensagemSetorInativo =
        "Não é possível reativar esta tara porque o setor vinculado está inativo.";
    internal const string MensagemTipoInativo =
        "Não é possível reativar esta tara porque o tipo de tara vinculado está inativo.";

    private readonly TaraRepositorio _taraRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public TaraServico(TaraRepositorio taraRepositorio, AuditoriaServico auditoriaServico)
    {
        _taraRepositorio = taraRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<TaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _taraRepositorio.ListarAsync(cancellationToken);

    /// <summary>Taras ativas do setor informado, para a selecao de tara da Entrada de Produto.</summary>
    public Task<IReadOnlyList<TaraCadastro>> ListarAtivasPorSetorAsync(long codigoSetor, CancellationToken cancellationToken = default)
        => _taraRepositorio.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarENormalizar(tara);
        if (validacao is not null) return validacao;

        try
        {
            // Ajuste 7: duplicidade GLOBAL (ativa OU inativa) no mesmo setor+tipo — orienta a reativar o existente.
            if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(tara.NomeTara, tara.CodigoSetor, tara.CodigoTipoTara, null, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            long id = await _taraRepositorio.InserirAsync(tara, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar a tara.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Tara '{tara.NomeTara}' (setor {tara.CodigoSetor}, tipo {tara.CodigoTipoTara})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Tara cadastrada com sucesso.", id);
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

    public async Task<ResultadoOperacao> AtualizarAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (tara.CodigoTara <= 0) return ResultadoOperacao.Falha("Id da tara invalido para edicao.");

        ResultadoOperacao? validacao = ValidarENormalizar(tara);
        if (validacao is not null) return validacao;

        try
        {
            TaraCadastro? anterior = await _taraRepositorio.ObterPorIdAsync(tara.CodigoTara, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Tara nao encontrada para edicao.");
            }

            // Ajuste 5: edição altera SOMENTE dados cadastrais. Situação muda só por Inativar/Reativar.
            if (anterior.SituacaoTara && !tara.SituacaoTara)
            {
                return ResultadoOperacao.Falha(MensagemInativarPelaAcao);
            }

            if (!anterior.SituacaoTara && tara.SituacaoTara)
            {
                return ResultadoOperacao.Falha(MensagemReativarPelaAcao);
            }

            // Ajuste 7: duplicidade GLOBAL contra OUTRO registro (ativo OU inativo) no mesmo setor+tipo.
            if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(tara.NomeTara, tara.CodigoSetor, tara.CodigoTipoTara, tara.CodigoTara, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            // Preserva a situação atual (não é alterada por edição).
            tara.SituacaoTara = anterior.SituacaoTara;

            int atualizados = await _taraRepositorio.AtualizarAsync(tara, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Tara nao encontrada para edicao.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, tara.CodigoTara, $"Tara '{tara.NomeTara}'", Tela, cancellationToken);
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

        if (id <= 0) return ResultadoOperacao.Falha("Id da tara invalido para exclusao.");

        try
        {
            int excluidos = await _taraRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Tara nao encontrada ou ja estava inativa.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tara inativada com sucesso.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id da tara invalido.");

        try
        {
            TaraCadastro? tara = await _taraRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (tara is null)
            {
                return ResultadoOperacao.Falha("Tara nao encontrada.");
            }

            // Ajuste 3: não reativar se o setor/tipo vinculado estiver inativo.
            ResultadoOperacao? vinculoInvalido = await ValidarVinculosAtivosParaReativacaoAsync(id, cancellationToken);
            if (vinculoInvalido is not null) return vinculoInvalido;

            // Ajuste 7/11: reativar respeitando duplicidade GLOBAL (não pode existir OUTRA tara com o mesmo
            // nome no mesmo setor+tipo, ativa OU inativa).
            if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(tara.NomeTara, tara.CodigoSetor, tara.CodigoTipoTara, id, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeReativacao);
            }

            int reativados = await _taraRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0)
            {
                // Ajuste 5: o UPDATE também protege atomicamente (NOT EXISTS). Se 0, reconsulta a causa exata.
                TaraCadastro? atual = await _taraRepositorio.ObterPorIdAsync(id, cancellationToken);
                if (atual is null) return ResultadoOperacao.Falha("Tara não encontrada.");
                if (atual.SituacaoTara) return ResultadoOperacao.Falha("Tara já estava ativa.");

                if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(atual.NomeTara, atual.CodigoSetor, atual.CodigoTipoTara, id, cancellationToken))
                {
                    return ResultadoOperacao.Falha(MensagemDuplicidadeReativacao);
                }

                ResultadoOperacao? vinculoInvalido2 = await ValidarVinculosAtivosParaReativacaoAsync(id, cancellationToken);
                if (vinculoInvalido2 is not null) return vinculoInvalido2;

                return ResultadoOperacao.Falha("Não foi possível reativar a tara. Acione o suporte.");
            }

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tara reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    // Ajuste 3: bloqueia reativação quando setor OU tipo de tara vinculado está inativo. Null = vínculos OK.
    private async Task<ResultadoOperacao?> ValidarVinculosAtivosParaReativacaoAsync(long codigoTara, CancellationToken cancellationToken)
    {
        ResumoValidacaoReativacaoTara resumo = await _taraRepositorio.ObterResumoValidacaoReativacaoAsync(codigoTara, cancellationToken);
        if (!resumo.Encontrado)
        {
            return null; // trata "não encontrada" no fluxo principal.
        }

        if (!resumo.SetorAtivo) return ResultadoOperacao.Falha(MensagemSetorInativo);
        if (!resumo.TipoAtivo) return ResultadoOperacao.Falha(MensagemTipoInativo);
        return null;
    }

    // Ajuste 8: validação/normalização no padrão Setor/Cargo/TipoTara. Peso em KG (> 0, até 3 casas — numeric(14,3)).
    private static ResultadoOperacao? ValidarENormalizar(TaraCadastro tara)
    {
        tara.NomeTara = tara.NomeTara?.Trim() ?? string.Empty;
        tara.Tamanho = tara.Tamanho?.Trim() ?? string.Empty;
        tara.Observacao = tara.Observacao?.Trim() ?? string.Empty;

        if (tara.CodigoTipoTara <= 0) return ResultadoOperacao.Falha("Tipo da tara é obrigatório.");
        if (tara.CodigoSetor <= 0) return ResultadoOperacao.Falha("Setor da tara é obrigatório.");

        if (tara.NomeTara.Length < TaraCadastro.TamanhoMinimoNome
            || tara.NomeTara.Length > TaraCadastro.TamanhoMaximoNome)
        {
            return ResultadoOperacao.Falha("Nome da tara deve ter entre 2 e 80 caracteres.");
        }

        if (tara.Tamanho.Length > TaraCadastro.TamanhoMaximoTamanho)
        {
            return ResultadoOperacao.Falha("Tamanho da tara deve ter no máximo 80 caracteres.");
        }

        if (tara.Observacao.Length > TaraCadastro.TamanhoMaximoObservacao)
        {
            return ResultadoOperacao.Falha("Observação da tara deve ter no máximo 255 caracteres.");
        }

        if (tara.PesoKg <= 0m)
        {
            return ResultadoOperacao.Falha("Informe um peso de tara válido em KG, maior que zero.");
        }

        // Ajuste 4: no máximo 3 casas decimais (numeric(14,3)); NÃO arredonda silenciosamente — bloqueia.
        if (tara.PesoKg != Math.Round(tara.PesoKg, 3))
        {
            return ResultadoOperacao.Falha("Peso da tara deve ter no máximo 3 casas decimais.");
        }

        return null;
    }
}


