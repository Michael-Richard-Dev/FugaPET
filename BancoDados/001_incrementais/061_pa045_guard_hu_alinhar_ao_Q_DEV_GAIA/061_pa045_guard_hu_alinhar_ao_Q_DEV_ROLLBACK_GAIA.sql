\set ON_ERROR_STOP on
-- ============================================================
-- 061_pa045_guard_hu_alinhar_ao_Q_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- ROLLBACK (ESCRITA) - transacao unica
--
-- Devolve a funcao ao corpo que o DEV tinha antes do 061: a guarda volta a cobrir
-- as QUATRO transicoes, incluindo PREVIEW_HU_GERADO.
--
-- EFEITO PRATICO: a pesagem de caixa volta a falhar com
-- "Nao foi possivel registrar a caixa no banco", porque o preview passa a exigir
-- um 101 confirmado que ainda nao existe nesse ponto do fluxo. Isto e a volta ao
-- estado anterior, nao uma avaria -- mas e bom saber antes de rodar.
--
-- O gatilho nao e tocado: ele continua ativo e apontando para a mesma funcao.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '061 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    RAISE NOTICE 'GUARDAS=OK';
END
$$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_pa_045_guard_hu_pos_101()
 RETURNS trigger
 LANGUAGE plpgsql
 SET search_path TO 'pg_catalog', 'desenvolvimento'
AS $function$
DECLARE v_etapas integer; v_101 boolean;
BEGIN
    IF NEW.status_hu_caixa IS NOT DISTINCT FROM OLD.status_hu_caixa THEN RETURN NEW; END IF;
    IF NEW.status_hu_caixa NOT IN ('PREVIEW_HU_GERADO','AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO','ENVIANDO_SAP') THEN
       RETURN NEW;
    END IF;
    SELECT count(*),bool_or(etapa_sap='101' AND status_etapa='CONFIRMADO_SAP')
      INTO v_etapas,v_101
      FROM desenvolvimento.hu_caixa_etapa_sap WHERE codigo_hu_caixa=NEW.codigo_hu_caixa;
    IF v_etapas=0 THEN RETURN NEW; END IF;
    IF v_etapas<>2 THEN RAISE EXCEPTION 'PA045_FLUXO_PARCIAL_BLOQUEIA_HU codigo=% etapas=%',NEW.codigo_hu_caixa,v_etapas; END IF;
    IF NOT COALESCE(v_101,false) THEN
       RAISE EXCEPTION 'PA045_HU_EXIGE_101_CONFIRMADO codigo_hu_caixa=% status_destino=%',NEW.codigo_hu_caixa,NEW.status_hu_caixa;
    END IF;
    RETURN NEW;
END
$function$;

DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_045_guard_hu_pos_101';
    IF v_def NOT LIKE '%NEW.status_hu_caixa NOT IN%' THEN
        RAISE EXCEPTION '061 ROLLBACK ABORTADO: a condicao antiga nao foi restaurada.';
    END IF;
    RAISE NOTICE 'SAIDA=OK  061 revertido; a guarda volta a cobrir o preview';
END
$$;

COMMIT;

\echo ''
\echo '=== 061 revertido. A pesagem de caixa volta a falhar no SALVAR_PREVIEW. ==='
\echo ''
