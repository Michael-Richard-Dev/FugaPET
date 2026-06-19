using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class UsuarioSetorServico
{
    private const string Entidade = "USUARIO_SETOR";
    private const string Tela = "CadastroUsuarioForm";

    private readonly UsuarioSetorRepositorio _usuarioSetorRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public UsuarioSetorServico(UsuarioSetorRepositorio usuarioSetorRepositorio, AuditoriaServico auditoriaServico)
    {
        _usuarioSetorRepositorio = usuarioSetorRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<UsuarioSetorCadastro>> ListarPorUsuarioAsync(long idUsuario, CancellationToken cancellationToken = default)
        => _usuarioSetorRepositorio.ListarPorUsuarioAsync(idUsuario, cancellationToken);

    public async Task<ResultadoOperacao> VincularSetorAsync(long idUsuario, long idSetor, bool setorPadrao = false, bool ativo = true, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0)
            return ResultadoOperacao.Falha("Usuario invalido para vinculo de setor.");

        if (idSetor <= 0)
            return ResultadoOperacao.Falha("Setor invalido para vinculo.");

        try
        {
            // Fluxo atomico no repositorio: limpar padrao + reativar/inserir numa unica transacao.
            ResultadoVinculoSetor resultado = await _usuarioSetorRepositorio.VincularComExclusividadeAsync(
                idUsuario, idSetor, setorPadrao, ativo, cancellationToken);

            switch (resultado.Tipo)
            {
                case TipoAplicacaoVinculoSetor.JaVinculadoAtivo:
                    return ResultadoOperacao.Falha("Este setor ja esta vinculado ao usuario.");

                case TipoAplicacaoVinculoSetor.Reativado:
                    await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, idUsuario, $"Setor {idSetor} reativado no usuario {idUsuario}", Tela, cancellationToken);
                    return ResultadoOperacao.Ok("Setor vinculado com sucesso.");

                case TipoAplicacaoVinculoSetor.Inserido:
                    if (resultado.IdVinculo <= 0) return ResultadoOperacao.Falha("Nao foi possivel vincular o setor ao usuario.");
                    await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, resultado.IdVinculo, $"Setor {idSetor} vinculado ao usuario {idUsuario}", Tela, cancellationToken);
                    return ResultadoOperacao.Ok("Setor vinculado com sucesso.");

                default:
                    return ResultadoOperacao.Falha("Nao foi possivel vincular o setor ao usuario.");
            }
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> DefinirSetorPadraoAsync(long idUsuario, long idSetor, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0 || idSetor <= 0)
            return ResultadoOperacao.Falha("Dados invalidos para definir setor padrao.");

        try
        {
            // Fluxo atomico no repositorio: validar + limpar padrao anterior + marcar novo numa unica transacao.
            ResultadoDefinirSetorPadrao resultado = await _usuarioSetorRepositorio.DefinirSetorPadraoExclusivoAsync(
                idUsuario, idSetor, cancellationToken);

            if (!resultado.VinculoAtivoEncontrado)
                return ResultadoOperacao.Falha("O setor informado nao esta vinculado/ativo no usuario.");
            if (resultado.Atualizados <= 0)
                return ResultadoOperacao.Falha("Nao foi possivel definir o setor padrao.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, idUsuario, $"Setor padrao do usuario {idUsuario} definido como {idSetor}", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Setor padrao definido com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> RemoverSetorAsync(long idUsuario, long idSetor, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0 || idSetor <= 0)
            return ResultadoOperacao.Falha("Dados invalidos para remover vinculo de setor.");

        try
        {
            int removidos = await _usuarioSetorRepositorio.RemoverAsync(idUsuario, idSetor, cancellationToken);
            if (removidos <= 0) return ResultadoOperacao.Falha("Vinculo de setor nao encontrado para remocao.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, idUsuario, $"Setor {idSetor} removido do usuario {idUsuario}", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Setor removido com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}
