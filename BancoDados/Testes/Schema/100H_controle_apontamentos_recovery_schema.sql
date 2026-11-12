-- =====================================================================
-- FugaPET - Recovery 100H
-- Canonicalizacao estrutural contemporanea do Controle de Apontamentos
--
-- GATE:
-- FUGAPET-Q-RECOVERY-100H-DDL-CANONICALIZATION-100H3A
--
-- FINALIDADE:
--   * bootstrap de database isolado de teste;
--   * formalizacao versionavel do shape atual requerido pelo recovery 100H;
--   * NAO representa a migration historica 048 ou 051;
--   * NAO deve ser aplicado no database Q;
--   * NAO contem backfill, seed operacional ou copia de dados.
--
-- PRE-REQUISITOS:
--   1. database alvo EXATO: fuga_balanca_teste_recovery_100h;
--   2. schema homologacao existente;
--   3. 039_controle_apontamentos_HML_PROPOSTA_GAIA aplicado;
--   4. objetos de consumo necessarios ao harness aplicados por suas fontes
--      versionadas proprias;
--   5. homologacao.operacao_producao_apontamento_processo AUSENTE.
--
-- IDEMPOTENCIA:
--   MODE_A_FAIL_IF_1N_EXISTS
--   Este artefato falha se a tabela 1:N ja existir. Ele nao mascara drift
--   com IF NOT EXISTS.
--
-- DELTAS FORMALIZADOS AQUI:
--   A. cria homologacao.operacao_producao_apontamento_processo no shape
--      atual observado no catalogo Q;
--   B. normaliza homologacao.operacao_producao_evento.resultado para TEXT
--      para equivalencia com o shape atual Q.
--
-- FORA DE ESCOPO:
--   * codigo_perfil_resultado: usar a fonte versionada 054;
--   * tabelas de consumo: usar a fonte versionada 030;
--   * qualquer dado operacional;
--   * qualquer DDL/DML no Q.
-- =====================================================================

\set ON_ERROR_STOP on
\pset pager off

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SET LOCAL search_path = homologacao, pg_catalog;

-- =====================================================================
-- 1. GUARD FAIL-CLOSED DO DATABASE E DOS PRE-REQUISITOS
-- =====================================================================

DO $guard$
DECLARE
    v_resultado_data_type text;
    v_resultado_max_len integer;
BEGIN
    IF current_database() <> 'fuga_balanca_teste_recovery_100h' THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_DATABASE_EXATO: atual=% esperado=fuga_balanca_teste_recovery_100h',
            current_database();
    END IF;

    IF current_database() NOT LIKE 'fuga_balanca_teste_%' THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_PREFIXO_DATABASE: %',
            current_database();
    END IF;

    IF current_database() = 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_Q_DATABASE_PROIBIDO';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento') IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_APONTAMENTO_AUSENTE';
    END IF;

    IF to_regclass('homologacao.operacao_producao_evento') IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_EVENTO_AUSENTE';
    END IF;

    IF to_regclass('homologacao.consumo_material_lancamento') IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_CONSUMO_LANCAMENTO_AUSENTE';
    END IF;

    IF to_regclass('homologacao.consumo_material_item') IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_CONSUMO_ITEM_AUSENTE';
    END IF;

    IF to_regclass(
        'homologacao.operacao_producao_apontamento_processo'
    ) IS NOT NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_1N_JA_EXISTE';
    END IF;

    -- Shape minimo autoritativo do 039 exigido pelo runtime 35b094d8.
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'resultado_operacional'
          AND data_type = 'character varying'
          AND character_maximum_length = 40
          AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_RESULTADO_OPERACIONAL_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_registro_processo'
          AND data_type = 'bigint'
          AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_CODIGO_REGISTRO_PROCESSO_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'concluido_operacional_em'
          AND data_type = 'timestamp with time zone'
          AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_CONCLUIDO_OPERACIONAL_EM_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'mensagem_resultado_operacional'
          AND data_type = 'text'
          AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_MENSAGEM_RESULTADO_OPERACIONAL_DIVERGENTE';
    END IF;

    SELECT
        data_type,
        character_maximum_length
    INTO
        v_resultado_data_type,
        v_resultado_max_len
    FROM information_schema.columns
    WHERE table_schema = 'homologacao'
      AND table_name = 'operacao_producao_evento'
      AND column_name = 'resultado';

    IF v_resultado_data_type IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_EVENTO_RESULTADO_AUSENTE';
    END IF;

    IF NOT (
        v_resultado_data_type = 'text'
        OR (
            v_resultado_data_type = 'character varying'
            AND v_resultado_max_len = 40
        )
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_ABORT_EVENTO_RESULTADO_DIVERGENTE: type=% len=%',
            v_resultado_data_type,
            v_resultado_max_len;
    END IF;
END
$guard$;

-- =====================================================================
-- 2. OBJETO 1:N CANONICO ATUAL
-- =====================================================================

CREATE TABLE homologacao.operacao_producao_apontamento_processo
(
    codigo_apontamento_processo
        BIGINT GENERATED BY DEFAULT AS IDENTITY,

    codigo_apontamento
        BIGINT NOT NULL,

    tipo_processo
        TEXT NOT NULL,

    codigo_registro_processo
        BIGINT NOT NULL,

    criado_em
        TIMESTAMPTZ NOT NULL DEFAULT now(),

    atualizado_em
        TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT operacao_producao_apontamento_processo_pkey
        PRIMARY KEY (codigo_apontamento_processo),

    CONSTRAINT fk_op_apontamento_processo_apontamento
        FOREIGN KEY (codigo_apontamento)
        REFERENCES homologacao.operacao_producao_apontamento
            (codigo_apontamento)
        ON DELETE RESTRICT,

    CONSTRAINT uq_op_apontamento_processo_tripla
        UNIQUE
        (
            codigo_apontamento,
            tipo_processo,
            codigo_registro_processo
        ),

    CONSTRAINT ck_op_apontamento_processo_tipo_nao_vazio
        CHECK (btrim(tipo_processo) <> '')
);

-- Nao existe FK polimorfica para codigo_registro_processo.
-- Os dois indices observados no Q sao produzidos automaticamente
-- pela PK e pela UNIQUE acima; nenhum indice adicional e criado aqui.

-- =====================================================================
-- 3. EQUIVALENCIA CONTEMPORANEA DO EVENTO
-- =====================================================================
--
-- O 039 versionado criou resultado como varchar(40).
-- O catalogo Q atual materializa resultado como text.
-- O recovery 100H, isoladamente, nao exige text (seus valores cabem em
-- varchar(40)); esta normalizacao pertence a autoridade estrutural atual,
-- nao a uma suposta "migration 051 original".
-- =====================================================================

ALTER TABLE homologacao.operacao_producao_evento
    ALTER COLUMN resultado TYPE text;

-- =====================================================================
-- 4. POSTCHECKS FAIL-CLOSED
-- =====================================================================

DO $postcheck$
DECLARE
    v_column_count integer;
    v_index_count integer;
    v_custom_trigger_count integer;
BEGIN
    SELECT count(*)
    INTO v_column_count
    FROM information_schema.columns
    WHERE table_schema = 'homologacao'
      AND table_name =
          'operacao_producao_apontamento_processo';

    IF v_column_count <> 6 THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_COLUMN_COUNT: atual=% esperado=6',
            v_column_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name =
              'operacao_producao_apontamento_processo'
          AND column_name = 'codigo_apontamento_processo'
          AND data_type = 'bigint'
          AND is_nullable = 'NO'
          AND is_identity = 'YES'
          AND identity_generation = 'BY DEFAULT'
          AND identity_start = '1'
          AND identity_increment = '1'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_IDENTITY_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name =
              'operacao_producao_apontamento_processo'
          AND column_name = 'codigo_apontamento'
          AND data_type = 'bigint'
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_CODIGO_APONTAMENTO_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name =
              'operacao_producao_apontamento_processo'
          AND column_name = 'tipo_processo'
          AND data_type = 'text'
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_TIPO_PROCESSO_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name =
              'operacao_producao_apontamento_processo'
          AND column_name = 'codigo_registro_processo'
          AND data_type = 'bigint'
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_CODIGO_REGISTRO_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name =
              'operacao_producao_apontamento_processo'
          AND column_name = 'criado_em'
          AND data_type = 'timestamp with time zone'
          AND is_nullable = 'NO'
          AND column_default = 'now()'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_CRIADO_EM_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name =
              'operacao_producao_apontamento_processo'
          AND column_name = 'atualizado_em'
          AND data_type = 'timestamp with time zone'
          AND is_nullable = 'NO'
          AND column_default = 'now()'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_ATUALIZADO_EM_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint con
        WHERE con.conrelid =
              'homologacao.operacao_producao_apontamento_processo'
              ::regclass
          AND con.conname =
              'operacao_producao_apontamento_processo_pkey'
          AND con.contype = 'p'
          AND pg_get_constraintdef(con.oid, true) =
              'PRIMARY KEY (codigo_apontamento_processo)'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_PK_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint con
        WHERE con.conrelid =
              'homologacao.operacao_producao_apontamento_processo'
              ::regclass
          AND con.conname =
              'fk_op_apontamento_processo_apontamento'
          AND con.contype = 'f'
          AND con.confdeltype = 'r'
          AND pg_get_constraintdef(con.oid, true) =
              'FOREIGN KEY (codigo_apontamento) REFERENCES operacao_producao_apontamento(codigo_apontamento) ON DELETE RESTRICT'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_FK_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint con
        WHERE con.conrelid =
              'homologacao.operacao_producao_apontamento_processo'
              ::regclass
          AND con.conname =
              'uq_op_apontamento_processo_tripla'
          AND con.contype = 'u'
          AND pg_get_constraintdef(con.oid, true) =
              'UNIQUE (codigo_apontamento, tipo_processo, codigo_registro_processo)'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_UNIQUE_TRIPLA_DIVERGENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint con
        WHERE con.conrelid =
              'homologacao.operacao_producao_apontamento_processo'
              ::regclass
          AND con.conname =
              'ck_op_apontamento_processo_tipo_nao_vazio'
          AND con.contype = 'c'
          AND pg_get_constraintdef(con.oid, true) =
              'CHECK (btrim(tipo_processo) <> ''''::text)'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_CHECK_TIPO_DIVERGENTE';
    END IF;

    SELECT count(*)
    INTO v_index_count
    FROM pg_indexes
    WHERE schemaname = 'homologacao'
      AND tablename =
          'operacao_producao_apontamento_processo';

    IF v_index_count <> 2 THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_INDEX_COUNT: atual=% esperado=2',
            v_index_count;
    END IF;

    IF pg_get_serial_sequence(
        'homologacao.operacao_producao_apontamento_processo',
        'codigo_apontamento_processo'
    ) IS NULL THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_IDENTITY_SEQUENCE_AUSENTE';
    END IF;

    SELECT count(*)
    INTO v_custom_trigger_count
    FROM pg_trigger
    WHERE tgrelid =
          'homologacao.operacao_producao_apontamento_processo'
          ::regclass
      AND NOT tgisinternal;

    IF v_custom_trigger_count <> 0 THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_CUSTOM_TRIGGER_COUNT: atual=% esperado=0',
            v_custom_trigger_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_evento'
          AND column_name = 'resultado'
          AND data_type = 'text'
          AND character_maximum_length IS NULL
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            '100H_CANON_POSTCHECK_EVENTO_RESULTADO_NAO_TEXT';
    END IF;
END
$postcheck$;

COMMIT;

\echo ============================================================
\echo 100H_CANONICAL_SCHEMA_APPLIED=SIM
\echo Q_APPLICATION_REQUIRED=NAO
\echo BACKFILL_EXECUTED=NAO
\echo OPERATIONAL_SEED_EXECUTED=NAO
\echo ============================================================
