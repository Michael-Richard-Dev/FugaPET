namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>Cenários do fluxo EXCLUIR PESAGEM (cancelamento lógico local; nunca DELETE físico, nunca SAP).</summary>
public enum CenarioExclusaoPesagem
{
    /// <summary>Pesagem cancelada logicamente com sucesso (recálculo + cascata de vazio operacional aplicados).</summary>
    Excluida,
    /// <summary>Usuário sem a permissão PROCESSO_PRODUCAO/ENTRADA_PRODUTO/EXCLUIR_PESAGEM (sem fallback legado).</summary>
    SemPermissao,
    /// <summary>Pesagem não encontrada / não localizável pela PK.</summary>
    PesagemInexistente,
    /// <summary>Pesagem já estava CANCELADA/inativa (idempotência — nada a fazer).</summary>
    JaCancelada,
    /// <summary>Estado do lançamento mudou (concorrência / serialization failure / revalidação falhou). SEM auto-retry.</summary>
    EstadoMudou,
    /// <summary>Estado SAP impeditivo (documento/exercício/envio, ERRO/ENVIADO/CONFIRMADO_SAP, ou guard outbox/tentativa > 0).</summary>
    BloqueadoSap,
    /// <summary>Falha técnica não classificada (rollback aplicado).</summary>
    FalhaTecnica
}

/// <summary>Resultado do cancelamento lógico de uma pesagem local (somente dados; a UI formata a apresentação).</summary>
public sealed record ResultadoExclusaoPesagem(
    CenarioExclusaoPesagem Cenario,
    string Mensagem,
    bool ArvoreVazia)
{
    public bool Sucesso => Cenario == CenarioExclusaoPesagem.Excluida;

    public static ResultadoExclusaoPesagem Ok(bool arvoreVazia) => new(
        CenarioExclusaoPesagem.Excluida,
        "Pesagem excluída com sucesso. O pedido foi atualizado e pode receber nova pesagem.",
        arvoreVazia);

    public static ResultadoExclusaoPesagem SemPermissao() => new(
        CenarioExclusaoPesagem.SemPermissao,
        "Você não tem permissão para excluir pesagem.",
        false);

    public static ResultadoExclusaoPesagem Inexistente() => new(
        CenarioExclusaoPesagem.PesagemInexistente,
        "A pesagem não foi encontrada. Atualize os dados antes de tentar novamente.",
        false);

    public static ResultadoExclusaoPesagem JaCancelada() => new(
        CenarioExclusaoPesagem.JaCancelada,
        "A pesagem já estava excluída. Atualize os dados.",
        false);

    public static ResultadoExclusaoPesagem EstadoMudou() => new(
        CenarioExclusaoPesagem.EstadoMudou,
        "A pesagem não foi excluída porque o estado do lançamento mudou. Atualize os dados antes de tentar novamente.",
        false);

    public static ResultadoExclusaoPesagem BloqueadoSap() => new(
        CenarioExclusaoPesagem.BloqueadoSap,
        "A pesagem não pode ser excluída: o lançamento possui estado de integração SAP que impede a exclusão local.",
        false);

    public static ResultadoExclusaoPesagem Falha() => new(
        CenarioExclusaoPesagem.FalhaTecnica,
        "Não foi possível excluir a pesagem. Atualize os dados e tente novamente.",
        false);
}

/// <summary>Resultado bruto do repositório (a transação já classificou o desfecho e se a árvore ficou vazia).</summary>
public sealed record ResultadoExclusaoPesagemRepositorio(
    CenarioExclusaoPesagem Cenario,
    bool ArvoreVazia);
