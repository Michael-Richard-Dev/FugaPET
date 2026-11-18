\set ON_ERROR_STOP on
-- ============================================================
-- 066_catalogo_produto_centro_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 066 - ROLLBACK
--
-- Apaga o ESPELHO local do catalogo. Nenhuma decisao do usuario se perde: os
-- vinculos vivem em produto_etiqueta_vinculo, que este script NAO toca.
--
-- O custo de reverter e tempo: a proxima sincronizacao volta a buscar tudo no SAP,
-- como antes do incremento.
--
-- ATENCAO: reverter aqui sem reverter o executavel deixa a tela sem a tabela que
-- ela le. Reverter os dois, ou nenhum.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '066 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
END
$$;

\echo '-- quantos materiais espelhados serao perdidos (recuperaveis do SAP):'
SELECT count(*) AS materiais FROM desenvolvimento.produto_catalogo_centro;

\echo '-- quantos vinculos permanecem intactos:'
SELECT count(*) AS vinculos FROM desenvolvimento.produto_etiqueta_vinculo;

DROP TABLE IF EXISTS desenvolvimento.produto_catalogo_centro;

COMMIT;

\echo '== 066 revertido. Os vinculos nao foram tocados. =='
