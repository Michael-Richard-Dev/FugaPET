\set ON_ERROR_STOP on
-- ============================================================
-- 051_operacao_producao_evento_resultado_text_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 051 - Rollback (text -> varchar(40))
--
-- ROLLBACK_EXECUTION = NAO_AUTORIZADO por padrao. Nao executar sem autorizacao
-- expressa.
--
-- Este rollback ESTREITA o tipo, e por isso e o unico sentido da migracao que
-- pode PERDER DADO. A guarda central: se existir qualquer linha com resultado
-- acima de 40 caracteres, ABORTA. Nunca truncar auditoria.
--
-- Lembrete do que se perde ao reverter: a coluna volta a ficar a um caractere do
-- estouro no pior caso do codigo ('SEM_PERMISSAO_MAPEAMENTO_CAMPO_ETIQUETA', 39),
-- e parte dos eventos e gravada dentro da transacao de transicao de estado.
--
--   SEM DML de negocio. NAO SAP.
-- ============================================================

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '2min';
SET LOCAL search_path = pg_catalog, desenvolvimento;

DO $$
DECLARE
    v_contrato_text boolean;
    v_ja_revertido  boolean;
    v_acima_40      bigint;
    v_linhas        bigint;
    v_hash          text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '051 ROLLBACK: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '051 ROLLBACK: principal administrativo inesperado: %', current_user;
    END IF;

    IF session_user = 'fugapet_dev_app' THEN
        RAISE EXCEPTION '051 ROLLBACK: o papel de aplicacao nao pode executar DDL.';
    END IF;

    IF to_regclass('desenvolvimento.operacao_producao_evento') IS NULL THEN
        RAISE EXCEPTION '051 ROLLBACK: tabela ausente.';
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento' AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado' AND data_type = 'text' AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::text', '[[:space:]]', '', 'g')
    ) INTO v_contrato_text;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento' AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado' AND data_type = 'character varying'
           AND character_maximum_length = 40 AND is_nullable = 'NO'
    ) INTO v_ja_revertido;

    IF v_ja_revertido THEN
        RAISE EXCEPTION '051 ROLLBACK: a coluna ja e varchar(40); nada a reverter.';
    END IF;

    IF NOT v_contrato_text THEN
        RAISE EXCEPTION '051 ROLLBACK: contrato atual nao e o deixado pela PROPOSTA; rollback bloqueado.';
    END IF;

    -- GUARDA CENTRAL: estreitar nao pode truncar auditoria.
    SELECT count(*) INTO v_acima_40
      FROM desenvolvimento.operacao_producao_evento
     WHERE length(resultado) > 40;

    IF v_acima_40 <> 0 THEN
        RAISE EXCEPTION
            '051 ROLLBACK: % linhas tem resultado acima de 40 caracteres; reverter truncaria auditoria. BLOQUEADO.',
            v_acima_40;
    END IF;

    SELECT count(*),
           md5(coalesce(string_agg(codigo_evento_apontamento::text || chr(31) || resultado,
                                   chr(30) ORDER BY codigo_evento_apontamento), ''))
      INTO v_linhas, v_hash
      FROM desenvolvimento.operacao_producao_evento;

    PERFORM set_config('fugapet051rb.linhas', v_linhas::text, true);
    PERFORM set_config('fugapet051rb.hash', v_hash, true);

    RAISE NOTICE 'GUARDAS=OK  LINHAS=%  ACIMA_DE_40=0', v_linhas;
END
$$;

ALTER TABLE desenvolvimento.operacao_producao_evento
    ALTER COLUMN resultado TYPE character varying(40);

ALTER TABLE desenvolvimento.operacao_producao_evento
    ALTER COLUMN resultado SET DEFAULT ''::character varying;

DO $$
DECLARE
    v_ok     boolean;
    v_linhas bigint;
    v_hash   text;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento' AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado' AND data_type = 'character varying'
           AND character_maximum_length = 40 AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::character varying', '[[:space:]]', '', 'g')
    ) INTO v_ok;

    IF NOT v_ok THEN
        RAISE EXCEPTION '051 ROLLBACK POSTCHECK: contrato nao voltou a varchar(40) NOT NULL DEFAULT ''''.';
    END IF;

    SELECT count(*),
           md5(coalesce(string_agg(codigo_evento_apontamento::text || chr(31) || resultado,
                                   chr(30) ORDER BY codigo_evento_apontamento), ''))
      INTO v_linhas, v_hash
      FROM desenvolvimento.operacao_producao_evento;

    IF v_linhas::text IS DISTINCT FROM current_setting('fugapet051rb.linhas', true) THEN
        RAISE EXCEPTION '051 ROLLBACK POSTCHECK: contagem divergiu.';
    END IF;

    IF v_hash IS DISTINCT FROM current_setting('fugapet051rb.hash', true) THEN
        RAISE EXCEPTION '051 ROLLBACK POSTCHECK: valores divergiram; houve truncamento.';
    END IF;

    RAISE NOTICE 'ROLLBACK=CONCLUIDO  CONTRATO=varchar(40)  VALORES=INALTERADOS';
END
$$;

COMMIT;

\echo '051_ROLLBACK_CONCLUIDO'
\echo 'CONTRATO=varchar(40) NOT NULL DEFAULT '''''
\echo 'TRUNCAMENTO=NENHUM'
\echo 'SAP_WRITE=0'
