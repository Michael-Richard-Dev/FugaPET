-- ============================================================
-- 027_h27_cadastro_cargo_regras_banco_ROLLBACK_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Cargo
--
-- OBJETIVO
--   Rollback estrutural do incremental 027.
--   Nao altera dados da tabela cargo nem usuario.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

DROP TRIGGER IF EXISTS trg_cargo_bloquear_inativacao_em_uso ON desenvolvimento.cargo;

DROP FUNCTION IF EXISTS desenvolvimento.fn_cargo_bloquear_inativacao_em_uso();

DROP VIEW IF EXISTS desenvolvimento.vw_cargo_diagnostico_inativacao;

DROP FUNCTION IF EXISTS desenvolvimento.fn_cargo_dependencias_ativas(bigint);

ALTER TABLE desenvolvimento.cargo
    DROP CONSTRAINT IF EXISTS ck_cargo_descricao_tamanho_funcional;

ALTER TABLE desenvolvimento.cargo
    DROP CONSTRAINT IF EXISTS ck_cargo_nome_tamanho_funcional;

COMMIT;

SELECT 'ROLLBACK 027 CARGO EXECUTADO - OBJETOS INCREMENTAIS REMOVIDOS' AS resultado_rollback;
