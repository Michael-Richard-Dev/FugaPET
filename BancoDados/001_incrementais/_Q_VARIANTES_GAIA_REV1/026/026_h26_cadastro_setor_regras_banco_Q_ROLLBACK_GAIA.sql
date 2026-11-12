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
-- 026_h26_cadastro_setor_regras_banco_HML_ROLLBACK_GAIA.sql
-- Rollback do incremental 026 para regras de banco do Cadastro de Setor.
-- Projeto FugaPET_HML - Schema homologacao
-- NAO executar automaticamente.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

DROP TRIGGER IF EXISTS trg_setor_bloquear_inativacao_em_uso ON homologacao.setor;
DROP FUNCTION IF EXISTS homologacao.fn_bloquear_inativacao_setor_em_uso();
DROP VIEW IF EXISTS homologacao.vw_setor_diagnostico_inativacao;
DROP FUNCTION IF EXISTS homologacao.fn_setor_dependencias_ativas(bigint);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'homologacao.setor'::regclass
          AND conname = 'ck_setor_nome_tamanho_funcional'
    ) THEN
        ALTER TABLE homologacao.setor DROP CONSTRAINT ck_setor_nome_tamanho_funcional;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'homologacao.setor'::regclass
          AND conname = 'ck_setor_descricao_tamanho_funcional'
    ) THEN
        ALTER TABLE homologacao.setor DROP CONSTRAINT ck_setor_descricao_tamanho_funcional;
    END IF;
END $$;

COMMIT;
