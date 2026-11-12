-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================
-- 028_h28_cadastro_balanca_regras_banco_HML_ROLLBACK_GAIA.sql
-- Projeto FugaPET_HML
-- Banco alvo: fuga_jales_local_homologacao_q_v1_2
-- Banco PostgreSQL - Schema homologacao
-- Objeto: Cadastro de Balanca
--
-- OBJETIVO
--   Rollback tecnico do incremental 028.
--   Nao altera dados. Remove somente objetos/regra criados pela proposta.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

DROP TRIGGER IF EXISTS trg_balanca_bloquear_inativacao_em_uso ON homologacao.balanca;
DROP FUNCTION IF EXISTS homologacao.fn_balanca_bloquear_inativacao_em_uso();
DROP VIEW IF EXISTS homologacao.vw_balanca_diagnostico_inativacao;
DROP FUNCTION IF EXISTS homologacao.fn_balanca_dependencias_ativas(bigint);

ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_usb_identificacao_obrigatoria;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_serial_campos_obrigatorios;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_tcp_ip_campos_obrigatorios;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_observacao_tamanho;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_protocolo_tamanho;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_flow_control_valores;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_stop_bits_valores;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_paridade_valores;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_porta_serial_tamanho;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_identificacao_local_tamanho;
ALTER TABLE homologacao.balanca DROP CONSTRAINT IF EXISTS ck_balanca_nome_tamanho_funcional;

COMMIT;

SELECT 'ROLLBACK 028 BALANCA EXECUTADO - OBJETOS REMOVIDOS, DADOS PRESERVADOS' AS resultado_rollback;
