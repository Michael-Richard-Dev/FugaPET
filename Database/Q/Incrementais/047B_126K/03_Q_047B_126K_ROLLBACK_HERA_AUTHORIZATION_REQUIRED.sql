-- FugaPET QAS110 / gate 126K / 047-B / no SAP, no operational DML
-- Prepared only; DO NOT EXECUTE before independent Hermes review and Hera authorization.
\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only on

-- Future HERA authorized ROLLBACK ONLY: tagged functions and tagged SELECT grant.
-- RESTRICT blocks if other objects depend on the newly created functions.
BEGIN;
SET LOCAL search_path TO pg_catalog,homologacao;
SET LOCAL statement_timeout='120s';
SET LOCAL lock_timeout='3s';
DO $guard$
DECLARE v_main oid; v_exact oid; v_tag text; v_companion_created bool;
BEGIN

  IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2'
     OR current_setting('port') <> '5432'
     OR (SELECT system_identifier::text FROM pg_catalog.pg_control_system()) <> '7649742235876580978'
     OR pg_catalog.to_regnamespace('homologacao') IS NULL
  THEN
    RAISE EXCEPTION '126K_WRONG_Q_DATABASE_OR_CLUSTER';
  END IF;

  v_main:=pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)');
  v_exact:=pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)');
  IF v_main IS NULL THEN RAISE EXCEPTION '126K_ROLLBACK_MAIN_MISSING_STOP'; END IF;
  v_tag:=pg_catalog.obj_description(v_main,'pg_proc');
  IF v_tag NOT IN (
      'FugaPET_Q_126K_CREATED_RANGE;VIEW_GRANT_ADDED=TRUE;COMPANION_CREATED=TRUE;original_047B_GaiaV3',
      'FugaPET_Q_126K_CREATED_RANGE;VIEW_GRANT_ADDED=TRUE;COMPANION_CREATED=FALSE;original_047B_GaiaV3',
      'FugaPET_Q_126K_CREATED_RANGE;VIEW_GRANT_ADDED=FALSE;COMPANION_CREATED=TRUE;original_047B_GaiaV3',
      'FugaPET_Q_126K_CREATED_RANGE;VIEW_GRANT_ADDED=FALSE;COMPANION_CREATED=FALSE;original_047B_GaiaV3')
  THEN RAISE EXCEPTION '126K_ROLLBACK_MAIN_UNOWNED_OR_TAG_DIVERGED'; END IF;
  v_companion_created := v_tag LIKE '%;COMPANION_CREATED=TRUE;%';
  IF v_companion_created AND (v_exact IS NULL OR pg_catalog.obj_description(v_exact,'pg_proc') IS DISTINCT FROM
       'FugaPET_Q_126K_CREATED_EXACT: original_047B_GaiaV3; SECURITY INVOKER')
  THEN RAISE EXCEPTION '126K_ROLLBACK_COMPANION_UNOWNED'; END IF;
  IF pg_catalog.pg_get_function_identity_arguments(v_main)<>'p_material text, p_hu_inicial text, p_hu_final text'
    OR pg_catalog.pg_get_function_result(v_main)<>'TABLE(codigo_hu_caixa bigint, handling_unit_external_id text, numero_ordem_producao text, material text, lote text, numero_caixa integer, hu_caixa text, codigo_caixa_local text, peso_bruto numeric, peso_liquido numeric, peso_tara numeric, unidade_peso text, centro text, deposito text, status_hu_caixa text)'
  THEN RAISE EXCEPTION '126K_ROLLBACK_MAIN_SIGNATURE_CHANGED'; END IF;
END $guard$;
-- Do not drop or touch existing compatible companion.
SELECT CASE WHEN pg_catalog.obj_description('homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)'::regprocedure,'pg_proc')
                    LIKE '%;COMPANION_CREATED=TRUE;%' THEN 'true' ELSE 'false' END AS drop_exact \gset
SELECT CASE WHEN pg_catalog.obj_description('homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)'::regprocedure,'pg_proc')
                    LIKE '%;VIEW_GRANT_ADDED=TRUE;%' THEN 'true' ELSE 'false' END AS revoke_view \gset
DROP FUNCTION homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text) RESTRICT;
\if :drop_exact
DROP FUNCTION homologacao.fn_pa_047_caixa_buscar_hu_exata(text) RESTRICT;
\endif
\if :revoke_view
REVOKE SELECT ON TABLE homologacao.vw_hu_caixas_sem_palete FROM fugapet_q_app RESTRICT;
\endif
COMMIT;
\echo 126K_ROLLBACK_COMPLETED_TAG_GUARDED
