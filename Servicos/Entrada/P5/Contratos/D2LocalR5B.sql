-- FUGAPET / ENTRADA MP / P5 / R5B / D2
-- READ ONLY. Leitura historica LOCAL; NUNCA constitui prova negativa SAP.
-- $1 = pedido ('4500000071'), $2 = item ('00010' ou '10'),
-- $3 = ID do lancamento corrente ou NULL, para diferenciar propria operacao.
-- Usar PREPARE apenas na conexao de leitura; em Npgsql parametrizar o SELECT
-- em vez de executar PREPARE globalmente. Nao usar interpolacao de strings.
-- Consulta fundamentada em R4A, sem dependencia das tabelas novas D3.

PREPARE fugapet_p5_local_read(text, text, bigint) AS
WITH entrada AS MATERIALIZED (
    SELECT btrim($1::text) AS pedido_raw,
           btrim($2::text) AS item_raw,
           $3::bigint AS lancamento_atual
), p AS MATERIALIZED (
    SELECT pedido_raw AS numero_pedido,
           CASE WHEN item_raw ~ '^[0-9]{1,5}$'
                THEN lpad(coalesce(nullif(ltrim(item_raw,'0'),''),'0'),5,'0')
                ELSE NULL END AS numero_item_norm,
           lancamento_atual,
           (pedido_raw ~ '^[0-9]{1,10}$' AND item_raw ~ '^[0-9]{1,5}$') AS chave_valida
    FROM entrada
), sap_item AS MATERIALIZED (
    SELECT pi.codigo_sap_pedido_compra_item, pi.numero_item,
           pi.codigo_produto, pi.status_cache, pi.sincronizado_em,
           pi.quantidade_pedida, pi.quantidade_recebida, pi.quantidade_pendente
    FROM homologacao.sap_pedido_compra pc
    JOIN homologacao.sap_pedido_compra_item pi
      ON pi.codigo_sap_pedido_compra = pc.codigo_sap_pedido_compra
    JOIN p ON btrim(pc.numero_pedido) = p.numero_pedido
    WHERE pi.numero_item ~ '^[[:space:]]*[0-9]{1,5}[[:space:]]*$'
      AND lpad(coalesce(nullif(ltrim(btrim(pi.numero_item),'0'),''),'0'),5,'0')
          = p.numero_item_norm
), local_item AS MATERIALIZED (
    SELECT l.codigo_entrada_produto_lancamento AS lancamento_id,
           l.numero_pedido, l.status_lancamento, l.situacao_entrada_produto_lancamento,
           l.iniciado_em, l.finalizado_em, l.enviado_sap_em AS lancamento_enviado_em,
           l.documento_material_sap AS lancamento_documento,
           l.exercicio_documento_material_sap AS lancamento_exercicio,
           i.codigo_entrada_produto_item AS item_id, i.numero_item,
           i.material, i.status_item, i.situacao_entrada_produto_item,
           i.codigo_sap_pedido_compra_item, i.documento_material_item,
           i.quantidade_recebida AS item_quantidade_recebida
    FROM homologacao.entrada_produto_lancamento l
    JOIN homologacao.entrada_produto_item i
      ON i.codigo_entrada_produto_lancamento=l.codigo_entrada_produto_lancamento
    CROSS JOIN p
    WHERE btrim(l.numero_pedido)=p.numero_pedido
      AND (
        (i.numero_item ~ '^[[:space:]]*[0-9]{1,5}[[:space:]]*$'
          AND lpad(coalesce(nullif(ltrim(btrim(i.numero_item),'0'),''),'0'),5,'0')=p.numero_item_norm)
        OR EXISTS (
          SELECT 1 FROM sap_item s
          WHERE s.codigo_sap_pedido_compra_item=i.codigo_sap_pedido_compra_item
        )
      )
), cabecalho_sem_item AS MATERIALIZED (
    SELECT l.codigo_entrada_produto_lancamento
    FROM homologacao.entrada_produto_lancamento l
    CROSS JOIN p
    WHERE btrim(l.numero_pedido)=p.numero_pedido
      AND NOT EXISTS (
        SELECT 1 FROM homologacao.entrada_produto_item i
        WHERE i.codigo_entrada_produto_lancamento=l.codigo_entrada_produto_lancamento
      )
), agregado_sap AS MATERIALIZED (
    SELECT count(*)::bigint AS cache_item_count,
           coalesce(jsonb_agg(jsonb_build_object(
               'sap_item_id', codigo_sap_pedido_compra_item,
               'numero_item', numero_item, 'material', codigo_produto,
               'status_cache', status_cache, 'sincronizado_em', sincronizado_em,
               'quantidade_recebida', quantidade_recebida,
               'quantidade_pendente', quantidade_pendente)), '[]'::jsonb) AS cache_itens
    FROM sap_item
)
SELECT
    p.numero_pedido,
    p.numero_item_norm AS numero_item,
    p.chave_valida,
    p.lancamento_atual,
    (SELECT count(*) FROM cabecalho_sem_item) AS lancamentos_sem_item_no_pedido,
    s.cache_item_count,
    s.cache_itens, -- CACHE NAO E HISTORICO AUTORITATIVO DE DOCUMENTOS MATERIAIS
    li.lancamento_id, li.item_id,
    (li.lancamento_id IS NOT NULL AND li.lancamento_id=p.lancamento_atual) AS eh_lancamento_atual,
    (li.lancamento_id IS NOT NULL AND li.lancamento_id IS DISTINCT FROM p.lancamento_atual) AS eh_outro_lancamento,
    li.numero_item AS numero_item_local, li.material AS material_local,
    li.codigo_sap_pedido_compra_item,
    li.status_lancamento, li.status_item,
    li.situacao_entrada_produto_lancamento, li.situacao_entrada_produto_item,
    li.iniciado_em, li.finalizado_em, li.lancamento_enviado_em,
    li.lancamento_documento, li.lancamento_exercicio,
    li.documento_material_item, li.item_quantidade_recebida,
    lote.codigo_entrada_produto_lote AS lote_id,
    lote.numero_lote, lote.status_lote, lote.correlation_id,
    lote.enviado_sap_em AS lote_enviado_em,
    lote.confirmado_sap_em,
    lote.documento_material_sap AS lote_documento,
    lote.exercicio_documento_material_sap AS lote_exercicio,
    lote.documento_material_item AS lote_documento_item,
    (lote.payload_enviado_json IS NOT NULL) AS lote_payload_presente,
    (lote.resposta_sap_json IS NOT NULL) AS lote_resposta_presente,
    (lote.erro_sanitizado IS NOT NULL) AS lote_erro_presente,
    CASE
      WHEN NOT p.chave_valida THEN 'CHAVE_INVALIDA_BLOCK'
      WHEN li.item_id IS NULL THEN 'SEM_ENTRADA_LOCAL_NAO_PROVA_SAP'
      WHEN coalesce(li.lancamento_documento,'')<>''
        OR coalesce(lote.documento_material_sap,'')<>''
        OR li.status_lancamento='CONFIRMADO_SAP'
        OR li.status_item='CONFIRMADO_SAP'
        OR lote.status_lote='CONFIRMADO_SAP' THEN 'RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK'
      WHEN coalesce(li.lancamento_enviado_em,lote.enviado_sap_em) IS NOT NULL
        OR li.status_lancamento IN ('ENVIADO_SAP','ERRO_SAP')
        OR li.status_item IN ('ENVIADO_SAP','ERRO_SAP')
        OR lote.status_lote IN ('ENVIANDO_SAP','ERRO_SAP')
        OR lote.payload_enviado_json IS NOT NULL THEN 'ENVIO_OU_RESULTADO_REMOTO_INCERTO_BLOCK'
      WHEN li.status_lancamento='CANCELADO' OR li.status_item='CANCELADO'
        OR lote.status_lote='CANCELADO' THEN 'CANCELAMENTO_LOCAL_SEM_PROVA_ESTORNO_BLOCK'
      WHEN li.lancamento_id IS DISTINCT FROM p.lancamento_atual
        THEN 'OUTRO_LANCAMENTO_LOCAL_BLOCK'
      WHEN li.status_lancamento NOT IN ('ABERTO','EM_PESAGEM','FINALIZADO_LOCAL')
        OR li.status_item NOT IN ('ABERTO','EM_PESAGEM','FINALIZADO_LOCAL')
        OR (lote.status_lote IS NOT NULL
            AND lote.status_lote NOT IN ('AGUARDANDO_DADOS','LOTE_CONFIRMADO','PESANDO','FINALIZADO_LOCAL'))
        THEN 'STATUS_NAO_CLASSIFICADO_BLOCK'
      ELSE 'PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO'
    END AS categoria_local_p5,
    FALSE AS prova_negativa_sap
FROM p
CROSS JOIN agregado_sap s
LEFT JOIN local_item li ON TRUE
LEFT JOIN homologacao.entrada_produto_lote lote
    ON lote.codigo_entrada_produto_item=li.item_id
ORDER BY li.lancamento_id NULLS FIRST, li.item_id NULLS FIRST,
         lote.codigo_entrada_produto_lote NULLS FIRST;

-- Exemplo estritamente read-only depois de preparar:
-- BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
-- EXECUTE fugapet_p5_local_read('4500000071','00010',NULL);
-- ROLLBACK;
-- IMPORTANTE: ausencia de linha local / categoria SEM_ENTRADA_LOCAL_NAO_PROVA_SAP
-- NUNCA autoriza P5=FALSE. O retorno do provider para isso e NULL (BLOCK).
