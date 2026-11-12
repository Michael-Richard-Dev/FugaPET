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
-- 027_h27_cadastro_cargo_regras_banco_HML_ROLLBACK_GAIA.sql
-- Projeto FugaPET_HML
-- Banco PostgreSQL - Banco alvo: fuga_jales_local_homologacao_q_v1_2
-- Schema alvo: homologacao
-- Objeto: Cadastro de Cargo
--
-- OBJETIVO
--   Rollback estrutural do incremental 027 HML.
--   Nao altera dados da tabela cargo nem usuario.
--   NAO executar em producao.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

DROP TRIGGER IF EXISTS trg_cargo_bloquear_inativacao_em_uso ON homologacao.cargo;

DROP FUNCTION IF EXISTS homologacao.fn_cargo_bloquear_inativacao_em_uso();

DROP VIEW IF EXISTS homologacao.vw_cargo_diagnostico_inativacao;

DROP FUNCTION IF EXISTS homologacao.fn_cargo_dependencias_ativas(bigint);

ALTER TABLE homologacao.cargo
    DROP CONSTRAINT IF EXISTS ck_cargo_descricao_tamanho_funcional;

ALTER TABLE homologacao.cargo
    DROP CONSTRAINT IF EXISTS ck_cargo_nome_tamanho_funcional;

COMMIT;

SELECT 'ROLLBACK 027 CARGO HML EXECUTADO - OBJETOS INCREMENTAIS REMOVIDOS' AS resultado_rollback;
