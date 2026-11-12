\set ON_ERROR_STOP on
-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_050_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_050_ABORT_DATABASE atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_050_ABORT_CURRENT_USER atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_050_ABORT_APP_NAO_PODE_EXECUTAR_MIGRATION';
    END IF;
END
$gaia_q_050_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================================
-- 050_consumo_material_zero_intencional_Q_PROPOSTA_GAIA.sql
-- Projeto: FugaPET Q
-- Schema: homologacao
-- Gate: 050-E2E-OP1002101-0060-ZERO-INTENCIONAL-MIGRATION-SOURCE-01
--
-- FINALIDADE
--   Criar persistencia auditavel e explicita da decisao operacional
--   ZERO_INTENCIONAL por componente, independente do pipeline SAP 261.
--
-- CONTRATO
--   - Nao gera consumo_material_item.
--   - Nao possui status SAP nem tipo_movimento_sap.
--   - Ausencia de linha continua significando componente PENDENTE.
--   - Uma decisao so cobre a chave exata:
--       codigo_apontamento + numero_reserva + item_reserva
--     e o agregado deve ainda conferir codigo_material com a matriz esperada.
--   - APP: SELECT + INSERT; sem UPDATE/DELETE.
--   - Sem backfill.
--
-- MODALIDADE DA MIGRATION
--   ONE-SHOT / FAIL-CLOSED.
--   Deliberadamente NAO usa CREATE TABLE IF NOT EXISTS.
--   Se o objeto alvo ja existir, a instalacao aborta para nao mascarar
--   schema parcial/divergente.
--
-- NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================================

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '2min';
SET LOCAL search_path = pg_catalog, homologacao;

-- ============================================================================
-- 0. PREFLIGHT FAIL-CLOSED
-- ============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.schemata
        WHERE schema_name = 'homologacao'
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: schema homologacao nao existe.';
    END IF;

    IF to_regclass('homologacao.consumo_material_componente_decisao') IS NOT NULL THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_componente_decisao ja existe; revisar instalacao parcial/divergente.';
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento') IS NULL THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: operacao_producao_apontamento ausente.';
    END IF;

    IF to_regclass('homologacao.consumo_material_item') IS NULL THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_item ausente; nao foi possivel validar compatibilidade de tipos.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_apontamento'
          AND data_type = 'bigint'
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: tipo/nullability de operacao_producao_apontamento.codigo_apontamento divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'consumo_material_item'
          AND column_name = 'numero_reserva'
          AND data_type = 'character varying'
          AND character_maximum_length = 10
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_item.numero_reserva nao e varchar(10).';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'consumo_material_item'
          AND column_name = 'item_reserva'
          AND data_type = 'character varying'
          AND character_maximum_length = 8
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_item.item_reserva nao e varchar(8).';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'consumo_material_item'
          AND column_name = 'codigo_material'
          AND data_type = 'character varying'
          AND character_maximum_length = 40
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_item.codigo_material nao e varchar(40).';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'consumo_material_item'
          AND column_name = 'quantidade_consumida_local'
          AND data_type = 'numeric'
          AND numeric_precision = 18
          AND numeric_scale = 3
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_item.quantidade_consumida_local nao e numeric(18,3).';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'consumo_material_item'
          AND column_name = 'unidade'
          AND data_type = 'character varying'
          AND character_maximum_length = 3
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: consumo_material_item.unidade nao e varchar(3) NOT NULL.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_app') THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: role fugapet_q_app ausente.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_owner') THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: role fugapet_q_owner ausente.';
    END IF;

    IF NOT has_schema_privilege('fugapet_q_app', 'homologacao', 'USAGE') THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: fugapet_q_app sem USAGE no schema homologacao.';
    END IF;
END
$$;

-- ============================================================================
-- 1. TABELA DE DECISAO OPERACIONAL
-- ============================================================================

CREATE TABLE homologacao.consumo_material_componente_decisao
(
    codigo_decisao      bigint GENERATED BY DEFAULT AS IDENTITY,
    codigo_apontamento  bigint         NOT NULL,
    numero_reserva      varchar(10)     NOT NULL,
    item_reserva        varchar(8)      NOT NULL,
    codigo_material     varchar(40)     NOT NULL,
    decisao_operacional varchar(30)     NOT NULL,
    quantidade          numeric(18,3)   NOT NULL,
    unidade             varchar(3)      NOT NULL,
    usuario             varchar(120)    NOT NULL,
    estacao             varchar         NOT NULL,
    decidido_em         timestamptz     NOT NULL DEFAULT now(),

    CONSTRAINT pk_consumo_material_comp_decisao
        PRIMARY KEY (codigo_decisao),

    CONSTRAINT fk_consumo_material_comp_decisao_apontamento
        FOREIGN KEY (codigo_apontamento)
        REFERENCES homologacao.operacao_producao_apontamento (codigo_apontamento)
        ON DELETE RESTRICT,

    CONSTRAINT uq_consumo_material_comp_decisao_ap_res_item
        UNIQUE (codigo_apontamento, numero_reserva, item_reserva),

    CONSTRAINT ck_consumo_material_comp_decisao_decisao
        CHECK (decisao_operacional = 'ZERO_INTENCIONAL'),

    CONSTRAINT ck_consumo_material_comp_decisao_quantidade
        CHECK (
            decisao_operacional <> 'ZERO_INTENCIONAL'
            OR quantidade = 0::numeric
        ),

    CONSTRAINT ck_consumo_material_comp_decisao_identidade
        CHECK (
            btrim(numero_reserva) <> ''
            AND btrim(item_reserva) <> ''
            AND btrim(codigo_material) <> ''
            AND btrim(unidade) <> ''
            AND btrim(usuario) <> ''
            AND btrim(estacao) <> ''
        )
);

COMMENT ON TABLE homologacao.consumo_material_componente_decisao IS
    'Decisao operacional explicita por componente. ZERO_INTENCIONAL e local, auditavel e fora do pipeline SAP 261.';

COMMENT ON COLUMN homologacao.consumo_material_componente_decisao.codigo_apontamento IS
    'Apontamento operacional ao qual a decisao pertence.';

COMMENT ON COLUMN homologacao.consumo_material_componente_decisao.numero_reserva IS
    'Reservation exata do componente.';

COMMENT ON COLUMN homologacao.consumo_material_componente_decisao.item_reserva IS
    'ReservationItem exato do componente.';

COMMENT ON COLUMN homologacao.consumo_material_componente_decisao.codigo_material IS
    'Material esperado para a Reservation/ReservationItem; o agregado deve conferir a identidade completa.';

COMMENT ON COLUMN homologacao.consumo_material_componente_decisao.decisao_operacional IS
    'Decisao local explicita; inicialmente somente ZERO_INTENCIONAL.';

COMMENT ON COLUMN homologacao.consumo_material_componente_decisao.quantidade IS
    'Quantidade decidida localmente. ZERO_INTENCIONAL exige exatamente 0.000.';

-- ============================================================================
-- 2. INDEXES
-- ============================================================================
-- Nao criar indice adicional em codigo_apontamento:
-- o indice UNIQUE gerado por uq_consumo_material_comp_decisao_ap_res_item
-- possui codigo_apontamento como primeira coluna e cobre:
--   (a) leitura por codigo_apontamento;
--   (b) correlacao codigo_apontamento + numero_reserva + item_reserva.
-- Evita indice redundante.

-- ============================================================================
-- 3. ACL / APPEND-ONLY PARA O RUNTIME
-- ============================================================================

REVOKE ALL
    ON TABLE homologacao.consumo_material_componente_decisao
    FROM PUBLIC;

REVOKE ALL
    ON TABLE homologacao.consumo_material_componente_decisao
    FROM fugapet_q_app;

GRANT SELECT, INSERT
    ON TABLE homologacao.consumo_material_componente_decisao
    TO fugapet_q_app;

REVOKE ALL
    ON TABLE homologacao.consumo_material_componente_decisao
    FROM fugapet_q_owner;

GRANT SELECT, INSERT
    ON TABLE homologacao.consumo_material_componente_decisao
    TO fugapet_q_owner;

REVOKE ALL
    ON SEQUENCE homologacao.consumo_material_componente_decisao_codigo_decisao_seq
    FROM PUBLIC;

REVOKE ALL
    ON SEQUENCE homologacao.consumo_material_componente_decisao_codigo_decisao_seq
    FROM fugapet_q_app;

GRANT USAGE, SELECT
    ON SEQUENCE homologacao.consumo_material_componente_decisao_codigo_decisao_seq
    TO fugapet_q_app;

REVOKE ALL
    ON SEQUENCE homologacao.consumo_material_componente_decisao_codigo_decisao_seq
    FROM fugapet_q_owner;

GRANT USAGE, SELECT
    ON SEQUENCE homologacao.consumo_material_componente_decisao_codigo_decisao_seq
    TO fugapet_q_owner;

-- ============================================================================
-- 4. ASSERTIONS PRE-COMMIT
-- ============================================================================

DO $$
BEGIN
    IF to_regclass('homologacao.consumo_material_componente_decisao') IS NULL THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: tabela alvo nao existe no pre-commit.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.consumo_material_componente_decisao
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: migration estrutural deve terminar com zero rows.';
    END IF;

    IF NOT has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'SELECT'
    ) OR NOT has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'INSERT'
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: APP sem SELECT/INSERT.';
    END IF;

    IF has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'UPDATE'
    ) OR has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'DELETE'
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: APP recebeu UPDATE/DELETE; append-only violado.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'homologacao.consumo_material_componente_decisao'::regclass
          AND contype = 'f'
          AND confrelid = 'homologacao.consumo_material_item'::regclass
    ) THEN
        RAISE EXCEPTION
            'MIGRATION 050 ABORTADA: FK indevida para consumo_material_item.';
    END IF;
END
$$;

COMMIT;

SELECT
    'MIGRATION 050 PREPARADA/APLICADA QUANDO AUTORIZADA: consumo_material_componente_decisao criada sem backfill'
    AS resultado_migration;
