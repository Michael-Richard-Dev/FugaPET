-- FugaPET QAS110 / gate 126K / 047-B / no SAP, no operational DML
-- Prepared only; DO NOT EXECUTE before independent Hermes review and Hera authorization.
\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only on

BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL search_path TO pg_catalog,homologacao;
SET LOCAL statement_timeout='45s';
SET LOCAL lock_timeout='3s';
DO $pre$
DECLARE v_invoker boolean; v_missing_deps text; v_missing_funcs text; v_exact oid;
BEGIN

  IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2'
     OR current_setting('port') <> '5432'
     OR (SELECT system_identifier::text FROM pg_catalog.pg_control_system()) <> '7649742235876580978'
     OR pg_catalog.to_regnamespace('homologacao') IS NULL
  THEN
    RAISE EXCEPTION '126K_WRONG_Q_DATABASE_OR_CLUSTER';
  END IF;

  IF current_setting('transaction_read_only')<>'on' THEN RAISE EXCEPTION '126K_PREFLIGHT_NOT_READ_ONLY'; END IF;

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

END $pre$;
SELECT '126K_PREFLIGHT|DATABASE='||current_database()||'|READ_ONLY='||current_setting('transaction_read_only');
SELECT 'BASELINE_BEFORE|MAIN=ABSENT|COMPLEMENTARY='||CASE WHEN pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)') IS NULL THEN 'ABSENT' ELSE 'EXISTING_COMPATIBLE_PRESERVE' END||
'|VIEW_SELECT='||CASE WHEN pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT') THEN 'EXISTING_EFFECTIVE' ELSE 'MISSING_TO_BE_GRANTED' END;
SELECT 'SECURITY_INVOKER='||CASE WHEN 'security_invoker=true'=ANY(coalesce(reloptions,ARRAY[]::text[])) THEN 'SIM_UNDERLYING_CHECK_PASS' ELSE 'NAO_DEFAULT_OWNER_CONTEXT' END
 FROM pg_catalog.pg_class WHERE oid='homologacao.vw_hu_caixas_sem_palete'::regclass;
ROLLBACK;
\echo 126K_PREFLIGHT_RESULT=PASS_READ_ONLY
