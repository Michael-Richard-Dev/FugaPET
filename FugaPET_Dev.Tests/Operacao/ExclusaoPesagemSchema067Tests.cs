namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// GATE 067 — reconciliação do ExclusaoPesagemRepositorio com o contrato físico REAL de entrada_produto_lote
/// (o schema NÃO possui situacao_entrada_produto_lote nem entrada_produto_lote_atualizado_por). Provas estáticas
/// sobre a fonte do repositório, sem DB Q real: elimina os dois mismatches (42703) SEM enfraquecer o safe-delete,
/// o guard SAP, a transação SERIALIZABLE, o locking, o cancelamento lógico e a auditoria.
/// </summary>
public sealed class ExclusaoPesagemSchema067Tests
{
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

    // Recorta o corpo textual da DEFINIÇÃO de um método. As definições vêm depois das chamadas neste arquivo,
    // então LastIndexOf ancora na definição (e não no call-site). Corta até o próximo "private " após ela.
    private static string Corpo(string src, string assinatura)
    {
        int inicio = src.LastIndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"método não encontrado: {assinatura}");
        int prox = src.IndexOf("private ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (prox < 0) prox = src.Length;
        return src.Substring(inicio, prox - inicio);
    }

    [Fact] // (§7) NENHUMA referência às colunas inexistentes do lote em todo o repositório.
    public void Repositorio_NaoReferenciaColunasInexistentesDoLote()
    {
        string src = FonteRepositorio();
        Assert.DoesNotContain("situacao_entrada_produto_lote", src, StringComparison.Ordinal);
        Assert.DoesNotContain("entrada_produto_lote_atualizado_por", src, StringComparison.Ordinal);
    }

    [Fact] // (A/C) predicado do lote: sem situação booleana; elegibilidade por status_lote='FINALIZADO_LOCAL'.
    public void BloquearLote_UsaStatusFinalizadoLocal_SemSituacaoBooleana()
    {
        string corpo = Corpo(FonteRepositorio(), "BloquearLoteElegivelAsync(");
        Assert.Contains("status_lote IN ('FINALIZADO_LOCAL', 'ERRO_SAP')", corpo, StringComparison.Ordinal);
        Assert.Contains("correlation_id", corpo, StringComparison.Ordinal);   // guard SAP continua alimentado
        Assert.Contains("FOR UPDATE", corpo, StringComparison.Ordinal);       // locking preservado
        Assert.DoesNotContain("situacao_entrada_produto_lote", corpo, StringComparison.Ordinal);
    }

    [Fact] // (B/D) cancelamento do lote: lógico (status='CANCELADO'), colunas físicas reais, sem DELETE físico.
    public void CancelarLote_UsaColunasFisicasReais_CancelamentoLogico()
    {
        string corpo = Corpo(FonteRepositorio(), "CancelarLoteSeVazioAsync(");
        Assert.Contains("status_lote = 'CANCELADO'", corpo, StringComparison.Ordinal);
        Assert.Contains("alterado_por = @usuario", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_entrada_produto_lote", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("entrada_produto_lote_atualizado_por", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", corpo, StringComparison.OrdinalIgnoreCase);
        // cascata só cancela lote sem pesagem VÁLIDA ativa (não afeta sibling válido).
        Assert.Contains("p.status_pesagem = 'VALIDA'", corpo, StringComparison.Ordinal);
        // autor é enviado como TEXTO (alterado_por é varchar(80)); alterado_em fica a cargo da trigger BEFORE UPDATE.
        Assert.Contains("ParametroTexto(\"@usuario\"", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("alterado_em =", corpo, StringComparison.Ordinal);
    }

    [Fact] // (E/F) o fluxo mantém guard SAP, SERIALIZABLE, app.usuario_id e sem auto-retry.
    public void Fluxo_PreservaGuardSap_Serializable_Auditoria()
    {
        string src = FonteRepositorio();
        Assert.Contains("fn_entrada_produto_sap_guard_counts", src, StringComparison.Ordinal);
        Assert.Contains("outbox != 0 || tentativa != 0", src, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", src, StringComparison.Ordinal);
        Assert.Contains("DefinirUsuarioAppAsync", src, StringComparison.Ordinal);
        Assert.Contains("\"40001\" or \"40P01\"", src, StringComparison.Ordinal); // serialization/deadlock → EstadoMudou
    }
}
