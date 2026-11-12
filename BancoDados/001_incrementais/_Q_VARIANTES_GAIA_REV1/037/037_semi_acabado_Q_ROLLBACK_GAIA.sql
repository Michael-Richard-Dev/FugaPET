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
-- 037_semi_acabado_HML_ROLLBACK_GAIA.sql
-- Projeto FugaPET_HML | Schema: homologacao
--
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- Rollback destrutivo e fora do fluxo normal.
-- Remove somente tabelas identificadas como pertencentes ao pacote 037.
-- Por seguranca, aborta se houver dados, salvo forca explicita via:
-- SET fugapet.forcar_drop_037 = '1';
-- ============================================================

SET search_path TO homologacao;

BEGIN;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_comentario text := 'FugaPET incremental 037 semi_acabado';
    v_forcar boolean := coalesce(current_setting('fugapet.forcar_drop_037', true) = '1', false);
    v_qtd_pes bigint := 0;
    v_qtd_lan bigint := 0;
BEGIN
    IF to_regclass('homologacao.semi_acabado_lancamento') IS NOT NULL THEN
        IF obj_description('homologacao.semi_acabado_lancamento'::regclass, 'pg_class') IS DISTINCT FROM v_comentario THEN
            RAISE EXCEPTION 'ROLLBACK 037 ABORTADO: semi_acabado_lancamento nao possui comentario de propriedade do pacote.';
        END IF;

        EXECUTE 'SELECT count(*) FROM homologacao.semi_acabado_lancamento' INTO v_qtd_lan;
    END IF;

    IF to_regclass('homologacao.semi_acabado_pesagem') IS NOT NULL THEN
        IF obj_description('homologacao.semi_acabado_pesagem'::regclass, 'pg_class') IS DISTINCT FROM v_comentario THEN
            RAISE EXCEPTION 'ROLLBACK 037 ABORTADO: semi_acabado_pesagem nao possui comentario de propriedade do pacote.';
        END IF;

        EXECUTE 'SELECT count(*) FROM homologacao.semi_acabado_pesagem' INTO v_qtd_pes;
    END IF;

    IF (v_qtd_pes > 0 OR v_qtd_lan > 0) AND NOT v_forcar THEN
        RAISE EXCEPTION 'ROLLBACK 037 ABORTADO: existem dados (% pesagens / % lancamentos). Rollback nao e fluxo normal.',
            v_qtd_pes, v_qtd_lan;
    END IF;

    DROP TABLE IF EXISTS homologacao.semi_acabado_pesagem;
    DROP TABLE IF EXISTS homologacao.semi_acabado_lancamento;

    RAISE NOTICE 'OK - ROLLBACK 037 HML concluido.';
END $$;

COMMIT;
