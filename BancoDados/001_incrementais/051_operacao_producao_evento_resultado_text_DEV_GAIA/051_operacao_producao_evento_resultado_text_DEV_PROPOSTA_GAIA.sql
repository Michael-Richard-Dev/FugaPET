\set ON_ERROR_STOP on
-- ============================================================
-- 051_operacao_producao_evento_resultado_text_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 051 - operacao_producao_evento.resultado: varchar(40) -> text
-- PROPOSTA (APLICA DDL) - ONE-SHOT FAIL-CLOSED
--
-- A procedencia, as diferencas declaradas em relacao ao pacote Q e a justificativa
-- medida (o pior caso hoje tem 39 de 40 caracteres) estao no PREFLIGHT deste mesmo
-- pacote.
--
-- O ALTER so acontece sob o contrato exato de partida. Se a coluna ja for text, o
-- script e no-op e avisa. Qualquer outro estado ABORTA.
--
-- Antes do DDL grava-se contagem, maior comprimento e um hash de todas as linhas;
-- depois do DDL os tres sao reconferidos. Alargar varchar(40) para text nao pode
-- alterar nenhum valor -- e isso e comprovado, nao suposto.
--
--   SEM DML de negocio. NAO SAP.
-- ============================================================

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '2min';
SET LOCAL search_path = pg_catalog, desenvolvimento;

-- ============================================================
-- 0. GUARDAS + SNAPSHOT DE ANTES
-- ============================================================

DO $$
DECLARE
    v_contrato_exato boolean;
    v_ja_aplicado    boolean;
    v_linhas         bigint;
    v_max            text;
    v_hash           text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '051 ABORTADO: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '051 ABORTADO: principal administrativo inesperado: %', current_user;
    END IF;

    IF session_user = 'fugapet_dev_app' THEN
        RAISE EXCEPTION '051 ABORTADO: o papel de aplicacao nao pode executar DDL.';
    END IF;

    IF to_regnamespace('desenvolvimento') IS NULL THEN
        RAISE EXCEPTION '051 ABORTADO: schema desenvolvimento ausente.';
    END IF;

    IF to_regclass('desenvolvimento.operacao_producao_evento') IS NULL THEN
        RAISE EXCEPTION '051 ABORTADO: tabela operacao_producao_evento ausente.';
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento' AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado' AND data_type = 'character varying'
           AND character_maximum_length = 40 AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::character varying', '[[:space:]]', '', 'g')
    ) INTO v_contrato_exato;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento' AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado' AND data_type = 'text' AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::text', '[[:space:]]', '', 'g')
    ) INTO v_ja_aplicado;

    IF NOT v_contrato_exato AND NOT v_ja_aplicado THEN
        RAISE EXCEPTION '051 ABORTADO: CONTRATO_ATUAL_DIVERGENTE.';
    END IF;

    SELECT count(*),
           coalesce(max(length(resultado))::text, '<NULL>'),
           md5(coalesce(string_agg(codigo_evento_apontamento::text || chr(31) || resultado,
                                   chr(30) ORDER BY codigo_evento_apontamento), ''))
      INTO v_linhas, v_max, v_hash
      FROM desenvolvimento.operacao_producao_evento;

    PERFORM set_config('fugapet051.linhas', v_linhas::text, true);
    PERFORM set_config('fugapet051.max', v_max, true);
    PERFORM set_config('fugapet051.hash', v_hash, true);
    PERFORM set_config('fugapet051.aplicar', CASE WHEN v_contrato_exato THEN 'SIM' ELSE 'NAO' END, true);

    RAISE NOTICE 'ANTES: LINHAS=%  MAIOR=%  HASH=%', v_linhas, v_max, v_hash;
    IF v_ja_aplicado THEN
        RAISE NOTICE 'CLASSIFICACAO=JA_APLICADO_NOOP';
    ELSE
        RAISE NOTICE 'CLASSIFICACAO=APLICAVEL';
    END IF;
END
$$;

-- ============================================================
-- 1. O ALTER, condicionado ao contrato de partida
-- ============================================================

DO $$
BEGIN
    IF current_setting('fugapet051.aplicar', true) = 'SIM' THEN
        EXECUTE 'ALTER TABLE desenvolvimento.operacao_producao_evento ALTER COLUMN resultado TYPE text';

        -- ACRESCIMO DECLARADO DESTE PORT, e correcao de um defeito do pacote original.
        -- Medido aqui: ALTER COLUMN ... TYPE text NAO reescreve o default. O tipo vira
        -- text e o default permanece ''::character varying. O proprio post-check do
        -- pacote Q exige ''::text, portanto o script original abortaria neste ponto.
        -- O SET DEFAULT abaixo fecha o contrato que a VALIDACAO do Q adjudica.
        EXECUTE 'ALTER TABLE desenvolvimento.operacao_producao_evento ALTER COLUMN resultado SET DEFAULT ''''::text';
        RAISE NOTICE 'ALTER=EXECUTADO (tipo + default)';
    ELSE
        RAISE NOTICE 'ALTER=NAO_EXECUTADO (coluna ja era text)';
    END IF;
END
$$;

-- ============================================================
-- 2. POST-CHECKS FAIL-CLOSED
-- ============================================================

DO $$
DECLARE
    v_contrato_ok boolean;
    v_linhas      bigint;
    v_max         text;
    v_hash        text;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento' AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado' AND data_type = 'text' AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::text', '[[:space:]]', '', 'g')
    ) INTO v_contrato_ok;

    IF NOT v_contrato_ok THEN
        RAISE EXCEPTION '051 ABORTADO: POSTCHECK_CONTRATO_DIVERGENTE (esperado text NOT NULL DEFAULT '''').';
    END IF;

    SELECT count(*),
           coalesce(max(length(resultado))::text, '<NULL>'),
           md5(coalesce(string_agg(codigo_evento_apontamento::text || chr(31) || resultado,
                                   chr(30) ORDER BY codigo_evento_apontamento), ''))
      INTO v_linhas, v_max, v_hash
      FROM desenvolvimento.operacao_producao_evento;

    IF v_linhas::text IS DISTINCT FROM current_setting('fugapet051.linhas', true) THEN
        RAISE EXCEPTION '051 ABORTADO: ROWS_DIVERGENTES (antes=%, depois=%).',
            current_setting('fugapet051.linhas', true), v_linhas;
    END IF;

    IF v_max IS DISTINCT FROM current_setting('fugapet051.max', true) THEN
        RAISE EXCEPTION '051 ABORTADO: MAX_LENGTH_DIVERGENTE (antes=%, depois=%).',
            current_setting('fugapet051.max', true), v_max;
    END IF;

    IF v_hash IS DISTINCT FROM current_setting('fugapet051.hash', true) THEN
        RAISE EXCEPTION '051 ABORTADO: VALORES_DIVERGENTES (hash de antes e depois diferem).';
    END IF;

    RAISE NOTICE 'POSTCHECK=OK  CONTRATO=text  LINHAS=%  MAIOR=%  VALORES=INALTERADOS', v_linhas, v_max;
END
$$;

COMMIT;

\echo '============================================================'
\echo '051_PROPOSTA_APLICADA'
\echo '============================================================'
\echo 'CONTRATO=desenvolvimento.operacao_producao_evento.resultado text NOT NULL DEFAULT '''''
\echo 'LINHAS_ALTERADAS=0 (alargamento de tipo)'
\echo 'VALORES=INALTERADOS (hash conferido antes e depois)'
\echo 'SAP_WRITE=0'
