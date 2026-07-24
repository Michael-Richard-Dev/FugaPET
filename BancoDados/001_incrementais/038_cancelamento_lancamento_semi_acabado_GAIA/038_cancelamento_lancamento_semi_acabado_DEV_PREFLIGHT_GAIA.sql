\set ON_ERROR_STOP on
-- ============================================================
-- 038_cancelamento_lancamento_semi_acabado_DEV_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_DEV | Schema: desenvolvimento
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
DECLARE
    v_status text;
BEGIN
    IF to_regclass('desenvolvimento.semi_acabado_lancamento') IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 038: tabela semi_acabado_lancamento ausente. Aplique/valide o pacote 037 antes.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'semi_acabado_lancamento'
           AND column_name = 'status_lancamento'
    ) THEN
        RAISE EXCEPTION 'PREFLIGHT 038: coluna status_lancamento ausente.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
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
              AND conrelid = 'desenvolvimento.semi_acabado_lancamento'::regclass
       ) THEN
        RAISE EXCEPTION 'PREFLIGHT 038: ESTADO_PARCIAL_OU_INCOMPATIVEL - colunas existem sem check de cancelamento.';
    END IF;

    RAISE NOTICE 'PREFLIGHT 038: %', v_status;
END $$;
