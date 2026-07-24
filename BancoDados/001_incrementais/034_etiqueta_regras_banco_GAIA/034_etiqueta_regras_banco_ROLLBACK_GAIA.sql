-- ============================================================
-- 034_etiqueta_regras_banco_ROLLBACK_GAIA.sql
-- PROPOSTA - NAO EXECUTAR
-- Rollback da proposta 034 - Cadastro de Etiqueta
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

DROP TRIGGER IF EXISTS trg_bloqueia_inativar_etiqueta_com_produto_ativo ON desenvolvimento.etiqueta;
DROP FUNCTION IF EXISTS desenvolvimento.fn_bloqueia_inativar_etiqueta_com_produto_ativo();

DROP INDEX IF EXISTS desenvolvimento.uq_etiqueta_codigo_interno_global;

ALTER TABLE desenvolvimento.etiqueta
    DROP CONSTRAINT IF EXISTS ck_etiqueta_descricao_tamanho;

ALTER TABLE desenvolvimento.etiqueta
    DROP CONSTRAINT IF EXISTS ck_etiqueta_nome_tamanho;

ALTER TABLE desenvolvimento.etiqueta
    DROP CONSTRAINT IF EXISTS ck_etiqueta_codigo_tamanho;

ALTER TABLE desenvolvimento.etiqueta
    ADD CONSTRAINT ck_etiqueta_codigo_nao_vazio
    CHECK (length(trim(codigo_interno::text)) > 0);

CREATE UNIQUE INDEX IF NOT EXISTS uq_etiqueta_codigo_interno_ativo
    ON desenvolvimento.etiqueta (upper(trim(codigo_interno)))
    WHERE situacao_etiqueta = true;

COMMIT;
