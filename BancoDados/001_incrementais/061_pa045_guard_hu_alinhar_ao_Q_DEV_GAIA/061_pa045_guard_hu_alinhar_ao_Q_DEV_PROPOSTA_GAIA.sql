\set ON_ERROR_STOP on
-- ============================================================
-- 061_pa045_guard_hu_alinhar_ao_Q_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 061 - Alinhar fn_pa_045_guard_hu_pos_101 ao QAS110
-- PROPOSTA (ESCRITA) - transacao unica
--
-- Substitui o CORPO da funcao do gatilho pelo do QAS110. Unica adaptacao: as
-- referencias de schema (homologacao -> desenvolvimento), no nome da funcao, no
-- search_path e na tabela consultada. Nenhuma outra linha foi alterada.
--
-- NAO cria, NAO remove e NAO desabilita gatilho: o trg_045_guard_hu_pos_101
-- continua ativo e apontando para a mesma funcao. O que muda e QUANDO ele cobra.
--
-- Leia o PREFLIGHT antes: ele prova que a guarda atual alcanca PREVIEW_HU_GERADO
-- e lista as caixas presas por causa disso.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '061 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    IF to_regprocedure('desenvolvimento.fn_pa_045_guard_hu_pos_101()') IS NULL THEN
        RAISE EXCEPTION '061 ABORTADO: a funcao nao existe; nada a substituir.';
    END IF;
    RAISE NOTICE 'GUARDAS=OK';
END
$$;

-- Corpo identico ao do QAS110, com o schema adaptado.
CREATE OR REPLACE FUNCTION desenvolvimento.fn_pa_045_guard_hu_pos_101()
 RETURNS trigger
 LANGUAGE plpgsql
 SET search_path TO 'pg_catalog', 'desenvolvimento'
AS $function$
DECLARE v_etapas integer; v_101 boolean;
BEGIN
    IF NEW.status_hu_caixa IS NOT DISTINCT FROM OLD.status_hu_caixa THEN RETURN NEW; END IF;
    IF NOT (
        OLD.status_hu_caixa='PRONTA_PARA_ENVIO'
        AND NEW.status_hu_caixa='ENVIANDO_SAP'
    ) THEN
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

-- ------------------------------------------------------------
-- GUARDA DE SAIDA
-- Nao basta "a funcao foi substituida": exige que ela tenha deixado de guardar o
-- preview E continue guardando o envio. Trocar por um corpo vazio passaria num
-- teste de existencia e destruiria a protecao.
-- ------------------------------------------------------------
DO $$
DECLARE
    v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'desenvolvimento' AND p.proname = 'fn_pa_045_guard_hu_pos_101';

    IF v_def LIKE '%NEW.status_hu_caixa NOT IN%' THEN
        RAISE EXCEPTION '061 ABORTADO: a condicao antiga (lista de 4 status) ainda esta la.';
    END IF;

    IF v_def NOT LIKE '%OLD.status_hu_caixa=''PRONTA_PARA_ENVIO''%'
       OR v_def NOT LIKE '%NEW.status_hu_caixa=''ENVIANDO_SAP''%' THEN
        RAISE EXCEPTION '061 ABORTADO: a condicao nova do QAS110 nao foi aplicada.';
    END IF;

    -- A protecao em si tem de continuar existindo.
    IF v_def NOT LIKE '%PA045_HU_EXIGE_101_CONFIRMADO%'
       OR v_def NOT LIKE '%PA045_FLUXO_PARCIAL_BLOQUEIA_HU%' THEN
        RAISE EXCEPTION '061 ABORTADO: as excecoes de protecao sumiram do corpo.';
    END IF;

    -- E precisa apontar para o schema deste banco, nao para o do Q.
    IF v_def LIKE '%homologacao.%' THEN
        RAISE EXCEPTION '061 ABORTADO: sobrou referencia ao schema homologacao.';
    END IF;

    RAISE NOTICE 'SAIDA=OK  guarda agora cobre so PRONTA_PARA_ENVIO -> ENVIANDO_SAP';
END
$$;

-- O gatilho tem de continuar ativo e ligado a esta funcao.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger t
          JOIN pg_class c ON c.oid = t.tgrelid
          JOIN pg_namespace n ON n.oid = c.relnamespace
          JOIN pg_proc pr ON pr.oid = t.tgfoid
         WHERE n.nspname='desenvolvimento' AND c.relname='hu_caixa'
           AND t.tgname='trg_045_guard_hu_pos_101'
           AND pr.proname='fn_pa_045_guard_hu_pos_101'
           AND t.tgenabled='O' AND NOT t.tgisinternal) THEN
        RAISE EXCEPTION '061 ABORTADO: o gatilho nao esta ativo apontando para a funcao.';
    END IF;
    RAISE NOTICE 'GATILHO=OK  ativo e inalterado';
END
$$;

COMMIT;

\echo ''
\echo '=== 061 PROPOSTA aplicado. Rode o VALIDACAO. ==='
\echo ''
