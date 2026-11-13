\set ON_ERROR_STOP on
-- ============================================================
-- 060_entrada_documento_material_colunas_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 060 - Colunas do Documento de Material na Entrada
-- ROLLBACK (ESCRITA DESTRUTIVA) - transacao unica
--
-- ATENCAO: DROP COLUMN APAGA DADO. Se algum envio 101 ja tiver ocorrido depois do
-- 060, estas colunas guardam o NUMERO DO DOCUMENTO DE MATERIAL criado no SAP --
-- a unica prova local de que o movimento existe. Apagar isso nao desfaz o movimento
-- no SAP; apenas faz o sistema esquecer que ele aconteceu, e o proximo envio
-- passaria a parecer o primeiro.
--
-- Por isso a guarda abaixo ABORTA se houver qualquer linha preenchida. Para forcar
-- mesmo assim, e preciso decisao humana explicita: exporte as linhas antes e
-- comente a guarda conscientemente.
-- ============================================================

BEGIN;

DO $$
DECLARE
    v_lancamentos bigint;
    v_itens       bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '060 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    SELECT count(*) INTO v_lancamentos
      FROM desenvolvimento.entrada_produto_lancamento
     WHERE documento_material_sap IS NOT NULL
        OR exercicio_documento_material_sap IS NOT NULL
        OR enviado_sap_em IS NOT NULL;

    SELECT count(*) INTO v_itens
      FROM desenvolvimento.entrada_produto_item
     WHERE documento_material_item IS NOT NULL;

    IF v_lancamentos <> 0 OR v_itens <> 0 THEN
        RAISE EXCEPTION
            '060 ROLLBACK ABORTADO: ha % lancamento(s) e % item(ns) com documento de material '
            'registrado. Apagar estas colunas destruiria a unica prova local de movimentos 101 '
            'que EXISTEM no SAP. Exporte antes e decida explicitamente.',
            v_lancamentos, v_itens;
    END IF;

    RAISE NOTICE 'GUARDAS=OK  nenhuma linha preenchida; a reversao nao perde dado';
END
$$;

ALTER TABLE desenvolvimento.entrada_produto_lancamento
    DROP COLUMN IF EXISTS documento_material_sap,
    DROP COLUMN IF EXISTS exercicio_documento_material_sap,
    DROP COLUMN IF EXISTS enviado_sap_em;

ALTER TABLE desenvolvimento.entrada_produto_item
    DROP COLUMN IF EXISTS documento_material_item;

DO $$
DECLARE
    v_restou text;
BEGIN
    SELECT string_agg(c.relname || '.' || a.attname, ', ') INTO v_restou
      FROM pg_attribute a
      JOIN pg_class c ON c.oid = a.attrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'desenvolvimento'
       AND a.attnum > 0 AND NOT a.attisdropped
       AND ((c.relname = 'entrada_produto_lancamento'
             AND a.attname IN ('documento_material_sap','exercicio_documento_material_sap','enviado_sap_em'))
         OR (c.relname = 'entrada_produto_item' AND a.attname = 'documento_material_item'));

    IF v_restou IS NOT NULL THEN
        RAISE EXCEPTION '060 ROLLBACK ABORTADO: sobraram colunas: %.', v_restou;
    END IF;
    RAISE NOTICE 'SAIDA=OK  060 revertido';
END
$$;

COMMIT;

\echo ''
\echo '=== 060 revertido. A consulta D2 volta a falhar com 42703 e o P5 a bloquear. ==='
\echo ''
