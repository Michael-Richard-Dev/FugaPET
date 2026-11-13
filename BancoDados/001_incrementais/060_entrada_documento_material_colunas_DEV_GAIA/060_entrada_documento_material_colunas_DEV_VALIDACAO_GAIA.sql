\set ON_ERROR_STOP on
-- ============================================================
-- 060_entrada_documento_material_colunas_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 060 - Colunas do Documento de Material na Entrada
-- VALIDACAO (SOMENTE LEITURA)
--
-- V3 e o teste que importa: nao pergunta se a coluna existe no catalogo, mas se a
-- CONSULTA QUE ESTAVA FALHANDO volta a executar. Catalogo conferido nao e o mesmo
-- que consulta que roda.
-- ============================================================

\echo ''
\echo '=== 060 VALIDACAO ==='

-- V1 - as 4 colunas existem com tipo e nulidade do Q.
DO $$
DECLARE
    v_div text;
BEGIN
    SELECT string_agg(e.tabela || '.' || e.coluna, ', ') INTO v_div
      FROM (VALUES
            ('entrada_produto_lancamento', 'documento_material_sap',           'character varying(20)'),
            ('entrada_produto_lancamento', 'exercicio_documento_material_sap', 'character varying(4)'),
            ('entrada_produto_lancamento', 'enviado_sap_em',                   'timestamp with time zone'),
            ('entrada_produto_item',       'documento_material_item',          'character varying(20)')
           ) AS e(tabela, coluna, tipo)
     WHERE NOT EXISTS (
            SELECT 1
              FROM pg_attribute a
              JOIN pg_class c ON c.oid = a.attrelid
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'desenvolvimento'
               AND c.relname = e.tabela
               AND a.attname = e.coluna
               AND a.attnum > 0 AND NOT a.attisdropped
               AND format_type(a.atttypid, a.atttypmod) = e.tipo
               AND a.attnotnull IS FALSE);

    IF v_div IS NOT NULL THEN
        RAISE EXCEPTION 'V1 FALHOU: ausentes ou divergentes: %.', v_div;
    END IF;
    RAISE NOTICE 'V1=OK  4 colunas com tipo e nulidade do Q';
END
$$;

-- V2 - nenhuma ganhou DEFAULT. Um default aqui inventaria documento de material.
DO $$
DECLARE
    v_com_default text;
BEGIN
    SELECT string_agg(c.relname || '.' || a.attname, ', ') INTO v_com_default
      FROM pg_attribute a
      JOIN pg_class c ON c.oid = a.attrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
      JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
     WHERE n.nspname = 'desenvolvimento'
       AND ((c.relname = 'entrada_produto_lancamento'
             AND a.attname IN ('documento_material_sap','exercicio_documento_material_sap','enviado_sap_em'))
         OR (c.relname = 'entrada_produto_item' AND a.attname = 'documento_material_item'));

    IF v_com_default IS NOT NULL THEN
        RAISE EXCEPTION 'V2 FALHOU: coluna com DEFAULT indevido: %.', v_com_default;
    END IF;
    RAISE NOTICE 'V2=OK  nenhuma coluna com default';
END
$$;

-- V3 - CAPACIDADE REAL: o trecho da consulta D2 que quebrava com 42703 agora executa.
--      Reproduz o acesso exato (l.enviado_sap_em + os dois campos de documento) sobre
--      a mesma juncao, e nao apenas um SELECT da coluna isolada.
DO $$
DECLARE
    v_linhas bigint;
BEGIN
    SELECT count(*) INTO v_linhas
      FROM desenvolvimento.entrada_produto_lancamento l
      LEFT JOIN desenvolvimento.entrada_produto_item i
             ON i.codigo_entrada_produto_lancamento = l.codigo_entrada_produto_lancamento
     WHERE l.enviado_sap_em IS NULL
        OR l.documento_material_sap IS NOT NULL
        OR l.exercicio_documento_material_sap IS NOT NULL
        OR i.documento_material_item IS NOT NULL;

    RAISE NOTICE 'V3=OK  a juncao que falhava com 42703 executou (% linha(s))', v_linhas;
END
$$;

-- V4 - nenhuma linha foi preenchida pelo incremento. Ele e estrutural.
DO $$
DECLARE
    v_preenchidas bigint;
BEGIN
    SELECT count(*) INTO v_preenchidas
      FROM desenvolvimento.entrada_produto_lancamento
     WHERE documento_material_sap IS NOT NULL
        OR exercicio_documento_material_sap IS NOT NULL
        OR enviado_sap_em IS NOT NULL;

    IF v_preenchidas <> 0 THEN
        RAISE EXCEPTION
            'V4 FALHOU: % lancamento(s) ja com documento de material. O 060 nao escreve dado; '
            'investigue a origem.', v_preenchidas;
    END IF;
    RAISE NOTICE 'V4=OK  nenhuma linha preenchida; incremento puramente estrutural';
END
$$;

\echo ''
\echo '=== 060 VALIDACAO: todos os checks passaram ==='
\echo ''
