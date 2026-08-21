\set ON_ERROR_STOP on
\pset pager off
\pset tuples_only off
\pset format aligned
\pset null '<NULL>'

-- FugaPET - Incremental 044 DEV - REV10
-- PREFLIGHT exclusivamente READ ONLY.
-- Compatibilidade operacional REV10: PostgreSQL major 15, versao minima 15.5.
-- Nenhum objeto ou dado e alterado.

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '5min';
SET LOCAL lock_timeout = '5s';

DO $gaia$
DECLARE
    v_version_num integer;
    v_major integer;
    v_schema_exists boolean;
    v_role_exists boolean;
    v_missing_tables text;
    v_elevated_memberships integer;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO BANCO_DIVERGENTE atual=% esperado=fuga_jales_local_desenvolvimento',
            current_database();
    END IF;

    v_version_num := current_setting('server_version_num')::integer;
    v_major := v_version_num / 10000;
    IF v_major <> 15 OR v_version_num < 150005 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO POSTGRESQL_INCOMPATIVEL major_atual=% versao_num_atual=% major_esperado=15 versao_minima=15.5',
            v_major,
            v_version_num;
    END IF;

    SELECT to_regnamespace('desenvolvimento') IS NOT NULL
      INTO v_schema_exists;
    IF NOT v_schema_exists THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO SCHEMA_DESENVOLVIMENTO_AUSENTE';
    END IF;

    SELECT EXISTS (
        SELECT 1
          FROM pg_roles
         WHERE rolname = 'fugapet_dev_app'
           AND rolcanlogin
           AND NOT rolsuper
           AND NOT rolcreatedb
           AND NOT rolcreaterole
           AND NOT rolreplication
           AND NOT rolbypassrls
    ) INTO v_role_exists;
    IF NOT v_role_exists THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO ROLE_FUGAPET_DEV_APP_AUSENTE_OU_COM_ATRIBUTO_ELEVADO';
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
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO ROLE_FUGAPET_DEV_APP_HERDA_ROLE_ELEVADA quantidade=%',
          v_elevated_memberships;
    END IF;

    IF has_schema_privilege('fugapet_dev_app', 'desenvolvimento', 'CREATE') THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO ROLE_FUGAPET_DEV_APP_COM_CREATE_EFETIVO_NO_SCHEMA';
    END IF;

    IF NOT has_schema_privilege('fugapet_dev_app', 'desenvolvimento', 'USAGE') THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO ROLE_FUGAPET_DEV_APP_SEM_USAGE_NO_SCHEMA';
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
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO TABELAS_HU_AUSENTES=%',
            v_missing_tables;
    END IF;

    IF to_regprocedure('desenvolvimento.fn_definir_atualizado_em()') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO FUNCAO_OFICIAL_ATUALIZADO_EM_AUSENTE';
    END IF;

    IF to_regclass('desenvolvimento.vw_hu_caixas_sem_palete') IS NULL THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO VIEW_VW_HU_CAIXAS_SEM_PALETE_AUSENTE';
    END IF;
END
$gaia$;

SELECT
    'AMBIENTE' AS grupo,
    current_database() AS banco,
    current_user AS executor,
    current_schema() AS schema_atual,
    current_setting('server_version') AS postgresql,
    current_setting('search_path') AS search_path,
    COALESCE(inet_server_addr()::text, '') AS servidor,
    COALESCE(inet_server_port()::text, '') AS porta;

SELECT
    'ROLE_APLICACAO' AS grupo,
    rolname,
    rolcanlogin,
    rolsuper,
    rolcreatedb,
    rolcreaterole,
    rolreplication,
    rolbypassrls,
    has_database_privilege(rolname, current_database(), 'CONNECT') AS connect_banco,
    has_schema_privilege(rolname, 'desenvolvimento', 'USAGE') AS usage_schema,
    has_schema_privilege(rolname, 'desenvolvimento', 'CREATE') AS create_schema
FROM pg_roles
WHERE rolname = 'fugapet_dev_app';

WITH RECURSIVE memberships AS (
    SELECT m.roleid, m.member, 1 AS nivel
      FROM pg_auth_members m
      JOIN pg_roles r ON r.oid = m.member
     WHERE r.rolname = 'fugapet_dev_app'
    UNION ALL
    SELECT m.roleid, m.member, memberships.nivel + 1
      FROM pg_auth_members m
      JOIN memberships ON memberships.roleid = m.member
)
SELECT DISTINCT
    'ROLE_HERDADA' AS grupo,
    r.rolname AS role_herdada,
    memberships.nivel,
    r.rolsuper,
    r.rolcreatedb,
    r.rolcreaterole,
    r.rolreplication,
    r.rolbypassrls,
    has_schema_privilege(r.rolname, 'desenvolvimento', 'CREATE') AS create_schema_role_herdada
FROM memberships
JOIN pg_roles r ON r.oid = memberships.roleid
ORDER BY memberships.nivel, r.rolname;

-- Inventario das quatro estruturas de caixa.
SELECT
    'TABELA' AS grupo,
    c.relname AS tabela,
    c.relkind,
    pg_get_userbyid(c.relowner) AS owner,
    c.relpersistence,
    obj_description(c.oid, 'pg_class') AS comentario,
    COALESCE(s.n_live_tup, 0) AS estimativa_registros
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
WHERE n.nspname = 'desenvolvimento'
  AND c.relname IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY c.relname;

SELECT
    'COLUNA' AS grupo,
    table_name,
    ordinal_position,
    column_name,
    data_type,
    udt_name,
    character_maximum_length,
    numeric_precision,
    numeric_scale,
    is_nullable,
    column_default,
    is_identity,
    identity_generation,
    is_generated,
    generation_expression
FROM information_schema.columns
WHERE table_schema = 'desenvolvimento'
  AND table_name IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY table_name, ordinal_position;

SELECT
    'CONSTRAINT' AS grupo,
    rel.relname AS tabela,
    con.conname,
    con.contype,
    con.convalidated,
    pg_get_constraintdef(con.oid, true) AS definicao
FROM pg_constraint con
JOIN pg_class rel ON rel.oid = con.conrelid
JOIN pg_namespace n ON n.oid = rel.relnamespace
WHERE n.nspname = 'desenvolvimento'
  AND rel.relname IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY rel.relname, con.contype, con.conname;

SELECT
    'INDICE' AS grupo,
    tablename AS tabela,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'desenvolvimento'
  AND tablename IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY tablename, indexname;

SELECT
    'TRIGGER' AS grupo,
    rel.relname AS tabela,
    t.tgname,
    t.tgenabled,
    pg_get_triggerdef(t.oid, true) AS definicao
FROM pg_trigger t
JOIN pg_class rel ON rel.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = rel.relnamespace
WHERE NOT t.tgisinternal
  AND n.nspname = 'desenvolvimento'
  AND rel.relname IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY rel.relname, t.tgname;

-- Snapshot documental exato da view critica.
SELECT
    'VIEW_VW_HU_CAIXAS_SEM_PALETE' AS grupo,
    n.nspname AS schema_view,
    c.relname AS view_name,
    pg_get_viewdef(c.oid,true) AS definicao_exata,
    pg_get_userbyid(c.relowner) AS owner,
    obj_description(c.oid,'pg_class') AS comentario,
    c.relacl AS acl_bruta
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='desenvolvimento'
  AND c.relname='vw_hu_caixas_sem_palete'
  AND c.relkind='v';

SELECT
    'VIEW_COLUNA_EXPOSTA' AS grupo,
    ordinal_position,
    column_name,
    data_type,
    udt_name,
    character_maximum_length,
    numeric_precision,
    numeric_scale,
    is_nullable
FROM information_schema.columns
WHERE table_schema='desenvolvimento'
  AND table_name='vw_hu_caixas_sem_palete'
ORDER BY ordinal_position;

SELECT
    'VIEW_GRANT' AS grupo,
    CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END AS grantee,
    a.privilege_type,
    a.is_grantable
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
JOIN LATERAL aclexplode(COALESCE(c.relacl,acldefault('r',c.relowner))) a ON true
WHERE n.nspname='desenvolvimento'
  AND c.relname='vw_hu_caixas_sem_palete'
ORDER BY grantee,a.privilege_type,a.is_grantable;

SELECT
    'OBJETO_DEPENDENTE_DA_VIEW' AS grupo,
    nd.nspname AS schema_dependente,
    dependente.relname AS objeto_dependente,
    dependente.relkind,
    pg_get_userbyid(dependente.relowner) AS owner
FROM pg_depend d
JOIN pg_rewrite r ON r.oid=d.objid
JOIN pg_class dependente ON dependente.oid=r.ev_class
JOIN pg_namespace nd ON nd.oid=dependente.relnamespace
WHERE d.refobjid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
  AND NOT (nd.nspname='desenvolvimento' AND dependente.relname='vw_hu_caixas_sem_palete')
ORDER BY nd.nspname,dependente.relname;

SELECT
    'FUNCAO_DEPENDENTE_DA_VIEW' AS grupo,
    n.nspname,
    p.proname,
    pg_get_function_identity_arguments(p.oid) AS argumentos,
    pg_get_userbyid(p.proowner) AS owner,
    pg_get_functiondef(p.oid) AS definicao
FROM pg_proc p
JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE p.prokind='f'
  AND lower(pg_get_functiondef(p.oid)) LIKE '%vw_hu_caixas_sem_palete%'
ORDER BY n.nspname,p.proname,argumentos;


-- Metadados adicionais da view que impedem DROP/RECREATE seguro nesta entrega.
SELECT
    'VIEW_COLUMN_ACL_COMMENT' AS grupo,
    a.attnum,
    a.attname,
    a.attacl,
    col_description(a.attrelid,a.attnum) AS comentario_coluna
FROM pg_attribute a
WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
  AND a.attnum>0 AND NOT a.attisdropped
ORDER BY a.attnum;

SELECT
    'VIEW_REGRA_ADICIONAL' AS grupo,
    r.rulename,
    pg_get_ruledef(r.oid,true) AS definicao
FROM pg_rewrite r
WHERE r.ev_class='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
  AND r.rulename<>'_RETURN'
ORDER BY r.rulename;

SELECT
    'VIEW_TRIGGER' AS grupo,
    t.tgname,
    pg_get_triggerdef(t.oid,true) AS definicao
FROM pg_trigger t
WHERE t.tgrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
  AND NOT t.tgisinternal
ORDER BY t.tgname;

-- Views que dependem diretamente ou indiretamente das estruturas HU.
SELECT DISTINCT
    'VIEW_DEPENDENTE' AS grupo,
    vn.nspname AS schema_view,
    v.relname AS view_name,
    tn.nspname AS schema_tabela,
    t.relname AS tabela_referenciada
FROM pg_rewrite r
JOIN pg_class v ON v.oid = r.ev_class
JOIN pg_namespace vn ON vn.oid = v.relnamespace
JOIN pg_depend d ON d.objid = r.oid
JOIN pg_class t ON t.oid = d.refobjid
JOIN pg_namespace tn ON tn.oid = t.relnamespace
WHERE vn.nspname = 'desenvolvimento'
  AND tn.nspname = 'desenvolvimento'
  AND t.relname IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY view_name, tabela_referenciada;

-- Funcoes que referenciam nominalmente as estruturas HU.
SELECT
    'FUNCAO_DEPENDENTE_TEXTO' AS grupo,
    p.proname,
    pg_get_function_identity_arguments(p.oid) AS argumentos,
    pg_get_userbyid(p.proowner) AS owner,
    p.prosecdef AS security_definer,
    l.lanname AS linguagem
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
JOIN pg_language l ON l.oid = p.prolang
WHERE n.nspname = 'desenvolvimento'
  AND p.prokind='f'
  AND lower(pg_get_functiondef(p.oid)) ~
      '(hu_caixa|hu_caixa_pesagem|hu_caixa_integracao_sap|hu_caixa_etiqueta)'
ORDER BY p.proname, argumentos;

SELECT
    'SEQUENCE' AS grupo,
    seq_ns.nspname AS schema_sequence,
    seq.relname AS sequence_name,
    tab.relname AS tabela,
    att.attname AS coluna,
    pg_get_userbyid(seq.relowner) AS owner
FROM pg_class seq
JOIN pg_namespace seq_ns ON seq_ns.oid = seq.relnamespace
JOIN pg_depend dep
  ON dep.objid = seq.oid
 AND dep.deptype IN ('a', 'i')
JOIN pg_class tab ON tab.oid = dep.refobjid
JOIN pg_namespace tab_ns ON tab_ns.oid = tab.relnamespace
JOIN pg_attribute att
  ON att.attrelid = tab.oid
 AND att.attnum = dep.refobjsubid
WHERE seq.relkind = 'S'
  AND tab_ns.nspname = 'desenvolvimento'
  AND tab.relname IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY tab.relname, att.attname;

SELECT
    'GRANT_TABELA' AS grupo,
    t.table_name,
    p.privilege_type,
    p.is_grantable
FROM information_schema.tables t
LEFT JOIN information_schema.role_table_grants p
  ON p.table_schema = t.table_schema
 AND p.table_name = t.table_name
 AND p.grantee = 'fugapet_dev_app'
WHERE t.table_schema = 'desenvolvimento'
  AND t.table_name IN (
      'hu_caixa',
      'hu_caixa_pesagem',
      'hu_caixa_integracao_sap',
      'hu_caixa_etiqueta'
  )
ORDER BY t.table_name, p.privilege_type;

SELECT
    'GRANT_EFETIVO' AS grupo,
    x.tabela,
    has_table_privilege('fugapet_dev_app', x.tabela, 'SELECT') AS pode_select,
    has_table_privilege('fugapet_dev_app', x.tabela, 'INSERT') AS pode_insert,
    has_table_privilege('fugapet_dev_app', x.tabela, 'UPDATE') AS pode_update,
    has_table_privilege('fugapet_dev_app', x.tabela, 'DELETE') AS pode_delete,
    has_table_privilege('fugapet_dev_app', x.tabela, 'TRUNCATE') AS pode_truncate,
    has_table_privilege('fugapet_dev_app', x.tabela, 'REFERENCES') AS pode_references,
    has_table_privilege('fugapet_dev_app', x.tabela, 'TRIGGER') AS pode_trigger
FROM (VALUES
    ('desenvolvimento.hu_caixa'),
    ('desenvolvimento.hu_caixa_pesagem'),
    ('desenvolvimento.hu_caixa_integracao_sap'),
    ('desenvolvimento.hu_caixa_etiqueta')
) AS x(tabela)
ORDER BY x.tabela;


SELECT
    'PRIVILEGIO_INSERT_POR_COLUNA' AS grupo,
    c.table_name,
    c.ordinal_position,
    c.column_name,
    has_column_privilege(
      'fugapet_dev_app',format('desenvolvimento.%I',c.table_name),c.column_name,'INSERT'
    ) AS insert_efetivo_inclusive_heranca,
    EXISTS (
      SELECT 1 FROM information_schema.column_privileges cp
       WHERE cp.grantee='fugapet_dev_app'
         AND cp.table_schema=c.table_schema
         AND cp.table_name=c.table_name
         AND cp.column_name=c.column_name
         AND cp.privilege_type='INSERT'
    ) AS insert_direto
FROM information_schema.columns c
WHERE c.table_schema='desenvolvimento'
  AND c.table_name IN ('hu_caixa','hu_caixa_pesagem')
ORDER BY c.table_name,c.ordinal_position;

SELECT
    'CONTAGEM' AS grupo,
    (SELECT count(*) FROM desenvolvimento.hu_caixa) AS hu_caixa,
    (SELECT count(*) FROM desenvolvimento.hu_caixa_pesagem) AS hu_caixa_pesagem,
    (SELECT count(*) FROM desenvolvimento.hu_caixa_integracao_sap) AS hu_caixa_integracao_sap,
    (SELECT count(*) FROM desenvolvimento.hu_caixa_etiqueta) AS hu_caixa_etiqueta;

SELECT
    'STATUS_HU_CAIXA' AS grupo,
    status_hu_caixa,
    count(*) AS quantidade
FROM desenvolvimento.hu_caixa
GROUP BY status_hu_caixa
ORDER BY status_hu_caixa;

SELECT
    'STATUS_INTEGRACAO_HISTORICA' AS grupo,
    COALESCE(status_sap, '<NULL>') AS status_sap,
    count(*) AS quantidade
FROM desenvolvimento.hu_caixa_integracao_sap
GROUP BY status_sap
ORDER BY status_sap;

SELECT
    'QUALIDADE_DADOS_HU_CAIXA' AS grupo,
    count(*) FILTER (WHERE hu_caixa IS NULL OR btrim(hu_caixa) = '') AS hu_vazia,
    count(*) FILTER (WHERE material IS NULL OR btrim(material) = '') AS material_vazio,
    count(*) FILTER (WHERE numero_caixa IS NULL OR numero_caixa <= 0) AS numero_caixa_invalido,
    count(*) FILTER (WHERE peso_bruto IS NULL OR peso_bruto <= 0) AS peso_bruto_invalido,
    count(*) FILTER (WHERE peso_liquido IS NULL OR peso_liquido <= 0) AS peso_liquido_invalido,
    count(*) FILTER (WHERE peso_tara IS NULL OR peso_tara < 0) AS tara_invalida,
    count(*) FILTER (
        WHERE peso_bruto IS NOT NULL
          AND peso_liquido IS NOT NULL
          AND peso_tara IS NOT NULL
          AND peso_bruto <> peso_liquido + peso_tara
    ) AS equacao_peso_invalida,
    count(*) FILTER (WHERE upper(btrim(unidade_peso)) <> 'KG') AS unidade_nao_kg
FROM desenvolvimento.hu_caixa;

SELECT
    'DUPLICIDADE_HU_LEGADA' AS grupo,
    upper(btrim(hu_caixa)) AS chave,
    count(*) AS quantidade
FROM desenvolvimento.hu_caixa
WHERE hu_caixa IS NOT NULL
  AND btrim(hu_caixa) <> ''
GROUP BY upper(btrim(hu_caixa))
HAVING count(*) > 1
ORDER BY quantidade DESC, chave;

SELECT
    'DUPLICIDADE_NUMERO_CAIXA_LEGADA' AS grupo,
    codigo_sap_ordem_producao,
    numero_caixa,
    count(*) AS quantidade
FROM desenvolvimento.hu_caixa
WHERE numero_caixa IS NOT NULL
GROUP BY codigo_sap_ordem_producao, numero_caixa
HAVING count(*) > 1
ORDER BY quantidade DESC, codigo_sap_ordem_producao, numero_caixa;

SELECT
    'HUS_REGISTRADAS' AS grupo,
    codigo_hu_caixa,
    hu_caixa,
    material,
    numero_caixa,
    status_hu_caixa,
    criada_sap_em,
    hu_caixa_criado_em
FROM desenvolvimento.hu_caixa
ORDER BY codigo_hu_caixa;

SELECT
    'REFERENCIAS_EXTERNAS' AS grupo,
    conrelid::regclass AS tabela_origem,
    conname,
    pg_get_constraintdef(oid, true) AS definicao
FROM pg_constraint
WHERE contype = 'f'
  AND confrelid IN (
      'desenvolvimento.hu_caixa'::regclass,
      'desenvolvimento.hu_caixa_pesagem'::regclass,
      'desenvolvimento.hu_caixa_integracao_sap'::regclass,
      'desenvolvimento.hu_caixa_etiqueta'::regclass
  )
ORDER BY conrelid::regclass::text, conname;

-- Dependencias documentais dos incrementais 024 e 028.
SELECT
    'DEPENDENCIA_024' AS grupo,
    p.proname,
    pg_get_userbyid(p.proowner) AS owner,
    p.prosecdef,
    lower(pg_get_functiondef(p.oid)) LIKE '%clock_timestamp()%' AS usa_clock_timestamp
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'desenvolvimento'
  AND p.proname = 'fn_definir_atualizado_em';

SELECT
    'DEPENDENCIA_028' AS grupo,
    to_regclass('desenvolvimento.hu_caixa') IS NOT NULL AS hu_caixa_presente,
    to_regclass('desenvolvimento.hu_palete') IS NOT NULL AS hu_palete_presente,
    to_regclass('desenvolvimento.hu_palete_item') IS NOT NULL AS hu_palete_item_presente,
    to_regclass('desenvolvimento.vw_hu_caixas_sem_palete') IS NOT NULL AS view_caixas_sem_palete_presente;

-- Estado do próprio incremental 044 REV10.
SELECT
    'ESTADO_044_REV10' AS grupo,
    (SELECT count(*) FROM (VALUES
        ('hu_caixa', 'numero_ordem_producao'),
        ('hu_caixa', 'item_ordem_producao'),
        ('hu_caixa', 'codigo_caixa_local'),
        ('hu_caixa', 'correlation_id'),
        ('hu_caixa', 'lote'),
        ('hu_caixa', 'centro'),
        ('hu_caixa', 'deposito'),
        ('hu_caixa', 'material_embalagem'),
        ('hu_caixa', 'origem_material_embalagem'),
        ('hu_caixa', 'quantidade'),
        ('hu_caixa', 'unidade_quantidade'),
        ('hu_caixa', 'origem_pesagem'),
        ('hu_caixa', 'terminal'),
        ('hu_caixa', 'endpoint_sanitizado'),
        ('hu_caixa', 'handling_unit_external_id'),
        ('hu_caixa', 'warehouse'),
        ('hu_caixa', 'odata_etag'),
        ('hu_caixa', 'created_by_user_sap'),
        ('hu_caixa', 'creation_datetime_sap'),
        ('hu_caixa', 'http_status'),
        ('hu_caixa', 'request_json_sanitizado'),
        ('hu_caixa', 'response_json_sanitizado'),
        ('hu_caixa', 'sap_messages_sanitizadas'),
        ('hu_caixa', 'erro_sanitizado'),
        ('hu_caixa', 'claim_em'),
        ('hu_caixa', 'claim_token'),
        ('hu_caixa', 'tentativa_iniciada_em'),
        ('hu_caixa', 'enviado_sap_em'),
        ('hu_caixa', 'confirmado_sap_em'),
        ('hu_caixa', 'reconciliado_em'),
        ('hu_caixa', 'tentativas'),
        ('hu_caixa', 'autorizado_envio_em'),
        ('hu_caixa', 'autorizado_envio_por'),
        ('hu_caixa', 'terminal_autorizacao'),
        ('hu_caixa', 'cancelado_em'),
        ('hu_caixa', 'cancelado_por'),
        ('hu_caixa', 'motivo_cancelamento'),
        ('hu_caixa', 'reprocessamento_liberado_em'),
        ('hu_caixa', 'reprocessamento_liberado_por'),
        ('hu_caixa', 'motivo_reprocessamento'),
        ('hu_caixa', 'criado_em'),
        ('hu_caixa', 'atualizado_em'),
        ('hu_caixa_integracao_sap', 'correlation_id'),
        ('hu_caixa_integracao_sap', 'tipo_operacao'),
        ('hu_caixa_integracao_sap', 'endpoint_sanitizado'),
        ('hu_caixa_integracao_sap', 'numero_tentativa'),
        ('hu_caixa_integracao_sap', 'claim_token'),
        ('hu_caixa_integracao_sap', 'request_json_sanitizado'),
        ('hu_caixa_integracao_sap', 'response_json_sanitizado'),
        ('hu_caixa_integracao_sap', 'http_status'),
        ('hu_caixa_integracao_sap', 'resultado'),
        ('hu_caixa_integracao_sap', 'handling_unit_external_id'),
        ('hu_caixa_integracao_sap', 'warehouse'),
        ('hu_caixa_integracao_sap', 'odata_etag'),
        ('hu_caixa_integracao_sap', 'mensagem_erro_sanitizada'),
        ('hu_caixa_integracao_sap', 'iniciado_em'),
        ('hu_caixa_integracao_sap', 'finalizado_em'),
        ('hu_caixa_integracao_sap', 'terminal'),
        ('hu_caixa_integracao_sap', 'criado_em')
    ) AS e(tabela,coluna)
    WHERE EXISTS (SELECT 1 FROM information_schema.columns c WHERE c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna)) AS colunas_presentes,
    59 AS colunas_total,
    ((to_regprocedure('desenvolvimento.fn_hu_caixa_preparar_insert()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_validar_update()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_integracao_preparar_insert()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_integracao_imutavel()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_inserir_evento(bigint,uuid,varchar,text,varchar,integer,uuid,jsonb,jsonb,jsonb,integer,varchar,varchar,varchar,text,text,boolean,timestamptz,timestamptz,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text)') IS NOT NULL)::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_preparar_insert')::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_validar_update')::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_integracao_preparar_insert')::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_integracao_imutavel')::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_correlation_id') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_codigo_local') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_op_numero') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_hu_sap_warehouse') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_terminal_ativo') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_status_atualizado_044') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_op') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_integracao_tentativa_claim') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_claim_token') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_integracao_claim_iniciado') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_integracao_correlation') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_integracao_hu_sap') IS NOT NULL)::integer
      + EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE n.nspname='desenvolvimento' AND c.conname='uq_hu_caixa_codigo_correlation')::integer) AS objetos_presentes,
    34 AS objetos_total;

DO $gaia$
DECLARE
    v_hu bigint;
    v_hist bigint;
    v_columns_044 integer;
    v_objects_044 integer;
    v_columns_rev5 integer;
    v_objects_rev5 integer;
    v_view_dependents integer;
    v_function_dependents integer;
    v_view_column_acl integer;
    v_view_column_comments integer;
    v_view_extra_rules integer;
    v_view_triggers integer;
    v_old_status_invalid integer;
    v_duplicates integer;
    v_dependency_count integer;
    v_table_privileges integer;
    v_sequence_privileges integer;
BEGIN
    SELECT count(*) INTO v_hu FROM desenvolvimento.hu_caixa;
    SELECT count(*) INTO v_hist FROM desenvolvimento.hu_caixa_integracao_sap;

    SELECT count(*) INTO v_columns_044
      FROM (VALUES
        ('hu_caixa', 'numero_ordem_producao'),
        ('hu_caixa', 'item_ordem_producao'),
        ('hu_caixa', 'codigo_caixa_local'),
        ('hu_caixa', 'correlation_id'),
        ('hu_caixa', 'lote'),
        ('hu_caixa', 'centro'),
        ('hu_caixa', 'deposito'),
        ('hu_caixa', 'material_embalagem'),
        ('hu_caixa', 'origem_material_embalagem'),
        ('hu_caixa', 'quantidade'),
        ('hu_caixa', 'unidade_quantidade'),
        ('hu_caixa', 'origem_pesagem'),
        ('hu_caixa', 'terminal'),
        ('hu_caixa', 'endpoint_sanitizado'),
        ('hu_caixa', 'handling_unit_external_id'),
        ('hu_caixa', 'warehouse'),
        ('hu_caixa', 'odata_etag'),
        ('hu_caixa', 'created_by_user_sap'),
        ('hu_caixa', 'creation_datetime_sap'),
        ('hu_caixa', 'http_status'),
        ('hu_caixa', 'request_json_sanitizado'),
        ('hu_caixa', 'response_json_sanitizado'),
        ('hu_caixa', 'sap_messages_sanitizadas'),
        ('hu_caixa', 'erro_sanitizado'),
        ('hu_caixa', 'claim_em'),
        ('hu_caixa', 'claim_token'),
        ('hu_caixa', 'tentativa_iniciada_em'),
        ('hu_caixa', 'enviado_sap_em'),
        ('hu_caixa', 'confirmado_sap_em'),
        ('hu_caixa', 'reconciliado_em'),
        ('hu_caixa', 'tentativas'),
        ('hu_caixa', 'autorizado_envio_em'),
        ('hu_caixa', 'autorizado_envio_por'),
        ('hu_caixa', 'terminal_autorizacao'),
        ('hu_caixa', 'cancelado_em'),
        ('hu_caixa', 'cancelado_por'),
        ('hu_caixa', 'motivo_cancelamento'),
        ('hu_caixa', 'reprocessamento_liberado_em'),
        ('hu_caixa', 'reprocessamento_liberado_por'),
        ('hu_caixa', 'motivo_reprocessamento'),
        ('hu_caixa', 'criado_em'),
        ('hu_caixa', 'atualizado_em'),
        ('hu_caixa_integracao_sap', 'correlation_id'),
        ('hu_caixa_integracao_sap', 'tipo_operacao'),
        ('hu_caixa_integracao_sap', 'endpoint_sanitizado'),
        ('hu_caixa_integracao_sap', 'numero_tentativa'),
        ('hu_caixa_integracao_sap', 'claim_token'),
        ('hu_caixa_integracao_sap', 'request_json_sanitizado'),
        ('hu_caixa_integracao_sap', 'response_json_sanitizado'),
        ('hu_caixa_integracao_sap', 'http_status'),
        ('hu_caixa_integracao_sap', 'resultado'),
        ('hu_caixa_integracao_sap', 'handling_unit_external_id'),
        ('hu_caixa_integracao_sap', 'warehouse'),
        ('hu_caixa_integracao_sap', 'odata_etag'),
        ('hu_caixa_integracao_sap', 'mensagem_erro_sanitizada'),
        ('hu_caixa_integracao_sap', 'iniciado_em'),
        ('hu_caixa_integracao_sap', 'finalizado_em'),
        ('hu_caixa_integracao_sap', 'terminal'),
        ('hu_caixa_integracao_sap', 'criado_em')
      ) AS e(tabela,coluna)
     WHERE EXISTS (
         SELECT 1 FROM information_schema.columns c
          WHERE c.table_schema='desenvolvimento'
            AND c.table_name=e.tabela
            AND c.column_name=e.coluna
     );

    SELECT (to_regprocedure('desenvolvimento.fn_hu_caixa_preparar_insert()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_validar_update()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_integracao_preparar_insert()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_integracao_imutavel()') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_inserir_evento(bigint,uuid,varchar,text,varchar,integer,uuid,jsonb,jsonb,jsonb,integer,varchar,varchar,varchar,text,text,boolean,timestamptz,timestamptz,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_finalizar_local(bigint,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_salvar_preview(bigint,jsonb,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_aguardar_autorizacao(bigint)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_autorizar_envio(bigint,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_claim_envio(bigint,bigint,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_sucesso(bigint,integer,uuid,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_erro(bigint,integer,uuid,integer,jsonb,jsonb,text,varchar,boolean)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_timeout(bigint,integer,uuid,jsonb,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_confirmar_reconciliacao(bigint,varchar,varchar,integer,jsonb,jsonb,text,varchar,timestamptz,boolean)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_registrar_reconciliacao_nao_encontrada(bigint,varchar,varchar,integer,jsonb,jsonb,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text)') IS NOT NULL)::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_liberar_reprocessamento(bigint,bigint,text,text)') IS NOT NULL)::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_preparar_insert')::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_validar_update')::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_integracao_preparar_insert')::integer
      + EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND n.nspname='desenvolvimento' AND t.tgname='trg_hu_caixa_integracao_imutavel')::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_correlation_id') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_codigo_local') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_op_numero') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_hu_sap_warehouse') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_terminal_ativo') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_status_atualizado_044') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_op') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_integracao_tentativa_claim') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_claim_token') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.uq_hu_caixa_integracao_claim_iniciado') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_integracao_correlation') IS NOT NULL)::integer
      + (to_regclass('desenvolvimento.ix_hu_caixa_integracao_hu_sap') IS NOT NULL)::integer
      + EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE n.nspname='desenvolvimento' AND c.conname='uq_hu_caixa_codigo_correlation')::integer
      INTO v_objects_044;

    SELECT count(*) INTO v_columns_rev5
      FROM (VALUES
          ('hu_caixa_pesagem','origem_pesagem'),
          ('hu_caixa_integracao_sap','sap_messages_sanitizadas')
      ) x(tabela,coluna)
     WHERE EXISTS (
         SELECT 1 FROM information_schema.columns c
          WHERE c.table_schema='desenvolvimento'
            AND c.table_name=x.tabela
            AND c.column_name=x.coluna
     );

    SELECT
        (to_regprocedure('desenvolvimento.fn_hu_caixa_pesagem_normalizar()') IS NOT NULL)::integer
      + EXISTS (
          SELECT 1 FROM pg_trigger t
          JOIN pg_class c ON c.oid=t.tgrelid
          JOIN pg_namespace n ON n.oid=c.relnamespace
          WHERE n.nspname='desenvolvimento'
            AND t.tgname='trg_hu_caixa_pesagem_normalizar'
            AND NOT t.tgisinternal
        )::integer
      + (to_regprocedure('desenvolvimento.fn_hu_caixa_bloquear_configuracao(bigint,bigint,text,text,jsonb)') IS NOT NULL)::integer
      INTO v_objects_rev5;

    IF NOT (
        (v_columns_044 = 0 AND v_objects_044 = 0 AND v_columns_rev5=0 AND v_objects_rev5=0)
        OR (v_columns_044 = 59 AND v_objects_044 = 34 AND v_columns_rev5=2 AND v_objects_rev5=3)
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO_ESTADO_PARCIAL colunas=%/59 objetos=%/34 colunas_rev5=%/2 objetos_rev5=%/3',
            v_columns_044, v_objects_044, v_columns_rev5, v_objects_rev5;
    END IF;

    IF v_columns_044 = 59 AND v_objects_044 = 34 AND v_columns_rev5=2 AND v_objects_rev5=3 THEN
        RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_APROVADO';
        RAISE NOTICE 'ESTADO_PACOTE=PACOTE_REV10_NOMES_PRESENTES_ASSINATURAS_DEVEM_SER_VALIDADAS_PELA_PROPOSTA';
        RETURN;
    END IF;

    SELECT count(*) INTO v_old_status_invalid
      FROM desenvolvimento.hu_caixa
     WHERE status_hu_caixa NOT IN ('PESADA','ETIQUETADA','ENVIADA_SAP','CONFIRMADA_SAP','ERRO_SAP','CANCELADA');

    SELECT count(*) INTO v_duplicates
      FROM (SELECT upper(btrim(hu_caixa)) FROM desenvolvimento.hu_caixa GROUP BY upper(btrim(hu_caixa)) HAVING count(*) > 1) d;

    SELECT count(*) INTO v_view_dependents
      FROM pg_depend d
      JOIN pg_rewrite r ON r.oid=d.objid
      JOIN pg_class dependente ON dependente.oid=r.ev_class
      JOIN pg_namespace nd ON nd.oid=dependente.relnamespace
     WHERE d.refobjid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND NOT (nd.nspname='desenvolvimento' AND dependente.relname='vw_hu_caixas_sem_palete');

    SELECT count(*) INTO v_function_dependents
      FROM pg_proc p
     WHERE p.prokind='f'
       AND lower(pg_get_functiondef(p.oid)) LIKE '%vw_hu_caixas_sem_palete%';

    IF v_view_dependents<>0 OR v_function_dependents<>0 THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO VIEW_VW_HU_CAIXAS_SEM_PALETE_COM_DEPENDENTES views=% funcoes=%; DROP_CASCADE_PROIBIDO',
          v_view_dependents,v_function_dependents;
    END IF;

    SELECT count(*) INTO v_view_column_acl FROM pg_attribute a
     WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND a.attnum>0 AND NOT a.attisdropped AND a.attacl IS NOT NULL;
    SELECT count(*) INTO v_view_column_comments FROM pg_attribute a
     WHERE a.attrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND a.attnum>0 AND NOT a.attisdropped
       AND col_description(a.attrelid,a.attnum) IS NOT NULL;
    SELECT count(*) INTO v_view_extra_rules FROM pg_rewrite r
     WHERE r.ev_class='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND r.rulename<>'_RETURN';
    SELECT count(*) INTO v_view_triggers FROM pg_trigger t
     WHERE t.tgrelid='desenvolvimento.vw_hu_caixas_sem_palete'::regclass
       AND NOT t.tgisinternal;
    IF v_view_column_acl<>0 OR v_view_column_comments<>0 OR v_view_extra_rules<>0 OR v_view_triggers<>0 THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO_VIEW_COM_METADATA_NAO_PRESERVADO column_acl=% column_comments=% extra_rules=% triggers=%',
          v_view_column_acl,v_view_column_comments,v_view_extra_rules,v_view_triggers;
    END IF;

    SELECT count(*) INTO v_dependency_count
      FROM pg_constraint
     WHERE contype='f'
       AND confrelid='desenvolvimento.hu_caixa'::regclass
       AND conrelid NOT IN (
           'desenvolvimento.hu_caixa_pesagem'::regclass,
           'desenvolvimento.hu_caixa_integracao_sap'::regclass,
           'desenvolvimento.hu_caixa_etiqueta'::regclass,
           'desenvolvimento.hu_palete_item'::regclass
       );

    IF v_old_status_invalid > 0 OR v_duplicates > 0 OR v_dependency_count > 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_BLOQUEADO DADOS_OU_DEPENDENCIAS_INCOMPATIVEIS status_invalidos=% duplicidades_hu=% dependencias_externas_inesperadas=%',
            v_old_status_invalid, v_duplicates, v_dependency_count;
    END IF;

    IF v_hu > 0 OR v_hist > 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_APROVADO_COM_PENDENCIAS MIGRACAO_LEGADA_MANUAL_NECESSARIA hu_caixa=% historico_integracao=%',
            v_hu, v_hist;
    END IF;

    SELECT count(*) INTO v_table_privileges
      FROM (VALUES ('desenvolvimento.hu_caixa'),('desenvolvimento.hu_caixa_pesagem'),('desenvolvimento.hu_caixa_integracao_sap')) t(tabela)
      CROSS JOIN (VALUES ('SELECT'),('INSERT'),('UPDATE'),('DELETE'),('TRUNCATE'),('REFERENCES'),('TRIGGER')) p(privilegio)
     WHERE has_table_privilege('fugapet_dev_app',t.tabela,p.privilegio);

    SELECT count(*) INTO v_sequence_privileges
      FROM (VALUES
          (pg_get_serial_sequence('desenvolvimento.hu_caixa','codigo_hu_caixa')),
          (pg_get_serial_sequence('desenvolvimento.hu_caixa_pesagem','codigo_hu_caixa_pesagem')),
          (pg_get_serial_sequence('desenvolvimento.hu_caixa_integracao_sap','codigo_hu_caixa_integracao_sap'))
      ) s(sequence_name)
      CROSS JOIN (VALUES ('USAGE'),('SELECT'),('UPDATE')) p(privilegio)
     WHERE s.sequence_name IS NOT NULL
       AND has_sequence_privilege('fugapet_dev_app',s.sequence_name,p.privilegio);

    IF v_table_privileges <> 0 OR v_sequence_privileges <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_APROVADO_COM_PENDENCIAS MATRIZ_GRANTS_ANTERIOR_NAO_E_VAZIA table_privileges=% sequence_privileges=%',
            v_table_privileges,v_sequence_privileges;
    END IF;

    RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_APROVADO';
    RAISE NOTICE 'ESTADO_PACOTE=PACOTE_REV10_NAO_APLICADO';
    RAISE NOTICE 'CONTAGEM_HU_CAIXA=%',v_hu;
    RAISE NOTICE 'CONTAGEM_HISTORICO_INTEGRACAO=%',v_hist;
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_IDENTIDADE';
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
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_COLUNAS esperado=11 atual=% nomes=%',
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
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_DEPENDENCIAS referencias=%',
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_ESTRUTURA_FILTROS';
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
        RAISE EXCEPTION 'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_PREDICADOS';
    END IF;

    SELECT COALESCE(array_agg(DISTINCT matches[1] ORDER BY matches[1]),ARRAY[]::text[])
      INTO v_literals
      FROM regexp_matches(v_definition,'''([^'']+)''','g') AS rm(matches);

    IF v_literals IS DISTINCT FROM ARRAY[
        'confirmada_sap','confirmado_sap','enviada_sap',
        'enviado_sap','etiquetada','vinculado'
    ]::text[] THEN
        RAISE EXCEPTION
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_STATUS_EXTRAS_OU_AUSENTES status=%',
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
          'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_REPROVADA_METADATA_EXTRA quantidade=%',
          v_forbidden;
    END IF;

    SELECT COALESCE(cardinality(c.relacl),0) INTO v_grants
      FROM pg_class c
     WHERE c.oid=v_view_oid;

    PERFORM * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;
    RAISE NOTICE
      'CLASSIFICACAO_OFICIAL=PREFLIGHT_044_DEV_REV10_VIEW_SEMANTICA_APROVADA owner=% comentario_presente=% grants_explicitos=% colunas=% dependencias=%',
      v_owner,v_comment IS NOT NULL,v_grants,cardinality(v_columns),v_references;
END
$gaia_view_semantic$;
ROLLBACK;

\echo 'PREFLIGHT 044 FINALIZADO - NENHUMA ALTERACAO PERSISTIDA'
