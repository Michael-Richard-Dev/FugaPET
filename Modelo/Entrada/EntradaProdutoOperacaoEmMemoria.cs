using System.Collections.ObjectModel;

namespace FugaPET_Dev.Modelo.Entrada;

public sealed class EntradaProdutoOperacaoEmMemoria
{
    private readonly Dictionary<string, EntradaProdutoItemEmMemoria> _itensPorNumeroItemSap = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, EntradaProdutoItemEmMemoria> ItensPorNumeroItemSap => new ReadOnlyDictionary<string, EntradaProdutoItemEmMemoria>(_itensPorNumeroItemSap);
    public string? NumeroItemSapSelecionado { get; private set; }

    public EntradaProdutoItemEmMemoria ObterOuCriarItem(string numeroItemSap, string material)
    {
        string chave = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItemSap);
        string materialNormalizado = EntradaProdutoItemEmMemoria.NormalizarMaterialObrigatorio(material);

        if (!_itensPorNumeroItemSap.TryGetValue(chave, out EntradaProdutoItemEmMemoria? item))
        {
            item = new EntradaProdutoItemEmMemoria(chave, materialNormalizado);
            _itensPorNumeroItemSap[chave] = item;
            return item;
        }

        item.CompatibilizarMaterial(materialNormalizado);
        return item;
    }

    public EntradaProdutoItemEmMemoria SelecionarItem(string numeroItemSap, string material)
    {
        EntradaProdutoItemEmMemoria item = ObterOuCriarItem(numeroItemSap, material);
        NumeroItemSapSelecionado = item.NumeroItemSap;
        return item;
    }

    public EntradaProdutoItemEmMemoria SelecionarItemExistente(string numeroItemSap)
    {
        string chave = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItemSap);
        if (!_itensPorNumeroItemSap.TryGetValue(chave, out EntradaProdutoItemEmMemoria? item))
        {
            throw new InvalidOperationException("Item SAP ainda não existe na operação em memória.");
        }

        NumeroItemSapSelecionado = item.NumeroItemSap;
        return item;
    }

    public EntradaProdutoItemEmMemoria? ObterItemSelecionado()
        => NumeroItemSapSelecionado is not null
            && _itensPorNumeroItemSap.TryGetValue(NumeroItemSapSelecionado, out EntradaProdutoItemEmMemoria? item)
                ? item
                : null;

    public EntradaProdutoLoteEmMemoria? ObterLoteAtivoDoItem(string numeroItemSap)
    {
        string chave = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItemSap);
        return _itensPorNumeroItemSap.TryGetValue(chave, out EntradaProdutoItemEmMemoria? item)
            ? item.ObterLoteAtivo()
            : null;
    }

    public IReadOnlyList<EntradaProdutoLoteEmMemoria> ListarTodosOsLotes()
        => _itensPorNumeroItemSap.Values.SelectMany(i => i.Lotes).ToList();

    public void Limpar()
    {
        _itensPorNumeroItemSap.Clear();
        NumeroItemSapSelecionado = null;
    }
}