using System.Collections.ObjectModel;

namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>Lote operacional mantido em memória até a finalização local do recebimento.</summary>
public sealed class EntradaProdutoLoteEmMemoria
{
    private readonly List<EntradaProdutoPesagemEmMemoria> _pesagens = [];

    public EntradaProdutoLoteEmMemoria(
        DadosLoteEntrada dados,
        Guid? codigoLocal = null,
        Guid? correlationId = null,
        EstadoOperacionalLoteEntrada estado = EstadoOperacionalLoteEntrada.AguardandoDados)
    {
        Dados = dados ?? throw new ArgumentNullException(nameof(dados));
        CodigoLocal = ValidarOuGerarGuid(codigoLocal, nameof(codigoLocal));
        CorrelationId = ValidarOuGerarGuid(correlationId, nameof(correlationId));
        ValidarEstadoDefinido(estado);
        Estado = estado;
    }

    public Guid CodigoLocal { get; }
    public DadosLoteEntrada Dados { get; private set; }
    public Guid CorrelationId { get; }
    public EstadoOperacionalLoteEntrada Estado { get; private set; }
    public IReadOnlyList<EntradaProdutoPesagem> Pesagens
        => new ReadOnlyCollection<EntradaProdutoPesagem>(_pesagens.Select(p => p.Pesagem).ToList());
    public IReadOnlyList<EntradaProdutoPesagemEmMemoria> PesagensComCodigoLocal => _pesagens.AsReadOnly();

    public decimal PesoLiquidoTotalMemoriaKg => _pesagens
        .Where(p => string.Equals(p.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusValida, StringComparison.OrdinalIgnoreCase))
        .Sum(p => p.Pesagem.PesoLiquidoKg);

    public bool PossuiPesagem => _pesagens.Count > 0;
    public bool PossuiPesagemValida => _pesagens.Any(p =>
        string.Equals(p.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusValida, StringComparison.OrdinalIgnoreCase));
    public bool PodeEditarDados => !PossuiPesagem && Estado == EstadoOperacionalLoteEntrada.AguardandoDados;

    public bool PodeReceberPesagem
        => Estado is EstadoOperacionalLoteEntrada.LoteConfirmado or EstadoOperacionalLoteEntrada.Pesando;

    public void Confirmar()
        => AlterarEstado(EstadoOperacionalLoteEntrada.LoteConfirmado);

    public void IniciarPesagem()
        => AlterarEstado(EstadoOperacionalLoteEntrada.Pesando);

    public void FinalizarEmMemoria()
    {
        if (Estado != EstadoOperacionalLoteEntrada.Pesando)
        {
            throw new InvalidOperationException("Lote deve estar em pesagem antes de finalizar em memória.");
        }

        if (!PossuiPesagem)
        {
            throw new InvalidOperationException("Lote não pode ser finalizado sem pesagem.");
        }

        if (!PossuiPesagemValida || PesoLiquidoTotalMemoriaKg <= 0m)
        {
            throw new InvalidOperationException("Lote precisa ter ao menos uma pesagem válida com peso líquido maior que zero.");
        }

        AlterarEstado(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria);
    }

    public void AlterarEstado(EstadoOperacionalLoteEntrada estado)
    {
        ValidarEstadoDefinido(estado);
        if (!TransicaoPermitida(Estado, estado))
        {
            throw new InvalidOperationException(
                $"Transição operacional de lote inválida: {Estado} -> {estado}.");
        }

        Estado = estado;
    }

    public EntradaProdutoPesagemEmMemoria RegistrarPesagem(EntradaProdutoPesagem pesagem)
    {
        ArgumentNullException.ThrowIfNull(pesagem);

        if (Estado == EstadoOperacionalLoteEntrada.LoteConfirmado)
        {
            IniciarPesagem();
        }

        if (!PodeReceberPesagem)
        {
            throw new InvalidOperationException("Lote não está liberado para receber pesagem.");
        }

        if (!EntradaProdutoPesagemCalculos.LeituraTemPesoValido(pesagem.PesoBrutoKg, pesagem.PesoLiquidoKg))
        {
            throw new InvalidOperationException("Pesagem deve ter peso bruto e líquido maiores que zero.");
        }

        if (pesagem.PesoTaraKg < 0m || pesagem.PesoLiquidoKg != EntradaProdutoPesagemCalculos.CalcularPesoLiquido(pesagem.PesoBrutoKg, pesagem.PesoTaraKg))
        {
            throw new InvalidOperationException("Peso líquido deve ser igual ao bruto menos a tara.");
        }

        EntradaProdutoPesagemEmMemoria pesagemEmMemoria = EntradaProdutoPesagemEmMemoria.Criar(pesagem);
        _pesagens.Add(pesagemEmMemoria);
        return pesagemEmMemoria;
    }

    public int CancelarPesagens()
    {
        if (Estado == EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
        {
            throw new InvalidOperationException("Pesagens de lote finalizado em memória não podem ser canceladas.");
        }

        int canceladas = 0;
        for (int indice = 0; indice < _pesagens.Count; indice++)
        {
            EntradaProdutoPesagemEmMemoria pesagem = _pesagens[indice];
            if (string.Equals(pesagem.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusValida, StringComparison.OrdinalIgnoreCase))
            {
                _pesagens[indice] = pesagem.ComStatus(EntradaProdutoPesagemCalculos.StatusCancelada);
                canceladas++;
            }
        }

        return canceladas;
    }

    public EntradaProdutoPesagemEmMemoria CancelarPesagem(Guid codigoLocalPesagem)
    {
        if (codigoLocalPesagem == Guid.Empty)
        {
            throw new ArgumentException("Identificador local da pesagem nÃ£o pode ser vazio.", nameof(codigoLocalPesagem));
        }

        if (Estado == EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
        {
            throw new InvalidOperationException("Pesagens de lote finalizado em memÃ³ria nÃ£o podem ser canceladas.");
        }

        int indice = _pesagens.FindIndex(p => p.CodigoLocalPesagem == codigoLocalPesagem);
        if (indice < 0)
        {
            throw new InvalidOperationException("Pesagem nÃ£o pertence ao lote ativo informado.");
        }

        EntradaProdutoPesagemEmMemoria pesagem = _pesagens[indice];
        if (string.Equals(pesagem.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusCancelada, StringComparison.OrdinalIgnoreCase))
        {
            return pesagem;
        }

        EntradaProdutoPesagemEmMemoria cancelada = pesagem.ComStatus(EntradaProdutoPesagemCalculos.StatusCancelada);
        _pesagens[indice] = cancelada;
        return cancelada;
    }

    private static Guid ValidarOuGerarGuid(Guid? valor, string nomeParametro)
    {
        if (!valor.HasValue)
        {
            return Guid.NewGuid();
        }

        if (valor.Value == Guid.Empty)
        {
            throw new ArgumentException("Identificador local não pode ser vazio.", nomeParametro);
        }

        return valor.Value;
    }

    private static bool TransicaoPermitida(
        EstadoOperacionalLoteEntrada atual,
        EstadoOperacionalLoteEntrada proximo)
        => (atual, proximo) switch
        {
            (EstadoOperacionalLoteEntrada.AguardandoDados, EstadoOperacionalLoteEntrada.LoteConfirmado) => true,
            (EstadoOperacionalLoteEntrada.LoteConfirmado, EstadoOperacionalLoteEntrada.Pesando) => true,
            (EstadoOperacionalLoteEntrada.Pesando, EstadoOperacionalLoteEntrada.FinalizadoEmMemoria) => true,
            _ when atual == proximo => true,
            _ => false
        };

    private static void ValidarEstadoDefinido(EstadoOperacionalLoteEntrada estado)
    {
        if (!Enum.IsDefined(estado))
        {
            throw new ArgumentOutOfRangeException(nameof(estado), "Estado operacional de lote inválido.");
        }
    }
}
