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
            // Ajuste 7/11: reativar respeitando duplicidade GLOBAL (não pode existir OUTRA tara com o mesmo
            // nome no mesmo setor+tipo, ativa OU inativa).
            TaraCadastro? tara = await _taraRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (tara is null)
            {
                return ResultadoOperacao.Falha("Tara nao encontrada.");
            }

            if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(tara.NomeTara, tara.CodigoSetor, tara.CodigoTipoTara, id, cancellationToken))
            {
                return ResultadoOperacao.Falha("Já existe outra tara com este nome no mesmo setor e tipo. Não é possível reativar este registro.");
            }

            int reativados = await _taraRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Tara nao encontrada ou ja estava ativa.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tara reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
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

        // Até 3 casas decimais (coerente com numeric(14,3)); mantém KG (nunca gramas).
        tara.PesoKg = Math.Round(tara.PesoKg, 3, MidpointRounding.AwayFromZero);
        return null;
    }
}


