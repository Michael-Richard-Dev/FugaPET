-- =====================================================================
-- FugaPET - Recovery 100H
-- Bootstrap-base minimo para database vazio
--
-- GATE:
-- FUGAPET-Q-RECOVERY-100H-TESTDB-BASE-CANONICALIZATION-100H4A
--
-- TYPE:
-- NEW_CURRENT_TEST_BOOTSTRAP_BASE_AUTHORITY
--
-- FINALIDADE:
--   Preparar SOMENTE os pre-requisitos estruturais necessarios para que
--   BancoDados/Testes/Schema/100H_controle_apontamentos_recovery_schema.sql
--   seja executado em seguida, sem alteracao.
--
-- ESTE ARTEFATO:
--   * NAO e a migration historica 030, 039, 048 ou 051;
--   * NAO substitui o delta 100H ja versionado;
--   * NAO cria operacao_producao_apontamento_processo;
--   * NAO cria codigo_perfil_resultado;
--   * NAO cria operacao_producao_configuracao;
--   * NAO contem dados, seeds, backfills ou copia de Q.
--
-- FONTES ESTRUTURAIS VERSIONADAS RECONCILIADAS:
--
-- 1. schema homologacao:
--    BancoDados/000_baseline/Banco_Homologacao_V1_1_Geral/
--    000_execucao_completa_homologacao_v1_1.sql
--
-- 2. funcao de atualizado_em corrigida:
--    BancoDados/001_incrementais/
--    024_h14_precisao_data_hora_auditoria_PROPOSTA_GAIA.sql
--    SHA256=C884E87FE5DE92060C494276D689A95AF2D338EC9202E2B8042C53D9C1DEDA06
--
-- 3. persistencia local de consumo:
--    BancoDados/001_incrementais/030_consumo_material_persistencia_local_GAIA/
--    030_consumo_material_persistencia_local_PROPOSTA_GAIA.sql
--    SHA256=3E310281D51CF70C5E135C7B477C93974FCBC5E666B2CB4A9B2AB0FC15C86FB2
--
-- 4. Controle de Apontamentos:
--    BancoDados/001_incrementais/
--    039_controle_apontamentos_producao_HML_GAIA_CORRIGIDO_FINAL.zip
--    ZIP_SHA256=D6B86F4ACC026243F32A6A514B9EAA69A29A93BD76DA9C49B5F04B741D87541E
--    ENTRY=039_controle_apontamentos_HML_PROPOSTA_GAIA.sql
--    ENTRY_SHA256=03642C65BB5B15D1D7F5565230E54794E7E476BB3950F5728F9FB46A84B72687
--
-- MODELO DE IDEMPOTENCIA:
--   FAIL_IF_BASE_OBJECTS_ALREADY_EXIST
--
-- DATABASE ALVO EXATO:
--   fuga_balanca_teste_recovery_100h
--
-- RESULTADO ESPERADO DESTE BASE:
--   schema homologacao
--   fn_definir_atualizado_em()
--   operacao_producao_apontamento
--   operacao_producao_evento
--   consumo_material_lancamento
--   consumo_material_item
--   consumo_material_pesagem
--
-- OBJETO RESERVADO AO DELTA 100H:
--   operacao_producao_apontamento_processo
-- =====================================================================

\set ON_ERROR_STOP on
\pset pager off

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SET LOCAL search_path = pg_catalog;

-- =====================================================================
-- 1. GUARD FAIL-CLOSED ANTES DE QUALQUER DDL
-- =====================================================================

DO $guard$
BEGIN
    IF current_database() <> 'fuga_balanca_teste_recovery_100h' THEN
        RAISE EXCEPTION
            '100H_BASE_ABORT_DATABASE_EXATO: atual=% esperado=fuga_balanca_teste_recovery_100h',
            current_database();
    END IF;

    IF current_database() NOT LIKE 'fuga_balanca_teste_%' THEN
        RAISE EXCEPTION
            '100H_BASE_ABORT_PREFIXO_DATABASE: %',
            current_database();
    END IF;

    IF current_database() = 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            '100H_BASE_ABORT_Q_DATABASE_PROIBIDO';
    END IF;

    IF NOT has_database_privilege(
        current_user,
        current_database(),
        'CREATE'
    ) THEN
        RAISE EXCEPTION
            '100H_BASE_ABORT_SEM_CREATE_NO_DATABASE: role=%',
            current_user;
    END IF;

    -- O contrato deste artefato e database estruturalmente vazio.
    IF to_regnamespace('homologacao') IS NOT NULL THEN
        RAISE EXCEPTION
            '100H_BASE_ABORT_SCHEMA_HOMOLOGACAO_JA_EXISTE';
    END IF;

    -- Defesa adicional contra execucao em database previamente materializado.
    IF to_regclass('homologacao.operacao_producao_apontamento') IS NOT NULL
       OR to_regclass('homologacao.operacao_producao_evento') IS NOT NULL
       OR to_regclass('homologacao.consumo_material_lancamento') IS NOT NULL
       OR to_regclass('homologacao.consumo_material_item') IS NOT NULL
       OR to_regclass('homologacao.consumo_material_pesagem') IS NOT NULL
       OR to_regclass(
            'homologacao.operacao_producao_apontamento_processo'
          ) IS NOT NULL
    THEN
        RAISE EXCEPTION
            '100H_BASE_ABORT_OBJETO_BASE_OU_1N_JA_EXISTE';
    END IF;
END
$guard$;

-- =====================================================================
-- 2. SCHEMA MINIMO
-- Fonte: baseline versionado.
-- =====================================================================

CREATE SCHEMA homologacao;

SET LOCAL search_path = homologacao, pg_catalog;

-- =====================================================================
-- 3. FUNCAO DE atualizado_em
-- Fonte: 024 versionado, corpo corrigido com clock_timestamp().
-- =====================================================================

CREATE OR REPLACE FUNCTION homologacao.fn_definir_atualizado_em()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_coluna_tabela text := TG_TABLE_NAME || '_atualizado_em';
BEGIN
    IF to_jsonb(NEW) ? v_coluna_tabela THEN
        NEW := jsonb_populate_record(
            NEW,
            jsonb_build_object(v_coluna_tabela, clock_timestamp())
        );
    END IF;
    RETURN NEW;
END;
$$;

-- =====================================================================
-- 4. CONSUMO MATERIAL - ESTRUTURA 030
-- Zero dados.
-- =====================================================================

CREATE TABLE homologacao.consumo_material_lancamento
(
    codigo_consumo_material_lancamento
        bigserial PRIMARY KEY,

    numero_ordem
        varchar(12) NOT NULL,

    centro
        varchar(4),

    material_produzido
        varchar(40),

    lote_ordem
        varchar(40),

    quantidade_prevista
        numeric(18,3),

    unidade
        varchar(3),

    status_lancamento
        varchar(30) NOT NULL DEFAULT 'PENDENTE_SAP',

    observacao
        text,

    usuario_criacao
        varchar(120),

    documento_material_sap
        varchar(10),

    exercicio_documento_material_sap
        varchar(4),

    confirmation_group_sap
        varchar(10),

    confirmation_count_sap
        varchar(8),

    enviado_sap_em
        timestamptz,

    consumo_material_lancamento_criado_em
        timestamptz NOT NULL DEFAULT now(),

    consumo_material_lancamento_atualizado_em
        timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_consumo_lancamento_status
        CHECK (
            status_lancamento IN (
                'PENDENTE_SAP',
                'ENVIANDO_SAP',
                'CONFIRMADO_SAP',
                'FALHA_SAP',
                'CANCELADO_LOCAL'
            )
        ),

    CONSTRAINT ck_consumo_lancamento_exercicio_formato
        CHECK (
            exercicio_documento_material_sap IS NULL
            OR exercicio_documento_material_sap ~ '^[0-9]{4}$'
        )
);

CREATE TABLE homologacao.consumo_material_item
(
    codigo_consumo_material_item
        bigserial PRIMARY KEY,

    codigo_consumo_material_lancamento
        bigint NOT NULL
        REFERENCES homologacao.consumo_material_lancamento
            (codigo_consumo_material_lancamento)
        ON DELETE CASCADE,

    numero_ordem
        varchar(12) NOT NULL,

    codigo_material
        varchar(40) NOT NULL,

    descricao_material
        varchar(255),

    centro
        varchar(4),

    deposito_consumo
        varchar(4),

    numero_reserva
        varchar(10),

    item_reserva
        varchar(8),

    lote
        varchar(40),

    quantidade_prevista
        numeric(18,3),

    quantidade_retirada_sap
        numeric(18,3),

    quantidade_pendente_sap
        numeric(18,3),

    quantidade_consumida_local
        numeric(18,3) NOT NULL DEFAULT 0,

    unidade
        varchar(3) NOT NULL,

    tipo_movimento_sap
        varchar(3) NOT NULL DEFAULT '261',

    status_item
        varchar(30) NOT NULL DEFAULT 'PENDENTE_SAP',

    consumo_material_item_criado_em
        timestamptz NOT NULL DEFAULT now(),

    consumo_material_item_atualizado_em
        timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_consumo_item_status
        CHECK (
            status_item IN (
                'PENDENTE_SAP',
                'ENVIANDO_SAP',
                'CONFIRMADO_SAP',
                'FALHA_SAP',
                'CANCELADO_LOCAL'
            )
        )
);

CREATE TABLE homologacao.consumo_material_pesagem
(
    codigo_consumo_material_pesagem
        bigserial PRIMARY KEY,

    codigo_consumo_material_item
        bigint NOT NULL
        REFERENCES homologacao.consumo_material_item
            (codigo_consumo_material_item)
        ON DELETE CASCADE,

    sequencia
        integer NOT NULL,

    peso_bruto_kg
        numeric(18,3) NOT NULL,

    peso_tara_kg
        numeric(18,3) NOT NULL,

    peso_liquido_kg
        numeric(18,3) NOT NULL,

    unidade
        varchar(3) NOT NULL DEFAULT 'KG',

    origem
        varchar(20) NOT NULL,

    status_pesagem
        varchar(30) NOT NULL DEFAULT 'REGISTRADA_LOCALMENTE',

    pesado_em
        timestamptz NOT NULL,

    usuario_criacao
        varchar(120),

    consumo_material_pesagem_criado_em
        timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_consumo_pesagem_origem
        CHECK (origem IN ('BALANCA', 'MANUAL')),

    CONSTRAINT ck_consumo_pesagem_status
        CHECK (status_pesagem IN ('REGISTRADA_LOCALMENTE')),

    CONSTRAINT ck_consumo_pesagem_liquido_positivo
        CHECK (peso_liquido_kg > 0),

    CONSTRAINT ck_consumo_pesagem_tara_nao_negativa
        CHECK (peso_tara_kg >= 0)
);

CREATE INDEX ix_consumo_lancamento_numero_ordem
    ON homologacao.consumo_material_lancamento (numero_ordem);

CREATE INDEX ix_consumo_lancamento_status
    ON homologacao.consumo_material_lancamento (status_lancamento);

CREATE INDEX ix_consumo_lancamento_doc_material_sap
    ON homologacao.consumo_material_lancamento
       (documento_material_sap, exercicio_documento_material_sap);

CREATE INDEX ix_consumo_item_lancamento
    ON homologacao.consumo_material_item
       (codigo_consumo_material_lancamento);

CREATE INDEX ix_consumo_item_reserva
    ON homologacao.consumo_material_item
       (numero_reserva, item_reserva);

CREATE INDEX ix_consumo_pesagem_item
    ON homologacao.consumo_material_pesagem
       (codigo_consumo_material_item);

CREATE TRIGGER trg_consumo_material_lancamento_atualizado_em
BEFORE UPDATE
ON homologacao.consumo_material_lancamento
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_definir_atualizado_em();

CREATE TRIGGER trg_consumo_material_item_atualizado_em
BEFORE UPDATE
ON homologacao.consumo_material_item
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_definir_atualizado_em();

-- =====================================================================
-- 5. CONTROLE DE APONTAMENTOS - ESTRUTURA BASE 039
--
-- Deliberadamente NAO cria:
--   * operacao_producao_configuracao
--   * permissoes / perfil_permissao
--   * grants para fugapet_hml_app
--   * qualquer seed
--   * codigo_perfil_resultado
-- =====================================================================

CREATE TABLE homologacao.operacao_producao_apontamento
(
    codigo_apontamento
        bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,

    numero_ordem
        varchar(40) NOT NULL,

    item_ordem
        varchar(20) NOT NULL DEFAULT '',

    produto
        varchar(80) NOT NULL DEFAULT '',

    sequencia
        varchar(10) NOT NULL DEFAULT '',

    operacao
        varchar(10) NOT NULL,

    suboperacao
        varchar(10) NOT NULL DEFAULT '',

    descricao_operacao
        varchar(255) NOT NULL DEFAULT '',

    centro_trabalho
        varchar(20) NOT NULL DEFAULT '',

    tipo_processo
        varchar(40) NOT NULL DEFAULT '',

    tela_destino
        varchar(80) NOT NULL DEFAULT '',

    status
        varchar(30) NOT NULL DEFAULT 'EM_ANDAMENTO',

    usuario_inicio
        varchar(80) NOT NULL,

    estacao_inicio
        varchar(80) NOT NULL DEFAULT '',

    iniciado_em
        timestamptz NOT NULL DEFAULT now(),

    codigo_barras_inicio
        varchar(60) NOT NULL,

    usuario_termino
        varchar(80),

    estacao_termino
        varchar(80),

    terminado_em
        timestamptz,

    codigo_barras_termino
        varchar(60),

    correlation_id
        varchar(40) NOT NULL DEFAULT '',

    idempotency_key
        varchar(120) NOT NULL,

    idempotency_key_termino
        varchar(120),

    resultado_operacional
        varchar(40),

    codigo_registro_processo
        bigint,

    concluido_operacional_em
        timestamptz,

    mensagem_resultado_operacional
        text,

    criado_em
        timestamptz NOT NULL DEFAULT now(),

    atualizado_em
        timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_apontamento_status
        CHECK (
            status IN (
                'EM_ANDAMENTO',
                'AGUARDANDO_FINALIZACAO',
                'CONCLUIDA',
                'CANCELADA'
            )
        ),

    CONSTRAINT ck_apontamento_resultado_operacional
        CHECK (
            status NOT IN (
                'AGUARDANDO_FINALIZACAO',
                'CONCLUIDA'
            )
            OR (
                resultado_operacional IS NOT NULL
                AND resultado_operacional IN (
                    'ConcluidoLocalmente',
                    'ConfirmadoSap'
                )
                AND concluido_operacional_em IS NOT NULL
            )
        ),

    CONSTRAINT ck_apontamento_termino_completo
        CHECK (
            status <> 'CONCLUIDA'
            OR (
                terminado_em IS NOT NULL
                AND usuario_termino IS NOT NULL
                AND codigo_barras_termino IS NOT NULL
                AND idempotency_key_termino IS NOT NULL
            )
        ),

    CONSTRAINT ck_apontamento_ordem_cronologica
        CHECK (
            terminado_em IS NULL
            OR terminado_em >= iniciado_em
        )
);

CREATE UNIQUE INDEX uq_apontamento_ativo_por_operacao
    ON homologacao.operacao_producao_apontamento
       (numero_ordem, sequencia, operacao, suboperacao)
    WHERE status IN (
        'EM_ANDAMENTO',
        'AGUARDANDO_FINALIZACAO'
    );

CREATE UNIQUE INDEX uq_apontamento_idempotency_inicio
    ON homologacao.operacao_producao_apontamento
       (idempotency_key);

CREATE UNIQUE INDEX uq_apontamento_idempotency_termino
    ON homologacao.operacao_producao_apontamento
       (idempotency_key_termino)
    WHERE idempotency_key_termino IS NOT NULL;

CREATE INDEX ix_apontamento_ordem
    ON homologacao.operacao_producao_apontamento
       (numero_ordem);

CREATE INDEX ix_apontamento_status
    ON homologacao.operacao_producao_apontamento
       (status);

CREATE TABLE homologacao.operacao_producao_evento
(
    codigo_evento_apontamento
        bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,

    codigo_apontamento
        bigint
        REFERENCES homologacao.operacao_producao_apontamento
            (codigo_apontamento)
        ON DELETE RESTRICT,

    codigo_original
        varchar(60) NOT NULL,

    formato_codigo
        varchar(40) NOT NULL,

    ordem_producao
        varchar(40) NOT NULL DEFAULT '',

    operacao
        varchar(10) NOT NULL DEFAULT '',

    codigo_evento
        varchar(10) NOT NULL DEFAULT '',

    usuario
        varchar(80) NOT NULL DEFAULT '',

    estacao
        varchar(80) NOT NULL DEFAULT '',

    ocorrido_em
        timestamptz NOT NULL DEFAULT now(),

    status_anterior
        varchar(30) NOT NULL DEFAULT '',

    status_novo
        varchar(30) NOT NULL DEFAULT '',

    resultado
        varchar(40) NOT NULL DEFAULT '',

    mensagem
        text NOT NULL DEFAULT '',

    correlation_id
        varchar(40) NOT NULL DEFAULT ''
);

CREATE INDEX ix_evento_apontamento
    ON homologacao.operacao_producao_evento (codigo_apontamento);

CREATE INDEX ix_evento_ordem
    ON homologacao.operacao_producao_evento (ordem_producao);

CREATE INDEX ix_evento_correlation
    ON homologacao.operacao_producao_evento (correlation_id);

-- =====================================================================
-- 6. POSTCHECK DO BASE
-- Garante o contrato de composicao antes do COMMIT.
-- =====================================================================

DO $postcheck$
DECLARE
    v_table_count integer;
    v_recovery_column_count integer;
    v_consumo_index_count integer;
    v_apontamento_index_count integer;
    v_evento_index_count integer;
    v_consumo_pk_count integer;
    v_sequence_count integer;
BEGIN
    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_SCHEMA_AUSENTE';
    END IF;

    SELECT count(*)
    INTO v_table_count
    FROM (
        VALUES
            ('operacao_producao_apontamento'),
            ('operacao_producao_evento'),
            ('consumo_material_lancamento'),
            ('consumo_material_item'),
            ('consumo_material_pesagem')
    ) AS x(nome)
    WHERE to_regclass('homologacao.' || x.nome) IS NOT NULL;

    IF v_table_count <> 5 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_TABLE_COUNT: atual=% esperado=5',
            v_table_count;
    END IF;

    IF to_regclass(
        'homologacao.operacao_producao_apontamento_processo'
    ) IS NOT NULL THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_1N_NAO_DEVERIA_EXISTIR';
    END IF;

    IF to_regclass(
        'homologacao.operacao_producao_configuracao'
    ) IS NOT NULL THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_CONFIG_NAO_DEVERIA_EXISTIR';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_perfil_resultado'
    ) THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_CODIGO_PERFIL_NAO_DEVERIA_EXISTIR';
    END IF;

    SELECT count(*)
    INTO v_recovery_column_count
    FROM information_schema.columns
    WHERE table_schema = 'homologacao'
      AND table_name = 'operacao_producao_apontamento'
      AND (
          (
              column_name = 'resultado_operacional'
              AND data_type = 'character varying'
              AND character_maximum_length = 40
              AND is_nullable = 'YES'
          )
          OR
          (
              column_name = 'codigo_registro_processo'
              AND data_type = 'bigint'
              AND is_nullable = 'YES'
          )
          OR
          (
              column_name = 'concluido_operacional_em'
              AND data_type = 'timestamp with time zone'
              AND is_nullable = 'YES'
          )
          OR
          (
              column_name = 'mensagem_resultado_operacional'
              AND data_type = 'text'
              AND is_nullable = 'YES'
          )
      );

    IF v_recovery_column_count <> 4 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_RECOVERY_COLUMNS: atual=% esperado=4',
            v_recovery_column_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_evento'
          AND column_name = 'resultado'
          AND data_type = 'character varying'
          AND character_maximum_length = 40
          AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_EVENTO_RESULTADO_DEVE_SER_VARCHAR40';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid =
              'homologacao.operacao_producao_apontamento'::regclass
          AND conname = 'ck_apontamento_status'
          AND contype = 'c'
    )
    OR NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid =
              'homologacao.operacao_producao_apontamento'::regclass
          AND conname = 'ck_apontamento_resultado_operacional'
          AND contype = 'c'
    )
    OR NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid =
              'homologacao.operacao_producao_apontamento'::regclass
          AND conname = 'ck_apontamento_termino_completo'
          AND contype = 'c'
    )
    OR NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid =
              'homologacao.operacao_producao_apontamento'::regclass
          AND conname = 'ck_apontamento_ordem_cronologica'
          AND contype = 'c'
    ) THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_APONTAMENTO_CONSTRAINT_AUSENTE';
    END IF;

    SELECT count(*)
    INTO v_apontamento_index_count
    FROM pg_indexes
    WHERE schemaname = 'homologacao'
      AND tablename = 'operacao_producao_apontamento'
      AND indexname IN (
          'uq_apontamento_ativo_por_operacao',
          'uq_apontamento_idempotency_inicio',
          'uq_apontamento_idempotency_termino',
          'ix_apontamento_ordem',
          'ix_apontamento_status'
      );

    IF v_apontamento_index_count <> 5 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_APONTAMENTO_INDEX_COUNT: atual=% esperado=5',
            v_apontamento_index_count;
    END IF;

    SELECT count(*)
    INTO v_evento_index_count
    FROM pg_indexes
    WHERE schemaname = 'homologacao'
      AND tablename = 'operacao_producao_evento'
      AND indexname IN (
          'ix_evento_apontamento',
          'ix_evento_ordem',
          'ix_evento_correlation'
      );

    IF v_evento_index_count <> 3 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_EVENTO_INDEX_COUNT: atual=% esperado=3',
            v_evento_index_count;
    END IF;

    SELECT count(*)
    INTO v_consumo_index_count
    FROM pg_indexes
    WHERE schemaname = 'homologacao'
      AND indexname IN (
          'ix_consumo_lancamento_numero_ordem',
          'ix_consumo_lancamento_status',
          'ix_consumo_lancamento_doc_material_sap',
          'ix_consumo_item_lancamento',
          'ix_consumo_item_reserva',
          'ix_consumo_pesagem_item'
      );

    IF v_consumo_index_count <> 6 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_CONSUMO_INDEX_COUNT: atual=% esperado=6',
            v_consumo_index_count;
    END IF;

    SELECT count(*)
    INTO v_consumo_pk_count
    FROM pg_constraint
    WHERE contype = 'p'
      AND conrelid IN (
          'homologacao.consumo_material_lancamento'::regclass,
          'homologacao.consumo_material_item'::regclass,
          'homologacao.consumo_material_pesagem'::regclass
      );

    IF v_consumo_pk_count <> 3 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_CONSUMO_PK_COUNT: atual=% esperado=3',
            v_consumo_pk_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid =
              'homologacao.consumo_material_item'::regclass
          AND contype = 'f'
          AND confdeltype = 'c'
    )
    OR NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid =
              'homologacao.consumo_material_pesagem'::regclass
          AND contype = 'f'
          AND confdeltype = 'c'
    ) THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_CONSUMO_FK_CASCADE_AUSENTE';
    END IF;

    IF to_regprocedure(
        'homologacao.fn_definir_atualizado_em()'
    ) IS NULL THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_FUNCAO_AUSENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgrelid =
              'homologacao.consumo_material_lancamento'::regclass
          AND tgname =
              'trg_consumo_material_lancamento_atualizado_em'
          AND NOT tgisinternal
    )
    OR NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgrelid =
              'homologacao.consumo_material_item'::regclass
          AND tgname =
              'trg_consumo_material_item_atualizado_em'
          AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_TRIGGER_AUSENTE';
    END IF;

    SELECT
        (CASE WHEN pg_get_serial_sequence(
            'homologacao.operacao_producao_apontamento',
            'codigo_apontamento'
        ) IS NOT NULL THEN 1 ELSE 0 END)
        +
        (CASE WHEN pg_get_serial_sequence(
            'homologacao.operacao_producao_evento',
            'codigo_evento_apontamento'
        ) IS NOT NULL THEN 1 ELSE 0 END)
        +
        (CASE WHEN pg_get_serial_sequence(
            'homologacao.consumo_material_lancamento',
            'codigo_consumo_material_lancamento'
        ) IS NOT NULL THEN 1 ELSE 0 END)
        +
        (CASE WHEN pg_get_serial_sequence(
            'homologacao.consumo_material_item',
            'codigo_consumo_material_item'
        ) IS NOT NULL THEN 1 ELSE 0 END)
        +
        (CASE WHEN pg_get_serial_sequence(
            'homologacao.consumo_material_pesagem',
            'codigo_consumo_material_pesagem'
        ) IS NOT NULL THEN 1 ELSE 0 END)
    INTO v_sequence_count;

    IF v_sequence_count <> 5 THEN
        RAISE EXCEPTION
            '100H_BASE_POSTCHECK_SEQUENCE_IDENTITY_COUNT: atual=% esperado=5',
            v_sequence_count;
    END IF;
END
$postcheck$;

COMMIT;

\echo ============================================================
\echo 100H_EMPTYDB_BASE_APPLIED=SIM
\echo BASE_TABLE_COUNT=5
\echo ONE_TO_MANY_CREATED_BY_BASE=NAO
\echo EVENT_RESULT_BEFORE_DELTA=varchar(40)
\echo BACKFILL=0
\echo SEED=0
\echo Q_DATA_COPY=0
\echo ============================================================
