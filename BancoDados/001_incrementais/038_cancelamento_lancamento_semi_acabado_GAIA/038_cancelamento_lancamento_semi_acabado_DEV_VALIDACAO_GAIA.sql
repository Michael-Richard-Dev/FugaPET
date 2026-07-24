\set ON_ERROR_STOP on
-- ============================================================
-- 038_cancelamento_lancamento_semi_acabado_DEV_VALIDACAO_GAIA.sql
-- Projeto FugaPET_DEV | Schema: desenvolvimento
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'desenvolvimento' AND table_name = 'semi_acabado_lancamento' AND column_name = 'cancelado_em') THEN
        RAISE EXCEPTION 'VALIDACAO 038: coluna cancelado_em ausente.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'desenvolvimento' AND table_name = 'semi_acabado_lancamento' AND column_name = 'cancelado_por') THEN
        RAISE EXCEPTION 'VALIDACAO 038: coluna cancelado_por ausente.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'desenvolvimento' AND table_name = 'semi_acabado_lancamento' AND column_name = 'motivo_cancelamento') THEN
        RAISE EXCEPTION 'VALIDACAO 038: coluna motivo_cancelamento ausente.';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_semi_acabado_lancamento_status'
           AND conrelid = 'desenvolvimento.semi_acabado_lancamento'::regclass
           AND pg_get_constraintdef(oid) LIKE '%CANCELADO%'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 038: status CANCELADO ausente no check.';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_semi_acabado_lancamento_cancelamento_local'
           AND conrelid = 'desenvolvimento.semi_acabado_lancamento'::regclass
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 038: check de cancelamento local ausente.';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_index i
        JOIN pg_class idx ON idx.oid = i.indexrelid
        JOIN pg_class tbl ON tbl.oid = i.indrelid
        JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
        WHERE ns.nspname = 'desenvolvimento'
          AND tbl.relname = 'semi_acabado_lancamento'
          AND idx.relname = 'uq_semi_acabado_lancamento_aberto_por_op_item'
          AND i.indisunique
          AND i.indpred IS NOT NULL
          AND pg_get_expr(i.indpred, i.indrelid) NOT LIKE '%CANCELADO%'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 038: indice de lancamento aberto ausente/divergente ou inclui CANCELADO.';
    END IF;
END $$;

BEGIN;

DO $$
DECLARE
    v_lancamento bigint;
BEGIN
    INSERT INTO desenvolvimento.semi_acabado_lancamento
        (numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
         item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
         quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
         payload_preview_json, usuario_operacao)
    VALUES
        ('OP038TESTE', 'MAT038', 'Teste 038', '3007', 'PA01', '0001', 'L038', 'KG',
         10, 0, 10, 2, 'ERRO_SAP', '{}', 'gaia')
    RETURNING codigo_semi_acabado_lancamento INTO v_lancamento;

    UPDATE desenvolvimento.semi_acabado_lancamento
       SET status_lancamento = 'CANCELADO',
           cancelado_em = now(),
           cancelado_por = 'gaia',
           motivo_cancelamento = 'teste transacional 038',
           atualizado_em = now()
     WHERE codigo_semi_acabado_lancamento = v_lancamento
       AND status_lancamento = 'ERRO_SAP'
       AND material_document IS NULL
       AND material_document_year IS NULL;

    IF NOT EXISTS (SELECT 1 FROM desenvolvimento.semi_acabado_lancamento WHERE codigo_semi_acabado_lancamento = v_lancamento AND status_lancamento = 'CANCELADO') THEN
        RAISE EXCEPTION 'VALIDACAO 038: cancelamento ERRO_SAP sem documento nao funcionou.';
    END IF;

    RAISE NOTICE 'OK - cancelamento transacional ERRO_SAP validado.';
END $$;

ROLLBACK;

\echo 'OK - regras de cancelamento local do Produto Semiacabado validadas (DEV)'
