using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class CargoController
{
    private readonly CargoServico _cargoServico;

    public CargoController(CargoServico cargoServico)
    {
        _cargoServico = cargoServico;
    }

    public Task<IReadOnlyList<CargoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _cargoServico.ListarAsync(cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
        => _cargoServico.InserirAsync(cargo, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
        => _cargoServico.AtualizarAsync(cargo, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long idCargo, CancellationToken cancellationToken = default)
        => _cargoServico.ExcluirAsync(idCargo, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long idCargo, CancellationToken cancellationToken = default)
        => _cargoServico.ReativarAsync(idCargo, cancellationToken);
}
