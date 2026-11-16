\set ON_ERROR_STOP on
-- ============================================================
-- 064_pa047_funcoes_paletizacao_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- ROLLBACK (ESCRITA) - transacao unica
--
-- Remove as duas funcoes criadas pelo 064, devolvendo o banco ao estado anterior.
--
-- EFEITO PRATICO, DITO SEM RODEIO: a tela "Paletizacao por HU" volta a falhar ao
-- carregar HUs, com o mesmo erro 42883 "function ... does not exist". Isto e a
-- volta ao estado anterior, nao uma avaria.
--
-- Seguro de rodar: as funcoes sao STABLE e somente leitura, entao remove-las nao
-- desfaz dado nenhum. Nenhum palete, caixa ou etapa e tocado.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '064 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    RAISE NOTICE 'GUARDAS=OK';
END
$$;

DROP FUNCTION IF EXISTS desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(text,text,text);

DO $$
DECLARE v_restantes integer;
BEGIN
    SELECT count(*) INTO v_restantes
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname LIKE 'fn_pa_047%';

    IF v_restantes <> 0 THEN
        RAISE EXCEPTION '064 ROLLBACK ABORTADO: sobraram % funcao(oes) fn_pa_047_*.', v_restantes;
    END IF;
    RAISE NOTICE 'SAIDA=OK  064 revertido; nenhuma funcao fn_pa_047_* restante';
END
$$;

COMMIT;

\echo ''
\echo '=== 064 revertido. A Paletizacao por HU volta a falhar com 42883. ==='
\echo ''
