-- =====================================================================
-- FugaPET Q
-- GATE FUGAPET-Q-DB-INSTALL-EXEC-16B
-- 037A_runtime_acl_q_GAIA.sql
--
-- Companion ACL exclusivo dos objetos criados pelo incremental 037.
--
-- BUSINESS DDL: NAO
-- DATA CHANGE: NAO
-- OWNER CHANGE: NAO
-- DEFAULT PRIVILEGES: NAO
-- =====================================================================

BEGIN;

-- =====================================================================
-- 1. FAIL-CLOSED DE AMBIENTE / ROLE
-- =====================================================================

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'STOP_DATABASE_INCORRETO: %',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'STOP_CURRENT_USER_INCORRETO: %',
            current_user;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = 'fugapet_q_migration'
    ) THEN
        RAISE EXCEPTION
            'STOP_ROLE_MIGRATION_Q_EXISTE';
    END IF;
END
$$;

-- =====================================================================
-- 2. FAIL-CLOSED DOS DOIS OBJETOS 037
-- =====================================================================

DO $$
DECLARE
    v_tabela text;
BEGIN
    FOREACH v_tabela IN ARRAY ARRAY[
        'semi_acabado_lancamento',
        'semi_acabado_pesagem'
    ]
    LOOP
        IF to_regclass(
            format('homologacao.%I', v_tabela)
        ) IS NULL THEN
            RAISE EXCEPTION
                'STOP_037A_TABELA_AUSENTE: homologacao.%',
                v_tabela;
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM pg_class c
            JOIN pg_namespace n
              ON n.oid = c.relnamespace
            WHERE n.nspname = 'homologacao'
              AND c.relname = v_tabela
              AND c.relkind IN ('r','p')
              AND pg_get_userbyid(c.relowner) =
                  'fugapet_q_owner'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_OWNER_TABELA_DIVERGENTE: homologacao.%',
                v_tabela;
        END IF;
    END LOOP;
END
$$;

-- =====================================================================
-- 3. ACL DAS TABELAS
-- =====================================================================

REVOKE ALL PRIVILEGES
ON TABLE
    homologacao.semi_acabado_lancamento,
    homologacao.semi_acabado_pesagem
FROM PUBLIC;

REVOKE
    DELETE,
    TRUNCATE,
    REFERENCES,
    TRIGGER
ON TABLE
    homologacao.semi_acabado_lancamento,
    homologacao.semi_acabado_pesagem
FROM fugapet_q_app;

GRANT
    SELECT,
    INSERT,
    UPDATE
ON TABLE
    homologacao.semi_acabado_lancamento,
    homologacao.semi_acabado_pesagem
TO fugapet_q_app;

-- =====================================================================
-- 4. ACL DAS IDENTITY SEQUENCES
--    Nomes resolvidos materialmente pelo catalogo PostgreSQL.
-- =====================================================================

DO $$
DECLARE
    r record;
    v_sequence text;
BEGIN
    FOR r IN
        SELECT *
        FROM (
            VALUES
                (
                    'semi_acabado_lancamento',
                    'codigo_semi_acabado_lancamento'
                ),
                (
                    'semi_acabado_pesagem',
                    'codigo_semi_acabado_pesagem'
                )
        ) AS x(tabela, coluna)
    LOOP
        v_sequence :=
            pg_get_serial_sequence(
                format('homologacao.%I', r.tabela),
                r.coluna
            );

        IF v_sequence IS NULL THEN
            RAISE EXCEPTION
                'STOP_037A_SEQUENCE_NAO_RESOLVIDA: %.%',
                r.tabela,
                r.coluna;
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM pg_class c
            WHERE c.oid = v_sequence::regclass
              AND c.relkind = 'S'
              AND pg_get_userbyid(c.relowner) =
                  'fugapet_q_owner'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_OWNER_SEQUENCE_DIVERGENTE: %',
                v_sequence;
        END IF;

        EXECUTE format(
            'REVOKE ALL PRIVILEGES ON SEQUENCE %s FROM PUBLIC',
            v_sequence
        );

        EXECUTE format(
            'REVOKE UPDATE ON SEQUENCE %s FROM fugapet_q_app',
            v_sequence
        );

        EXECUTE format(
            'GRANT USAGE, SELECT ON SEQUENCE %s TO fugapet_q_app',
            v_sequence
        );
    END LOOP;
END
$$;

-- =====================================================================
-- 5. VALIDACAO FAIL-CLOSED DAS TABELAS
-- =====================================================================

DO $$
DECLARE
    v_tabela text;
BEGIN
    FOREACH v_tabela IN ARRAY ARRAY[
        'semi_acabado_lancamento',
        'semi_acabado_pesagem'
    ]
    LOOP
        IF NOT has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'SELECT'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_SELECT_AUSENTE: %',
                v_tabela;
        END IF;

        IF NOT has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'INSERT'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_INSERT_AUSENTE: %',
                v_tabela;
        END IF;

        IF NOT has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'UPDATE'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_UPDATE_AUSENTE: %',
                v_tabela;
        END IF;

        IF has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'DELETE'
        )
        OR has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'TRUNCATE'
        )
        OR has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'REFERENCES'
        )
        OR has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_tabela),
            'TRIGGER'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_PRIVILEGIO_TABELA_INDEVIDO: %',
                v_tabela;
        END IF;
    END LOOP;
END
$$;

-- =====================================================================
-- 6. VALIDACAO FAIL-CLOSED DAS SEQUENCES
-- =====================================================================

DO $$
DECLARE
    r record;
    v_sequence text;
BEGIN
    FOR r IN
        SELECT *
        FROM (
            VALUES
                (
                    'semi_acabado_lancamento',
                    'codigo_semi_acabado_lancamento'
                ),
                (
                    'semi_acabado_pesagem',
                    'codigo_semi_acabado_pesagem'
                )
        ) AS x(tabela, coluna)
    LOOP
        v_sequence :=
            pg_get_serial_sequence(
                format('homologacao.%I', r.tabela),
                r.coluna
            );

        IF v_sequence IS NULL THEN
            RAISE EXCEPTION
                'STOP_037A_SEQUENCE_NAO_RESOLVIDA_VALIDACAO: %.%',
                r.tabela,
                r.coluna;
        END IF;

        IF NOT has_sequence_privilege(
            'fugapet_q_app',
            v_sequence,
            'USAGE'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_SEQUENCE_USAGE_AUSENTE: %',
                v_sequence;
        END IF;

        IF NOT has_sequence_privilege(
            'fugapet_q_app',
            v_sequence,
            'SELECT'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_SEQUENCE_SELECT_AUSENTE: %',
                v_sequence;
        END IF;

        IF has_sequence_privilege(
            'fugapet_q_app',
            v_sequence,
            'UPDATE'
        ) THEN
            RAISE EXCEPTION
                'STOP_037A_SEQUENCE_UPDATE_INDEVIDO: %',
                v_sequence;
        END IF;
    END LOOP;
END
$$;

COMMIT;