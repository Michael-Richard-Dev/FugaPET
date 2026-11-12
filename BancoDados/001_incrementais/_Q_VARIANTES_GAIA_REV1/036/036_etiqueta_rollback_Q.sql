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
-- 036_etiqueta_rollback_HML.sql
-- Projeto FugaPET | Schema: homologacao
-- Objeto: Cadastro de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 036
-- Finalidade: rollback controlado
-- ============================================================
-- Rollback nao faz parte do fluxo normal.
-- Remove somente objetos criados pelo pacote 036 e identificados por comentario de propriedade.

BEGIN;
SET LOCAL search_path TO homologacao, public;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_comment constant text := 'FugaPET incremental 036 etiqueta';
    v_oid oid;
BEGIN
    -- Abortar se algum objeto alvo existir com nome do pacote, mas sem comentario de propriedade 036.
    IF EXISTS (
        SELECT 1
        FROM (
            SELECT c.oid, obj_description(c.oid, 'pg_constraint') AS comentario
            FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace ns ON ns.oid = t.relnamespace
            WHERE ns.nspname='homologacao' AND t.relname='etiqueta'
              AND c.conname IN ('ck_etiqueta_codigo_tamanho','ck_etiqueta_nome_tamanho','ck_etiqueta_descricao_tamanho')
            UNION ALL
            SELECT i.oid, obj_description(i.oid, 'pg_class')
            FROM pg_class i JOIN pg_namespace ns ON ns.oid = i.relnamespace
            WHERE ns.nspname='homologacao' AND i.relkind='i' AND i.relname='uq_etiqueta_codigo_interno_global'
            UNION ALL
            SELECT p.oid, obj_description(p.oid, 'pg_proc')
            FROM pg_proc p JOIN pg_namespace ns ON ns.oid = p.pronamespace
            WHERE ns.nspname='homologacao' AND p.proname='fn_bloqueia_inativar_etiqueta_com_produto_ativo' AND p.pronargs=0
            UNION ALL
            SELECT tg.oid, obj_description(tg.oid, 'pg_trigger')
            FROM pg_trigger tg JOIN pg_class t ON t.oid=tg.tgrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
            WHERE ns.nspname='homologacao' AND t.relname='etiqueta' AND tg.tgname='trg_bloqueia_inativar_etiqueta_com_produto_ativo' AND NOT tg.tgisinternal
        ) x
        WHERE comentario IS DISTINCT FROM v_comment
    ) THEN
        RAISE EXCEPTION 'ROLLBACK 036 HML bloqueado: existe objeto alvo sem comentario de propriedade do pacote 036.';
    END IF;

    SELECT tg.oid INTO v_oid
      FROM pg_trigger tg JOIN pg_class t ON t.oid=tg.tgrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
     WHERE ns.nspname='homologacao' AND t.relname='etiqueta'
       AND tg.tgname='trg_bloqueia_inativar_etiqueta_com_produto_ativo'
       AND obj_description(tg.oid, 'pg_trigger') = v_comment;
    IF v_oid IS NOT NULL THEN
        EXECUTE 'DROP TRIGGER trg_bloqueia_inativar_etiqueta_com_produto_ativo ON homologacao.etiqueta';
    END IF;

    SELECT p.oid INTO v_oid
      FROM pg_proc p JOIN pg_namespace ns ON ns.oid=p.pronamespace
     WHERE ns.nspname='homologacao' AND p.proname='fn_bloqueia_inativar_etiqueta_com_produto_ativo' AND p.pronargs=0
       AND obj_description(p.oid, 'pg_proc') = v_comment;
    IF v_oid IS NOT NULL THEN
        EXECUTE 'DROP FUNCTION homologacao.fn_bloqueia_inativar_etiqueta_com_produto_ativo()';
    END IF;

    IF to_regclass('homologacao.uq_etiqueta_codigo_interno_global') IS NOT NULL
       AND obj_description('homologacao.uq_etiqueta_codigo_interno_global'::regclass, 'pg_class') = v_comment THEN
        EXECUTE 'DROP INDEX homologacao.uq_etiqueta_codigo_interno_global';
    END IF;

    FOR v_oid IN
        SELECT c.oid
          FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
         WHERE ns.nspname='homologacao' AND t.relname='etiqueta'
           AND c.conname IN ('ck_etiqueta_codigo_tamanho','ck_etiqueta_nome_tamanho','ck_etiqueta_descricao_tamanho')
           AND obj_description(c.oid, 'pg_constraint') = v_comment
    LOOP
        EXECUTE (
            SELECT 'ALTER TABLE homologacao.etiqueta DROP CONSTRAINT ' || quote_ident(c.conname)
              FROM pg_constraint c WHERE c.oid = v_oid
        );
    END LOOP;

    -- Restaurar indice parcial legado somente se ele nao existir.
    IF to_regclass('homologacao.uq_etiqueta_codigo') IS NULL THEN
        EXECUTE 'CREATE UNIQUE INDEX uq_etiqueta_codigo ON homologacao.etiqueta (upper(trim(codigo_interno))) WHERE situacao_etiqueta = true';
    END IF;

    RAISE NOTICE 'OK - ROLLBACK 036 HML concluido';
END $$;

COMMIT;
