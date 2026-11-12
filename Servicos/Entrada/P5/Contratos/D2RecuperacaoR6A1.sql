-- R6A.1 / D2 de recuperacao SEM CLAIM_TOKEN ou RECOVERY_TOKEN.
-- CONTRATO APENAS. View deste SELECT so existira apos aprovacao/aplicacao futura R6A.1.
-- $1: numero_pedido text, $2: numero_item text, $3: lancamento_atual bigint nullable.
-- Sem leitura direta de entrada_mp_p5_guarda/tentativa pela role provider.
-- Nao retorna P5=FALSE. Ausencia de D3 local => BLOCK.
WITH chave AS (
 SELECT pg_catalog.btrim($1::text) AS numero_pedido,
        CASE WHEN pg_catalog.btrim($2::text) ~ '^[0-9]{1,5}$'
          THEN pg_catalog.lpad(
                 COALESCE(NULLIF(
                   pg_catalog.ltrim(pg_catalog.btrim($2::text),'0'),''),'0'),5,'0')
          ELSE NULL END AS numero_item,
        $3::bigint AS lancamento_atual,
        pg_catalog.btrim($1::text) ~ '^[0-9]{1,10}$'
          AND pg_catalog.btrim($2::text) ~ '^[0-9]{1,5}$' AS chave_valida
), registros AS (
 SELECT v.* FROM homologacao.vw_entrada_mp_p5_d2_recuperacao v
 JOIN chave k ON k.chave_valida AND k.numero_pedido=v.numero_pedido
             AND k.numero_item=v.numero_item
), resumo AS (
 SELECT pg_catalog.count(*) FILTER (WHERE codigo_p5_tentativa IS NOT NULL)::bigint AS tentativas,
        pg_catalog.count(*) FILTER (WHERE estado_tentativa='CONFIRMADO_SAP')::bigint AS confirmadas,
        pg_catalog.count(*) FILTER (WHERE estado_tentativa IN
          ('RESERVADO','ENVIO_INICIADO','RESULTADO_INDETERMINADO'))::bigint AS bloqueantes,
        pg_catalog.count(*) FILTER (WHERE estado_tentativa='RESULTADO_INDETERMINADO')::bigint AS incertas,
        pg_catalog.count(*) FILTER (WHERE codigo_p5_tentativa IS NOT NULL AND
          codigo_entrada_produto_lancamento IS DISTINCT FROM (SELECT lancamento_atual FROM chave))::bigint
          AS tentativas_outro_lancamento,
        COALESCE(pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
          'id',codigo_p5_tentativa,'numero',numero_tentativa,
          'lancamento',codigo_entrada_produto_lancamento,
          'estado',estado_tentativa,'correlation_id',correlation_id,
          'envio_iniciado_em',envio_iniciado_em,'http',http_status,
          'documento',documento_material_sap,'ano',exercicio_documento_material_sap,
          'item_documento',documento_material_item,'tem_erro',tem_erro
        ) ORDER BY numero_tentativa) FILTER (WHERE codigo_p5_tentativa IS NOT NULL),
          '[]'::jsonb) AS historico
 FROM registros
)
SELECT k.numero_pedido,k.numero_item,k.chave_valida,k.lancamento_atual,
       (SELECT pg_catalog.max(codigo_p5_guarda) FROM registros) AS codigo_p5_guarda,
       (SELECT pg_catalog.max(estado_guarda) FROM registros) AS estado_guarda,
       (SELECT pg_catalog.max(tentativa_atual) FROM registros) AS tentativa_atual,
       (SELECT pg_catalog.bool_or(recovery_lease_ativo) FROM registros) AS recovery_lease_ativo,
       a.tentativas,a.confirmadas,a.bloqueantes,a.incertas,a.tentativas_outro_lancamento,
       a.historico,
       FALSE AS prova_negativa_sap,
       CASE WHEN NOT k.chave_valida THEN 'NULL_BLOCK_INVALID_PO_ITEM'
            WHEN NOT EXISTS (SELECT 1 FROM registros) THEN 'NULL_BLOCK_NO_LOCAL_GUARD'
            WHEN a.confirmadas>0 THEN 'BLOCK_CONFIRMED_RECEIPT'
            WHEN a.bloqueantes>0 THEN 'NULL_BLOCK_PENDING_OR_UNKNOWN_REMOTE'
            WHEN a.tentativas_outro_lancamento>0 THEN 'NULL_BLOCK_OTHER_LOCAL_ATTEMPT'
            ELSE 'NULL_BLOCK_NEEDS_AUTHORITATIVE_SAP_PROOF_AND_ATOMIC_GUARD'
       END AS p5_local_decision
FROM chave k CROSS JOIN resumo a;
-- D2 nao e uma autorizacao para POST. Prova SAP externa + claim atomico sao gates separados.
