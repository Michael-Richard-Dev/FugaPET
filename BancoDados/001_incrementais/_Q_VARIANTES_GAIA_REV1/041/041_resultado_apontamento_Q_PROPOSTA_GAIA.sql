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
-- 041_resultado_apontamento_DEV_PROPOSTA_GAIA.sql
-- Projeto: FugaPET_Dev | Schema: homologacao
-- Escopo:
--   1. ampliar ck_operacao_config_tipo_processo;
--   2. criar definicoes de resultado;
--   3. criar cabecalho e itens do resultado;
--   4. aplicar grants minimos para fugapet_q_app.
--
-- NAO INSERE ROTAS, DEFINICOES OU RESULTADOS.
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- ============================================================================
SET search_path TO homologacao, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_tabelas_039 integer;
    v_indices_039 integer;
    v_tabelas_041 integer;
    v_rotas_0050 integer;
    v_rotas_0060 integer;
    v_rotas_conflitantes integer;
    v_rotas_especificas integer;
    v_pares_ambiguos integer;
    v_constraint_oid oid;
    v_constraint_def text;
    v_tipos_atuais text[];
    v_tipos_antigos constant text[] := ARRAY[
        'CONSUMO_MATERIA_PRIMA',
        'CONSUMO_QUIMICOS',
        'PRODUTO_ACABADO',
        'SEMI_ACABADO',
        'SEM_DESTINO_CONFIGURADO'
    ]::text[];
    v_tipos_041 constant text[] := ARRAY[
        'CONSUMO_MATERIA_PRIMA',
        'CONSUMO_QUIMICOS',
        'CONSUMO_SEMI_ACABADO',
        'PRODUTO_ACABADO',
        'RESULTADO_APONTAMENTO',
        'SEMI_ACABADO',
        'SEM_DESTINO_CONFIGURADO'
    ]::text[];
    v_incompativeis text;
    v_valor bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION 'PROPOSTA 041: banco incorreto: %. Esperado fuga_jales_local_homologacao_q_v1_2.', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'PROPOSTA 041: schema homologacao inexistente.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_app' AND rolsuper = false) THEN
        RAISE EXCEPTION 'PROPOSTA 041: papel fugapet_q_app inexistente ou superusuario.';
    END IF;

    IF NOT has_database_privilege('fugapet_q_app', current_database(), 'CONNECT')
       OR NOT has_schema_privilege('fugapet_q_app', 'homologacao', 'USAGE') THEN
        RAISE EXCEPTION 'PROPOSTA 041: fugapet_q_app sem CONNECT no banco ou USAGE no schema.';
    END IF;

    SELECT count(*) INTO v_tabelas_039
    FROM information_schema.tables
    WHERE table_schema = 'homologacao'
      AND table_name IN (
          'operacao_producao_configuracao',
          'operacao_producao_apontamento',
          'operacao_producao_evento'
      );

    SELECT count(*) INTO v_indices_039
    FROM pg_indexes
    WHERE schemaname = 'homologacao'
      AND tablename IN (
          'operacao_producao_configuracao',
          'operacao_producao_apontamento',
          'operacao_producao_evento'
      );

    IF v_tabelas_039 <> 3 OR v_indices_039 <> 13 THEN
        RAISE EXCEPTION 'PROPOSTA 041: DEPENDENCIA_039_INCOMPATIVEL. tabelas=%, indices=%.', v_tabelas_039, v_indices_039;
    END IF;

    WITH cfg AS (
        SELECT
            codigo_configuracao,
            COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
            tipo_processo,
            tela_destino,
            exige_operacao_anterior,
            COALESCE(centro, '') AS centro,
            COALESCE(tipo_ordem, '') AS tipo_ordem,
            COALESCE(sequencia_sap, '') AS sequencia_sap,
            COALESCE(suboperacao_sap, '') AS suboperacao_sap,
            COALESCE(centro_trabalho, '') AS centro_trabalho,
            (
                CASE WHEN COALESCE(centro, '') <> '' THEN 1 ELSE 0 END +
                CASE WHEN COALESCE(tipo_ordem, '') <> '' THEN 1 ELSE 0 END +
                CASE WHEN COALESCE(sequencia_sap, '') <> '' THEN 1 ELSE 0 END +
                CASE WHEN COALESCE(suboperacao_sap, '') <> '' THEN 1 ELSE 0 END +
                CASE WHEN COALESCE(centro_trabalho, '') <> '' THEN 1 ELSE 0 END
            ) AS especificidade
        FROM homologacao.operacao_producao_configuracao
        WHERE ativo = true
    ), globais AS (
        SELECT * FROM cfg
        WHERE operacao_normalizada IN ('50','60')
          AND centro = '' AND tipo_ordem = '' AND sequencia_sap = ''
          AND suboperacao_sap = '' AND centro_trabalho = ''
    ), especificas AS (
        SELECT * FROM cfg
        WHERE operacao_normalizada IN ('50','60')
          AND NOT (
              centro = '' AND tipo_ordem = '' AND sequencia_sap = ''
              AND suboperacao_sap = '' AND centro_trabalho = ''
          )
    ), pares AS (
        SELECT 1
        FROM cfg a
        JOIN cfg b
          ON a.codigo_configuracao < b.codigo_configuracao
         AND a.operacao_normalizada = b.operacao_normalizada
         AND a.especificidade = b.especificidade
         AND (a.centro = b.centro OR a.centro = '' OR b.centro = '')
         AND (a.tipo_ordem = b.tipo_ordem OR a.tipo_ordem = '' OR b.tipo_ordem = '')
         AND (a.sequencia_sap = b.sequencia_sap OR a.sequencia_sap = '' OR b.sequencia_sap = '')
         AND (a.suboperacao_sap = b.suboperacao_sap OR a.suboperacao_sap = '' OR b.suboperacao_sap = '')
         AND (a.centro_trabalho = b.centro_trabalho OR a.centro_trabalho = '' OR b.centro_trabalho = '')
        WHERE a.operacao_normalizada IN ('50','60')
    )
    SELECT
        count(*) FILTER (
            WHERE operacao_normalizada = '50'
              AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
              AND tela_destino = 'ProcessoConsumoMaterialForm'
              AND exige_operacao_anterior IS FALSE
        ),
        count(*) FILTER (
            WHERE operacao_normalizada = '60'
              AND tipo_processo = 'CONSUMO_QUIMICOS'
              AND tela_destino = 'ProcessoConsumoMaterialForm'
              AND exige_operacao_anterior IS TRUE
        ),
        count(*) FILTER (
            WHERE NOT (
                operacao_normalizada = '50'
                AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
                AND tela_destino = 'ProcessoConsumoMaterialForm'
                AND exige_operacao_anterior IS FALSE
            )
            AND NOT (
                operacao_normalizada = '60'
                AND tipo_processo = 'CONSUMO_QUIMICOS'
                AND tela_destino = 'ProcessoConsumoMaterialForm'
                AND exige_operacao_anterior IS TRUE
            )
        ),
        (SELECT count(*) FROM especificas),
        (SELECT count(*) FROM pares)
    INTO v_rotas_0050, v_rotas_0060, v_rotas_conflitantes, v_rotas_especificas, v_pares_ambiguos
    FROM globais;

    IF v_rotas_0050 <> 1 OR v_rotas_0060 <> 1
       OR v_rotas_conflitantes <> 0 OR v_rotas_especificas <> 0
       OR v_pares_ambiguos <> 0 THEN
        RAISE EXCEPTION
            'PROPOSTA 041: DEPENDENCIA_040_INCOMPATIVEL. 0050=%, 0060=%, conflitos=%, especificas=%, ambiguos=%.',
            v_rotas_0050, v_rotas_0060, v_rotas_conflitantes, v_rotas_especificas, v_pares_ambiguos;
    END IF;

    SELECT count(*) INTO v_tabelas_041
    FROM information_schema.tables
    WHERE table_schema = 'homologacao'
      AND table_name IN (
          'operacao_resultado_definicao',
          'operacao_resultado_registro',
          'operacao_resultado_registro_item'
      );

    IF v_tabelas_041 <> 0 THEN
        IF v_tabelas_041 = 3 THEN
            RAISE EXCEPTION 'PROPOSTA 041: PACOTE_COMPLETAMENTE_APLICADO ou estrutura preexistente. Nao reaplicar a PROPOSTA; execute o PREFLIGHT para diagnostico.';
        END IF;
        RAISE EXCEPTION 'PROPOSTA 041: PACOTE_PARCIAL_INCOMPATIVEL. Existem % de 3 tabelas.', v_tabelas_041;
    END IF;

    SELECT c.oid, pg_get_constraintdef(c.oid, true)
      INTO v_constraint_oid, v_constraint_def
      FROM pg_constraint c
     WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
       AND c.conname = 'ck_operacao_config_tipo_processo'
       AND c.contype = 'c'
       AND c.convalidated = true;

    IF v_constraint_oid IS NULL THEN
        RAISE EXCEPTION 'PROPOSTA 041: constraint ck_operacao_config_tipo_processo inexistente.';
    END IF;

    SELECT array_agg(x.match[1] ORDER BY x.match[1])
      INTO v_tipos_atuais
      FROM regexp_matches(v_constraint_def, '''([^'']+)''', 'g') AS x(match);

    IF v_tipos_atuais IS DISTINCT FROM v_tipos_antigos THEN
        RAISE EXCEPTION 'PROPOSTA 041: constraint atual diverge do estado anterior esperado. Definicao: %.', v_constraint_def;
    END IF;

    SELECT string_agg(DISTINCT tipo_processo, ', ' ORDER BY tipo_processo)
      INTO v_incompativeis
      FROM homologacao.operacao_producao_configuracao
     WHERE tipo_processo IS NULL
        OR btrim(tipo_processo) = ''
        OR tipo_processo <> ALL(v_tipos_041);

    IF v_incompativeis IS NOT NULL THEN
        RAISE EXCEPTION 'PROPOSTA 041: existem tipos atuais incompatíveis com a nova constraint: %.', v_incompativeis;
    END IF;

    SELECT count(*) INTO v_valor FROM homologacao.operacao_producao_configuracao;
    PERFORM set_config('fugapet_i041.config_antes', v_valor::text, false);
    SELECT count(*) INTO v_valor FROM homologacao.operacao_producao_apontamento;
    PERFORM set_config('fugapet_i041.apontamentos_antes', v_valor::text, false);
    SELECT count(*) INTO v_valor FROM homologacao.operacao_producao_evento;
    PERFORM set_config('fugapet_i041.eventos_antes', v_valor::text, false);
END $$;

-- Amplia exatamente a constraint de tipo_processo, preservando os tipos anteriores.
ALTER TABLE homologacao.operacao_producao_configuracao
    DROP CONSTRAINT ck_operacao_config_tipo_processo;

ALTER TABLE homologacao.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_tipo_processo
    CHECK (
        btrim(tipo_processo) <> ''
        AND tipo_processo IN (
            'CONSUMO_MATERIA_PRIMA',
            'CONSUMO_QUIMICOS',
            'CONSUMO_SEMI_ACABADO',
            'RESULTADO_APONTAMENTO',
            'SEMI_ACABADO',
            'PRODUTO_ACABADO',
            'SEM_DESTINO_CONFIGURADO'
        )
    );

-- Definicoes dos itens esperados para uma configuracao de operacao.
CREATE TABLE homologacao.operacao_resultado_definicao
(
    codigo_definicao      bigint GENERATED BY DEFAULT AS IDENTITY,
    codigo_configuracao   bigint          NOT NULL,
    codigo_item           varchar(50)     NOT NULL,
    tipo                  varchar(40)     NOT NULL,
    medida                varchar(30)     NOT NULL,
    referencia            numeric(18,3)   NOT NULL,
    ordem_exibicao        integer         NOT NULL,
    obrigatorio           boolean         NOT NULL DEFAULT true,
    ativo                 boolean         NOT NULL DEFAULT true,
    criado_em             timestamptz     NOT NULL DEFAULT now(),
    criado_por            varchar(80)     NOT NULL,
    alterado_em           timestamptz,
    alterado_por          varchar(80),

    CONSTRAINT pk_operacao_resultado_definicao
        PRIMARY KEY (codigo_definicao),
    CONSTRAINT fk_resultado_definicao_configuracao
        FOREIGN KEY (codigo_configuracao)
        REFERENCES homologacao.operacao_producao_configuracao (codigo_configuracao)
        ON DELETE RESTRICT,
    CONSTRAINT ck_resultado_definicao_codigo_item_nao_vazio
        CHECK (length(btrim(codigo_item)) > 0),
    CONSTRAINT ck_resultado_definicao_tipo_nao_vazio
        CHECK (length(btrim(tipo)) > 0),
    CONSTRAINT ck_resultado_definicao_medida_nao_vazia
        CHECK (length(btrim(medida)) > 0),
    CONSTRAINT ck_resultado_definicao_referencia_nao_negativa
        CHECK (referencia >= 0),
    CONSTRAINT ck_resultado_definicao_ordem_positiva
        CHECK (ordem_exibicao > 0)
);

CREATE UNIQUE INDEX uq_resultado_definicao_ativa_item
    ON homologacao.operacao_resultado_definicao
       (codigo_configuracao, codigo_item)
    WHERE ativo = true;

CREATE UNIQUE INDEX uq_resultado_definicao_ativa_ordem
    ON homologacao.operacao_resultado_definicao
       (codigo_configuracao, ordem_exibicao)
    WHERE ativo = true;

CREATE INDEX ix_resultado_definicao_config_ativo
    ON homologacao.operacao_resultado_definicao
       (codigo_configuracao, ativo);

CREATE INDEX ix_resultado_definicao_tipo_medida
    ON homologacao.operacao_resultado_definicao
       (tipo, medida)
    WHERE ativo = true;

-- Cabecalho append-only do resultado registrado para um apontamento.
CREATE TABLE homologacao.operacao_resultado_registro
(
    codigo_resultado      bigint GENERATED BY DEFAULT AS IDENTITY,
    codigo_apontamento    bigint          NOT NULL,
    usuario               varchar(80)     NOT NULL,
    estacao               varchar(80)     NOT NULL DEFAULT '',
    registrado_em         timestamptz     NOT NULL DEFAULT now(),
    mensagem_resumo       text            NOT NULL DEFAULT '',
    criado_em             timestamptz     NOT NULL DEFAULT now(),

    CONSTRAINT pk_operacao_resultado_registro
        PRIMARY KEY (codigo_resultado),
    CONSTRAINT fk_resultado_registro_apontamento
        FOREIGN KEY (codigo_apontamento)
        REFERENCES homologacao.operacao_producao_apontamento (codigo_apontamento)
        ON DELETE RESTRICT,
    CONSTRAINT uq_operacao_resultado_registro_apontamento
        UNIQUE (codigo_apontamento)
);

-- Itens com snapshot integral da definicao utilizada no momento da gravacao.
CREATE TABLE homologacao.operacao_resultado_registro_item
(
    codigo_resultado_item bigint GENERATED BY DEFAULT AS IDENTITY,
    codigo_resultado      bigint          NOT NULL,
    codigo_definicao      bigint          NOT NULL,
    codigo_item           varchar(50)     NOT NULL,
    tipo                  varchar(40)     NOT NULL,
    medida                varchar(30)     NOT NULL,
    referencia            numeric(18,3)   NOT NULL,
    resultado             numeric(18,3),
    ordem_exibicao        integer         NOT NULL,
    obrigatorio           boolean         NOT NULL,
    criado_em             timestamptz     NOT NULL DEFAULT now(),

    CONSTRAINT pk_operacao_resultado_registro_item
        PRIMARY KEY (codigo_resultado_item),
    CONSTRAINT fk_resultado_item_resultado
        FOREIGN KEY (codigo_resultado)
        REFERENCES homologacao.operacao_resultado_registro (codigo_resultado)
        ON DELETE RESTRICT,
    CONSTRAINT fk_resultado_item_definicao
        FOREIGN KEY (codigo_definicao)
        REFERENCES homologacao.operacao_resultado_definicao (codigo_definicao)
        ON DELETE RESTRICT,
    CONSTRAINT uq_operacao_resultado_item_codigo
        UNIQUE (codigo_resultado, codigo_item),
    CONSTRAINT ck_resultado_item_referencia_nao_negativa
        CHECK (referencia >= 0),
    CONSTRAINT ck_resultado_item_resultado_nao_negativo
        CHECK (resultado IS NULL OR resultado >= 0),
    CONSTRAINT ck_resultado_item_ordem_positiva
        CHECK (ordem_exibicao > 0),
    CONSTRAINT ck_resultado_item_obrigatorio_preenchido
        CHECK (obrigatorio IS FALSE OR resultado IS NOT NULL)
);

CREATE INDEX ix_resultado_registro_item_resultado
    ON homologacao.operacao_resultado_registro_item (codigo_resultado);

-- Privilegios minimos. Remove privilegios publicos e escrita nao autorizada.
REVOKE ALL ON homologacao.operacao_resultado_definicao FROM PUBLIC;
REVOKE ALL ON homologacao.operacao_resultado_registro FROM PUBLIC;
REVOKE ALL ON homologacao.operacao_resultado_registro_item FROM PUBLIC;

REVOKE INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
    ON homologacao.operacao_resultado_definicao FROM fugapet_q_app;
REVOKE UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
    ON homologacao.operacao_resultado_registro FROM fugapet_q_app;
REVOKE UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
    ON homologacao.operacao_resultado_registro_item FROM fugapet_q_app;

GRANT SELECT
    ON homologacao.operacao_resultado_definicao TO fugapet_q_app;
GRANT SELECT, INSERT
    ON homologacao.operacao_resultado_registro TO fugapet_q_app;
GRANT SELECT, INSERT
    ON homologacao.operacao_resultado_registro_item TO fugapet_q_app;

DO $$
DECLARE
    v_seq text;
    v_constraint_def text;
    v_tipos_atuais text[];
    v_tipos_041 constant text[] := ARRAY[
        'CONSUMO_MATERIA_PRIMA',
        'CONSUMO_QUIMICOS',
        'CONSUMO_SEMI_ACABADO',
        'PRODUTO_ACABADO',
        'RESULTADO_APONTAMENTO',
        'SEMI_ACABADO',
        'SEM_DESTINO_CONFIGURADO'
    ]::text[];
    v_qtd bigint;
    r record;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'codigo_definicao'),
            ('operacao_resultado_registro', 'codigo_resultado'),
            ('operacao_resultado_registro_item', 'codigo_resultado_item')
        ) AS e(tabela, coluna)
    LOOP
        v_seq := pg_get_serial_sequence('homologacao.' || r.tabela, r.coluna);
        IF v_seq IS NULL THEN
            RAISE EXCEPTION 'PROPOSTA 041: sequence identity nao localizada para %.%.', r.tabela, r.coluna;
        END IF;
        EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', v_seq);
        EXECUTE format('REVOKE UPDATE ON SEQUENCE %s FROM fugapet_q_app', v_seq);
        EXECUTE format('GRANT USAGE, SELECT ON SEQUENCE %s TO fugapet_q_app', v_seq);
    END LOOP;

    SELECT pg_get_constraintdef(c.oid, true)
      INTO v_constraint_def
      FROM pg_constraint c
     WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
       AND c.conname = 'ck_operacao_config_tipo_processo'
       AND c.contype = 'c'
       AND c.convalidated = true;

    SELECT array_agg(x.match[1] ORDER BY x.match[1])
      INTO v_tipos_atuais
      FROM regexp_matches(v_constraint_def, '''([^'']+)''', 'g') AS x(match);

    IF v_constraint_def IS NULL
       OR position('tipo_processo' in lower(v_constraint_def)) = 0
       OR position('btrim' in lower(v_constraint_def)) = 0
       OR v_tipos_atuais IS DISTINCT FROM v_tipos_041 THEN
        RAISE EXCEPTION 'PROPOSTA 041: constraint final de tipo_processo divergente: %.', v_constraint_def;
    END IF;

    SELECT count(*) INTO v_qtd FROM homologacao.operacao_resultado_definicao;
    IF v_qtd <> 0 THEN RAISE EXCEPTION 'PROPOSTA 041: definicoes foram inseridas indevidamente (%).', v_qtd; END IF;
    SELECT count(*) INTO v_qtd FROM homologacao.operacao_resultado_registro;
    IF v_qtd <> 0 THEN RAISE EXCEPTION 'PROPOSTA 041: resultados foram inseridos indevidamente (%).', v_qtd; END IF;
    SELECT count(*) INTO v_qtd FROM homologacao.operacao_resultado_registro_item;
    IF v_qtd <> 0 THEN RAISE EXCEPTION 'PROPOSTA 041: itens foram inseridos indevidamente (%).', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM homologacao.operacao_producao_configuracao;
    IF v_qtd <> current_setting('fugapet_i041.config_antes')::bigint THEN
        RAISE EXCEPTION 'PROPOSTA 041: configuracoes 039/040 foram alteradas indevidamente.';
    END IF;
    SELECT count(*) INTO v_qtd FROM homologacao.operacao_producao_apontamento;
    IF v_qtd <> current_setting('fugapet_i041.apontamentos_antes')::bigint THEN
        RAISE EXCEPTION 'PROPOSTA 041: apontamentos 039 foram alterados indevidamente.';
    END IF;
    SELECT count(*) INTO v_qtd FROM homologacao.operacao_producao_evento;
    IF v_qtd <> current_setting('fugapet_i041.eventos_antes')::bigint THEN
        RAISE EXCEPTION 'PROPOSTA 041: eventos 039 foram alterados indevidamente.';
    END IF;

    IF NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'SELECT')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'INSERT')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'UPDATE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'DELETE')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'SELECT')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'INSERT')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'UPDATE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'DELETE')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'SELECT')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'INSERT')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'UPDATE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'TRUNCATE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'REFERENCES')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_definicao', 'TRIGGER')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'TRUNCATE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'REFERENCES')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro', 'TRIGGER')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'DELETE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'TRUNCATE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'REFERENCES')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_resultado_registro_item', 'TRIGGER') THEN
        RAISE EXCEPTION 'PROPOSTA 041: matriz final de grants divergente.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='homologacao'
          AND c.relname IN ('operacao_resultado_definicao','operacao_resultado_registro','operacao_resultado_registro_item')
          AND pg_get_userbyid(c.relowner)='fugapet_q_app'
    ) THEN
        RAISE EXCEPTION 'PROPOSTA 041: fugapet_q_app nao pode ser owner das tabelas 041.';
    END IF;

    RAISE NOTICE 'PROPOSTA 041 OK: constraint ampliada, tres tabelas criadas, grants minimos aplicados e nenhum seed inserido.';
END $$;

COMMIT;
\echo 'OK - APLICACAO 041 DEV concluida'
