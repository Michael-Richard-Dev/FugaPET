using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// GATE 054 — EXCLUIR PESAGEM: contrato do serviço (permissão EXATA sem fallback, orquestração, mapeamento de
/// cenário) + provas estáticas do repositório (SERIALIZABLE, guard SAP, lock order, cancelamento lógico,
/// cascata de vazio, ZERO DELETE físico, ZERO SAP). Sem DB Q real.
/// </summary>
public sealed class ExclusaoPesagemServico054Tests
{
    private sealed class FakeRepo : IExclusaoPesagemRepositorio
    {
        public CenarioExclusaoPesagem Cenario = CenarioExclusaoPesagem.Excluida;
        public bool ArvoreVazia;
        public bool Chamado;
        public long UltimoCodigo;
        public long UltimoUsuario;
        public Exception? Lancar;

        public Task<ResultadoExclusaoPesagemRepositorio> ExcluirPesagemLocalAsync(
            long codigoPesagem, long codigoUsuario, CancellationToken cancellationToken)
        {
            Chamado = true;
            UltimoCodigo = codigoPesagem;
            UltimoUsuario = codigoUsuario;
            if (Lancar is not null)
            {
                throw Lancar;
            }
            return Task.FromResult(new ResultadoExclusaoPesagemRepositorio(Cenario, ArvoreVazia));
        }
    }

    private static ExclusaoPesagemServico Servico(FakeRepo repo, bool autorizado, long? usuario)
        => new(repo, usuarioAutorizado: () => autorizado, codigoUsuarioLogado: () => usuario);

    private static string FonteRepositorio()
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 9 && raiz is not null; i++)
        {
            foreach (string cand in new[]
            {
                Path.Combine(raiz, "AcessoDados", "Repositorio", "ExclusaoPesagemRepositorio.cs"),
                Path.Combine(raiz, "FugaPet_HML", "AcessoDados", "Repositorio", "ExclusaoPesagemRepositorio.cs")
            })
            {
                if (File.Exists(cand)) return File.ReadAllText(cand);
            }
            raiz = Directory.GetParent(raiz)?.FullName!;
        }
        throw new FileNotFoundException("ExclusaoPesagemRepositorio.cs");
    }

    // ---------- serviço: permissão + orquestração + mapeamento ----------

    [Fact] // usuário autorizado + repo Excluída → sucesso; repo chamado com PK e usuário.
    public async Task Autorizado_Exclui_ComSucesso()
    {
        FakeRepo repo = new() { Cenario = CenarioExclusaoPesagem.Excluida, ArvoreVazia = true };
        ResultadoExclusaoPesagem r = await Servico(repo, autorizado: true, usuario: 7).ExcluirAsync(42);
        Assert.True(r.Sucesso);
        Assert.True(r.ArvoreVazia);
        Assert.True(repo.Chamado);
        Assert.Equal(42, repo.UltimoCodigo);
        Assert.Equal(7, repo.UltimoUsuario);
    }

    [Fact] // sem permissão EXCLUIR_PESAGEM → SemPermissao e o repositório NÃO é chamado.
    public async Task SemPermissao_NaoChamaRepositorio()
    {
        FakeRepo repo = new();
        ResultadoExclusaoPesagem r = await Servico(repo, autorizado: false, usuario: 7).ExcluirAsync(42);
        Assert.Equal(CenarioExclusaoPesagem.SemPermissao, r.Cenario);
        Assert.False(repo.Chamado);
    }

    [Fact] // autorizado porém sem sessão (usuário nulo) → SemPermissao, repo NÃO chamado.
    public async Task SemUsuarioLogado_Bloqueia()
    {
        FakeRepo repo = new();
        ResultadoExclusaoPesagem r = await Servico(repo, autorizado: true, usuario: null).ExcluirAsync(42);
        Assert.Equal(CenarioExclusaoPesagem.SemPermissao, r.Cenario);
        Assert.False(repo.Chamado);
    }

    [Fact] // pesagem inexistente por PK inválida → Inexistente sem tocar o repositório.
    public async Task CodigoInvalido_Inexistente()
    {
        FakeRepo repo = new();
        ResultadoExclusaoPesagem r = await Servico(repo, autorizado: true, usuario: 7).ExcluirAsync(0);
        Assert.Equal(CenarioExclusaoPesagem.PesagemInexistente, r.Cenario);
        Assert.False(repo.Chamado);
    }

    [Theory] // cada cenário do repositório é mapeado 1:1 para o resultado do serviço.
    [InlineData(CenarioExclusaoPesagem.PesagemInexistente)]
    [InlineData(CenarioExclusaoPesagem.JaCancelada)]
    [InlineData(CenarioExclusaoPesagem.EstadoMudou)]
    [InlineData(CenarioExclusaoPesagem.BloqueadoSap)]
    [InlineData(CenarioExclusaoPesagem.FalhaTecnica)]
    public async Task MapeiaCenarioDoRepositorio(CenarioExclusaoPesagem cenario)
    {
        FakeRepo repo = new() { Cenario = cenario };
        ResultadoExclusaoPesagem r = await Servico(repo, autorizado: true, usuario: 7).ExcluirAsync(42);
        Assert.Equal(cenario, r.Cenario);
        Assert.False(r.Sucesso);
    }

    [Fact] // exceção do repositório vira FalhaTecnica (sem vazar).
    public async Task ExcecaoRepositorio_FalhaTecnica()
    {
        FakeRepo repo = new() { Lancar = new InvalidOperationException("x") };
        ResultadoExclusaoPesagem r = await Servico(repo, autorizado: true, usuario: 7).ExcluirAsync(42);
        Assert.Equal(CenarioExclusaoPesagem.FalhaTecnica, r.Cenario);
    }

    [Fact] // a permissão usada é EXATAMENTE PROCESSO_PRODUCAO/ENTRADA_PRODUTO/EXCLUIR_PESAGEM.
    public void PermissaoExata()
    {
        Assert.Equal("EXCLUIR_PESAGEM", PermissoesSistema.Acoes.ExcluirPesagem);
    }

    // ---------- provas estáticas do repositório (contrato GATE 052) ----------

    [Fact] // SERIALIZABLE + SET LOCAL app.usuario_id + lock order LANCAMENTO→ITEM→LOTE→PESAGEM (FOR UPDATE).
    public void Repositorio_Serializable_LockOrder()
    {
        string src = FonteRepositorio();
        Assert.Contains("IsolationLevel.Serializable", src, StringComparison.Ordinal);
        Assert.Contains("DefinirUsuarioAppAsync", src, StringComparison.Ordinal); // set_config('app.usuario_id',...)
        int lanc = src.IndexOf("BloquearLancamentoAsync", StringComparison.Ordinal);
        int item = src.IndexOf("BloquearItemElegivelAsync", StringComparison.Ordinal);
        int lote = src.IndexOf("BloquearLoteElegivelAsync", StringComparison.Ordinal);
        int pes = src.IndexOf("BloquearPesagemValidaAsync", StringComparison.Ordinal);
        Assert.True(lanc >= 0 && lanc < item && item < lote && lote < pes, "ordem de lock incorreta");
        Assert.Contains("FOR UPDATE", src, StringComparison.Ordinal);
    }

    [Fact] // guard SAP por correlation_id + sem auto-retry de serialization_failure.
    public void Repositorio_GuardSap_SemAutoRetry()
    {
        string src = FonteRepositorio();
        Assert.Contains("fn_entrada_produto_sap_guard_counts", src, StringComparison.Ordinal);
        Assert.Contains("outbox != 0 || tentativa != 0", src, StringComparison.Ordinal);
        Assert.Contains("\"40001\" or \"40P01\"", src, StringComparison.Ordinal);
        Assert.Contains("CenarioExclusaoPesagem.EstadoMudou", src, StringComparison.Ordinal); // sem retry, retorna mudou
    }

    [Fact] // cancelamento lógico (rowcount==1) + recálculo + cascata de vazio; NUNCA DELETE físico; ZERO SAP.
    public void Repositorio_CancelamentoLogico_Cascata_SemDeleteSemSap()
    {
        string src = FonteRepositorio();
        Assert.Contains("status_pesagem = 'CANCELADA'", src, StringComparison.Ordinal);
        Assert.Contains("cancelados != 1", src, StringComparison.Ordinal);         // rowcount exato
        Assert.Contains("quantidade_recebida = (", src, StringComparison.Ordinal);  // recálculo
        Assert.Contains("CancelarLoteSeVazioAsync", src, StringComparison.Ordinal);
        Assert.Contains("CancelarItemSeVazioAsync", src, StringComparison.Ordinal);
        Assert.Contains("CancelarLancamentoSeVazioAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", src, StringComparison.Ordinal);
        Assert.DoesNotContain("MaterialDocument", src, StringComparison.Ordinal);
    }

    [Fact] // predicado bloqueia estados SAP impeditivos do lançamento (doc/exercício/enviado) e exige FINALIZADO_LOCAL.
    public void Repositorio_PredicadoLancamentoBloqueiaSap()
    {
        string src = FonteRepositorio();
        Assert.Contains("status_lancamento = 'FINALIZADO_LOCAL'", src, StringComparison.Ordinal);
        Assert.Contains("documento_material_sap IS NULL", src, StringComparison.Ordinal);
        Assert.Contains("exercicio_documento_material_sap IS NULL", src, StringComparison.Ordinal);
        Assert.Contains("enviado_sap_em IS NULL", src, StringComparison.Ordinal);
    }
}
