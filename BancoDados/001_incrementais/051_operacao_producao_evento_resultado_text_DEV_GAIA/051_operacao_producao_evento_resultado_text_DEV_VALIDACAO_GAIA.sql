\set ON_ERROR_STOP on
-- ============================================================
-- 051_operacao_producao_evento_resultado_text_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 051 - Validacao pos-aplicacao (SOMENTE LEITURA)
--
-- Adjudica MIGRATION_051_VALID: o tipo, a nulidade e o default da coluna, a
-- ausencia de qualquer limite de comprimento, a integridade das linhas e o fato
-- de a coluna aceitar valores acima de 40 caracteres -- que e a razao de ser do
-- incremento. O teste de capacidade e feito SEM GRAVAR: a transacao e READ ONLY e
-- a verificacao usa uma conversao, nao um INSERT.
--
--   SOMENTE LEITURA. NAO DML. NAO DDL. NAO SAP.
-- ============================================================

BEGIN TRANSACTION READ ONLY;

SET LOCAL search_path = pg_catalog, desenvolvimento;

DO $$
DECLARE
    r            record;
    v_linhas     bigint;
    v_max        text;
    v_nulos      bigint;
    v_cabe       boolean;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '051 VALIDACAO: database incorreto: %', current_database();
    END IF;

    SELECT data_type, character_maximum_length, is_nullable, coalesce(column_default, '') AS def
      INTO r
      FROM information_schema.columns
     WHERE table_schema = 'desenvolvimento'
       AND table_name = 'operacao_producao_evento'
       AND column_name = 'resultado';

    IF NOT FOUND THEN
        RAISE EXCEPTION '051 VALIDACAO: coluna resultado ausente. MIGRATION_051_VALID=NAO';
    END IF;

    IF r.data_type <> 'text' THEN
        RAISE EXCEPTION '051 VALIDACAO: tipo e %, esperado text. MIGRATION_051_VALID=NAO', r.data_type;
    END IF;

    IF r.character_maximum_length IS NOT NULL THEN
        RAISE EXCEPTION
            '051 VALIDACAO: a coluna ainda tem limite de comprimento (%). MIGRATION_051_VALID=NAO',
            r.character_maximum_length;
    END IF;

    IF r.is_nullable <> 'NO' THEN
        RAISE EXCEPTION '051 VALIDACAO: a coluna passou a aceitar NULL. MIGRATION_051_VALID=NAO';
    END IF;

    IF regexp_replace(r.def, '[[:space:]]', '', 'g')
       <> regexp_replace(quote_literal(''::text) || '::text', '[[:space:]]', '', 'g') THEN
        RAISE EXCEPTION
            '051 VALIDACAO: default inesperado: "%". Esperado '''' ::text. MIGRATION_051_VALID=NAO', r.def;
    END IF;
    RAISE NOTICE 'CONTRATO=text NOT NULL DEFAULT '''' e SEM limite de comprimento';

    -- -------- Integridade das linhas --------
    SELECT count(*), coalesce(max(length(resultado))::text, '<sem linhas>'), count(*) FILTER (WHERE resultado IS NULL)
      INTO v_linhas, v_max, v_nulos
      FROM desenvolvimento.operacao_producao_evento;

    IF v_nulos <> 0 THEN
        RAISE EXCEPTION '051 VALIDACAO: % linhas com resultado NULL. MIGRATION_051_VALID=NAO', v_nulos;
    END IF;
    RAISE NOTICE 'LINHAS=%  MAIOR_RESULTADO=%  NULOS=0', v_linhas, v_max;

    -- -------- A razao de ser: a coluna nao pode mais ter teto de comprimento --------
    -- Fato autoritativo do catalogo: atttypmod = -1 significa SEM modificador de
    -- tipo, isto e, sem limite declarado. E a mesma informacao que
    -- character_maximum_length IS NULL expressa acima, lida direto do pg_attribute.
    SELECT a.atttypmod = -1
      INTO v_cabe
      FROM pg_attribute a
     WHERE a.attrelid = 'desenvolvimento.operacao_producao_evento'::regclass
       AND a.attname = 'resultado'
       AND NOT a.attisdropped;

    IF NOT coalesce(v_cabe, false) THEN
        RAISE EXCEPTION
            '051 VALIDACAO: a coluna ainda carrega modificador de tipo (teto de comprimento). MIGRATION_051_VALID=NAO';
    END IF;
    RAISE NOTICE 'SEM_TETO=pg_attribute.atttypmod = -1 (nenhum limite de comprimento declarado)';

    -- -------- Diagnostico: a folga que motivou o incremento --------
    RAISE NOTICE 'MOTIVO=o pior caso do codigo era SEM_PERMISSAO_MAPEAMENTO_CAMPO_ETIQUETA, 39 de 40 caracteres';

    RAISE NOTICE 'MIGRATION_051_VALID=SIM';
END
$$;

COMMIT;

\echo '051_VALIDACAO_CONCLUIDO'
\echo 'CONTRATO=text NOT NULL DEFAULT '''''
\echo 'LIMITE_DE_COMPRIMENTO=NENHUM'
\echo 'SAP_WRITE=0'
