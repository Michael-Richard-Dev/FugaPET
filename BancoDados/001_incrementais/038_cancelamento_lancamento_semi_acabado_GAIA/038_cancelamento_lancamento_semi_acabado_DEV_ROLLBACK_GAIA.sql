\set ON_ERROR_STOP on
-- ============================================================
-- 038_cancelamento_lancamento_semi_acabado_DEV_ROLLBACK_GAIA.sql
-- Projeto FugaPET_DEV | Schema: desenvolvimento
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

ALTER TABLE desenvolvimento.semi_acabado_lancamento
    DROP CONSTRAINT IF EXISTS ck_semi_acabado_lancamento_cancelamento_local;

ALTER TABLE desenvolvimento.semi_acabado_lancamento
    DROP CONSTRAINT IF EXISTS ck_semi_acabado_lancamento_status;

ALTER TABLE desenvolvimento.semi_acabado_lancamento
    ADD CONSTRAINT ck_semi_acabado_lancamento_status
    CHECK (status_lancamento IN ('FINALIZADO_LOCAL','ENVIANDO_SAP','CONFIRMADO_SAP','ERRO_SAP','DIVERGENCIA_SAP'));
COMMENT ON CONSTRAINT ck_semi_acabado_lancamento_status ON desenvolvimento.semi_acabado_lancamento IS 'FugaPET incremental 037 semi_acabado';

ALTER TABLE desenvolvimento.semi_acabado_lancamento
    DROP COLUMN IF EXISTS cancelado_em,
    DROP COLUMN IF EXISTS cancelado_por,
    DROP COLUMN IF EXISTS motivo_cancelamento;

DROP INDEX IF EXISTS desenvolvimento.uq_semi_acabado_lancamento_aberto_por_op_item;
CREATE UNIQUE INDEX uq_semi_acabado_lancamento_aberto_por_op_item
    ON desenvolvimento.semi_acabado_lancamento (numero_ordem, item_ordem)
    WHERE status_lancamento IN ('FINALIZADO_LOCAL','ENVIANDO_SAP','ERRO_SAP','DIVERGENCIA_SAP');
COMMENT ON INDEX desenvolvimento.uq_semi_acabado_lancamento_aberto_por_op_item IS 'FugaPET incremental 037 semi_acabado';

COMMIT;

\echo 'OK - ROLLBACK 038 DEV concluido'
