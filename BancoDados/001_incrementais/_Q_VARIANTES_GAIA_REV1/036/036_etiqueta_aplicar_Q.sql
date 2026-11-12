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
-- 036_etiqueta_aplicar_HML.sql
-- Projeto FugaPET | Schema: homologacao
-- Objeto: Cadastro de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 036
-- Finalidade: aplicacao transacional
-- ============================================================

BEGIN;
SET LOCAL search_path TO homologacao, public;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_comment constant text := 'FugaPET incremental 036 etiqueta';
    v_existentes integer := 0;
    v_comentados integer := 0;
    v_inconsistencias integer := 0;
    v_old_idx boolean;
    v_hist_idx boolean;
BEGIN
    IF to_regclass('homologacao.etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.etiqueta nao encontrada.';
    END IF;
    IF to_regclass('homologacao.modelo_etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.modelo_etiqueta nao encontrada.';
    END IF;
    IF to_regclass('homologacao.produto_referencia') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.produto_referencia nao encontrada.';
    END IF;
    IF to_regclass('homologacao.produto_etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.produto_etiqueta nao encontrada.';
    END IF;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE comentario = v_comment)
      INTO v_existentes, v_comentados
    FROM (
        SELECT c.oid, obj_description(c.oid, 'pg_constraint') AS comentario
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'homologacao' AND t.relname = 'etiqueta'
          AND c.conname IN ('ck_etiqueta_codigo_tamanho','ck_etiqueta_nome_tamanho','ck_etiqueta_descricao_tamanho')
        UNION ALL
        SELECT i.oid, obj_description(i.oid, 'pg_class')
        FROM pg_class i
        JOIN pg_namespace ns ON ns.oid = i.relnamespace
        WHERE ns.nspname = 'homologacao' AND i.relkind = 'i' AND i.relname = 'uq_etiqueta_codigo_interno_global'
        UNION ALL
        SELECT p.oid, obj_description(p.oid, 'pg_proc')
        FROM pg_proc p
        JOIN pg_namespace ns ON ns.oid = p.pronamespace
        WHERE ns.nspname = 'homologacao' AND p.proname = 'fn_bloqueia_inativar_etiqueta_com_produto_ativo' AND p.pronargs = 0
        UNION ALL
        SELECT tg.oid, obj_description(tg.oid, 'pg_trigger')
        FROM pg_trigger tg
        JOIN pg_class t ON t.oid = tg.tgrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'homologacao' AND t.relname = 'etiqueta'
          AND tg.tgname = 'trg_bloqueia_inativar_etiqueta_com_produto_ativo'
          AND NOT tg.tgisinternal
    ) x;

    v_old_idx := to_regclass('homologacao.uq_etiqueta_codigo') IS NOT NULL;
    v_hist_idx := to_regclass('homologacao.uq_etiqueta_codigo_interno_ativo') IS NOT NULL;

    IF v_existentes = 6 AND v_comentados = 6 AND NOT v_old_idx AND NOT v_hist_idx THEN
        RAISE NOTICE 'PACOTE_COMPLETAMENTE_APLICADO - nenhuma alteracao executada.';
        RAISE NOTICE 'OK - APLICACAO 036 HML concluida';
        RETURN;
    ELSIF v_existentes <> 0 THEN
        RAISE EXCEPTION 'ESTADO_PARCIAL_OU_INCOMPATIVEL: objetos_encontrados=%, objetos_com_comentario_036=%, indice_parcial_antigo=%, indice_historico=%', v_existentes, v_comentados, v_old_idx, v_hist_idx;
    END IF;

    SELECT COUNT(*) INTO v_inconsistencias
    FROM (
        SELECT 1 FROM (
            SELECT upper(trim(codigo_interno))
            FROM homologacao.etiqueta
            GROUP BY upper(trim(codigo_interno))
            HAVING COUNT(*) > 1
        ) d
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE codigo_interno IS NULL OR trim(codigo_interno) = '' OR char_length(trim(codigo_interno)) > 80
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE nome_etiqueta IS NULL OR char_length(trim(nome_etiqueta)) < 2 OR char_length(trim(nome_etiqueta)) > 80
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE descricao_etiqueta IS NOT NULL AND char_length(trim(descricao_etiqueta)) > 255
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE tipo_etiqueta IS NULL OR tipo_etiqueta NOT IN ('CAIXA', 'PALETE', 'HU', 'INTERNA', 'OUTRA')
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta e JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta = e.codigo_modelo_etiqueta WHERE e.situacao_etiqueta = true AND m.situacao_modelo_etiqueta = false
        UNION ALL
        SELECT 1 FROM homologacao.produto_etiqueta pe JOIN homologacao.etiqueta e ON e.codigo_etiqueta = pe.codigo_etiqueta WHERE pe.situacao_produto_etiqueta = true AND e.situacao_etiqueta = false
    ) s;
    IF v_inconsistencias > 0 THEN
        RAISE EXCEPTION 'APLICACAO 036 HML bloqueada: existem % ocorrencias invalidas. Execute o preflight para listar os registros.', v_inconsistencias;
    END IF;

    -- Remover os indices parciais legados somente depois da verificacao de duplicidade global e demais precondicoes.
    EXECUTE 'DROP INDEX IF EXISTS homologacao.uq_etiqueta_codigo';
    EXECUTE 'DROP INDEX IF EXISTS homologacao.uq_etiqueta_codigo_interno_ativo';

    -- Criar indice global. Importante: o nome do indice NAO deve ser qualificado com schema no CREATE INDEX.
    EXECUTE 'CREATE UNIQUE INDEX uq_etiqueta_codigo_interno_global ON homologacao.etiqueta (upper(trim(codigo_interno)))';
    EXECUTE 'COMMENT ON INDEX homologacao.uq_etiqueta_codigo_interno_global IS ' || quote_literal(v_comment);

    EXECUTE 'ALTER TABLE homologacao.etiqueta ADD CONSTRAINT ck_etiqueta_codigo_tamanho CHECK (char_length(trim(codigo_interno)) BETWEEN 1 AND 80)';
    EXECUTE 'COMMENT ON CONSTRAINT ck_etiqueta_codigo_tamanho ON homologacao.etiqueta IS ' || quote_literal(v_comment);

    EXECUTE 'ALTER TABLE homologacao.etiqueta ADD CONSTRAINT ck_etiqueta_nome_tamanho CHECK (char_length(trim(nome_etiqueta)) BETWEEN 2 AND 80)';
    EXECUTE 'COMMENT ON CONSTRAINT ck_etiqueta_nome_tamanho ON homologacao.etiqueta IS ' || quote_literal(v_comment);

    EXECUTE 'ALTER TABLE homologacao.etiqueta ADD CONSTRAINT ck_etiqueta_descricao_tamanho CHECK (descricao_etiqueta IS NULL OR char_length(trim(descricao_etiqueta)) <= 255)';
    EXECUTE 'COMMENT ON CONSTRAINT ck_etiqueta_descricao_tamanho ON homologacao.etiqueta IS ' || quote_literal(v_comment);

    EXECUTE $ddl$
CREATE FUNCTION homologacao.fn_bloqueia_inativar_etiqueta_com_produto_ativo()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
BEGIN
    IF OLD.situacao_etiqueta = true AND NEW.situacao_etiqueta = false THEN
        IF EXISTS (
            SELECT 1
              FROM homologacao.produto_etiqueta pe
             WHERE pe.codigo_etiqueta = OLD.codigo_etiqueta
               AND pe.situacao_produto_etiqueta = true
        ) THEN
            RAISE EXCEPTION 'Nao e possivel inativar esta etiqueta porque existem produtos ativos vinculados a ela.';
        END IF;
    END IF;

    RETURN NEW;
END;
$fn$;
$ddl$;
    EXECUTE 'COMMENT ON FUNCTION homologacao.fn_bloqueia_inativar_etiqueta_com_produto_ativo() IS ' || quote_literal(v_comment);

    EXECUTE 'CREATE TRIGGER trg_bloqueia_inativar_etiqueta_com_produto_ativo BEFORE UPDATE OF situacao_etiqueta ON homologacao.etiqueta FOR EACH ROW EXECUTE FUNCTION homologacao.fn_bloqueia_inativar_etiqueta_com_produto_ativo()';
    EXECUTE 'COMMENT ON TRIGGER trg_bloqueia_inativar_etiqueta_com_produto_ativo ON homologacao.etiqueta IS ' || quote_literal(v_comment);

    RAISE NOTICE 'OK - APLICACAO 036 HML concluida';
END $$;

COMMIT;
