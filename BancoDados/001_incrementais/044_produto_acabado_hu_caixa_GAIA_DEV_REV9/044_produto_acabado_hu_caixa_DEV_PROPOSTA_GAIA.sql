\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only off
\pset format aligned
\pset null '<NULL>'

-- FugaPET - Incremental 044 DEV - REV9
-- Persistencia controlada de uma HU SAP para uma caixa individual de Produto Acabado.
-- Compatibilidade operacional REV9: PostgreSQL major 15, versao minima 15.5.
-- A PROPOSTA nao insere dados produtivos, nao chama SAP e nao altera objetos de palete.

-- A deteccao e calculada integralmente antes de qualquer \if.
-- TOTAL_COLUNAS=59 e TOTAL_OBJETOS=34 correspondem ao nucleo REV2 preservado.
-- A REV5 acrescenta dois campos e tres objetos controlados, validados separadamente.
WITH expected_columns(tabela,coluna) AS (
    VALUES
        ('hu_caixa','numero_ordem_producao'),('hu_caixa','item_ordem_producao'),
        ('hu_caixa','codigo_caixa_local'),('hu_caixa','correlation_id'),
        ('hu_caixa','lote'),('hu_caixa','centro'),('hu_caixa','deposito'),
        ('hu_caixa','material_embalagem'),('hu_caixa','origem_material_embalagem'),
        ('hu_caixa','quantidade'),('hu_caixa','unidade_quantidade'),
        ('hu_caixa','origem_pesagem'),('hu_caixa','terminal'),
        ('hu_caixa','endpoint_sanitizado'),('hu_caixa','handling_unit_external_id'),
        ('hu_caixa','warehouse'),('hu_caixa','odata_etag'),
        ('hu_caixa','created_by_user_sap'),('hu_caixa','creation_datetime_sap'),
        ('hu_caixa','http_status'),('hu_caixa','request_json_sanitizado'),
        ('hu_caixa','response_json_sanitizado'),('hu_caixa','sap_messages_sanitizadas'),
        ('hu_caixa','erro_sanitizado'),('hu_caixa','claim_em'),('hu_caixa','claim_token'),
        ('hu_caixa','tentativa_iniciada_em'),('hu_caixa','enviado_sap_em'),
        ('hu_caixa','confirmado_sap_em'),('hu_caixa','reconciliado_em'),
        ('hu_caixa','tentativas'),('hu_caixa','autorizado_envio_em'),
        ('hu_caixa','autorizado_envio_por'),('hu_caixa','terminal_autorizacao'),
        ('hu_caixa','cancelado_em'),('hu_caixa','cancelado_por'),
        ('hu_caixa','motivo_cancelamento'),('hu_caixa','reprocessamento_liberado_em'),
        ('hu_caixa','reprocessamento_liberado_por'),('hu_caixa','motivo_reprocessamento'),
        ('hu_caixa','criado_em'),('hu_caixa','atualizado_em'),
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
        ('hu_caixa_integracao_sap','criado_em')
), expected_objects(nome,tipo) AS (
    VALUES
      ('fn_hu_caixa_preparar_insert()','FUNCTION'),('fn_hu_caixa_validar_update()','FUNCTION'),
      ('fn_hu_caixa_integracao_preparar_insert()','FUNCTION'),('fn_hu_caixa_integracao_imutavel()','FUNCTION'),
      ('fn_hu_caixa_inserir_evento(bigint,uuid,varchar,text,varchar,integer,uuid,jsonb,jsonb,jsonb,integer,varchar,varchar,varchar,text,text,boolean,timestamptz,timestamptz,bigint,text)','FUNCTION'),('fn_hu_caixa_finalizar_local(bigint,bigint,text)','FUNCTION'),
      ('fn_hu_caixa_salvar_preview(bigint,jsonb,text)','FUNCTION'),('fn_hu_caixa_aguardar_autorizacao(bigint)','FUNCTION'),
      ('fn_hu_caixa_autorizar_envio(bigint,bigint,text)','FUNCTION'),('fn_hu_caixa_claim_envio(bigint,bigint,text)','FUNCTION'),
      ('fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz)','FUNCTION'),('fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean)','FUNCTION'),
      ('fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text)','FUNCTION'),('fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean)','FUNCTION'),
      ('fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text)','FUNCTION'),
      ('fn_hu_caixa_cancelar(bigint,bigint,text,text)','FUNCTION'),('fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text)','FUNCTION'),
      ('trg_hu_caixa_preparar_insert','TRIGGER'),('trg_hu_caixa_validar_update','TRIGGER'),
      ('trg_hu_caixa_integracao_preparar_insert','TRIGGER'),('trg_hu_caixa_integracao_imutavel','TRIGGER'),
      ('uq_hu_caixa_correlation_id','RELATION'),('uq_hu_caixa_codigo_local','RELATION'),
      ('uq_hu_caixa_op_numero','RELATION'),('uq_hu_caixa_hu_sap_warehouse','RELATION'),
      ('uq_hu_caixa_terminal_ativo','RELATION'),('ix_hu_caixa_status_atualizado_044','RELATION'),
      ('ix_hu_caixa_op','RELATION'),('ix_hu_caixa_integracao_tentativa_claim','RELATION'),
      ('uq_hu_caixa_claim_token','RELATION'),('uq_hu_caixa_integracao_claim_iniciado','RELATION'),
      ('ix_hu_caixa_integracao_correlation','RELATION'),('ix_hu_caixa_integracao_hu_sap','RELATION'),
      ('uq_hu_caixa_codigo_correlation','CONSTRAINT')
), counts AS (
 SELECT
   (SELECT count(*) FROM expected_columns e
      WHERE EXISTS (SELECT 1 FROM information_schema.columns c
                    WHERE c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna)) AS colunas_044,
   (SELECT count(*) FROM expected_objects e
      WHERE (e.tipo='FUNCTION' AND to_regprocedure(
                format('desenvolvimento.%s',e.nome)
              ) IS NOT NULL)
         OR (e.tipo='TRIGGER' AND EXISTS (
                SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid
                JOIN pg_namespace n ON n.oid=c.relnamespace
                 WHERE n.nspname='desenvolvimento' AND NOT t.tgisinternal AND t.tgname=e.nome))
         OR (e.tipo='RELATION' AND to_regclass(format('desenvolvimento.%I',e.nome)) IS NOT NULL)
         OR (e.tipo='CONSTRAINT' AND EXISTS (
                SELECT 1 FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace
                 WHERE n.nspname='desenvolvimento' AND c.conname=e.nome))) AS objetos_044,
   (SELECT count(*) FROM (VALUES
      ('hu_caixa_pesagem','origem_pesagem'),
      ('hu_caixa_integracao_sap','sap_messages_sanitizadas')
    ) x(tabela,coluna)
    WHERE EXISTS (SELECT 1 FROM information_schema.columns c
                  WHERE c.table_schema='desenvolvimento' AND c.table_name=x.tabela AND c.column_name=x.coluna)) AS colunas_rev5,
   ((to_regprocedure('desenvolvimento.fn_hu_caixa_pesagem_normalizar()') IS NOT NULL)::integer
    + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid
              JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_pesagem_normalizar'
                AND NOT t.tgisinternal)::integer
    + (to_regprocedure('desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb)') IS NOT NULL)::integer
   ) AS objetos_rev5
)
SELECT
 colunas_044, 59 AS total_colunas,
 objetos_044, 34 AS total_objetos,
 colunas_rev5, 2 AS total_colunas_rev5,
 objetos_rev5, 3 AS total_objetos_rev5,
 ((colunas_044=0 AND objetos_044=0 AND colunas_rev5=0 AND objetos_rev5=0)
   OR (colunas_044=59 AND objetos_044=34 AND colunas_rev5=2 AND objetos_rev5=3))::text AS estado_valido,
 (colunas_044=59 AND objetos_044=34 AND colunas_rev5=2 AND objetos_rev5=3)::text AS pacote_044_ja_aplicado
FROM counts
\gset

\if :estado_valido
\else
    \echo 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA_ESTADO_PARCIAL_OU_DIVERGENTE'
    \echo 'TOTAL_COLUNAS=' :colunas_044 '/59 TOTAL_OBJETOS=' :objetos_044 '/34 COLUNAS_REV5=' :colunas_rev5 '/2 OBJETOS_REV5=' :objetos_rev5 '/3'
    \quit 3
\endif

\if :pacote_044_ja_aplicado
DO $gaia$
DECLARE
    v_divergencias text[] := ARRAY[]::text[];
    v_public_execute integer;
    v_app_execute integer;
    v_view_forbidden integer;
BEGIN
    -- Nomes completos nao bastam: tipos, tamanhos, nulabilidade e defaults.
    IF EXISTS (
        SELECT 1
          FROM (VALUES
            ('hu_caixa','material','character varying',18,'NO'),
            ('hu_caixa','peso_bruto','numeric',NULL,'NO'),
            ('hu_caixa','peso_liquido','numeric',NULL,'NO'),
            ('hu_caixa','peso_tara','numeric',NULL,'NO'),
            ('hu_caixa','unidade_peso','character varying',3,'NO'),
            ('hu_caixa','terminal','character varying',120,'NO'),
            ('hu_caixa_pesagem','origem_pesagem','character varying',30,'NO'),
            ('hu_caixa_pesagem','peso_bruto','numeric',NULL,'NO'),
            ('hu_caixa_pesagem','peso_liquido','numeric',NULL,'NO'),
            ('hu_caixa_pesagem','peso_tara','numeric',NULL,'NO'),
            ('hu_caixa_pesagem','codigo_usuario','bigint',NULL,'NO'),
            ('hu_caixa_integracao_sap','claim_token','uuid',NULL,'YES'),
            ('hu_caixa_integracao_sap','sap_messages_sanitizadas','jsonb',NULL,'YES')
          ) e(tabela,coluna,tipo,tamanho,nulo)
          LEFT JOIN information_schema.columns c
            ON c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna
         WHERE c.column_name IS NULL
            OR c.data_type<>e.tipo
            OR (e.tamanho IS NOT NULL AND c.character_maximum_length<>e.tamanho)
            OR c.is_nullable<>e.nulo
    ) THEN v_divergencias:=array_append(v_divergencias,'COLUNAS_TIPOS_NULABILIDADE'); END IF;

    IF to_regprocedure('desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text)') IS NULL
       OR to_regprocedure('desenvolvimento.fn_hu_caixa_claim_envio(bigint)') IS NOT NULL THEN
        v_divergencias:=array_append(v_divergencias,'ASSINATURA_CLAIM');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid='desenvolvimento.hu_caixa_pesagem'::regclass
          AND c.conname='ck_hu_caixa_pesagem_equacao'
          AND pg_get_constraintdef(c.oid,true) ILIKE '%peso_bruto = (peso_liquido + peso_tara)%'
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid='desenvolvimento.hu_caixa_pesagem'::regclass
          AND c.conname='fk_hu_caixa_pesagem_caixa' AND c.confdeltype='r'
    ) THEN v_divergencias:=array_append(v_divergencias,'CONSTRAINTS_OU_FKS'); END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_terminal_ativo'
           AND indexdef ILIKE '%WHERE%status_hu_caixa%CONFIRMADA_SAP%CANCELADA%'
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname='desenvolvimento' AND indexname='ix_hu_caixa_integracao_tentativa_claim'
           AND indexdef ILIKE '%codigo_hu_caixa%numero_tentativa%claim_token%criado_em%'
    ) THEN v_divergencias:=array_append(v_divergencias,'INDICES_OU_PREDICADOS'); END IF;

    IF EXISTS (
        SELECT 1 FROM (VALUES
          ('trg_hu_caixa_preparar_insert','hu_caixa'),
          ('trg_hu_caixa_validar_update','hu_caixa'),
          ('trg_hu_caixa_integracao_preparar_insert','hu_caixa_integracao_sap'),
          ('trg_hu_caixa_integracao_imutavel','hu_caixa_integracao_sap'),
          ('trg_hu_caixa_pesagem_normalizar','hu_caixa_pesagem')
        ) e(trigger_name,table_name)
        WHERE NOT EXISTS (
          SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid
          JOIN pg_namespace n ON n.oid=c.relnamespace
          WHERE n.nspname='desenvolvimento' AND c.relname=e.table_name
            AND t.tgname=e.trigger_name AND NOT t.tgisinternal
        )
    ) THEN v_divergencias:=array_append(v_divergencias,'TRIGGERS_ASSOCIADOS'); END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
         WHERE n.nspname='desenvolvimento'
           AND c.relname IN ('hu_caixa','hu_caixa_pesagem','hu_caixa_integracao_sap','vw_hu_caixas_sem_palete')
           AND pg_get_userbyid(c.relowner)<>'postgres'
    ) THEN v_divergencias:=array_append(v_divergencias,'OWNERS'); END IF;

    IF has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','INSERT')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_pesagem','INSERT')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','UPDATE')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_integracao_sap','INSERT')
       OR has_schema_privilege('fugapet_dev_app','desenvolvimento','CREATE') THEN
        v_divergencias:=array_append(v_divergencias,'GRANTS_TABELA_OU_SCHEMA');
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
      WHERE NOT has_column_privilege('fugapet_dev_app',format('desenvolvimento.%I',e.tabela),e.coluna,'INSERT')
    ) OR EXISTS (
      SELECT 1 FROM information_schema.columns cp
       WHERE cp.table_schema='desenvolvimento'
         AND cp.table_name IN ('hu_caixa','hu_caixa_pesagem')
         AND has_column_privilege('fugapet_dev_app',format('desenvolvimento.%I',cp.table_name),cp.column_name,'INSERT')
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
           WHERE e.tabela=cp.table_name AND e.coluna=cp.column_name
         )
    ) THEN v_divergencias:=array_append(v_divergencias,'GRANTS_COLUNA'); END IF;


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

    SELECT count(*) INTO v_public_execute
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
      JOIN LATERAL aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a ON true
     WHERE n.nspname='desenvolvimento' AND p.proname LIKE 'fn_hu_caixa%'
       AND a.grantee=0 AND a.privilege_type='EXECUTE';
    IF v_public_execute<>0 THEN v_divergencias:=array_append(v_divergencias,'EXECUTE_PUBLIC'); END IF;

    SELECT count(*) INTO v_app_execute
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento'
       AND p.proname IN ('fn_hu_caixa_finalizar_local','fn_hu_caixa_salvar_preview','fn_hu_caixa_aguardar_autorizacao','fn_hu_caixa_autorizar_envio','fn_hu_caixa_claim_envio','fn_hu_caixa_registrar_sucesso','fn_hu_caixa_registrar_erro','fn_hu_caixa_registrar_timeout','fn_hu_caixa_confirmar_reconciliacao','fn_hu_caixa_registrar_reconciliacao_nao_encontrada','fn_hu_caixa_bloquear_configuracao','fn_hu_caixa_cancelar','fn_hu_caixa_liberar_reprocessamento')
       AND has_function_privilege('fugapet_dev_app',p.oid,'EXECUTE');
    IF v_app_execute<>13 THEN v_divergencias:=array_append(v_divergencias,'EXECUTE_APP'); END IF;

    SELECT
      (SELECT count(*) FROM pg_attribute a WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND a.attnum>0 AND NOT a.attisdropped AND a.attacl IS NOT NULL)
      + (SELECT count(*) FROM pg_attribute a WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND a.attnum>0 AND NOT a.attisdropped AND col_description(a.attrelid,a.attnum) IS NOT NULL)
      + (SELECT count(*) FROM pg_rewrite r WHERE r.ev_class='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND r.rulename<>'_RETURN')
      + (SELECT count(*) FROM pg_trigger t WHERE t.tgrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass AND NOT t.tgisinternal)
      INTO v_view_forbidden;
    IF v_view_forbidden<>0 THEN v_divergencias:=array_append(v_divergencias,'VIEW_METADATA_ADICIONAL'); END IF;
    PERFORM * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;

    IF cardinality(v_divergencias)>0 THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA_ESTADO_PARCIAL_OU_DIVERGENTE divergencias=%',array_to_string(v_divergencias,',');
    END IF;
END
$gaia$;
    \echo 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_JA_APLICADA'
    \echo 'ESTADO_PACOTE=PACOTE_REV9_INTEGRALMENTE_APLICADO_NENHUMA_ALTERACAO_EXECUTADA'
\else

BEGIN;
SET LOCAL statement_timeout = '15min';
SET LOCAL lock_timeout = '10s';

DO $gaia$
DECLARE
    v_version_num integer;
    v_major integer;
    v_schema_exists boolean;
    v_role_ok boolean;
    v_missing_tables text;
    v_hu bigint;
    v_pesagens bigint;
    v_integracoes bigint;
    v_etiquetas bigint;
    v_partial_columns integer;
    v_partial_objects integer;
    v_owner_invalid integer;
    v_existing_table_privileges integer;
    v_existing_sequence_privileges integer;
    v_elevated_memberships integer;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA BANCO_DIVERGENTE atual=% esperado=fuga_jales_local_desenvolvimento',
            current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA EXECUTOR_DEVE_SER_POSTGRES atual=%',
            current_user;
    END IF;

    v_version_num := current_setting('server_version_num')::integer;
    v_major := v_version_num / 10000;
    IF v_major <> 15 OR v_version_num < 150005 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA POSTGRESQL_INCOMPATIVEL major_atual=% versao_num_atual=% major_esperado=15 versao_minima=15.5',
            v_major,
            v_version_num;
    END IF;

    SELECT to_regnamespace('desenvolvimento') IS NOT NULL
      INTO v_schema_exists;
    IF NOT v_schema_exists THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA SCHEMA_DESENVOLVIMENTO_AUSENTE';
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname='fugapet_dev_app'
          AND rolcanlogin
          AND NOT rolsuper
          AND NOT rolcreatedb
          AND NOT rolcreaterole
          AND NOT rolreplication
          AND NOT rolbypassrls
    ) INTO v_role_ok;
    IF NOT v_role_ok THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA ROLE_FUGAPET_DEV_APP_AUSENTE_OU_INSEGURA';
    END IF;

    WITH RECURSIVE memberships(roleid) AS (
        SELECT m.roleid
          FROM pg_auth_members m
          JOIN pg_roles member_role ON member_role.oid=m.member
         WHERE member_role.rolname='fugapet_dev_app'
        UNION
        SELECT m.roleid
          FROM pg_auth_members m
          JOIN memberships x ON x.roleid=m.member
    )
    SELECT count(*) INTO v_elevated_memberships
      FROM memberships m
      JOIN pg_roles r ON r.oid=m.roleid
     WHERE r.rolsuper OR r.rolcreatedb OR r.rolcreaterole
        OR r.rolreplication OR r.rolbypassrls
        OR has_schema_privilege(r.rolname,'desenvolvimento','CREATE');

    IF v_elevated_memberships<>0 THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA ROLE_FUGAPET_DEV_APP_HERDA_ROLE_ELEVADA quantidade=%',
          v_elevated_memberships;
    END IF;

    IF NOT has_schema_privilege('fugapet_dev_app','desenvolvimento','USAGE') THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA ROLE_SEM_USAGE_NO_SCHEMA';
    END IF;

    IF has_schema_privilege('fugapet_dev_app','desenvolvimento','CREATE')
       AND NOT EXISTS (
           SELECT 1
             FROM pg_namespace n
             JOIN LATERAL aclexplode(COALESCE(n.nspacl, acldefault('n', n.nspowner))) a ON true
             JOIN pg_roles r ON r.oid=a.grantee
            WHERE n.nspname='desenvolvimento'
              AND r.rolname='fugapet_dev_app'
              AND a.privilege_type='CREATE'
       ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA CREATE_SCHEMA_HERDADO_OU_PUBLIC_NAO_PODE_SER_CORRIGIDO_ISOLADAMENTE';
    END IF;

    SELECT string_agg(t.nome, ', ' ORDER BY t.nome)
      INTO v_missing_tables
      FROM (VALUES
          ('hu_caixa'),
          ('hu_caixa_pesagem'),
          ('hu_caixa_integracao_sap'),
          ('hu_caixa_etiqueta')
      ) AS t(nome)
     WHERE to_regclass(format('desenvolvimento.%I', t.nome)) IS NULL;

    IF v_missing_tables IS NOT NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA TABELAS_HU_AUSENTES=%',
            v_missing_tables;
    END IF;

    IF to_regprocedure('desenvolvimento.fn_definir_atualizado_em()') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA FUNCAO_OFICIAL_ATUALIZADO_EM_AUSENTE';
    END IF;

    SELECT count(*)
      INTO v_owner_invalid
      FROM pg_class c
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'desenvolvimento'
       AND c.relname IN (
           'hu_caixa',
           'hu_caixa_pesagem',
           'hu_caixa_integracao_sap',
           'hu_caixa_etiqueta'
       )
       AND pg_get_userbyid(c.relowner) <> current_user;

    IF v_owner_invalid > 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA EXECUTOR_NAO_E_OWNER_DE_TODAS_AS_TABELAS quantidade=% executor=%',
            v_owner_invalid,
            current_user;
    END IF;

    SELECT count(*) INTO v_hu FROM desenvolvimento.hu_caixa;
    SELECT count(*) INTO v_pesagens FROM desenvolvimento.hu_caixa_pesagem;
    SELECT count(*) INTO v_integracoes FROM desenvolvimento.hu_caixa_integracao_sap;
    SELECT count(*) INTO v_etiquetas FROM desenvolvimento.hu_caixa_etiqueta;

    IF v_hu <> 0 OR v_pesagens <> 0 OR v_integracoes <> 0 OR v_etiquetas <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA MIGRACAO_LEGADA_MANUAL_NECESSARIA hu_caixa=% pesagens=% integracoes=% etiquetas=%',
            v_hu,
            v_pesagens,
            v_integracoes,
            v_etiquetas;
    END IF;

    SELECT count(*)
      INTO v_existing_table_privileges
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

    SELECT count(*)
      INTO v_existing_sequence_privileges
      FROM (VALUES
          (pg_get_serial_sequence('desenvolvimento.hu_caixa', 'codigo_hu_caixa')),
          (pg_get_serial_sequence('desenvolvimento.hu_caixa_pesagem', 'codigo_hu_caixa_pesagem')),
          (pg_get_serial_sequence('desenvolvimento.hu_caixa_integracao_sap', 'codigo_hu_caixa_integracao_sap'))
      ) AS s(sequence_name)
      CROSS JOIN (VALUES ('USAGE'), ('SELECT'), ('UPDATE')) AS p(privilegio)
     WHERE s.sequence_name IS NOT NULL
       AND has_sequence_privilege('fugapet_dev_app', s.sequence_name, p.privilegio);

    IF v_existing_table_privileges <> 0 OR v_existing_sequence_privileges <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA MATRIZ_GRANTS_ANTERIOR_NAO_E_VAZIA table_privileges=% sequence_privileges=%; rollback_exato_exige_revisao_controlada',
            v_existing_table_privileges,
            v_existing_sequence_privileges;
    END IF;

    -- O estado parcial foi calculado integralmente antes do \if.
    v_partial_columns := 0;
    v_partial_objects := 0;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_namespace n ON n.oid = c.connamespace
        WHERE n.nspname = 'desenvolvimento'
          AND c.conrelid = 'desenvolvimento.hu_caixa'::regclass
          AND c.conname NOT IN (
              'hu_caixa_pkey',
              'hu_caixa_codigo_sap_ordem_producao_fkey',
              'hu_caixa_codigo_sap_produto_fkey',
              'hu_caixa_codigo_produto_referencia_fkey',
              'hu_caixa_codigo_balanca_fkey',
              'hu_caixa_codigo_usuario_fkey',
              'fk_hu_caixa_criado_por',
              'fk_hu_caixa_atualizado_por',
              'ck_hu_caixa_hu_nao_vazia',
              'ck_hu_caixa_numero_caixa',
              'ck_hu_caixa_peso_bruto',
              'ck_hu_caixa_peso_tara',
              'ck_hu_caixa_pesos',
              'ck_hu_caixa_status',
              'ck_hu_caixa_unidade_peso_kg'
          )
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA CATALOGO_HU_CAIXA_DIVERGENTE_DO_BASELINE';
    END IF;

    RAISE NOTICE 'PROPOSTA 044 PRECHECK OK: ambiente, executor, baseline e ausencia de dados confirmados.';
END
$gaia$;

LOCK TABLE desenvolvimento.hu_caixa IN ACCESS EXCLUSIVE MODE;
LOCK TABLE desenvolvimento.hu_caixa_pesagem IN ACCESS EXCLUSIVE MODE;
LOCK TABLE desenvolvimento.hu_caixa_integracao_sap IN ACCESS EXCLUSIVE MODE;
LOCK TABLE desenvolvimento.hu_caixa_etiqueta IN ACCESS EXCLUSIVE MODE;

-- =====================================================================
-- 0. SNAPSHOT CONTROLADO DA VIEW DEPENDENTE (SEM DROP CASCADE)
-- =====================================================================
CREATE TEMP TABLE gaia_044_view_snapshot (
    view_definition text NOT NULL,
    view_owner name NOT NULL,
    view_comment text,
    columns_json jsonb NOT NULL
) ON COMMIT DROP;

CREATE TEMP TABLE gaia_044_view_grants (
    ordem integer GENERATED ALWAYS AS IDENTITY,
    grant_sql text NOT NULL
) ON COMMIT DROP;

INSERT INTO gaia_044_view_snapshot(view_definition,view_owner,view_comment,columns_json)
SELECT
    pg_get_viewdef(c.oid,true),
    pg_get_userbyid(c.relowner),
    obj_description(c.oid,'pg_class'),
    (
      SELECT jsonb_agg(jsonb_build_object(
          'ordinal_position',ic.ordinal_position,
          'column_name',ic.column_name,
          'data_type',ic.data_type,
          'udt_name',ic.udt_name,
          'character_maximum_length',ic.character_maximum_length,
          'numeric_precision',ic.numeric_precision,
          'numeric_scale',ic.numeric_scale
      ) ORDER BY ic.ordinal_position)
      FROM information_schema.columns ic
      WHERE ic.table_schema='desenvolvimento'
        AND ic.table_name='vw_hu_caixas_sem_palete'
    )
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='desenvolvimento'
  AND c.relname='vw_hu_caixas_sem_palete'
  AND c.relkind='v';

DO $gaia$
DECLARE v_count integer; v_dependentes integer; v_funcoes_dependentes integer;
BEGIN
    SELECT count(*) INTO v_count FROM gaia_044_view_snapshot;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA_VIEW_VW_HU_CAIXAS_SEM_PALETE_AUSENTE_OU_AMBIGUA';
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
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA_VIEW_COM_OBJETOS_DEPENDENTES views=% funcoes=%; DROP_CASCADE_PROIBIDO',
          v_dependentes,v_funcoes_dependentes;
    END IF;
END
$gaia$;

DO $gaia$
DECLARE
    v_column_acl integer;
    v_column_comments integer;
    v_extra_rules integer;
    v_view_triggers integer;
BEGIN
    SELECT count(*) INTO v_column_acl
      FROM pg_attribute a
     WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND a.attnum>0 AND NOT a.attisdropped AND a.attacl IS NOT NULL;
    SELECT count(*) INTO v_column_comments
      FROM pg_attribute a
     WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND a.attnum>0 AND NOT a.attisdropped
       AND col_description(a.attrelid,a.attnum) IS NOT NULL;
    SELECT count(*) INTO v_extra_rules
      FROM pg_rewrite r
     WHERE r.ev_class='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND r.rulename<>'_RETURN';
    SELECT count(*) INTO v_view_triggers
      FROM pg_trigger t
     WHERE t.tgrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND NOT t.tgisinternal;
    IF v_column_acl<>0 OR v_column_comments<>0 OR v_extra_rules<>0 OR v_view_triggers<>0 THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA_VIEW_COM_METADATA_NAO_PRESERVADO column_acl=% column_comments=% extra_rules=% triggers=%',
          v_column_acl,v_column_comments,v_extra_rules,v_view_triggers;
    END IF;
END
$gaia$;

INSERT INTO gaia_044_view_grants(grant_sql)
SELECT format(
    'GRANT %s ON TABLE desenvolvimento.vw_hu_caixas_sem_palete TO %s%s',
    a.privilege_type,
    CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE quote_ident(pg_get_userbyid(a.grantee)) END,
    CASE WHEN a.is_grantable THEN ' WITH GRANT OPTION' ELSE '' END
)
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
JOIN LATERAL aclexplode(c.relacl) a ON true
WHERE n.nspname='desenvolvimento'
  AND c.relname='vw_hu_caixas_sem_palete'
  AND a.privilege_type<>'TRUNCATE'
ORDER BY a.grantee,a.privilege_type,a.is_grantable;

DROP VIEW desenvolvimento.vw_hu_caixas_sem_palete;

-- =====================================================================
-- 1. EVOLUCAO DA CAIXA PRINCIPAL
-- =====================================================================

ALTER TABLE desenvolvimento.hu_caixa
    ADD COLUMN numero_ordem_producao varchar(20),
    ADD COLUMN item_ordem_producao varchar(10),
    ADD COLUMN codigo_caixa_local varchar(80),
    ADD COLUMN correlation_id uuid,
    ADD COLUMN lote varchar(10),
    ADD COLUMN centro varchar(4),
    ADD COLUMN deposito varchar(4),
    ADD COLUMN material_embalagem varchar(18),
    ADD COLUMN origem_material_embalagem varchar(30),
    ADD COLUMN quantidade numeric(15,3),
    ADD COLUMN unidade_quantidade varchar(3),
    ADD COLUMN origem_pesagem varchar(30),
    ADD COLUMN terminal varchar(120),
    ADD COLUMN endpoint_sanitizado text,
    ADD COLUMN handling_unit_external_id varchar(20),
    ADD COLUMN warehouse varchar(4) NOT NULL DEFAULT '',
    ADD COLUMN odata_etag text,
    ADD COLUMN created_by_user_sap varchar(40),
    ADD COLUMN creation_datetime_sap timestamptz,
    ADD COLUMN http_status integer,
    ADD COLUMN request_json_sanitizado jsonb,
    ADD COLUMN response_json_sanitizado jsonb,
    ADD COLUMN sap_messages_sanitizadas jsonb,
    ADD COLUMN erro_sanitizado text,
    ADD COLUMN claim_em timestamptz,
    ADD COLUMN claim_token uuid,
    ADD COLUMN tentativa_iniciada_em timestamptz,
    ADD COLUMN enviado_sap_em timestamptz,
    ADD COLUMN confirmado_sap_em timestamptz,
    ADD COLUMN reconciliado_em timestamptz,
    ADD COLUMN tentativas integer NOT NULL DEFAULT 0,
    ADD COLUMN autorizado_envio_em timestamptz,
    ADD COLUMN autorizado_envio_por bigint REFERENCES desenvolvimento.usuario(codigo_usuario) ON DELETE RESTRICT,
    ADD COLUMN terminal_autorizacao varchar(120),
    ADD COLUMN cancelado_em timestamptz,
    ADD COLUMN cancelado_por bigint REFERENCES desenvolvimento.usuario(codigo_usuario) ON DELETE SET NULL,
    ADD COLUMN motivo_cancelamento text,
    ADD COLUMN reprocessamento_liberado_em timestamptz,
    ADD COLUMN reprocessamento_liberado_por bigint REFERENCES desenvolvimento.usuario(codigo_usuario) ON DELETE SET NULL,
    ADD COLUMN motivo_reprocessamento text,
    ADD COLUMN criado_em timestamptz NOT NULL DEFAULT clock_timestamp(),
    ADD COLUMN atualizado_em timestamptz NOT NULL DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa
    ALTER COLUMN material TYPE varchar(18) USING material::varchar(18),
    ALTER COLUMN peso_bruto TYPE numeric(15,3) USING peso_bruto::numeric(15,3),
    ALTER COLUMN peso_liquido TYPE numeric(15,3) USING peso_liquido::numeric(15,3),
    ALTER COLUMN peso_tara TYPE numeric(15,3) USING peso_tara::numeric(15,3),
    ALTER COLUMN unidade_peso TYPE varchar(3) USING unidade_peso::varchar(3),
    ALTER COLUMN status_hu_caixa SET DEFAULT 'EM_PESAGEM',
    ALTER COLUMN hu_caixa_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa
    ALTER COLUMN numero_ordem_producao SET NOT NULL,
    ALTER COLUMN item_ordem_producao SET NOT NULL,
    ALTER COLUMN codigo_caixa_local SET NOT NULL,
    ALTER COLUMN correlation_id SET NOT NULL,
    ALTER COLUMN material SET NOT NULL,
    ALTER COLUMN lote SET NOT NULL,
    ALTER COLUMN centro SET NOT NULL,
    ALTER COLUMN deposito SET NOT NULL,
    ALTER COLUMN material_embalagem SET NOT NULL,
    ALTER COLUMN origem_material_embalagem SET NOT NULL,
    ALTER COLUMN numero_caixa SET NOT NULL,
    ALTER COLUMN peso_bruto SET NOT NULL,
    ALTER COLUMN peso_liquido SET NOT NULL,
    ALTER COLUMN peso_tara SET NOT NULL,
    ALTER COLUMN quantidade SET NOT NULL,
    ALTER COLUMN unidade_quantidade SET NOT NULL,
    ALTER COLUMN origem_pesagem SET NOT NULL,
    ALTER COLUMN terminal SET NOT NULL,
    ALTER COLUMN codigo_usuario SET NOT NULL;

ALTER TABLE desenvolvimento.hu_caixa
    DROP CONSTRAINT ck_hu_caixa_numero_caixa,
    DROP CONSTRAINT ck_hu_caixa_peso_bruto,
    DROP CONSTRAINT ck_hu_caixa_peso_tara,
    DROP CONSTRAINT ck_hu_caixa_pesos,
    DROP CONSTRAINT ck_hu_caixa_status,
    DROP CONSTRAINT ck_hu_caixa_unidade_peso_kg;

ALTER TABLE desenvolvimento.hu_caixa
    ADD CONSTRAINT ck_hu_caixa_numero_ordem_producao
        CHECK (char_length(btrim(numero_ordem_producao)) BETWEEN 1 AND 20),
    ADD CONSTRAINT ck_hu_caixa_item_ordem_producao
        CHECK (char_length(btrim(item_ordem_producao)) BETWEEN 1 AND 10),
    ADD CONSTRAINT ck_hu_caixa_codigo_local_formato
        CHECK (
            codigo_caixa_local = hu_caixa
            AND codigo_caixa_local =
                'CX-' || numero_ordem_producao || '-' || lpad(numero_caixa::text, 4, '0')
        ),
    ADD CONSTRAINT ck_hu_caixa_numero_caixa
        CHECK (numero_caixa BETWEEN 1 AND 9999),
    ADD CONSTRAINT ck_hu_caixa_material
        CHECK (char_length(btrim(material)) BETWEEN 1 AND 18),
    ADD CONSTRAINT ck_hu_caixa_lote
        CHECK (char_length(btrim(lote)) BETWEEN 1 AND 10),
    ADD CONSTRAINT ck_hu_caixa_centro
        CHECK (char_length(btrim(centro)) BETWEEN 1 AND 4),
    ADD CONSTRAINT ck_hu_caixa_deposito
        CHECK (char_length(btrim(deposito)) BETWEEN 1 AND 4),
    ADD CONSTRAINT ck_hu_caixa_material_embalagem
        CHECK (char_length(btrim(material_embalagem)) BETWEEN 1 AND 18),
    ADD CONSTRAINT ck_hu_caixa_origem_material_embalagem
        CHECK (char_length(btrim(origem_material_embalagem)) BETWEEN 1 AND 30),
    ADD CONSTRAINT ck_hu_caixa_peso_bruto
        CHECK (peso_bruto > 0),
    ADD CONSTRAINT ck_hu_caixa_peso_tara
        CHECK (peso_tara >= 0),
    ADD CONSTRAINT ck_hu_caixa_peso_liquido
        CHECK (peso_liquido > 0),
    ADD CONSTRAINT ck_hu_caixa_equacao_pesagem
        CHECK (peso_bruto = peso_liquido + peso_tara),
    ADD CONSTRAINT ck_hu_caixa_quantidade
        CHECK (quantidade > 0),
    ADD CONSTRAINT ck_hu_caixa_unidade_peso_kg
        CHECK (unidade_peso = 'KG'),
    ADD CONSTRAINT ck_hu_caixa_unidade_quantidade
        CHECK (char_length(btrim(unidade_quantidade)) BETWEEN 1 AND 3),
    ADD CONSTRAINT ck_hu_caixa_origem_pesagem
        CHECK (origem_pesagem IN ('BALANCA', 'MANUAL')),
    ADD CONSTRAINT ck_hu_caixa_balanca_coerente
        CHECK (
            (origem_pesagem='BALANCA' AND codigo_balanca IS NOT NULL)
            OR (origem_pesagem='MANUAL' AND codigo_balanca IS NULL)
        ),
    ADD CONSTRAINT ck_hu_caixa_terminal
        CHECK (char_length(btrim(terminal)) BETWEEN 1 AND 120),
    ADD CONSTRAINT ck_hu_caixa_warehouse
        CHECK (char_length(warehouse) <= 4),
    ADD CONSTRAINT ck_hu_caixa_handling_unit_external_id
        CHECK (
            handling_unit_external_id IS NULL
            OR char_length(btrim(handling_unit_external_id)) BETWEEN 1 AND 20
        ),
    ADD CONSTRAINT ck_hu_caixa_http_status
        CHECK (http_status IS NULL OR http_status BETWEEN 100 AND 599),
    ADD CONSTRAINT ck_hu_caixa_tentativas
        CHECK (tentativas >= 0),
    ADD CONSTRAINT ck_hu_caixa_claim_token
        CHECK (
            status_hu_caixa NOT IN ('ENVIANDO_SAP','CONFIRMADA_SAP','ERRO_SAP','INDETERMINADO_TIMEOUT')
            OR claim_token IS NOT NULL
        ),
    ADD CONSTRAINT ck_hu_caixa_autorizacao_envio
        CHECK (
            status_hu_caixa NOT IN ('PRONTA_PARA_ENVIO','ENVIANDO_SAP','CONFIRMADA_SAP','ERRO_SAP','INDETERMINADO_TIMEOUT')
            OR (
                autorizado_envio_em IS NOT NULL
                AND autorizado_envio_por IS NOT NULL
                AND char_length(btrim(COALESCE(terminal_autorizacao,''))) BETWEEN 1 AND 120
            )
        ),
    ADD CONSTRAINT ck_hu_caixa_status
        CHECK (status_hu_caixa IN (
            'EM_PESAGEM',
            'FINALIZADA_LOCAL',
            'PREVIEW_HU_GERADO',
            'AGUARDANDO_AUTORIZACAO_SAP',
            'PRONTA_PARA_ENVIO',
            'ENVIANDO_SAP',
            'CONFIRMADA_SAP',
            'ERRO_SAP',
            'INDETERMINADO_TIMEOUT',
            'BLOQUEADA',
            'CANCELADA'
        )),
    ADD CONSTRAINT ck_hu_caixa_json_objeto
        CHECK (
            (request_json_sanitizado IS NULL OR jsonb_typeof(request_json_sanitizado) = 'object')
            AND (response_json_sanitizado IS NULL OR jsonb_typeof(response_json_sanitizado) = 'object')
            AND (sap_messages_sanitizadas IS NULL OR jsonb_typeof(sap_messages_sanitizadas) = 'object')
        ),
    ADD CONSTRAINT ck_hu_caixa_json_sem_segredos
        CHECK (
            COALESCE(request_json_sanitizado::text, '') !~* '"(authorization|cookie|x-csrf-token|password|senha|client_secret|access_token)"[[:space:]]*:'
            AND COALESCE(response_json_sanitizado::text, '') !~* '"(authorization|cookie|x-csrf-token|password|senha|client_secret|access_token)"[[:space:]]*:'
            AND COALESCE(sap_messages_sanitizadas::text, '') !~* '"(authorization|cookie|x-csrf-token|password|senha|client_secret|access_token)"[[:space:]]*:'
        ),
    ADD CONSTRAINT ck_hu_caixa_confirmacao_sap
        CHECK (
            status_hu_caixa <> 'CONFIRMADA_SAP'
            OR (
                handling_unit_external_id IS NOT NULL
                AND confirmado_sap_em IS NOT NULL
                AND http_status IN (200, 201)
            )
        ),
    ADD CONSTRAINT ck_hu_caixa_envio_sap
        CHECK (
            status_hu_caixa NOT IN ('ENVIANDO_SAP', 'CONFIRMADA_SAP', 'ERRO_SAP', 'INDETERMINADO_TIMEOUT')
            OR (
                claim_em IS NOT NULL
                AND tentativa_iniciada_em IS NOT NULL
                AND enviado_sap_em IS NOT NULL
                AND tentativas > 0
            )
        ),
    ADD CONSTRAINT ck_hu_caixa_erro_sap
        CHECK (
            status_hu_caixa <> 'ERRO_SAP'
            OR char_length(btrim(COALESCE(erro_sanitizado, ''))) > 0
        ),
    ADD CONSTRAINT ck_hu_caixa_timeout_indeterminado
        CHECK (
            status_hu_caixa <> 'INDETERMINADO_TIMEOUT'
            OR char_length(btrim(COALESCE(erro_sanitizado, ''))) > 0
        ),
    ADD CONSTRAINT ck_hu_caixa_cancelamento
        CHECK (
            status_hu_caixa <> 'CANCELADA'
            OR (
                cancelado_em IS NOT NULL
                AND cancelado_por IS NOT NULL
                AND char_length(btrim(COALESCE(motivo_cancelamento, ''))) > 0
            )
        ),
    ADD CONSTRAINT ck_hu_caixa_reprocessamento
        CHECK (
            reprocessamento_liberado_em IS NULL
            OR (
                reprocessamento_liberado_por IS NOT NULL
                AND char_length(btrim(COALESCE(motivo_reprocessamento, ''))) > 0
            )
        ),
    ADD CONSTRAINT ck_hu_caixa_legado_sincronizado
        CHECK (
            payload_criacao_sap IS NOT DISTINCT FROM request_json_sanitizado
            AND response_criacao_sap IS NOT DISTINCT FROM response_json_sanitizado
            AND criada_sap_em IS NOT DISTINCT FROM confirmado_sap_em
        );

COMMENT ON TABLE desenvolvimento.hu_caixa IS
'FugaPET incremental 044: caixa individual de Produto Acabado e sua HU SAP standalone; estruturas de palete permanecem fora do escopo.';
COMMENT ON COLUMN desenvolvimento.hu_caixa.hu_caixa IS
'Alias historico preservado; deve ser identico a codigo_caixa_local.';
COMMENT ON COLUMN desenvolvimento.hu_caixa.peso_tara IS
'Tara local da caixa em KG; nome historico preservado para compatibilidade.';
COMMENT ON COLUMN desenvolvimento.hu_caixa.status_hu_caixa IS
'Fonte unica do status de integracao da caixa no incremental 044.';
COMMENT ON COLUMN desenvolvimento.hu_caixa.handling_unit_external_id IS
'Identificador externo da HU retornado pelo SAP; nullable antes da confirmacao.';
COMMENT ON COLUMN desenvolvimento.hu_caixa.warehouse IS
'Parte da chave OData da HU; string vazia e valida fora de EWM.';


-- Recriacao da view com a definicao exata capturada antes do ALTER COLUMN TYPE.
DO $gaia$
DECLARE
    v_definition text;
    v_owner name;
    v_comment text;
    v_sql text;
    v_columns_before jsonb;
    v_columns_after jsonb;
BEGIN
    SELECT view_definition,view_owner,view_comment,columns_json
      INTO v_definition,v_owner,v_comment,v_columns_before
      FROM gaia_044_view_snapshot;

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

    FOR v_sql IN SELECT grant_sql FROM gaia_044_view_grants ORDER BY ordem LOOP
        EXECUTE v_sql;
    END LOOP;

    SELECT jsonb_agg(jsonb_build_object(
        'ordinal_position',ic.ordinal_position,
        'column_name',ic.column_name,
        'data_type',ic.data_type,
        'udt_name',ic.udt_name,
        'character_maximum_length',ic.character_maximum_length,
        'numeric_precision',ic.numeric_precision,
        'numeric_scale',ic.numeric_scale
    ) ORDER BY ic.ordinal_position)
      INTO v_columns_after
      FROM information_schema.columns ic
     WHERE ic.table_schema='desenvolvimento'
       AND ic.table_name='vw_hu_caixas_sem_palete';

    IF v_columns_after IS NULL THEN
        RAISE EXCEPTION 'VIEW_VW_HU_CAIXAS_SEM_PALETE_NAO_RECRIADA';
    END IF;

    -- Nomes e ordem devem permanecer; tipos podem refletir os ALTER TYPE controlados.
    IF (SELECT jsonb_agg(jsonb_build_object(
            'ordinal_position',x->>'ordinal_position',
            'column_name',x->>'column_name'
        ) ORDER BY (x->>'ordinal_position')::integer)
        FROM jsonb_array_elements(v_columns_before) x)
       IS DISTINCT FROM
       (SELECT jsonb_agg(jsonb_build_object(
            'ordinal_position',x->>'ordinal_position',
            'column_name',x->>'column_name'
        ) ORDER BY (x->>'ordinal_position')::integer)
        FROM jsonb_array_elements(v_columns_after) x) THEN
        RAISE EXCEPTION 'VIEW_VW_HU_CAIXAS_SEM_PALETE_COLUNAS_OU_ORDEM_DIVERGENTES';
    END IF;

    PERFORM * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;
END
$gaia$;

-- =====================================================================
-- 2. PESAGENS: PRESERVACAO E CORRECAO DA EQUACAO
-- =====================================================================

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ADD COLUMN origem_pesagem varchar(30);

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ALTER COLUMN codigo_balanca DROP NOT NULL,
    ALTER COLUMN peso_lido TYPE numeric(15,3) USING peso_lido::numeric(15,3),
    ALTER COLUMN peso_bruto TYPE numeric(15,3) USING peso_bruto::numeric(15,3),
    ALTER COLUMN peso_liquido TYPE numeric(15,3) USING peso_liquido::numeric(15,3),
    ALTER COLUMN peso_tara TYPE numeric(15,3) USING peso_tara::numeric(15,3),
    ALTER COLUMN unidade_peso TYPE varchar(3) USING unidade_peso::varchar(3),
    ALTER COLUMN pesado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN hu_caixa_pesagem_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ALTER COLUMN origem_pesagem SET NOT NULL,
    ALTER COLUMN peso_bruto SET NOT NULL,
    ALTER COLUMN peso_liquido SET NOT NULL,
    ALTER COLUMN peso_tara SET NOT NULL,
    ALTER COLUMN codigo_usuario SET NOT NULL,
    DROP CONSTRAINT ck_hu_caixa_pesagem_pesos;

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ADD CONSTRAINT ck_hu_caixa_pesagem_origem
        CHECK (origem_pesagem IN ('BALANCA','MANUAL')),
    ADD CONSTRAINT ck_hu_caixa_pesagem_origem_balanca
        CHECK (
            (origem_pesagem='BALANCA' AND codigo_balanca IS NOT NULL)
            OR (origem_pesagem='MANUAL' AND codigo_balanca IS NULL)
        ),
    ADD CONSTRAINT ck_hu_caixa_pesagem_equacao
        CHECK (
            peso_bruto > 0
            AND peso_liquido > 0
            AND peso_tara >= 0
            AND peso_bruto = peso_liquido + peso_tara
        );

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    DROP CONSTRAINT hu_caixa_pesagem_codigo_hu_caixa_fkey,
    ADD CONSTRAINT fk_hu_caixa_pesagem_caixa
        FOREIGN KEY (codigo_hu_caixa)
        REFERENCES desenvolvimento.hu_caixa(codigo_hu_caixa)
        ON DELETE RESTRICT;

COMMENT ON TABLE desenvolvimento.hu_caixa_pesagem IS
'FugaPET incremental 044 REV9: historico auditavel da leitura de peso. peso_lido preserva o valor bruto capturado pela balanca ou informado manualmente; peso_bruto, peso_liquido, peso_tara e codigo_usuario sao obrigatorios.';
COMMENT ON COLUMN desenvolvimento.hu_caixa_pesagem.origem_pesagem IS
'BALANCA exige codigo_balanca; MANUAL exige codigo_balanca nulo.';

-- =====================================================================
-- 3. HISTORICO IMUTAVEL DE INTEGRACAO SAP
-- =====================================================================

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ADD COLUMN correlation_id uuid,
    ADD COLUMN tipo_operacao varchar(40),
    ADD COLUMN endpoint_sanitizado text,
    ADD COLUMN numero_tentativa integer,
    ADD COLUMN claim_token uuid,
    ADD COLUMN request_json_sanitizado jsonb,
    ADD COLUMN response_json_sanitizado jsonb,
    ADD COLUMN http_status integer,
    ADD COLUMN resultado varchar(40),
    ADD COLUMN handling_unit_external_id varchar(20),
    ADD COLUMN warehouse varchar(4) NOT NULL DEFAULT '',
    ADD COLUMN odata_etag text,
    ADD COLUMN sap_messages_sanitizadas jsonb,
    ADD COLUMN mensagem_erro_sanitizada text,
    ADD COLUMN iniciado_em timestamptz,
    ADD COLUMN finalizado_em timestamptz,
    ADD COLUMN terminal varchar(120),
    ADD COLUMN criado_em timestamptz NOT NULL DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ALTER COLUMN metodo_http DROP DEFAULT,
    ALTER COLUMN processado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN hu_caixa_integracao_sap_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ALTER COLUMN correlation_id SET NOT NULL,
    ALTER COLUMN tipo_operacao SET NOT NULL,
    ALTER COLUMN endpoint_sanitizado SET NOT NULL,
    ALTER COLUMN numero_tentativa SET NOT NULL,
    ALTER COLUMN resultado SET NOT NULL,
    ALTER COLUMN iniciado_em SET NOT NULL,
    ALTER COLUMN codigo_usuario SET NOT NULL,
    ALTER COLUMN terminal SET NOT NULL;

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    DROP CONSTRAINT ck_hu_caixa_integracao_sap_tentativa,
    DROP CONSTRAINT hu_caixa_integracao_sap_codigo_hu_caixa_fkey;

ALTER TABLE desenvolvimento.hu_caixa
    ADD CONSTRAINT uq_hu_caixa_codigo_correlation
        UNIQUE (codigo_hu_caixa, correlation_id);

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ADD CONSTRAINT fk_hu_caixa_integracao_identidade
        FOREIGN KEY (codigo_hu_caixa, correlation_id)
        REFERENCES desenvolvimento.hu_caixa(codigo_hu_caixa, correlation_id)
        ON DELETE RESTRICT,
    ADD CONSTRAINT ck_hu_caixa_integracao_tipo_operacao
        CHECK (tipo_operacao IN (
            'FINALIZACAO_LOCAL',
            'PREVIEW',
            'AGUARDAR_AUTORIZACAO',
            'AUTORIZACAO_ENVIO',
            'POST_CRIACAO',
            'GET_RECONCILIACAO',
            'BLOQUEIO_CONFIGURACAO',
            'LIBERACAO_REPROCESSAMENTO',
            'CANCELAMENTO_LOCAL'
        )),
    ADD CONSTRAINT ck_hu_caixa_integracao_metodo
        CHECK (metodo_http IN ('GET', 'POST', 'LOCAL')),
    ADD CONSTRAINT ck_hu_caixa_integracao_numero_tentativa
        CHECK (numero_tentativa >= 0),
    ADD CONSTRAINT ck_hu_caixa_integracao_resultado
        CHECK (resultado IN (
            'FINALIZADO',
            'PREVIEW_GERADO',
            'AGUARDANDO_AUTORIZACAO',
            'AUTORIZADO',
            'INICIADO',
            'CONFIRMADO',
            'ERRO_DEFINITIVO',
            'NAO_ENCONTRADO',
            'INDETERMINADO_TIMEOUT',
            'BLOQUEADO_CONFIGURACAO',
            'NAO_AUTORIZADO',
            'CANCELADO',
            'LIBERADO_REPROCESSAMENTO'
        )),
    ADD CONSTRAINT ck_hu_caixa_integracao_warehouse
        CHECK (char_length(warehouse) <= 4),
    ADD CONSTRAINT ck_hu_caixa_integracao_http_status
        CHECK (http_status IS NULL OR http_status BETWEEN 100 AND 599),
    ADD CONSTRAINT ck_hu_caixa_integracao_hu
        CHECK (
            handling_unit_external_id IS NULL
            OR char_length(btrim(handling_unit_external_id)) BETWEEN 1 AND 20
        ),
    ADD CONSTRAINT ck_hu_caixa_integracao_terminal
        CHECK (char_length(btrim(terminal)) BETWEEN 1 AND 120),
    ADD CONSTRAINT ck_hu_caixa_integracao_json_objeto
        CHECK (
            (request_json_sanitizado IS NULL OR jsonb_typeof(request_json_sanitizado) = 'object')
            AND (response_json_sanitizado IS NULL OR jsonb_typeof(response_json_sanitizado) = 'object')
            AND (sap_messages_sanitizadas IS NULL OR jsonb_typeof(sap_messages_sanitizadas) = 'object')
        ),
    ADD CONSTRAINT ck_hu_caixa_integracao_json_sem_segredos
        CHECK (
            COALESCE(request_json_sanitizado::text, '') !~* '"(authorization|cookie|x-csrf-token|password|senha|client_secret|access_token)"[[:space:]]*:'
            AND COALESCE(response_json_sanitizado::text, '') !~* '"(authorization|cookie|x-csrf-token|password|senha|client_secret|access_token)"[[:space:]]*:'
            AND COALESCE(sap_messages_sanitizadas::text, '') !~* '"(authorization|cookie|x-csrf-token|password|senha|client_secret|access_token)"[[:space:]]*:'
        ),
    ADD CONSTRAINT ck_hu_caixa_integracao_periodo
        CHECK (finalizado_em IS NULL OR finalizado_em >= iniciado_em),
    ADD CONSTRAINT ck_hu_caixa_integracao_operacao_metodo
        CHECK (
            (tipo_operacao IN ('POST_CRIACAO') AND metodo_http = 'POST')
            OR (tipo_operacao = 'GET_RECONCILIACAO' AND metodo_http = 'GET')
            OR (tipo_operacao IN ('FINALIZACAO_LOCAL','PREVIEW','AGUARDAR_AUTORIZACAO','AUTORIZACAO_ENVIO','BLOQUEIO_CONFIGURACAO','LIBERACAO_REPROCESSAMENTO','CANCELAMENTO_LOCAL') AND metodo_http = 'LOCAL')
        ),
    ADD CONSTRAINT ck_hu_caixa_integracao_reprocessamento
        CHECK (NOT pode_reprocessar OR resultado = 'ERRO_DEFINITIVO'),
    ADD CONSTRAINT ck_hu_caixa_integracao_finalizacao
        CHECK (finalizado_em IS NOT NULL AND finalizado_em >= iniciado_em),
    ADD CONSTRAINT ck_hu_caixa_integracao_claim
        CHECK (
            (tipo_operacao IN ('POST_CRIACAO','GET_RECONCILIACAO') AND numero_tentativa > 0 AND claim_token IS NOT NULL)
            OR (tipo_operacao NOT IN ('POST_CRIACAO','GET_RECONCILIACAO') AND numero_tentativa >= 0 AND claim_token IS NULL)
        ),
    ADD CONSTRAINT ck_hu_caixa_integracao_legado_sincronizado
        CHECK (
            endpoint IS NOT DISTINCT FROM endpoint_sanitizado
            AND payload_request IS NOT DISTINCT FROM request_json_sanitizado
            AND payload_response IS NOT DISTINCT FROM response_json_sanitizado
            AND status_http IS NOT DISTINCT FROM http_status
            AND hu_gerada IS NOT DISTINCT FROM handling_unit_external_id
            AND mensagem_erro IS NOT DISTINCT FROM mensagem_erro_sanitizada
            AND tentativa IS NOT DISTINCT FROM numero_tentativa
            AND processado_em IS NOT DISTINCT FROM iniciado_em
        );

COMMENT ON TABLE desenvolvimento.hu_caixa_integracao_sap IS
'FugaPET incremental 044: historico imutavel das operacoes SAP e locais da caixa; DELETE e UPDATE sao proibidos para a aplicacao.';

-- =====================================================================
-- 4. FUNCOES E TRIGGERS DE IDENTIDADE, STATUS E HISTORICO
-- =====================================================================

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_preparar_insert()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, desenvolvimento
AS $func$
DECLARE
    v_proximo_numero integer;
BEGIN
    NEW.numero_ordem_producao := btrim(NEW.numero_ordem_producao);
    NEW.item_ordem_producao := btrim(NEW.item_ordem_producao);
    NEW.material := btrim(NEW.material);
    NEW.lote := btrim(NEW.lote);
    NEW.centro := btrim(NEW.centro);
    NEW.deposito := btrim(NEW.deposito);
    NEW.material_embalagem := btrim(NEW.material_embalagem);
    NEW.origem_material_embalagem := upper(btrim(NEW.origem_material_embalagem));
    NEW.unidade_peso := upper(btrim(NEW.unidade_peso));
    NEW.unidade_quantidade := upper(btrim(NEW.unidade_quantidade));
    NEW.origem_pesagem := upper(btrim(NEW.origem_pesagem));
    NEW.terminal := upper(btrim(NEW.terminal));

    IF NEW.numero_caixa IS NOT NULL OR NEW.codigo_caixa_local IS NOT NULL THEN
        RAISE EXCEPTION 'NUMERACAO_CAIXA_DEVE_SER_GERADA_ATOMICAMENTE_PELO_BANCO';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended('FUGAPET_HU_CAIXA|' || NEW.numero_ordem_producao,0));
    SELECT COALESCE(max(numero_caixa),0)+1 INTO v_proximo_numero
      FROM desenvolvimento.hu_caixa
     WHERE numero_ordem_producao=NEW.numero_ordem_producao;

    IF v_proximo_numero > 9999 THEN
        RAISE EXCEPTION 'LIMITE_NUMERACAO_CAIXA_POR_OP_EXCEDIDO op=%',NEW.numero_ordem_producao;
    END IF;

    NEW.numero_caixa := v_proximo_numero;
    NEW.codigo_caixa_local := 'CX-' || NEW.numero_ordem_producao || '-' || lpad(v_proximo_numero::text,4,'0');
    NEW.hu_caixa := NEW.codigo_caixa_local;
    NEW.correlation_id := COALESCE(NEW.correlation_id,gen_random_uuid());
    NEW.status_hu_caixa := 'EM_PESAGEM';
    NEW.warehouse := '';
    NEW.endpoint_sanitizado := NULL;
    NEW.handling_unit_external_id := NULL;
    NEW.odata_etag := NULL;
    NEW.created_by_user_sap := NULL;
    NEW.creation_datetime_sap := NULL;
    NEW.http_status := NULL;
    NEW.request_json_sanitizado := NULL;
    NEW.response_json_sanitizado := NULL;
    NEW.sap_messages_sanitizadas := NULL;
    NEW.erro_sanitizado := NULL;
    NEW.claim_em := NULL;
    NEW.claim_token := NULL;
    NEW.tentativa_iniciada_em := NULL;
    NEW.enviado_sap_em := NULL;
    NEW.confirmado_sap_em := NULL;
    NEW.reconciliado_em := NULL;
    NEW.tentativas := 0;
    NEW.autorizado_envio_em := NULL;
    NEW.autorizado_envio_por := NULL;
    NEW.terminal_autorizacao := NULL;
    NEW.cancelado_em := NULL;
    NEW.cancelado_por := NULL;
    NEW.motivo_cancelamento := NULL;
    NEW.reprocessamento_liberado_em := NULL;
    NEW.reprocessamento_liberado_por := NULL;
    NEW.motivo_reprocessamento := NULL;
    NEW.payload_criacao_sap := NULL;
    NEW.response_criacao_sap := NULL;
    NEW.criada_sap_em := NULL;
    NEW.criado_em := clock_timestamp();
    NEW.atualizado_em := NEW.criado_em;
    NEW.hu_caixa_criado_em := NEW.criado_em;
    RETURN NEW;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_validar_update()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, desenvolvimento
AS $func$
DECLARE
    v_owner name;
    v_transicao_permitida boolean;
BEGIN
    SELECT pg_get_userbyid(c.relowner) INTO v_owner FROM pg_class c WHERE c.oid=TG_RELID;
    IF current_user <> v_owner THEN
        RAISE EXCEPTION 'UPDATE_DIRETO_HU_CAIXA_BLOQUEADO_USE_FUNCOES_CONTROLADAS usuario=%',current_user;
    END IF;

    IF NEW.correlation_id IS DISTINCT FROM OLD.correlation_id
       OR NEW.codigo_caixa_local IS DISTINCT FROM OLD.codigo_caixa_local
       OR NEW.hu_caixa IS DISTINCT FROM OLD.hu_caixa
       OR NEW.numero_ordem_producao IS DISTINCT FROM OLD.numero_ordem_producao
       OR NEW.item_ordem_producao IS DISTINCT FROM OLD.item_ordem_producao
       OR NEW.numero_caixa IS DISTINCT FROM OLD.numero_caixa
       OR NEW.terminal IS DISTINCT FROM OLD.terminal THEN
        RAISE EXCEPTION 'IDENTIDADE_CAIXA_IMUTAVEL codigo_hu_caixa=%',OLD.codigo_hu_caixa;
    END IF;

    IF OLD.status_hu_caixa <> 'EM_PESAGEM' AND (
        NEW.material IS DISTINCT FROM OLD.material OR NEW.lote IS DISTINCT FROM OLD.lote
        OR NEW.centro IS DISTINCT FROM OLD.centro OR NEW.deposito IS DISTINCT FROM OLD.deposito
        OR NEW.material_embalagem IS DISTINCT FROM OLD.material_embalagem
        OR NEW.origem_material_embalagem IS DISTINCT FROM OLD.origem_material_embalagem
        OR NEW.peso_bruto IS DISTINCT FROM OLD.peso_bruto OR NEW.peso_liquido IS DISTINCT FROM OLD.peso_liquido
        OR NEW.peso_tara IS DISTINCT FROM OLD.peso_tara OR NEW.unidade_peso IS DISTINCT FROM OLD.unidade_peso
        OR NEW.quantidade IS DISTINCT FROM OLD.quantidade OR NEW.unidade_quantidade IS DISTINCT FROM OLD.unidade_quantidade
        OR NEW.origem_pesagem IS DISTINCT FROM OLD.origem_pesagem OR NEW.codigo_balanca IS DISTINCT FROM OLD.codigo_balanca
    ) THEN
        RAISE EXCEPTION 'SNAPSHOT_FUNCIONAL_CAIXA_IMUTAVEL_APOS_FINALIZACAO codigo_hu_caixa=%',OLD.codigo_hu_caixa;
    END IF;

    IF OLD.handling_unit_external_id IS NOT NULL
       AND NEW.handling_unit_external_id IS DISTINCT FROM OLD.handling_unit_external_id THEN
        RAISE EXCEPTION 'HANDLING_UNIT_EXTERNAL_ID_IMUTAVEL codigo=%',OLD.codigo_hu_caixa;
    END IF;

    IF OLD.handling_unit_external_id IS NOT NULL
       AND NEW.warehouse IS DISTINCT FROM OLD.warehouse THEN
        RAISE EXCEPTION 'WAREHOUSE_DA_HU_CONFIRMADA_IMUTAVEL codigo=%',OLD.codigo_hu_caixa;
    END IF;

    IF OLD.handling_unit_external_id IS NULL
       AND NEW.handling_unit_external_id IS NOT NULL
       AND NOT (
           (OLD.status_hu_caixa='ENVIANDO_SAP' AND NEW.status_hu_caixa='CONFIRMADA_SAP')
           OR (OLD.status_hu_caixa='INDETERMINADO_TIMEOUT' AND NEW.status_hu_caixa='CONFIRMADA_SAP')
       ) THEN
        RAISE EXCEPTION 'HU_SAP_SO_PODE_SER_PERSISTIDA_NO_RESULTADO_OU_RECONCILIACAO codigo=%',OLD.codigo_hu_caixa;
    END IF;

    IF OLD.autorizado_envio_em IS NOT NULL AND (
        NEW.autorizado_envio_em IS DISTINCT FROM OLD.autorizado_envio_em
        OR NEW.autorizado_envio_por IS DISTINCT FROM OLD.autorizado_envio_por
        OR NEW.terminal_autorizacao IS DISTINCT FROM OLD.terminal_autorizacao
    ) THEN
        RAISE EXCEPTION 'AUTORIZACAO_ENVIO_IMUTAVEL codigo=%',OLD.codigo_hu_caixa;
    END IF;

    IF NEW.status_hu_caixa IS DISTINCT FROM OLD.status_hu_caixa THEN
        v_transicao_permitida := CASE OLD.status_hu_caixa
            WHEN 'EM_PESAGEM' THEN NEW.status_hu_caixa IN ('FINALIZADA_LOCAL','CANCELADA')
            WHEN 'FINALIZADA_LOCAL' THEN NEW.status_hu_caixa IN ('PREVIEW_HU_GERADO','BLOQUEADA','CANCELADA')
            WHEN 'PREVIEW_HU_GERADO' THEN NEW.status_hu_caixa IN ('AGUARDANDO_AUTORIZACAO_SAP','BLOQUEADA','CANCELADA')
            WHEN 'AGUARDANDO_AUTORIZACAO_SAP' THEN NEW.status_hu_caixa IN ('PRONTA_PARA_ENVIO','BLOQUEADA','CANCELADA')
            WHEN 'PRONTA_PARA_ENVIO' THEN NEW.status_hu_caixa IN ('ENVIANDO_SAP','BLOQUEADA','CANCELADA')
            WHEN 'ENVIANDO_SAP' THEN NEW.status_hu_caixa IN ('CONFIRMADA_SAP','ERRO_SAP','INDETERMINADO_TIMEOUT')
            WHEN 'ERRO_SAP' THEN NEW.status_hu_caixa IN ('PRONTA_PARA_ENVIO','CANCELADA')
            WHEN 'INDETERMINADO_TIMEOUT' THEN NEW.status_hu_caixa='CONFIRMADA_SAP'
            WHEN 'BLOQUEADA' THEN NEW.status_hu_caixa='CANCELADA'
            ELSE false END;
        IF NOT COALESCE(v_transicao_permitida,false) THEN
            RAISE EXCEPTION 'TRANSICAO_STATUS_HU_CAIXA_NAO_PERMITIDA codigo=% de=% para=%',OLD.codigo_hu_caixa,OLD.status_hu_caixa,NEW.status_hu_caixa;
        END IF;
    END IF;

    IF OLD.status_hu_caixa='AGUARDANDO_AUTORIZACAO_SAP' AND NEW.status_hu_caixa='PRONTA_PARA_ENVIO' AND (
        NEW.autorizado_envio_em IS NULL OR NEW.autorizado_envio_por IS NULL
        OR char_length(btrim(COALESCE(NEW.terminal_autorizacao,'')))=0
    ) THEN RAISE EXCEPTION 'AUTORIZACAO_ENVIO_INCOMPLETA codigo=%',OLD.codigo_hu_caixa; END IF;

    IF OLD.status_hu_caixa='PRONTA_PARA_ENVIO' AND NEW.status_hu_caixa='ENVIANDO_SAP' AND (
        NEW.claim_token IS NULL OR NEW.claim_token IS NOT DISTINCT FROM OLD.claim_token
        OR NEW.claim_em IS NULL OR NEW.tentativa_iniciada_em IS NULL OR NEW.enviado_sap_em IS NULL
        OR NEW.tentativas <> OLD.tentativas+1
    ) THEN RAISE EXCEPTION 'CLAIM_ATOMICO_INCOMPLETO codigo=%',OLD.codigo_hu_caixa; END IF;

    IF NOT (OLD.status_hu_caixa='PRONTA_PARA_ENVIO' AND NEW.status_hu_caixa='ENVIANDO_SAP')
       AND NEW.tentativas IS DISTINCT FROM OLD.tentativas THEN
        RAISE EXCEPTION 'CONTADOR_TENTATIVAS_SO_PODE_AVANCAR_NO_CLAIM codigo=%',OLD.codigo_hu_caixa;
    END IF;

    IF NEW.claim_token IS DISTINCT FROM OLD.claim_token AND NOT (
        (OLD.status_hu_caixa='PRONTA_PARA_ENVIO' AND NEW.status_hu_caixa='ENVIANDO_SAP')
        OR (OLD.status_hu_caixa='ERRO_SAP' AND NEW.status_hu_caixa='PRONTA_PARA_ENVIO' AND NEW.claim_token IS NULL)
    ) THEN RAISE EXCEPTION 'CLAIM_TOKEN_IMUTAVEL_FORA_DO_CLAIM codigo=%',OLD.codigo_hu_caixa; END IF;

    IF NEW.status_hu_caixa='CONFIRMADA_SAP' AND OLD.status_hu_caixa='INDETERMINADO_TIMEOUT' AND NEW.reconciliado_em IS NULL THEN
        RAISE EXCEPTION 'CONFIRMACAO_APOS_TIMEOUT_EXIGE_RECONCILIACAO codigo=%',OLD.codigo_hu_caixa;
    END IF;

    IF NEW.status_hu_caixa='CANCELADA' AND OLD.status_hu_caixa IN ('ENVIANDO_SAP','CONFIRMADA_SAP','INDETERMINADO_TIMEOUT') THEN
        RAISE EXCEPTION 'CANCELAMENTO_INSEGURO_BLOQUEADO codigo=% status=%',OLD.codigo_hu_caixa,OLD.status_hu_caixa;
    END IF;

    NEW.hu_caixa:=NEW.codigo_caixa_local;
    NEW.payload_criacao_sap:=NEW.request_json_sanitizado;
    NEW.response_criacao_sap:=NEW.response_json_sanitizado;
    NEW.criada_sap_em:=NEW.confirmado_sap_em;
    NEW.atualizado_em:=clock_timestamp();
    NEW.hu_caixa_atualizado_em:=NEW.atualizado_em;
    RETURN NEW;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_integracao_preparar_insert()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,desenvolvimento
AS $func$
BEGIN
    NEW.endpoint_sanitizado:=COALESCE(NULLIF(btrim(NEW.endpoint_sanitizado),''),btrim(NEW.endpoint));
    NEW.request_json_sanitizado:=COALESCE(NEW.request_json_sanitizado,NEW.payload_request);
    NEW.response_json_sanitizado:=COALESCE(NEW.response_json_sanitizado,NEW.payload_response);
    NEW.http_status:=COALESCE(NEW.http_status,NEW.status_http);
    NEW.handling_unit_external_id:=COALESCE(NEW.handling_unit_external_id,NEW.hu_gerada);
    NEW.mensagem_erro_sanitizada:=COALESCE(NEW.mensagem_erro_sanitizada,NEW.mensagem_erro);
    NEW.numero_tentativa:=COALESCE(NEW.numero_tentativa,NEW.tentativa,0);
    NEW.iniciado_em:=COALESCE(NEW.iniciado_em,NEW.processado_em,clock_timestamp());
    NEW.finalizado_em:=COALESCE(NEW.finalizado_em,NEW.iniciado_em);
    NEW.terminal:=upper(btrim(NEW.terminal));
    NEW.warehouse:=btrim(COALESCE(NEW.warehouse,''));
    NEW.criado_em:=clock_timestamp();
    NEW.endpoint:=NEW.endpoint_sanitizado;
    NEW.payload_request:=NEW.request_json_sanitizado;
    NEW.payload_response:=NEW.response_json_sanitizado;
    NEW.status_http:=NEW.http_status;
    NEW.hu_gerada:=NEW.handling_unit_external_id;
    NEW.mensagem_erro:=NEW.mensagem_erro_sanitizada;
    NEW.tentativa:=NEW.numero_tentativa;
    NEW.processado_em:=NEW.iniciado_em;
    NEW.hu_caixa_integracao_sap_criado_em:=NEW.criado_em;
    RETURN NEW;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_integracao_imutavel()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,desenvolvimento
AS $func$
BEGIN
    RAISE EXCEPTION
      'HISTORICO_INTEGRACAO_HU_CAIXA_IMUTAVEL operacao=% codigo=%',
      TG_OP,OLD.codigo_hu_caixa_integracao_sap;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_pesagem_normalizar()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,desenvolvimento
AS $func$
BEGIN
    NEW.origem_pesagem:=upper(btrim(NEW.origem_pesagem));
    NEW.unidade_peso:=upper(btrim(NEW.unidade_peso));
    NEW.pesado_em:=COALESCE(NEW.pesado_em,clock_timestamp());
    NEW.hu_caixa_pesagem_criado_em:=COALESCE(NEW.hu_caixa_pesagem_criado_em,clock_timestamp());
    RETURN NEW;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_inserir_evento(
    p_codigo bigint,
    p_correlation uuid,
    p_tipo varchar,
    p_endpoint text,
    p_metodo varchar,
    p_tentativa integer,
    p_claim uuid,
    p_request jsonb,
    p_response jsonb,
    p_sap_messages jsonb,
    p_http integer,
    p_resultado varchar,
    p_hu varchar,
    p_warehouse varchar,
    p_etag text,
    p_erro text,
    p_reprocessar boolean,
    p_inicio timestamptz,
    p_fim timestamptz,
    p_usuario bigint,
    p_terminal text
) RETURNS bigint
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v_id bigint;
BEGIN
    IF p_codigo < 0 THEN
        SELECT p_codigo*1000000-COALESCE(count(*),0)-1
          INTO v_id
          FROM desenvolvimento.hu_caixa_integracao_sap
         WHERE codigo_hu_caixa=p_codigo;
    ELSE
        v_id:=nextval(pg_get_serial_sequence(
            'desenvolvimento.hu_caixa_integracao_sap',
            'codigo_hu_caixa_integracao_sap'
        ));
    END IF;

    INSERT INTO desenvolvimento.hu_caixa_integracao_sap(
        codigo_hu_caixa_integracao_sap,codigo_hu_caixa,correlation_id,
        tipo_operacao,endpoint_sanitizado,metodo_http,numero_tentativa,
        claim_token,request_json_sanitizado,response_json_sanitizado,
        sap_messages_sanitizadas,http_status,resultado,
        handling_unit_external_id,warehouse,odata_etag,
        mensagem_erro_sanitizada,pode_reprocessar,iniciado_em,finalizado_em,
        codigo_usuario,terminal,endpoint,payload_request,payload_response,
        status_http,hu_gerada,mensagem_erro,tentativa,processado_em
    ) VALUES(
        v_id,p_codigo,p_correlation,p_tipo,p_endpoint,p_metodo,p_tentativa,
        p_claim,p_request,p_response,p_sap_messages,p_http,p_resultado,
        p_hu,COALESCE(p_warehouse,''),p_etag,p_erro,COALESCE(p_reprocessar,false),
        p_inicio,p_fim,p_usuario,upper(btrim(p_terminal)),p_endpoint,p_request,
        p_response,p_http,p_hu,p_erro,p_tentativa,p_inicio
    );
    RETURN v_id;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_finalizar_local(
    p_codigo bigint,p_usuario bigint,p_terminal text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_agora timestamptz:=clock_timestamp();
BEGIN
    IF p_usuario IS NULL OR char_length(btrim(COALESCE(p_terminal,'')))=0 THEN
        RAISE EXCEPTION 'USUARIO_E_TERMINAL_OBRIGATORIOS';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='FINALIZADA_LOCAL'
     WHERE codigo_hu_caixa=p_codigo
       AND codigo_usuario=p_usuario
       AND terminal=upper(btrim(p_terminal))
       AND status_hu_caixa='EM_PESAGEM'
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'FINALIZACAO_LOCAL',
        'LOCAL://produto-acabado/hu-caixa/finalizar','LOCAL',0,NULL,
        NULL,NULL,NULL,NULL,'FINALIZADO',NULL,'',NULL,NULL,false,
        v_agora,v_agora,p_usuario,p_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_salvar_preview(
    p_codigo bigint,p_request jsonb,p_endpoint text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_agora timestamptz:=clock_timestamp();
BEGIN
    IF p_request IS NULL OR jsonb_typeof(p_request)<>'object'
       OR char_length(btrim(COALESCE(p_endpoint,'')))=0 THEN
        RAISE EXCEPTION 'PREVIEW_INVALIDO';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='PREVIEW_HU_GERADO',
           request_json_sanitizado=p_request,
           endpoint_sanitizado=btrim(p_endpoint)
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='FINALIZADA_LOCAL'
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'PREVIEW',v.endpoint_sanitizado,
        'LOCAL',0,NULL,v.request_json_sanitizado,NULL,NULL,NULL,
        'PREVIEW_GERADO',NULL,'',NULL,NULL,false,v_agora,v_agora,
        v.codigo_usuario,v.terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_aguardar_autorizacao(
    p_codigo bigint
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_agora timestamptz:=clock_timestamp();
BEGIN
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='AGUARDANDO_AUTORIZACAO_SAP'
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='PREVIEW_HU_GERADO'
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'AGUARDAR_AUTORIZACAO',
        'LOCAL://produto-acabado/hu-caixa/aguardar-autorizacao','LOCAL',0,NULL,
        v.request_json_sanitizado,NULL,NULL,NULL,'AGUARDANDO_AUTORIZACAO',
        NULL,'',NULL,NULL,false,v_agora,v_agora,v.codigo_usuario,v.terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_autorizar_envio(
    p_codigo bigint,p_usuario bigint,p_terminal text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_agora timestamptz:=clock_timestamp();
BEGIN
    IF p_usuario IS NULL THEN RAISE EXCEPTION 'AUTORIZACAO_EXIGE_USUARIO'; END IF;
    IF NOT EXISTS (
        SELECT 1 FROM desenvolvimento.usuario
         WHERE codigo_usuario=p_usuario AND situacao_usuario AND NOT bloqueado_usuario
    ) THEN
        RAISE EXCEPTION 'AUTORIZACAO_EXIGE_USUARIO_ATIVO';
    END IF;
    IF char_length(btrim(COALESCE(p_terminal,'')))=0 THEN
        RAISE EXCEPTION 'AUTORIZACAO_EXIGE_TERMINAL';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='PRONTA_PARA_ENVIO',
           autorizado_envio_em=v_agora,
           autorizado_envio_por=p_usuario,
           terminal_autorizacao=upper(btrim(p_terminal))
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='AGUARDANDO_AUTORIZACAO_SAP'
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'AUTORIZACAO_ENVIO',
        'LOCAL://produto-acabado/hu-caixa/autorizar-envio','LOCAL',0,NULL,
        v.request_json_sanitizado,NULL,NULL,NULL,'AUTORIZADO',NULL,'',
        NULL,NULL,false,v_agora,v_agora,p_usuario,p_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_claim_envio(
    p_codigo bigint,
    p_usuario bigint,
    p_terminal text
) RETURNS SETOF desenvolvimento.hu_caixa
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE;
BEGIN
    IF p_usuario IS NULL THEN
        RAISE EXCEPTION 'CLAIM_USUARIO_OBRIGATORIO';
    END IF;
    IF char_length(btrim(COALESCE(p_terminal,'')))=0 THEN
        RAISE EXCEPTION 'CLAIM_TERMINAL_OBRIGATORIO';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM desenvolvimento.usuario u
         WHERE u.codigo_usuario=p_usuario
           AND u.situacao_usuario
           AND NOT u.bloqueado_usuario
    ) THEN
        RAISE EXCEPTION 'CLAIM_USUARIO_INATIVO_OU_BLOQUEADO codigo_usuario=%',p_usuario;
    END IF;

    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='ENVIANDO_SAP',
           claim_em=clock_timestamp(),
           claim_token=gen_random_uuid(),
           tentativa_iniciada_em=clock_timestamp(),
           enviado_sap_em=clock_timestamp(),
           tentativas=tentativas+1,
           erro_sanitizado=NULL,
           http_status=NULL,
           response_json_sanitizado=NULL,
           sap_messages_sanitizadas=NULL,
           confirmado_sap_em=NULL,
           reconciliado_em=NULL
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='PRONTA_PARA_ENVIO'
       AND terminal=upper(btrim(p_terminal))
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN; END IF;

    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'POST_CRIACAO',
        COALESCE(v.endpoint_sanitizado,'/HandlingUnit?sap-client=110'),
        'POST',v.tentativas,v.claim_token,v.request_json_sanitizado,
        NULL,NULL,NULL,'INICIADO',NULL,'',NULL,NULL,false,
        v.tentativa_iniciada_em,v.tentativa_iniciada_em,
        p_usuario,p_terminal
    );
    RETURN NEXT v;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_registrar_sucesso(
    p_codigo bigint,p_tentativa integer,p_claim uuid,p_hu varchar,
    p_warehouse varchar,p_http integer,p_response jsonb,p_sap_messages jsonb,
    p_etag text,p_created_by varchar,p_creation timestamptz
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE
    v desenvolvimento.hu_caixa%ROWTYPE;
    v_fim timestamptz:=clock_timestamp();
    v_actor_usuario bigint;
    v_actor_terminal text;
BEGIN
    IF char_length(btrim(COALESCE(p_hu,'')))=0 OR p_http<>201 THEN
        RAISE EXCEPTION 'RESULTADO_SUCESSO_INVALIDO';
    END IF;
    SELECT h.codigo_usuario,h.terminal
      INTO v_actor_usuario,v_actor_terminal
      FROM desenvolvimento.hu_caixa_integracao_sap h
     WHERE h.codigo_hu_caixa=p_codigo AND h.numero_tentativa=p_tentativa
       AND h.claim_token=p_claim AND h.resultado='INICIADO'
     ORDER BY h.criado_em DESC LIMIT 1;
    IF NOT FOUND THEN RAISE EXCEPTION 'CLAIM_INICIADO_NAO_ENCONTRADO'; END IF;

    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='CONFIRMADA_SAP',
           handling_unit_external_id=btrim(p_hu),
           warehouse=btrim(COALESCE(p_warehouse,'')),
           http_status=p_http,
           response_json_sanitizado=p_response,
           sap_messages_sanitizadas=p_sap_messages,
           odata_etag=p_etag,
           created_by_user_sap=p_created_by,
           creation_datetime_sap=p_creation,
           confirmado_sap_em=v_fim,
           erro_sanitizado=NULL
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='ENVIANDO_SAP'
       AND tentativas=p_tentativa
       AND claim_token=p_claim
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'POST_CRIACAO',
        COALESCE(v.endpoint_sanitizado,'/HandlingUnit?sap-client=110'),
        'POST',v.tentativas,v.claim_token,v.request_json_sanitizado,
        v.response_json_sanitizado,v.sap_messages_sanitizadas,v.http_status,
        'CONFIRMADO',v.handling_unit_external_id,v.warehouse,v.odata_etag,
        NULL,false,v.tentativa_iniciada_em,v_fim,v_actor_usuario,v_actor_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_registrar_erro(
    p_codigo bigint,p_tentativa integer,p_claim uuid,p_http integer,
    p_response jsonb,p_sap_messages jsonb,p_erro text,
    p_resultado varchar,p_pode_reprocessar boolean
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE
    v desenvolvimento.hu_caixa%ROWTYPE;
    v_fim timestamptz:=clock_timestamp();
    v_actor_usuario bigint;
    v_actor_terminal text;
BEGIN
    p_resultado:=upper(btrim(COALESCE(p_resultado,'')));
    IF p_resultado NOT IN ('ERRO_DEFINITIVO','NAO_AUTORIZADO') THEN
        RAISE EXCEPTION 'CLASSIFICACAO_ERRO_INVALIDA resultado=%',p_resultado;
    END IF;
    IF char_length(btrim(COALESCE(p_erro,'')))=0 THEN
        RAISE EXCEPTION 'ERRO_SANITIZADO_OBRIGATORIO';
    END IF;
    IF p_http IN (401,403) AND p_resultado <> 'NAO_AUTORIZADO' THEN
        RAISE EXCEPTION 'HTTP_401_403_EXIGE_RESULTADO_NAO_AUTORIZADO';
    END IF;
    IF p_resultado='NAO_AUTORIZADO' AND p_http NOT IN (401,403) THEN
        RAISE EXCEPTION 'NAO_AUTORIZADO_EXIGE_HTTP_401_OU_403';
    END IF;
    IF p_resultado='NAO_AUTORIZADO' AND COALESCE(p_pode_reprocessar,false) THEN
        RAISE EXCEPTION 'NAO_AUTORIZADO_NAO_PODE_REPROCESSAR';
    END IF;
    SELECT h.codigo_usuario,h.terminal
      INTO v_actor_usuario,v_actor_terminal
      FROM desenvolvimento.hu_caixa_integracao_sap h
     WHERE h.codigo_hu_caixa=p_codigo AND h.numero_tentativa=p_tentativa
       AND h.claim_token=p_claim AND h.resultado='INICIADO'
     ORDER BY h.criado_em DESC LIMIT 1;
    IF NOT FOUND THEN RAISE EXCEPTION 'CLAIM_INICIADO_NAO_ENCONTRADO'; END IF;

    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='ERRO_SAP',
           http_status=p_http,
           response_json_sanitizado=p_response,
           sap_messages_sanitizadas=p_sap_messages,
           erro_sanitizado=btrim(p_erro)
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='ENVIANDO_SAP'
       AND tentativas=p_tentativa
       AND claim_token=p_claim
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'POST_CRIACAO',
        COALESCE(v.endpoint_sanitizado,'/HandlingUnit?sap-client=110'),
        'POST',v.tentativas,v.claim_token,v.request_json_sanitizado,
        v.response_json_sanitizado,v.sap_messages_sanitizadas,v.http_status,
        p_resultado,NULL,'',NULL,v.erro_sanitizado,
        CASE WHEN p_resultado='ERRO_DEFINITIVO' THEN COALESCE(p_pode_reprocessar,false) ELSE false END,
        v.tentativa_iniciada_em,v_fim,v_actor_usuario,v_actor_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_registrar_timeout(
    p_codigo bigint,p_tentativa integer,p_claim uuid,
    p_sap_messages jsonb,p_erro text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE
    v desenvolvimento.hu_caixa%ROWTYPE;
    v_fim timestamptz:=clock_timestamp();
    v_actor_usuario bigint;
    v_actor_terminal text;
BEGIN
    IF char_length(btrim(COALESCE(p_erro,'')))=0 THEN
        RAISE EXCEPTION 'TIMEOUT_EXIGE_ERRO_SANITIZADO';
    END IF;
    SELECT h.codigo_usuario,h.terminal
      INTO v_actor_usuario,v_actor_terminal
      FROM desenvolvimento.hu_caixa_integracao_sap h
     WHERE h.codigo_hu_caixa=p_codigo AND h.numero_tentativa=p_tentativa
       AND h.claim_token=p_claim AND h.resultado='INICIADO'
     ORDER BY h.criado_em DESC LIMIT 1;
    IF NOT FOUND THEN RAISE EXCEPTION 'CLAIM_INICIADO_NAO_ENCONTRADO'; END IF;

    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='INDETERMINADO_TIMEOUT',
           sap_messages_sanitizadas=p_sap_messages,
           erro_sanitizado=btrim(p_erro)
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='ENVIANDO_SAP'
       AND tentativas=p_tentativa
       AND claim_token=p_claim
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'POST_CRIACAO',
        COALESCE(v.endpoint_sanitizado,'/HandlingUnit?sap-client=110'),
        'POST',v.tentativas,v.claim_token,v.request_json_sanitizado,
        NULL,v.sap_messages_sanitizadas,NULL,'INDETERMINADO_TIMEOUT',
        NULL,'',NULL,v.erro_sanitizado,false,
        v.tentativa_iniciada_em,v_fim,v_actor_usuario,v_actor_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(
    p_codigo bigint,p_hu varchar,p_warehouse varchar,p_http integer,
    p_response jsonb,p_sap_messages jsonb,p_etag text,p_created_by varchar,
    p_creation timestamptz,p_comparacao_aprovada boolean
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_fim timestamptz:=clock_timestamp();
BEGIN
    IF NOT COALESCE(p_comparacao_aprovada,false) THEN
        RAISE EXCEPTION 'RECONCILIACAO_EXIGE_COMPARACAO_APROVADA';
    END IF;
    IF char_length(btrim(COALESCE(p_hu,'')))=0 OR p_http<>200 THEN
        RAISE EXCEPTION 'RECONCILIACAO_SUCESSO_INVALIDA';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='CONFIRMADA_SAP',
           handling_unit_external_id=btrim(p_hu),
           warehouse=btrim(COALESCE(p_warehouse,'')),
           http_status=p_http,
           response_json_sanitizado=p_response,
           sap_messages_sanitizadas=p_sap_messages,
           odata_etag=p_etag,
           created_by_user_sap=p_created_by,
           creation_datetime_sap=p_creation,
           reconciliado_em=v_fim,
           confirmado_sap_em=v_fim,
           erro_sanitizado=NULL
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='INDETERMINADO_TIMEOUT'
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'GET_RECONCILIACAO',
        '/HandlingUnit(HandlingUnitExternalID=...,Warehouse=...)',
        'GET',v.tentativas,v.claim_token,NULL,v.response_json_sanitizado,
        v.sap_messages_sanitizadas,v.http_status,'CONFIRMADO',
        v.handling_unit_external_id,v.warehouse,v.odata_etag,NULL,false,
        v_fim,v_fim,v.codigo_usuario,v.terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(
    p_codigo bigint,p_hu varchar,p_warehouse varchar,p_http integer,
    p_response jsonb,p_sap_messages jsonb,p_erro text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_fim timestamptz:=clock_timestamp();
BEGIN
    IF p_http<>404 THEN
        RAISE EXCEPTION 'RECONCILIACAO_NAO_ENCONTRADA_EXIGE_HTTP_404';
    END IF;
    IF char_length(btrim(COALESCE(p_hu,'')))=0 THEN
        RAISE EXCEPTION 'RECONCILIACAO_NAO_ENCONTRADA_EXIGE_HU_CONHECIDA';
    END IF;
    SELECT * INTO v
      FROM desenvolvimento.hu_caixa
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa='INDETERMINADO_TIMEOUT';
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'GET_RECONCILIACAO',
        '/HandlingUnit(HandlingUnitExternalID=...,Warehouse=...)',
        'GET',v.tentativas,v.claim_token,NULL,p_response,p_sap_messages,
        p_http,'NAO_ENCONTRADO',btrim(p_hu),
        btrim(COALESCE(p_warehouse,'')),NULL,p_erro,false,
        v_fim,v_fim,v.codigo_usuario,v.terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_bloquear_configuracao(
    p_codigo bigint,p_usuario bigint,p_terminal text,p_erro text,
    p_sap_messages jsonb
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_fim timestamptz:=clock_timestamp();
BEGIN
    IF p_usuario IS NULL
       OR char_length(btrim(COALESCE(p_terminal,'')))=0
       OR char_length(btrim(COALESCE(p_erro,'')))=0 THEN
        RAISE EXCEPTION 'BLOQUEIO_CONFIGURACAO_EXIGE_USUARIO_TERMINAL_ERRO';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='BLOQUEADA',
           erro_sanitizado=btrim(p_erro),
           sap_messages_sanitizadas=p_sap_messages
     WHERE codigo_hu_caixa=p_codigo
       AND status_hu_caixa IN (
           'FINALIZADA_LOCAL','PREVIEW_HU_GERADO',
           'AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO'
       )
       AND claim_token IS NULL
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'BLOQUEIO_CONFIGURACAO',
        'LOCAL://produto-acabado/hu-caixa/bloquear-configuracao',
        'LOCAL',0,NULL,NULL,NULL,p_sap_messages,NULL,
        'BLOQUEADO_CONFIGURACAO',NULL,'',NULL,v.erro_sanitizado,
        false,v_fim,v_fim,p_usuario,p_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_cancelar(
    p_codigo bigint,p_usuario bigint,p_terminal text,p_motivo text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_fim timestamptz:=clock_timestamp();
BEGIN
    IF p_usuario IS NULL
       OR char_length(btrim(COALESCE(p_terminal,'')))=0
       OR char_length(btrim(COALESCE(p_motivo,'')))=0 THEN
        RAISE EXCEPTION 'CANCELAMENTO_EXIGE_USUARIO_TERMINAL_MOTIVO';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='CANCELADA',
           cancelado_em=v_fim,
           cancelado_por=p_usuario,
           motivo_cancelamento=btrim(p_motivo)
     WHERE codigo_hu_caixa=p_codigo
       AND terminal=upper(btrim(p_terminal))
       AND handling_unit_external_id IS NULL
       AND status_hu_caixa IN (
           'EM_PESAGEM','FINALIZADA_LOCAL','PREVIEW_HU_GERADO',
           'AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO',
           'ERRO_SAP','BLOQUEADA'
       )
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'CANCELAMENTO_LOCAL',
        'LOCAL://produto-acabado/hu-caixa/cancelar','LOCAL',0,NULL,
        NULL,NULL,NULL,NULL,'CANCELADO',NULL,'',NULL,
        v.motivo_cancelamento,false,v_fim,v_fim,p_usuario,p_terminal
    );
    RETURN true;
END
$func$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_liberar_reprocessamento(
    p_codigo bigint,p_usuario bigint,p_terminal text,p_motivo text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,desenvolvimento
AS $func$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_fim timestamptz:=clock_timestamp();
BEGIN
    IF p_usuario IS NULL
       OR char_length(btrim(COALESCE(p_terminal,'')))=0
       OR char_length(btrim(COALESCE(p_motivo,'')))=0 THEN
        RAISE EXCEPTION 'REPROCESSAMENTO_EXIGE_USUARIO_TERMINAL_MOTIVO';
    END IF;
    IF NOT EXISTS (
        SELECT 1
          FROM desenvolvimento.hu_caixa_integracao_sap h
         WHERE h.codigo_hu_caixa=p_codigo
           AND h.numero_tentativa=(
               SELECT tentativas FROM desenvolvimento.hu_caixa
                WHERE codigo_hu_caixa=p_codigo
           )
           AND h.resultado='ERRO_DEFINITIVO'
           AND h.pode_reprocessar
         ORDER BY h.criado_em DESC
         LIMIT 1
    ) THEN
        RETURN false;
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='PRONTA_PARA_ENVIO',
           reprocessamento_liberado_em=v_fim,
           reprocessamento_liberado_por=p_usuario,
           motivo_reprocessamento=btrim(p_motivo),
           claim_token=NULL,claim_em=NULL,tentativa_iniciada_em=NULL,
           enviado_sap_em=NULL,erro_sanitizado=NULL,http_status=NULL,
           response_json_sanitizado=NULL,sap_messages_sanitizadas=NULL
     WHERE codigo_hu_caixa=p_codigo
       AND terminal=upper(btrim(p_terminal))
       AND status_hu_caixa='ERRO_SAP'
       AND handling_unit_external_id IS NULL
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'LIBERACAO_REPROCESSAMENTO',
        'LOCAL://produto-acabado/hu-caixa/liberar-reprocessamento',
        'LOCAL',0,NULL,NULL,NULL,NULL,NULL,'LIBERADO_REPROCESSAMENTO',
        NULL,'',NULL,v.motivo_reprocessamento,false,v_fim,v_fim,
        p_usuario,p_terminal
    );
    RETURN true;
END
$func$;

-- Owners and execute surface
ALTER FUNCTION desenvolvimento.fn_hu_caixa_preparar_insert() OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_validar_update() OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_integracao_preparar_insert() OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_integracao_imutavel() OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_pesagem_normalizar() OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_inserir_evento(bigint,uuid,varchar,text,varchar,integer,uuid,jsonb,jsonb,jsonb,integer,varchar,varchar,varchar,text,text,boolean,timestamptz,timestamptz,bigint,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text) OWNER TO postgres;
ALTER FUNCTION desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text) OWNER TO postgres;

REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_preparar_insert() FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_validar_update() FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_integracao_preparar_insert() FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_integracao_imutavel() FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_pesagem_normalizar() FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_inserir_evento(bigint,uuid,varchar,text,varchar,integer,uuid,jsonb,jsonb,jsonb,integer,varchar,varchar,varchar,text,text,boolean,timestamptz,timestamptz,bigint,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text) FROM PUBLIC;

CREATE TRIGGER trg_hu_caixa_preparar_insert BEFORE INSERT ON desenvolvimento.hu_caixa FOR EACH ROW EXECUTE FUNCTION desenvolvimento.fn_hu_caixa_preparar_insert();
CREATE TRIGGER trg_hu_caixa_validar_update BEFORE UPDATE ON desenvolvimento.hu_caixa FOR EACH ROW EXECUTE FUNCTION desenvolvimento.fn_hu_caixa_validar_update();
CREATE TRIGGER trg_hu_caixa_integracao_preparar_insert BEFORE INSERT ON desenvolvimento.hu_caixa_integracao_sap FOR EACH ROW EXECUTE FUNCTION desenvolvimento.fn_hu_caixa_integracao_preparar_insert();
CREATE TRIGGER trg_hu_caixa_integracao_imutavel BEFORE UPDATE OR DELETE ON desenvolvimento.hu_caixa_integracao_sap FOR EACH ROW EXECUTE FUNCTION desenvolvimento.fn_hu_caixa_integracao_imutavel();
CREATE TRIGGER trg_hu_caixa_pesagem_normalizar BEFORE INSERT OR UPDATE ON desenvolvimento.hu_caixa_pesagem FOR EACH ROW EXECUTE FUNCTION desenvolvimento.fn_hu_caixa_pesagem_normalizar();

-- Indices
CREATE UNIQUE INDEX uq_hu_caixa_correlation_id ON desenvolvimento.hu_caixa(correlation_id);
CREATE UNIQUE INDEX uq_hu_caixa_codigo_local ON desenvolvimento.hu_caixa(codigo_caixa_local);
CREATE UNIQUE INDEX uq_hu_caixa_op_numero ON desenvolvimento.hu_caixa(numero_ordem_producao,numero_caixa);
CREATE UNIQUE INDEX uq_hu_caixa_hu_sap_warehouse ON desenvolvimento.hu_caixa(handling_unit_external_id,warehouse) WHERE handling_unit_external_id IS NOT NULL;
CREATE UNIQUE INDEX uq_hu_caixa_terminal_ativo ON desenvolvimento.hu_caixa(upper(btrim(terminal))) WHERE status_hu_caixa NOT IN ('CONFIRMADA_SAP','CANCELADA');
CREATE INDEX ix_hu_caixa_status_atualizado_044 ON desenvolvimento.hu_caixa(status_hu_caixa,atualizado_em DESC);
CREATE INDEX ix_hu_caixa_op ON desenvolvimento.hu_caixa(numero_ordem_producao,numero_caixa DESC);
CREATE INDEX ix_hu_caixa_integracao_tentativa_claim ON desenvolvimento.hu_caixa_integracao_sap(codigo_hu_caixa,numero_tentativa,claim_token,criado_em);
CREATE UNIQUE INDEX uq_hu_caixa_claim_token ON desenvolvimento.hu_caixa(claim_token) WHERE claim_token IS NOT NULL;
CREATE UNIQUE INDEX uq_hu_caixa_integracao_claim_iniciado ON desenvolvimento.hu_caixa_integracao_sap(claim_token) WHERE resultado='INICIADO';
CREATE INDEX ix_hu_caixa_integracao_correlation ON desenvolvimento.hu_caixa_integracao_sap(correlation_id,criado_em);
CREATE INDEX ix_hu_caixa_integracao_hu_sap ON desenvolvimento.hu_caixa_integracao_sap(handling_unit_external_id,warehouse) WHERE handling_unit_external_id IS NOT NULL;

-- Privilegios: sem UPDATE direto na caixa e sem INSERT/UPDATE/DELETE no historico.
REVOKE CREATE ON SCHEMA desenvolvimento FROM fugapet_dev_app;
GRANT USAGE ON SCHEMA desenvolvimento TO fugapet_dev_app;
GRANT SELECT ON TABLE desenvolvimento.hu_caixa TO fugapet_dev_app;
REVOKE INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER ON TABLE desenvolvimento.hu_caixa FROM fugapet_dev_app;
DO $gaia$
DECLARE v_col record;
BEGIN
    FOR v_col IN
        SELECT column_name FROM information_schema.columns
         WHERE table_schema='desenvolvimento' AND table_name='hu_caixa'
    LOOP
        EXECUTE format('REVOKE INSERT (%I) ON desenvolvimento.hu_caixa FROM fugapet_dev_app',v_col.column_name);
    END LOOP;
END
$gaia$;
GRANT INSERT (
    numero_ordem_producao,item_ordem_producao,correlation_id,
    codigo_sap_ordem_producao,codigo_sap_produto,codigo_produto_referencia,
    material,lote,centro,deposito,material_embalagem,origem_material_embalagem,
    peso_bruto,peso_liquido,peso_tara,unidade_peso,quantidade,
    unidade_quantidade,origem_pesagem,codigo_balanca,codigo_usuario,terminal
) ON desenvolvimento.hu_caixa TO fugapet_dev_app;

GRANT SELECT ON TABLE desenvolvimento.hu_caixa_pesagem TO fugapet_dev_app;
REVOKE INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER ON TABLE desenvolvimento.hu_caixa_pesagem FROM fugapet_dev_app;
DO $gaia$
DECLARE v_col record;
BEGIN
    FOR v_col IN
        SELECT column_name FROM information_schema.columns
         WHERE table_schema='desenvolvimento' AND table_name='hu_caixa_pesagem'
    LOOP
        EXECUTE format('REVOKE INSERT (%I) ON desenvolvimento.hu_caixa_pesagem FROM fugapet_dev_app',v_col.column_name);
    END LOOP;
END
$gaia$;
GRANT INSERT (
    codigo_hu_caixa,codigo_balanca,origem_pesagem,peso_lido,
    peso_bruto,peso_liquido,peso_tara,unidade_peso,payload_balanca,codigo_usuario
) ON desenvolvimento.hu_caixa_pesagem TO fugapet_dev_app;

GRANT SELECT ON TABLE desenvolvimento.hu_caixa_integracao_sap TO fugapet_dev_app;
REVOKE INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER ON TABLE desenvolvimento.hu_caixa_integracao_sap FROM fugapet_dev_app;

DO $gaia$ DECLARE v_sequence regclass; BEGIN
    FOREACH v_sequence IN ARRAY ARRAY[
        pg_get_serial_sequence('desenvolvimento.hu_caixa','codigo_hu_caixa')::regclass,
        pg_get_serial_sequence('desenvolvimento.hu_caixa_pesagem','codigo_hu_caixa_pesagem')::regclass
    ] LOOP
        EXECUTE format('GRANT USAGE, SELECT ON SEQUENCE %s TO fugapet_dev_app',v_sequence);
        EXECUTE format('REVOKE UPDATE ON SEQUENCE %s FROM fugapet_dev_app',v_sequence);
    END LOOP;
END $gaia$;

GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text) TO fugapet_dev_app;
GRANT EXECUTE ON FUNCTION desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text) TO fugapet_dev_app;


DO $gaia$
DECLARE
    v_owner_before name;
    v_owner_after name;
    v_comment_before text;
    v_comment_after text;
    v_definition_before text;
    v_definition_after text;
    v_grants_before text[];
    v_grants_after text[];
BEGIN
    SELECT view_owner,view_comment,view_definition
      INTO v_owner_before,v_comment_before,v_definition_before
      FROM gaia_044_view_snapshot;

    SELECT pg_get_userbyid(c.relowner),obj_description(c.oid,'pg_class'),pg_get_viewdef(c.oid,true)
      INTO v_owner_after,v_comment_after,v_definition_after
      FROM pg_class c
      JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='desenvolvimento'
       AND c.relname='vw_hu_caixas_sem_palete'
       AND c.relkind='v';

    SELECT array_agg(grant_sql ORDER BY grant_sql)
      INTO v_grants_before
      FROM gaia_044_view_grants;

    SELECT array_agg(g.grant_sql ORDER BY g.grant_sql)
      INTO v_grants_after
      FROM (
        SELECT format(
            'GRANT %s ON TABLE desenvolvimento.vw_hu_caixas_sem_palete TO %s%s',
            a.privilege_type,
            CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE quote_ident(pg_get_userbyid(a.grantee)) END,
            CASE WHEN a.is_grantable THEN ' WITH GRANT OPTION' ELSE '' END
        ) AS grant_sql
        FROM pg_class c
        JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN LATERAL aclexplode(c.relacl) a ON true
        WHERE n.nspname='desenvolvimento'
          AND c.relname='vw_hu_caixas_sem_palete'
          AND a.privilege_type<>'TRUNCATE'
      ) g;

    IF v_owner_after IS DISTINCT FROM v_owner_before
       OR v_comment_after IS DISTINCT FROM v_comment_before
       OR COALESCE(v_grants_after,ARRAY[]::text[]) IS DISTINCT FROM
          COALESCE(v_grants_before,ARRAY[]::text[]) THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_VIEW_METADATA_NAO_PRESERVADA owner=%/% comentario_igual=% grants_iguais=%',
          v_owner_before,v_owner_after,
          v_comment_after IS NOT DISTINCT FROM v_comment_before,
          COALESCE(v_grants_after,ARRAY[]::text[]) IS NOT DISTINCT FROM
          COALESCE(v_grants_before,ARRAY[]::text[]);
    END IF;

    RAISE NOTICE 'VIEW_DEFINICAO_TEXTUAL_IGUAL=%',
      v_definition_after IS NOT DISTINCT FROM v_definition_before;
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_IDENTIDADE';
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
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_COLUNAS esperado=11 atual=% nomes=%',
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
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_DEPENDENCIAS referencias=%',
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_ESTRUTURA_FILTROS';
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_PREDICADOS';
    END IF;

    SELECT COALESCE(array_agg(DISTINCT matches[1] ORDER BY matches[1]),ARRAY[]::text[])
      INTO v_literals
      FROM regexp_matches(v_definition,'''([^'']+)''','g') AS rm(matches);

    IF v_literals IS DISTINCT FROM ARRAY[
        'confirmada_sap','confirmado_sap','enviada_sap',
        'enviado_sap','etiquetada','vinculado'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_STATUS_EXTRAS_OU_AUSENTES status=%',
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
          'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_REPROVADA_METADATA_EXTRA quantidade=%',
          v_forbidden;
    END IF;

    SELECT COALESCE(cardinality(c.relacl),0) INTO v_grants
      FROM pg_class c
     WHERE c.oid=v_view_oid;

    PERFORM * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;
    RAISE NOTICE
      'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_VIEW_SEMANTICA_APROVADA owner=% comentario_presente=% grants_explicitos=% colunas=% dependencias=%',
      v_owner,v_comment IS NOT NULL,v_grants,cardinality(v_columns),v_references;
END
$gaia_view_semantic$;
DO $gaia$
DECLARE v_role record; v_functions integer; v_triggers integer; v_indexes integer; v_history_open bigint; v_public_execute integer; v_app_execute integer;
BEGIN
    SELECT * INTO v_role FROM pg_roles WHERE rolname='fugapet_dev_app';
    IF v_role.rolsuper OR v_role.rolcreatedb OR v_role.rolcreaterole OR v_role.rolreplication OR v_role.rolbypassrls
       OR has_schema_privilege('fugapet_dev_app','desenvolvimento','CREATE') THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_ROLE_INSEGURA';
    END IF;
    IF has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','INSERT')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_pesagem','INSERT')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','UPDATE')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_integracao_sap','INSERT')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_integracao_sap','UPDATE')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_integracao_sap','DELETE') THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_PRIVILEGIOS_DIRETOS_INDEVIDOS';
    END IF;

    IF EXISTS (
      SELECT 1 FROM (VALUES
        ('hu_caixa','numero_ordem_producao'),('hu_caixa','item_ordem_producao'),('hu_caixa','correlation_id'),
        ('hu_caixa','codigo_sap_ordem_producao'),('hu_caixa','codigo_sap_produto'),('hu_caixa','codigo_produto_referencia'),
        ('hu_caixa','material'),('hu_caixa','lote'),('hu_caixa','centro'),('hu_caixa','deposito'),
        ('hu_caixa','material_embalagem'),('hu_caixa','origem_material_embalagem'),('hu_caixa','peso_bruto'),
        ('hu_caixa','peso_liquido'),('hu_caixa','peso_tara'),('hu_caixa','unidade_peso'),('hu_caixa','quantidade'),
        ('hu_caixa','unidade_quantidade'),('hu_caixa','origem_pesagem'),('hu_caixa','codigo_balanca'),
        ('hu_caixa','codigo_usuario'),('hu_caixa','terminal'),
        ('hu_caixa_pesagem','codigo_hu_caixa'),('hu_caixa_pesagem','codigo_balanca'),
        ('hu_caixa_pesagem','origem_pesagem'),('hu_caixa_pesagem','peso_lido'),('hu_caixa_pesagem','peso_bruto'),
        ('hu_caixa_pesagem','peso_liquido'),('hu_caixa_pesagem','peso_tara'),('hu_caixa_pesagem','unidade_peso'),
        ('hu_caixa_pesagem','payload_balanca'),('hu_caixa_pesagem','codigo_usuario')
      ) e(tabela,coluna)
      WHERE NOT has_column_privilege('fugapet_dev_app',format('desenvolvimento.%I',e.tabela),e.coluna,'INSERT')
    ) THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_GRANTS_COLUNA_AUSENTES';
    END IF;
    IF has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','codigo_hu_caixa','INSERT')
       OR has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','status_hu_caixa','INSERT')
       OR has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','handling_unit_external_id','INSERT')
       OR has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa','situacao_hu_caixa','INSERT')
       OR has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_pesagem','codigo_hu_caixa_pesagem','INSERT')
       OR has_column_privilege('fugapet_dev_app','desenvolvimento.hu_caixa_pesagem','situacao_hu_caixa_pesagem','INSERT') THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_GRANTS_COLUNA_SISTEMICA';
    END IF;

    IF EXISTS (
      SELECT 1
        FROM information_schema.columns c
       WHERE c.table_schema='desenvolvimento'
         AND c.table_name IN ('hu_caixa','hu_caixa_pesagem')
         AND has_column_privilege(
               'fugapet_dev_app',format('desenvolvimento.%I',c.table_name),c.column_name,'INSERT'
             )
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
    ) THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_GRANTS_COLUNA_FORA_DA_LISTA';
    END IF;

    SELECT count(*) INTO v_functions FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='desenvolvimento' AND p.proname LIKE 'fn_hu_caixa%';
    SELECT count(*) INTO v_triggers FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname IN ('trg_hu_caixa_preparar_insert','trg_hu_caixa_validar_update','trg_hu_caixa_integracao_preparar_insert','trg_hu_caixa_integracao_imutavel','trg_hu_caixa_pesagem_normalizar');
    SELECT count(*) INTO v_indexes FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='desenvolvimento' AND c.relname IN ('uq_hu_caixa_correlation_id','uq_hu_caixa_codigo_local','uq_hu_caixa_op_numero','uq_hu_caixa_hu_sap_warehouse','uq_hu_caixa_terminal_ativo','ix_hu_caixa_status_atualizado_044','ix_hu_caixa_op','ix_hu_caixa_integracao_tentativa_claim','uq_hu_caixa_claim_token','uq_hu_caixa_integracao_claim_iniciado','ix_hu_caixa_integracao_correlation','ix_hu_caixa_integracao_hu_sap');
    SELECT count(*) INTO v_history_open FROM desenvolvimento.hu_caixa_integracao_sap WHERE finalizado_em IS NULL;
    SELECT count(*) INTO v_public_execute
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid=p.pronamespace
      JOIN LATERAL aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a ON true
     WHERE n.nspname='desenvolvimento'
       AND p.proname IN ('fn_hu_caixa_preparar_insert','fn_hu_caixa_validar_update','fn_hu_caixa_integracao_preparar_insert','fn_hu_caixa_integracao_imutavel','fn_hu_caixa_pesagem_normalizar','fn_hu_caixa_inserir_evento','fn_hu_caixa_finalizar_local','fn_hu_caixa_salvar_preview','fn_hu_caixa_aguardar_autorizacao','fn_hu_caixa_autorizar_envio','fn_hu_caixa_claim_envio','fn_hu_caixa_registrar_sucesso','fn_hu_caixa_registrar_erro','fn_hu_caixa_registrar_timeout','fn_hu_caixa_confirmar_reconciliacao','fn_hu_caixa_registrar_reconciliacao_nao_encontrada','fn_hu_caixa_bloquear_configuracao','fn_hu_caixa_cancelar','fn_hu_caixa_liberar_reprocessamento')
       AND a.grantee=0 AND a.privilege_type='EXECUTE';
    SELECT count(*) INTO v_app_execute
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento'
       AND p.proname IN ('fn_hu_caixa_finalizar_local','fn_hu_caixa_salvar_preview','fn_hu_caixa_aguardar_autorizacao','fn_hu_caixa_autorizar_envio','fn_hu_caixa_claim_envio','fn_hu_caixa_registrar_sucesso','fn_hu_caixa_registrar_erro','fn_hu_caixa_registrar_timeout','fn_hu_caixa_confirmar_reconciliacao','fn_hu_caixa_registrar_reconciliacao_nao_encontrada','fn_hu_caixa_bloquear_configuracao','fn_hu_caixa_cancelar','fn_hu_caixa_liberar_reprocessamento')
       AND has_function_privilege('fugapet_dev_app',p.oid,'EXECUTE');
    IF v_functions < 19 OR v_triggers<>5 OR v_indexes<>12 OR v_history_open<>0 OR v_public_execute<>0 OR v_app_execute<>13 THEN
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_REPROVADA_POSTCHECK funcoes=% triggers=% indices=% historico_aberto=% public_execute=% app_execute=%/13',v_functions,v_triggers,v_indexes,v_history_open,v_public_execute,v_app_execute;
    END IF;
    RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_APLICADA_COM_SUCESSO';
    RAISE NOTICE 'CLAIM_TOKEN=IMPLEMENTADO';
    RAISE NOTICE 'UPDATE_DIRETO_ROLE_APLICACAO=AUSENTE';
    RAISE NOTICE 'CREATE_SCHEMA_ROLE_APLICACAO=AUSENTE';
END $gaia$;

COMMIT;
\echo 'CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_REV9_APLICADA_COM_SUCESSO'
\echo 'OK - incremental 044 DEV REV9 aplicado sem dados produtivos'
\endif
