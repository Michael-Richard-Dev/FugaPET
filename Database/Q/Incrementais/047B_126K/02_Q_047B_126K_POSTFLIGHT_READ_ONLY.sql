-- FugaPET QAS110 / gate 126K / 047-B / no SAP, no operational DML
-- Prepared only; DO NOT EXECUTE before independent Hermes review and Hera authorization.
\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only on

BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL search_path TO pg_catalog,homologacao;
SET LOCAL statement_timeout='45s';
DO $post$
DECLARE v_exact oid; v_main oid;
BEGIN

  IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2'
     OR current_setting('port') <> '5432'
     OR (SELECT system_identifier::text FROM pg_catalog.pg_control_system()) <> '7649742235876580978'
     OR pg_catalog.to_regnamespace('homologacao') IS NULL
  THEN
    RAISE EXCEPTION '126K_WRONG_Q_DATABASE_OR_CLUSTER';
  END IF;

  IF current_setting('transaction_read_only')<>'on' THEN RAISE EXCEPTION '126K_POSTFLIGHT_NOT_READ_ONLY'; END IF;
  v_exact:=pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)');
  v_main:=pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)');
  IF v_exact IS NULL OR v_main IS NULL THEN RAISE EXCEPTION '126K_POSTFLIGHT_FUNCTION_MISSING'; END IF;
  IF NOT pg_catalog.has_schema_privilege('fugapet_q_app','homologacao','USAGE')
     OR NOT pg_catalog.has_table_privilege('fugapet_q_app','homologacao.hu_caixa','SELECT')
     OR NOT pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT')
  THEN RAISE EXCEPTION '126K_POSTFLIGHT_PERMISSION_MISSING'; END IF;
  IF pg_catalog.pg_get_function_identity_arguments(v_exact)<>'p_hu text'
     OR pg_catalog.pg_get_function_identity_arguments(v_main)<>'p_material text, p_hu_inicial text, p_hu_final text'
  THEN RAISE EXCEPTION '126K_POSTFLIGHT_SIGNATURE_MISMATCH'; END IF;
  IF EXISTS(SELECT 1 FROM pg_catalog.pg_proc p WHERE p.oid IN(v_exact,v_main)
       AND (p.prosecdef OR p.provolatile<>'s'
         OR pg_catalog.pg_get_function_result(p.oid)<>'TABLE(codigo_hu_caixa bigint, handling_unit_external_id text, numero_ordem_producao text, material text, lote text, numero_caixa integer, hu_caixa text, codigo_caixa_local text, peso_bruto numeric, peso_liquido numeric, peso_tara numeric, unidade_peso text, centro text, deposito text, status_hu_caixa text)'
         OR NOT pg_catalog.has_function_privilege('fugapet_q_app',p.oid,'EXECUTE')
         OR EXISTS(SELECT 1 FROM pg_catalog.aclexplode(COALESCE(p.proacl,pg_catalog.acldefault('f',p.proowner))) acl
                    WHERE acl.grantee=0 AND acl.privilege_type='EXECUTE')))
  THEN RAISE EXCEPTION '126K_POSTFLIGHT_FUNCTION_CONTRACT_INVALID'; END IF;
  IF pg_catalog.obj_description(v_main,'pg_proc') NOT LIKE 'FugaPET_Q_126K_CREATED_RANGE;%'
  THEN RAISE EXCEPTION '126K_POSTFLIGHT_MAIN_PROVENANCE_MISSING'; END IF;
END $post$;
SELECT 'FUNCTION|NAME='||p.proname||'|ARGS='||pg_catalog.pg_get_function_identity_arguments(p.oid)||
 '|EXECUTE_Q_ROLE='||pg_catalog.has_function_privilege('fugapet_q_app',p.oid,'EXECUTE')||
 '|MARKER='||coalesce(pg_catalog.obj_description(p.oid,'pg_proc'),'PREEXISTING')
 FROM pg_catalog.pg_proc p WHERE p.oid IN (
 pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_buscar_hu_exata(text)'),
 pg_catalog.to_regprocedure('homologacao.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)'));
SELECT 'VIEW_SELECT_Q_APP='||pg_catalog.has_table_privilege('fugapet_q_app','homologacao.vw_hu_caixas_sem_palete','SELECT');
ROLLBACK;
\echo 126K_POSTFLIGHT_RESULT=PASS_READ_ONLY
