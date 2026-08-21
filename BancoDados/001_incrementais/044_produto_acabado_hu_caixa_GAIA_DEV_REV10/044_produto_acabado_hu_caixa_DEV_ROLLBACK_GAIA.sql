\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only off
\pset format aligned
\pset null '<NULL>'

-- FugaPET - Incremental 044 DEV - REV10
-- Compatibilidade operacional REV10: PostgreSQL major 15, versao minima 15.5.
-- ROLLBACK oficial de contingencia.
-- NAO faz parte do fluxo normal e recusa perda silenciosa de caixas ou historicos.

\if :{?CONFIRMAR_ROLLBACK_044}
\else
    \echo 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO_CONFIRMACAO_AUSENTE'
    \echo 'Use: -v CONFIRMAR_ROLLBACK_044=EU_CONFIRMO_ROLLBACK_044_DEV'
    \quit 3
\endif

SELECT (:'CONFIRMAR_ROLLBACK_044' = 'EU_CONFIRMO_ROLLBACK_044_DEV')::text AS confirmacao_valida
\gset

\if :confirmacao_valida
\else
    \echo 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO_CONFIRMACAO_INVALIDA'
    \quit 3
\endif

BEGIN;
SET LOCAL statement_timeout = '15min';
SET LOCAL lock_timeout = '10s';

DO $gaia$
DECLARE
    v_version_num integer;
    v_major integer;
    v_hu bigint;
    v_pesagens bigint;
    v_integracoes bigint;
    v_etiquetas bigint;
    v_aplicado boolean;
    v_divergencias text[] := ARRAY[]::text[];
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO BANCO_DIVERGENTE atual=%',
            current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO EXECUTOR_DEVE_SER_POSTGRES atual=%',
            current_user;
    END IF;

    v_version_num := current_setting('server_version_num')::integer;
    v_major := v_version_num / 10000;
    IF v_major <> 15 OR v_version_num < 150005 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO POSTGRESQL_INCOMPATIVEL major_atual=% versao_num_atual=% major_esperado=15 versao_minima=15.5',
            v_major,
            v_version_num;
    END IF;

    -- REV10: o rollback so aceita o pacote integral e com assinaturas exatas.
    v_aplicado :=
        (SELECT count(*)=61 FROM (VALUES
          ('hu_caixa','numero_ordem_producao'),('hu_caixa','item_ordem_producao'),('hu_caixa','codigo_caixa_local'),('hu_caixa','correlation_id'),
          ('hu_caixa','lote'),('hu_caixa','centro'),('hu_caixa','deposito'),('hu_caixa','material_embalagem'),('hu_caixa','origem_material_embalagem'),
          ('hu_caixa','quantidade'),('hu_caixa','unidade_quantidade'),('hu_caixa','origem_pesagem'),('hu_caixa','terminal'),('hu_caixa','endpoint_sanitizado'),
          ('hu_caixa','handling_unit_external_id'),('hu_caixa','warehouse'),('hu_caixa','odata_etag'),('hu_caixa','created_by_user_sap'),('hu_caixa','creation_datetime_sap'),
          ('hu_caixa','http_status'),('hu_caixa','request_json_sanitizado'),('hu_caixa','response_json_sanitizado'),('hu_caixa','sap_messages_sanitizadas'),
          ('hu_caixa','erro_sanitizado'),('hu_caixa','claim_em'),('hu_caixa','claim_token'),('hu_caixa','tentativa_iniciada_em'),('hu_caixa','enviado_sap_em'),
          ('hu_caixa','confirmado_sap_em'),('hu_caixa','reconciliado_em'),('hu_caixa','tentativas'),('hu_caixa','autorizado_envio_em'),('hu_caixa','autorizado_envio_por'),
          ('hu_caixa','terminal_autorizacao'),('hu_caixa','cancelado_em'),('hu_caixa','cancelado_por'),('hu_caixa','motivo_cancelamento'),
          ('hu_caixa','reprocessamento_liberado_em'),('hu_caixa','reprocessamento_liberado_por'),('hu_caixa','motivo_reprocessamento'),('hu_caixa','criado_em'),('hu_caixa','atualizado_em'),
          ('hu_caixa_integracao_sap','correlation_id'),('hu_caixa_integracao_sap','tipo_operacao'),('hu_caixa_integracao_sap','endpoint_sanitizado'),
          ('hu_caixa_integracao_sap','numero_tentativa'),('hu_caixa_integracao_sap','claim_token'),('hu_caixa_integracao_sap','request_json_sanitizado'),
          ('hu_caixa_integracao_sap','response_json_sanitizado'),('hu_caixa_integracao_sap','http_status'),('hu_caixa_integracao_sap','resultado'),
          ('hu_caixa_integracao_sap','handling_unit_external_id'),('hu_caixa_integracao_sap','warehouse'),('hu_caixa_integracao_sap','odata_etag'),
          ('hu_caixa_integracao_sap','mensagem_erro_sanitizada'),('hu_caixa_integracao_sap','iniciado_em'),('hu_caixa_integracao_sap','finalizado_em'),
          ('hu_caixa_integracao_sap','terminal'),('hu_caixa_integracao_sap','criado_em'),('hu_caixa_pesagem','origem_pesagem'),
          ('hu_caixa_integracao_sap','sap_messages_sanitizadas')
        ) e(tabela,coluna)
        WHERE EXISTS (SELECT 1 FROM information_schema.columns c WHERE c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna))
        AND to_regprocedure('desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text)') IS NOT NULL
        AND to_regprocedure('desenvolvimento.fn_hu_caixa_claim_envio(bigint)') IS NULL
        AND to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz)') IS NOT NULL
        AND to_regprocedure('desenvolvimento.fn_hu_caixa_pesagem_normalizar()') IS NOT NULL
        AND to_regprocedure('desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb)') IS NOT NULL
        AND to_regclass('desenvolvimento.uq_hu_caixa_correlation_id') IS NOT NULL
        AND NOT has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','INSERT')
        AND NOT has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_pesagem','INSERT')
        AND NOT has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','UPDATE')
        AND NOT has_schema_privilege('fugapet_dev_app','desenvolvimento','CREATE')
        AND NOT has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','codigo_hu_caixa','INSERT')
        AND NOT has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','status_hu_caixa','INSERT')
        AND NOT has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_pesagem','codigo_hu_caixa_pesagem','INSERT')
        AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_pesagem' AND column_name='peso_bruto' AND is_nullable='NO')
        AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_pesagem' AND column_name='peso_liquido' AND is_nullable='NO')
        AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_pesagem' AND column_name='peso_tara' AND is_nullable='NO')
        AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_pesagem' AND column_name='codigo_usuario' AND is_nullable='NO');

    IF NOT v_aplicado THEN
        v_divergencias:=array_append(v_divergencias,'COLUNAS_FUNCOES_GRANTS_BASE');
    END IF;

    IF EXISTS (
      SELECT 1 FROM (VALUES
          ('ck_hu_caixa_numero_ordem_producao'),
          ('ck_hu_caixa_item_ordem_producao'),
          ('ck_hu_caixa_codigo_local_formato'),
          ('ck_hu_caixa_numero_caixa'),
          ('ck_hu_caixa_material'),
          ('ck_hu_caixa_lote'),
          ('ck_hu_caixa_centro'),
          ('ck_hu_caixa_deposito'),
          ('ck_hu_caixa_material_embalagem'),
          ('ck_hu_caixa_origem_material_embalagem'),
          ('ck_hu_caixa_peso_bruto'),
          ('ck_hu_caixa_peso_tara'),
          ('ck_hu_caixa_peso_liquido'),
          ('ck_hu_caixa_equacao_pesagem'),
          ('ck_hu_caixa_quantidade'),
          ('ck_hu_caixa_unidade_peso_kg'),
          ('ck_hu_caixa_unidade_quantidade'),
          ('ck_hu_caixa_origem_pesagem'),
          ('ck_hu_caixa_balanca_coerente'),
          ('ck_hu_caixa_terminal'),
          ('ck_hu_caixa_warehouse'),
          ('ck_hu_caixa_handling_unit_external_id'),
          ('ck_hu_caixa_http_status'),
          ('ck_hu_caixa_tentativas'),
          ('ck_hu_caixa_claim_token'),
          ('ck_hu_caixa_autorizacao_envio'),
          ('ck_hu_caixa_status'),
          ('ck_hu_caixa_json_objeto'),
          ('ck_hu_caixa_json_sem_segredos'),
          ('ck_hu_caixa_confirmacao_sap'),
          ('ck_hu_caixa_envio_sap'),
          ('ck_hu_caixa_erro_sap'),
          ('ck_hu_caixa_timeout_indeterminado'),
          ('ck_hu_caixa_cancelamento'),
          ('ck_hu_caixa_reprocessamento'),
          ('ck_hu_caixa_legado_sincronizado'),
          ('ck_hu_caixa_pesagem_origem'),
          ('ck_hu_caixa_pesagem_origem_balanca'),
          ('ck_hu_caixa_pesagem_equacao'),
          ('fk_hu_caixa_pesagem_caixa'),
          ('uq_hu_caixa_codigo_correlation'),
          ('fk_hu_caixa_integracao_identidade'),
          ('ck_hu_caixa_integracao_tipo_operacao'),
          ('ck_hu_caixa_integracao_metodo'),
          ('ck_hu_caixa_integracao_numero_tentativa'),
          ('ck_hu_caixa_integracao_resultado'),
          ('ck_hu_caixa_integracao_warehouse'),
          ('ck_hu_caixa_integracao_http_status'),
          ('ck_hu_caixa_integracao_hu'),
          ('ck_hu_caixa_integracao_terminal'),
          ('ck_hu_caixa_integracao_json_objeto'),
          ('ck_hu_caixa_integracao_json_sem_segredos'),
          ('ck_hu_caixa_integracao_periodo'),
          ('ck_hu_caixa_integracao_operacao_metodo'),
          ('ck_hu_caixa_integracao_reprocessamento'),
          ('ck_hu_caixa_integracao_finalizacao'),
          ('ck_hu_caixa_integracao_claim'),
          ('ck_hu_caixa_integracao_legado_sincronizado')
      ) e(nome)
      WHERE NOT EXISTS (
        SELECT 1 FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace
         WHERE n.nspname='desenvolvimento' AND c.conname=e.nome
      )
    ) THEN v_divergencias:=array_append(v_divergencias,'CONSTRAINTS_COMPLETAS'); END IF;

    IF NOT EXISTS (
       SELECT 1 FROM pg_constraint c
        WHERE c.conname='fk_hu_caixa_pesagem_caixa'
          AND c.conrelid='desenvolvimento.hu_caixa_pesagem'::regclass
          AND c.confrelid='desenvolvimento.hu_caixa'::regclass
          AND c.confdeltype='r'
    ) OR NOT EXISTS (
       SELECT 1 FROM pg_constraint c
        WHERE c.conname='fk_hu_caixa_integracao_identidade'
          AND c.conrelid='desenvolvimento.hu_caixa_integracao_sap'::regclass
          AND c.confrelid='desenvolvimento.hu_caixa'::regclass
          AND c.confdeltype='r'
    ) THEN v_divergencias:=array_append(v_divergencias,'FKS_ON_DELETE_RESTRICT'); END IF;

    IF EXISTS (
      SELECT 1 FROM (VALUES
          ('uq_hu_caixa_correlation_id'),
          ('uq_hu_caixa_codigo_local'),
          ('uq_hu_caixa_op_numero'),
          ('uq_hu_caixa_hu_sap_warehouse'),
          ('uq_hu_caixa_terminal_ativo'),
          ('ix_hu_caixa_status_atualizado_044'),
          ('ix_hu_caixa_op'),
          ('ix_hu_caixa_integracao_tentativa_claim'),
          ('uq_hu_caixa_claim_token'),
          ('uq_hu_caixa_integracao_claim_iniciado'),
          ('ix_hu_caixa_integracao_correlation'),
          ('ix_hu_caixa_integracao_hu_sap')
      ) e(nome)
      WHERE to_regclass(format('desenvolvimento.%I',e.nome)) IS NULL
    ) THEN v_divergencias:=array_append(v_divergencias,'INDICES_COMPLETOS'); END IF;

    IF EXISTS (
      SELECT 1 FROM (VALUES
        ('uq_hu_caixa_correlation_id','%UNIQUE INDEX%','%hu_caixa%correlation_id%'),
        ('uq_hu_caixa_codigo_local','%UNIQUE INDEX%','%hu_caixa%codigo_caixa_local%'),
        ('uq_hu_caixa_op_numero','%UNIQUE INDEX%','%numero_ordem_producao%numero_caixa%'),
        ('uq_hu_caixa_hu_sap_warehouse','%UNIQUE INDEX%','%handling_unit_external_id%warehouse%WHERE%handling_unit_external_id IS NOT NULL%'),
        ('uq_hu_caixa_terminal_ativo','%UNIQUE INDEX%','%upper%btrim%terminal%WHERE%status_hu_caixa%CONFIRMADA_SAP%CANCELADA%'),
        ('ix_hu_caixa_status_atualizado_044','%INDEX%','%status_hu_caixa%atualizado_em%DESC%'),
        ('ix_hu_caixa_op','%INDEX%','%numero_ordem_producao%numero_caixa%DESC%'),
        ('ix_hu_caixa_integracao_tentativa_claim','%INDEX%','%codigo_hu_caixa%numero_tentativa%claim_token%criado_em%'),
        ('uq_hu_caixa_claim_token','%UNIQUE INDEX%','%claim_token%WHERE%claim_token IS NOT NULL%'),
        ('uq_hu_caixa_integracao_claim_iniciado','%UNIQUE INDEX%','%claim_token%WHERE%resultado%INICIADO%'),
        ('ix_hu_caixa_integracao_correlation','%INDEX%','%correlation_id%criado_em%'),
        ('ix_hu_caixa_integracao_hu_sap','%INDEX%','%handling_unit_external_id%warehouse%WHERE%handling_unit_external_id IS NOT NULL%')
      ) e(nome,padrao1,padrao2)
      LEFT JOIN pg_indexes i
        ON i.schemaname='desenvolvimento' AND i.indexname=e.nome
      WHERE i.indexname IS NULL
         OR i.indexdef NOT ILIKE e.padrao1
         OR i.indexdef NOT ILIKE e.padrao2
    ) THEN v_divergencias:=array_append(v_divergencias,'INDICES_DEFINICOES_COMPLETAS'); END IF;


    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_hu_sap_warehouse' AND indexdef ILIKE '%WHERE%handling_unit_external_id IS NOT NULL%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_terminal_ativo' AND indexdef ILIKE '%WHERE%status_hu_caixa%CONFIRMADA_SAP%CANCELADA%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_claim_token' AND indexdef ILIKE '%WHERE%claim_token IS NOT NULL%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_integracao_claim_iniciado' AND indexdef ILIKE '%WHERE%resultado%INICIADO%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='desenvolvimento' AND indexname='ix_hu_caixa_integracao_tentativa_claim' AND indexdef ILIKE '%codigo_hu_caixa%numero_tentativa%claim_token%criado_em%')
    THEN v_divergencias:=array_append(v_divergencias,'INDICES_DEFINICOES_PREDICADOS'); END IF;

    IF EXISTS (
      SELECT 1 FROM (VALUES
          ('trg_hu_caixa_preparar_insert','hu_caixa','fn_hu_caixa_preparar_insert'),
          ('trg_hu_caixa_validar_update','hu_caixa','fn_hu_caixa_validar_update'),
          ('trg_hu_caixa_integracao_preparar_insert','hu_caixa_integracao_sap','fn_hu_caixa_integracao_preparar_insert'),
          ('trg_hu_caixa_integracao_imutavel','hu_caixa_integracao_sap','fn_hu_caixa_integracao_imutavel'),
          ('trg_hu_caixa_pesagem_normalizar','hu_caixa_pesagem','fn_hu_caixa_pesagem_normalizar')
      ) e(trigger_name,table_name,function_name)
      WHERE NOT EXISTS (
        SELECT 1 FROM pg_trigger t
        JOIN pg_class c ON c.oid=t.tgrelid
        JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN pg_proc p ON p.oid=t.tgfoid
        WHERE n.nspname='desenvolvimento' AND c.relname=e.table_name
          AND t.tgname=e.trigger_name AND p.proname=e.function_name AND NOT t.tgisinternal
      )
    ) THEN v_divergencias:=array_append(v_divergencias,'TRIGGERS_DEFINICOES_ASSOCIACOES'); END IF;

    IF EXISTS (
      SELECT 1 FROM (VALUES
        ('hu_caixa','status_hu_caixa','EM_PESAGEM'),
        ('hu_caixa','warehouse',''),
        ('hu_caixa','tentativas','0'),
        ('hu_caixa_integracao_sap','warehouse','')
      ) e(tabela,coluna,trecho)
      LEFT JOIN information_schema.columns c
        ON c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna
      WHERE c.column_name IS NULL OR c.column_default IS NULL
         OR (e.trecho<>'' AND c.column_default NOT ILIKE '%'||e.trecho||'%')
    ) OR EXISTS (
      SELECT 1 FROM information_schema.columns c
       WHERE c.table_schema='desenvolvimento'
         AND ((c.table_name='hu_caixa' AND c.column_name IN ('criado_em','atualizado_em'))
           OR (c.table_name='hu_caixa_integracao_sap' AND c.column_name='criado_em'))
         AND (c.column_default IS NULL OR c.column_default NOT ILIKE '%clock_timestamp%')
    ) THEN v_divergencias:=array_append(v_divergencias,'DEFAULTS'); END IF;

    IF EXISTS (
      SELECT 1 FROM information_schema.columns c
       WHERE c.table_schema='desenvolvimento'
         AND ((c.table_name='hu_caixa' AND c.column_name='warehouse')
           OR (c.table_name='hu_caixa_integracao_sap' AND c.column_name='warehouse'))
         AND COALESCE(c.column_default,'') NOT IN ($q$''::character varying$q$,$q$''::text$q$)
    ) THEN v_divergencias:=array_append(v_divergencias,'DEFAULT_WAREHOUSE_VAZIO'); END IF;


    IF EXISTS (
      SELECT 1 FROM (VALUES
          ('fn_hu_caixa_preparar_insert'),
          ('fn_hu_caixa_validar_update'),
          ('fn_hu_caixa_integracao_preparar_insert'),
          ('fn_hu_caixa_integracao_imutavel'),
          ('fn_hu_caixa_pesagem_normalizar'),
          ('fn_hu_caixa_inserir_evento'),
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
      ) e(nome)
      WHERE NOT EXISTS (
        SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
         WHERE n.nspname='desenvolvimento' AND p.proname=e.nome
           AND pg_get_userbyid(p.proowner)='postgres'
      )
    ) THEN v_divergencias:=array_append(v_divergencias,'OWNERS_FUNCOES'); END IF;

    IF EXISTS (
      SELECT 1 FROM unnest(ARRAY[
        pg_get_serial_sequence('desenvolvimento.hu_caixa','codigo_hu_caixa')::regclass,
        pg_get_serial_sequence('desenvolvimento.hu_caixa_pesagem','codigo_hu_caixa_pesagem')::regclass
      ]) s(seq)
      WHERE NOT has_sequence_privilege('fugapet_dev_app',s.seq,'USAGE')
         OR NOT has_sequence_privilege('fugapet_dev_app',s.seq,'SELECT')
         OR has_sequence_privilege('fugapet_dev_app',s.seq,'UPDATE')
         OR pg_get_userbyid((SELECT c.relowner FROM pg_class c WHERE c.oid=s.seq))<>'postgres'
    ) THEN v_divergencias:=array_append(v_divergencias,'SEQUENCES_GRANTS_OWNERS'); END IF;

    IF cardinality(v_divergencias)>0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO_ESTADO_PARCIAL_OU_DIVERGENTE divergencias=%',
            array_to_string(v_divergencias,',');
    END IF;

    SELECT count(*) INTO v_hu FROM desenvolvimento.hu_caixa;
    SELECT count(*) INTO v_pesagens FROM desenvolvimento.hu_caixa_pesagem;
    SELECT count(*) INTO v_integracoes FROM desenvolvimento.hu_caixa_integracao_sap;
    SELECT count(*) INTO v_etiquetas FROM desenvolvimento.hu_caixa_etiqueta;

    IF v_hu <> 0 OR v_pesagens <> 0 OR v_integracoes <> 0 OR v_etiquetas <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO DADOS_NOVOS_EXISTENTES hu_caixa=% pesagens=% integracoes=% etiquetas=%; rollback_nao_pode_apagar_historico',
            v_hu,
            v_pesagens,
            v_integracoes,
            v_etiquetas;
    END IF;

    RAISE NOTICE 'ROLLBACK 044 PRECHECK OK: confirmacao, ambiente e ausencia total de dados comprovados.';
END
$gaia$;

LOCK TABLE desenvolvimento.hu_caixa IN ACCESS EXCLUSIVE MODE;
LOCK TABLE desenvolvimento.hu_caixa_pesagem IN ACCESS EXCLUSIVE MODE;
LOCK TABLE desenvolvimento.hu_caixa_integracao_sap IN ACCESS EXCLUSIVE MODE;
LOCK TABLE desenvolvimento.hu_caixa_etiqueta IN ACCESS EXCLUSIVE MODE;

CREATE TEMP TABLE gaia_044_rollback_view_snapshot (
    view_definition text NOT NULL,
    view_owner name NOT NULL,
    view_comment text,
    column_signature jsonb NOT NULL,
    grants_sql text[],
    grants_signature text[]
) ON COMMIT DROP;

INSERT INTO gaia_044_rollback_view_snapshot(
    view_definition,view_owner,view_comment,column_signature,grants_sql,grants_signature
)
SELECT
    pg_get_viewdef(c.oid,true),
    pg_get_userbyid(c.relowner),
    obj_description(c.oid,'pg_class'),
    (
      SELECT jsonb_agg(jsonb_build_object(
          'ordinal_position',a.attnum,
          'column_name',a.attname
      ) ORDER BY a.attnum)
      FROM pg_attribute a
      WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
    ),
    (
      SELECT array_agg(format(
          'GRANT %s ON TABLE desenvolvimento.vw_hu_caixas_sem_palete TO %s%s',
          a.privilege_type,
          CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE quote_ident(pg_get_userbyid(a.grantee)) END,
          CASE WHEN a.is_grantable THEN ' WITH GRANT OPTION' ELSE '' END
      ) ORDER BY a.grantee,a.privilege_type,a.is_grantable)
      FROM aclexplode(c.relacl) a
      WHERE a.privilege_type<>'TRUNCATE'
    ),
    (
      SELECT array_agg(u.acl::text ORDER BY u.acl::text)
      FROM unnest(c.relacl) AS u(acl)
    )
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='desenvolvimento'
  AND c.relname='vw_hu_caixas_sem_palete'
  AND c.relkind='v';

DO $gaia$
DECLARE v_count integer; v_dependentes integer; v_funcoes_dependentes integer;
BEGIN
    SELECT count(*) INTO v_count FROM gaia_044_rollback_view_snapshot;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO_VIEW_AUSENTE_OU_AMBIGUA';
    END IF;
    SELECT count(*) INTO v_dependentes
      FROM pg_depend d
      JOIN pg_rewrite r ON r.oid=d.objid
      JOIN pg_class dependente ON dependente.oid=r.ev_class
      JOIN pg_namespace nd ON nd.oid=dependente.relnamespace
     WHERE d.refobjid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND NOT (nd.nspname='desenvolvimento' AND dependente.relname='vw_hu_caixas_sem_palete');
    SELECT count(*) INTO v_funcoes_dependentes
      FROM pg_proc p
     WHERE p.prokind='f'
       AND lower(pg_get_functiondef(p.oid)) LIKE '%vw_hu_caixas_sem_palete%';

    IF v_dependentes<>0 OR v_funcoes_dependentes<>0 THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO_VIEW_COM_DEPENDENTES views=% funcoes=%; DROP_CASCADE_PROIBIDO',
          v_dependentes,v_funcoes_dependentes;
    END IF;
END
$gaia$;

DO $gaia$
DECLARE v_forbidden integer;
BEGIN
    SELECT
      (SELECT count(*) FROM pg_attribute a WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND a.attnum>0 AND NOT a.attisdropped AND a.attacl IS NOT NULL)
      + (SELECT count(*) FROM pg_attribute a WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND a.attnum>0 AND NOT a.attisdropped AND col_description(a.attrelid,a.attnum) IS NOT NULL)
      + (SELECT count(*) FROM pg_rewrite r WHERE r.ev_class='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND r.rulename<>'_RETURN')
      + (SELECT count(*) FROM pg_trigger t WHERE t.tgrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND NOT t.tgisinternal)
      INTO v_forbidden;
    IF v_forbidden<>0 THEN
       RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_BLOQUEADO_VIEW_COM_METADATA_NAO_PRESERVADO quantidade=%',v_forbidden;
    END IF;
END
$gaia$;

DROP VIEW desenvolvimento.vw_hu_caixas_sem_palete;

-- Revogar somente os privilegios adicionados pelo 044.
REVOKE SELECT, INSERT
ON TABLE desenvolvimento.hu_caixa
FROM fugapet_dev_app;

REVOKE SELECT, INSERT
ON TABLE desenvolvimento.hu_caixa_pesagem
FROM fugapet_dev_app;

REVOKE SELECT
ON TABLE desenvolvimento.hu_caixa_integracao_sap
FROM fugapet_dev_app;

-- Remove integralmente ACLs por coluna criadas pela REV10.
DO $gaia$
DECLARE v_col record;
BEGIN
    FOR v_col IN
        SELECT table_name,column_name FROM information_schema.columns
         WHERE table_schema='desenvolvimento'
           AND table_name IN ('hu_caixa','hu_caixa_pesagem')
    LOOP
        EXECUTE format(
          'REVOKE INSERT (%I) ON desenvolvimento.%I FROM fugapet_dev_app',
          v_col.column_name,v_col.table_name
        );
    END LOOP;
END
$gaia$;

DO $gaia$
DECLARE
    v_sequence regclass;
BEGIN
    FOREACH v_sequence IN ARRAY ARRAY[
        pg_get_serial_sequence('desenvolvimento.hu_caixa', 'codigo_hu_caixa')::regclass,
        pg_get_serial_sequence('desenvolvimento.hu_caixa_pesagem', 'codigo_hu_caixa_pesagem')::regclass
    ]
    LOOP
        EXECUTE format('REVOKE USAGE, SELECT ON SEQUENCE %s FROM fugapet_dev_app', v_sequence);
    END LOOP;
END
$gaia$;

REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text) FROM fugapet_dev_app;
REVOKE EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text) FROM fugapet_dev_app;

DROP TRIGGER IF EXISTS trg_hu_caixa_validar_update
ON desenvolvimento.hu_caixa;
DROP TRIGGER IF EXISTS trg_hu_caixa_preparar_insert
ON desenvolvimento.hu_caixa;
DROP TRIGGER IF EXISTS trg_hu_caixa_integracao_imutavel
ON desenvolvimento.hu_caixa_integracao_sap;
DROP TRIGGER IF EXISTS trg_hu_caixa_integracao_preparar_insert
ON desenvolvimento.hu_caixa_integracao_sap;
DROP TRIGGER IF EXISTS trg_hu_caixa_pesagem_normalizar
ON desenvolvimento.hu_caixa_pesagem;

DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_inserir_evento(bigint,uuid,varchar,text,varchar,integer,uuid,jsonb,jsonb,jsonb,integer,varchar,varchar,varchar,text,text,boolean,timestamptz,timestamptz,bigint,text);
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_integracao_imutavel();
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_pesagem_normalizar();
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_integracao_preparar_insert();
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_validar_update();
DROP FUNCTION IF EXISTS desenvolvimento.fn_hu_caixa_preparar_insert();

DROP INDEX IF EXISTS desenvolvimento.ix_hu_caixa_integracao_hu_sap;
DROP INDEX IF EXISTS desenvolvimento.ix_hu_caixa_integracao_correlation;
DROP INDEX IF EXISTS desenvolvimento.ix_hu_caixa_integracao_tentativa_claim;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_integracao_claim_iniciado;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_claim_token;
DROP INDEX IF EXISTS desenvolvimento.ix_hu_caixa_op;
DROP INDEX IF EXISTS desenvolvimento.ix_hu_caixa_status_atualizado_044;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_terminal_ativo;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_hu_sap_warehouse;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_op_numero;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_codigo_local;
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_correlation_id;

-- Restaurar a tabela de integração ao contrato anterior.
ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    DROP CONSTRAINT IF EXISTS fk_hu_caixa_integracao_identidade,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_tipo_operacao,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_metodo,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_numero_tentativa,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_resultado,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_warehouse,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_http_status,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_hu,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_terminal,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_json_objeto,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_json_sem_segredos,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_periodo,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_operacao_metodo,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_reprocessamento,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_finalizacao,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_claim,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_integracao_legado_sincronizado;

ALTER TABLE desenvolvimento.hu_caixa
    DROP CONSTRAINT IF EXISTS uq_hu_caixa_codigo_correlation;

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ALTER COLUMN codigo_usuario DROP NOT NULL;

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    DROP COLUMN IF EXISTS correlation_id,
    DROP COLUMN IF EXISTS tipo_operacao,
    DROP COLUMN IF EXISTS endpoint_sanitizado,
    DROP COLUMN IF EXISTS numero_tentativa,
    DROP COLUMN IF EXISTS claim_token,
    DROP COLUMN IF EXISTS request_json_sanitizado,
    DROP COLUMN IF EXISTS response_json_sanitizado,
    DROP COLUMN IF EXISTS http_status,
    DROP COLUMN IF EXISTS resultado,
    DROP COLUMN IF EXISTS handling_unit_external_id,
    DROP COLUMN IF EXISTS warehouse,
    DROP COLUMN IF EXISTS odata_etag,
    DROP COLUMN IF EXISTS sap_messages_sanitizadas,
    DROP COLUMN IF EXISTS mensagem_erro_sanitizada,
    DROP COLUMN IF EXISTS iniciado_em,
    DROP COLUMN IF EXISTS finalizado_em,
    DROP COLUMN IF EXISTS terminal,
    DROP COLUMN IF EXISTS criado_em;

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ALTER COLUMN metodo_http SET DEFAULT 'POST',
    ALTER COLUMN processado_em SET DEFAULT date_trunc('minute', now()),
    ALTER COLUMN hu_caixa_integracao_sap_criado_em SET DEFAULT date_trunc('minute', now());

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ADD CONSTRAINT hu_caixa_integracao_sap_codigo_hu_caixa_fkey
        FOREIGN KEY (codigo_hu_caixa)
        REFERENCES desenvolvimento.hu_caixa(codigo_hu_caixa)
        ON DELETE CASCADE,
    ADD CONSTRAINT ck_hu_caixa_integracao_sap_tentativa
        CHECK (tentativa > 0);

COMMENT ON TABLE desenvolvimento.hu_caixa_integracao_sap IS
'Tentativas de integração SAP para criação/validação de HU de caixa.';

-- Restaurar o histórico de pesagens ao contrato anterior.
ALTER TABLE desenvolvimento.hu_caixa_pesagem
    DROP CONSTRAINT IF EXISTS fk_hu_caixa_pesagem_caixa,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_pesagem_origem,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_pesagem_origem_balanca,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_pesagem_equacao;

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ALTER COLUMN peso_bruto DROP NOT NULL,
    ALTER COLUMN peso_liquido DROP NOT NULL,
    ALTER COLUMN peso_tara DROP NOT NULL,
    ALTER COLUMN codigo_usuario DROP NOT NULL,
    DROP COLUMN IF EXISTS origem_pesagem;

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ALTER COLUMN codigo_balanca SET NOT NULL,
    ALTER COLUMN peso_lido TYPE numeric(14,3) USING peso_lido::numeric(14,3),
    ALTER COLUMN peso_bruto TYPE numeric(14,3) USING peso_bruto::numeric(14,3),
    ALTER COLUMN peso_liquido TYPE numeric(14,3) USING peso_liquido::numeric(14,3),
    ALTER COLUMN peso_tara TYPE numeric(14,3) USING peso_tara::numeric(14,3),
    ALTER COLUMN unidade_peso TYPE text USING unidade_peso::text,
    ALTER COLUMN pesado_em SET DEFAULT date_trunc('minute', now()),
    ALTER COLUMN hu_caixa_pesagem_criado_em SET DEFAULT date_trunc('minute', now());

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ADD CONSTRAINT hu_caixa_pesagem_codigo_hu_caixa_fkey
        FOREIGN KEY (codigo_hu_caixa)
        REFERENCES desenvolvimento.hu_caixa(codigo_hu_caixa)
        ON DELETE CASCADE,
    ADD CONSTRAINT ck_hu_caixa_pesagem_pesos
        CHECK (
            peso_bruto IS NULL
            OR peso_liquido IS NULL
            OR peso_tara IS NULL
            OR (peso_bruto > peso_liquido AND peso_liquido > peso_tara)
        );

COMMENT ON TABLE desenvolvimento.hu_caixa_pesagem IS
'Histórico de pesagens das HUs de caixa.';

-- Remover constraints e colunas novas da caixa principal.
ALTER TABLE desenvolvimento.hu_caixa
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_numero_ordem_producao,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_item_ordem_producao,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_codigo_local_formato,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_numero_caixa,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_material,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_lote,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_centro,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_deposito,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_material_embalagem,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_origem_material_embalagem,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_peso_bruto,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_peso_tara,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_peso_liquido,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_equacao_pesagem,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_quantidade,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_unidade_peso_kg,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_unidade_quantidade,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_origem_pesagem,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_balanca_coerente,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_terminal,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_warehouse,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_handling_unit_external_id,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_http_status,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_tentativas,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_claim_token,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_autorizacao_envio,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_status,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_json_objeto,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_json_sem_segredos,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_confirmacao_sap,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_envio_sap,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_erro_sap,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_timeout_indeterminado,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_cancelamento,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_reprocessamento,
    DROP CONSTRAINT IF EXISTS ck_hu_caixa_legado_sincronizado;

ALTER TABLE desenvolvimento.hu_caixa
    ALTER COLUMN material DROP NOT NULL,
    ALTER COLUMN numero_caixa DROP NOT NULL,
    ALTER COLUMN peso_bruto DROP NOT NULL,
    ALTER COLUMN peso_liquido DROP NOT NULL,
    ALTER COLUMN peso_tara DROP NOT NULL,
    ALTER COLUMN codigo_usuario DROP NOT NULL;

ALTER TABLE desenvolvimento.hu_caixa
    DROP COLUMN IF EXISTS numero_ordem_producao,
    DROP COLUMN IF EXISTS item_ordem_producao,
    DROP COLUMN IF EXISTS codigo_caixa_local,
    DROP COLUMN IF EXISTS correlation_id,
    DROP COLUMN IF EXISTS lote,
    DROP COLUMN IF EXISTS centro,
    DROP COLUMN IF EXISTS deposito,
    DROP COLUMN IF EXISTS material_embalagem,
    DROP COLUMN IF EXISTS origem_material_embalagem,
    DROP COLUMN IF EXISTS quantidade,
    DROP COLUMN IF EXISTS unidade_quantidade,
    DROP COLUMN IF EXISTS origem_pesagem,
    DROP COLUMN IF EXISTS terminal,
    DROP COLUMN IF EXISTS endpoint_sanitizado,
    DROP COLUMN IF EXISTS handling_unit_external_id,
    DROP COLUMN IF EXISTS warehouse,
    DROP COLUMN IF EXISTS odata_etag,
    DROP COLUMN IF EXISTS created_by_user_sap,
    DROP COLUMN IF EXISTS creation_datetime_sap,
    DROP COLUMN IF EXISTS http_status,
    DROP COLUMN IF EXISTS request_json_sanitizado,
    DROP COLUMN IF EXISTS response_json_sanitizado,
    DROP COLUMN IF EXISTS sap_messages_sanitizadas,
    DROP COLUMN IF EXISTS erro_sanitizado,
    DROP COLUMN IF EXISTS claim_em,
    DROP COLUMN IF EXISTS claim_token,
    DROP COLUMN IF EXISTS tentativa_iniciada_em,
    DROP COLUMN IF EXISTS enviado_sap_em,
    DROP COLUMN IF EXISTS confirmado_sap_em,
    DROP COLUMN IF EXISTS reconciliado_em,
    DROP COLUMN IF EXISTS tentativas,
    DROP COLUMN IF EXISTS autorizado_envio_em,
    DROP COLUMN IF EXISTS autorizado_envio_por,
    DROP COLUMN IF EXISTS terminal_autorizacao,
    DROP COLUMN IF EXISTS cancelado_em,
    DROP COLUMN IF EXISTS cancelado_por,
    DROP COLUMN IF EXISTS motivo_cancelamento,
    DROP COLUMN IF EXISTS reprocessamento_liberado_em,
    DROP COLUMN IF EXISTS reprocessamento_liberado_por,
    DROP COLUMN IF EXISTS motivo_reprocessamento,
    DROP COLUMN IF EXISTS criado_em,
    DROP COLUMN IF EXISTS atualizado_em;

ALTER TABLE desenvolvimento.hu_caixa
    ALTER COLUMN material TYPE text USING material::text,
    ALTER COLUMN peso_bruto TYPE numeric(14,3) USING peso_bruto::numeric(14,3),
    ALTER COLUMN peso_liquido TYPE numeric(14,3) USING peso_liquido::numeric(14,3),
    ALTER COLUMN peso_tara TYPE numeric(14,3) USING peso_tara::numeric(14,3),
    ALTER COLUMN unidade_peso TYPE text USING unidade_peso::text,
    ALTER COLUMN status_hu_caixa SET DEFAULT 'PESADA',
    ALTER COLUMN hu_caixa_criado_em SET DEFAULT date_trunc('minute', now());

ALTER TABLE desenvolvimento.hu_caixa
    ADD CONSTRAINT ck_hu_caixa_numero_caixa
        CHECK (numero_caixa IS NULL OR numero_caixa > 0),
    ADD CONSTRAINT ck_hu_caixa_peso_bruto
        CHECK (peso_bruto IS NULL OR peso_bruto > 0),
    ADD CONSTRAINT ck_hu_caixa_peso_tara
        CHECK (peso_tara IS NULL OR peso_tara >= 0),
    ADD CONSTRAINT ck_hu_caixa_pesos
        CHECK (
            peso_bruto IS NULL
            OR peso_liquido IS NULL
            OR peso_tara IS NULL
            OR (peso_bruto > peso_liquido AND peso_liquido > peso_tara)
        ),
    ADD CONSTRAINT ck_hu_caixa_status
        CHECK (status_hu_caixa IN (
            'PESADA',
            'ETIQUETADA',
            'ENVIADA_SAP',
            'CONFIRMADA_SAP',
            'ERRO_SAP',
            'CANCELADA'
        )),
    ADD CONSTRAINT ck_hu_caixa_unidade_peso_kg
        CHECK (upper(trim(unidade_peso)) = 'KG');

COMMENT ON TABLE desenvolvimento.hu_caixa IS
'Registro local de HUs de caixa usadas na formação de paletes INT012.';
COMMENT ON COLUMN desenvolvimento.hu_caixa.hu_caixa IS NULL;
COMMENT ON COLUMN desenvolvimento.hu_caixa.peso_tara IS NULL;
COMMENT ON COLUMN desenvolvimento.hu_caixa.status_hu_caixa IS NULL;


-- Restaurar a definicao capturada e comprovar equivalencia estrutural e semantica.
DO $gaia$
DECLARE
    v_definition text;
    v_definition_after text;
    v_owner name;
    v_owner_after name;
    v_comment text;
    v_comment_after text;
    v_column_signature jsonb;
    v_sql text;
    v_recreated_signature jsonb;
    v_grants_before text[];
    v_grants_signature_before text[];
    v_grants_signature_after text[];
BEGIN
    SELECT view_definition,view_owner,view_comment,column_signature,grants_sql,grants_signature
      INTO v_definition,v_owner,v_comment,v_column_signature,v_grants_before,v_grants_signature_before
      FROM gaia_044_rollback_view_snapshot;

    EXECUTE format(
        'CREATE VIEW desenvolvimento.vw_hu_caixas_sem_palete AS %s',
        v_definition
    );
    EXECUTE format(
        'ALTER VIEW desenvolvimento.vw_hu_caixas_sem_palete OWNER TO %I',
        v_owner
    );
    IF v_comment IS NULL THEN
        EXECUTE 'COMMENT ON VIEW desenvolvimento.vw_hu_caixas_sem_palete IS NULL';
    ELSE
        EXECUTE format(
          'COMMENT ON VIEW desenvolvimento.vw_hu_caixas_sem_palete IS %L',
          v_comment
        );
    END IF;

    FOR v_sql IN
        SELECT unnest(COALESCE(grants_sql,ARRAY[]::text[]))
          FROM gaia_044_rollback_view_snapshot
    LOOP
        EXECUTE v_sql;
    END LOOP;

    SELECT jsonb_agg(jsonb_build_object(
               'ordinal_position',a.attnum,
               'column_name',a.attname
           ) ORDER BY a.attnum)
      INTO v_recreated_signature
      FROM pg_attribute a
     WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND a.attnum>0 AND NOT a.attisdropped;

    IF v_recreated_signature IS DISTINCT FROM v_column_signature THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_BLOQUEADO_VIEW_COLUNAS_DIVERGENTES esperado=% atual=%',
          v_column_signature,v_recreated_signature;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns c
         WHERE c.table_schema='desenvolvimento'
           AND c.table_name='vw_hu_caixas_sem_palete'
           AND (
               (c.column_name IN ('material','unidade_peso') AND c.data_type<>'text')
               OR (c.column_name IN ('peso_bruto','peso_liquido','peso_tara')
                   AND (c.data_type<>'numeric' OR c.numeric_precision<>14 OR c.numeric_scale<>3))
               OR (c.column_name='status_hu_caixa'
                   AND (c.data_type<>'character varying' OR c.character_maximum_length<>30))
           )
    ) THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_BLOQUEADO_VIEW_TIPOS_ANTERIORES_NAO_RESTAURADOS';
    END IF;

    SELECT pg_get_userbyid(c.relowner),obj_description(c.oid,'pg_class'),pg_get_viewdef(c.oid,true)
      INTO v_owner_after,v_comment_after,v_definition_after
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='desenvolvimento'
       AND c.relname='vw_hu_caixas_sem_palete'
       AND c.relkind='v';

    SELECT array_agg(u.acl::text ORDER BY u.acl::text)
      INTO v_grants_signature_after
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
      JOIN LATERAL unnest(c.relacl) AS u(acl) ON true
     WHERE n.nspname='desenvolvimento'
       AND c.relname='vw_hu_caixas_sem_palete';

    IF v_owner_after IS DISTINCT FROM v_owner
       OR v_comment_after IS DISTINCT FROM v_comment
       OR COALESCE(v_grants_signature_after,ARRAY[]::text[]) IS DISTINCT FROM
          COALESCE(v_grants_signature_before,ARRAY[]::text[]) THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_BLOQUEADO_VIEW_METADATA_DIVERGENTE owner=%/% comentario_igual=% grants_iguais=%',
          v_owner,v_owner_after,
          v_comment_after IS NOT DISTINCT FROM v_comment,
          COALESCE(v_grants_signature_after,ARRAY[]::text[]) IS NOT DISTINCT FROM
          COALESCE(v_grants_signature_before,ARRAY[]::text[]);
    END IF;

    RAISE NOTICE 'VIEW_DEFINICAO_TEXTUAL_IGUAL=%',
      v_definition_after IS NOT DISTINCT FROM v_definition;
    PERFORM * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;
END
$gaia$;
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
     WHERE n.nspname='desenvolvimento'
       AND c.relname='vw_hu_caixas_sem_palete'
       AND c.relkind='v';

    IF v_view_oid IS NULL OR v_schema<>'desenvolvimento' OR v_view_name<>'vw_hu_caixas_sem_palete' THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_IDENTIDADE';
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
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_COLUNAS esperado=11 atual=% nomes=%',
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
        'desenvolvimento.hu_caixa',
        'desenvolvimento.hu_palete_item'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_DEPENDENCIAS referencias=%',
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_ESTRUTURA_FILTROS';
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_PREDICADOS';
    END IF;

    SELECT COALESCE(array_agg(DISTINCT matches[1] ORDER BY matches[1]),ARRAY[]::text[])
      INTO v_literals
      FROM regexp_matches(v_definition,'''([^'']+)''','g') AS rm(matches);

    IF v_literals IS DISTINCT FROM ARRAY[
        'confirmada_sap','confirmado_sap','enviada_sap',
        'enviado_sap','etiquetada','vinculado'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_STATUS_EXTRAS_OU_AUSENTES status=%',
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
          'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_METADATA_EXTRA quantidade=%',
          v_forbidden;
    END IF;

    SELECT COALESCE(cardinality(c.relacl),0) INTO v_grants
      FROM pg_class c
     WHERE c.oid=v_view_oid;

    PERFORM * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;
    RAISE NOTICE
      'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REV10_VIEW_SEMANTICA_APROVADA owner=% comentario_presente=% grants_explicitos=% colunas=% dependencias=%',
      v_owner,v_comment IS NOT NULL,v_grants,cardinality(v_columns),v_references;
END
$gaia_view_semantic$;

DO $gaia$
DECLARE
    v_coluna_correlation boolean;
    v_funcao_claim boolean;
    v_indice boolean;
    v_grants integer;
    v_view_ok boolean;
    v_rev5_residuos integer;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'hu_caixa'
          AND column_name = 'correlation_id'
    ) INTO v_coluna_correlation;

    v_funcao_claim := to_regprocedure(
        'desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text)'
    ) IS NOT NULL;
    v_indice := to_regclass(
        'desenvolvimento.uq_hu_caixa_correlation_id'
    ) IS NOT NULL;

    SELECT (
        to_regclass('desenvolvimento.vw_hu_caixas_sem_palete') IS NOT NULL
        AND EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema='desenvolvimento'
               AND table_name='vw_hu_caixas_sem_palete'
               AND column_name='codigo_hu_caixa'
        )
    ) INTO v_view_ok;

    SELECT
        (EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_pesagem' AND column_name='origem_pesagem'))::integer
      + (EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_integracao_sap' AND column_name='sap_messages_sanitizadas'))::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_pesagem_normalizar()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb)') IS NOT NULL)::integer
      INTO v_rev5_residuos;

    SELECT count(*) INTO v_grants
    FROM (VALUES
        ('desenvolvimento.hu_caixa'),
        ('desenvolvimento.hu_caixa_pesagem'),
        ('desenvolvimento.hu_caixa_integracao_sap')
    ) AS t(tabela)
    CROSS JOIN (VALUES
        ('SELECT'), ('INSERT'), ('UPDATE'), ('DELETE'),
        ('TRUNCATE'), ('REFERENCES'), ('TRIGGER')
    ) AS p(privilegio)
    WHERE has_table_privilege('fugapet_dev_app', t.tabela, p.privilegio);

    IF v_coluna_correlation OR v_funcao_claim OR v_indice
       OR v_grants <> 0 OR NOT v_view_ok OR v_rev5_residuos<>0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_REPROVADO_POSTCHECK correlation=% funcao_claim=% indice=% grants_residuais=% view_ok=% residuos_rev5=%',
            v_coluna_correlation,
            v_funcao_claim,
            v_indice,
            v_grants,
            v_view_ok,
            v_rev5_residuos;
    END IF;

    RAISE NOTICE 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_CONCLUIDO_COM_SUCESSO';
    RAISE NOTICE 'DADOS_REMOVIDOS=0';
    RAISE NOTICE 'HISTORICO_REMOVIDO=0';
    RAISE NOTICE 'ESTRUTURAS_PALETE_ALTERADAS=0';
    RAISE NOTICE 'MATRIZ_GRANTS_ANTERIOR_RESTAURADA=VAZIA';
END
$gaia$;

COMMIT;

\echo 'CLASSIFICACAO_OFICIAL=ROLLBACK_044_DEV_CONCLUIDO_COM_SUCESSO'
\echo 'OK - incremental 044 DEV revertido sem perda de dados'
