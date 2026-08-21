using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Repositorio;

/// <summary>
/// GATE 046-E §14: atomicidade de <see cref="ProdutoAcabadoPipelinePostgresStore.CriarPaleteComCaixasAsync"/>.
/// Um executor FAKE simula a transação (mesma conexão/mesma transação): as chamadas de função ficam PENDENTES até
/// o COMMIT; qualquer exceção antes do COMMIT descarta tudo (ROLLBACK). Sem banco real.
/// </summary>
public sealed class ProdutoAcabadoPaleteAtomicidadeStoreTests
{
    private const long Usuario = 7;
    private const string Terminal = "T-ATOM";
    private const string Material = "PACK_REAL_001";

    [Fact] // A) criar + 3 vínculos OK → commit → sucesso
    public async Task CriarComTresVinculos_TodosOk_Commit()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 900 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal);

        Assert.True(r.Sucesso);
        Assert.Equal(900, r.CodigoHuPalete);
        Assert.Equal(1, exec.Commits);
        Assert.Equal(0, exec.Rollbacks);
        // 1 criar + 3 vínculos efetivamente COMMITADOS.
        Assert.Equal("fn_pa_045_palete_criar", exec.Comitadas[0]);
        Assert.Equal(3, exec.Comitadas.Count(f => f == "fn_pa_045_palete_vincular_caixa"));
    }

    [Fact] // B) falha no primeiro vínculo → rollback → nada permanece
    public async Task FalhaPrimeiroVinculo_Rollback_NadaPermanece()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, VinculoFalhaNoCodigo = 101 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(0, exec.Commits);
        Assert.Equal(1, exec.Rollbacks);
        Assert.Empty(exec.Comitadas);
    }

    [Fact] // C) falha no vínculo intermediário → rollback integral
    public async Task FalhaVinculoIntermediario_RollbackIntegral()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, VinculoFalhaNoCodigo = 102 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(0, exec.Commits);
        Assert.Equal(1, exec.Rollbacks);
        Assert.Empty(exec.Comitadas);
    }

    [Fact] // D) falha no último vínculo → rollback integral
    public async Task FalhaUltimoVinculo_RollbackIntegral()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, VinculoFalhaNoCodigo = 103 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(0, exec.Commits);
        Assert.Equal(1, exec.Rollbacks);
        Assert.Empty(exec.Comitadas);
    }

    [Fact] // Criação negada pelo banco → rollback, nenhum vínculo
    public async Task CriacaoNegada_Rollback_SemVinculo()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 0 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102], Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(1, exec.Rollbacks);
        Assert.Empty(exec.Comitadas);
        Assert.DoesNotContain("fn_pa_045_palete_vincular_caixa", exec.Pendentes);
    }

    [Fact] // E) exception PostgreSQL → rollback
    public async Task ExcecaoInfra_Rollback()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, LancarNoVinculoDoCodigo = 102 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(1, exec.Rollbacks);
        Assert.Empty(exec.Comitadas);
    }

    [Fact] // F) cancelamento antes do commit → rollback
    public async Task CancelamentoAntesCommit_Rollback()
    {
        using CancellationTokenSource cts = new();
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, CancelarNoVinculoDoCodigo = 102, Cts = cts };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal, cts.Token);

        Assert.False(r.Sucesso);
        Assert.Equal(0, exec.Commits);
        Assert.Equal(1, exec.Rollbacks);
        Assert.Empty(exec.Comitadas);
    }

    [Fact] // REV2 §4) FRONTEIRA EXATA: criar OK + todos os vínculos OK + operação terminou; token cancelado
           // ANTES do CommitAsync ⇒ COMMIT não ocorre, rollback ocorre, resultado fail-closed, nada permanece.
    public async Task CancelamentoEntreUltimaOperacaoECommit_Rollback()
    {
        using CancellationTokenSource cts = new();
        // Sem falha/cancelamento DURANTE os vínculos: a operação completa 100%. O cancelamento acontece só na
        // fronteira, após a última operação e antes do commit (CancelarAposOperacaoAntesCommit).
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, CancelarAposOperacaoAntesCommit = cts };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102, 103], Usuario, Terminal, cts.Token);

        Assert.False(r.Sucesso);
        Assert.Equal(0, exec.Commits);   // COMMIT NÃO ocorreu
        Assert.Equal(1, exec.Rollbacks); // Rollback ocorreu
        Assert.Empty(exec.Comitadas);    // palete e vínculos NÃO permanecem
        Assert.Equal(1, exec.Transacoes); // uma única transação foi aberta (e revertida)
    }

    [Fact] // REV2: o executor Npgsql REAL reavalia o token ANTES do CommitAsync (ordem certificável na fonte).
    public void ExecutorReal_ThrowIfCancellation_AntesDoCommit()
    {
        string src = File.ReadAllText(CaminhoFonte("AcessoDados", "Repositorio", "ProdutoAcabadoPipeline045NpgsqlExecutor.cs"));
        int fimOperacao = src.IndexOf("await operacao(executorTx, cancellationToken);", StringComparison.Ordinal);
        int throwIf = src.IndexOf("cancellationToken.ThrowIfCancellationRequested();", StringComparison.Ordinal);
        int commit = src.IndexOf("transacao.CommitAsync(CancellationToken.None)", StringComparison.Ordinal);
        Assert.True(fimOperacao >= 0 && throwIf >= 0 && commit >= 0);
        Assert.True(fimOperacao < throwIf, "ThrowIfCancellationRequested deve vir DEPOIS da última operação.");
        Assert.True(throwIf < commit, "ThrowIfCancellationRequested deve vir ANTES do CommitAsync.");
    }

    private static string CaminhoFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return Path.Combine(dir, Path.Combine(partes));
    }

    [Fact] // G) commit concluído → operação considerada persistida (cancelamento posterior não reverte)
    public async Task CommitConcluido_CancelamentoPosterior_NaoReverte()
    {
        using CancellationTokenSource cts = new();
        FakeExecutorTransacional exec = new() { CodigoCriado = 900, CancelarTokenAposCommit = cts };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            Material, "3007", "PP01", 30m, 27m, 3m, [101, 102], Usuario, Terminal, cts.Token);

        Assert.True(r.Sucesso);
        Assert.Equal(900, r.CodigoHuPalete);
        Assert.Equal(1, exec.Commits);
        Assert.Equal(0, exec.Rollbacks);
        Assert.True(cts.IsCancellationRequested); // token foi cancelado APÓS o commit, mas o resultado é sucesso
    }

    [Fact] // Material vazio ⇒ zero transação
    public async Task MaterialVazio_ZeroTransacao()
    {
        FakeExecutorTransacional exec = new() { CodigoCriado = 900 };
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ResultadoCriacaoPaleteLocalAtomica r = await store.CriarPaleteComCaixasAsync(
            "   ", "3007", "PP01", 30m, 27m, 3m, [101], Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(0, exec.Transacoes);
    }

    /// <summary>
    /// Executor fake com semântica transacional: funções chamadas dentro da operação ficam em <see cref="Pendentes"/>;
    /// se a operação retorna normalmente ⇒ COMMIT (Pendentes → <see cref="Comitadas"/>); se lança ⇒ ROLLBACK (descarta).
    /// </summary>
    private sealed class FakeExecutorTransacional : IProdutoAcabadoPipeline045Executor
    {
        public bool Disponivel => true;
        public long CodigoCriado { get; set; } = 900;
        public long? VinculoFalhaNoCodigo { get; set; }
        public long? LancarNoVinculoDoCodigo { get; set; }
        public long? CancelarNoVinculoDoCodigo { get; set; }
        public CancellationTokenSource? Cts { get; set; }
        public CancellationTokenSource? CancelarTokenAposCommit { get; set; }
        // REV2: simula token cancelado APÓS a última operação e ANTES do commit (fronteira exata do Blocker 1).
        public CancellationTokenSource? CancelarAposOperacaoAntesCommit { get; set; }

        public int Transacoes { get; private set; }
        public int Commits { get; private set; }
        public int Rollbacks { get; private set; }
        public List<string> Pendentes { get; } = [];
        public List<string> Comitadas { get; } = [];

        public async Task<T> ExecutarEmTransacaoAsync<T>(
            Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao, CancellationToken cancellationToken = default)
        {
            Transacoes++;
            Pendentes.Clear();
            TxExecutor tx = new(this);
            try
            {
                T r = await operacao(tx, cancellationToken);
                // Fronteira exata: token pode ser cancelado APÓS a última operação e ANTES do commit.
                CancelarAposOperacaoAntesCommit?.Cancel();
                // REV2 Blocker 1 (MESMA regra do executor Npgsql real): reavaliar o token ANTES de iniciar o commit.
                cancellationToken.ThrowIfCancellationRequested();
                Comitadas.AddRange(Pendentes); // COMMIT: torna visível
                Commits++;
                CancelarTokenAposCommit?.Cancel(); // §G: cancelamento POSTERIOR ao commit não deve reverter
                return r;
            }
            catch
            {
                Pendentes.Clear(); // ROLLBACK: descarta tudo
                Rollbacks++;
                throw;
            }
        }

        private sealed class TxExecutor : IExecutorFuncoes045Transacional
        {
            private readonly FakeExecutorTransacional _dono;
            public TxExecutor(FakeExecutorTransacional dono) => _dono = dono;

            public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(
                string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
            {
                _dono.Pendentes.Add(funcao);
                if (funcao == "fn_pa_045_palete_criar")
                {
                    return Task.FromResult<IReadOnlyList<Linha045>>(
                        [new Linha045(new Dictionary<string, object?> { ["codigo_hu_palete"] = _dono.CodigoCriado })]);
                }

                // fn_pa_045_palete_vincular_caixa: parametros posicionais [palete, caixa, seq, usuario, terminal]
                long codigoCaixa = parametros.Count > 1 && parametros[1].Valor is long c ? c : 0;
                if (_dono.LancarNoVinculoDoCodigo == codigoCaixa)
                {
                    throw new InvalidOperationException("Falha infra simulada no vínculo.");
                }
                if (_dono.CancelarNoVinculoDoCodigo == codigoCaixa)
                {
                    _dono.Cts?.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                bool ok = _dono.VinculoFalhaNoCodigo != codigoCaixa;
                return Task.FromResult<IReadOnlyList<Linha045>>(
                    [new Linha045(new Dictionary<string, object?> { ["fn_pa_045_palete_vincular_caixa"] = ok })]);
            }
        }

        // Métodos não usados por estes testes (fail-closed).
        public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(string numeroOrdemProducao, string? terminal, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Linha045>>([]);
    }
}
