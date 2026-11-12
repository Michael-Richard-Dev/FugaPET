\set ON_ERROR_STOP on
-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
\pset pager off
\pset tuples_only off
\pset format aligned
\pset null '<NULL>'

-- FugaPET - Incremental 044 HML - PREFLIGHT REV2 CONSOLIDADO POS-REV11 / COMPATIBILIDADE LEGADA
-- PREPARADO, NAO EXECUTADO nesta missao.
-- PostgreSQL HML esperado: major 18, minimo 18.4.
-- Fail-closed: aceita somente a baseline HML legada vazia inventariada em 2026-08-07 e os ACLs legados de auditoria exatamente preservados.

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL statement_timeout='10min';
SET LOCAL lock_timeout='5s';

DO $gaia$
DECLARE
  v_ver integer:=current_setting('server_version_num')::integer;
  v_missing text;
  v_rows bigint;
  v_target_cols integer;
  v_fn_hu integer;
  v_public13 integer;
  v_new_idx integer;
  v_new_trg integer;
  v_dedicated oid;
  v_generic oid;
  v_generic_trg integer;
  v_role_ok boolean;
  v_seq text;
  v_acl_diff integer;
  v_hist_trg integer;
  v_generic_md5 text;
BEGIN
  IF current_database()<>'fuga_jales_local_homologacao_q_v1_2' THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO BANCO_DIVERGENTE atual=%',current_database();
  END IF;
  IF current_user<>'fugapet_q_owner' THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO EXECUTOR_DEVE_SER_POSTGRES atual=%',current_user;
  END IF;
  IF v_ver<180004 OR v_ver>=190000 THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO POSTGRESQL_INCOMPATIVEL versao_num=% esperado=18.4+',v_ver;
  END IF;
  IF to_regnamespace('homologacao') IS NULL THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO SCHEMA_HOMOLOGACAO_AUSENTE';
  END IF;
  SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='fugapet_q_app' AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls) INTO v_role_ok;
  IF NOT v_role_ok THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO ROLE_RUNTIME_AUSENTE_OU_INSEGURA'; END IF;
  IF NOT has_schema_privilege('fugapet_q_app','homologacao','USAGE') OR has_schema_privilege('fugapet_q_app','homologacao','CREATE') THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO PRIVILEGIO_SCHEMA_RUNTIME_DIVERGENTE';
  END IF;

  SELECT string_agg(x.nome,',' ORDER BY x.nome) INTO v_missing FROM (VALUES
    ('hu_caixa'),('hu_caixa_pesagem'),('hu_caixa_integracao_sap'),('hu_caixa_etiqueta'),
    ('hu_palete'),('hu_palete_item'),('hu_palete_etiqueta'),('hu_palete_integracao_sap'),
    ('log_alteracao_cadastral'),('vw_hu_caixas_sem_palete')
  ) x(nome) WHERE to_regclass(format('homologacao.%I',x.nome)) IS NULL;
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO OBJETOS_BASE_AUSENTES=%',v_missing; END IF;

  SELECT (SELECT count(*) FROM homologacao.hu_caixa)+(SELECT count(*) FROM homologacao.hu_caixa_pesagem)+(SELECT count(*) FROM homologacao.hu_caixa_integracao_sap)+(SELECT count(*) FROM homologacao.hu_caixa_etiqueta)+(SELECT count(*) FROM homologacao.hu_palete)+(SELECT count(*) FROM homologacao.hu_palete_item)+(SELECT count(*) FROM homologacao.hu_palete_etiqueta)+(SELECT count(*) FROM homologacao.hu_palete_integracao_sap) INTO v_rows;
  IF v_rows<>0 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO DADOS_HU_EXISTENTES_TOTAL=% MIGRACAO_MANUAL_NECESSARIA',v_rows; END IF;

  SELECT count(*) INTO v_target_cols FROM (VALUES
        ('hu_caixa','numero_ordem_producao'),
        ('hu_caixa','item_ordem_producao'),
        ('hu_caixa','codigo_caixa_local'),
        ('hu_caixa','correlation_id'),
        ('hu_caixa','lote'),
        ('hu_caixa','centro'),
        ('hu_caixa','deposito'),
        ('hu_caixa','material_embalagem'),
        ('hu_caixa','origem_material_embalagem'),
        ('hu_caixa','quantidade'),
        ('hu_caixa','unidade_quantidade'),
        ('hu_caixa','origem_pesagem'),
        ('hu_caixa','terminal'),
        ('hu_caixa','endpoint_sanitizado'),
        ('hu_caixa','handling_unit_external_id'),
        ('hu_caixa','warehouse'),
        ('hu_caixa','odata_etag'),
        ('hu_caixa','created_by_user_sap'),
        ('hu_caixa','creation_datetime_sap'),
        ('hu_caixa','http_status'),
        ('hu_caixa','request_json_sanitizado'),
        ('hu_caixa','response_json_sanitizado'),
        ('hu_caixa','sap_messages_sanitizadas'),
        ('hu_caixa','erro_sanitizado'),
        ('hu_caixa','claim_em'),
        ('hu_caixa','claim_token'),
        ('hu_caixa','tentativa_iniciada_em'),
        ('hu_caixa','enviado_sap_em'),
        ('hu_caixa','confirmado_sap_em'),
        ('hu_caixa','reconciliado_em'),
        ('hu_caixa','tentativas'),
        ('hu_caixa','autorizado_envio_em'),
        ('hu_caixa','autorizado_envio_por'),
        ('hu_caixa','terminal_autorizacao'),
        ('hu_caixa','cancelado_em'),
        ('hu_caixa','cancelado_por'),
        ('hu_caixa','motivo_cancelamento'),
        ('hu_caixa','reprocessamento_liberado_em'),
        ('hu_caixa','reprocessamento_liberado_por'),
        ('hu_caixa','motivo_reprocessamento'),
        ('hu_caixa','criado_em'),
        ('hu_caixa','atualizado_em'),
        ('hu_caixa_integracao_sap','correlation_id'),
        ('hu_caixa_integracao_sap','tipo_operacao'),
        ('hu_caixa_integracao_sap','endpoint_sanitizado'),
        ('hu_caixa_integracao_sap','numero_tentativa'),
        ('hu_caixa_integracao_sap','claim_token'),
        ('hu_caixa_integracao_sap','request_json_sanitizado'),
        ('hu_caixa_integracao_sap','response_json_sanitizado'),
        ('hu_caixa_integracao_sap','http_status'),
        ('hu_caixa_integracao_sap','resultado'),
        ('hu_caixa_integracao_sap','handling_unit_external_id'),
        ('hu_caixa_integracao_sap','warehouse'),
        ('hu_caixa_integracao_sap','odata_etag'),
        ('hu_caixa_integracao_sap','mensagem_erro_sanitizada'),
        ('hu_caixa_integracao_sap','iniciado_em'),
        ('hu_caixa_integracao_sap','finalizado_em'),
        ('hu_caixa_integracao_sap','terminal'),
        ('hu_caixa_integracao_sap','criado_em'),
        ('hu_caixa_pesagem','origem_pesagem'),
        ('hu_caixa_integracao_sap','sap_messages_sanitizadas')
  ) e(tabela,coluna) WHERE EXISTS(SELECT 1 FROM information_schema.columns c WHERE c.table_schema='homologacao' AND c.table_name=e.tabela AND c.column_name=e.coluna);
  IF v_target_cols<>0 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO ESTADO_PARCIAL_COLUNAS=%/61',v_target_cols; END IF;

  SELECT count(*) INTO v_fn_hu FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='homologacao' AND p.proname LIKE 'fn_hu\_%' ESCAPE '\';
  IF v_fn_hu<>0 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO FUNCOES_HU_JA_EXISTEM=%',v_fn_hu; END IF;

  SELECT count(*) INTO v_public13 FROM (VALUES
        ('fn_hu_caixa_finalizar_local'),
        ('fn_hu_caixa_salvar_preview'),
        ('fn_hu_caixa_aguardar_autorizacao'),
        ('fn_hu_caixa_autorizar_envio'),
        ('fn_hu_caixa_claim_envio'),
        ('fn_hu_caixa_registrar_sucesso'),
        ('fn_hu_caixa_registrar_erro'),
        ('fn_hu_caixa_registrar_timeout'),
        ('fn_hu_caixa_confirmar_reconciliacao'),
        ('fn_hu_caixa_registrar_reconciliacao_nao_encontrada'),
        ('fn_hu_caixa_bloquear_configuracao'),
        ('fn_hu_caixa_cancelar'),
        ('fn_hu_caixa_liberar_reprocessamento')
  ) e(nome) WHERE EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='homologacao' AND p.proname=e.nome);
  IF v_public13<>0 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO FUNCOES_PUBLICAS_044_PARCIAIS=%/13',v_public13; END IF;

  SELECT count(*) INTO v_new_idx FROM (VALUES
        ('ix_hu_caixa_op'),
        ('ix_hu_caixa_status_atualizado_044'),
        ('uq_hu_caixa_claim_token'),
        ('uq_hu_caixa_codigo_correlation'),
        ('uq_hu_caixa_codigo_local'),
        ('uq_hu_caixa_correlation_id'),
        ('uq_hu_caixa_hu_sap_warehouse'),
        ('uq_hu_caixa_op_numero'),
        ('uq_hu_caixa_terminal_ativo'),
        ('ix_hu_caixa_integracao_correlation'),
        ('ix_hu_caixa_integracao_hu_sap'),
        ('ix_hu_caixa_integracao_tentativa_claim'),
        ('uq_hu_caixa_integracao_claim_iniciado')
  ) e(nome) WHERE to_regclass(format('homologacao.%I',e.nome)) IS NOT NULL;
  IF v_new_idx<>0 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO INDICES_044_PARCIAIS=%/13',v_new_idx; END IF;

  SELECT count(*) INTO v_new_trg FROM (VALUES
        ('hu_caixa','trg_hu_caixa_preparar_insert'),
        ('hu_caixa','trg_hu_caixa_validar_update'),
        ('hu_caixa_integracao_sap','trg_hu_caixa_integracao_imutavel'),
        ('hu_caixa_integracao_sap','trg_hu_caixa_integracao_preparar_insert'),
        ('hu_caixa_pesagem','trg_hu_caixa_pesagem_normalizar')
  ) e(tabela,nome) WHERE EXISTS(SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='homologacao' AND c.relname=e.tabela AND t.tgname=e.nome AND NOT t.tgisinternal);
  IF v_new_trg<>0 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO TRIGGERS_044_PARCIAIS=%/5',v_new_trg; END IF;

  v_dedicated:=to_regprocedure('homologacao.fn_hu_registrar_log_alteracao_cadastral()');
  IF v_dedicated IS NOT NULL THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO AUDITORIA_DEDICADA_JA_EXISTE'; END IF;
  v_generic:=to_regprocedure('homologacao.fn_registrar_log_alteracao_cadastral()');
  IF v_generic IS NULL OR (SELECT prosecdef FROM pg_proc WHERE oid=v_generic) OR (SELECT pg_get_userbyid(proowner)<>'fugapet_q_owner' FROM pg_proc WHERE oid=v_generic) OR (SELECT proconfig IS NOT NULL FROM pg_proc WHERE oid=v_generic) THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO FUNCAO_AUDITORIA_GENERICA_DIVERGENTE';
  END IF;
  SELECT md5(regexp_replace(pg_get_functiondef(v_generic),'homologacao\.','<SCHEMA>.','g')) INTO v_generic_md5;
  IF v_generic_md5<>'4c0b7abd0a48e87dd60d10b414385d25' THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO FUNCAO_HISTORICA_DEFINICAO_DIVERGENTE md5=%',v_generic_md5;
  END IF;
  SELECT count(*) INTO v_generic_trg FROM (VALUES
    ('hu_caixa','trg_hu_caixa_log_alteracao_cadastral'),
    ('hu_caixa_pesagem','trg_hu_caixa_pesagem_log_alteracao_cadastral'),
    ('hu_caixa_integracao_sap','trg_hu_caixa_integracao_sap_log_alteracao_cadastral')
  ) x(tabela,trg) JOIN pg_class c ON c.relname=x.tabela JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname='homologacao' JOIN pg_trigger t ON t.tgrelid=c.oid AND t.tgname=x.trg AND NOT t.tgisinternal WHERE t.tgfoid=v_generic AND t.tgenabled<>'D';
  IF v_generic_trg<>3 THEN RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO TRIGGERS_AUDITORIA_LEGADOS=%/3',v_generic_trg; END IF;

  -- ACLs HU/palete historicos serao ajustados ao contrato DEV final; a excecao HML vale SOMENTE para log/sequence de auditoria.
  IF EXISTS (
    SELECT 1 FROM (VALUES
      ('homologacao.hu_caixa'),('homologacao.hu_caixa_pesagem'),('homologacao.hu_caixa_integracao_sap'),('homologacao.hu_caixa_etiqueta'),
      ('homologacao.hu_palete'),('homologacao.hu_palete_etiqueta'),('homologacao.hu_palete_integracao_sap'),('homologacao.hu_palete_item')
    ) t(obj)
    WHERE NOT has_table_privilege('fugapet_q_app',t.obj,'SELECT,INSERT,UPDATE')
       OR has_table_privilege('fugapet_q_app',t.obj,'DELETE')
  ) THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO ACL_HU_PALETE_LEGADA_DIVERGIU_DA_BASELINE_AUDITADA';
  END IF;
  IF EXISTS (
    SELECT 1 FROM (VALUES
      (pg_get_serial_sequence('homologacao.hu_caixa','codigo_hu_caixa')),
      (pg_get_serial_sequence('homologacao.hu_caixa_pesagem','codigo_hu_caixa_pesagem')),
      (pg_get_serial_sequence('homologacao.hu_caixa_integracao_sap','codigo_hu_caixa_integracao_sap')),
      (pg_get_serial_sequence('homologacao.hu_caixa_etiqueta','codigo_hu_caixa_etiqueta')),
      (pg_get_serial_sequence('homologacao.hu_palete','codigo_hu_palete')),
      (pg_get_serial_sequence('homologacao.hu_palete_etiqueta','codigo_hu_palete_etiqueta')),
      (pg_get_serial_sequence('homologacao.hu_palete_integracao_sap','codigo_hu_palete_integracao_sap')),
      (pg_get_serial_sequence('homologacao.hu_palete_item','codigo_hu_palete_item'))
    ) s(seq)
    WHERE s.seq IS NULL
       OR NOT has_sequence_privilege('fugapet_q_app',s.seq,'USAGE')
       OR NOT has_sequence_privilege('fugapet_q_app',s.seq,'SELECT')
       OR has_sequence_privilege('fugapet_q_app',s.seq,'UPDATE')
  ) THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO ACL_SEQUENCES_HU_PALETE_LEGADA_DIVERGIU_DA_BASELINE_AUDITADA';
  END IF;
  SELECT (WITH actual(grantee,privilege_type,is_grantable) AS (
      SELECT CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
             a.privilege_type,
             a.is_grantable
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
      CROSS JOIN LATERAL aclexplode(c.relacl) a
      WHERE n.nspname='homologacao' AND c.relname='log_alteracao_cadastral'
    ), expected(grantee,privilege_type,is_grantable) AS (
      VALUES
        ('fugapet_q_app','INSERT',false),('fugapet_q_app','SELECT',false),('fugapet_q_app','UPDATE',false),
        ('fugapet_q_owner','DELETE',false),('fugapet_q_owner','INSERT',false),('fugapet_q_owner','MAINTAIN',false),('fugapet_q_owner','REFERENCES',false),
        ('fugapet_q_owner','SELECT',false),('fugapet_q_owner','TRIGGER',false),('fugapet_q_owner','TRUNCATE',false),('fugapet_q_owner','UPDATE',false)
    )
    SELECT count(*) FROM (
      (SELECT * FROM actual EXCEPT SELECT * FROM expected)
      UNION ALL
      (SELECT * FROM expected EXCEPT SELECT * FROM actual)
    ) d) INTO v_acl_diff;
  IF v_acl_diff<>0 THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO ACL_LEGADO_TABLE_LOG_DIVERGENTE diff=%',v_acl_diff;
  END IF;
  SELECT (WITH s AS (
      SELECT pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral')::regclass AS oid
    ), actual(grantee,privilege_type,is_grantable) AS (
      SELECT CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
             a.privilege_type,
             a.is_grantable
      FROM s
      JOIN pg_class c ON c.oid=s.oid
      CROSS JOIN LATERAL aclexplode(c.relacl) a
    ), expected(grantee,privilege_type,is_grantable) AS (
      VALUES
        ('fugapet_q_app','SELECT',false),('fugapet_q_app','USAGE',false),
        ('fugapet_q_owner','SELECT',false),('fugapet_q_owner','UPDATE',false),('fugapet_q_owner','USAGE',false)
    )
    SELECT count(*) FROM (
      (SELECT * FROM actual EXCEPT SELECT * FROM expected)
      UNION ALL
      (SELECT * FROM expected EXCEPT SELECT * FROM actual)
    ) d) INTO v_acl_diff;
  IF v_acl_diff<>0 THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO ACL_LEGADO_SEQUENCE_LOG_DIVERGENTE diff=%',v_acl_diff;
  END IF;
  SELECT count(*) INTO v_hist_trg
  FROM pg_trigger t
  JOIN pg_class c ON c.oid=t.tgrelid
  JOIN pg_namespace n ON n.oid=c.relnamespace
  JOIN pg_proc p ON p.oid=t.tgfoid
  WHERE n.nspname='homologacao' AND NOT t.tgisinternal AND p.oid=v_generic
    AND NOT ((c.relname='hu_caixa' AND t.tgname='trg_hu_caixa_log_alteracao_cadastral')
      OR (c.relname='hu_caixa_pesagem' AND t.tgname='trg_hu_caixa_pesagem_log_alteracao_cadastral')
      OR (c.relname='hu_caixa_integracao_sap' AND t.tgname='trg_hu_caixa_integracao_sap_log_alteracao_cadastral'));
  IF v_hist_trg<>37 THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_REPROVADO TRIGGERS_HISTORICOS_FORA_HU_DIVERGENTES=%/37',v_hist_trg;
  END IF;


  RAISE NOTICE 'ACL_LEGADO_PRESERVACAO_REV2=OBRIGATORIA';
  RAISE NOTICE 'FUNCAO_HISTORICA_DEFINICAO_ALTERADA=NAO';
  RAISE NOTICE 'TRIGGERS_HISTORICOS_FORA_HU=%/37',v_hist_trg;

  IF EXISTS(SELECT 1 FROM pg_depend d JOIN pg_rewrite r ON r.oid=d.objid JOIN pg_class dep ON dep.oid=r.ev_class JOIN pg_namespace nd ON nd.oid=dep.relnamespace WHERE d.refobjid='homologacao.vw_hu_caixas_sem_palete'::regclass AND NOT (nd.nspname='homologacao' AND dep.relname='vw_hu_caixas_sem_palete')) THEN
    RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REPROVADO VIEW_COM_DEPENDENTES_NAO_PREVISTOS';
  END IF;

  RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_APROVADO_PARA_PROMOCAO_CONSOLIDADA_COMPATIBILIDADE_LEGADA';
  RAISE NOTICE 'ESTADO_HML=BASELINE_HU_LEGADA_VAZIA_SEM_CONTRATO_044';
  RAISE NOTICE 'TARGET_COLUNAS_044=0/61 FUNCOES_HU=0 FUNCOES_PUBLICAS=0/13 INDICES_NOVOS=0/13 TRIGGERS_NOVOS=0/5';
  RAISE NOTICE 'BACKUP_HML_NECESSARIO_ANTES_PROPOSTA=SIM';
END
$gaia$;

SELECT current_database() banco,current_user usuario,current_setting('server_version') postgresql,current_setting('transaction_read_only') read_only;
SELECT count(*) hu_caixa FROM homologacao.hu_caixa;
SELECT count(*) hu_caixa_pesagem FROM homologacao.hu_caixa_pesagem;
SELECT count(*) hu_caixa_integracao_sap FROM homologacao.hu_caixa_integracao_sap;
SELECT count(*) hu_caixa_etiqueta FROM homologacao.hu_caixa_etiqueta;

SELECT string_agg(
  format('TABLE|%s|%s|%s|%s|%s',n.nspname,c.relname,CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,CASE WHEN a.is_grantable THEN 'YES' ELSE 'NO' END),
  ';' ORDER BY CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,a.is_grantable
) AS acl_legado_antes_table_log
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a
WHERE n.nspname='homologacao' AND c.relname='log_alteracao_cadastral' \gset
\echo 'ACL_LEGADO_ANTES_TABLE_LOG=' :acl_legado_antes_table_log

SELECT string_agg(
  format('SEQUENCE|%s|%s|%s|%s|%s',n.nspname,c.relname,CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,CASE WHEN a.is_grantable THEN 'YES' ELSE 'NO' END),
  ';' ORDER BY CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,a.is_grantable
) AS acl_legado_antes_sequence_log
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a
WHERE c.oid=pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral')::regclass \gset
\echo 'ACL_LEGADO_ANTES_SEQUENCE_LOG=' :acl_legado_antes_sequence_log

\echo '=== INVENTARIO_TRIGGERS_HISTORICOS_FORA_HU_ANTES ==='
SELECT c.relname AS tabela,t.tgname AS trigger_name,p.oid::regprocedure::text AS funcao_alvo,t.tgenabled AS enabled,
       pg_get_triggerdef(t.oid,true) AS definicao,pg_get_userbyid(c.relowner) AS table_owner,pg_get_userbyid(p.proowner) AS function_owner
FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_proc p ON p.oid=t.tgfoid
WHERE n.nspname='homologacao' AND NOT t.tgisinternal AND p.proname='fn_registrar_log_alteracao_cadastral'
  AND NOT ((c.relname='hu_caixa' AND t.tgname='trg_hu_caixa_log_alteracao_cadastral')
    OR (c.relname='hu_caixa_pesagem' AND t.tgname='trg_hu_caixa_pesagem_log_alteracao_cadastral')
    OR (c.relname='hu_caixa_integracao_sap' AND t.tgname='trg_hu_caixa_integracao_sap_log_alteracao_cadastral'))
ORDER BY c.relname,t.tgname;

ROLLBACK;
\echo 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_Q_REV2_APROVADO_PARA_PROMOCAO_CONSOLIDADA_COMPATIBILIDADE_LEGADA'
\echo 'ALTERACAO_PERSISTIDA=NAO'
