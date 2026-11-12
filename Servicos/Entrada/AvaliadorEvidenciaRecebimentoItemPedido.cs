using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Servicos.Entrada;

/// <summary>
/// GATE R5 / ETAPA C: motor PURO da decisao P5 para UM item do Pedido de Compra. Sem I/O, sem SAP, sem
/// banco — recebe as evidencias ja coletadas e devolve o desfecho.
/// <para>
/// Ordem de precedencia (a primeira que casa decide): exclusividade, fonte local, historico remoto,
/// abrangencia. Qualquer coisa que nao seja "comprovadamente sem recebimento" BLOQUEIA
/// (default UNKNOWN=BLOCK).
/// </para>
/// </summary>
public static class AvaliadorEvidenciaRecebimentoItemPedido
{
    public static ResultadoEvidenciaRecebimento Avaliar(
        ResultadoExclusividadeItemPedido exclusividade,
        ResultadoRecebimentoLocal local,
        ResultadoHistoricoMaterialSap historico)
    {
        ArgumentNullException.ThrowIfNull(exclusividade);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(historico);

        // ---- ETAPA B: sem exclusividade duravel por pedido+item, nao se autoriza o envio. ----
        switch (exclusividade.Cenario)
        {
            case CenarioExclusividadeItemPedido.Indisponivel:
                return ResultadoEvidenciaRecebimento.Bloqueado(
                    CenarioEvidenciaRecebimento.ExclusividadeIndisponivel,
                    "Proteção de concorrência por pedido+item indisponível: " + exclusividade.Motivo + ".");
            case CenarioExclusividadeItemPedido.EmUsoPorOutroLancamento:
                return ResultadoEvidenciaRecebimento.Bloqueado(
                    CenarioEvidenciaRecebimento.TentativaLocalPendente,
                    "Outro lançamento já está autorizado para este item do pedido: "
                    + exclusividade.Motivo + ".");
        }

        // ---- Regras 12/13/14 sobre a fonte LOCAL. ----
        switch (local.Cenario)
        {
            case CenarioRecebimentoLocal.Indisponivel:
                return ResultadoEvidenciaRecebimento.Bloqueado(
                    CenarioEvidenciaRecebimento.FalhaConsulta,
                    "Não foi possível consultar recebimentos locais deste item: " + local.Motivo + ".");
            case CenarioRecebimentoLocal.Pendente:
                return ResultadoEvidenciaRecebimento.Bloqueado(
                    CenarioEvidenciaRecebimento.TentativaLocalPendente,
                    "Existe tentativa local em aberto para este item do pedido: " + local.Motivo + ".");
            case CenarioRecebimentoLocal.Confirmado:
                return ResultadoEvidenciaRecebimento.Bloqueado(
                    CenarioEvidenciaRecebimento.RecebimentoImpeditivo,
                    "Já existe recebimento confirmado localmente para este item do pedido: "
                    + local.Motivo + ".");
        }

        // ---- Regra 14 sobre o historico REMOTO (falha, indisponibilidade, paginacao). ----
        if (historico.Cenario != CenarioHistoricoMaterialSap.Lido)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.FalhaConsulta,
                "Não foi possível obter o histórico de documentos de material do item: "
                + historico.Motivo + ".");
        }

        if (!historico.PaginacaoCompleta)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.FalhaConsulta,
                "O histórico de documentos de material do item não foi lido por completo "
                + "(paginação interrompida). Envio bloqueado.");
        }

        // GATE R6: documento sem identidade/tipo, ou estorno 102 SEM vinculo documental, torna o
        // historico NAO conclusivo. A correspondencia de estorno nunca e presumida.
        if (historico.TemDocumentoIncerto)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado,
                "O histórico do item contém documento sem identificação documental completa (ou estorno "
                + "sem vínculo comprovado ao documento estornado). Correspondência de estorno não é "
                + "presumida; reconciliação necessária e envio bloqueado.");
        }

        if (historico.Documentos101 < 0 || historico.Movimentos102 < 0)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado,
                "Contagem inválida de movimentos no histórico do item. Envio bloqueado.");
        }

        int liquido = historico.Documentos101 - historico.Movimentos102;

        if (liquido > 0)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.RecebimentoImpeditivo,
                $"O SAP já registra {historico.Documentos101} entrada(s) 101 e "
                + $"{historico.Movimentos102} estorno(s) 102 para este item do pedido "
                + "(saldo de recebimento em aberto). Envio bloqueado.");
        }

        if (liquido < 0)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado,
                $"Histórico inconsistente no SAP: {historico.Movimentos102} estorno(s) 102 para "
                + $"{historico.Documentos101} entrada(s) 101. Reconciliação necessária; envio bloqueado.");
        }

        // liquido == 0 com movimentacao existente: 101 integralmente estornado. Nao e "sem historico" —
        // exige reconciliacao humana para confirmar que o estorno foi completo e intencional.
        if (historico.Documentos101 > 0)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado,
                $"Este item já teve {historico.Documentos101} entrada(s) 101 integralmente estornada(s). "
                + "O caso exige reconciliação antes de um novo recebimento; envio bloqueado.");
        }

        // ---- Regra 15: ausencia de registros com abrangencia NAO comprovada nao vira P5=FALSE. ----
        if (!historico.AbrangenciaComprovada)
        {
            return ResultadoEvidenciaRecebimento.Bloqueado(
                CenarioEvidenciaRecebimento.AbrangenciaNaoComprovada,
                "Nenhum movimento foi encontrado para este item, porém a ABRANGÊNCIA da consulta não está "
                + "comprovada. Ausência de registros não é prova de ausência de recebimento; "
                + "envio bloqueado.");
        }

        return ResultadoEvidenciaRecebimento.Liberado(
            "Sem recebimento anterior: nenhuma entrada local e histórico SAP lido integralmente, "
            + "com abrangência comprovada e sem movimentos 101/102 para o item.");
    }
}
