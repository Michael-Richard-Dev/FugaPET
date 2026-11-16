\set ON_ERROR_STOP on
-- ============================================================
-- 064_pa047_funcoes_paletizacao_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 064 - Portar as funcoes fn_pa_047_* (Paletizacao por HU) do QAS110
-- PREFLIGHT (SOMENTE LEITURA)
--
-- POR QUE ESTE INCREMENTO EXISTE
-- Em 16/11/2026, a tela "Paletizacao por HU" do DEV110, ao carregar as HUs,
-- devolveu:
--   Falha ao carregar HUs [42883]: function fn_pa_047_caixa_listar_intervalo_hu(
--   p_material => text, p_hu_inicial => text, p_hu_final => text) does not exist
--
-- 42883 e "undefined_function": nao e permissao, nao e erro de dado. O banco do
-- DEV simplesmente nao tem NENHUMA das funcoes do pacote 047 -- a tela foi
-- promovida ao DEV, o banco nao.
--
-- ORIGEM. As duas funcoes foram extraidas em LEITURA do banco de homologacao do Q
-- (fuga_jales_local_homologacao_q_v1_2), via pg_get_functiondef. Unica adaptacao:
-- o schema (homologacao -> desenvolvimento) no nome, no search_path e nas tabelas
-- consultadas. Nenhuma outra linha foi alterada; nada foi inferido.
--
-- O QUE ENTRA
--   fn_pa_047_caixa_buscar_hu_exata(text)
--       -> busca por HU exata; so devolve linha quando o candidato e UNICO
--          (total_candidatos=1). HU ambigua devolve VAZIO, nao a primeira.
--   fn_pa_047_caixa_listar_intervalo_hu(text,text,text)
--       -> lista por material + intervalo numerico de HU, com validacao de
--          entrada por excecao nomeada (PA047_*).
--
-- As duas sao STABLE e SOMENTE LEITURA: nao escrevem, nao chamam SAP e so leem
-- vw_hu_caixas_sem_palete + hu_caixa. Ambas filtram por status CONFIRMADA_SAP,
-- ou seja, so caixa com o fluxo SAP fechado pode entrar num palete.
--
-- LIMITE DECLARADO: este incremento NAO desbloqueia o ENVIO do palete ao SAP.
-- Esse envio depende da tara fisica da embalagem, que exige identidade QAS110
-- (host vhfufqs4ci) e nao resolve no DEV110 (host vhfufds4ci). O 064 trata
-- apenas da SELECAO de HUs na tela e da etiqueta, que e local.
-- ============================================================

\echo ''
\echo '=== 064 PREFLIGHT ==='

SELECT current_database() AS banco, current_user AS papel, current_schema() AS schema_corrente;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '064 PREFLIGHT: alvo errado. Conectado em %.', current_database();
    END IF;
    RAISE NOTICE 'ALVO=OK';
END
$$;

-- 1. As funcoes estao mesmo ausentes? (esperado: zero -- e a causa do 42883)
\echo ''
\echo '-- funcoes fn_pa_047_* existentes neste banco --'
SELECT count(*) AS quantidade,
       COALESCE(string_agg(p.proname, ', ' ORDER BY p.proname), '<nenhuma>') AS funcoes
  FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
 WHERE n.nspname='desenvolvimento' AND p.proname LIKE 'fn_pa_047%';

-- 2. As dependencias existem? Portar funcao sem elas so troca um erro por outro.
\echo ''
\echo '-- dependencias: view e colunas usadas pelas duas funcoes --'
SELECT to_regclass('desenvolvimento.vw_hu_caixas_sem_palete') IS NOT NULL AS tem_view,
       (SELECT count(*) FROM information_schema.columns
         WHERE table_schema='desenvolvimento' AND table_name='hu_caixa'
           AND column_name IN ('situacao_hu_caixa','hu_caixa','codigo_caixa_local','peso_bruto',
                               'peso_liquido','peso_tara','unidade_peso','centro','deposito',
                               'handling_unit_external_id','numero_caixa','material','lote',
                               'numero_ordem_producao','status_hu_caixa')) AS colunas_presentes_de_15;

-- 3. Ha caixa elegivel para paletizar? Util para a VALIDACAO exercitar de verdade.
\echo ''
\echo '-- caixas CONFIRMADA_SAP com HU numerica e sem palete --'
SELECT hc.codigo_hu_caixa, btrim(hc.handling_unit_external_id) AS hu,
       hc.material, hc.numero_ordem_producao, hc.peso_liquido
  FROM desenvolvimento.vw_hu_caixas_sem_palete v
  JOIN desenvolvimento.hu_caixa hc ON hc.codigo_hu_caixa=v.codigo_hu_caixa
 WHERE hc.situacao_hu_caixa IS TRUE
   AND hc.status_hu_caixa='CONFIRMADA_SAP'
   AND hc.handling_unit_external_id IS NOT NULL
   AND btrim(hc.handling_unit_external_id) ~ '^[0-9]+$'
 ORDER BY btrim(hc.handling_unit_external_id)::numeric;

-- 4. Padrao de concessao a espelhar.
\echo ''
\echo '-- ACL de funcoes comparaveis (o 064 vai conceder igual) --'
SELECT p.proname, COALESCE(array_to_string(p.proacl,' | '),'<sem ACL explicita>') AS acl
  FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
 WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_045_claim_etapa';

\echo ''
\echo '=== 064 PREFLIGHT concluido: nada foi escrito ==='
\echo ''
