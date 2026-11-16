\set ON_ERROR_STOP on
-- ============================================================
-- 064_pa047_funcoes_paletizacao_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 064 - Portar as funcoes fn_pa_047_* (Paletizacao por HU) do QAS110
-- PROPOSTA (ESCRITA) - transacao unica
--
-- Cria as duas funcoes com o corpo do QAS110 e concede EXECUTE a fugapet_dev_app,
-- espelhando a ACL das demais fn_pa_* deste banco.
--
-- Somente leitura em runtime: as duas sao STABLE, nao escrevem nada e nao tocam
-- em tabela alguma alem de ler vw_hu_caixas_sem_palete e hu_caixa.
--
-- Leia o PREFLIGHT antes: ele prova a ausencia das funcoes e a presenca das
-- dependencias.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '064 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.vw_hu_caixas_sem_palete') IS NULL THEN
        RAISE EXCEPTION '064 ABORTADO: vw_hu_caixas_sem_palete nao existe; a funcao nasceria quebrada.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') THEN
        RAISE EXCEPTION '064 ABORTADO: o papel fugapet_dev_app nao existe neste servidor.';
    END IF;

    RAISE NOTICE 'GUARDAS=OK';
END
$$;

-- ------------------------------------------------------------
-- 1) Busca por HU EXATA.
-- Corpo identico ao do QAS110, com o schema adaptado.
-- Regra que NAO pode se perder na copia: so devolve linha quando o candidato e
-- UNICO (total_candidatos=1). HU que casa com mais de uma caixa devolve VAZIO em
-- vez da primeira -- paletizar a caixa errada e pior que nao achar nenhuma.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(p_hu text)
 RETURNS TABLE(codigo_hu_caixa bigint, handling_unit_external_id text, numero_ordem_producao text,
               material text, lote text, numero_caixa integer, hu_caixa text, codigo_caixa_local text,
               peso_bruto numeric, peso_liquido numeric, peso_tara numeric, unidade_peso text,
               centro text, deposito text, status_hu_caixa text)
 LANGUAGE plpgsql
 STABLE
 SET search_path TO 'pg_catalog', 'desenvolvimento'
AS $function$
DECLARE
    v_hu text := btrim(COALESCE(p_hu,''));
BEGIN
    IF v_hu='' THEN
        RETURN;
    END IF;

    RETURN QUERY
    WITH candidatos AS (
        SELECT
            hc.codigo_hu_caixa,
            btrim(hc.handling_unit_external_id)::text AS handling_unit_external_id,
            hc.numero_ordem_producao::text AS numero_ordem_producao,
            hc.material::text AS material,
            hc.lote::text AS lote,
            hc.numero_caixa,
            hc.hu_caixa::text AS hu_caixa,
            hc.codigo_caixa_local::text AS codigo_caixa_local,
            hc.peso_bruto::numeric AS peso_bruto,
            hc.peso_liquido::numeric AS peso_liquido,
            hc.peso_tara::numeric AS peso_tara,
            hc.unidade_peso::text AS unidade_peso,
            hc.centro::text AS centro,
            hc.deposito::text AS deposito,
            hc.status_hu_caixa::text AS status_hu_caixa,
            count(*) OVER () AS total_candidatos
        FROM desenvolvimento.vw_hu_caixas_sem_palete v
        JOIN desenvolvimento.hu_caixa hc
          ON hc.codigo_hu_caixa=v.codigo_hu_caixa
        WHERE hc.situacao_hu_caixa IS TRUE
          AND hc.status_hu_caixa='CONFIRMADA_SAP'
          AND hc.handling_unit_external_id IS NOT NULL
          AND char_length(btrim(hc.handling_unit_external_id))>0
          AND btrim(hc.handling_unit_external_id)=v_hu
    )
    SELECT
        c.codigo_hu_caixa,
        c.handling_unit_external_id,
        c.numero_ordem_producao,
        c.material,
        c.lote,
        c.numero_caixa,
        c.hu_caixa,
        c.codigo_caixa_local,
        c.peso_bruto,
        c.peso_liquido,
        c.peso_tara,
        c.unidade_peso,
        c.centro,
        c.deposito,
        c.status_hu_caixa
    FROM candidatos c
    WHERE c.total_candidatos=1
    ORDER BY c.codigo_hu_caixa;
END
$function$;

-- ------------------------------------------------------------
-- 2) Listagem por material + INTERVALO de HU.
-- Corpo identico ao do QAS110, com o schema adaptado. As validacoes de entrada
-- sao por EXCECAO NOMEADA (PA047_*), nao por retorno vazio: entrada invalida tem
-- de ser distinguivel de "nao ha caixa no intervalo".
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(
    p_material text, p_hu_inicial text, p_hu_final text)
 RETURNS TABLE(codigo_hu_caixa bigint, handling_unit_external_id text, numero_ordem_producao text,
               material text, lote text, numero_caixa integer, hu_caixa text, codigo_caixa_local text,
               peso_bruto numeric, peso_liquido numeric, peso_tara numeric, unidade_peso text,
               centro text, deposito text, status_hu_caixa text)
 LANGUAGE plpgsql
 STABLE
 SET search_path TO 'pg_catalog', 'desenvolvimento'
AS $function$
DECLARE
    v_material text := btrim(COALESCE(p_material,''));
    v_hu_inicial_text text := btrim(COALESCE(p_hu_inicial,''));
    v_hu_final_text text := btrim(COALESCE(p_hu_final,''));
    v_hu_inicial numeric;
    v_hu_final numeric;
BEGIN
    IF v_material='' THEN
        RAISE EXCEPTION 'PA047_MATERIAL_OBRIGATORIO';
    END IF;
    IF v_hu_inicial_text='' OR v_hu_inicial_text !~ '^[0-9]+$' THEN
        RAISE EXCEPTION 'PA047_HU_INICIAL_INVALIDA';
    END IF;
    IF v_hu_final_text='' OR v_hu_final_text !~ '^[0-9]+$' THEN
        RAISE EXCEPTION 'PA047_HU_FINAL_INVALIDA';
    END IF;

    v_hu_inicial := v_hu_inicial_text::numeric;
    v_hu_final := v_hu_final_text::numeric;

    IF v_hu_inicial>v_hu_final THEN
        RAISE EXCEPTION 'PA047_INTERVALO_HU_INVALIDO_INICIO_MAIOR_QUE_FIM';
    END IF;

    RETURN QUERY
    SELECT
        hc.codigo_hu_caixa,
        btrim(hc.handling_unit_external_id)::text AS handling_unit_external_id,
        hc.numero_ordem_producao::text AS numero_ordem_producao,
        hc.material::text AS material,
        hc.lote::text AS lote,
        hc.numero_caixa,
        hc.hu_caixa::text AS hu_caixa,
        hc.codigo_caixa_local::text AS codigo_caixa_local,
        hc.peso_bruto::numeric AS peso_bruto,
        hc.peso_liquido::numeric AS peso_liquido,
        hc.peso_tara::numeric AS peso_tara,
        hc.unidade_peso::text AS unidade_peso,
        hc.centro::text AS centro,
        hc.deposito::text AS deposito,
        hc.status_hu_caixa::text AS status_hu_caixa
    FROM desenvolvimento.vw_hu_caixas_sem_palete v
    JOIN desenvolvimento.hu_caixa hc
      ON hc.codigo_hu_caixa=v.codigo_hu_caixa
    WHERE hc.situacao_hu_caixa IS TRUE
      AND hc.status_hu_caixa='CONFIRMADA_SAP'
      AND hc.handling_unit_external_id IS NOT NULL
      AND btrim(hc.handling_unit_external_id) ~ '^[0-9]+$'
      AND btrim(hc.material::text)=v_material
      AND btrim(hc.handling_unit_external_id)::numeric BETWEEN v_hu_inicial AND v_hu_final
    ORDER BY btrim(hc.handling_unit_external_id)::numeric ASC, hc.codigo_hu_caixa ASC;
END
$function$;

GRANT EXECUTE ON FUNCTION desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(text,text,text) TO fugapet_dev_app;

-- ------------------------------------------------------------
-- GUARDA DE SAIDA
-- Nao basta "as funcoes existem": exige que as regras que importam tenham vindo
-- junto. Um corpo vazio, ou sem o criterio de unicidade, passaria num teste de
-- existencia e deixaria a tela paletizar a caixa errada.
-- ------------------------------------------------------------
DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_047_caixa_buscar_hu_exata';

    IF v_def IS NULL THEN
        RAISE EXCEPTION '064 ABORTADO: fn_pa_047_caixa_buscar_hu_exata nao foi criada.';
    END IF;
    IF v_def NOT LIKE '%total_candidatos=1%' THEN
        RAISE EXCEPTION '064 ABORTADO: o criterio de UNICIDADE da HU nao veio no corpo.';
    END IF;
    IF v_def NOT LIKE '%CONFIRMADA_SAP%' THEN
        RAISE EXCEPTION '064 ABORTADO: o filtro de CONFIRMADA_SAP sumiu.';
    END IF;
    IF v_def LIKE '%homologacao.%' THEN
        RAISE EXCEPTION '064 ABORTADO: sobrou referencia ao schema homologacao.';
    END IF;

    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_047_caixa_listar_intervalo_hu';

    IF v_def IS NULL THEN
        RAISE EXCEPTION '064 ABORTADO: fn_pa_047_caixa_listar_intervalo_hu nao foi criada.';
    END IF;
    IF v_def NOT LIKE '%PA047_MATERIAL_OBRIGATORIO%'
       OR v_def NOT LIKE '%PA047_HU_INICIAL_INVALIDA%'
       OR v_def NOT LIKE '%PA047_HU_FINAL_INVALIDA%'
       OR v_def NOT LIKE '%PA047_INTERVALO_HU_INVALIDO_INICIO_MAIOR_QUE_FIM%' THEN
        RAISE EXCEPTION '064 ABORTADO: as validacoes nomeadas PA047_* nao vieram no corpo.';
    END IF;
    IF v_def NOT LIKE '%CONFIRMADA_SAP%' THEN
        RAISE EXCEPTION '064 ABORTADO: o filtro de CONFIRMADA_SAP sumiu da listagem.';
    END IF;
    IF v_def LIKE '%homologacao.%' THEN
        RAISE EXCEPTION '064 ABORTADO: sobrou referencia ao schema homologacao.';
    END IF;

    RAISE NOTICE 'SAIDA=OK  as duas funcoes existem com as regras preservadas';
END
$$;

-- A concessao tem de ter pegado: sem EXECUTE a tela veria "permission denied".
DO $$
BEGIN
    IF NOT has_function_privilege('fugapet_dev_app',
            'desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)', 'EXECUTE')
       OR NOT has_function_privilege('fugapet_dev_app',
            'desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(text)', 'EXECUTE') THEN
        RAISE EXCEPTION '064 ABORTADO: fugapet_dev_app nao recebeu EXECUTE nas duas funcoes.';
    END IF;
    RAISE NOTICE 'GRANT=OK  fugapet_dev_app pode executar as duas';
END
$$;

COMMIT;

\echo ''
\echo '=== 064 PROPOSTA aplicado. Rode o VALIDACAO. ==='
\echo ''
