using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Autorização do Controle de Apontamentos, por AÇÃO. Abstração injetável para permitir teste do
/// bloqueio sem depender da sessão real nem do banco.
/// </summary>
public interface IControleApontamentosAutorizacaoServico
{
    /// <summary>
    /// Decisão ÚNICA e explícita sobre o pacote 039 estar aplicado (as três tabelas existem no
    /// search_path atual). É a mesma decisão usada pela abertura da tela e pela autorização funcional.
    /// </summary>
    Task<bool> EstruturaControleApontamentosDisponivelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// PROCESSO_PRODUCAO / CONTROLE_APONTAMENTOS / VISUALIZAR — abrir a tela e consultar.
    /// NÃO é um OR permanente com a rotina antiga: o fallback para LEITURA_PRODUCAO existe SOMENTE
    /// enquanto a estrutura do 039 não estiver aplicada.
    /// </summary>
    Task<bool> PodeVisualizarAsync(CancellationToken cancellationToken = default);

    /// <summary>PROCESSO_PRODUCAO / CONTROLE_APONTAMENTOS / INICIAR — evento 01. Nunca usa fallback.</summary>
    bool PodeIniciar();

    /// <summary>PROCESSO_PRODUCAO / CONTROLE_APONTAMENTOS / FINALIZAR — evento 02. Nunca usa fallback.</summary>
    bool PodeFinalizar();
}

/// <summary>
/// Implementação real sobre a matriz de permissões, com a decisão de fallback amarrada à EXISTÊNCIA da
/// estrutura do 039 (não à lista de permissões do usuário — um usuário sem acesso não consegue
/// distinguir "rotina ausente" de "rotina existente sem permissão").
///
/// Estrutura AUSENTE (039 não aplicado):
///   VISUALIZAR  -> permissão própria OU fallback LEITURA_PRODUCAO/VISUALIZAR (transitório).
///   INICIAR/FINALIZAR -> negados (a estrutura também não existe; nada a registrar).
///
/// Estrutura PRESENTE (039 aplicado):
///   VISUALIZAR  -> SOMENTE CONTROLE_APONTAMENTOS/VISUALIZAR. LEITURA_PRODUCAO não abre o módulo.
///   INICIAR     -> SOMENTE CONTROLE_APONTAMENTOS/INICIAR.
///   FINALIZAR   -> SOMENTE CONTROLE_APONTAMENTOS/FINALIZAR.
/// </summary>
public sealed class ControleApontamentosAutorizacaoServico : IControleApontamentosAutorizacaoServico
{
    private readonly Func<string, string, string, bool> _possuiPermissao;
    private readonly Func<CancellationToken, Task<bool>> _estruturaDisponivel;

    public ControleApontamentosAutorizacaoServico()
        : this(AutorizacaoServico.PossuiPermissao, ProbeEstruturaPadraoAsync)
    {
    }

    internal ControleApontamentosAutorizacaoServico(
        Func<string, string, string, bool> possuiPermissao,
        Func<CancellationToken, Task<bool>>? estruturaDisponivel = null)
    {
        _possuiPermissao = possuiPermissao ?? throw new ArgumentNullException(nameof(possuiPermissao));
        // Sem probe injetado assume estrutura AUSENTE: mantém o fallback transitório de visualização.
        _estruturaDisponivel = estruturaDisponivel ?? (_ => Task.FromResult(false));
    }

    public Task<bool> EstruturaControleApontamentosDisponivelAsync(CancellationToken cancellationToken = default)
        => _estruturaDisponivel(cancellationToken);

    public async Task<bool> PodeVisualizarAsync(CancellationToken cancellationToken = default)
    {
        // A permissão PRÓPRIA sempre abre — inclusive para quem não tem a rotina antiga.
        if (PossuiAcaoPropria(PermissoesSistema.Acoes.Visualizar))
        {
            return true;
        }

        // Estrutura aplicada: acabou o fallback. LEITURA_PRODUCAO não concede o novo módulo.
        if (await EstruturaControleApontamentosDisponivelAsync(cancellationToken))
        {
            return false;
        }

        // Estrutura ausente: fallback TRANSITÓRIO, só de visualização/consulta.
        return _possuiPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.LeituraProducao,
            PermissoesSistema.Acoes.Visualizar);
    }

    public bool PodeIniciar() => PossuiAcaoPropria(PermissoesSistema.Acoes.Iniciar);

    public bool PodeFinalizar() => PossuiAcaoPropria(PermissoesSistema.Acoes.Finalizar);

    private bool PossuiAcaoPropria(string acao)
        => _possuiPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.ControleApontamentos,
            acao);

    /// <summary>
    /// Probe padrão: pergunta ao repository se as três tabelas do 039 existem. Falha de banco é tratada
    /// como estrutura AUSENTE (conservador: mantém o fallback de visualização e segue bloqueando
    /// início/término, que já dependem da estrutura).
    /// </summary>
    private static async Task<bool> ProbeEstruturaPadraoAsync(CancellationToken cancellationToken)
    {
        try
        {
            ControleApontamentosRepositorio repositorio = new(
                new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar()));
            return await repositorio.EstruturaDisponivelAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            return false;
        }
    }
}
