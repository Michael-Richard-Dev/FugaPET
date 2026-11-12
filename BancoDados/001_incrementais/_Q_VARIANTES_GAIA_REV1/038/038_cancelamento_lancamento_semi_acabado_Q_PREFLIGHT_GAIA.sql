\set ON_ERROR_STOP on
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
-- 038_cancelamento_lancamento_semi_acabado_HML_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_HML | Schema: homologacao
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_status text;
BEGIN
    IF to_regclass('homologacao.semi_acabado_lancamento') IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 038: tabela semi_acabado_lancamento ausente. Aplique/valide o pacote 037 antes.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'homologacao'
           AND table_name = 'semi_acabado_lancamento'
           AND column_name = 'status_lancamento'
    ) THEN
        RAISE EXCEPTION 'PREFLIGHT 038: coluna status_lancamento ausente.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'homologacao'
           AND table_name = 'semi_acabado_lancamento'
           AND column_name IN ('cancelado_em','cancelado_por','motivo_cancelamento')
    ) THEN
        v_status := 'PACOTE_COMPLETAMENTE_APLICADO';
    ELSE
        v_status := 'PACOTE_NAO_APLICADO';
    END IF;

    IF v_status = 'PACOTE_COMPLETAMENTE_APLICADO'
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint
            WHERE conname = 'ck_semi_acabado_lancamento_cancelamento_local'
              AND conrelid = 'homologacao.semi_acabado_lancamento'::regclass
       ) THEN
        RAISE EXCEPTION 'PREFLIGHT 038: ESTADO_PARCIAL_OU_INCOMPATIVEL - colunas existem sem check de cancelamento.';
    END IF;

    RAISE NOTICE 'PREFLIGHT 038: %', v_status;
END $$;
