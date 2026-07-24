\set ON_ERROR_STOP on
-- ============================================================
-- 038_cancelamento_lancamento_semi_acabado_HML_PROPOSTA_GAIA.sql
-- Projeto FugaPET_HML | Schema: homologacao
-- Objeto: cancelamento local controlado de lancamento antigo ERRO_SAP de Produto Semiacabado
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

ALTER TABLE homologacao.semi_acabado_lancamento
    ADD COLUMN IF NOT EXISTS cancelado_em timestamptz,
    ADD COLUMN IF NOT EXISTS cancelado_por varchar(120),
    ADD COLUMN IF NOT EXISTS motivo_cancelamento varchar(500);

COMMENT ON COLUMN homologacao.semi_acabado_lancamento.cancelado_em IS 'FugaPET incremental 038 semi_acabado_cancelamento';
COMMENT ON COLUMN homologacao.semi_acabado_lancamento.cancelado_por IS 'FugaPET incremental 038 semi_acabado_cancelamento';
COMMENT ON COLUMN homologacao.semi_acabado_lancamento.motivo_cancelamento IS 'FugaPET incremental 038 semi_acabado_cancelamento';

ALTER TABLE homologacao.semi_acabado_lancamento
    DROP CONSTRAINT IF EXISTS ck_semi_acabado_lancamento_status;

ALTER TABLE homologacao.semi_acabado_lancamento
    ADD CONSTRAINT ck_semi_acabado_lancamento_status
    CHECK (status_lancamento IN ('FINALIZADO_LOCAL','ENVIANDO_SAP','CONFIRMADO_SAP','ERRO_SAP','DIVERGENCIA_SAP','CANCELADO'));
COMMENT ON CONSTRAINT ck_semi_acabado_lancamento_status ON homologacao.semi_acabado_lancamento IS 'FugaPET incremental 038 semi_acabado_cancelamento';

ALTER TABLE homologacao.semi_acabado_lancamento
    DROP CONSTRAINT IF EXISTS ck_semi_acabado_lancamento_cancelamento_local;

ALTER TABLE homologacao.semi_acabado_lancamento
    ADD CONSTRAINT ck_semi_acabado_lancamento_cancelamento_local
    CHECK (
        (status_lancamento = 'CANCELADO'
         AND cancelado_em IS NOT NULL
         AND nullif(trim(cancelado_por), '') IS NOT NULL
         AND nullif(trim(motivo_cancelamento), '') IS NOT NULL
         AND material_document IS NULL
         AND material_document_year IS NULL)
        OR
        (status_lancamento <> 'CANCELADO'
         AND cancelado_em IS NULL
         AND cancelado_por IS NULL
         AND motivo_cancelamento IS NULL)
    );
COMMENT ON CONSTRAINT ck_semi_acabado_lancamento_cancelamento_local ON homologacao.semi_acabado_lancamento IS 'FugaPET incremental 038 semi_acabado_cancelamento';

DROP INDEX IF EXISTS homologacao.uq_semi_acabado_lancamento_aberto_por_op_item;
CREATE UNIQUE INDEX uq_semi_acabado_lancamento_aberto_por_op_item
    ON homologacao.semi_acabado_lancamento (numero_ordem, item_ordem)
    WHERE status_lancamento IN ('FINALIZADO_LOCAL','ENVIANDO_SAP','ERRO_SAP','DIVERGENCIA_SAP');
COMMENT ON INDEX homologacao.uq_semi_acabado_lancamento_aberto_por_op_item IS 'FugaPET incremental 038 semi_acabado_cancelamento';

COMMIT;

\echo 'OK - APLICACAO 038 HML concluida'
