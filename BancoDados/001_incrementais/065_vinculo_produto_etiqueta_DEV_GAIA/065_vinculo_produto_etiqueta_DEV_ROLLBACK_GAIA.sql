\set ON_ERROR_STOP on
-- ============================================================
-- 065_vinculo_produto_etiqueta_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 065 - ROLLBACK
--
-- DESTRUTIVO: apaga a tabela e, com ela, TODOS os vinculos ja cadastrados. Nao ha
-- como recuperar as decisoes do usuario depois disso. Use somente enquanto a tela
-- ainda nao estiver em uso; passado esse ponto, exporte a tabela antes.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '065 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
END
$$;

\echo '-- quantos vinculos serao perdidos:'
SELECT count(*) AS vinculos_a_perder
  FROM desenvolvimento.produto_etiqueta_vinculo;

DROP TABLE IF EXISTS desenvolvimento.produto_etiqueta_vinculo;

COMMIT;

\echo '== 065 revertido. =='
