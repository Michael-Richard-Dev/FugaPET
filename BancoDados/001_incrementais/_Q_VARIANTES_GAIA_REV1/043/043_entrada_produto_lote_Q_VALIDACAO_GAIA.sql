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
-- 043_entrada_produto_lote_HML_VALIDACAO_GAIA.sql
-- Validacao estrutural e funcional transacional do incremental 043 HML.
--
-- Usa lotes de teste com IDs negativos e uma pesagem historica temporariamente
-- vinculada dentro da transacao. Todo o cenario e descartado por ROLLBACK.
-- NAO avanca a sequence identity da tabela de lote.
-- ============================================================================


SELECT
    current_database() = 'fuga_jales_local_homologacao_q_v1_2'
        AS ambiente_database_ok,
    to_regnamespace('homologacao') IS NOT NULL
        AS ambiente_schema_ok
\gset

\if :ambiente_database_ok
\else
    \echo 'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADO_DATABASE'
    \quit 41
\endif

\if :ambiente_schema_ok
\else
    \echo 'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADO_SCHEMA'
    \quit 42
\endif


-- ============================================================================
-- ROLE DE APLICACAO HML
-- O nome nao e presumido. Informe somente a role resolvida pelo PREFLIGHT:
--   -v role_aplicacao_q=<ROLE_APLICACAO_Q_RESOLVIDA>
-- ============================================================================
\if :{?role_aplicacao_q}
\else
    \echo 'VALIDACAO 043 HML BLOQUEADA: informe -v role_aplicacao_q=<ROLE_RESOLVIDA_PELO_PREFLIGHT>.'
    \quit 43
\endif

SELECT
    EXISTS (
        SELECT 1
        FROM pg_roles r
        WHERE r.rolname = :'role_aplicacao_q'
          AND r.rolsuper = false
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
                  ('homologacao.entrada_produto_pesagem'),
                  ('homologacao.entrada_produto_lote')
              ) AS alvo(tabela)
              WHERE NOT has_table_privilege(r.rolname, alvo.tabela, 'SELECT')
                 OR NOT has_table_privilege(r.rolname, alvo.tabela, 'INSERT')
                 OR NOT has_table_privilege(r.rolname, alvo.tabela, 'UPDATE')
                 OR has_table_privilege(r.rolname, alvo.tabela, 'DELETE')
                 OR has_table_privilege(r.rolname, alvo.tabela, 'TRUNCATE')
                 OR has_table_privilege(r.rolname, alvo.tabela, 'REFERENCES')
                 OR has_table_privilege(r.rolname, alvo.tabela, 'TRIGGER')
          )
          AND (
              WITH seq AS (
                  SELECT pg_get_serial_sequence(tabela, coluna)
                      AS sequence_name
                  FROM (VALUES
                      ('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
                      ('homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
                      ('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem'),
                      ('homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote')
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
          ) = 4
    ) AS role_aplicacao_q_valida
\gset

\if :role_aplicacao_q_valida
\else
    \echo 'VALIDACAO 043 HML BLOQUEADA: role informada nao corresponde ao padrao real validado pelo catalogo.'
    \quit 44
\endif

SELECT set_config(
    'fugapet_q.role_aplicacao',
    :'role_aplicacao_q',
    false
);

\echo 'ROLE_APLICACAO_Q_CONFIRMADA=' :role_aplicacao_q

SET search_path TO homologacao, public;

-- ============================================================================
-- PRECHECK DE RESIDUOS DA TENTATIVA ANTERIOR
-- Bloqueia antes dos snapshots e dos testes caso existam marcadores exclusivos
-- da VALIDACAO 043. Nenhum residuo e corrigido ou removido automaticamente.
-- ============================================================================
DO $$
DECLARE
    v_residuos_lote bigint;
    v_residuos_pesagem bigint;
BEGIN
    SELECT count(*)
      INTO v_residuos_lote
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote BETWEEN -430099 AND -430001
        OR correlation_id::text LIKE '04300000-%'
        OR criado_por LIKE 'VALIDADOR_043%';

    SELECT count(*)
      INTO v_residuos_pesagem
      FROM homologacao.entrada_produto_pesagem
     WHERE codigo_entrada_produto_lote BETWEEN -430099 AND -430001;

    IF v_residuos_lote <> 0 OR v_residuos_pesagem <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_BLOQUEADA_RESIDUOS_TENTATIVA_ANTERIOR lotes=% pesagens=%',
            v_residuos_lote,
            v_residuos_pesagem;
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 PRECHECK OK: residuos da tentativa anterior=0.';
END $$;

SELECT set_config(
    'fugapet_v043.lote_hash_antes',
    md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_lote
        ),
        ''
    )),
    false
)
FROM homologacao.entrada_produto_lote t;

SELECT set_config(
    'fugapet_v043.pesagem_hash_antes',
    md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_pesagem
        ),
        ''
    )),
    false
)
FROM homologacao.entrada_produto_pesagem t;

SELECT set_config(
    'fugapet_v043.item_hash_antes',
    md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_item
        ),
        ''
    )),
    false
)
FROM homologacao.entrada_produto_item t;

SELECT set_config(
    'fugapet_v043.lancamento_hash_antes',
    md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_lancamento
        ),
        ''
    )),
    false
)
FROM homologacao.entrada_produto_lancamento t;

SELECT set_config(
    'fugapet_v043.lote_qtd_antes',
    count(*)::text,
    false
)
FROM homologacao.entrada_produto_lote;

SELECT set_config(
    'fugapet_v043.log_alteracao_hash_antes',
    md5(COALESCE(
        string_agg(
            md5(to_jsonb(t)::text),
            '' ORDER BY codigo_log_alteracao_cadastral
        ),
        ''
    )),
    false
)
FROM homologacao.log_alteracao_cadastral t;

SELECT set_config(
    'fugapet_v043.log_alteracao_qtd_antes',
    count(*)::text,
    false
)
FROM homologacao.log_alteracao_cadastral;

DO $$
DECLARE
    v_rotulo text;
    v_tabela text;
    v_coluna text;
    v_sequence text;
    v_last_value bigint;
    v_is_called boolean;
BEGIN
    FOR v_rotulo, v_tabela, v_coluna IN
        SELECT *
        FROM (VALUES
            ('lancamento', 'homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
            ('item', 'homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
            ('pesagem', 'homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem'),
            ('lote', 'homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote'),
            ('log_alteracao', 'homologacao.log_alteracao_cadastral', 'codigo_log_alteracao_cadastral')
        ) AS alvo(rotulo, tabela, coluna)
    LOOP
        v_sequence := pg_get_serial_sequence(v_tabela, v_coluna);
        IF v_sequence IS NULL THEN
            RAISE EXCEPTION
                'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_SEQUENCE_NAO_RESOLVIDA tabela=% coluna=%',
                v_tabela,
                v_coluna;
        END IF;

        EXECUTE format('SELECT last_value, is_called FROM %s', v_sequence::regclass)
        INTO v_last_value, v_is_called;

        PERFORM set_config(
            'fugapet_v043.sequence_' || v_rotulo || '_antes',
            v_sequence || '|' || v_last_value::text || '|' || v_is_called::text,
            false
        );
    END LOOP;
END $$;

DO $$
DECLARE
    v_colunas_lote integer;
    v_colunas_pesagem integer;
    v_constraints_lote integer;
    v_constraints_pesagem integer;
    v_indices integer;
    v_triggers integer;
    v_funcoes integer;
    v_fk_defs integer;
    v_index_defs integer;
    v_trigger_defs integer;
    v_function_defs integer;
    v_sequences_conformes integer;
    v_owners_conformes integer;
    v_public_privilegios integer;
    v_audit_trigger_pesagem integer;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_BANCO_INCORRETO banco_atual=%',
            current_database();
    END IF;

    SELECT count(*) INTO v_colunas_lote
    FROM information_schema.columns
    WHERE table_schema = 'homologacao'
      AND table_name = 'entrada_produto_lote';

    SELECT count(*) INTO v_colunas_pesagem
    FROM information_schema.columns
    WHERE table_schema = 'homologacao'
      AND table_name = 'entrada_produto_pesagem'
      AND column_name IN (
          'codigo_entrada_produto_lote',
          'numero_lote_snapshot',
          'data_fabricacao_snapshot',
          'data_vencimento_snapshot'
      );

    SELECT count(*) INTO v_constraints_lote
    FROM pg_constraint
    WHERE conrelid =
          'homologacao.entrada_produto_lote'::regclass
      AND conname IN (
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

    SELECT count(*) INTO v_constraints_pesagem
    FROM pg_constraint
    WHERE conrelid =
          'homologacao.entrada_produto_pesagem'::regclass
      AND conname IN (
          'fk_entrada_pesagem_lote',
          'ck_entrada_pesagem_lote_snapshot_conjunto'
      );

    SELECT count(*) INTO v_indices
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

    SELECT count(*) INTO v_triggers
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
      );

    SELECT count(*) INTO v_funcoes
    FROM pg_proc p
    JOIN pg_namespace n ON n.oid = p.pronamespace
    WHERE n.nspname = 'homologacao'
      AND p.proname IN (
          'fn_entrada_produto_lote_proteger_update',
          'fn_entrada_produto_pesagem_validar_lote_snapshot',
          'fn_entrada_produto_lote_recalcular_peso',
          'fn_entrada_produto_pesagem_recalcular_lote'
      );

    SELECT count(*) INTO v_fk_defs
    FROM pg_constraint c
    WHERE (
        c.conname = 'fk_entrada_lote_item'
        AND c.conrelid = 'homologacao.entrada_produto_lote'::regclass
        AND c.confrelid = 'homologacao.entrada_produto_item'::regclass
        AND c.confdeltype = 'r'
        AND position('ON DELETE RESTRICT' in upper(pg_get_constraintdef(c.oid, true))) > 0
    ) OR (
        c.conname = 'fk_entrada_pesagem_lote'
        AND c.conrelid = 'homologacao.entrada_produto_pesagem'::regclass
        AND c.confrelid = 'homologacao.entrada_produto_lote'::regclass
        AND c.confdeltype = 'r'
        AND position('ON DELETE RESTRICT' in upper(pg_get_constraintdef(c.oid, true))) > 0
    );

    WITH idx AS (
        SELECT
            ci.relname AS indexname,
            ct.relname AS tablename,
            i.indisunique,
            i.indnkeyatts,
            lower(regexp_replace(pg_get_indexdef(ci.oid), '[[:space:]]+', ' ', 'g')) AS def,
            lower(COALESCE(pg_get_expr(i.indpred, i.indrelid), '')) AS pred
        FROM pg_index i
        JOIN pg_class ci ON ci.oid = i.indexrelid
        JOIN pg_class ct ON ct.oid = i.indrelid
        JOIN pg_namespace n ON n.oid = ct.relnamespace
        WHERE n.nspname = 'homologacao'
          AND ci.relname IN (
              'uq_entrada_lote_ativo_item_lote_datas',
              'uq_entrada_lote_documento_confirmado',
              'ix_entrada_lote_pendente_envio',
              'ix_entrada_lote_erro_sap'
          )
    )
    SELECT count(*) INTO v_index_defs
    FROM idx x
    WHERE
       (x.indexname = 'uq_entrada_lote_ativo_item_lote_datas'
        AND x.indisunique AND x.indnkeyatts = 4
        AND position('upper' in x.def) > 0
        AND position('btrim' in x.def) > 0
        AND position('numero_lote' in x.def) > 0
        AND position('cancelado' in x.pred) > 0)
    OR (x.indexname = 'uq_entrada_lote_documento_confirmado'
        AND x.indisunique AND x.indnkeyatts = 2
        AND position('documento_material_sap' in x.def) > 0
        AND position('exercicio_documento_material_sap' in x.def) > 0
        AND position('documento_material_item' in x.def) = 0
        AND position('confirmado_sap' in x.pred) > 0)
    OR (x.indexname = 'ix_entrada_lote_pendente_envio'
        AND NOT x.indisunique
        AND position('finalizado_local' in x.pred) > 0
        AND position('erro_sap' in x.pred) > 0)
    OR (x.indexname = 'ix_entrada_lote_erro_sap'
        AND NOT x.indisunique
        AND position('erro_sap' in x.pred) > 0);

    WITH trg AS (
        SELECT
            t.tgname,
            rel.relname AS tablename,
            t.tgtype,
            t.tgfoid,
            lower(regexp_replace(pg_get_triggerdef(t.oid, true), '[[:space:]]+', ' ', 'g')) AS def
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
    SELECT count(*) INTO v_trigger_defs
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
        AND t.tgfoid = 'homologacao.fn_entrada_produto_pesagem_validar_lote_snapshot()'::regprocedure)
    OR (t.tgname = 'trg_entrada_produto_pesagem_recalcular_lote'
        AND t.tablename = 'entrada_produto_pesagem'
        AND t.tgtype = 29
        AND t.tgfoid = 'homologacao.fn_entrada_produto_pesagem_recalcular_lote()'::regprocedure
        AND position('update of codigo_entrada_produto_lote, peso_liquido_kg, status_pesagem, situacao_entrada_produto_pesagem' in t.def) > 0);

    WITH fun AS (
        SELECT
            p.*,
            l.lanname,
            r.rolname AS owner_name,
            lower(regexp_replace(pg_get_functiondef(p.oid), '[[:space:]]+', ' ', 'g')) AS def,
            EXISTS (
                SELECT 1
                FROM unnest(COALESCE(p.proconfig, ARRAY[]::text[])) cfg
                WHERE replace(lower(cfg), ' ', '') = 'search_path=homologacao,pg_temp'
            ) AS search_path_ok,
            NOT EXISTS (
                SELECT 1
                FROM aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) a
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
    SELECT count(*) INTO v_function_defs
    FROM fun f
    WHERE f.lanname = 'plpgsql'
      AND f.owner_name <> current_setting('fugapet_q.role_aplicacao')
      AND f.search_path_ok
      AND f.public_execute_revogado
      AND (
          (f.proname = 'fn_entrada_produto_lote_proteger_update'
           AND NOT f.prosecdef
           AND position('tg_op = ''insert''' in f.def) > 0
           AND position('peso_liquido_total_kg is distinct from 0' in f.def) > 0
           AND position('new.alterado_em := clock_timestamp()' in f.def) > 0)
       OR (f.proname = 'fn_entrada_produto_pesagem_validar_lote_snapshot'
           AND NOT f.prosecdef
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
           AND f.prosecdef)
       OR (f.proname = 'fn_entrada_produto_pesagem_recalcular_lote'
           AND f.prosecdef)
      );

    IF v_colunas_lote <> 23
       OR v_colunas_pesagem <> 4
       OR v_constraints_lote <> 16
       OR v_constraints_pesagem <> 2
       OR v_indices <> 8
       OR v_triggers <> 3
       OR v_funcoes <> 4
       OR v_fk_defs <> 2
       OR v_index_defs <> 4
       OR v_trigger_defs <> 3
       OR v_function_defs <> 4 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ESTRUTURA colunas_lote=% colunas_pesagem=% constraints_lote=% constraints_pesagem=% indices=% triggers=% funcoes=% fk_defs=%/2 index_defs=%/4 trigger_defs=%/3 function_defs=%/4',
            v_colunas_lote,
            v_colunas_pesagem,
            v_constraints_lote,
            v_constraints_pesagem,
            v_indices,
            v_triggers,
            v_funcoes,
            v_fk_defs,
            v_index_defs,
            v_trigger_defs,
            v_function_defs;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = current_setting('fugapet_q.role_aplicacao')
          AND rolsuper = false
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ROLE_INEXISTENTE_OU_SUPERUSUARIO';
    END IF;

    IF NOT has_database_privilege(current_setting('fugapet_q.role_aplicacao'), current_database(), 'CONNECT')
       OR NOT has_schema_privilege(current_setting('fugapet_q.role_aplicacao'), 'homologacao', 'USAGE') THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_CONNECT_OU_USAGE';
    END IF;

    IF EXISTS (
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
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_GRANTS_TABELAS';
    END IF;

    WITH seq AS (
        SELECT pg_get_serial_sequence(tabela, coluna) AS sequence_name
        FROM (VALUES
            ('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
            ('homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
            ('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem'),
            ('homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote')
        ) AS alvo(tabela, coluna)
    )
    SELECT count(*)
      INTO v_sequences_conformes
      FROM seq
     WHERE sequence_name IS NOT NULL
       AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), sequence_name, 'USAGE')
       AND has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), sequence_name, 'SELECT')
       AND NOT has_sequence_privilege(current_setting('fugapet_q.role_aplicacao'), sequence_name, 'UPDATE');

    IF v_sequences_conformes <> 4 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_GRANTS_SEQUENCES conformes=%/4',
            v_sequences_conformes;
    END IF;

    WITH objetos AS (
        SELECT 'homologacao.entrada_produto_lancamento'::regclass AS oid
        UNION ALL SELECT 'homologacao.entrada_produto_item'::regclass
        UNION ALL SELECT 'homologacao.entrada_produto_pesagem'::regclass
        UNION ALL SELECT 'homologacao.entrada_produto_lote'::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_item', 'codigo_entrada_produto_item')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote')::regclass
    )
    SELECT count(*)
      INTO v_owners_conformes
      FROM objetos o
      JOIN pg_class c ON c.oid = o.oid
      JOIN pg_roles r ON r.oid = c.relowner
     WHERE r.rolname <> current_setting('fugapet_q.role_aplicacao');

    IF v_owners_conformes <> 8 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_OWNER objetos_conformes=%/8',
            v_owners_conformes;
    END IF;

    WITH objetos AS (
        SELECT 'homologacao.entrada_produto_lancamento'::regclass AS oid
        UNION ALL SELECT 'homologacao.entrada_produto_item'::regclass
        UNION ALL SELECT 'homologacao.entrada_produto_pesagem'::regclass
        UNION ALL SELECT 'homologacao.entrada_produto_lote'::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_item', 'codigo_entrada_produto_item')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote')::regclass
    )
    SELECT count(*)
      INTO v_public_privilegios
      FROM objetos o
      JOIN pg_class c ON c.oid = o.oid
      CROSS JOIN LATERAL aclexplode(COALESCE(c.relacl, '{}'::aclitem[])) a
     WHERE a.grantee = 0;

    IF v_public_privilegios <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_PUBLIC_PRIVILEGIOS total=%',
            v_public_privilegios;
    END IF;

    SELECT count(*)
      INTO v_audit_trigger_pesagem
      FROM pg_trigger t
      JOIN pg_class rel ON rel.oid = t.tgrelid
      JOIN pg_namespace n ON n.oid = rel.relnamespace
     WHERE NOT t.tgisinternal
       AND n.nspname = 'homologacao'
       AND rel.relname = 'entrada_produto_pesagem'
       AND t.tgname = 'trg_entrada_produto_pesagem_log_alteracao_cadastral'
       AND t.tgenabled = 'O';

    IF v_audit_trigger_pesagem <> 1 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TRIGGER_AUDITORIA_PESAGEM_INCOMPATIVEL encontrado=%/1',
            v_audit_trigger_pesagem;
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 ESTRUTURAL OK: estrutura e matriz exata de grants confirmadas.';
END $$;


\echo 'VALIDACAO 043 - MATRIZ EXATA DAS TABELAS'
WITH alvos(rotulo, tabela) AS (
    VALUES
        ('GRANTS_LANCAMENTO', 'homologacao.entrada_produto_lancamento'),
        ('GRANTS_ITEM', 'homologacao.entrada_produto_item'),
        ('GRANTS_PESAGEM', 'homologacao.entrada_produto_pesagem'),
        ('GRANTS_LOTE', 'homologacao.entrada_produto_lote')
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

\echo 'VALIDACAO 043 - MATRIZ EXATA DAS SEQUENCES'
WITH alvos(rotulo, tabela, coluna) AS (
    VALUES
        ('GRANTS_SEQUENCE_LANCAMENTO', 'homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
        ('GRANTS_SEQUENCE_ITEM', 'homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
        ('GRANTS_SEQUENCE_PESAGEM', 'homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem'),
        ('GRANTS_SEQUENCE_LOTE', 'homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote')
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

\echo 'VALIDACAO 043 - DEFINICOES REAIS CRITICAS'
SELECT c.conname, pg_get_constraintdef(c.oid, true) AS definicao
FROM pg_constraint c
WHERE c.conname IN ('fk_entrada_lote_item', 'fk_entrada_pesagem_lote')
ORDER BY c.conname;

SELECT ci.relname AS indice, pg_get_indexdef(ci.oid) AS definicao,
       pg_get_expr(i.indpred, i.indrelid) AS predicado
FROM pg_index i
JOIN pg_class ci ON ci.oid = i.indexrelid
JOIN pg_namespace n ON n.oid = ci.relnamespace
WHERE n.nspname = 'homologacao'
  AND ci.relname IN (
      'uq_entrada_lote_ativo_item_lote_datas',
      'uq_entrada_lote_documento_confirmado',
      'ix_entrada_lote_pendente_envio',
      'ix_entrada_lote_erro_sap'
  )
ORDER BY ci.relname;

SELECT t.tgname, t.tgrelid::regclass AS tabela,
       pg_get_triggerdef(t.oid, true) AS definicao
FROM pg_trigger t
WHERE NOT t.tgisinternal
  AND t.tgrelid IN (
      'homologacao.entrada_produto_lote'::regclass,
      'homologacao.entrada_produto_pesagem'::regclass
  )
  AND t.tgname IN (
      'trg_entrada_produto_lote_proteger_update',
      'trg_entrada_produto_pesagem_validar_lote',
      'trg_entrada_produto_pesagem_recalcular_lote'
  )
ORDER BY t.tgname;


BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '5min';

-- O trigger generico de auditoria da pesagem usa uma sequence externa ao 043.
-- Ele e desabilitado somente dentro desta transacao para impedir avanco nao
-- transacional da sequence de log durante os testes. O ROLLBACK restaura seu
-- estado original; os tres triggers funcionais do 043 permanecem habilitados.
ALTER TABLE homologacao.entrada_produto_pesagem
    DISABLE TRIGGER trg_entrada_produto_pesagem_log_alteracao_cadastral;

DO $$
DECLARE
    v_pesagem bigint;
    v_item_1 bigint;
    v_item_2 bigint;
    v_peso_esperado numeric(14,3);
    v_peso_lote numeric(14,3);
    v_peso_lote_antes_bloqueios numeric(14,3);
    v_peso_lote_depois_bloqueios numeric(14,3);
    v_codigo_lote_pesagem bigint;
    v_numero_lote_snapshot character varying(10);
    v_data_fabricacao_snapshot date;
    v_data_vencimento_snapshot date;
    v_alterado_em_1 timestamp with time zone;
    v_alterado_em_2 timestamp with time zone;
    v_falhou boolean;
BEGIN
    SELECT
        p.codigo_entrada_produto_pesagem,
        p.codigo_entrada_produto_item,
        CASE
            WHEN p.situacao_entrada_produto_pesagem = true
             AND p.status_pesagem = 'VALIDA'
            THEN p.peso_liquido_kg
            ELSE 0
        END::numeric(14,3)
    INTO
        v_pesagem,
        v_item_1,
        v_peso_esperado
    FROM homologacao.entrada_produto_pesagem p
    WHERE p.codigo_entrada_produto_lote IS NULL
      AND p.numero_lote_snapshot IS NULL
      AND p.data_fabricacao_snapshot IS NULL
      AND p.data_vencimento_snapshot IS NULL
    ORDER BY p.codigo_entrada_produto_pesagem
    LIMIT 1;

    IF v_pesagem IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_SEM_PESAGEM_LEGADA_PARA_TESTE';
    END IF;

    SELECT i.codigo_entrada_produto_item
      INTO v_item_2
      FROM homologacao.entrada_produto_item i
     WHERE i.codigo_entrada_produto_item <> v_item_1
     ORDER BY i.codigo_entrada_produto_item
     LIMIT 1;

    IF v_item_2 IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_SEM_SEGUNDO_ITEM_PARA_TESTE';
    END IF;

    INSERT INTO homologacao.entrada_produto_lote (
        codigo_entrada_produto_lote,
        codigo_entrada_produto_item,
        numero_lote,
        data_fabricacao,
        data_vencimento,
        status_lote,
        correlation_id,
        criado_por
    ) VALUES (
        -430001,
        v_item_1,
        'LT43001',
        DATE '2026-01-01',
        DATE '2027-01-01',
        'AGUARDANDO_DADOS',
        '04300000-0000-0000-0000-000000000001'::uuid,
        'VALIDADOR_043'
    );

    INSERT INTO homologacao.entrada_produto_lote (
        codigo_entrada_produto_lote,
        codigo_entrada_produto_item,
        numero_lote,
        data_fabricacao,
        data_vencimento,
        status_lote,
        correlation_id,
        criado_por
    ) VALUES (
        -430002,
        v_item_2,
        'LT43001',
        DATE '2026-01-01',
        DATE '2027-01-01',
        'AGUARDANDO_DADOS',
        '04300000-0000-0000-0000-000000000002'::uuid,
        'VALIDADOR_043'
    );

    RAISE NOTICE
        'VALIDACAO 043 OK: mesmo lote/datas em item diferente permitido.';

    -- A pesagem preserva simultaneamente o item original e o vinculo ao lote.
    -- Um lote pertencente a outro item deve ser rejeitado.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET codigo_entrada_produto_lote = -430002,
               numero_lote_snapshot = 'LT43001',
               data_fabricacao_snapshot = DATE '2026-01-01',
               data_vencimento_snapshot = DATE '2027-01-01'
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;

    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ITEM_DIVERGENTE detalhe=lote de outro item foi aceito pela pesagem.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.entrada_produto_pesagem
        WHERE codigo_entrada_produto_pesagem = v_pesagem
          AND (
              codigo_entrada_produto_lote IS NOT NULL
              OR numero_lote_snapshot IS NOT NULL
              OR data_fabricacao_snapshot IS NOT NULL
              OR data_vencimento_snapshot IS NOT NULL
          )
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ITEM_DIVERGENTE detalhe=tentativa bloqueada alterou a pesagem legada.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 HML OK: lote pertencente a outro item bloqueado; codigo_entrada_produto_item preservado.';

    -- As datas sao obrigatorias na criacao do lote.
    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_vencimento,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430014,
            v_item_1,
            'LT43014',
            DATE '2027-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000014'::uuid,
            'VALIDADOR_043_HML'
        );
    EXCEPTION WHEN not_null_violation THEN
        v_falhou := true;
    END;

    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe=data_fabricacao ausente foi aceita.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430015,
            v_item_1,
            'LT43015',
            DATE '2026-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000015'::uuid,
            'VALIDADOR_043_HML'
        );
    EXCEPTION WHEN not_null_violation THEN
        v_falhou := true;
    END;

    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe=data_vencimento ausente foi aceita.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 HML OK: fabricacao e vencimento obrigatorios confirmados.';

    -- Segundo lote valido do mesmo item, usado somente para provar que a
    -- pesagem ja vinculada nao pode trocar de lote.
    INSERT INTO homologacao.entrada_produto_lote (
        codigo_entrada_produto_lote,
        codigo_entrada_produto_item,
        numero_lote,
        data_fabricacao,
        data_vencimento,
        status_lote,
        correlation_id,
        criado_por
    ) VALUES (
        -430013,
        v_item_1,
        'LT43013',
        DATE '2026-02-01',
        DATE '2027-02-01',
        'AGUARDANDO_DADOS',
        '04300000-0000-0000-0000-000000000013'::uuid,
        'VALIDADOR_043'
    );

    SELECT peso_liquido_total_kg
      INTO v_peso_lote
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote = -430001;

    IF v_peso_lote IS DISTINCT FROM 0::numeric THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= INSERT sem peso nao gravou zero; encontrado %.',
            v_peso_lote;
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: INSERT sem peso aceito e gravado com zero.';

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            peso_liquido_total_kg,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430012,
            v_item_1,
            'LT43012',
            DATE '2026-01-01',
            DATE '2027-01-01',
            1,
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000012'::uuid,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= peso positivo manual no INSERT foi aceito.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: peso positivo manual no INSERT bloqueado.';

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430003,
            v_item_1,
            '  ',
            DATE '2026-01-01',
            DATE '2027-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000003'::uuid,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN check_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= lote vazio foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430004,
            v_item_1,
            'LT43004',
            DATE '2027-01-01',
            DATE '2026-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000004'::uuid,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN check_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= vencimento anterior a fabricacao foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430005,
            v_item_1,
            'LT43005',
            DATE '2026-01-01',
            DATE '2027-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000001'::uuid,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN unique_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= correlation_id duplicado foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430006,
            v_item_1,
            ' lt43001 ',
            DATE '2026-01-01',
            DATE '2027-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000006'::uuid,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN unique_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= agrupamento ativo duplicado normalizado foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            peso_liquido_total_kg,
            status_lote,
            correlation_id,
            criado_por
        ) VALUES (
            -430007,
            v_item_1,
            'LT43007',
            DATE '2026-01-01',
            DATE '2027-01-01',
            -1,
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000007'::uuid,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN check_violation OR raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= peso negativo foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            confirmado_em,
            enviado_sap_em,
            confirmado_sap_em,
            criado_por
        ) VALUES (
            -430008,
            v_item_1,
            'LT43008',
            DATE '2026-01-01',
            DATE '2027-01-01',
            'CONFIRMADO_SAP',
            '04300000-0000-0000-0000-000000000008'::uuid,
            clock_timestamp(),
            clock_timestamp(),
            clock_timestamp(),
            'VALIDADOR_043'
        );
    EXCEPTION WHEN check_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= CONFIRMADO_SAP sem documento foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            payload_enviado_json,
            criado_por
        ) VALUES (
            -430009,
            v_item_1,
            'LT43009',
            DATE '2026-01-01',
            DATE '2027-01-01',
            'AGUARDANDO_DADOS',
            '04300000-0000-0000-0000-000000000009'::uuid,
            '{"Authorization":"Basic segredo"}'::jsonb,
            'VALIDADOR_043'
        );
    EXCEPTION WHEN check_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= payload com credencial foi aceito.';
    END IF;

    UPDATE homologacao.entrada_produto_pesagem
       SET codigo_entrada_produto_lote = -430001,
           numero_lote_snapshot = 'LT43001',
           data_fabricacao_snapshot = DATE '2026-01-01',
           data_vencimento_snapshot = DATE '2027-01-01'
     WHERE codigo_entrada_produto_pesagem = v_pesagem;

    SELECT peso_liquido_total_kg
      INTO v_peso_lote
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote = -430001;

    IF v_peso_lote IS DISTINCT FROM v_peso_esperado THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= total do lote %, esperado %.',
            v_peso_lote,
            v_peso_esperado;
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: pesagem vinculada, snapshot validado e peso consolidado pela fonte de verdade.';

    v_peso_lote_antes_bloqueios := v_peso_lote;

    -- 1. Desvinculacao integral: lote e os tres snapshots para NULL.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET codigo_entrada_produto_lote = NULL,
               numero_lote_snapshot = NULL,
               data_fabricacao_snapshot = NULL,
               data_vencimento_snapshot = NULL
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_DESVINCULACAO_INTEGRAL detalhe= lote e snapshots foram removidos de pesagem ja vinculada.';
    END IF;

    SELECT
        p.codigo_entrada_produto_lote,
        p.numero_lote_snapshot,
        p.data_fabricacao_snapshot,
        p.data_vencimento_snapshot
      INTO
        v_codigo_lote_pesagem,
        v_numero_lote_snapshot,
        v_data_fabricacao_snapshot,
        v_data_vencimento_snapshot
      FROM homologacao.entrada_produto_pesagem p
     WHERE p.codigo_entrada_produto_pesagem = v_pesagem;

    SELECT peso_liquido_total_kg
      INTO v_peso_lote_depois_bloqueios
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote = -430001;

    IF v_codigo_lote_pesagem IS DISTINCT FROM -430001
       OR v_numero_lote_snapshot IS DISTINCT FROM 'LT43001'
       OR v_data_fabricacao_snapshot IS DISTINCT FROM DATE '2026-01-01'
       OR v_data_vencimento_snapshot IS DISTINCT FROM DATE '2027-01-01'
       OR v_peso_lote_depois_bloqueios
          IS DISTINCT FROM v_peso_lote_antes_bloqueios THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_DESVINCULACAO_INTEGRAL detalhe= estado ou peso mudou apos tentativa bloqueada; lote=%, numero=%, fabricacao=%, vencimento=%, peso_antes=%, peso_depois=%.',
            v_codigo_lote_pesagem,
            v_numero_lote_snapshot,
            v_data_fabricacao_snapshot,
            v_data_vencimento_snapshot,
            v_peso_lote_antes_bloqueios,
            v_peso_lote_depois_bloqueios;
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: desvinculacao integral da pesagem e remocao dos snapshots bloqueadas.';

    -- 2. Troca para outro lote valido do mesmo item, com snapshots coerentes.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET codigo_entrada_produto_lote = -430013,
               numero_lote_snapshot = 'LT43013',
               data_fabricacao_snapshot = DATE '2026-02-01',
               data_vencimento_snapshot = DATE '2027-02-01'
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TROCA_LOTE detalhe= pesagem ja vinculada foi movida para outro lote.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: troca do lote vinculado bloqueada.';

    -- 3. Remocao isolada somente do codigo do lote.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET codigo_entrada_produto_lote = NULL
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_REMOCAO_CODIGO_LOTE detalhe= codigo do lote foi removido isoladamente.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: remocao isolada do codigo do lote bloqueada.';

    -- 4. Remocao isolada dos tres snapshots, preservando o codigo do lote.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET numero_lote_snapshot = NULL,
               data_fabricacao_snapshot = NULL,
               data_vencimento_snapshot = NULL
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_REMOCAO_SNAPSHOTS detalhe= snapshots foram removidos de pesagem vinculada.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: remocao isolada dos snapshots bloqueada.';

    -- 5. Alteracao do numero do lote no snapshot.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET numero_lote_snapshot = 'ALTERADO'
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ALTERACAO_NUMERO_SNAPSHOT detalhe= numero do lote no snapshot foi alterado.';
    END IF;

    -- 6. Alteracao isolada de cada data do snapshot.
    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET data_fabricacao_snapshot = DATE '2026-01-02'
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ALTERACAO_DATA_FABRICACAO_SNAPSHOT detalhe= data de fabricacao do snapshot foi alterada.';
    END IF;

    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_pesagem
           SET data_vencimento_snapshot = DATE '2027-01-02'
         WHERE codigo_entrada_produto_pesagem = v_pesagem;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_ALTERACAO_DATA_VENCIMENTO_SNAPSHOT detalhe= data de vencimento do snapshot foi alterada.';
    END IF;

    SELECT
        p.codigo_entrada_produto_lote,
        p.numero_lote_snapshot,
        p.data_fabricacao_snapshot,
        p.data_vencimento_snapshot
      INTO
        v_codigo_lote_pesagem,
        v_numero_lote_snapshot,
        v_data_fabricacao_snapshot,
        v_data_vencimento_snapshot
      FROM homologacao.entrada_produto_pesagem p
     WHERE p.codigo_entrada_produto_pesagem = v_pesagem;

    SELECT peso_liquido_total_kg
      INTO v_peso_lote_depois_bloqueios
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote = -430001;

    IF v_codigo_lote_pesagem IS DISTINCT FROM -430001
       OR v_numero_lote_snapshot IS DISTINCT FROM 'LT43001'
       OR v_data_fabricacao_snapshot IS DISTINCT FROM DATE '2026-01-01'
       OR v_data_vencimento_snapshot IS DISTINCT FROM DATE '2027-01-01'
       OR v_peso_lote_depois_bloqueios
          IS DISTINCT FROM v_peso_lote_antes_bloqueios THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_IMUTABILIDADE_FINAL detalhe= vinculo, snapshots ou peso mudaram apos testes bloqueados.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: numero e datas dos snapshots permanecem imutaveis depois da primeira vinculacao.';

    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_lote
           SET peso_liquido_total_kg = peso_liquido_total_kg + 1
         WHERE codigo_entrada_produto_lote = -430001;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= update direto do peso total foi aceito.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: update direto do peso bloqueado; consolidacao interna preservada.';

    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_lote
           SET numero_lote = 'ALTERADO'
         WHERE codigo_entrada_produto_lote = -430001;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= lote alterado apos pesagem.';
    END IF;

    UPDATE homologacao.entrada_produto_lote
       SET status_lote = 'LOTE_CONFIRMADO',
           confirmado_em = clock_timestamp(),
           alterado_por = 'VALIDADOR_043'
     WHERE codigo_entrada_produto_lote = -430001;

    SELECT alterado_em
      INTO v_alterado_em_1
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote = -430001;

    PERFORM pg_sleep(0.01);

    UPDATE homologacao.entrada_produto_lote
       SET alterado_por = 'VALIDADOR_043_SEGUNDA_ATUALIZACAO'
     WHERE codigo_entrada_produto_lote = -430001;

    SELECT alterado_em
      INTO v_alterado_em_2
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote = -430001;

    IF v_alterado_em_1 IS NULL
       OR v_alterado_em_2 IS NULL
       OR v_alterado_em_2 <= v_alterado_em_1 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= alterado_em nao foi renovado; anterior=%, atual=%.',
            v_alterado_em_1,
            v_alterado_em_2;
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: alterado_em renovado pelo banco em toda atualizacao.';

    UPDATE homologacao.entrada_produto_lote
       SET status_lote = 'ENVIANDO_SAP',
           confirmado_em = clock_timestamp(),
           enviado_sap_em = clock_timestamp(),
           alterado_por = 'VALIDADOR_043'
     WHERE codigo_entrada_produto_lote = -430002;

    UPDATE homologacao.entrada_produto_lote
       SET status_lote = 'ERRO_SAP',
           erro_sanitizado = 'ERRO CONTROLADO DE VALIDACAO',
           alterado_por = 'VALIDADOR_043'
     WHERE codigo_entrada_produto_lote = -430002;

    RAISE NOTICE
        'VALIDACAO 043 OK: atualizacoes de status SAP permitidas quando coerentes.';

    INSERT INTO homologacao.entrada_produto_lote (
        codigo_entrada_produto_lote,
        codigo_entrada_produto_item,
        numero_lote,
        data_fabricacao,
        data_vencimento,
        status_lote,
        correlation_id,
        documento_material_sap,
        exercicio_documento_material_sap,
        documento_material_item,
        confirmado_em,
        enviado_sap_em,
        confirmado_sap_em,
        criado_por
    ) VALUES (
        -430010,
        v_item_2,
        'LT43010',
        DATE '2026-01-01',
        DATE '2027-01-01',
        'CONFIRMADO_SAP',
        '04300000-0000-0000-0000-000000000010'::uuid,
        '4900000043',
        '2026',
        '0001',
        clock_timestamp(),
        clock_timestamp(),
        clock_timestamp(),
        'VALIDADOR_043'
    );

    v_falhou := false;
    BEGIN
        UPDATE homologacao.entrada_produto_lote
           SET status_lote = 'ERRO_SAP',
               erro_sanitizado = 'TENTATIVA DE REENVIO',
               alterado_por = 'VALIDADOR_043'
         WHERE codigo_entrada_produto_lote = -430010;
    EXCEPTION WHEN raise_exception THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= reenvio de CONFIRMADO_SAP foi permitido.';
    END IF;

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote,
            codigo_entrada_produto_item,
            numero_lote,
            data_fabricacao,
            data_vencimento,
            status_lote,
            correlation_id,
            documento_material_sap,
            exercicio_documento_material_sap,
            documento_material_item,
            confirmado_em,
            enviado_sap_em,
            confirmado_sap_em,
            criado_por
        ) VALUES (
            -430011,
            v_item_1,
            'LT43011',
            DATE '2026-01-01',
            DATE '2027-01-01',
            'CONFIRMADO_SAP',
            '04300000-0000-0000-0000-000000000011'::uuid,
            '4900000043',
            '2026',
            '0002',
            clock_timestamp(),
            clock_timestamp(),
            clock_timestamp(),
            'VALIDADOR_043'
        );
    EXCEPTION WHEN unique_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= mesmo documento/ano com item SAP diferente foi aceito.';
    END IF;

    v_falhou := false;
    BEGIN
        DELETE FROM homologacao.entrada_produto_lote
        WHERE codigo_entrada_produto_lote = -430001;
    EXCEPTION
        WHEN SQLSTATE '23001' THEN
            v_falhou := true;
            RAISE NOTICE
                'VALIDACAO 043 OK: ON DELETE RESTRICT confirmado.';
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= ON DELETE RESTRICT do lote foi ignorado.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: documento/ano unico mesmo com item SAP diferente.';
    RAISE NOTICE
        'VALIDACAO 043 OK: constraints, idempotencia, peso, alterado_em, imutabilidade, bloqueio de desvinculacao/troca, snapshots, grants e FKs validados.';
END $$;

ROLLBACK;

\echo 'VALIDACAO 043 - POS-ROLLBACK'

SELECT
    current_setting('fugapet_v043.lote_qtd_antes')::bigint AS lotes_antes,
    (SELECT count(*) FROM homologacao.entrada_produto_lote) AS lotes_depois;

DO $$
DECLARE
    v_lote_hash_depois text;
    v_pesagem_hash_depois text;
    v_item_hash_depois text;
    v_lancamento_hash_depois text;
    v_log_alteracao_hash_depois text;
    v_lote_qtd_depois bigint;
    v_log_alteracao_qtd_depois bigint;
BEGIN
    SELECT md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_lote
        ),
        ''
    ))
    INTO v_lote_hash_depois
    FROM homologacao.entrada_produto_lote t;

    SELECT md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_pesagem
        ),
        ''
    ))
    INTO v_pesagem_hash_depois
    FROM homologacao.entrada_produto_pesagem t;

    SELECT md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_item
        ),
        ''
    ))
    INTO v_item_hash_depois
    FROM homologacao.entrada_produto_item t;

    SELECT md5(COALESCE(
        string_agg(
            md5(row_to_json(t)::text),
            '' ORDER BY codigo_entrada_produto_lancamento
        ),
        ''
    ))
    INTO v_lancamento_hash_depois
    FROM homologacao.entrada_produto_lancamento t;

    SELECT md5(COALESCE(
        string_agg(
            md5(to_jsonb(t)::text),
            '' ORDER BY codigo_log_alteracao_cadastral
        ),
        ''
    ))
    INTO v_log_alteracao_hash_depois
    FROM homologacao.log_alteracao_cadastral t;

    SELECT count(*)
      INTO v_lote_qtd_depois
      FROM homologacao.entrada_produto_lote;

    SELECT count(*)
      INTO v_log_alteracao_qtd_depois
      FROM homologacao.log_alteracao_cadastral;

    IF v_lote_hash_depois
           <> current_setting('fugapet_v043.lote_hash_antes')
       OR v_pesagem_hash_depois
           <> current_setting('fugapet_v043.pesagem_hash_antes')
       OR v_item_hash_depois
           <> current_setting('fugapet_v043.item_hash_antes')
       OR v_lancamento_hash_depois
           <> current_setting('fugapet_v043.lancamento_hash_antes')
       OR v_log_alteracao_hash_depois
           <> current_setting('fugapet_v043.log_alteracao_hash_antes')
       OR v_lote_qtd_depois
           <> current_setting('fugapet_v043.lote_qtd_antes')::bigint
       OR v_log_alteracao_qtd_depois
           <> current_setting('fugapet_v043.log_alteracao_qtd_antes')::bigint THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TESTE_FUNCIONAL detalhe= residuos ou alteracao historica detectados.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 043 OK: rollback interno confirmado e zero residuos.';
END $$;


DO $$
DECLARE
    v_audit_trigger_pesagem integer;
BEGIN
    SELECT count(*)
      INTO v_audit_trigger_pesagem
      FROM pg_trigger t
      JOIN pg_class rel ON rel.oid = t.tgrelid
      JOIN pg_namespace n ON n.oid = rel.relnamespace
     WHERE NOT t.tgisinternal
       AND n.nspname = 'homologacao'
       AND rel.relname = 'entrada_produto_pesagem'
       AND t.tgname = 'trg_entrada_produto_pesagem_log_alteracao_cadastral'
       AND t.tgenabled = 'O';

    IF v_audit_trigger_pesagem <> 1 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_TRIGGER_AUDITORIA_NAO_RESTAURADO encontrado=%/1',
            v_audit_trigger_pesagem;
    END IF;

    RAISE NOTICE 'VALIDACAO 043 OK: trigger historico de auditoria restaurado pelo rollback interno.';
END $$;

DO $$
DECLARE
    v_rotulo text;
    v_tabela text;
    v_coluna text;
    v_sequence text;
    v_last_value bigint;
    v_is_called boolean;
    v_antes text;
    v_depois text;
BEGIN
    FOR v_rotulo, v_tabela, v_coluna IN
        SELECT *
        FROM (VALUES
            ('lancamento', 'homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
            ('item', 'homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
            ('pesagem', 'homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem'),
            ('lote', 'homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote'),
            ('log_alteracao', 'homologacao.log_alteracao_cadastral', 'codigo_log_alteracao_cadastral')
        ) AS alvo(rotulo, tabela, coluna)
    LOOP
        v_sequence := pg_get_serial_sequence(v_tabela, v_coluna);
        EXECUTE format('SELECT last_value, is_called FROM %s', v_sequence::regclass)
        INTO v_last_value, v_is_called;

        v_antes := current_setting('fugapet_v043.sequence_' || v_rotulo || '_antes');
        v_depois := v_sequence || '|' || v_last_value::text || '|' || v_is_called::text;

        IF v_depois <> v_antes THEN
            RAISE EXCEPTION
                'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_REPROVADA_SEQUENCE_AVANCADA rotulo=% antes=% depois=%',
                v_rotulo,
                v_antes,
                v_depois;
        END IF;
    END LOOP;

    RAISE NOTICE 'VALIDACAO 043 OK: nenhuma das quatro sequences do 043 nem a sequence de auditoria foi avancada pelos testes.';
    RAISE NOTICE 'CLASSIFICACAO_OFICIAL=VALIDACAO_043_HML_CONCLUIDA_COM_SUCESSO';
END $$;

\echo 'OK - persistencia por lote 043 validada (HML)'
