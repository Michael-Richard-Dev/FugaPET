namespace FugaPET_Dev.Modelo.Entrada;

public sealed class EntradaProdutoItemEmMemoria
{
    private readonly List<EntradaProdutoLoteEmMemoria> _lotes = [];
    private string _numeroItemSap = string.Empty;
    private string _material = string.Empty;

    public EntradaProdutoItemEmMemoria(string numeroItemSap, string material)
    {
        NumeroItemSap = numeroItemSap;
        Material = NormalizarMaterialObrigatorio(material);
    }

    public string NumeroItemSap
    {
        get => _numeroItemSap;
        private set => _numeroItemSap = NormalizarNumeroItemSap(value);
    }

    public string Material
    {
        get => _material;
        private set => _material = value;
    }

    public IReadOnlyList<EntradaProdutoLoteEmMemoria> Lotes => _lotes.AsReadOnly();
    public Guid? CodigoLoteAtivoLocal { get; private set; }

    public EntradaProdutoLoteEmMemoria? ObterLoteAtivo()
        => CodigoLoteAtivoLocal is Guid codigo
            ? _lotes.FirstOrDefault(l => l.CodigoLocal == codigo)
            : null;

    public void DefinirLoteAtivo(Guid codigoLocal)
    {
        if (codigoLocal == Guid.Empty)
        {
            throw new ArgumentException("Código local do lote ativo não pode ser vazio.", nameof(codigoLocal));
        }

        if (!_lotes.Any(l => l.CodigoLocal == codigoLocal))
        {
            throw new ArgumentException("Lote ativo não pertence ao item informado.", nameof(codigoLocal));
        }

        CodigoLoteAtivoLocal = codigoLocal;
    }

    public EntradaProdutoLoteEmMemoria AdicionarLote(DadosLoteEntrada dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (LocalizarLote(dados.NumeroLote, dados.DataFabricacao, dados.DataVencimento) is not null)
        {
            throw new InvalidOperationException("Lote já existe para este item com o mesmo número e datas.");
        }

        if (PossuiLoteComMesmoNumeroEDatasDiferentes(dados.NumeroLote, dados.DataFabricacao, dados.DataVencimento))
        {
            throw new InvalidOperationException(
                "Já existe lote com este número para o item, mas com datas diferentes. Confira fabricação e vencimento.");
        }

        EntradaProdutoLoteEmMemoria lote = new(dados);
        _lotes.Add(lote);
        CodigoLoteAtivoLocal = lote.CodigoLocal;
        return lote;
    }

    public bool RemoverLote(Guid codigoLocal)
    {
        if (codigoLocal == Guid.Empty)
        {
            return false;
        }

        EntradaProdutoLoteEmMemoria? lote = _lotes.FirstOrDefault(l => l.CodigoLocal == codigoLocal);
        if (lote is null)
        {
            return false;
        }

        bool removido = _lotes.Remove(lote);
        if (removido && CodigoLoteAtivoLocal == codigoLocal)
        {
            CodigoLoteAtivoLocal = _lotes.LastOrDefault()?.CodigoLocal;
        }

        return removido;
    }

    public EntradaProdutoLoteEmMemoria? LocalizarLote(string? numero, DateTime fabricacao, DateTime vencimento)
    {
        string numeroNormalizado = DadosLoteEntrada.NormalizarNumeroLote(numero);
        DateTime fabricacaoNormalizada = fabricacao.Date;
        DateTime vencimentoNormalizado = vencimento.Date;

        return _lotes.FirstOrDefault(l =>
            string.Equals(l.Dados.NumeroLote, numeroNormalizado, StringComparison.Ordinal)
            && l.Dados.DataFabricacao == fabricacaoNormalizada
            && l.Dados.DataVencimento == vencimentoNormalizado);
    }

    public bool PossuiLoteComMesmoNumeroEDatasDiferentes(string? numero, DateTime fabricacao, DateTime vencimento)
    {
        string numeroNormalizado = DadosLoteEntrada.NormalizarNumeroLote(numero);
        DateTime fabricacaoNormalizada = fabricacao.Date;
        DateTime vencimentoNormalizado = vencimento.Date;

        return _lotes.Any(l =>
            string.Equals(l.Dados.NumeroLote, numeroNormalizado, StringComparison.Ordinal)
            && (l.Dados.DataFabricacao != fabricacaoNormalizada
                || l.Dados.DataVencimento != vencimentoNormalizado));
    }

    public void CompatibilizarMaterial(string material)
    {
        string materialNormalizado = NormalizarMaterialObrigatorio(material);
        if (string.IsNullOrWhiteSpace(_material))
        {
            _material = materialNormalizado;
            return;
        }

        if (!string.Equals(_material, materialNormalizado, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Item SAP já está vinculado a outro material.");
        }
    }

    public static string NormalizarNumeroItemSap(string? numeroItemSap)
    {
        string valor = (numeroItemSap ?? string.Empty).Trim();
        if (valor.Length == 0)
        {
            throw new ArgumentException("Número do item SAP é obrigatório.", nameof(numeroItemSap));
        }

        if (!valor.All(char.IsDigit) || valor.Length > 5)
        {
            throw new ArgumentException("Número do item SAP deve ser numérico com até cinco dígitos.", nameof(numeroItemSap));
        }

        return valor.PadLeft(5, '0');
    }

    public static string NormalizarMaterialObrigatorio(string? material)
    {
        string valor = (material ?? string.Empty).Trim();
        if (valor.Length == 0)
        {
            throw new ArgumentException("Material do item é obrigatório.", nameof(material));
        }

        return valor;
    }
}