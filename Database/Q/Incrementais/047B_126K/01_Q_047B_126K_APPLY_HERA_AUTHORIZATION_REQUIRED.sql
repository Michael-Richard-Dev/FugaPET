-- FugaPET QAS110 / gate 126K / 047-B / no SAP, no operational DML
-- Prepared only; DO NOT EXECUTE before independent Hermes review and Hera authorization.
\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only on

-- APPLY IS FUTURE GATE ONLY; no current approval to execute.
-- All ACL and function creation are in ONE transaction; any ERROR => transaction aborts before COMMIT.
BEGIN;
SET LOCAL search_path TO pg_catalog,homologacao;
SET LOCAL statement_timeout='120s';
SET LOCAL lock_timeout='3s';
DO $guard$
DECLARE v_invoker boolean; v_missing_deps text; v_missing_funcs text; v_exact oid;
BEGIN

  IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2'
     OR current_setting('port') <> '5432'
     OR (SELECT system_identifier::text FROM pg_catalog.pg_control_system()) <> '7649742235876580978'
     OR pg_catalog.to_regnamespace('homologacao') IS NULL
  THEN
    RAISE EXCEPTION '126K_WRONG_Q_DATABASE_OR_CLUSTER';
  END IF;


  IF pg_catalog.to_regclass('homologacao.hu_caixa') IS NULL
     OR pg_catalog.to_regclass('homologacao.vw_hu_caixas_sem_palete') IS NULL
     OR NOT EXISTS (SELECT 1 FROM pg_catalog.pg_class c WHERE c.oid='homologacao.vw_hu_caixas_sem_palete'::regclass AND c.relkind='v')
  THEN RAISE EXCEPTION '126K_MISSING_BASE_TABLE_OR_VIEW'; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='fugapet_q_app')
  THEN RAISE EXCEPTION '126K_Q_ROLE_MISSING'; END IF;
  IF NOT pg_catalog.has_schema_privilege('fugapet_q_app','homologacao','USAGE')
     OR NOT pg_catalog.has_table_privilege('fugapet_q_app','homologacao.hu_caixa','SELECT')
  THEN RAISE EXCEPTION '126K_Q_ROLE_BASE_PRIVILEGE_MISSING'; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a WHERE a.attrelid='homologacao.vw_hu_caixas_sem_palete'::regclass
      AND a.attname='codigo_hu_caixa' AND a.attnum>0 AND NOT a.attisdropped)
  THEN RAISE EXCEPTION '126K_VIEW_JOIN_KEY_MISSING'; END IF;
  IF EXISTS (SELECT 1 FROM pg_catalog.unnest(ARRAY[
     'codigo_hu_caixa','handling_unit_external_id','numero_ordem_producao','material','lote','numero_caixa',
     'hu_caixa','codigo_caixa_local','peso_bruto','peso_liquido','peso_tara','unidade_peso',
     'centro','deposito','status_hu_caixa','situacao_hu_caixa'
  ]) required(column_name)
      WHERE NOT EXISTS(SELECT 1 FROM pg_catalog.pg_attribute a WHERE a.attrelid='homologacao.hu_caixa'::regclass
                      AND a.attname=required.column_name AND a.attnum>0 AND NOT a.attisdropped))
  THEN RAISE EXCEPTION '126K_MISSING_REQUIRED_HU_CAIXA_COLUMN'; END IF;

  SELECT 'security_invoker=true' = ANY(coalesce(c.reloptions, ARRAY[]::text[]))
    INTO v_invoker
  FROM pg_catalog.pg_class c
  WHERE c.oid='homologacao.vw_hu_caixas_sem_palete'::regclass;
  IF v_invoker THEN
    WITH RECURSIVE dep_rel(relid) AS (
       SELECT 'homologacao.vw_hu_caixas_sem_palete'::regclass::oid
       UNION
       SELECT d.refobjid
         FROM dep_rel dr
         JOIN pg_catalog.pg_rewrite rw ON rw.ev_class=dr.relid
         JOIN pg_catalog.pg_depend d ON d.classid='pg_catalog.pg_rewrite'::regclass
                                     AND d.objid=rw.oid
                                     AND d.refclassid='pg_catalog.pg_class'::regclass
         WHERE d.refobjid <> dr.relid
    )
    SELECT pg_catalog.string_agg(pg_catalog.format('%I.%I',n.nspname,c.relname),', ')
      INTO v_missing_deps
      FROM dep_rel dr
      JOIN pg_catalog.pg_class c ON c.oid=dr.relid
      JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
     WHERE dr.relid <> 'homologacao.vw_hu_caixas_sem_palete'::regclass::oid
       AND c.relkind IN ('r','p','v','m','f')
       AND (NOT pg_catalog.has_schema_privilege('fugapet_q_app',n.oid,'USAGE')
         OR NOT pg_catalog.has_table_privilege('fugapet_q_app',c.oid,'SELECT'));
    IF v_missing_deps IS NOT NULL THEN
       RAISE EXCEPTION '126K_SECURITY_INVOKER_UNDERLYING_PRIVILEGES_MISSING: %',v_missing_deps;
    END IF;
    -- Include functions on view rewrite trees, not execution of functions.
    WITH RECURSIVE dep_rel(relid) AS (
       SELECT 'homologacao.vw_hu_caixas_sem_palete'::regclass::oid
       UNION
       SELECT d.refobjid
         FROM dep_rel dr
         JOIN pg_catalog.pg_rewrite rw ON rw.ev_class=dr.relid
         JOIN pg_catalog.pg_depend d ON d.classid='pg_catalog.pg_rewrite'::regclass
                                     AND d.objid=rw.oid
                                     AND d.refclassid='pg_catalog.pg_class'::regclass
         WHERE d.refobjid <> dr.relid
    )
    SELECT pg_catalog.string_agg(p.oid::regprocedure::text,', ')
      INTO v_missing_funcs
      FROM dep_rel dr
      JOIN pg_catalog.pg_rewrite rw ON rw.ev_class=dr.relid
      JOIN pg_catalog.pg_depend d ON d.classid='pg_catalog.pg_rewrite'::regclass
                                  AND d.objid=rw.oid
                                  AND d.refclassid='pg_catalog.pg_proc'::regclass
      JOIN pg_catalog.pg_proc p ON p.oid=d.refobjid
      JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
     WHERE NOT pg_catalog.has_schema_privilege('fugapet_q_app',n.oid,'USAGE')
        OR NOT pg_catalog.has_function_privilege('fugapet_q_app',p.oid,'EXECUTE');
    IF v_missing_funcs IS NOT NULL THEN
       RAISE EXCEPTION '126K_SECURITY_INVOKER_FUNCTION_PRIVILEGES_MISSING: %',v_missing_funcs;
    END IF;
  END IF;


  IF EXISTS (SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
       WHERE n.nspname='homologacao' AND p.proname='fn_pa_047_caixa_listar_intervalo_hu')
  THEN RAISE EXCEPTION '126K_MAIN_EXISTS_NO_AUTOMATIC_REPLACE'; END IF;

  -- Existing complementary function: preserve iff compatible with approved 047-B.
  v_exact := pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)');
  IF EXISTS (SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
        WHERE n.nspname='homologacao' AND p.proname='fn_pa_047_caixa_buscar_hu_exata'
          AND (v_exact IS NULL OR p.oid <> v_exact))
  THEN RAISE EXCEPTION '126K_COMPLEMENTARY_UNEXPECTED_OVERLOAD'; END IF;
  IF v_exact IS NOT NULL THEN
    IF NOT EXISTS (
       SELECT 1 FROM pg_catalog.pg_proc p
       JOIN pg_catalog.pg_language l ON l.oid=p.prolang
       WHERE p.oid=v_exact
         AND l.lanname='plpgsql' AND p.provolatile='s' AND NOT p.prosecdef
         AND pg_catalog.pg_get_function_identity_arguments(p.oid)='p_hu text'
         AND pg_catalog.pg_get_function_result(p.oid)='TABLE(codigo_hu_caixa bigint, handling_unit_external_id text, numero_ordem_producao text, material text, lote text, numero_caixa integer, hu_caixa text, codigo_caixa_local text, peso_bruto numeric, peso_liquido numeric, peso_tara numeric, unidade_peso text, centro text, deposito text, status_hu_caixa text)'
         AND pg_catalog.has_function_privilege('fugapet_q_app',p.oid,'EXECUTE')
         AND NOT EXISTS (SELECT 1 FROM pg_catalog.aclexplode(COALESCE(p.proacl,pg_catalog.acldefault('f',p.proowner))) acl
             WHERE acl.grantee=0 AND acl.privilege_type='EXECUTE')
         AND pg_catalog.md5(pg_catalog.regexp_replace(pg_catalog.lower(p.prosrc),'[[:space:]]+','','g'))='21ea9e769d74c81d02264e06606ef10d'
         AND EXISTS (SELECT 1 FROM pg_catalog.unnest(coalesce(p.proconfig,ARRAY[]::text[])) cfg
           WHERE pg_catalog.regexp_replace(pg_catalog.lower(cfg),'[[:space:]]+','','g')='search_path=pg_catalog,homologacao')
    ) THEN RAISE EXCEPTION '126K_COMPLEMENTARY_EXISTING_FUNCTION_INCOMPATIBLE'; END IF;
  END IF;

END $guard$;
-- In-transaction provenance snapshot before any changes.
SELECT '126K_BEFORE|VIEW_SELECT='||CASE WHEN pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT') THEN 'EXISTING' ELSE 'MISSING' END||
  '|COMPANION='||CASE WHEN pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)') IS NULL THEN 'ABSENT' ELSE 'EXISTING_PRESERVED' END||
  '|MAIN=ABSENT';
SELECT CASE WHEN pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT') THEN 'false' ELSE 'true' END AS grant_view \gset
SELECT CASE WHEN pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)') IS NULL THEN 'true' ELSE 'false' END AS create_exact \gset
-- Apply only the missing Q VIEW SELECT, do not widen base object privileges.
\if :grant_view
GRANT SELECT ON TABLE homologacao.vw_hu_caixas_sem_palete TO fugapet_q_app;
\endif
DO $acl$
BEGIN
 IF NOT pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT')
 THEN RAISE EXCEPTION '126K_VIEW_GRANT_IN_TX_NOT_EFFECTIVE'; END IF;
END $acl$;
-- Exact HU function: create only when absent; never overwrite compatible pre-existing function.
\if :create_exact
CREATE FUNCTION homologacao.fn_pa_047_caixa_buscar_hu_exata(
    p_hu text
)
RETURNS TABLE (
    codigo_hu_caixa bigint,
    handling_unit_external_id text,
    numero_ordem_producao text,
    material text,
    lote text,
    numero_caixa integer,
    hu_caixa text,
    codigo_caixa_local text,
    peso_bruto numeric,
    peso_liquido numeric,
    peso_tara numeric,
    unidade_peso text,
    centro text,
    deposito text,
    status_hu_caixa text
)
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path TO pg_catalog, homologacao
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
        FROM homologacao.vw_hu_caixas_sem_palete v
        JOIN homologacao.hu_caixa hc
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
COMMENT ON FUNCTION homologacao.fn_pa_047_caixa_buscar_hu_exata(text)
 IS 'FugaPET_Q_126K_CREATED_EXACT: original_047B_GaiaV3; SECURITY INVOKER';
REVOKE ALL ON FUNCTION homologacao.fn_pa_047_caixa_buscar_hu_exata(text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION homologacao.fn_pa_047_caixa_buscar_hu_exata(text) TO fugapet_q_app;
\endif
-- Range HU function: original 047-B definition, newly created only.
CREATE FUNCTION homologacao.fn_pa_047_caixa_listar_intervalo_hu(
    p_material text,
    p_hu_inicial text,
    p_hu_final text
)
RETURNS TABLE (
    codigo_hu_caixa bigint,
    handling_unit_external_id text,
    numero_ordem_producao text,
    material text,
    lote text,
    numero_caixa integer,
    hu_caixa text,
    codigo_caixa_local text,
    peso_bruto numeric,
    peso_liquido numeric,
    peso_tara numeric,
    unidade_peso text,
    centro text,
    deposito text,
    status_hu_caixa text
)
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path TO pg_catalog, homologacao
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
    FROM homologacao.vw_hu_caixas_sem_palete v
    JOIN homologacao.hu_caixa hc
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
REVOKE ALL ON FUNCTION homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text) TO fugapet_q_app;
SELECT pg_catalog.format('COMMENT ON FUNCTION homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text) IS %L;',
 'FugaPET_Q_126K_CREATED_RANGE;VIEW_GRANT_ADDED='||CASE WHEN :'grant_view'='true' THEN 'TRUE' ELSE 'FALSE' END||
 ';COMPANION_CREATED='||CASE WHEN :'create_exact'='true' THEN 'TRUE' ELSE 'FALSE' END||';original_047B_GaiaV3') \gexec
DO $check$
DECLARE v_exact oid; v_main oid; v_tag text;
BEGIN

  IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2'
     OR current_setting('port') <> '5432'
     OR (SELECT system_identifier::text FROM pg_catalog.pg_control_system()) <> '7649742235876580978'
     OR pg_catalog.to_regnamespace('homologacao') IS NULL
  THEN
    RAISE EXCEPTION '126K_WRONG_Q_DATABASE_OR_CLUSTER';
  END IF;

  v_exact:=pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)');
  v_main:=pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)');
  IF v_exact IS NULL OR v_main IS NULL THEN RAISE EXCEPTION '126K_POST_TX_MISSING_FUNCTION'; END IF;
  IF NOT pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT')
  THEN RAISE EXCEPTION '126K_VIEW_SELECT_NOT_EFFECTIVE_AFTER_GRANT'; END IF;
  IF pg_catalog.pg_get_function_identity_arguments(v_exact)<>'p_hu text'
      OR pg_catalog.pg_get_function_identity_arguments(v_main)<>'p_material text, p_hu_inicial text, p_hu_final text'
  THEN RAISE EXCEPTION '126K_NAMED_ARGUMENTS_MISMATCH'; END IF;
  IF EXISTS (SELECT 1 FROM pg_catalog.pg_proc p WHERE p.oid IN(v_exact,v_main)
      AND (p.prosecdef OR p.provolatile<>'s'
        OR pg_catalog.pg_get_function_result(p.oid)<>'TABLE(codigo_hu_caixa bigint, handling_unit_external_id text, numero_ordem_producao text, material text, lote text, numero_caixa integer, hu_caixa text, codigo_caixa_local text, peso_bruto numeric, peso_liquido numeric, peso_tara numeric, unidade_peso text, centro text, deposito text, status_hu_caixa text)'
        OR NOT pg_catalog.has_function_privilege('fugapet_q_app',p.oid,'EXECUTE')
        OR EXISTS (SELECT 1 FROM pg_catalog.aclexplode(COALESCE(p.proacl,pg_catalog.acldefault('f',p.proowner))) acl
            WHERE acl.grantee=0 AND acl.privilege_type='EXECUTE')))
  THEN RAISE EXCEPTION '126K_FUNCTION_SECURITY_RESULT_OR_EXECUTE_INVALID'; END IF;
  v_tag:=pg_catalog.obj_description(v_main,'pg_proc');
  IF v_tag NOT LIKE 'FugaPET_Q_126K_CREATED_RANGE;VIEW_GRANT_ADDED=%;COMPANION_CREATED=%;original_047B_GaiaV3'
  THEN RAISE EXCEPTION '126K_PROVENANCE_MARKER_MISSING'; END IF;
END $check$;
SELECT '126K_IN_TX_POSTFLIGHT=PASS|VIEW_SELECT=SIM|MAIN=SIM|COMPLEMENTARY=SIM|FUNCTION_EXECUTE=SIM';
COMMIT;
\echo 126K_APPLY_COMPLETED_ONLY_IF_PRIOR_HERA_AUTHORIZATION
