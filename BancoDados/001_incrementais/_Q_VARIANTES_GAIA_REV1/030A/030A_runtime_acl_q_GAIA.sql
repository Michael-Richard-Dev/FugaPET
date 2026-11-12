-- =====================================================================
-- FugaPET Q
-- GATE FUGAPET-Q-DB-INSTALL-EXEC-16A
-- Corretiva formal ACL 030
--
-- ESCOPO:
--   ACL runtime somente.
--
-- PROIBIDO POR ESTE ARTEFATO:
--   - alterar estrutura
--   - alterar dados
--   - alterar owner
--   - criar role
--   - criar database
--   - alterar HML
--   - alterar HBA
--   - SAP/CPI/HTTP
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- FAIL-CLOSED: database e execution role
-- ---------------------------------------------------------------------

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

-- ---------------------------------------------------------------------
-- FAIL-CLOSED: objetos e ownership
-- ---------------------------------------------------------------------

DO $$
DECLARE
    v_objeto text;
BEGIN
    FOREACH v_objeto IN ARRAY ARRAY[
        'consumo_material_lancamento',
        'consumo_material_item',
        'consumo_material_pesagem'
    ]
    LOOP
        IF to_regclass(
            format('homologacao.%I', v_objeto)
        ) IS NULL THEN
            RAISE EXCEPTION
                'STOP_TABELA_AUSENTE: homologacao.%',
                v_objeto;
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM pg_class c
            JOIN pg_namespace n
              ON n.oid = c.relnamespace
            WHERE n.nspname = 'homologacao'
              AND c.relname = v_objeto
              AND c.relkind IN ('r', 'p')
              AND pg_get_userbyid(c.relowner) =
                  'fugapet_q_owner'
        ) THEN
            RAISE EXCEPTION
                'STOP_OWNER_DIVERGENTE: homologacao.%',
                v_objeto;
        END IF;
    END LOOP;
END
$$;

-- ---------------------------------------------------------------------
-- TABLE ACL
-- ---------------------------------------------------------------------

REVOKE ALL PRIVILEGES
ON TABLE
    homologacao.consumo_material_lancamento,
    homologacao.consumo_material_item,
    homologacao.consumo_material_pesagem
FROM PUBLIC;

REVOKE
    DELETE,
    TRUNCATE,
    REFERENCES,
    TRIGGER
ON TABLE
    homologacao.consumo_material_lancamento,
    homologacao.consumo_material_item,
    homologacao.consumo_material_pesagem
FROM fugapet_q_app;

GRANT
    SELECT,
    INSERT,
    UPDATE
ON TABLE
    homologacao.consumo_material_lancamento,
    homologacao.consumo_material_item,
    homologacao.consumo_material_pesagem
TO fugapet_q_app;

-- ---------------------------------------------------------------------
-- IDENTITY SEQUENCE ACL
-- Nomes resolvidos materialmente pelo catálogo.
-- ---------------------------------------------------------------------

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
                    'consumo_material_lancamento',
                    'codigo_consumo_material_lancamento'
                ),
                (
                    'consumo_material_item',
                    'codigo_consumo_material_item'
                ),
                (
                    'consumo_material_pesagem',
                    'codigo_consumo_material_pesagem'
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
                'STOP_SEQUENCE_NAO_RESOLVIDA: %.%',
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
                'STOP_SEQUENCE_OWNER_DIVERGENTE: %',
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

-- ---------------------------------------------------------------------
-- VALIDAÇÃO FAIL-CLOSED DAS TABELAS
-- ---------------------------------------------------------------------

DO $$
DECLARE
    v_objeto text;
BEGIN
    FOREACH v_objeto IN ARRAY ARRAY[
        'consumo_material_lancamento',
        'consumo_material_item',
        'consumo_material_pesagem'
    ]
    LOOP
        IF NOT has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'SELECT'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_SELECT: %',
                v_objeto;
        END IF;

        IF NOT has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'INSERT'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_INSERT: %',
                v_objeto;
        END IF;

        IF NOT has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'UPDATE'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_UPDATE: %',
                v_objeto;
        END IF;

        IF has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'DELETE'
        )
        OR has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'TRUNCATE'
        )
        OR has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'REFERENCES'
        )
        OR has_table_privilege(
            'fugapet_q_app',
            format('homologacao.%I', v_objeto),
            'TRIGGER'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_PRIVILEGIO_INDEVIDO_TABELA: %',
                v_objeto;
        END IF;
    END LOOP;
END
$$;

-- ---------------------------------------------------------------------
-- VALIDAÇÃO FAIL-CLOSED DAS SEQUENCES
-- ---------------------------------------------------------------------

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
                    'consumo_material_lancamento',
                    'codigo_consumo_material_lancamento'
                ),
                (
                    'consumo_material_item',
                    'codigo_consumo_material_item'
                ),
                (
                    'consumo_material_pesagem',
                    'codigo_consumo_material_pesagem'
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
                'ACL_FAIL_SEQUENCE_NAO_RESOLVIDA: %.%',
                r.tabela,
                r.coluna;
        END IF;

        IF NOT has_sequence_privilege(
            'fugapet_q_app',
            v_sequence,
            'USAGE'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_SEQUENCE_USAGE: %',
                v_sequence;
        END IF;

        IF NOT has_sequence_privilege(
            'fugapet_q_app',
            v_sequence,
            'SELECT'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_SEQUENCE_SELECT: %',
                v_sequence;
        END IF;

        IF has_sequence_privilege(
            'fugapet_q_app',
            v_sequence,
            'UPDATE'
        ) THEN
            RAISE EXCEPTION
                'ACL_FAIL_SEQUENCE_UPDATE_INDEVIDO: %',
                v_sequence;
        END IF;
    END LOOP;
END
$$;

COMMIT;