using System.Collections.Concurrent;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Store do snapshot do pipeline (261/101/HU) por caixa. Persistência definitiva = DEPENDENCIA_GAIA
/// (colunas ainda não entregues). O <see cref="ProdutoAcabadoPipelineStoreMemoria"/> serve testes/execução
/// em memória; o repository real será plugado quando o schema suportar.
/// </summary>
public interface IProdutoAcabadoPipelineStore
{
    ProdutoAcabadoPipelineSnapshot? Obter(long codigoCaixa);
    void Salvar(ProdutoAcabadoPipelineSnapshot snapshot);

    /// <summary>
    /// REV4-§5: true SOMENTE quando o store persiste o estado do pipeline (261/101/HU, tentativas, correlation,
    /// MaterialDocument/Year, timestamps) de forma DEFINITIVA e recuperável entre execuções. O store em memória
    /// retorna false ⇒ a composição runtime NÃO ativa o pipeline SAP produtivo (fail-closed, DEPENDENCIA_GAIA).
    /// </summary>
    bool SuportaPersistenciaDefinitiva { get; }
}

/// <summary>Store em memória (DEPENDENCIA_GAIA para persistência real). Thread-safe. Apenas testes/DEV estrutural.</summary>
public sealed class ProdutoAcabadoPipelineStoreMemoria : IProdutoAcabadoPipelineStore
{
    private readonly ConcurrentDictionary<long, ProdutoAcabadoPipelineSnapshot> _mapa = new();

    /// <summary>REV4-§5: memória NÃO é persistência definitiva ⇒ pipeline produtivo permanece fail-closed.</summary>
    public bool SuportaPersistenciaDefinitiva => false;

    public ProdutoAcabadoPipelineSnapshot? Obter(long codigoCaixa)
        => _mapa.TryGetValue(codigoCaixa, out ProdutoAcabadoPipelineSnapshot? s) ? s : null;

    public void Salvar(ProdutoAcabadoPipelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CodigoProdutoAcabadoCaixa is long codigo)
        {
            _mapa[codigo] = snapshot;
        }
    }
}

/// <summary>Resultado da execução do pipeline (uma passagem).</summary>
public sealed record ResultadoPipelineProdutoAcabado(
    bool ClaimObtido,
    EtapaPipelineProdutoAcabado UltimaEtapa,
    string Mensagem,
    ProdutoAcabadoPipelineSnapshot? Snapshot);
