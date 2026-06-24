-- ============================================================
-- 028_h28_cadastro_balanca_regras_banco_ROLLBACK_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Balanca
--
-- OBJETIVO
--   Rollback tecnico do incremental 028.
--   Nao altera dados. Remove somente objetos/regra criados pela proposta.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

DROP TRIGGER IF EXISTS trg_balanca_bloquear_inativacao_em_uso ON desenvolvimento.balanca;
DROP FUNCTION IF EXISTS desenvolvimento.fn_balanca_bloquear_inativacao_em_uso();
DROP VIEW IF EXISTS desenvolvimento.vw_balanca_diagnostico_inativacao;
DROP FUNCTION IF EXISTS desenvolvimento.fn_balanca_dependencias_ativas(bigint);

ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_usb_identificacao_obrigatoria;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_serial_campos_obrigatorios;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_tcp_ip_campos_obrigatorios;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_observacao_tamanho;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_protocolo_tamanho;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_flow_control_valores;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_stop_bits_valores;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_paridade_valores;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_porta_serial_tamanho;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_identificacao_local_tamanho;
ALTER TABLE desenvolvimento.balanca DROP CONSTRAINT IF EXISTS ck_balanca_nome_tamanho_funcional;

COMMIT;

SELECT 'ROLLBACK 028 BALANCA EXECUTADO - OBJETOS REMOVIDOS, DADOS PRESERVADOS' AS resultado_rollback;
