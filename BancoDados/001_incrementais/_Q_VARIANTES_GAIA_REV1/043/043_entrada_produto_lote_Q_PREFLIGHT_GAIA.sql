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
-- ============================================================================
-- 043_entrada_produto_lote_HML_PREFLIGHT_GAIA.sql
-- Projeto: FugaPET_HML
-- Banco esperado: fuga_jales_local_homologacao_q_v1_2
-- Schema esperado: homologacao
-- Escopo: persistencia por lote para Entrada de Materia-Prima.
--
-- SOMENTE LEITURA.
-- NAO EXECUTA DDL OU DML PERSISTENTE.
--
-- Classificacoes:
--   PREFLIGHT_043_HML_APROVADO
--   PREFLIGHT_043_HML_APROVADO
--   PREFLIGHT_043_HML_REPROVADO_PACOTE_PARCIAL
-- ============================================================================

SET search_path TO homologacao, public;

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '3min';

-- ============================================================================
-- DESCOBERTA CONTROLADA DA ROLE DE APLICACAO HML
-- O PREFLIGHT exige uma unica role nao-superusuaria com CONNECT, USAGE no
-- schema e a matriz operacional ja existente nas tres tabelas e sequences
-- base. Nenhum nome de role e presumido.
-- ============================================================================
DO $$
DECLARE
    v_roles text[];
    v_role text;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_DATABASE banco_atual=%',
            current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_SCHEMA';
    END IF;

    IF (
        SELECT count(*)
        FROM information_schema.tables
        WHERE table_schema = 'homologacao'
          AND table_name IN (
              'entrada_produto_lancamento',
              'entrada_produto_item',
              'entrada_produto_pesagem'
          )
    ) <> 3 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL detalhe=tabelas_base';
    END IF;

    SELECT array_agg(r.rolname ORDER BY r.rolname)
      INTO v_roles
      FROM pg_roles r
     WHERE r.rolsuper = false
       AND r.rolname <> current_user
       AND has_database_privilege(
           r.rolname,
           current_database(),
           'CONNECT'
       )
       AND has_schema_privilege(
           r.rolname,
           'homologacao',
           'USAGE'
       )
       AND NOT has_schema_privilege(
           r.rolname,
           'homologacao',
           'CREATE'
       )
       AND NOT EXISTS (
           SELECT 1
           FROM (VALUES
               ('homologacao.entrada_produto_lancamento'),
               ('homologacao.entrada_produto_item'),
               ('homologacao.entrada_produto_pesagem')
           ) AS alvo(tabela)
           WHERE NOT has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'SELECT'
                 )
              OR NOT has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'INSERT'
                 )
              OR NOT has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'UPDATE'
                 )
              OR has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'DELETE'
                 )
              OR has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'TRUNCATE'
                 )
              OR has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'REFERENCES'
                 )
              OR has_table_privilege(
                     r.rolname,
                     alvo.tabela,
                     'TRIGGER'
                 )
       )
       AND (
           WITH seq AS (
               SELECT pg_get_serial_sequence(tabela, coluna)
                   AS sequence_name
               FROM (VALUES
                   ('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
                   ('homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
                   ('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem')
               ) AS alvo(tabela, coluna)
           )
           SELECT count(*)
           FROM seq
           WHERE sequence_name IS NOT NULL
             AND has_sequence_privilege(
                 r.rolname,
                 sequence_name,
                 'USAGE'
             )
             AND has_sequence_privilege(
                 r.rolname,
                 sequence_name,
                 'SELECT'
             )
             AND NOT has_sequence_privilege(
                 r.rolname,
                 sequence_name,
                 'UPDATE'
             )
       ) = 3;

    IF COALESCE(cardinality(v_roles), 0) <> 1 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_GRANTS roles_canonicas=% candidatas=%',
            COALESCE(cardinality(v_roles), 0),
            COALESCE(array_to_string(v_roles, ','), '<nenhuma>');
    END IF;

    v_role := v_roles[1];

    PERFORM set_config(
        'fugapet_q.role_aplicacao',
        v_role,
        true
    );

    RAISE NOTICE
        'ROLE_APLICACAO_Q_RESOLVIDA=%',
        v_role;
END $$;


-- ============================================================================
-- DEPENDENCIAS HISTORICAS E RESIDUOS DE VALIDACAO
-- ============================================================================
DO $$
DECLARE
    v_residuos_lote bigint := 0;
    v_residuos_pesagem bigint := 0;
BEGIN
    IF to_regclass('homologacao.log_alteracao_cadastral') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL detalhe=tabela_auditoria_ausente';
    END IF;

    IF pg_get_serial_sequence(
        'homologacao.log_alteracao_cadastral',
        'codigo_log_alteracao_cadastral'
    ) IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL detalhe=sequence_auditoria_ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger t
        JOIN pg_class c
          ON c.oid = t.tgrelid
        JOIN pg_namespace n
          ON n.oid = c.relnamespace
        WHERE NOT t.tgisinternal
          AND n.nspname = 'homologacao'
          AND c.relname = 'entrada_produto_pesagem'
          AND t.tgname =
              'trg_entrada_produto_pesagem_log_alteracao_cadastral'
          AND t.tgenabled = 'O'
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL detalhe=trigger_auditoria_pesagem_ausente_ou_inativo';
    END IF;

    IF to_regclass('homologacao.entrada_produto_lote') IS NOT NULL THEN
        EXECUTE
            'SELECT count(*) '
            'FROM homologacao.entrada_produto_lote '
            'WHERE codigo_entrada_produto_lote BETWEEN -430099 AND -430001 '
            '   OR correlation_id::text LIKE ''04300000-%'' '
            '   OR criado_por LIKE ''VALIDADOR_043%'''
        INTO v_residuos_lote;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name = 'codigo_entrada_produto_lote'
    ) THEN
        EXECUTE
            'SELECT count(*) '
            'FROM homologacao.entrada_produto_pesagem '
            'WHERE codigo_entrada_produto_lote BETWEEN -430099 AND -430001 '
            '   OR numero_lote_snapshot LIKE ''LT43%'''
        INTO v_residuos_pesagem;
    END IF;

    IF v_residuos_lote <> 0 OR v_residuos_pesagem <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_PACOTE_PARCIAL detalhe=residuos_validacao lotes=% pesagens=%',
            v_residuos_lote,
            v_residuos_pesagem;
    END IF;

    RAISE NOTICE
        'PREFLIGHT 043 HML: trigger de auditoria ativo e residuos de validacao=0.';
END $$;

DO $$
DECLARE
    v_tabelas_base integer;
    v_colunas_base integer;
    v_fks_base integer;
    v_role_count integer;
    v_historico_lancamentos bigint;
    v_historico_itens bigint;
    v_historico_pesagens bigint;

    v_tabela_lote integer;
    v_colunas_pesagem_043 integer;
    v_funcoes_043 integer;
    v_triggers_043 integer;
    v_indices_043 integer;
    v_constraints_lote integer;
    v_constraints_pesagem integer;
    v_homonimos integer;

    v_colunas_lote_conformes integer;
    v_colunas_pesagem_conformes integer;
    v_grants_tabelas_ok boolean;
    v_grants_sequences_ok boolean;
    v_grants_base_normalizar boolean;
    v_owner_ok boolean;
    v_public_privilegios integer;
    v_seq_lancamento text;
    v_seq_item text;
    v_seq_pesagem text;
    v_seq_lote text;
    v_constraints_lote_def_ok integer;
    v_constraints_pesagem_def_ok integer;
    v_indices_def_ok integer;
    v_triggers_def_ok integer;
    v_funcoes_def_ok integer;
    v_estado_objetos integer;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_DATABASE banco_atual=%',
            current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_SCHEMA';
    END IF;

    IF current_setting('server_version_num')::integer < 100000 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL versao=%',
            current_setting('server_version');
    END IF;

    v_role_count := CASE
        WHEN NULLIF(
            current_setting('fugapet_q.role_aplicacao', true),
            ''
        ) IS NULL THEN 0
        ELSE 1
    END;

    IF v_role_count <> 1 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_GRANTS detalhe=role_canonica_nao_resolvida';
    END IF;

    IF NOT has_database_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        current_database(),
        'CONNECT'
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_GRANTS detalhe=connect_ausente';
    END IF;

    IF NOT has_schema_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        'homologacao',
        'USAGE'
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_GRANTS detalhe=usage_schema_ausente';
    END IF;

    SELECT count(*)
      INTO v_tabelas_base
      FROM information_schema.tables
     WHERE table_schema = 'homologacao'
       AND table_name IN (
           'entrada_produto_lancamento',
           'entrada_produto_item',
           'entrada_produto_pesagem'
       );

    IF v_tabelas_base <> 3 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL encontrado=%/3',
            v_tabelas_base;
    END IF;

    WITH esperadas(tabela, coluna, tipo) AS (
        VALUES
            ('entrada_produto_item', 'codigo_entrada_produto_item', 'bigint'),
            ('entrada_produto_pesagem', 'codigo_entrada_produto_pesagem', 'bigint'),
            ('entrada_produto_pesagem', 'codigo_entrada_produto_item', 'bigint'),
            ('entrada_produto_pesagem', 'peso_liquido_kg', 'numeric'),
            ('entrada_produto_pesagem', 'status_pesagem', 'character varying'),
            ('entrada_produto_pesagem', 'situacao_entrada_produto_pesagem', 'boolean')
    )
    SELECT count(*)
      INTO v_colunas_base
      FROM esperadas e
      JOIN information_schema.columns c
        ON c.table_schema = 'homologacao'
       AND c.table_name = e.tabela
       AND c.column_name = e.coluna
       AND c.data_type = e.tipo;

    IF v_colunas_base <> 6 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL conformes=%/6',
            v_colunas_base;
    END IF;

    SELECT count(*)
      INTO v_fks_base
      FROM pg_constraint c
     WHERE c.contype = 'f'
       AND (
           (
               c.conrelid = 'homologacao.entrada_produto_item'::regclass
               AND c.confrelid =
                   'homologacao.entrada_produto_lancamento'::regclass
               AND c.confdeltype = 'r'
           )
           OR
           (
               c.conrelid = 'homologacao.entrada_produto_pesagem'::regclass
               AND c.confrelid =
                   'homologacao.entrada_produto_item'::regclass
               AND c.confdeltype = 'r'
           )
       );

    IF v_fks_base <> 2 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL encontradas=%/2',
            v_fks_base;
    END IF;

    v_seq_lancamento := pg_get_serial_sequence(
        'homologacao.entrada_produto_lancamento',
        'codigo_entrada_produto_lancamento'
    );
    v_seq_item := pg_get_serial_sequence(
        'homologacao.entrada_produto_item',
        'codigo_entrada_produto_item'
    );
    v_seq_pesagem := pg_get_serial_sequence(
        'homologacao.entrada_produto_pesagem',
        'codigo_entrada_produto_pesagem'
    );

    IF v_seq_lancamento IS NULL
       OR v_seq_item IS NULL
       OR v_seq_pesagem IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_BASE_INCOMPATIVEL';
    END IF;

    RAISE NOTICE
        'GRANTS_LANCAMENTO|SELECT=%|INSERT=%|UPDATE=%|DELETE=%|TRUNCATE=%|REFERENCES=%|TRIGGER=%',
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'SELECT'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'INSERT'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'UPDATE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'DELETE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'TRUNCATE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'REFERENCES'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'TRIGGER');

    RAISE NOTICE
        'GRANTS_ITEM|SELECT=%|INSERT=%|UPDATE=%|DELETE=%|TRUNCATE=%|REFERENCES=%|TRIGGER=%',
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'SELECT'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'INSERT'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'UPDATE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'DELETE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'TRUNCATE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'REFERENCES'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'TRIGGER');

    RAISE NOTICE
        'GRANTS_PESAGEM|SELECT=%|INSERT=%|UPDATE=%|DELETE=%|TRUNCATE=%|REFERENCES=%|TRIGGER=%',
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'SELECT'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'INSERT'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'UPDATE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'DELETE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'TRUNCATE'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'REFERENCES'),
        has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'TRIGGER');

    RAISE NOTICE
        'GRANTS_SEQUENCE_LANCAMENTO|SEQUENCE=%|USAGE=%|SELECT=%|UPDATE=%',
        v_seq_lancamento,
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'USAGE'),
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'SELECT'),
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'UPDATE');

    RAISE NOTICE
        'GRANTS_SEQUENCE_ITEM|SEQUENCE=%|USAGE=%|SELECT=%|UPDATE=%',
        v_seq_item,
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'USAGE'),
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'SELECT'),
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'UPDATE');

    RAISE NOTICE
        'GRANTS_SEQUENCE_PESAGEM|SEQUENCE=%|USAGE=%|SELECT=%|UPDATE=%',
        v_seq_pesagem,
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'USAGE'),
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'SELECT'),
        has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'UPDATE');

    v_grants_base_normalizar :=
        NOT (
            has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'SELECT')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'INSERT')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_lancamento', 'UPDATE')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'SELECT')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'INSERT')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_item', 'UPDATE')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'SELECT')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'INSERT')
            AND has_table_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao.entrada_produto_pesagem', 'UPDATE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'SELECT')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'SELECT')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'SELECT')
        );

    IF v_grants_base_normalizar THEN
        RAISE NOTICE 'GRANTS_BASE_A_NORMALIZAR_PELO_043';
    ELSE
        RAISE NOTICE 'GRANTS_BASE_JA_CONFORMES';
    END IF;

    RAISE NOTICE 'GRANTS_SERAO_NORMALIZADOS_PELA_PROPOSTA=SIM';

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('homologacao.entrada_produto_lancamento'),
            ('homologacao.entrada_produto_item'),
            ('homologacao.entrada_produto_pesagem')
        ) AS alvo(tabela)
        WHERE has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'DELETE')
           OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'TRUNCATE')
           OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'REFERENCES')
           OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'TRIGGER')
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_GRANTS';
    END IF;

    IF has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'UPDATE')
       OR has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'UPDATE')
       OR has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'UPDATE') THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_GRANTS';
    END IF;

    SELECT count(*) INTO v_historico_lancamentos
    FROM homologacao.entrada_produto_lancamento;

    SELECT count(*) INTO v_historico_itens
    FROM homologacao.entrada_produto_item;

    SELECT count(*) INTO v_historico_pesagens
    FROM homologacao.entrada_produto_pesagem;

    SELECT count(*)
      INTO v_homonimos
      FROM pg_class c
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE c.relname = 'entrada_produto_lote'
       AND n.nspname <> 'homologacao'
       AND n.nspname NOT IN ('pg_catalog', 'information_schema')
       AND n.nspname NOT LIKE 'pg_toast%';

    IF v_homonimos <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_PACOTE_PARCIAL';
    END IF;

    SELECT count(*)
      INTO v_tabela_lote
      FROM information_schema.tables
     WHERE table_schema = 'homologacao'
       AND table_name = 'entrada_produto_lote';

    SELECT count(*)
      INTO v_colunas_pesagem_043
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'entrada_produto_pesagem'
       AND column_name IN (
           'codigo_entrada_produto_lote',
           'numero_lote_snapshot',
           'data_fabricacao_snapshot',
           'data_vencimento_snapshot'
       );

    SELECT count(*)
      INTO v_funcoes_043
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname IN (
           'fn_entrada_produto_lote_proteger_update',
           'fn_entrada_produto_pesagem_validar_lote_snapshot',
           'fn_entrada_produto_lote_recalcular_peso',
           'fn_entrada_produto_pesagem_recalcular_lote'
       );

    SELECT count(*)
      INTO v_triggers_043
      FROM pg_trigger t
      JOIN pg_class rel ON rel.oid = t.tgrelid
      JOIN pg_namespace n ON n.oid = rel.relnamespace
     WHERE NOT t.tgisinternal
       AND n.nspname = 'homologacao'
       AND rel.relname IN (
           'entrada_produto_lote',
           'entrada_produto_pesagem'
       )
       AND t.tgname IN (
           'trg_entrada_produto_lote_proteger_update',
           'trg_entrada_produto_pesagem_validar_lote',
           'trg_entrada_produto_pesagem_recalcular_lote'
       );

    SELECT count(*)
      INTO v_indices_043
      FROM pg_indexes
     WHERE schemaname = 'homologacao'
       AND indexname IN (
           'uq_entrada_lote_ativo_item_lote_datas',
           'uq_entrada_lote_documento_confirmado',
           'ix_entrada_lote_item',
           'ix_entrada_lote_status',
           'ix_entrada_lote_numero',
           'ix_entrada_lote_pendente_envio',
           'ix_entrada_lote_erro_sap',
           'ix_entrada_pesagem_lote'
       );

    SELECT count(*)
      INTO v_constraints_lote
      FROM pg_constraint c
     WHERE c.conrelid = to_regclass('homologacao.entrada_produto_lote')
       AND c.conname IN (
           'pk_entrada_produto_lote',
           'fk_entrada_lote_item',
           'uq_entrada_lote_correlation_id',
           'ck_entrada_lote_numero_nao_vazio',
           'ck_entrada_lote_datas',
           'ck_entrada_lote_peso_total',
           'ck_entrada_lote_status',
           'ck_entrada_lote_documento_conjunto',
           'ck_entrada_lote_confirmado_sap_documento',
           'ck_entrada_lote_erro_sap',
           'ck_entrada_lote_quantidade_retorno',
           'ck_entrada_lote_criado_por',
           'ck_entrada_lote_alterado_por',
           'ck_entrada_lote_enviado_em',
           'ck_entrada_lote_confirmado_em',
           'ck_entrada_lote_payload_sem_credencial'
       );

    SELECT count(*)
      INTO v_constraints_pesagem
      FROM pg_constraint c
     WHERE c.conrelid =
           'homologacao.entrada_produto_pesagem'::regclass
       AND c.conname IN (
           'fk_entrada_pesagem_lote',
           'ck_entrada_pesagem_lote_snapshot_conjunto'
       );

    v_estado_objetos :=
        v_tabela_lote
        + v_colunas_pesagem_043
        + v_funcoes_043
        + v_triggers_043
        + v_indices_043
        + v_constraints_lote
        + v_constraints_pesagem;

    IF v_estado_objetos = 0 THEN
        RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_APROVADO';
        RAISE NOTICE 'PREFLIGHT 043 HML OK: estado=PACOTE_NAO_APLICADO role=%.', current_setting('fugapet_q.role_aplicacao');
        RAISE NOTICE
            'Historico preservado: lancamentos=%, itens=%, pesagens=%.',
            v_historico_lancamentos,
            v_historico_itens,
            v_historico_pesagens;
        RETURN;
    END IF;

    IF v_tabela_lote = 1 THEN
        WITH esperadas(
            coluna,
            tipo,
            tamanho,
            precisao,
            escala,
            nullable,
            identity_flag,
            identity_generation,
            regra_default
        ) AS (
            VALUES
                ('codigo_entrada_produto_lote', 'bigint', NULL::integer, NULL::integer, NULL::integer, 'NO', 'YES', 'BY DEFAULT', 'NULL'),
                ('codigo_entrada_produto_item', 'bigint', NULL, NULL, NULL, 'NO', 'NO', NULL, 'NULL'),
                ('numero_lote', 'character varying', 10, NULL, NULL, 'NO', 'NO', NULL, 'NULL'),
                ('data_fabricacao', 'date', NULL, NULL, NULL, 'NO', 'NO', NULL, 'NULL'),
                ('data_vencimento', 'date', NULL, NULL, NULL, 'NO', 'NO', NULL, 'NULL'),
                ('peso_liquido_total_kg', 'numeric', NULL, 14, 3, 'NO', 'NO', NULL, 'ZERO'),
                ('status_lote', 'character varying', 30, NULL, NULL, 'NO', 'NO', NULL, 'AGUARDANDO'),
                ('correlation_id', 'uuid', NULL, NULL, NULL, 'NO', 'NO', NULL, 'NULL'),
                ('documento_material_sap', 'character varying', 20, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('exercicio_documento_material_sap', 'character varying', 4, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('documento_material_item', 'character varying', 10, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('batch_retornado_sap', 'character varying', 10, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('quantidade_retorno_sap', 'numeric', NULL, 14, 3, 'YES', 'NO', NULL, 'NULL'),
                ('payload_enviado_json', 'jsonb', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('resposta_sap_json', 'jsonb', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('erro_sanitizado', 'text', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('criado_em', 'timestamp with time zone', NULL, NULL, NULL, 'NO', 'NO', NULL, 'CLOCK'),
                ('criado_por', 'character varying', 80, NULL, NULL, 'NO', 'NO', NULL, 'NULL'),
                ('confirmado_em', 'timestamp with time zone', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('enviado_sap_em', 'timestamp with time zone', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('confirmado_sap_em', 'timestamp with time zone', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('alterado_em', 'timestamp with time zone', NULL, NULL, NULL, 'YES', 'NO', NULL, 'NULL'),
                ('alterado_por', 'character varying', 80, NULL, NULL, 'YES', 'NO', NULL, 'NULL')
        )
        SELECT count(*)
          INTO v_colunas_lote_conformes
          FROM esperadas e
          JOIN information_schema.columns c
            ON c.table_schema = 'homologacao'
           AND c.table_name = 'entrada_produto_lote'
           AND c.column_name = e.coluna
           AND c.data_type = e.tipo
           AND (e.tamanho IS NULL OR c.character_maximum_length = e.tamanho)
           AND (e.precisao IS NULL OR c.numeric_precision = e.precisao)
           AND (e.escala IS NULL OR c.numeric_scale = e.escala)
           AND c.is_nullable = e.nullable
           AND c.is_identity = e.identity_flag
           AND COALESCE(c.identity_generation, '') = COALESCE(e.identity_generation, '')
           AND CASE e.regra_default
               WHEN 'NULL' THEN c.column_default IS NULL
               WHEN 'ZERO' THEN lower(COALESCE(c.column_default, '')) ~ '(^|[^0-9])0([^0-9]|$)'
               WHEN 'AGUARDANDO' THEN position('aguardando_dados' in lower(COALESCE(c.column_default, ''))) > 0
               WHEN 'CLOCK' THEN position('clock_timestamp()' in lower(COALESCE(c.column_default, ''))) > 0
               ELSE false
           END;
    ELSE
        v_colunas_lote_conformes := 0;
    END IF;

    WITH esperadas(coluna, tipo, tamanho, nullable) AS (
        VALUES
            ('codigo_entrada_produto_lote', 'bigint', NULL::integer, 'YES'),
            ('numero_lote_snapshot', 'character varying', 10, 'YES'),
            ('data_fabricacao_snapshot', 'date', NULL, 'YES'),
            ('data_vencimento_snapshot', 'date', NULL, 'YES')
    )
    SELECT count(*)
      INTO v_colunas_pesagem_conformes
      FROM esperadas e
      JOIN information_schema.columns c
        ON c.table_schema = 'homologacao'
       AND c.table_name = 'entrada_produto_pesagem'
       AND c.column_name = e.coluna
       AND c.data_type = e.tipo
       AND (e.tamanho IS NULL OR c.character_maximum_length = e.tamanho)
       AND c.is_nullable = e.nullable
       AND c.is_identity = 'NO'
       AND c.column_default IS NULL;


    v_constraints_lote_def_ok := 0;
    v_constraints_pesagem_def_ok := 0;
    v_indices_def_ok := 0;
    v_triggers_def_ok := 0;
    v_funcoes_def_ok := 0;

    IF v_tabela_lote = 1 THEN
        WITH defs AS (
            SELECT
                c.*,
                lower(regexp_replace(
                    pg_get_constraintdef(c.oid, true),
                    '[[:space:]]+',
                    ' ',
                    'g'
                )) AS def
            FROM pg_constraint c
            WHERE c.conrelid =
                  'homologacao.entrada_produto_lote'::regclass
        )
        SELECT count(*)
          INTO v_constraints_lote_def_ok
          FROM defs d
         WHERE
            (d.conname = 'pk_entrada_produto_lote'
             AND d.contype = 'p'
             AND position('primary key' in d.def) > 0
             AND position('codigo_entrada_produto_lote' in d.def) > 0)
         OR (d.conname = 'fk_entrada_lote_item'
             AND d.contype = 'f'
             AND d.confrelid = 'homologacao.entrada_produto_item'::regclass
             AND d.confdeltype = 'r'
             AND position('codigo_entrada_produto_item' in d.def) > 0
             AND position('on delete restrict' in d.def) > 0)
         OR (d.conname = 'uq_entrada_lote_correlation_id'
             AND d.contype = 'u'
             AND position('unique' in d.def) > 0
             AND position('correlation_id' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_numero_nao_vazio'
             AND d.contype = 'c'
             AND position('numero_lote' in d.def) > 0
             AND position('btrim' in d.def) > 0
             AND position('length' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_datas'
             AND d.contype = 'c'
             AND position('data_vencimento' in d.def) > 0
             AND position('data_fabricacao' in d.def) > 0
             AND position('>=' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_peso_total'
             AND d.contype = 'c'
             AND position('peso_liquido_total_kg' in d.def) > 0
             AND position('>=' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_status'
             AND d.contype = 'c'
             AND position('status_lote' in d.def) > 0
             AND position('aguardando_dados' in d.def) > 0
             AND position('lote_confirmado' in d.def) > 0
             AND position('pesando' in d.def) > 0
             AND position('finalizado_local' in d.def) > 0
             AND position('enviando_sap' in d.def) > 0
             AND position('confirmado_sap' in d.def) > 0
             AND position('erro_sap' in d.def) > 0
             AND position('cancelado' in d.def) > 0
             AND (SELECT count(*) FROM regexp_matches(d.def, '''[a-z_]+''', 'g')) = 8)
         OR (d.conname = 'ck_entrada_lote_documento_conjunto'
             AND d.contype = 'c'
             AND position('documento_material_sap' in d.def) > 0
             AND position('exercicio_documento_material_sap' in d.def) > 0
             AND position('documento_material_item' in d.def) > 0
             AND position('is null' in d.def) > 0
             AND position('is not null' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_confirmado_sap_documento'
             AND d.contype = 'c'
             AND position('confirmado_sap' in d.def) > 0
             AND position('documento_material_sap' in d.def) > 0
             AND position('documento_material_item' in d.def) > 0
             AND position('confirmado_sap_em' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_erro_sap'
             AND d.contype = 'c'
             AND position('erro_sap' in d.def) > 0
             AND position('erro_sanitizado' in d.def) > 0
             AND position('btrim' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_quantidade_retorno'
             AND d.contype = 'c'
             AND position('quantidade_retorno_sap' in d.def) > 0
             AND position('>=' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_criado_por'
             AND d.contype = 'c'
             AND position('criado_por' in d.def) > 0
             AND position('btrim' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_alterado_por'
             AND d.contype = 'c'
             AND position('alterado_por' in d.def) > 0
             AND position('is null' in d.def) > 0
             AND position('btrim' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_enviado_em'
             AND d.contype = 'c'
             AND position('enviando_sap' in d.def) > 0
             AND position('confirmado_sap' in d.def) > 0
             AND position('enviado_sap_em' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_confirmado_em'
             AND d.contype = 'c'
             AND position('confirmado_em' in d.def) > 0
             AND position('aguardando_dados' in d.def) > 0
             AND position('cancelado' in d.def) > 0)
         OR (d.conname = 'ck_entrada_lote_payload_sem_credencial'
             AND d.contype = 'c'
             AND position('payload_enviado_json' in d.def) > 0
             AND position('authorization' in d.def) > 0
             AND position('password' in d.def) > 0
             AND position('token' in d.def) > 0
             AND position('connection_string' in d.def) > 0);

        WITH defs AS (
            SELECT
                c.*,
                lower(regexp_replace(
                    pg_get_constraintdef(c.oid, true),
                    '[[:space:]]+',
                    ' ',
                    'g'
                )) AS def
            FROM pg_constraint c
            WHERE c.conrelid =
                  'homologacao.entrada_produto_pesagem'::regclass
        )
        SELECT count(*)
          INTO v_constraints_pesagem_def_ok
          FROM defs d
         WHERE
            (d.conname = 'fk_entrada_pesagem_lote'
             AND d.contype = 'f'
             AND d.confrelid = 'homologacao.entrada_produto_lote'::regclass
             AND d.confdeltype = 'r'
             AND position('codigo_entrada_produto_lote' in d.def) > 0
             AND position('on delete restrict' in d.def) > 0)
         OR (d.conname = 'ck_entrada_pesagem_lote_snapshot_conjunto'
             AND d.contype = 'c'
             AND position('codigo_entrada_produto_lote' in d.def) > 0
             AND position('numero_lote_snapshot' in d.def) > 0
             AND position('data_fabricacao_snapshot' in d.def) > 0
             AND position('data_vencimento_snapshot' in d.def) > 0
             AND position('btrim' in d.def) > 0
             AND position('>=' in d.def) > 0);

        WITH idx AS (
            SELECT
                ci.relname AS indexname,
                ct.relname AS tablename,
                i.indisunique,
                i.indnkeyatts,
                lower(regexp_replace(
                    pg_get_indexdef(ci.oid),
                    '[[:space:]]+',
                    ' ',
                    'g'
                )) AS def,
                lower(COALESCE(pg_get_expr(i.indpred, i.indrelid), '')) AS pred
            FROM pg_index i
            JOIN pg_class ci ON ci.oid = i.indexrelid
            JOIN pg_class ct ON ct.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = ct.relnamespace
            WHERE n.nspname = 'homologacao'
              AND ct.relname IN ('entrada_produto_lote', 'entrada_produto_pesagem')
              AND ci.relname IN (
                  'uq_entrada_lote_ativo_item_lote_datas',
                  'uq_entrada_lote_documento_confirmado',
                  'ix_entrada_lote_item',
                  'ix_entrada_lote_status',
                  'ix_entrada_lote_numero',
                  'ix_entrada_lote_pendente_envio',
                  'ix_entrada_lote_erro_sap',
                  'ix_entrada_pesagem_lote'
              )
        )
        SELECT count(*)
          INTO v_indices_def_ok
          FROM idx x
         WHERE
            (x.indexname = 'uq_entrada_lote_ativo_item_lote_datas'
             AND x.tablename = 'entrada_produto_lote'
             AND x.indisunique
             AND x.indnkeyatts = 4
             AND position('codigo_entrada_produto_item' in x.def) > 0
             AND position('upper' in x.def) > 0
             AND position('btrim' in x.def) > 0
             AND position('numero_lote' in x.def) > 0
             AND position('data_fabricacao' in x.def) > 0
             AND position('data_vencimento' in x.def) > 0
             AND position('status_lote' in x.pred) > 0
             AND position('cancelado' in x.pred) > 0)
         OR (x.indexname = 'uq_entrada_lote_documento_confirmado'
             AND x.tablename = 'entrada_produto_lote'
             AND x.indisunique
             AND x.indnkeyatts = 2
             AND position('documento_material_sap' in x.def) > 0
             AND position('exercicio_documento_material_sap' in x.def) > 0
             AND position('documento_material_item' in x.def) = 0
             AND position('status_lote' in x.pred) > 0
             AND position('confirmado_sap' in x.pred) > 0)
         OR (x.indexname = 'ix_entrada_lote_item'
             AND x.tablename = 'entrada_produto_lote'
             AND NOT x.indisunique
             AND position('codigo_entrada_produto_item' in x.def) > 0)
         OR (x.indexname = 'ix_entrada_lote_status'
             AND x.tablename = 'entrada_produto_lote'
             AND NOT x.indisunique
             AND position('status_lote' in x.def) > 0
             AND position('criado_em' in x.def) > 0)
         OR (x.indexname = 'ix_entrada_lote_numero'
             AND x.tablename = 'entrada_produto_lote'
             AND NOT x.indisunique
             AND position('upper' in x.def) > 0
             AND position('btrim' in x.def) > 0
             AND position('numero_lote' in x.def) > 0)
         OR (x.indexname = 'ix_entrada_lote_pendente_envio'
             AND x.tablename = 'entrada_produto_lote'
             AND NOT x.indisunique
             AND position('criado_em' in x.def) > 0
             AND position('codigo_entrada_produto_lote' in x.def) > 0
             AND position('finalizado_local' in x.pred) > 0
             AND position('erro_sap' in x.pred) > 0)
         OR (x.indexname = 'ix_entrada_lote_erro_sap'
             AND x.tablename = 'entrada_produto_lote'
             AND NOT x.indisunique
             AND position('alterado_em' in x.def) > 0
             AND position('codigo_entrada_produto_lote' in x.def) > 0
             AND position('erro_sap' in x.pred) > 0)
         OR (x.indexname = 'ix_entrada_pesagem_lote'
             AND x.tablename = 'entrada_produto_pesagem'
             AND NOT x.indisunique
             AND position('codigo_entrada_produto_lote' in x.def) > 0
             AND position('codigo_entrada_produto_lote' in x.pred) > 0
             AND position('is not null' in x.pred) > 0);

        WITH trg AS (
            SELECT
                t.tgname,
                rel.relname AS tablename,
                t.tgtype,
                t.tgfoid,
                lower(regexp_replace(
                    pg_get_triggerdef(t.oid, true),
                    '[[:space:]]+',
                    ' ',
                    'g'
                )) AS def
            FROM pg_trigger t
            JOIN pg_class rel ON rel.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = rel.relnamespace
            WHERE NOT t.tgisinternal
              AND n.nspname = 'homologacao'
              AND rel.relname IN ('entrada_produto_lote', 'entrada_produto_pesagem')
              AND t.tgname IN (
                  'trg_entrada_produto_lote_proteger_update',
                  'trg_entrada_produto_pesagem_validar_lote',
                  'trg_entrada_produto_pesagem_recalcular_lote'
              )
        )
        SELECT count(*)
          INTO v_triggers_def_ok
          FROM trg t
         WHERE
            (t.tgname = 'trg_entrada_produto_lote_proteger_update'
             AND t.tablename = 'entrada_produto_lote'
             AND t.tgtype = 23
             AND t.tgfoid = 'homologacao.fn_entrada_produto_lote_proteger_update()'::regprocedure
             AND position('before insert or update' in t.def) > 0)
         OR (t.tgname = 'trg_entrada_produto_pesagem_validar_lote'
             AND t.tablename = 'entrada_produto_pesagem'
             AND t.tgtype = 31
             AND t.tgfoid = 'homologacao.fn_entrada_produto_pesagem_validar_lote_snapshot()'::regprocedure
             AND position('before' in t.def) > 0
             AND position('insert' in t.def) > 0
             AND position('update' in t.def) > 0
             AND position('delete' in t.def) > 0)
         OR (t.tgname = 'trg_entrada_produto_pesagem_recalcular_lote'
             AND t.tablename = 'entrada_produto_pesagem'
             AND t.tgtype = 29
             AND t.tgfoid = 'homologacao.fn_entrada_produto_pesagem_recalcular_lote()'::regprocedure
             AND position('after' in t.def) > 0
             AND position('insert' in t.def) > 0
             AND position('delete' in t.def) > 0
             AND position('update of codigo_entrada_produto_lote, peso_liquido_kg, status_pesagem, situacao_entrada_produto_pesagem' in t.def) > 0);

        WITH fun AS (
            SELECT
                p.*,
                l.lanname,
                r.rolname AS owner_name,
                lower(regexp_replace(
                    pg_get_functiondef(p.oid),
                    '[[:space:]]+',
                    ' ',
                    'g'
                )) AS def,
                EXISTS (
                    SELECT 1
                    FROM unnest(COALESCE(p.proconfig, ARRAY[]::text[])) cfg
                    WHERE replace(lower(cfg), ' ', '') =
                          'search_path=homologacao,pg_temp'
                ) AS search_path_ok,
                NOT EXISTS (
                    SELECT 1
                    FROM aclexplode(COALESCE(
                        p.proacl,
                        acldefault('f', p.proowner)
                    )) a
                    WHERE a.grantee = 0
                      AND a.privilege_type = 'EXECUTE'
                ) AS public_execute_revogado
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            JOIN pg_language l ON l.oid = p.prolang
            JOIN pg_roles r ON r.oid = p.proowner
            WHERE n.nspname = 'homologacao'
              AND p.proname IN (
                  'fn_entrada_produto_lote_proteger_update',
                  'fn_entrada_produto_pesagem_validar_lote_snapshot',
                  'fn_entrada_produto_lote_recalcular_peso',
                  'fn_entrada_produto_pesagem_recalcular_lote'
              )
        )
        SELECT count(*)
          INTO v_funcoes_def_ok
          FROM fun f
         WHERE f.lanname = 'plpgsql'
           AND f.owner_name <> current_setting('fugapet_q.role_aplicacao')
           AND f.search_path_ok
           AND f.public_execute_revogado
           AND (
              (f.proname = 'fn_entrada_produto_lote_proteger_update'
               AND f.pronargs = 0
               AND f.prorettype = 'trigger'::regtype
               AND NOT f.prosecdef
               AND position('tg_op = ''insert''' in f.def) > 0
               AND position('peso_liquido_total_kg is distinct from 0' in f.def) > 0
               AND position('pg_trigger_depth() <= 1' in f.def) > 0
               AND position('new.alterado_em := clock_timestamp()' in f.def) > 0)
           OR (f.proname = 'fn_entrada_produto_pesagem_validar_lote_snapshot'
               AND f.pronargs = 0
               AND f.prorettype = 'trigger'::regtype
               AND NOT f.prosecdef
               AND position('snapshots completos sao obrigatorios' in f.def) > 0
               AND position('lote pertence a outro item' in f.def) > 0
               AND position('enviando_sap' in f.def) > 0
               AND position('confirmado_sap' in f.def) > 0
               AND position(
                   'if tg_op = ''update'' and old.codigo_entrada_produto_lote is not null'
                   in f.def
               ) > 0
               AND position(
                   'if new.codigo_entrada_produto_lote is null'
                   in f.def
               ) > 0
               AND position(
                   'if tg_op = ''update'' and old.codigo_entrada_produto_lote is not null'
                   in f.def
               ) < position(
                   'if new.codigo_entrada_produto_lote is null'
                   in f.def
               )
               AND position(
                   'vinculo e snapshots de lote sao imutaveis'
                   in f.def
               ) > 0)
           OR (f.proname = 'fn_entrada_produto_lote_recalcular_peso'
               AND f.pronargs = 1
               AND f.proargtypes = '20'::oidvector
               AND f.prorettype = 'void'::regtype
               AND f.prosecdef
               AND position('sum(p.peso_liquido_kg)' in f.def) > 0
               AND position('status_pesagem = ''valida''' in f.def) > 0)
           OR (f.proname = 'fn_entrada_produto_pesagem_recalcular_lote'
               AND f.pronargs = 0
               AND f.prorettype = 'trigger'::regtype
               AND f.prosecdef
               AND position('fn_entrada_produto_lote_recalcular_peso' in f.def) > 0)
           );
    END IF;

    v_grants_tabelas_ok := false;
    v_grants_sequences_ok := false;
    v_public_privilegios := 0;
    v_owner_ok := false;

    IF v_tabela_lote = 1 THEN
        v_seq_lote := pg_get_serial_sequence(
            'homologacao.entrada_produto_lote',
            'codigo_entrada_produto_lote'
        );

        v_grants_tabelas_ok := NOT EXISTS (
            SELECT 1
            FROM (VALUES
                ('homologacao.entrada_produto_lancamento'),
                ('homologacao.entrada_produto_item'),
                ('homologacao.entrada_produto_pesagem'),
                ('homologacao.entrada_produto_lote')
            ) AS alvo(tabela)
            WHERE NOT has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'SELECT')
               OR NOT has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'INSERT')
               OR NOT has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'UPDATE')
               OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'DELETE')
               OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'TRUNCATE')
               OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'REFERENCES')
               OR has_table_privilege(current_setting('fugapet_q.role_aplicacao'), alvo.tabela, 'TRIGGER')
        );

        v_grants_sequences_ok :=
            v_seq_lancamento IS NOT NULL
            AND v_seq_item IS NOT NULL
            AND v_seq_pesagem IS NOT NULL
            AND v_seq_lote IS NOT NULL
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'SELECT')
            AND NOT has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lancamento, 'UPDATE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'SELECT')
            AND NOT has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_item, 'UPDATE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'SELECT')
            AND NOT has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_pesagem, 'UPDATE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lote, 'USAGE')
            AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lote, 'SELECT')
            AND NOT has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), v_seq_lote, 'UPDATE');

        SELECT count(*)
          INTO v_public_privilegios
          FROM pg_class c
          JOIN pg_namespace n ON n.oid = c.relnamespace
          CROSS JOIN LATERAL aclexplode(
              COALESCE(c.relacl, '{}'::aclitem[])
          ) a
         WHERE n.nspname = 'homologacao'
           AND c.oid IN (
               'homologacao.entrada_produto_lancamento'::regclass,
               'homologacao.entrada_produto_item'::regclass,
               'homologacao.entrada_produto_pesagem'::regclass,
               'homologacao.entrada_produto_lote'::regclass,
               v_seq_lancamento::regclass,
               v_seq_item::regclass,
               v_seq_pesagem::regclass,
               v_seq_lote::regclass
           )
           AND a.grantee = 0;

        SELECT count(*) = 8
          INTO v_owner_ok
          FROM pg_class c
          JOIN pg_roles r ON r.oid = c.relowner
         WHERE c.oid IN (
               'homologacao.entrada_produto_lancamento'::regclass,
               'homologacao.entrada_produto_item'::regclass,
               'homologacao.entrada_produto_pesagem'::regclass,
               'homologacao.entrada_produto_lote'::regclass,
               v_seq_lancamento::regclass,
               v_seq_item::regclass,
               v_seq_pesagem::regclass,
               v_seq_lote::regclass
           )
           AND r.rolname <> current_setting('fugapet_q.role_aplicacao');
    END IF;

    IF v_tabela_lote = 1
       AND v_colunas_lote_conformes = 23
       AND v_colunas_pesagem_conformes = 4
       AND v_constraints_lote = 16
       AND v_constraints_pesagem = 2
       AND v_constraints_lote_def_ok = 16
       AND v_constraints_pesagem_def_ok = 2
       AND v_funcoes_043 = 4
       AND v_funcoes_def_ok = 4
       AND v_triggers_043 = 3
       AND v_triggers_def_ok = 3
       AND v_indices_043 = 8
       AND v_indices_def_ok = 8
       AND v_grants_tabelas_ok
       AND v_grants_sequences_ok
       AND v_public_privilegios = 0
       AND v_owner_ok THEN

        RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_APROVADO';
        RAISE NOTICE 'PREFLIGHT 043 HML OK: estado=PACOTE_COMPLETAMENTE_APLICADO role=%.', current_setting('fugapet_q.role_aplicacao');
        RETURN;
    END IF;

    RAISE EXCEPTION
        'CLASSIFICACAO_OFICIAL=PREFLIGHT_043_HML_REPROVADO_PACOTE_PARCIAL tabela_lote=% colunas_lote=%/23 colunas_pesagem=%/4 constraints_lote=%/16 defs=%/16 constraints_pesagem=%/2 defs=%/2 funcoes=%/4 defs=%/4 triggers=%/3 defs=%/3 indices=%/8 defs=%/8 grants_tabelas=% grants_sequences=% public_privilegios=% owner_ok=%',
        v_tabela_lote,
        v_colunas_lote_conformes,
        v_colunas_pesagem_conformes,
        v_constraints_lote,
        v_constraints_lote_def_ok,
        v_constraints_pesagem,
        v_constraints_pesagem_def_ok,
        v_funcoes_043,
        v_funcoes_def_ok,
        v_triggers_043,
        v_triggers_def_ok,
        v_indices_043,
        v_indices_def_ok,
        v_grants_tabelas_ok,
        v_grants_sequences_ok,
        v_public_privilegios,
        v_owner_ok;
END $$;

\echo 'PREFLIGHT 043 - AMBIENTE'
SELECT
    current_database() AS banco,
    current_user AS usuario,
    current_setting('server_version') AS postgresql,
    current_schema() AS schema_atual,
    to_regnamespace('homologacao') AS schema_hml;

\echo 'PREFLIGHT 043 - HISTORICO DA ENTRADA'
SELECT
    (SELECT count(*) FROM homologacao.entrada_produto_lancamento)
        AS lancamentos,
    (SELECT count(*) FROM homologacao.entrada_produto_item)
        AS itens,
    (SELECT count(*) FROM homologacao.entrada_produto_pesagem)
        AS pesagens;

\echo 'PREFLIGHT 043 - COLUNAS ATUAIS DA PESAGEM'
SELECT
    column_name,
    data_type,
    character_maximum_length,
    numeric_precision,
    numeric_scale,
    is_nullable,
    column_default,
    is_identity
FROM information_schema.columns
WHERE table_schema = 'homologacao'
  AND table_name = 'entrada_produto_pesagem'
ORDER BY ordinal_position;

\echo 'PREFLIGHT 043 - OBJETOS DO PACOTE'
SELECT
    to_regclass('homologacao.entrada_produto_lote')
        AS entrada_produto_lote,
    (
        SELECT count(*)
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name IN (
              'codigo_entrada_produto_lote',
              'numero_lote_snapshot',
              'data_fabricacao_snapshot',
              'data_vencimento_snapshot'
          )
    ) AS colunas_pesagem_043;


\echo 'PREFLIGHT 043 - DEFINICOES REAIS DE CONSTRAINTS'
SELECT
    c.conname,
    c.contype,
    c.confdeltype,
    pg_get_constraintdef(c.oid, true) AS definicao
FROM pg_constraint c
WHERE c.conrelid IN (
    to_regclass('homologacao.entrada_produto_lote'),
    'homologacao.entrada_produto_pesagem'::regclass
)
  AND c.conname LIKE '%entrada%lote%'
ORDER BY c.conrelid::regclass::text, c.conname;

\echo 'PREFLIGHT 043 - DEFINICOES REAIS DE INDICES'
SELECT
    ci.relname AS indice,
    ct.relname AS tabela,
    i.indisunique,
    pg_get_indexdef(ci.oid) AS definicao,
    pg_get_expr(i.indpred, i.indrelid) AS predicado
FROM pg_index i
JOIN pg_class ci ON ci.oid = i.indexrelid
JOIN pg_class ct ON ct.oid = i.indrelid
JOIN pg_namespace n ON n.oid = ct.relnamespace
WHERE n.nspname = 'homologacao'
  AND ci.relname IN (
      'uq_entrada_lote_ativo_item_lote_datas',
      'uq_entrada_lote_documento_confirmado',
      'ix_entrada_lote_item',
      'ix_entrada_lote_status',
      'ix_entrada_lote_numero',
      'ix_entrada_lote_pendente_envio',
      'ix_entrada_lote_erro_sap',
      'ix_entrada_pesagem_lote'
  )
ORDER BY ci.relname;

\echo 'PREFLIGHT 043 - DEFINICOES REAIS DE TRIGGERS'
SELECT
    t.tgname,
    t.tgrelid::regclass AS tabela,
    pg_get_triggerdef(t.oid, true) AS definicao
FROM pg_trigger t
WHERE NOT t.tgisinternal
  AND t.tgrelid IN (
      to_regclass('homologacao.entrada_produto_lote'),
      'homologacao.entrada_produto_pesagem'::regclass
  )
  AND t.tgname IN (
      'trg_entrada_produto_lote_proteger_update',
      'trg_entrada_produto_pesagem_validar_lote',
      'trg_entrada_produto_pesagem_recalcular_lote'
  )
ORDER BY t.tgname;

\echo 'PREFLIGHT 043 - DEFINICOES REAIS DE FUNCOES'
SELECT
    p.oid::regprocedure AS assinatura,
    l.lanname AS linguagem,
    p.prosecdef AS security_definer,
    r.rolname AS owner,
    p.proconfig,
    pg_get_functiondef(p.oid) AS definicao
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
JOIN pg_language l ON l.oid = p.prolang
JOIN pg_roles r ON r.oid = p.proowner
WHERE n.nspname = 'homologacao'
  AND p.proname IN (
      'fn_entrada_produto_lote_proteger_update',
      'fn_entrada_produto_pesagem_validar_lote_snapshot',
      'fn_entrada_produto_lote_recalcular_peso',
      'fn_entrada_produto_pesagem_recalcular_lote'
  )
ORDER BY p.oid::regprocedure::text;


\echo 'PREFLIGHT 043 - MATRIZ EFETIVA DAS TABELAS BASE'
WITH alvos(rotulo, tabela) AS (
    VALUES
        ('GRANTS_LANCAMENTO', 'homologacao.entrada_produto_lancamento'),
        ('GRANTS_ITEM', 'homologacao.entrada_produto_item'),
        ('GRANTS_PESAGEM', 'homologacao.entrada_produto_pesagem')
)
SELECT
    rotulo,
    tabela,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'SELECT') AS possui_select,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'INSERT') AS possui_insert,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'UPDATE') AS possui_update,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'DELETE') AS possui_delete,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'TRUNCATE') AS possui_truncate,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'REFERENCES') AS possui_references,
    has_table_privilege(current_setting('fugapet_q.role_aplicacao'), tabela, 'TRIGGER') AS possui_trigger
FROM alvos
ORDER BY rotulo;

\echo 'PREFLIGHT 043 - MATRIZ EFETIVA DAS SEQUENCES BASE'
WITH alvos(rotulo, tabela, coluna) AS (
    VALUES
        ('GRANTS_SEQUENCE_LANCAMENTO', 'homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
        ('GRANTS_SEQUENCE_ITEM', 'homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
        ('GRANTS_SEQUENCE_PESAGEM', 'homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem')
), seq AS (
    SELECT rotulo, pg_get_serial_sequence(tabela, coluna) AS sequence_name
    FROM alvos
)
SELECT
    rotulo,
    sequence_name,
    has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), sequence_name, 'USAGE') AS possui_usage,
    has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), sequence_name, 'SELECT') AS possui_select,
    has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), sequence_name, 'UPDATE') AS possui_update
FROM seq
ORDER BY rotulo;



\echo 'PREFLIGHT 043 HML - OWNERS DAS TABELAS BASE'
SELECT
    c.relname AS objeto,
    c.relkind,
    pg_get_userbyid(c.relowner) AS owner
FROM pg_class c
JOIN pg_namespace n
  ON n.oid = c.relnamespace
WHERE n.nspname = 'homologacao'
  AND c.relname IN (
      'entrada_produto_lancamento',
      'entrada_produto_item',
      'entrada_produto_pesagem',
      'log_alteracao_cadastral'
  )
ORDER BY c.relname;

\echo 'PREFLIGHT 043 HML - OWNERS DAS SEQUENCES BASE'
WITH alvos(tabela, coluna) AS (
    VALUES
        ('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
        ('homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
        ('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem'),
        ('homologacao.log_alteracao_cadastral', 'codigo_log_alteracao_cadastral')
), seq AS (
    SELECT pg_get_serial_sequence(tabela, coluna)::regclass AS oid
    FROM alvos
)
SELECT
    c.oid::regclass AS sequence_name,
    pg_get_userbyid(c.relowner) AS owner
FROM seq
JOIN pg_class c
  ON c.oid = seq.oid
ORDER BY c.oid::regclass::text;

\echo 'PREFLIGHT 043 HML - TRIGGER HISTORICO DE AUDITORIA'
SELECT
    t.tgname,
    t.tgenabled,
    t.tgrelid::regclass AS tabela,
    pg_get_triggerdef(t.oid, true) AS definicao
FROM pg_trigger t
WHERE NOT t.tgisinternal
  AND t.tgrelid =
      'homologacao.entrada_produto_pesagem'::regclass
  AND t.tgname =
      'trg_entrada_produto_pesagem_log_alteracao_cadastral';

\echo 'PREFLIGHT 043 HML - ROLE CANONICA RESOLVIDA'
SELECT
    current_setting('fugapet_q.role_aplicacao')
        AS role_aplicacao_q,
    has_database_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        current_database(),
        'CONNECT'
    ) AS possui_connect,
    has_schema_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        'homologacao',
        'USAGE'
    ) AS possui_usage_schema,
    has_schema_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        'homologacao',
        'CREATE'
    ) AS possui_create_schema;

\echo 'PREFLIGHT 043 HML - ROLES NAO SUPERUSUARIAS COM USAGE NO SCHEMA'
SELECT
    r.rolname,
    r.rolcanlogin,
    has_database_privilege(
        r.rolname,
        current_database(),
        'CONNECT'
    ) AS possui_connect,
    has_schema_privilege(
        r.rolname,
        'homologacao',
        'USAGE'
    ) AS possui_usage,
    has_schema_privilege(
        r.rolname,
        'homologacao',
        'CREATE'
    ) AS possui_create,
    (
        SELECT count(*)
        FROM (VALUES
            ('homologacao.entrada_produto_lancamento'),
            ('homologacao.entrada_produto_item'),
            ('homologacao.entrada_produto_pesagem')
        ) AS alvo(tabela)
        WHERE has_table_privilege(
                  r.rolname,
                  alvo.tabela,
                  'SELECT'
              )
           OR has_table_privilege(
                  r.rolname,
                  alvo.tabela,
                  'INSERT'
              )
           OR has_table_privilege(
                  r.rolname,
                  alvo.tabela,
                  'UPDATE'
              )
    ) AS tabelas_entrada_com_privilegio
FROM pg_roles r
WHERE r.rolsuper = false
  AND has_schema_privilege(
      r.rolname,
      'homologacao',
      'USAGE'
  )
ORDER BY r.rolname;

ROLLBACK;

\echo 'OK - PREFLIGHT 043 HML concluido sem alteracao persistente'
