\set ON_ERROR_STOP on
-- ============================================================
-- 060_entrada_documento_material_colunas_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 060 - Colunas do Documento de Material na Entrada
-- PROPOSTA (ESCRITA) - transacao unica, tudo ou nada
--
-- Acrescenta 4 colunas ANULAVEIS, SEM default, replicando o Q byte a byte em tipo e
-- nulidade. Nao altera coluna existente, nao cria indice, constraint ou trigger, nao
-- escreve uma linha de dado. IDEMPOTENTE por IF NOT EXISTS.
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '060 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.entrada_produto_lancamento') IS NULL
       OR to_regclass('desenvolvimento.entrada_produto_item') IS NULL THEN
        RAISE EXCEPTION '060 ABORTADO: tabela-alvo ausente.';
    END IF;

    RAISE NOTICE 'GUARDAS=OK';
END
$$;

ALTER TABLE desenvolvimento.entrada_produto_lancamento
    ADD COLUMN IF NOT EXISTS documento_material_sap           varchar(20),
    ADD COLUMN IF NOT EXISTS exercicio_documento_material_sap varchar(4),
    ADD COLUMN IF NOT EXISTS enviado_sap_em                   timestamptz;

ALTER TABLE desenvolvimento.entrada_produto_item
    ADD COLUMN IF NOT EXISTS documento_material_item          varchar(20);

-- ------------------------------------------------------------
-- GUARDA DE SAIDA
-- Nao basta "a coluna existe": exige TIPO, TAMANHO e NULIDADE iguais aos do Q.
-- Uma coluna criada com o tipo errado passaria num teste de existencia e falharia
-- no primeiro envio real.
-- ------------------------------------------------------------
DO $$
DECLARE
    v_divergencias text;
BEGIN
    SELECT string_agg(
               format('%s.%s: esperado %s/%s, encontrado %s/%s',
                      esperado.tabela, esperado.coluna,
                      esperado.tipo, CASE WHEN esperado.not_null THEN 'NOT NULL' ELSE 'NULL' END,
                      COALESCE(real.tipo, '<ausente>'),
                      CASE WHEN real.not_null IS NULL THEN '<ausente>'
                           WHEN real.not_null THEN 'NOT NULL' ELSE 'NULL' END),
               '; ')
      INTO v_divergencias
      FROM (VALUES
            ('entrada_produto_lancamento', 'documento_material_sap',           'character varying(20)',    false),
            ('entrada_produto_lancamento', 'exercicio_documento_material_sap', 'character varying(4)',     false),
            ('entrada_produto_lancamento', 'enviado_sap_em',                   'timestamp with time zone', false),
            ('entrada_produto_item',       'documento_material_item',          'character varying(20)',    false)
           ) AS esperado(tabela, coluna, tipo, not_null)
      LEFT JOIN LATERAL (
            SELECT format_type(a.atttypid, a.atttypmod) AS tipo, a.attnotnull AS not_null
              FROM pg_attribute a
              JOIN pg_class c ON c.oid = a.attrelid
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'desenvolvimento'
               AND c.relname = esperado.tabela
               AND a.attname = esperado.coluna
               AND a.attnum > 0 AND NOT a.attisdropped
           ) AS real ON TRUE
     WHERE real.tipo IS DISTINCT FROM esperado.tipo
        OR real.not_null IS DISTINCT FROM esperado.not_null;

    IF v_divergencias IS NOT NULL THEN
        RAISE EXCEPTION '060 ABORTADO: colunas divergentes do Q: %.', v_divergencias;
    END IF;

    RAISE NOTICE 'SAIDA=OK  as 4 colunas conferem com o Q em tipo e nulidade';
END
$$;

COMMIT;

\echo ''
\echo '=== 060 PROPOSTA aplicado. Rode o VALIDACAO. ==='
\echo ''
