using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class ModeloEtiquetaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.ModeloEtiqueta;
    private const string Tela = "ModeloEtiquetaForm";

    // Mensagens públicas para testes e para consistência de UX com Tara/TipoTara.
    public const string MensagemDuplicidadeGlobal =
        "Já existe um modelo com este nome e versão, mesmo que esteja inativo. Localize o registro existente e utilize a ação Reativar.";
    public const string MensagemInativarPelaAcao =
        "A inativação do modelo deve ser feita pela ação Inativar.";
    public const string MensagemReativarPelaAcao =
        "A reativação do modelo deve ser feita pela ação Reativar.";
    public const string MensagemEtiquetaAtivaVinculada =
        "Não é possível inativar este modelo porque existem etiquetas ativas vinculadas a ele.";
    public const string MensagemNovoDeveSerAtivo =
        "Novo modelo de etiqueta deve ser cadastrado como Ativo. Utilize a ação Inativar após o cadastro, quando necessário.";

    private readonly ModeloEtiquetaRepositorio _repositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public ModeloEtiquetaServico(ModeloEtiquetaRepositorio repositorio, AuditoriaServico auditoriaServico)
    {
        _repositorio = repositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<ModeloEtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _repositorio.ListarAsync(cancellationToken);

    public Task<ModeloEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        // Defesa de servidor: todo novo modelo nasce Ativo. Não corrige false→true silenciosamente; falha explícita
        // (a regra não pode depender só da View).
        if (!modelo.SituacaoModeloEtiqueta)
            return ResultadoOperacao.Falha(MensagemNovoDeveSerAtivo);

        ResultadoOperacao? validacao = ValidarENormalizar(modelo);
        if (validacao is not null) return validacao;

        try
        {
            if (await _repositorio.ExisteNomeVersaoAsync(modelo.NomeModeloEtiqueta, modelo.Versao, null, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            long id = await _repositorio.InserirAsync(modelo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Não foi possível cadastrar o modelo de etiqueta.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Modelo '{modelo.NomeModeloEtiqueta}' v{modelo.Versao}", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Modelo de etiqueta cadastrado com sucesso.", id);
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

    public async Task<ResultadoOperacao> AtualizarAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (modelo.CodigoModeloEtiqueta <= 0)
            return ResultadoOperacao.Falha("Id do modelo inválido para edição.");

        ResultadoOperacao? validacao = ValidarENormalizar(modelo);
        if (validacao is not null) return validacao;

        try
        {
            ModeloEtiquetaCadastro? anterior = await _repositorio.ObterPorIdAsync(modelo.CodigoModeloEtiqueta, cancellationToken);
            if (anterior is null) return ResultadoOperacao.Falha("Modelo não encontrado para edição.");

            // Situação NÃO muda pela edição: bloqueia tentativa de alterá-la e reforça a situação anterior.
            if (anterior.SituacaoModeloEtiqueta && !modelo.SituacaoModeloEtiqueta)
                return ResultadoOperacao.Falha(MensagemInativarPelaAcao);
            if (!anterior.SituacaoModeloEtiqueta && modelo.SituacaoModeloEtiqueta)
                return ResultadoOperacao.Falha(MensagemReativarPelaAcao);
            modelo.SituacaoModeloEtiqueta = anterior.SituacaoModeloEtiqueta;

            if (await _repositorio.ExisteNomeVersaoAsync(modelo.NomeModeloEtiqueta, modelo.Versao, modelo.CodigoModeloEtiqueta, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            int atualizados = await _repositorio.AtualizarAsync(modelo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Modelo não encontrado para edição.");

            string descricao = $"Modelo '{modelo.NomeModeloEtiqueta}' v{modelo.Versao}";
            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, modelo.CodigoModeloEtiqueta, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edição concluída com sucesso.");
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
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do modelo inválido.");

        try
        {
            // Verificação explícita: modelo com etiqueta ativa vinculada não pode ser inativado.
            if (await _repositorio.ExisteEtiquetaAtivaVinculadaAsync(id, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemEtiquetaAtivaVinculada);
            }

            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0)
            {
                // O UPDATE atômico (NOT EXISTS) pode ter retornado 0: diferencia o motivo real.
                ModeloEtiquetaCadastro? atual = await _repositorio.ObterPorIdAsync(id, cancellationToken);
                if (atual is null) return ResultadoOperacao.Falha("Modelo não encontrado.");
                if (!atual.SituacaoModeloEtiqueta) return ResultadoOperacao.Falha("Modelo já estava inativo.");
                if (await _repositorio.ExisteEtiquetaAtivaVinculadaAsync(id, cancellationToken))
                    return ResultadoOperacao.Falha(MensagemEtiquetaAtivaVinculada);
                return ResultadoOperacao.Falha("Não foi possível inativar o modelo. Acione o suporte.");
            }

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Modelo inativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do modelo inválido.");

        try
        {
            ModeloEtiquetaCadastro? modelo = await _repositorio.ObterPorIdAsync(id, cancellationToken);
            if (modelo is null) return ResultadoOperacao.Falha("Modelo não encontrado.");
            if (modelo.SituacaoModeloEtiqueta) return ResultadoOperacao.Falha("Modelo já estava ativo.");

            if (await _repositorio.ExisteNomeVersaoAsync(modelo.NomeModeloEtiqueta, modelo.Versao, id, cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            int reativados = await _repositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0)
            {
                // O UPDATE atômico (NOT EXISTS) pode ter retornado 0: diferencia o motivo real.
                ModeloEtiquetaCadastro? atual = await _repositorio.ObterPorIdAsync(id, cancellationToken);
                if (atual is null) return ResultadoOperacao.Falha("Modelo não encontrado.");
                if (atual.SituacaoModeloEtiqueta) return ResultadoOperacao.Falha("Modelo já estava ativo.");
                if (await _repositorio.ExisteNomeVersaoAsync(atual.NomeModeloEtiqueta, atual.Versao, id, cancellationToken))
                    return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
                return ResultadoOperacao.Falha("Não foi possível reativar o modelo. Acione o suporte.");
            }

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Modelo reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    // Valida e normaliza (Trim de nome/observação; ZPL preservado integralmente — sem Trim).
    private static ResultadoOperacao? ValidarENormalizar(ModeloEtiquetaCadastro modelo)
    {
        modelo.NomeModeloEtiqueta = (modelo.NomeModeloEtiqueta ?? string.Empty).Trim();
        modelo.Observacao = (modelo.Observacao ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(modelo.NomeModeloEtiqueta))
            return ResultadoOperacao.Falha("Nome do modelo é obrigatório.");
        if (modelo.NomeModeloEtiqueta.Length < ModeloEtiquetaCadastro.TamanhoMinimoNome)
            return ResultadoOperacao.Falha($"Nome do modelo deve ter ao menos {ModeloEtiquetaCadastro.TamanhoMinimoNome} caracteres.");
        if (modelo.NomeModeloEtiqueta.Length > ModeloEtiquetaCadastro.TamanhoMaximoNome)
            return ResultadoOperacao.Falha($"Nome do modelo deve ter no máximo {ModeloEtiquetaCadastro.TamanhoMaximoNome} caracteres.");

        if (modelo.Versao <= 0)
            return ResultadoOperacao.Falha("Versão deve ser um inteiro maior que zero.");

        if (!modelo.Dpi.HasValue || modelo.Dpi.Value <= 0)
            return ResultadoOperacao.Falha("DPI deve ser um inteiro maior que zero.");

        if (modelo.LarguraMm.HasValue)
        {
            if (modelo.LarguraMm.Value <= 0)
                return ResultadoOperacao.Falha("Largura deve ser maior que zero.");
            if (modelo.LarguraMm.Value != Math.Round(modelo.LarguraMm.Value, 2))
                return ResultadoOperacao.Falha("Largura e altura devem possuir no máximo duas casas decimais.");
        }

        if (modelo.AlturaMm.HasValue)
        {
            if (modelo.AlturaMm.Value <= 0)
                return ResultadoOperacao.Falha("Altura deve ser maior que zero.");
            if (modelo.AlturaMm.Value != Math.Round(modelo.AlturaMm.Value, 2))
                return ResultadoOperacao.Falha("Largura e altura devem possuir no máximo duas casas decimais.");
        }

        // ZPL obrigatório, mas preservado exatamente como digitado (sem Trim) — só verifica se está vazio.
        if (string.IsNullOrWhiteSpace(modelo.ConteudoZpl))
            return ResultadoOperacao.Falha("Conteúdo ZPL é obrigatório.");

        if (modelo.Observacao.Length > ModeloEtiquetaCadastro.TamanhoMaximoObservacao)
            return ResultadoOperacao.Falha($"Observação deve ter no máximo {ModeloEtiquetaCadastro.TamanhoMaximoObservacao} caracteres.");

        return null;
    }
}
