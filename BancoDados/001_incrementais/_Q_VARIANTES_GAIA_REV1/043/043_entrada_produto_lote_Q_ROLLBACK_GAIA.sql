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
-- 043_entrada_produto_lote_HML_ROLLBACK_GAIA.sql
-- ROLLBACK OFICIAL, SEPARADO E NUNCA AUTOMATICO.
--
-- Exige:
--   -v CONFIRMAR_ROLLBACK_043=EU_CONFIRMO_ROLLBACK_043_Q
--
-- Bloqueia quando houver qualquer lote registrado ou pesagem vinculada.
-- NAO apaga lancamentos, itens ou pesagens historicas.
-- NAO toca em HML/PRD nem em integracoes anteriores.
-- ============================================================================

\if :{?CONFIRMAR_ROLLBACK_043}
\else
    \echo 'ROLLBACK 043 BLOQUEADO: informe -v CONFIRMAR_ROLLBACK_043=EU_CONFIRMO_ROLLBACK_043_Q'
    \quit 43
\endif

SELECT
    :'CONFIRMAR_ROLLBACK_043' =
    'EU_CONFIRMO_ROLLBACK_043_Q' AS confirmacao_ok
\gset

\if :confirmacao_ok
\else
    \echo 'ROLLBACK 043 BLOQUEADO: confirmacao explicita incorreta.'
    \quit 44
\endif


SELECT
    current_database() = 'fuga_jales_local_homologacao_q_v1_2'
        AS ambiente_database_ok,
    to_regnamespace('homologacao') IS NOT NULL
        AS ambiente_schema_ok
\gset

\if :ambiente_database_ok
\else
    \echo 'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_DATABASE'
    \quit 41
\endif

\if :ambiente_schema_ok
\else
    \echo 'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_SCHEMA'
    \quit 42
\endif


-- ============================================================================
-- ROLE DE APLICACAO HML
-- O nome nao e presumido. Informe somente a role resolvida pelo PREFLIGHT:
--   -v role_aplicacao_q=<ROLE_APLICACAO_Q_RESOLVIDA>
-- ============================================================================
\if :{?role_aplicacao_q}
\else
    \echo 'ROLLBACK 043 HML BLOQUEADA: informe -v role_aplicacao_q=<ROLE_RESOLVIDA_PELO_PREFLIGHT>.'
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
    \echo 'ROLLBACK 043 HML BLOQUEADA: role informada nao corresponde ao padrao real validado pelo catalogo.'
    \quit 44
\endif

SELECT set_config(
    'fugapet_q.role_aplicacao',
    :'role_aplicacao_q',
    false
);

\echo 'ROLE_APLICACAO_Q_CONFIRMADA=' :role_aplicacao_q

SET search_path TO homologacao, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '5min';

DO $$
DECLARE
    v_tabela_lote integer;
    v_colunas_pesagem integer;
    v_lotes bigint;
    v_pesagens_vinculadas bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_BANCO_INCORRETO banco_atual=%',
            current_database();
    END IF;

    SELECT count(*)
      INTO v_tabela_lote
      FROM information_schema.tables
     WHERE table_schema = 'homologacao'
       AND table_name = 'entrada_produto_lote';

    SELECT count(*)
      INTO v_colunas_pesagem
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'entrada_produto_pesagem'
       AND column_name IN (
           'codigo_entrada_produto_lote',
           'numero_lote_snapshot',
           'data_fabricacao_snapshot',
           'data_vencimento_snapshot'
       );

    IF v_tabela_lote <> 1 OR v_colunas_pesagem <> 4 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_BLOQUEADO_ESTRUTURA_PARCIAL tabela_lote=% colunas_pesagem=%/4',
            v_tabela_lote,
            v_colunas_pesagem;
    END IF;

    SELECT count(*)
      INTO v_lotes
      FROM homologacao.entrada_produto_lote;

    SELECT count(*)
      INTO v_pesagens_vinculadas
      FROM homologacao.entrada_produto_pesagem
     WHERE codigo_entrada_produto_lote IS NOT NULL
        OR numero_lote_snapshot IS NOT NULL
        OR data_fabricacao_snapshot IS NOT NULL
        OR data_vencimento_snapshot IS NOT NULL;

    IF v_lotes <> 0 OR v_pesagens_vinculadas <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_BLOQUEADO_DADOS_PRODUTIVOS lotes=% pesagens_vinculadas=%',
            v_lotes,
            v_pesagens_vinculadas;
    END IF;

    RAISE NOTICE
        'ROLLBACK 043 PRECHECK OK: sem lotes e sem pesagens vinculadas.';
END $$;

DROP TRIGGER trg_entrada_produto_pesagem_recalcular_lote
ON homologacao.entrada_produto_pesagem;

DROP TRIGGER trg_entrada_produto_pesagem_validar_lote
ON homologacao.entrada_produto_pesagem;

DROP TRIGGER trg_entrada_produto_lote_proteger_update
ON homologacao.entrada_produto_lote;

DROP INDEX homologacao.ix_entrada_pesagem_lote;

ALTER TABLE homologacao.entrada_produto_pesagem
    DROP CONSTRAINT ck_entrada_pesagem_lote_snapshot_conjunto;

ALTER TABLE homologacao.entrada_produto_pesagem
    DROP CONSTRAINT fk_entrada_pesagem_lote;

ALTER TABLE homologacao.entrada_produto_pesagem
    DROP COLUMN data_vencimento_snapshot,
    DROP COLUMN data_fabricacao_snapshot,
    DROP COLUMN numero_lote_snapshot,
    DROP COLUMN codigo_entrada_produto_lote;

DROP FUNCTION homologacao.fn_entrada_produto_pesagem_recalcular_lote();
DROP FUNCTION homologacao.fn_entrada_produto_lote_recalcular_peso(bigint);
DROP FUNCTION homologacao.fn_entrada_produto_pesagem_validar_lote_snapshot();
DROP FUNCTION homologacao.fn_entrada_produto_lote_proteger_update();

DROP TABLE homologacao.entrada_produto_lote;

-- A PROPOSTA HML nao modifica grants das tres tabelas ou sequences base.
-- O ROLLBACK, portanto, nao toca em ACLs historicas. A matriz base e
-- verificada novamente no postcheck e deve permanecer exatamente conforme
-- o padrao catalogado antes da aplicacao.

DO $$
DECLARE
    v_sequence_conformes integer;
    v_owner_conformes integer;
BEGIN
    IF to_regclass('homologacao.entrada_produto_lote') IS NOT NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_TABELA_LOTE_AINDA_EXISTE';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name IN (
              'codigo_entrada_produto_lote',
              'numero_lote_snapshot',
              'data_fabricacao_snapshot',
              'data_vencimento_snapshot'
          )
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_COLUNAS_043_AINDA_EXISTEM';
    END IF;

    IF to_regclass('homologacao.entrada_produto_lancamento') IS NULL
       OR to_regclass('homologacao.entrada_produto_item') IS NULL
       OR to_regclass('homologacao.entrada_produto_pesagem') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_TABELAS_HISTORICAS_AFETADAS';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name = 'codigo_entrada_produto_item'
          AND is_nullable = 'NO'
          AND data_type = 'bigint'
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_VINCULO_ITEM_AFETADO';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        WHERE c.contype = 'f'
          AND c.conrelid =
              'homologacao.entrada_produto_pesagem'::regclass
          AND c.confrelid =
              'homologacao.entrada_produto_item'::regclass
          AND c.confdeltype = 'r'
          AND c.conkey = ARRAY[
              (
                  SELECT attnum
                  FROM pg_attribute
                  WHERE attrelid =
                      'homologacao.entrada_produto_pesagem'::regclass
                    AND attname = 'codigo_entrada_produto_item'
                    AND NOT attisdropped
              )
          ]::smallint[]
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_FK_ITEM_AFETADA';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('homologacao.entrada_produto_lancamento'),
            ('homologacao.entrada_produto_item'),
            ('homologacao.entrada_produto_pesagem')
        ) AS alvo(tabela)
        WHERE NOT has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'SELECT'
              )
           OR NOT has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'INSERT'
              )
           OR NOT has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'UPDATE'
              )
           OR has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'DELETE'
              )
           OR has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'TRUNCATE'
              )
           OR has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'REFERENCES'
              )
           OR has_table_privilege(
                  current_setting('fugapet_q.role_aplicacao'),
                  alvo.tabela,
                  'TRIGGER'
              )
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_MATRIZ_TABELAS_BASE_NAO_PRESERVADA';
    END IF;

    WITH seq AS (
        SELECT pg_get_serial_sequence(tabela, coluna) AS sequence_name
        FROM (VALUES
            ('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento'),
            ('homologacao.entrada_produto_item', 'codigo_entrada_produto_item'),
            ('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem')
        ) AS alvo(tabela, coluna)
    )
    SELECT count(*)
      INTO v_sequence_conformes
      FROM seq
     WHERE sequence_name IS NOT NULL
       AND has_sequence_privilege(
           current_setting('fugapet_q.role_aplicacao'),
           sequence_name,
           'USAGE'
       )
       AND has_sequence_privilege(
           current_setting('fugapet_q.role_aplicacao'),
           sequence_name,
           'SELECT'
       )
       AND NOT has_sequence_privilege(
           current_setting('fugapet_q.role_aplicacao'),
           sequence_name,
           'UPDATE'
       );

    IF v_sequence_conformes <> 3 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_MATRIZ_SEQUENCES_BASE_NAO_PRESERVADA conformes=%/3',
            v_sequence_conformes;
    END IF;

    WITH objetos AS (
        SELECT 'homologacao.entrada_produto_lancamento'::regclass AS oid
        UNION ALL SELECT 'homologacao.entrada_produto_item'::regclass
        UNION ALL SELECT 'homologacao.entrada_produto_pesagem'::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_lancamento', 'codigo_entrada_produto_lancamento')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_item', 'codigo_entrada_produto_item')::regclass
        UNION ALL SELECT pg_get_serial_sequence('homologacao.entrada_produto_pesagem', 'codigo_entrada_produto_pesagem')::regclass
    )
    SELECT count(*)
      INTO v_owner_conformes
      FROM objetos o
      JOIN pg_class c ON c.oid = o.oid
      JOIN pg_roles r ON r.oid = c.relowner
     WHERE r.rolname <> current_setting('fugapet_q.role_aplicacao');

    IF v_owner_conformes <> 6 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_OWNER_BASE_ALTERADO conformes=%/6',
            v_owner_conformes;
    END IF;

    IF NOT has_database_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        current_database(),
        'CONNECT'
    )
    OR NOT has_schema_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        'homologacao',
        'USAGE'
    )
    OR has_schema_privilege(
        current_setting('fugapet_q.role_aplicacao'),
        'homologacao',
        'CREATE'
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_REPROVADO_MATRIZ_BANCO_SCHEMA_NAO_RESTAURADA';
    END IF;

    RAISE NOTICE 'CLASSIFICACAO_OFICIAL=ROLLBACK_043_HML_CONCLUIDO_COM_SUCESSO';
    RAISE NOTICE 'ROLLBACK 043 HML OK: objetos 043 removidos e matriz historica preservada.';
END $$;

COMMIT;

\echo 'OK - ROLLBACK 043 HML concluido'
