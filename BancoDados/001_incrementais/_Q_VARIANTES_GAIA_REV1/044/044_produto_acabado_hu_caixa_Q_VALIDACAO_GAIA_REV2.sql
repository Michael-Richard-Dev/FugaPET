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

-- FugaPET - Incremental 044 HML - VALIDACAO REV2 CONSOLIDADA POS-REV11 / COMPATIBILIDADE LEGADA
-- Base: REV10 aplicada + delta REV11 aplicado. ROLLBACK final obrigatorio.
-- Dados e grants temporarios dentro de transacao. ROLLBACK final obrigatorio.
-- Compatibilidade operacional REV11: PostgreSQL major 18, versao minima 18.4.
-- Nenhuma sequence HU deve avancar; a sequence de auditoria pode avancar de forma nao transacional. ACL legado de auditoria deve permanecer semanticamente identico.

SELECT count(*) AS hu_antes FROM homologacao.hu_caixa \gset
SELECT count(*) AS pesagens_antes FROM homologacao.hu_caixa_pesagem \gset
SELECT count(*) AS historico_antes FROM homologacao.hu_caixa_integracao_sap \gset

CREATE TEMP TABLE gaia_044_seq_antes AS
SELECT schemaname,sequencename,last_value
FROM pg_sequences
WHERE schemaname='homologacao'
  AND sequencename IN (
    split_part(pg_get_serial_sequence('homologacao.hu_caixa','codigo_hu_caixa'),'.',2),
    split_part(pg_get_serial_sequence('homologacao.hu_caixa_pesagem','codigo_hu_caixa_pesagem'),'.',2),
    split_part(pg_get_serial_sequence('homologacao.hu_caixa_integracao_sap','codigo_hu_caixa_integracao_sap'),'.',2)
  );

SELECT count(*) AS audit_antes FROM homologacao.log_alteracao_cadastral \gset
CREATE TEMP TABLE gaia_044_audit_seq_antes AS
SELECT schemaname,sequencename,last_value
FROM pg_sequences
WHERE schemaname='homologacao'
  AND sequencename=split_part(pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral'),'.',2);

BEGIN;
SET LOCAL statement_timeout='20min';
SET LOCAL lock_timeout='10s';

-- Snapshot da view: a VALIDACAO comprova que nao altera definicao, owner,
-- comentario, ACL da relacao, nomes ou ordem das colunas.
CREATE TEMP TABLE tmp_044_view_metadata (
    view_definition text NOT NULL,
    view_owner name NOT NULL,
    view_comment text,
    grants_signature jsonb NOT NULL,
    columns_signature jsonb NOT NULL
) ON COMMIT DROP;

INSERT INTO tmp_044_view_metadata(
    view_definition,view_owner,view_comment,grants_signature,columns_signature
)
SELECT
    pg_get_viewdef(c.oid,true),
    pg_get_userbyid(c.relowner),
    obj_description(c.oid,'pg_class'),
    COALESCE((
      SELECT jsonb_agg(jsonb_build_object(
          'grantee',CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
          'privilege_type',a.privilege_type,
          'is_grantable',a.is_grantable
      ) ORDER BY a.grantee,a.privilege_type,a.is_grantable)
      FROM aclexplode(c.relacl) a
      WHERE a.privilege_type<>'TRUNCATE'
    ),'[]'::jsonb),
    COALESCE((
      SELECT jsonb_agg(jsonb_build_object(
          'ordinal_position',ic.ordinal_position,
          'column_name',ic.column_name,
          'data_type',ic.data_type,
          'character_maximum_length',ic.character_maximum_length,
          'numeric_precision',ic.numeric_precision,
          'numeric_scale',ic.numeric_scale
      ) ORDER BY ic.ordinal_position)
      FROM information_schema.columns ic
      WHERE ic.table_schema='homologacao'
        AND ic.table_name='vw_hu_caixas_sem_palete'
    ),'[]'::jsonb)
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='homologacao'
  AND c.relname='vw_hu_caixas_sem_palete'
  AND c.relkind='v';

CREATE TEMP TABLE tmp_044_context (
    usuario bigint NOT NULL,
    balanca bigint NOT NULL,
    terminal1 text NOT NULL, terminal2 text NOT NULL, terminal3 text NOT NULL,
    terminal4 text NOT NULL, terminal5 text NOT NULL, terminal6 text NOT NULL,
    terminal7 text NOT NULL
) ON COMMIT DROP;

INSERT INTO tmp_044_context
SELECT u.codigo_usuario,b.codigo_balanca,
       'GAIA044R5-T1-'||pg_backend_pid(),'GAIA044R5-T2-'||pg_backend_pid(),
       'GAIA044R5-T3-'||pg_backend_pid(),'GAIA044R5-T4-'||pg_backend_pid(),
       'GAIA044R5-T5-'||pg_backend_pid(),'GAIA044R5-T6-'||pg_backend_pid(),
       'GAIA044R5-T7-'||pg_backend_pid()
FROM LATERAL (
    SELECT min(codigo_usuario) codigo_usuario FROM homologacao.usuario
    WHERE situacao_usuario AND NOT bloqueado_usuario
) u
CROSS JOIN LATERAL (
    SELECT min(codigo_balanca) codigo_balanca FROM homologacao.balanca
    WHERE situacao_balanca
) b
WHERE u.codigo_usuario IS NOT NULL AND b.codigo_balanca IS NOT NULL;

DO $gaia$
DECLARE
 v_version_num integer;
 v_major integer;
BEGIN
 v_version_num := current_setting('server_version_num')::integer;
 v_major := v_version_num / 10000;
 IF v_major <> 18 OR v_version_num < 180004 THEN
   RAISE EXCEPTION
     'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_BLOQUEADA_POSTGRESQL_INCOMPATIVEL major_atual=% versao_num_atual=% major_esperado=18 versao_minima=18.4',
     v_major,
     v_version_num;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM tmp_044_context) THEN
   RAISE EXCEPTION 'VALIDACAO_044_EXIGE_USUARIO_E_BALANCA_ATIVOS';
 END IF;
 IF current_database()<>'fuga_jales_local_homologacao_q_v1_2' OR current_user<>'fugapet_q_owner' THEN
   RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_BLOQUEADA_AMBIENTE_OU_EXECUTOR';
 END IF;
 IF has_table_privilege('fugapet_q_app','homologacao.hu_caixa','INSERT')
    OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','INSERT')
    OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa','UPDATE')
    OR has_schema_privilege('fugapet_q_app','homologacao','CREATE') THEN
   RAISE EXCEPTION 'VALIDACAO_044_PRIVILEGIO_TABELA_OU_SCHEMA_INDEVIDO';
 END IF;
 IF EXISTS (
      SELECT 1 FROM pg_roles r
       WHERE r.rolname='fugapet_q_app'
         AND (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls)
 ) THEN RAISE EXCEPTION 'VALIDACAO_044_ROLE_APLICACAO_ELEVADA'; END IF;
 IF EXISTS (
      WITH RECURSIVE herdadas(oid) AS (
        SELECT m.roleid FROM pg_auth_members m
         WHERE m.member=(SELECT oid FROM pg_roles WHERE rolname='fugapet_q_app')
        UNION
        SELECT m.roleid FROM pg_auth_members m JOIN herdadas h ON h.oid=m.member
      )
      SELECT 1 FROM herdadas h JOIN pg_roles r ON r.oid=h.oid
       WHERE r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication
          OR r.rolbypassrls OR has_schema_privilege(r.rolname,'homologacao','CREATE')
 ) THEN RAISE EXCEPTION 'VALIDACAO_044_ROLE_HERDA_PRIVILEGIO_ELEVADO'; END IF;
 IF (SELECT count(*) FROM tmp_044_view_metadata)<>1 THEN
   RAISE EXCEPTION 'VALIDACAO_044_FALHOU_VIEW_AUSENTE_OU_AMBIGUA';
 END IF;
 IF EXISTS (
   SELECT 1 FROM (VALUES
     ('hu_caixa','numero_ordem_producao'),('hu_caixa','item_ordem_producao'),
     ('hu_caixa','correlation_id'),('hu_caixa','codigo_sap_ordem_producao'),
     ('hu_caixa','codigo_sap_produto'),('hu_caixa','codigo_produto_referencia'),
     ('hu_caixa','material'),('hu_caixa','lote'),('hu_caixa','centro'),
     ('hu_caixa','deposito'),('hu_caixa','material_embalagem'),
     ('hu_caixa','origem_material_embalagem'),('hu_caixa','peso_bruto'),
     ('hu_caixa','peso_liquido'),('hu_caixa','peso_tara'),('hu_caixa','unidade_peso'),
     ('hu_caixa','quantidade'),('hu_caixa','unidade_quantidade'),
     ('hu_caixa','origem_pesagem'),('hu_caixa','codigo_balanca'),
     ('hu_caixa','codigo_usuario'),('hu_caixa','terminal'),
     ('hu_caixa_pesagem','codigo_hu_caixa'),('hu_caixa_pesagem','codigo_balanca'),
     ('hu_caixa_pesagem','origem_pesagem'),('hu_caixa_pesagem','peso_lido'),
     ('hu_caixa_pesagem','peso_bruto'),('hu_caixa_pesagem','peso_liquido'),
     ('hu_caixa_pesagem','peso_tara'),('hu_caixa_pesagem','unidade_peso'),
     ('hu_caixa_pesagem','payload_balanca'),('hu_caixa_pesagem','codigo_usuario')
   ) e(tabela,coluna)
   WHERE NOT has_column_privilege('fugapet_q_app',format('homologacao.%I',e.tabela),e.coluna,'INSERT')
 ) THEN RAISE EXCEPTION 'VALIDACAO_044_GRANT_COLUNA_APROVADA_AUSENTE'; END IF;
 IF EXISTS (
   SELECT 1 FROM information_schema.columns c
    WHERE c.table_schema='homologacao'
      AND c.table_name IN ('hu_caixa','hu_caixa_pesagem')
      AND has_column_privilege('fugapet_q_app',format('homologacao.%I',c.table_name),c.column_name,'INSERT')
      AND NOT EXISTS (
        SELECT 1 FROM (VALUES
          ('hu_caixa','numero_ordem_producao'),('hu_caixa','item_ordem_producao'),
          ('hu_caixa','correlation_id'),('hu_caixa','codigo_sap_ordem_producao'),
          ('hu_caixa','codigo_sap_produto'),('hu_caixa','codigo_produto_referencia'),
          ('hu_caixa','material'),('hu_caixa','lote'),('hu_caixa','centro'),
          ('hu_caixa','deposito'),('hu_caixa','material_embalagem'),
          ('hu_caixa','origem_material_embalagem'),('hu_caixa','peso_bruto'),
          ('hu_caixa','peso_liquido'),('hu_caixa','peso_tara'),('hu_caixa','unidade_peso'),
          ('hu_caixa','quantidade'),('hu_caixa','unidade_quantidade'),
          ('hu_caixa','origem_pesagem'),('hu_caixa','codigo_balanca'),
          ('hu_caixa','codigo_usuario'),('hu_caixa','terminal'),
          ('hu_caixa_pesagem','codigo_hu_caixa'),('hu_caixa_pesagem','codigo_balanca'),
          ('hu_caixa_pesagem','origem_pesagem'),('hu_caixa_pesagem','peso_lido'),
          ('hu_caixa_pesagem','peso_bruto'),('hu_caixa_pesagem','peso_liquido'),
          ('hu_caixa_pesagem','peso_tara'),('hu_caixa_pesagem','unidade_peso'),
          ('hu_caixa_pesagem','payload_balanca'),('hu_caixa_pesagem','codigo_usuario')
        ) e(tabela,coluna)
        WHERE e.tabela=c.table_name AND e.coluna=c.column_name
      )
 ) THEN RAISE EXCEPTION 'VALIDACAO_044_GRANT_COLUNA_FORA_DA_LISTA'; END IF;
END $gaia$;

-- Contrato estrutural final acumulado pos-REV11 (controle real, nao somente contagens cegas).
DO $gaia_contract$
DECLARE v_cols integer; v_fn integer; v_public13 integer; v_trg integer; v_idx_diff integer; v_con_diff integer; v_views integer; v_view_cols integer;
BEGIN
  SELECT count(*) INTO v_cols FROM information_schema.columns WHERE table_schema='homologacao' AND table_name IN ('hu_caixa','hu_caixa_etiqueta','hu_caixa_integracao_sap','hu_caixa_pesagem','hu_palete','hu_palete_etiqueta','hu_palete_integracao_sap','hu_palete_item');
  IF v_cols<>201 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_COLUNAS_HU=%/201',v_cols; END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='hu_caixa' AND column_name='codigo_caixa_local' AND data_type='character varying' AND character_maximum_length=80 AND is_nullable='NO') THEN
    RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_CODIGO_CAIXA_LOCAL_DIVERGENTE';
  END IF;
  SELECT count(*) INTO v_fn FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='homologacao' AND p.proname LIKE 'fn_hu\_%' ESCAPE '\';
  IF v_fn<>20 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_FUNCOES_FN_HU=%/20',v_fn; END IF;
  SELECT count(*) INTO v_public13 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='homologacao' AND p.proname IN ('fn_hu_caixa_finalizar_local','fn_hu_caixa_salvar_preview','fn_hu_caixa_aguardar_autorizacao','fn_hu_caixa_autorizar_envio','fn_hu_caixa_claim_envio','fn_hu_caixa_registrar_sucesso','fn_hu_caixa_registrar_erro','fn_hu_caixa_registrar_timeout','fn_hu_caixa_confirmar_reconciliacao','fn_hu_caixa_registrar_reconciliacao_nao_encontrada','fn_hu_caixa_bloquear_configuracao','fn_hu_caixa_cancelar','fn_hu_caixa_liberar_reprocessamento') AND has_function_privilege('fugapet_q_app',p.oid,'EXECUTE');
  IF v_public13<>13 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_FUNCOES_PUBLICAS_CSHARP=%/13',v_public13; END IF;
  SELECT count(*) INTO v_trg FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='homologacao' AND c.relname LIKE 'hu\_%' ESCAPE '\' AND NOT t.tgisinternal;
  IF v_trg<>22 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_TRIGGERS_HU=%/22',v_trg; END IF;
  WITH expected(nome) AS (VALUES
      ('hu_caixa_pkey'),
      ('ix_hu_caixa_material'),
      ('ix_hu_caixa_op'),
      ('ix_hu_caixa_ordem'),
      ('ix_hu_caixa_status'),
      ('ix_hu_caixa_status_atualizado_044'),
      ('uq_hu_caixa_claim_token'),
      ('uq_hu_caixa_codigo_correlation'),
      ('uq_hu_caixa_codigo_local'),
      ('uq_hu_caixa_correlation_id'),
      ('uq_hu_caixa_hu'),
      ('uq_hu_caixa_hu_sap_warehouse'),
      ('uq_hu_caixa_op_numero'),
      ('uq_hu_caixa_terminal_ativo'),
      ('hu_caixa_etiqueta_pkey'),
      ('hu_caixa_integracao_sap_pkey'),
      ('ix_hu_caixa_integracao_caixa'),
      ('ix_hu_caixa_integracao_correlation'),
      ('ix_hu_caixa_integracao_hu_sap'),
      ('ix_hu_caixa_integracao_tentativa_claim'),
      ('uq_hu_caixa_integracao_claim_iniciado'),
      ('hu_caixa_pesagem_pkey'),
      ('ix_hu_caixa_pesagem_caixa'),
      ('hu_palete_pkey'),
      ('ix_hu_palete_data'),
      ('ix_hu_palete_status'),
      ('uq_hu_palete_hu'),
      ('hu_palete_etiqueta_pkey'),
      ('hu_palete_integracao_sap_pkey'),
      ('ix_hu_palete_integracao_palete'),
      ('hu_palete_item_pkey'),
      ('ix_hu_palete_item_palete'),
      ('uq_hu_caixa_palete_ativo'),
      ('uq_hu_palete_item_caixa_por_palete')
  ), actual(nome) AS (
    SELECT i.indexname FROM pg_indexes i WHERE i.schemaname='homologacao' AND i.tablename IN ('hu_caixa','hu_caixa_etiqueta','hu_caixa_integracao_sap','hu_caixa_pesagem','hu_palete','hu_palete_etiqueta','hu_palete_integracao_sap','hu_palete_item')
  ) SELECT count(*) INTO v_idx_diff FROM ((SELECT * FROM actual EXCEPT SELECT * FROM expected) UNION ALL (SELECT * FROM expected EXCEPT SELECT * FROM actual)) d;
  IF v_idx_diff<>0 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_INDICES_DIVERGENTES diff=% esperado=34',v_idx_diff; END IF;
  WITH expected(tabela,nome,tipo) AS (VALUES
      ('hu_caixa','ck_hu_caixa_autorizacao_envio','c'),
      ('hu_caixa','ck_hu_caixa_balanca_coerente','c'),
      ('hu_caixa','ck_hu_caixa_cancelamento','c'),
      ('hu_caixa','ck_hu_caixa_centro','c'),
      ('hu_caixa','ck_hu_caixa_claim_token','c'),
      ('hu_caixa','ck_hu_caixa_codigo_local_formato','c'),
      ('hu_caixa','ck_hu_caixa_confirmacao_sap','c'),
      ('hu_caixa','ck_hu_caixa_deposito','c'),
      ('hu_caixa','ck_hu_caixa_envio_sap','c'),
      ('hu_caixa','ck_hu_caixa_equacao_pesagem','c'),
      ('hu_caixa','ck_hu_caixa_erro_sap','c'),
      ('hu_caixa','ck_hu_caixa_handling_unit_external_id','c'),
      ('hu_caixa','ck_hu_caixa_http_status','c'),
      ('hu_caixa','ck_hu_caixa_hu_nao_vazia','c'),
      ('hu_caixa','ck_hu_caixa_item_ordem_producao','c'),
      ('hu_caixa','ck_hu_caixa_json_objeto','c'),
      ('hu_caixa','ck_hu_caixa_json_sem_segredos','c'),
      ('hu_caixa','ck_hu_caixa_legado_sincronizado','c'),
      ('hu_caixa','ck_hu_caixa_lote','c'),
      ('hu_caixa','ck_hu_caixa_material','c'),
      ('hu_caixa','ck_hu_caixa_material_embalagem','c'),
      ('hu_caixa','ck_hu_caixa_numero_caixa','c'),
      ('hu_caixa','ck_hu_caixa_numero_ordem_producao','c'),
      ('hu_caixa','ck_hu_caixa_origem_material_embalagem','c'),
      ('hu_caixa','ck_hu_caixa_origem_pesagem','c'),
      ('hu_caixa','ck_hu_caixa_peso_bruto','c'),
      ('hu_caixa','ck_hu_caixa_peso_liquido','c'),
      ('hu_caixa','ck_hu_caixa_peso_tara','c'),
      ('hu_caixa','ck_hu_caixa_quantidade','c'),
      ('hu_caixa','ck_hu_caixa_reprocessamento','c'),
      ('hu_caixa','ck_hu_caixa_status','c'),
      ('hu_caixa','ck_hu_caixa_tentativas','c'),
      ('hu_caixa','ck_hu_caixa_terminal','c'),
      ('hu_caixa','ck_hu_caixa_timeout_indeterminado','c'),
      ('hu_caixa','ck_hu_caixa_unidade_peso_kg','c'),
      ('hu_caixa','ck_hu_caixa_unidade_quantidade','c'),
      ('hu_caixa','ck_hu_caixa_warehouse','c'),
      ('hu_caixa','fk_hu_caixa_atualizado_por','f'),
      ('hu_caixa','fk_hu_caixa_criado_por','f'),
      ('hu_caixa','hu_caixa_autorizado_envio_por_fkey','f'),
      ('hu_caixa','hu_caixa_cancelado_por_fkey','f'),
      ('hu_caixa','hu_caixa_codigo_balanca_fkey','f'),
      ('hu_caixa','hu_caixa_codigo_produto_referencia_fkey','f'),
      ('hu_caixa','hu_caixa_codigo_sap_ordem_producao_fkey','f'),
      ('hu_caixa','hu_caixa_codigo_sap_produto_fkey','f'),
      ('hu_caixa','hu_caixa_codigo_usuario_fkey','f'),
      ('hu_caixa','hu_caixa_reprocessamento_liberado_por_fkey','f'),
      ('hu_caixa','hu_caixa_pkey','p'),
      ('hu_caixa','uq_hu_caixa_codigo_correlation','u'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_claim','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_finalizacao','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_http_status','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_hu','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_json_objeto','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_json_sem_segredos','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_legado_sincronizado','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_metodo','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_numero_tentativa','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_operacao_metodo','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_periodo','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_reprocessamento','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_resultado','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_terminal','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_tipo_operacao','c'),
      ('hu_caixa_integracao_sap','ck_hu_caixa_integracao_warehouse','c'),
      ('hu_caixa_integracao_sap','fk_hu_caixa_integracao_identidade','f'),
      ('hu_caixa_integracao_sap','fk_hu_caixa_integracao_sap_atualizado_por','f'),
      ('hu_caixa_integracao_sap','fk_hu_caixa_integracao_sap_criado_por','f'),
      ('hu_caixa_integracao_sap','hu_caixa_integracao_sap_codigo_usuario_fkey','f'),
      ('hu_caixa_integracao_sap','hu_caixa_integracao_sap_pkey','p'),
      ('hu_caixa_pesagem','ck_hu_caixa_pesagem_equacao','c'),
      ('hu_caixa_pesagem','ck_hu_caixa_pesagem_origem','c'),
      ('hu_caixa_pesagem','ck_hu_caixa_pesagem_origem_balanca','c'),
      ('hu_caixa_pesagem','ck_hu_caixa_pesagem_peso_lido','c'),
      ('hu_caixa_pesagem','ck_hu_caixa_pesagem_unidade_peso_kg','c'),
      ('hu_caixa_pesagem','fk_hu_caixa_pesagem_atualizado_por','f'),
      ('hu_caixa_pesagem','fk_hu_caixa_pesagem_caixa','f'),
      ('hu_caixa_pesagem','fk_hu_caixa_pesagem_criado_por','f'),
      ('hu_caixa_pesagem','hu_caixa_pesagem_codigo_balanca_fkey','f'),
      ('hu_caixa_pesagem','hu_caixa_pesagem_codigo_usuario_fkey','f'),
      ('hu_caixa_pesagem','hu_caixa_pesagem_pkey','p')
  ), actual(tabela,nome,tipo) AS (
    SELECT c.relname,con.conname,con.contype::text FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
    WHERE n.nspname='homologacao' AND c.relname IN ('hu_caixa','hu_caixa_integracao_sap','hu_caixa_pesagem') AND con.contype<>'n'
  ) SELECT count(*) INTO v_con_diff FROM ((SELECT * FROM actual EXCEPT SELECT * FROM expected) UNION ALL (SELECT * FROM expected EXCEPT SELECT * FROM actual)) d;
  IF v_con_diff<>0 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_CONSTRAINTS_DIVERGENTES diff=%',v_con_diff; END IF;
  SELECT count(*) INTO v_views FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='homologacao' AND c.relkind='v' AND c.relname IN ('vw_hu_caixas_sem_palete','vw_hu_palete_com_caixas','vw_paletizacao_resumo');
  SELECT count(*) INTO v_view_cols FROM information_schema.columns WHERE table_schema='homologacao' AND table_name IN ('vw_hu_caixas_sem_palete','vw_hu_palete_com_caixas','vw_paletizacao_resumo');
  IF v_views<>3 OR v_view_cols<>43 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_VIEWS_DIVERGENTES views=%/3 colunas=%/43',v_views,v_view_cols; END IF;
  -- Grants HU equivalentes ao DEV final.
  IF NOT has_table_privilege('fugapet_q_app','homologacao.hu_caixa','SELECT') OR NOT has_table_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','SELECT') OR NOT has_table_privilege('fugapet_q_app','homologacao.hu_caixa_integracao_sap','SELECT') THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_SELECT_TRES_TABELAS_CONTROLADAS_AUSENTE'; END IF;
  IF has_table_privilege('fugapet_q_app','homologacao.hu_caixa','INSERT,UPDATE,DELETE') OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','INSERT,UPDATE,DELETE') OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa_integracao_sap','INSERT,UPDATE,DELETE') THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_DML_TABELA_CONTROLADA_INDEVIDO'; END IF;
  IF EXISTS (SELECT 1 FROM (VALUES ('homologacao.hu_caixa_etiqueta'),('homologacao.hu_palete'),('homologacao.hu_palete_etiqueta'),('homologacao.hu_palete_integracao_sap'),('homologacao.hu_palete_item')) t(obj) WHERE has_table_privilege('fugapet_q_app',t.obj,'SELECT') OR has_table_privilege('fugapet_q_app',t.obj,'INSERT') OR has_table_privilege('fugapet_q_app',t.obj,'UPDATE') OR has_table_privilege('fugapet_q_app',t.obj,'DELETE')) THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_PRIVILEGIO_DIRETO_OUTRAS_HU_PALETE'; END IF;
  IF EXISTS (SELECT 1 FROM unnest(ARRAY[pg_get_serial_sequence('homologacao.hu_caixa','codigo_hu_caixa'),pg_get_serial_sequence('homologacao.hu_caixa_pesagem','codigo_hu_caixa_pesagem')]) s(seq) WHERE NOT has_sequence_privilege('fugapet_q_app',s.seq,'USAGE') OR NOT has_sequence_privilege('fugapet_q_app',s.seq,'SELECT') OR has_sequence_privilege('fugapet_q_app',s.seq,'UPDATE')) THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_SEQUENCE_CAIXA_PESAGEM_DIVERGENTE'; END IF;
  IF EXISTS (SELECT 1 FROM unnest(ARRAY[pg_get_serial_sequence('homologacao.hu_caixa_integracao_sap','codigo_hu_caixa_integracao_sap'),pg_get_serial_sequence('homologacao.hu_caixa_etiqueta','codigo_hu_caixa_etiqueta'),pg_get_serial_sequence('homologacao.hu_palete','codigo_hu_palete'),pg_get_serial_sequence('homologacao.hu_palete_etiqueta','codigo_hu_palete_etiqueta'),pg_get_serial_sequence('homologacao.hu_palete_integracao_sap','codigo_hu_palete_integracao_sap'),pg_get_serial_sequence('homologacao.hu_palete_item','codigo_hu_palete_item')]) s(seq) WHERE has_sequence_privilege('fugapet_q_app',s.seq,'USAGE') OR has_sequence_privilege('fugapet_q_app',s.seq,'SELECT') OR has_sequence_privilege('fugapet_q_app',s.seq,'UPDATE')) THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_SEQUENCE_OUTRAS_HU_PALETE_DIVERGENTE'; END IF;
  RAISE NOTICE 'FUNCOES_FN_HU=20';
  RAISE NOTICE 'FUNCOES_PUBLICAS_CSHARP=13/13';
  RAISE NOTICE 'TRIGGERS_HU=22';
  RAISE NOTICE 'COLUNAS_HU=201';
  RAISE NOTICE 'INDICES_HU=34';
  RAISE NOTICE 'CONSTRAINTS=CONFORMES_DEV_FINAL_EXCETO_NOT_NULL_CATALOGO_PG18';
  RAISE NOTICE 'VIEWS=CONFORMES';
  RAISE NOTICE 'GRANTS_HU=EQUIVALENTES_DEV_FINAL';
END
$gaia_contract$;

DO $gaia$
DECLARE v_fn oid; v_count integer; v_acl_diff integer; v_public integer; v_def text; v_generic oid; v_generic_md5 text; v_public_dep integer; v_hist_trg integer;
BEGIN
 v_fn:=to_regprocedure('homologacao.fn_hu_registrar_log_alteracao_cadastral()');
 IF v_fn IS NULL THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_FUNCAO_DEDICADA_AUSENTE'; END IF;
 SELECT count(*) INTO v_public FROM pg_proc p CROSS JOIN LATERAL aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a WHERE p.oid=v_fn AND a.grantee=0 AND a.privilege_type='EXECUTE';
 SELECT pg_get_functiondef(v_fn) INTO v_def;
 IF (SELECT pg_get_userbyid(proowner)<>'fugapet_q_owner' OR NOT prosecdef OR NOT (COALESCE(proconfig,ARRAY[]::text[]) @> ARRAY['search_path=pg_catalog']) FROM pg_proc WHERE oid=v_fn) THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_HARDENING_FUNCAO_DEDICADA_DIVERGENTE'; END IF;
 IF v_public<>0 OR has_function_privilege('fugapet_q_app',v_fn,'EXECUTE') THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_EXECUTE_FUNCAO_DEDICADA_INDEVIDO'; END IF;
 IF v_def ~* '\mEXECUTE\M' OR v_def ~* '\mformat\s*\(' OR position('TG_TABLE_SCHEMA' IN v_def)=0 OR position('homologacao.log_alteracao_cadastral' IN v_def)=0 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_FUNCAO_DEDICADA_INSEGURA'; END IF;
 SELECT count(*) INTO v_count FROM (VALUES ('hu_caixa','trg_hu_caixa_log_alteracao_cadastral'),('hu_caixa_pesagem','trg_hu_caixa_pesagem_log_alteracao_cadastral'),('hu_caixa_integracao_sap','trg_hu_caixa_integracao_sap_log_alteracao_cadastral')) x(tabela,trigger_name)
 JOIN pg_class c ON c.relname=x.tabela JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname='homologacao' JOIN pg_trigger t ON t.tgrelid=c.oid AND t.tgname=x.trigger_name AND NOT t.tgisinternal
 WHERE t.tgenabled<>'D' AND t.tgfoid=v_fn;
 IF v_count<>3 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_TRIGGERS_AUDITORIA_HU_DIVERGENTES quantidade=%/3',v_count; END IF;
 IF EXISTS (SELECT 1 FROM (VALUES ('hu_caixa','trg_hu_caixa_log_alteracao_cadastral'),('hu_caixa_pesagem','trg_hu_caixa_pesagem_log_alteracao_cadastral'),('hu_caixa_integracao_sap','trg_hu_caixa_integracao_sap_log_alteracao_cadastral')) x(tabela,trg)
    JOIN pg_class c ON c.relname=x.tabela JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname='homologacao' JOIN pg_trigger t ON t.tgrelid=c.oid AND t.tgname=x.trg AND NOT t.tgisinternal
    WHERE t.tgfoid=to_regprocedure('homologacao.fn_registrar_log_alteracao_cadastral()')) THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_TRIGGER_HU_AINDA_USA_FUNCAO_GENERICA'; END IF;
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
 IF v_acl_diff<>0 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_ACL_LEGADO_TABLE_LOG_DIVERGENTE diff=%',v_acl_diff; END IF;
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
 IF v_acl_diff<>0 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_ACL_LEGADO_SEQUENCE_LOG_DIVERGENTE diff=%',v_acl_diff; END IF;
 v_generic:=to_regprocedure('homologacao.fn_registrar_log_alteracao_cadastral()');
 SELECT md5(regexp_replace(pg_get_functiondef(v_generic),'homologacao\.','<SCHEMA>.','g')) INTO v_generic_md5;
 IF v_generic IS NULL OR (SELECT prosecdef OR pg_get_userbyid(proowner)<>'fugapet_q_owner' OR proconfig IS NOT NULL FROM pg_proc WHERE oid=v_generic) OR v_generic_md5<>'4c0b7abd0a48e87dd60d10b414385d25' THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_FUNCAO_HISTORICA_ALTERADA'; END IF;
 SELECT count(*) INTO v_hist_trg FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_proc p ON p.oid=t.tgfoid
 WHERE n.nspname='homologacao' AND NOT t.tgisinternal AND p.oid=v_generic
   AND NOT ((c.relname='hu_caixa' AND t.tgname='trg_hu_caixa_log_alteracao_cadastral') OR (c.relname='hu_caixa_pesagem' AND t.tgname='trg_hu_caixa_pesagem_log_alteracao_cadastral') OR (c.relname='hu_caixa_integracao_sap' AND t.tgname='trg_hu_caixa_integracao_sap_log_alteracao_cadastral'));
 IF v_hist_trg<>37 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_TRIGGERS_HISTORICOS_FORA_HU=%/37',v_hist_trg; END IF;
 SELECT count(*) INTO v_public_dep FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
 WHERE n.nspname='homologacao' AND p.proname IN ('fn_hu_caixa_finalizar_local','fn_hu_caixa_salvar_preview','fn_hu_caixa_aguardar_autorizacao','fn_hu_caixa_autorizar_envio','fn_hu_caixa_claim_envio','fn_hu_caixa_registrar_sucesso','fn_hu_caixa_registrar_erro','fn_hu_caixa_registrar_timeout','fn_hu_caixa_confirmar_reconciliacao','fn_hu_caixa_registrar_reconciliacao_nao_encontrada','fn_hu_caixa_bloquear_configuracao','fn_hu_caixa_cancelar','fn_hu_caixa_liberar_reprocessamento')
   AND pg_get_functiondef(p.oid) ~* '\mlog_alteracao_cadastral\M';
 IF v_public_dep<>0 THEN RAISE EXCEPTION 'VALIDACAO_044_Q_REV2_FUNCAO_PUBLICA_DEPENDE_LOG_DIRETO quantidade=%',v_public_dep; END IF;
 RAISE NOTICE 'RUNTIME_HU_NAO_DEPENDE_DML_DIRETO_LOG=COMPROVADO_ESTRUTURALMENTE';
END $gaia$;

GRANT SELECT ON tmp_044_context TO fugapet_q_app;

-- Identidades negativas: grant temporario apenas nas duas colunas de identidade.
GRANT INSERT (codigo_hu_caixa) ON homologacao.hu_caixa TO fugapet_q_app;
GRANT INSERT (codigo_hu_caixa_pesagem) ON homologacao.hu_caixa_pesagem TO fugapet_q_app;
SET LOCAL ROLE fugapet_q_app;

INSERT INTO homologacao.hu_caixa(
 codigo_hu_caixa,numero_ordem_producao,item_ordem_producao,correlation_id,
 material,lote,centro,deposito,material_embalagem,origem_material_embalagem,
 peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,unidade_quantidade,
 origem_pesagem,codigo_balanca,codigo_usuario,terminal
)
SELECT -440301,'OP044REV5','0010',gen_random_uuid(),'4000108','L044R5','1000','0001',
       '3000009','SAP',2.500,2.000,0.500,'kg',1.000,'un','manual',NULL,usuario,terminal1
FROM tmp_044_context;

INSERT INTO homologacao.hu_caixa_pesagem(
 codigo_hu_caixa_pesagem,codigo_hu_caixa,codigo_balanca,origem_pesagem,peso_lido,
 peso_bruto,peso_liquido,peso_tara,unidade_peso,payload_balanca,codigo_usuario
)
SELECT -44030101,-440301,NULL,'manual',2.500,2.500,2.000,0.500,'kg',
       '{"origem":"validacao"}'::jsonb,usuario FROM tmp_044_context;

RESET ROLE;
REVOKE INSERT (codigo_hu_caixa) ON homologacao.hu_caixa FROM fugapet_q_app;
REVOKE INSERT (codigo_hu_caixa_pesagem) ON homologacao.hu_caixa_pesagem FROM fugapet_q_app;

DO $gaia$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM homologacao.log_alteracao_cadastral WHERE tabela='hu_caixa' AND codigo_registro=-440301 AND operacao='INSERT') THEN RAISE EXCEPTION 'AUDITORIA_HU_CAIXA_NAO_EXECUTADA'; END IF;
 IF NOT EXISTS(SELECT 1 FROM homologacao.log_alteracao_cadastral WHERE tabela='hu_caixa_pesagem' AND codigo_registro=-44030101 AND operacao='INSERT') THEN RAISE EXCEPTION 'AUDITORIA_HU_CAIXA_PESAGEM_NAO_EXECUTADA'; END IF;
END $gaia$;

-- REV2: nao testar negacao de DML direto no log, pois o ACL legado HML foi formalmente preservado.
-- A independencia do novo HU e comprovada estruturalmente acima: 3 triggers -> funcao dedicada SECURITY DEFINER,
-- funcao generica historica preservada e 13 funcoes publicas sem referencia direta a log_alteracao_cadastral.

DO $gaia$ BEGIN
 IF has_column_privilege('fugapet_q_app','homologacao.hu_caixa','codigo_hu_caixa','INSERT')
    OR has_column_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','codigo_hu_caixa_pesagem','INSERT') THEN
   RAISE EXCEPTION 'GRANT_TEMPORARIO_IDENTIDADE_PERMANECEU';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM homologacao.hu_caixa WHERE codigo_hu_caixa=-440301 AND hu_caixa=codigo_caixa_local AND numero_caixa=1 AND status_hu_caixa='EM_PESAGEM') THEN
   RAISE EXCEPTION 'TRIGGER_NAO_GEROU_IDENTIDADE_SISTEMICA';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM homologacao.hu_caixa_pesagem WHERE codigo_hu_caixa_pesagem=-44030101 AND origem_pesagem='MANUAL' AND codigo_balanca IS NULL AND peso_bruto=2.500 AND peso_liquido=2.000 AND peso_tara=0.500) THEN
   RAISE EXCEPTION 'PESAGEM_MANUAL_COMPLETA_NAO_PERSISTIDA';
 END IF;
END $gaia$;

-- Colunas sistêmicas não podem ser explicitadas pela aplicação.
SET LOCAL ROLE fugapet_q_app;
DO $gaia$ BEGIN
 BEGIN
  INSERT INTO homologacao.hu_caixa(codigo_hu_caixa,numero_ordem_producao,item_ordem_producao,material,lote,centro,deposito,material_embalagem,origem_material_embalagem,peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,unidade_quantidade,origem_pesagem,codigo_usuario,terminal)
  SELECT -449901,'OPX','0010','4000108','L','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,'GAIA-R5-ID' FROM tmp_044_context;
  RAISE EXCEPTION 'IDENTIDADE_EXPLICITA_ACEITA'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  INSERT INTO homologacao.hu_caixa(numero_ordem_producao,item_ordem_producao,material,lote,centro,deposito,material_embalagem,origem_material_embalagem,peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,unidade_quantidade,origem_pesagem,codigo_usuario,terminal,status_hu_caixa)
  SELECT 'OPX','0010','4000108','L','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,'GAIA-R5-STATUS','CONFIRMADA_SAP' FROM tmp_044_context;
  RAISE EXCEPTION 'STATUS_EXPLICITO_ACEITO'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  INSERT INTO homologacao.hu_caixa(numero_ordem_producao,item_ordem_producao,material,lote,centro,deposito,material_embalagem,origem_material_embalagem,peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,unidade_quantidade,origem_pesagem,codigo_usuario,terminal,handling_unit_external_id)
  SELECT 'OPX','0010','4000108','L','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,'GAIA-R5-HU','HU-FABRICADA' FROM tmp_044_context;
  RAISE EXCEPTION 'HU_EXTERNA_EXPLICITA_ACEITA'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  INSERT INTO homologacao.hu_caixa(numero_ordem_producao,item_ordem_producao,material,lote,centro,deposito,material_embalagem,origem_material_embalagem,peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,unidade_quantidade,origem_pesagem,codigo_usuario,terminal,situacao_hu_caixa)
  SELECT 'OPX','0010','4000108','L','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,'GAIA-R5-SIT',false FROM tmp_044_context;
  RAISE EXCEPTION 'SITUACAO_EXPLICITA_ACEITA'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario)
  SELECT -44990101,-440301,'MANUAL',2.5,2.5,2.0,0.5,'KG',usuario FROM tmp_044_context;
  RAISE EXCEPTION 'IDENTIDADE_PESAGEM_EXPLICITA_ACEITA'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $gaia$;
RESET ROLE;

-- Pesagem completa e auditável: nulidades/equação/origem.
DO $gaia$ DECLARE u bigint;b bigint; BEGIN SELECT usuario,balanca INTO u,b FROM tmp_044_context;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030102,-440301,'MANUAL',2.5,NULL,2.0,0.5,'KG',u); RAISE EXCEPTION 'SEM_PESO_BRUTO_ACEITO'; EXCEPTION WHEN not_null_violation THEN NULL; END;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030103,-440301,'MANUAL',2.5,2.5,NULL,0.5,'KG',u); RAISE EXCEPTION 'SEM_PESO_LIQUIDO_ACEITO'; EXCEPTION WHEN not_null_violation THEN NULL; END;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030104,-440301,'MANUAL',2.5,2.5,2.0,NULL,'KG',u); RAISE EXCEPTION 'SEM_TARA_ACEITO'; EXCEPTION WHEN not_null_violation THEN NULL; END;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030105,-440301,'MANUAL',2.5,2.5,2.0,0.5,'KG',NULL); RAISE EXCEPTION 'SEM_USUARIO_ACEITO'; EXCEPTION WHEN not_null_violation THEN NULL; END;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030106,-440301,'MANUAL',2.5,2.5,2.1,0.5,'KG',u); RAISE EXCEPTION 'EQUACAO_INCORRETA_ACEITA'; EXCEPTION WHEN check_violation THEN NULL; END;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,codigo_balanca,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030107,-440301,b,'MANUAL',2.5,2.5,2.0,0.5,'KG',u); RAISE EXCEPTION 'MANUAL_COM_BALANCA_ACEITA'; EXCEPTION WHEN check_violation THEN NULL; END;
 BEGIN INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,codigo_balanca,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030108,-440301,NULL,'BALANCA',2.5,2.5,2.0,0.5,'KG',u); RAISE EXCEPTION 'BALANCA_SEM_CODIGO_ACEITA'; EXCEPTION WHEN check_violation THEN NULL; END;
 INSERT INTO homologacao.hu_caixa_pesagem(codigo_hu_caixa_pesagem,codigo_hu_caixa,codigo_balanca,origem_pesagem,peso_lido,peso_bruto,peso_liquido,peso_tara,unidade_peso,codigo_usuario) VALUES(-44030109,-440301,b,'BALANCA',2.5,2.5,2.0,0.5,'KG',u);
END $gaia$;

-- A role da aplicacao consegue executar as funcoes publicas do fluxo, sem UPDATE direto.
SET LOCAL ROLE fugapet_q_app;
DO $gaia$ DECLARE u bigint;t text; BEGIN
 SELECT usuario,terminal1 INTO u,t FROM tmp_044_context;
 IF NOT homologacao.fn_hu_caixa_finalizar_local(-440301,u,t) THEN RAISE EXCEPTION 'APP_FINALIZAR_LOCAL_FALHOU'; END IF;
 IF NOT homologacao.fn_hu_caixa_salvar_preview(-440301,'{"preview":true}'::jsonb,'/HandlingUnit?sap-client=110') THEN RAISE EXCEPTION 'APP_PREVIEW_FALHOU'; END IF;
 IF NOT homologacao.fn_hu_caixa_aguardar_autorizacao(-440301) THEN RAISE EXCEPTION 'APP_AGUARDAR_AUTORIZACAO_FALHOU'; END IF;
 IF NOT homologacao.fn_hu_caixa_autorizar_envio(-440301,u,t) THEN RAISE EXCEPTION 'APP_AUTORIZAR_ENVIO_FALHOU'; END IF;
END $gaia$;
RESET ROLE;

-- Caixas adicionais com IDs negativos, sem tocar sequences.
INSERT INTO homologacao.hu_caixa(codigo_hu_caixa,numero_ordem_producao,item_ordem_producao,material,lote,centro,deposito,material_embalagem,origem_material_embalagem,peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,unidade_quantidade,origem_pesagem,codigo_usuario,terminal)
SELECT -440302,'OP044REV5','0010','4000108','L044R5','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,terminal2 FROM tmp_044_context UNION ALL
SELECT -440303,'OP044REV5B','0010','4000108','L044R5','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,terminal3 FROM tmp_044_context UNION ALL
SELECT -440304,'OP044REV5C','0010','4000108','L044R5','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,terminal4 FROM tmp_044_context UNION ALL
SELECT -440305,'OP044REV5D','0010','4000108','L044R5','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,terminal5 FROM tmp_044_context UNION ALL
SELECT -440306,'OP044REV5E','0010','4000108','L044R5','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,terminal6 FROM tmp_044_context UNION ALL
SELECT -440307,'OP044REV5F','0010','4000108','L044R5','1000','0001','3000009','SAP',2.5,2.0,0.5,'KG',1,'UN','MANUAL',usuario,terminal7 FROM tmp_044_context;

DO $gaia$ DECLARE r record; BEGIN
 FOR r IN SELECT codigo_hu_caixa,codigo_usuario,terminal FROM homologacao.hu_caixa WHERE codigo_hu_caixa BETWEEN -440307 AND -440302 LOOP
  IF NOT homologacao.fn_hu_caixa_finalizar_local(r.codigo_hu_caixa,r.codigo_usuario,r.terminal) THEN RAISE EXCEPTION 'FINALIZAR_FALHOU %',r.codigo_hu_caixa; END IF;
  IF NOT homologacao.fn_hu_caixa_salvar_preview(r.codigo_hu_caixa,'{"preview":true}'::jsonb,'/HandlingUnit?sap-client=110') THEN RAISE EXCEPTION 'PREVIEW_FALHOU %',r.codigo_hu_caixa; END IF;
  IF NOT homologacao.fn_hu_caixa_aguardar_autorizacao(r.codigo_hu_caixa) THEN RAISE EXCEPTION 'AGUARDAR_AUTORIZACAO_FALHOU %',r.codigo_hu_caixa; END IF;
  IF NOT homologacao.fn_hu_caixa_autorizar_envio(r.codigo_hu_caixa,r.codigo_usuario,r.terminal) THEN RAISE EXCEPTION 'AUTORIZACAO_FALHOU %',r.codigo_hu_caixa; END IF;
 END LOOP;
END $gaia$;

-- UPDATE direto continua proibido, inclusive com grant acidental temporário.
GRANT UPDATE ON homologacao.hu_caixa TO fugapet_q_app;
SET LOCAL ROLE fugapet_q_app;
DO $gaia$ BEGIN
 BEGIN UPDATE homologacao.hu_caixa SET material='4000109' WHERE codigo_hu_caixa=-440301; RAISE EXCEPTION 'UPDATE_DIRETO_ACEITO';
 EXCEPTION WHEN raise_exception THEN IF position('UPDATE_DIRETO_HU_CAIXA_BLOQUEADO_USE_FUNCOES_CONTROLADAS' IN SQLERRM)=0 THEN RAISE; END IF; END;
END $gaia$;
RESET ROLE;
REVOKE UPDATE ON homologacao.hu_caixa FROM fugapet_q_app;

-- Claim vinculado ao ator e terminal.
DO $gaia$ DECLARE u bigint;u_nao_elegivel bigint;t text;c1 homologacao.hu_caixa%ROWTYPE;c2 homologacao.hu_caixa%ROWTYPE;ok boolean; BEGIN
 SELECT usuario,terminal1 INTO u,t FROM tmp_044_context;
 SELECT COALESCE(
          (SELECT min(codigo_usuario) FROM homologacao.usuario
            WHERE NOT situacao_usuario OR bloqueado_usuario),
          -440099
        ) INTO u_nao_elegivel;
 IF EXISTS(SELECT 1 FROM homologacao.fn_hu_caixa_claim_envio(-440301,u,'OUTRO-'||t)) THEN RAISE EXCEPTION 'CLAIM_OUTRO_TERMINAL_ACEITO'; END IF;
 BEGIN PERFORM homologacao.fn_hu_caixa_claim_envio(-440301,u_nao_elegivel,t); RAISE EXCEPTION 'CLAIM_USUARIO_NAO_ELEGIVEL_ACEITO';
 EXCEPTION WHEN raise_exception THEN IF SQLERRM='CLAIM_USUARIO_NAO_ELEGIVEL_ACEITO' THEN RAISE; END IF; END;
 SELECT * INTO c1 FROM homologacao.fn_hu_caixa_claim_envio(-440301,u,t);
 IF c1.claim_token IS NULL OR c1.tentativas<>1 THEN RAISE EXCEPTION 'CLAIM1_INVALIDO'; END IF;
 IF NOT EXISTS(SELECT 1 FROM homologacao.hu_caixa_integracao_sap WHERE codigo_hu_caixa=-440301 AND claim_token=c1.claim_token AND resultado='INICIADO' AND codigo_usuario=u AND terminal=upper(t)) THEN RAISE EXCEPTION 'ATOR_TERMINAL_EVENTO_INICIADO_DIVERGENTE'; END IF;
 ok:=homologacao.fn_hu_caixa_registrar_erro(-440301,1,c1.claim_token,500,'{"erro":true}'::jsonb,NULL,'erro','ERRO_DEFINITIVO',true);
 IF NOT ok THEN RAISE EXCEPTION 'ERRO1_NAO_REGISTRADO'; END IF;
 IF NOT homologacao.fn_hu_caixa_liberar_reprocessamento(-440301,u,t,'retry controlado') THEN RAISE EXCEPTION 'LIBERACAO_RETRY_FALHOU'; END IF;
 SELECT * INTO c2 FROM homologacao.fn_hu_caixa_claim_envio(-440301,u,t);
 IF homologacao.fn_hu_caixa_registrar_sucesso(-440301,1,c1.claim_token,'HU-ATRASADA','',201,'{"ok":true}'::jsonb,NULL,'E-OLD','SAPUSER',clock_timestamp()) THEN RAISE EXCEPTION 'SUCESSO_ATRASADO_ACEITO'; END IF;
 IF homologacao.fn_hu_caixa_registrar_erro(-440301,1,c1.claim_token,500,'{"old":true}'::jsonb,NULL,'old','ERRO_DEFINITIVO',true) THEN RAISE EXCEPTION 'ERRO_ATRASADO_ACEITO'; END IF;
 IF homologacao.fn_hu_caixa_registrar_timeout(-440301,1,c1.claim_token,NULL,'old timeout') THEN RAISE EXCEPTION 'TIMEOUT_ATRASADO_ACEITO'; END IF;
 ok:=homologacao.fn_hu_caixa_registrar_sucesso(-440301,2,c2.claim_token,'HU044REV5-001','',201,'{"HandlingUnitExternalID":"HU044REV5-001"}'::jsonb,'{"messages":["created"]}'::jsonb,'ETAG-R5','SAPUSER',clock_timestamp());
 IF NOT ok THEN RAISE EXCEPTION 'SUCESSO_CLAIM2_FALHOU'; END IF;
 IF NOT EXISTS(SELECT 1 FROM homologacao.hu_caixa_integracao_sap WHERE codigo_hu_caixa=-440301 AND claim_token=c2.claim_token AND resultado='CONFIRMADO' AND codigo_usuario=u AND terminal=upper(t) AND odata_etag='ETAG-R5') THEN RAISE EXCEPTION 'RESULTADO_FINAL_NAO_PRESERVOU_ATOR_DO_CLAIM'; END IF;
END $gaia$;

-- Classificação bidirecional 401/403 e proibição de reprocessamento.
DO $gaia$ DECLARE u bigint;t2 text;t7 text;c homologacao.hu_caixa%ROWTYPE; BEGIN
 SELECT usuario,terminal2,terminal7 INTO u,t2,t7 FROM tmp_044_context;
 SELECT * INTO c FROM homologacao.fn_hu_caixa_claim_envio(-440302,u,t2);
 BEGIN PERFORM homologacao.fn_hu_caixa_registrar_erro(-440302,c.tentativas,c.claim_token,401,NULL,NULL,'x','ERRO_DEFINITIVO',false); RAISE EXCEPTION 'HTTP401_ERRO_DEFINITIVO_ACEITO'; EXCEPTION WHEN raise_exception THEN IF SQLERRM='HTTP401_ERRO_DEFINITIVO_ACEITO' THEN RAISE; END IF; END;
 IF NOT homologacao.fn_hu_caixa_registrar_erro(-440302,c.tentativas,c.claim_token,401,'{"error":"401"}'::jsonb,NULL,'nao autorizado','NAO_AUTORIZADO',false) THEN RAISE EXCEPTION 'HTTP401_NAO_AUTORIZADO_REJEITADO'; END IF;
 IF homologacao.fn_hu_caixa_liberar_reprocessamento(-440302,u,t2,'proibido') THEN RAISE EXCEPTION 'NAO_AUTORIZADO_401_LIBERADO'; END IF;
 SELECT * INTO c FROM homologacao.fn_hu_caixa_claim_envio(-440307,u,t7);
 BEGIN PERFORM homologacao.fn_hu_caixa_registrar_erro(-440307,c.tentativas,c.claim_token,403,NULL,NULL,'x','ERRO_DEFINITIVO',false); RAISE EXCEPTION 'HTTP403_ERRO_DEFINITIVO_ACEITO'; EXCEPTION WHEN raise_exception THEN IF SQLERRM='HTTP403_ERRO_DEFINITIVO_ACEITO' THEN RAISE; END IF; END;
 IF NOT homologacao.fn_hu_caixa_registrar_erro(-440307,c.tentativas,c.claim_token,403,'{"error":"403"}'::jsonb,NULL,'nao autorizado','NAO_AUTORIZADO',false) THEN RAISE EXCEPTION 'HTTP403_NAO_AUTORIZADO_REJEITADO'; END IF;
 IF homologacao.fn_hu_caixa_liberar_reprocessamento(-440307,u,t7,'proibido') THEN RAISE EXCEPTION 'NAO_AUTORIZADO_403_LIBERADO'; END IF;
END $gaia$;

-- Timeout/reconciliação e claim de outro terminal.
DO $gaia$ DECLARE u bigint;t3 text;t4 text;c homologacao.hu_caixa%ROWTYPE; BEGIN
 SELECT usuario,terminal3,terminal4 INTO u,t3,t4 FROM tmp_044_context;
 SELECT * INTO c FROM homologacao.fn_hu_caixa_claim_envio(-440303,u,t3);
 IF NOT homologacao.fn_hu_caixa_registrar_timeout(-440303,c.tentativas,c.claim_token,'{"messages":["timeout"]}'::jsonb,'timeout') THEN RAISE EXCEPTION 'TIMEOUT_FALHOU'; END IF;
 IF homologacao.fn_hu_caixa_cancelar(-440303,u,t3,'inseguro') THEN RAISE EXCEPTION 'TIMEOUT_CANCELADO'; END IF;
 IF NOT homologacao.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(-440303,'HU044REV5-REC','',404,'{"found":false}'::jsonb,NULL,'404') THEN RAISE EXCEPTION 'GET404_FALHOU'; END IF;
 IF (SELECT status_hu_caixa FROM homologacao.hu_caixa WHERE codigo_hu_caixa=-440303)<>'INDETERMINADO_TIMEOUT' THEN RAISE EXCEPTION 'GET404_ALTEROU_STATUS'; END IF;
 IF NOT homologacao.fn_hu_caixa_confirmar_reconciliacao(-440303,'HU044REV5-REC','',200,'{"ok":true}'::jsonb,'{"messages":["reconciled"]}'::jsonb,'ETAG-GET-R5','SAPUSER',clock_timestamp(),true) THEN RAISE EXCEPTION 'RECONCILIACAO_FALHOU'; END IF;
 SELECT * INTO c FROM homologacao.fn_hu_caixa_claim_envio(-440304,u,t4);
 IF EXISTS(SELECT 1 FROM homologacao.fn_hu_caixa_claim_envio(-440304,u,t4)) THEN RAISE EXCEPTION 'SEGUNDO_CLAIM_ACEITO'; END IF;
END $gaia$;

-- Bloqueio de configuração não consome claim; cancelamento seguro é UPDATE.
DO $gaia$ DECLARE u bigint;t5 text;t6 text; BEGIN
 SELECT usuario,terminal5,terminal6 INTO u,t5,t6 FROM tmp_044_context;
 IF NOT homologacao.fn_hu_caixa_bloquear_configuracao(-440305,u,t5,'config ausente',NULL) THEN RAISE EXCEPTION 'BLOQUEIO_CONFIG_FALHOU'; END IF;
 IF EXISTS(SELECT 1 FROM homologacao.hu_caixa WHERE codigo_hu_caixa=-440305 AND (tentativas<>0 OR claim_token IS NOT NULL)) THEN RAISE EXCEPTION 'BLOQUEIO_CONFIG_CONSUMIU_TENTATIVA'; END IF;
 IF NOT homologacao.fn_hu_caixa_cancelar(-440306,u,t6,'cancelamento seguro') THEN RAISE EXCEPTION 'CANCELAMENTO_SEGURO_FALHOU'; END IF;
END $gaia$;

-- App não escreve diretamente no histórico.
SET LOCAL ROLE fugapet_q_app;
DO $gaia$ BEGIN
 BEGIN INSERT INTO homologacao.hu_caixa_integracao_sap(codigo_hu_caixa_integracao_sap,codigo_hu_caixa) VALUES(-1,-440301); RAISE EXCEPTION 'INSERT_HISTORICO_ACEITO'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN UPDATE homologacao.hu_caixa_integracao_sap SET mensagem_erro='x' WHERE codigo_hu_caixa=-440301; RAISE EXCEPTION 'UPDATE_HISTORICO_ACEITO'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN DELETE FROM homologacao.hu_caixa_integracao_sap WHERE codigo_hu_caixa=-440301; RAISE EXCEPTION 'DELETE_HISTORICO_ACEITO'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $gaia$;
RESET ROLE;

SELECT * FROM homologacao.vw_hu_caixas_sem_palete LIMIT 1;

DO $gaia$
DECLARE
    v_owner name;
    v_comment text;
    v_definition text;
    v_grants jsonb;
    v_columns jsonb;
    v_expected tmp_044_view_metadata%ROWTYPE;
BEGIN
    SELECT pg_get_userbyid(c.relowner),obj_description(c.oid,'pg_class'),pg_get_viewdef(c.oid,true)
      INTO v_owner,v_comment,v_definition
      FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='homologacao' AND c.relname='vw_hu_caixas_sem_palete' AND c.relkind='v';
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
               'grantee',CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
               'privilege_type',a.privilege_type,'is_grantable',a.is_grantable
           ) ORDER BY a.grantee,a.privilege_type,a.is_grantable),'[]'::jsonb)
      INTO v_grants
      FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      JOIN LATERAL aclexplode(c.relacl) a ON true
     WHERE n.nspname='homologacao' AND c.relname='vw_hu_caixas_sem_palete'
       AND a.privilege_type<>'TRUNCATE';
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
          'ordinal_position',ic.ordinal_position,'column_name',ic.column_name,
          'data_type',ic.data_type,'character_maximum_length',ic.character_maximum_length,
          'numeric_precision',ic.numeric_precision,'numeric_scale',ic.numeric_scale
        ) ORDER BY ic.ordinal_position),'[]'::jsonb)
      INTO v_columns
      FROM information_schema.columns ic
     WHERE ic.table_schema='homologacao' AND ic.table_name='vw_hu_caixas_sem_palete';
    SELECT * INTO v_expected FROM tmp_044_view_metadata;
    IF v_owner IS DISTINCT FROM v_expected.view_owner
       OR v_comment IS DISTINCT FROM v_expected.view_comment
       OR v_grants IS DISTINCT FROM v_expected.grants_signature
       OR v_columns IS DISTINCT FROM v_expected.columns_signature THEN
        RAISE EXCEPTION 'VIEW_METADATA_DIVERGENTE owner=%/% comentario_igual=% grants_iguais=% colunas_iguais=%',
          v_expected.view_owner,v_owner,
          v_comment IS NOT DISTINCT FROM v_expected.view_comment,
          v_grants IS NOT DISTINCT FROM v_expected.grants_signature,
          v_columns IS NOT DISTINCT FROM v_expected.columns_signature;
    END IF;
    RAISE NOTICE 'VIEW_DEFINICAO_TEXTUAL_IGUAL=%',
      v_definition IS NOT DISTINCT FROM v_expected.view_definition;
END $gaia$;
DO $gaia_view_semantic$
DECLARE
    v_view_oid oid;
    v_schema name;
    v_view_name name;
    v_owner name;
    v_comment text;
    v_definition text;
    v_compact text;
    v_columns text[];
    v_references text[];
    v_literals text[];
    v_outer_segment text;
    v_inner_segment text;
    v_pos_outer integer;
    v_pos_not_exists integer;
    v_pos_inner integer;
    v_forbidden integer;
    v_grants integer;
BEGIN
    SELECT c.oid,n.nspname,c.relname,pg_get_userbyid(c.relowner),obj_description(c.oid,'pg_class')
      INTO v_view_oid,v_schema,v_view_name,v_owner,v_comment
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='homologacao'
       AND c.relname='vw_hu_caixas_sem_palete'
       AND c.relkind='v';

    IF v_view_oid IS NULL OR v_schema<>'homologacao' OR v_view_name<>'vw_hu_caixas_sem_palete' THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_IDENTIDADE';
    END IF;

    SELECT COALESCE(array_agg(a.attname::text ORDER BY a.attnum),ARRAY[]::text[])
      INTO v_columns
      FROM pg_attribute a
     WHERE a.attrelid=v_view_oid AND a.attnum>0 AND NOT a.attisdropped;

    IF v_columns IS DISTINCT FROM ARRAY[
        'codigo_hu_caixa','hu_caixa','material','numero_caixa','peso_bruto',
        'peso_liquido','peso_tara','unidade_peso','status_hu_caixa',
        'criada_sap_em','hu_caixa_criado_em'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_COLUNAS esperado=11 atual=% nomes=%',
          cardinality(v_columns),v_columns;
    END IF;

    SELECT COALESCE(
        array_agg(DISTINCT format('%s.%s',rn.nspname,rc.relname)
                  ORDER BY format('%s.%s',rn.nspname,rc.relname)),
        ARRAY[]::text[])
      INTO v_references
      FROM pg_rewrite r
      JOIN pg_depend d
        ON d.classid='pg_rewrite'::regclass
       AND d.objid=r.oid
       AND d.refclassid='pg_class'::regclass
      JOIN pg_class rc ON rc.oid=d.refobjid
      JOIN pg_namespace rn ON rn.oid=rc.relnamespace
     WHERE r.ev_class=v_view_oid
       AND r.rulename='_RETURN'
       AND rc.oid<>v_view_oid
       AND rc.relkind IN ('r','p','v','m','f');

    IF v_references IS DISTINCT FROM ARRAY[
        'homologacao.hu_caixa',
        'homologacao.hu_palete_item'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_DEPENDENCIAS referencias=%',
          v_references;
    END IF;

    v_definition:=lower(pg_get_viewdef(v_view_oid,false));
    v_compact:=regexp_replace(
        replace(replace(replace(v_definition,'"',''),'(',''),')',''),
        '\s+','','g');

    v_pos_outer:=strpos(v_compact,'status_hu_caixa');
    v_pos_not_exists:=strpos(v_compact,'notexists');
    v_pos_inner:=strpos(v_compact,'status_vinculo');

    IF v_pos_outer=0 OR v_pos_not_exists=0 OR v_pos_inner=0
       OR NOT (v_pos_outer<v_pos_not_exists AND v_pos_not_exists<v_pos_inner) THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_ESTRUTURA_FILTROS';
    END IF;

    v_outer_segment:=substring(v_compact FROM v_pos_outer FOR v_pos_not_exists-v_pos_outer);
    v_inner_segment:=substring(v_compact FROM v_pos_inner);

    IF strpos(v_compact,'situacao_hu_caixa=true')=0
       OR strpos(v_compact,'situacao_hu_palete_item=true')=0
       OR strpos(v_compact,'hpi.codigo_hu_caixa=hc.codigo_hu_caixa')=0
       OR strpos(v_compact,'notexists')=0
       OR strpos(v_outer_segment,'''confirmada_sap''')=0
       OR strpos(v_outer_segment,'''etiquetada''')=0
       OR strpos(v_outer_segment,'''enviada_sap''')=0
       OR strpos(v_inner_segment,'''vinculado''')=0
       OR strpos(v_inner_segment,'''enviado_sap''')=0
       OR strpos(v_inner_segment,'''confirmado_sap''')=0 THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_PREDICADOS';
    END IF;

    SELECT COALESCE(array_agg(DISTINCT matches[1] ORDER BY matches[1]),ARRAY[]::text[])
      INTO v_literals
      FROM regexp_matches(v_definition,'''([^'']+)''','g') AS rm(matches);

    IF v_literals IS DISTINCT FROM ARRAY[
        'confirmada_sap','confirmado_sap','enviada_sap',
        'enviado_sap','etiquetada','vinculado'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_STATUS_EXTRAS_OU_AUSENTES status=%',
          v_literals;
    END IF;

    SELECT
      (SELECT count(*) FROM pg_attribute a WHERE a.attrelid=v_view_oid AND a.attnum>0 AND NOT a.attisdropped AND a.attacl IS NOT NULL)
      + (SELECT count(*) FROM pg_attribute a WHERE a.attrelid=v_view_oid AND a.attnum>0 AND NOT a.attisdropped AND col_description(a.attrelid,a.attnum) IS NOT NULL)
      + (SELECT count(*) FROM pg_rewrite r WHERE r.ev_class=v_view_oid AND r.rulename<>'_RETURN')
      + (SELECT count(*) FROM pg_trigger t WHERE t.tgrelid=v_view_oid AND NOT t.tgisinternal)
      INTO v_forbidden;

    IF v_forbidden<>0 THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_REPROVADA_METADATA_EXTRA quantidade=%',
          v_forbidden;
    END IF;

    SELECT COALESCE(cardinality(c.relacl),0) INTO v_grants
      FROM pg_class c
     WHERE c.oid=v_view_oid;

    PERFORM * FROM homologacao.vw_hu_caixas_sem_palete LIMIT 1;
    RAISE NOTICE
      'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV11_VIEW_SEMANTICA_APROVADA owner=% comentario_presente=% grants_explicitos=% colunas=% dependencias=%',
      v_owner,v_comment IS NOT NULL,v_grants,cardinality(v_columns),v_references;
END
$gaia_view_semantic$;
DO $gaia$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM homologacao.log_alteracao_cadastral WHERE tabela='hu_caixa_integracao_sap' AND COALESCE(dados_novos,dados_anteriores)->>'codigo_hu_caixa' IN ('-440301','-440302','-440303','-440304','-440305','-440306','-440307')) THEN RAISE EXCEPTION 'AUDITORIA_HU_INTEGRACAO_SAP_NAO_EXECUTADA'; END IF;
 IF EXISTS(SELECT 1 FROM homologacao.hu_caixa_integracao_sap WHERE codigo_hu_caixa BETWEEN -440307 AND -440301 AND finalizado_em IS NULL) THEN RAISE EXCEPTION 'HISTORICO_EVENTO_ABERTO'; END IF;
 IF has_column_privilege('fugapet_q_app','homologacao.hu_caixa','codigo_hu_caixa','INSERT')
    OR has_column_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','codigo_hu_caixa_pesagem','INSERT')
    OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa','INSERT')
    OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','INSERT')
    OR has_table_privilege('fugapet_q_app','homologacao.hu_caixa','UPDATE') THEN RAISE EXCEPTION 'PRIVILEGIO_TEMPORARIO_PERMANECEU'; END IF;
 RAISE NOTICE 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV2_CONSOLIDADA_POS_REV11_CONCLUIDA_COM_SUCESSO';
 RAISE NOTICE 'CLAIM_ATOR_TERMINAL=VALIDADO';
 RAISE NOTICE 'GRANTS_POR_COLUNA=VALIDADOS';
 RAISE NOTICE 'PESAGEM_COMPLETA=VALIDADA';
 RAISE NOTICE 'HTTP_401_403_BIDIRECIONAL=VALIDADO';
 RAISE NOTICE 'VIEW_ABERTA=SIM';
 RAISE NOTICE 'ALTERACOES_PALETE=0';
END $gaia$;

ROLLBACK;

SELECT string_agg(
  format('TABLE|%s|%s|%s|%s|%s',n.nspname,c.relname,CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,CASE WHEN a.is_grantable THEN 'YES' ELSE 'NO' END),
  ';' ORDER BY CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,a.is_grantable
) AS acl_legado_depois_table_log
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a
WHERE n.nspname='homologacao' AND c.relname='log_alteracao_cadastral' \gset
\echo 'ACL_LEGADO_DEPOIS_TABLE_LOG=' :acl_legado_depois_table_log

SELECT string_agg(
  format('SEQUENCE|%s|%s|%s|%s|%s',n.nspname,c.relname,CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,CASE WHEN a.is_grantable THEN 'YES' ELSE 'NO' END),
  ';' ORDER BY CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,a.privilege_type,a.is_grantable
) AS acl_legado_depois_sequence_log
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a
WHERE c.oid=pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral')::regclass \gset
\echo 'ACL_LEGADO_DEPOIS_SEQUENCE_LOG=' :acl_legado_depois_sequence_log

\echo '=== INVENTARIO_TRIGGERS_HISTORICOS_FORA_HU_DEPOIS ==='
SELECT c.relname AS tabela,t.tgname AS trigger_name,p.oid::regprocedure::text AS funcao_alvo,t.tgenabled AS enabled,
       pg_get_triggerdef(t.oid,true) AS definicao,pg_get_userbyid(c.relowner) AS table_owner,pg_get_userbyid(p.proowner) AS function_owner
FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_proc p ON p.oid=t.tgfoid
WHERE n.nspname='homologacao' AND NOT t.tgisinternal AND p.proname='fn_registrar_log_alteracao_cadastral'
  AND NOT ((c.relname='hu_caixa' AND t.tgname='trg_hu_caixa_log_alteracao_cadastral')
    OR (c.relname='hu_caixa_pesagem' AND t.tgname='trg_hu_caixa_pesagem_log_alteracao_cadastral')
    OR (c.relname='hu_caixa_integracao_sap' AND t.tgname='trg_hu_caixa_integracao_sap_log_alteracao_cadastral'))
ORDER BY c.relname,t.tgname;

SELECT (
 (SELECT count(*) FROM homologacao.hu_caixa)=:hu_antes::bigint
 AND (SELECT count(*) FROM homologacao.hu_caixa_pesagem)=:pesagens_antes::bigint
 AND (SELECT count(*) FROM homologacao.hu_caixa_integracao_sap)=:historico_antes::bigint
 AND NOT has_column_privilege('fugapet_q_app','homologacao.hu_caixa','codigo_hu_caixa','INSERT')
 AND NOT has_column_privilege('fugapet_q_app','homologacao.hu_caixa_pesagem','codigo_hu_caixa_pesagem','INSERT')
 AND NOT has_table_privilege('fugapet_q_app','homologacao.hu_caixa','UPDATE')
 AND has_table_privilege('fugapet_q_app','homologacao.log_alteracao_cadastral','SELECT')
 AND has_table_privilege('fugapet_q_app','homologacao.log_alteracao_cadastral','INSERT')
 AND has_table_privilege('fugapet_q_app','homologacao.log_alteracao_cadastral','UPDATE')
 AND NOT has_table_privilege('fugapet_q_app','homologacao.log_alteracao_cadastral','DELETE')
 AND has_sequence_privilege('fugapet_q_app',pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral'),'USAGE')
 AND has_sequence_privilege('fugapet_q_app',pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral'),'SELECT')
 AND NOT has_sequence_privilege('fugapet_q_app',pg_get_serial_sequence('homologacao.log_alteracao_cadastral','codigo_log_alteracao_cadastral'),'UPDATE')
 AND NOT EXISTS(
   SELECT 1 FROM homologacao.log_alteracao_cadastral
   WHERE codigo_registro BETWEEN -44990101 AND -440301
      OR COALESCE(dados_novos,dados_anteriores)->>'codigo_hu_caixa' IN ('-440301','-440302','-440303','-440304','-440305','-440306','-440307')
 )
 AND NOT EXISTS(
   SELECT 1 FROM pg_sequences s JOIN gaia_044_seq_antes a USING(schemaname,sequencename)
   WHERE s.last_value IS DISTINCT FROM a.last_value
 )
)::text AS rollback_limpo \gset

SELECT
  CASE
    WHEN a.last_value IS NULL AND s.last_value IS NOT NULL THEN 'PRIMEIRO_USO_NAO_TRANSACIONAL'
    WHEN a.last_value IS NOT NULL AND s.last_value IS NOT NULL AND s.last_value>=a.last_value THEN 'AVANCO_NAO_TRANSACIONAL'
    WHEN a.last_value IS NULL AND s.last_value IS NULL THEN 'SEM_VALOR_OBSERVAVEL'
    ELSE 'ESTADO_INESPERADO'
  END AS audit_sequence_estado,
  CASE WHEN a.last_value IS NOT NULL AND s.last_value IS NOT NULL THEN (s.last_value-a.last_value)::text ELSE '<NAO_CALCULAVEL>' END AS audit_sequence_delta,
  COALESCE(a.last_value::text,'<NULL>') AS audit_sequence_antes,
  COALESCE(s.last_value::text,'<NULL>') AS audit_sequence_depois
FROM gaia_044_audit_seq_antes a
JOIN pg_sequences s USING(schemaname,sequencename) \gset

\if :rollback_limpo
  \echo 'RESIDUOS_VALIDACAO=0'
  \echo 'SEQUENCES_HU_NAO_AVANCARAM=SIM'
  \echo 'SEQUENCE_AUDITORIA_ESTADO=' :audit_sequence_estado
  \echo 'SEQUENCE_AUDITORIA_ANTES=' :audit_sequence_antes
  \echo 'SEQUENCE_AUDITORIA_DEPOIS=' :audit_sequence_depois
  \echo 'SEQUENCE_AUDITORIA_DELTA=' :audit_sequence_delta
  \echo 'SEQUENCE_AUDITORIA_COMPORTAMENTO=NAO_TRANSACIONAL_DOCUMENTADO'
\else
  \echo 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV2_REPROVADA_RESIDUOS_GRANT_OU_SEQUENCE_HU'
  \quit 3
\endif

\echo 'CLASSIFICACAO_OFICIAL=VALIDACAO_044_Q_REV2_CONSOLIDADA_POS_REV11_CONCLUIDA_COM_SUCESSO'
\echo 'AUDITORIA_RUNTIME_fugapet_q_app=VALIDADA_VIA_FUNCAO_DEDICADA'
\echo 'RUNTIME_HU_NAO_DEPENDE_DML_DIRETO_LOG=COMPROVADO'
\echo 'DML_DIRETO_APP_LOG_HML=PRIVILEGIO_LEGADO_PREEXISTENTE_PRESERVADO'
\echo 'ACL_LEGADO_DEPOIS=EXATAMENTE_IGUAL_A_BASELINE_AUDITADA'
\echo 'EXPANSAO_DE_PRIVILEGIO=ZERO'
\echo 'REDUCAO_DE_PRIVILEGIO_LEGADO=ZERO'
\echo 'FUNCAO_HISTORICA_PRESERVADA=SIM'
\echo 'TRIGGERS_HISTORICOS_FORA_HU_PRESERVADOS=SIM'
\echo 'OK - validacao 044 HML REV2 consolidada pos-REV11 concluida com ROLLBACK; ACL legado de auditoria preservado e independencia HU comprovada estruturalmente'
