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
-- 043_entrada_produto_lote_HML_PROPOSTA_GAIA.sql
-- Projeto: FugaPET_HML
-- Banco esperado: fuga_jales_local_homologacao_q_v1_2
-- Schema: homologacao
--
-- Cria persistencia por lote para Entrada de Materia-Prima.
-- NAO insere lotes, rotas, permissoes de tela ou dados produtivos.
-- NAO altera registros historicos.
-- ============================================================================



SELECT
    current_database() = 'fuga_jales_local_homologacao_q_v1_2'
        AS ambiente_database_ok,
    to_regnamespace('homologacao') IS NOT NULL
        AS ambiente_schema_ok
\gset

\if :ambiente_database_ok
\else
    \echo 'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADO_DATABASE'
    \quit 41
\endif

\if :ambiente_schema_ok
\else
    \echo 'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADO_SCHEMA'
    \quit 42
\endif


-- ============================================================================
-- ROLE DE APLICACAO HML
-- O nome nao e presumido. Informe somente a role resolvida pelo PREFLIGHT:
--   -v role_aplicacao_q=<ROLE_APLICACAO_Q_RESOLVIDA>
-- ============================================================================
\if :{?role_aplicacao_q}
\else
    \echo 'PROPOSTA 043 HML BLOQUEADA: informe -v role_aplicacao_q=<ROLE_RESOLVIDA_PELO_PREFLIGHT>.'
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
                  ('homologacao.entrada_produto_pesagem')
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
          ) = 3
    ) AS role_aplicacao_q_valida
\gset

\if :role_aplicacao_q_valida
\else
    \echo 'PROPOSTA 043 HML BLOQUEADA: role informada nao corresponde ao padrao real validado pelo catalogo.'
    \quit 44
\endif

SELECT set_config(
    'fugapet_q.role_aplicacao',
    :'role_aplicacao_q',
    false
);

\echo 'ROLE_APLICACAO_Q_CONFIRMADA=' :role_aplicacao_q

-- Canonicaliza a visibilidade dos objetos antes de qualquer snapshot.
-- O mesmo search_path permanece ativo nos hashes anterior e posterior do
-- catalogo, evitando diferencas textuais de decompilacao sem alteracao real.
BEGIN;
SET LOCAL search_path TO homologacao, public;

SELECT set_config(
    'fugapet_p043.catalogo_search_path_antes',
    current_setting('search_path'),
    false
);

-- Snapshot somente leitura anterior para provar ausencia de alteracao de dados.
SELECT set_config(
    'fugapet_p043.lancamento_hash_antes',
    md5(COALESCE(string_agg(md5(to_jsonb(t)::text), '' ORDER BY codigo_entrada_produto_lancamento), '')),
    false
)
FROM homologacao.entrada_produto_lancamento t;

SELECT set_config(
    'fugapet_p043.item_hash_antes',
    md5(COALESCE(string_agg(md5(to_jsonb(t)::text), '' ORDER BY codigo_entrada_produto_item), '')),
    false
)
FROM homologacao.entrada_produto_item t;

SELECT set_config(
    'fugapet_p043.pesagem_hash_antes',
    md5(COALESCE(string_agg(
        md5((to_jsonb(t) - ARRAY[
            'codigo_entrada_produto_lote',
            'numero_lote_snapshot',
            'data_fabricacao_snapshot',
            'data_vencimento_snapshot'
        ]::text[])::text),
        '' ORDER BY codigo_entrada_produto_pesagem
    ), '')),
    false
)
FROM homologacao.entrada_produto_pesagem t;

SELECT set_config(
    'fugapet_p043.lancamento_qtd_antes',
    count(*)::text,
    false
)
FROM homologacao.entrada_produto_lancamento;

SELECT set_config(
    'fugapet_p043.item_qtd_antes',
    count(*)::text,
    false
)
FROM homologacao.entrada_produto_item;

SELECT set_config(
    'fugapet_p043.pesagem_qtd_antes',
    count(*)::text,
    false
)
FROM homologacao.entrada_produto_pesagem;

WITH componentes AS (
    SELECT
        'REL|' || c.relname || '|' || c.relkind::text || '|' ||
        pg_get_userbyid(c.relowner) || '|' || COALESCE(c.relacl::text, '<NULL>')
            AS componente
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'homologacao'
      AND c.relkind IN ('r', 'p')
      AND c.relname NOT IN (
          'entrada_produto_lancamento',
          'entrada_produto_item',
          'entrada_produto_pesagem',
          'entrada_produto_lote'
      )

    UNION ALL

    SELECT
        'COL|' || c.table_name || '|' || c.ordinal_position || '|' ||
        c.column_name || '|' || c.data_type || '|' ||
        COALESCE(c.character_maximum_length::text, '') || '|' ||
        COALESCE(c.numeric_precision::text, '') || '|' ||
        COALESCE(c.numeric_scale::text, '') || '|' ||
        c.is_nullable || '|' || COALESCE(c.column_default, '')
    FROM information_schema.columns c
    WHERE c.table_schema = 'homologacao'
      AND c.table_name NOT IN (
          'entrada_produto_lancamento',
          'entrada_produto_item',
          'entrada_produto_pesagem',
          'entrada_produto_lote'
      )

    UNION ALL

    SELECT
        'CON|' || rel.relname || '|' || con.conname || '|' ||
        pg_get_constraintdef(con.oid, true)
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace n ON n.oid = rel.relnamespace
    WHERE n.nspname = 'homologacao'
      AND rel.relname NOT IN (
          'entrada_produto_lancamento',
          'entrada_produto_item',
          'entrada_produto_pesagem',
          'entrada_produto_lote'
      )

    UNION ALL

    SELECT
        'IDX|' || i.tablename || '|' || i.indexname || '|' || i.indexdef
    FROM pg_indexes i
    WHERE i.schemaname = 'homologacao'
      AND i.tablename NOT IN (
          'entrada_produto_lancamento',
          'entrada_produto_item',
          'entrada_produto_pesagem',
          'entrada_produto_lote'
      )

    UNION ALL

    SELECT
        'TRG|' || rel.relname || '|' || t.tgname || '|' ||
        pg_get_triggerdef(t.oid, true)
    FROM pg_trigger t
    JOIN pg_class rel ON rel.oid = t.tgrelid
    JOIN pg_namespace n ON n.oid = rel.relnamespace
    WHERE NOT t.tgisinternal
      AND n.nspname = 'homologacao'
      AND rel.relname NOT IN (
          'entrada_produto_lancamento',
          'entrada_produto_item',
          'entrada_produto_pesagem',
          'entrada_produto_lote'
      )
)
SELECT set_config(
    'fugapet_p043.catalogo_fora_escopo_hash_antes',
    md5(COALESCE(string_agg(componente, E'\n' ORDER BY componente), '')),
    false
)
FROM componentes;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '5min';

DO $$
DECLARE
    v_tabelas_base integer;
    v_colunas_novas integer;
    v_objetos_043 integer;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_BANCO_INCORRETO banco_atual=%',
            current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_SCHEMA_INEXISTENTE';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = current_setting('fugapet_q.role_aplicacao')
          AND rolsuper = false
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_ROLE_INEXISTENTE_OU_SUPERUSUARIO';
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
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_CONNECT_OU_USAGE_AUSENTE';
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
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_TABELAS_BASE_INCOMPATIVEIS';
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
    ) THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_FK_PESAGEM_ITEM_RESTRICT_AUSENTE';
    END IF;

    RAISE NOTICE
        'PROPOSTA 043: grants ausentes nas tabelas base serao normalizados transacionalmente.';

    SELECT count(*)
      INTO v_colunas_novas
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'entrada_produto_pesagem'
       AND column_name IN (
           'codigo_entrada_produto_lote',
           'numero_lote_snapshot',
           'data_fabricacao_snapshot',
           'data_vencimento_snapshot'
       );

    SELECT
        (CASE WHEN to_regclass(
            'homologacao.entrada_produto_lote'
        ) IS NOT NULL THEN 1 ELSE 0 END)
        + v_colunas_novas
        + (
            SELECT count(*)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'homologacao'
              AND p.proname IN (
                  'fn_entrada_produto_lote_proteger_update',
                  'fn_entrada_produto_pesagem_validar_lote_snapshot',
                  'fn_entrada_produto_lote_recalcular_peso',
                  'fn_entrada_produto_pesagem_recalcular_lote'
              )
        )
        + (
            SELECT count(*)
            FROM pg_trigger t
            WHERE NOT t.tgisinternal
              AND t.tgname IN (
                  'trg_entrada_produto_lote_proteger_update',
                  'trg_entrada_produto_pesagem_validar_lote',
                  'trg_entrada_produto_pesagem_recalcular_lote'
              )
        )
    INTO v_objetos_043;

    IF v_objetos_043 <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_PACOTE_PARCIAL_OU_APLICADO objetos_encontrados=%',
            v_objetos_043;
    END IF;

    RAISE NOTICE
        'PROPOSTA 043 PRECHECK OK: base compativel e pacote nao aplicado.';
END $$;

CREATE TABLE homologacao.entrada_produto_lote (
    codigo_entrada_produto_lote bigint
        GENERATED BY DEFAULT AS IDENTITY,
    codigo_entrada_produto_item bigint NOT NULL,
    numero_lote character varying(10) NOT NULL,
    data_fabricacao date NOT NULL,
    data_vencimento date NOT NULL,
    peso_liquido_total_kg numeric(14,3) NOT NULL DEFAULT 0,
    status_lote character varying(30) NOT NULL DEFAULT 'AGUARDANDO_DADOS',
    correlation_id uuid NOT NULL,
    documento_material_sap character varying(20),
    exercicio_documento_material_sap character varying(4),
    documento_material_item character varying(10),
    batch_retornado_sap character varying(10),
    quantidade_retorno_sap numeric(14,3),
    payload_enviado_json jsonb,
    resposta_sap_json jsonb,
    erro_sanitizado text,
    criado_em timestamp with time zone NOT NULL DEFAULT clock_timestamp(),
    criado_por character varying(80) NOT NULL,
    confirmado_em timestamp with time zone,
    enviado_sap_em timestamp with time zone,
    confirmado_sap_em timestamp with time zone,
    alterado_em timestamp with time zone,
    alterado_por character varying(80),

    CONSTRAINT pk_entrada_produto_lote
        PRIMARY KEY (codigo_entrada_produto_lote),

    CONSTRAINT fk_entrada_lote_item
        FOREIGN KEY (codigo_entrada_produto_item)
        REFERENCES homologacao.entrada_produto_item (
            codigo_entrada_produto_item
        )
        ON DELETE RESTRICT,

    CONSTRAINT uq_entrada_lote_correlation_id
        UNIQUE (correlation_id),

    CONSTRAINT ck_entrada_lote_numero_nao_vazio
        CHECK (length(btrim(numero_lote)) > 0),

    CONSTRAINT ck_entrada_lote_datas
        CHECK (data_vencimento >= data_fabricacao),

    CONSTRAINT ck_entrada_lote_peso_total
        CHECK (peso_liquido_total_kg >= 0),

    CONSTRAINT ck_entrada_lote_status
        CHECK (
            status_lote IN (
                'AGUARDANDO_DADOS',
                'LOTE_CONFIRMADO',
                'PESANDO',
                'FINALIZADO_LOCAL',
                'ENVIANDO_SAP',
                'CONFIRMADO_SAP',
                'ERRO_SAP',
                'CANCELADO'
            )
        ),

    CONSTRAINT ck_entrada_lote_documento_conjunto
        CHECK (
            (
                documento_material_sap IS NULL
                AND exercicio_documento_material_sap IS NULL
                AND documento_material_item IS NULL
            )
            OR
            (
                documento_material_sap IS NOT NULL
                AND length(btrim(documento_material_sap)) > 0
                AND exercicio_documento_material_sap IS NOT NULL
                AND exercicio_documento_material_sap ~ '^[0-9]{4}$'
                AND documento_material_item IS NOT NULL
                AND length(btrim(documento_material_item)) > 0
            )
        ),

    CONSTRAINT ck_entrada_lote_confirmado_sap_documento
        CHECK (
            status_lote <> 'CONFIRMADO_SAP'
            OR (
                documento_material_sap IS NOT NULL
                AND exercicio_documento_material_sap IS NOT NULL
                AND documento_material_item IS NOT NULL
                AND confirmado_sap_em IS NOT NULL
            )
        ),

    CONSTRAINT ck_entrada_lote_erro_sap
        CHECK (
            status_lote <> 'ERRO_SAP'
            OR length(btrim(COALESCE(erro_sanitizado, ''))) > 0
        ),

    CONSTRAINT ck_entrada_lote_quantidade_retorno
        CHECK (
            quantidade_retorno_sap IS NULL
            OR quantidade_retorno_sap >= 0
        ),

    CONSTRAINT ck_entrada_lote_criado_por
        CHECK (length(btrim(criado_por)) > 0),

    CONSTRAINT ck_entrada_lote_alterado_por
        CHECK (
            alterado_por IS NULL
            OR length(btrim(alterado_por)) > 0
        ),

    CONSTRAINT ck_entrada_lote_enviado_em
        CHECK (
            status_lote NOT IN ('ENVIANDO_SAP', 'CONFIRMADO_SAP')
            OR enviado_sap_em IS NOT NULL
        ),

    CONSTRAINT ck_entrada_lote_confirmado_em
        CHECK (
            status_lote = 'AGUARDANDO_DADOS'
            OR status_lote = 'CANCELADO'
            OR confirmado_em IS NOT NULL
        ),

    CONSTRAINT ck_entrada_lote_payload_sem_credencial
        CHECK (
            payload_enviado_json IS NULL
            OR lower(payload_enviado_json::text) !~
                '"(authorization|password|senha|token|connectionstring|connection_string|clientsecret|client_secret)"[[:space:]]*:'
        )
);

CREATE UNIQUE INDEX uq_entrada_lote_ativo_item_lote_datas
    ON homologacao.entrada_produto_lote (
        codigo_entrada_produto_item,
        upper(btrim(numero_lote)),
        data_fabricacao,
        data_vencimento
    )
    WHERE status_lote <> 'CANCELADO';

CREATE UNIQUE INDEX uq_entrada_lote_documento_confirmado
    ON homologacao.entrada_produto_lote (
        documento_material_sap,
        exercicio_documento_material_sap
    )
    WHERE status_lote = 'CONFIRMADO_SAP';

CREATE INDEX ix_entrada_lote_item
    ON homologacao.entrada_produto_lote (
        codigo_entrada_produto_item
    );

CREATE INDEX ix_entrada_lote_status
    ON homologacao.entrada_produto_lote (
        status_lote,
        criado_em DESC
    );

CREATE INDEX ix_entrada_lote_numero
    ON homologacao.entrada_produto_lote (
        upper(btrim(numero_lote))
    );

CREATE INDEX ix_entrada_lote_pendente_envio
    ON homologacao.entrada_produto_lote (
        criado_em,
        codigo_entrada_produto_lote
    )
    WHERE status_lote IN ('FINALIZADO_LOCAL', 'ERRO_SAP');

CREATE INDEX ix_entrada_lote_erro_sap
    ON homologacao.entrada_produto_lote (
        alterado_em DESC NULLS LAST,
        codigo_entrada_produto_lote
    )
    WHERE status_lote = 'ERRO_SAP';

ALTER TABLE homologacao.entrada_produto_pesagem
    ADD COLUMN codigo_entrada_produto_lote bigint,
    ADD COLUMN numero_lote_snapshot character varying(10),
    ADD COLUMN data_fabricacao_snapshot date,
    ADD COLUMN data_vencimento_snapshot date;

ALTER TABLE homologacao.entrada_produto_pesagem
    ADD CONSTRAINT fk_entrada_pesagem_lote
        FOREIGN KEY (codigo_entrada_produto_lote)
        REFERENCES homologacao.entrada_produto_lote (
            codigo_entrada_produto_lote
        )
        ON DELETE RESTRICT
        NOT VALID;

ALTER TABLE homologacao.entrada_produto_pesagem
    VALIDATE CONSTRAINT fk_entrada_pesagem_lote;

ALTER TABLE homologacao.entrada_produto_pesagem
    ADD CONSTRAINT ck_entrada_pesagem_lote_snapshot_conjunto
        CHECK (
            (
                codigo_entrada_produto_lote IS NULL
                AND numero_lote_snapshot IS NULL
                AND data_fabricacao_snapshot IS NULL
                AND data_vencimento_snapshot IS NULL
            )
            OR
            (
                codigo_entrada_produto_lote IS NOT NULL
                AND numero_lote_snapshot IS NOT NULL
                AND length(btrim(numero_lote_snapshot)) > 0
                AND data_fabricacao_snapshot IS NOT NULL
                AND data_vencimento_snapshot IS NOT NULL
                AND data_vencimento_snapshot >= data_fabricacao_snapshot
            )
        )
        NOT VALID;

ALTER TABLE homologacao.entrada_produto_pesagem
    VALIDATE CONSTRAINT ck_entrada_pesagem_lote_snapshot_conjunto;

CREATE INDEX ix_entrada_pesagem_lote
    ON homologacao.entrada_produto_pesagem (
        codigo_entrada_produto_lote
    )
    WHERE codigo_entrada_produto_lote IS NOT NULL;

CREATE FUNCTION homologacao.fn_entrada_produto_lote_proteger_update()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = homologacao, pg_temp
AS $$
DECLARE
    v_pesagens_ativas bigint;
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.peso_liquido_total_kg IS DISTINCT FROM 0::numeric THEN
            RAISE EXCEPTION
                'entrada_produto_lote: peso_liquido_total_kg deve ser zero no INSERT; a pesagem e a fonte da verdade.';
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.codigo_entrada_produto_lote
       IS DISTINCT FROM OLD.codigo_entrada_produto_lote THEN
        RAISE EXCEPTION
            'entrada_produto_lote: codigo_entrada_produto_lote e imutavel.';
    END IF;

    IF NEW.correlation_id IS DISTINCT FROM OLD.correlation_id THEN
        RAISE EXCEPTION
            'entrada_produto_lote: correlation_id e imutavel.';
    END IF;

    IF NEW.criado_em IS DISTINCT FROM OLD.criado_em
       OR NEW.criado_por IS DISTINCT FROM OLD.criado_por THEN
        RAISE EXCEPTION
            'entrada_produto_lote: campos de criacao sao imutaveis.';
    END IF;

    SELECT count(*)
      INTO v_pesagens_ativas
      FROM homologacao.entrada_produto_pesagem p
     WHERE p.codigo_entrada_produto_lote =
           OLD.codigo_entrada_produto_lote
       AND p.situacao_entrada_produto_pesagem = true;

    IF v_pesagens_ativas > 0
       AND (
           NEW.codigo_entrada_produto_item
               IS DISTINCT FROM OLD.codigo_entrada_produto_item
           OR NEW.numero_lote
               IS DISTINCT FROM OLD.numero_lote
           OR NEW.data_fabricacao
               IS DISTINCT FROM OLD.data_fabricacao
           OR NEW.data_vencimento
               IS DISTINCT FROM OLD.data_vencimento
       ) THEN
        RAISE EXCEPTION
            'entrada_produto_lote: item, lote e datas nao podem ser alterados apos a primeira pesagem ativa.';
    END IF;

    IF NEW.peso_liquido_total_kg
       IS DISTINCT FROM OLD.peso_liquido_total_kg
       AND pg_trigger_depth() <= 1 THEN
        RAISE EXCEPTION
            'entrada_produto_lote: peso total e mantido exclusivamente pela trigger interna de consolidacao das pesagens.';
    END IF;

    IF OLD.status_lote IN ('ENVIANDO_SAP', 'CONFIRMADO_SAP')
       AND NEW.peso_liquido_total_kg
           IS DISTINCT FROM OLD.peso_liquido_total_kg THEN
        RAISE EXCEPTION
            'entrada_produto_lote: peso nao pode mudar apos inicio do envio SAP.';
    END IF;

    IF OLD.status_lote = 'CONFIRMADO_SAP' THEN
        IF NEW.status_lote <> 'CONFIRMADO_SAP'
           OR NEW.codigo_entrada_produto_item
               IS DISTINCT FROM OLD.codigo_entrada_produto_item
           OR NEW.numero_lote
               IS DISTINCT FROM OLD.numero_lote
           OR NEW.data_fabricacao
               IS DISTINCT FROM OLD.data_fabricacao
           OR NEW.data_vencimento
               IS DISTINCT FROM OLD.data_vencimento
           OR NEW.documento_material_sap
               IS DISTINCT FROM OLD.documento_material_sap
           OR NEW.exercicio_documento_material_sap
               IS DISTINCT FROM OLD.exercicio_documento_material_sap
           OR NEW.documento_material_item
               IS DISTINCT FROM OLD.documento_material_item
           OR NEW.batch_retornado_sap
               IS DISTINCT FROM OLD.batch_retornado_sap
           OR NEW.quantidade_retorno_sap
               IS DISTINCT FROM OLD.quantidade_retorno_sap
           OR NEW.payload_enviado_json
               IS DISTINCT FROM OLD.payload_enviado_json
           OR NEW.resposta_sap_json
               IS DISTINCT FROM OLD.resposta_sap_json
           OR NEW.confirmado_sap_em
               IS DISTINCT FROM OLD.confirmado_sap_em THEN
            RAISE EXCEPTION
                'entrada_produto_lote: lote CONFIRMADO_SAP e imutavel para reenvio e dados SAP.';
        END IF;
    END IF;

    NEW.alterado_em := clock_timestamp();

    RETURN NEW;
END $$;

CREATE FUNCTION homologacao.fn_entrada_produto_pesagem_validar_lote_snapshot()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = homologacao, pg_temp
AS $$
DECLARE
    v_lote homologacao.entrada_produto_lote%ROWTYPE;
    v_status_antigo character varying(30);
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.codigo_entrada_produto_lote IS NOT NULL THEN
            SELECT status_lote
              INTO v_status_antigo
              FROM homologacao.entrada_produto_lote
             WHERE codigo_entrada_produto_lote =
                   OLD.codigo_entrada_produto_lote;

            IF v_status_antigo IN ('ENVIANDO_SAP', 'CONFIRMADO_SAP') THEN
                RAISE EXCEPTION
                    'entrada_produto_pesagem: exclusao bloqueada para lote em envio ou confirmado SAP.';
            END IF;
        END IF;

        RETURN OLD;
    END IF;

    -- A protecao de UPDATE deve ocorrer antes do retorno do fluxo legado.
    -- Uma pesagem nunca vinculada pode permanecer com os quatro campos NULL,
    -- mas uma pesagem ja vinculada nao pode voltar ao estado legado, trocar
    -- de lote ou alterar/remover qualquer snapshot.
    IF TG_OP = 'UPDATE'
       AND OLD.codigo_entrada_produto_lote IS NOT NULL
       AND (
           NEW.codigo_entrada_produto_lote
               IS DISTINCT FROM OLD.codigo_entrada_produto_lote
           OR NEW.numero_lote_snapshot
               IS DISTINCT FROM OLD.numero_lote_snapshot
           OR NEW.data_fabricacao_snapshot
               IS DISTINCT FROM OLD.data_fabricacao_snapshot
           OR NEW.data_vencimento_snapshot
               IS DISTINCT FROM OLD.data_vencimento_snapshot
       ) THEN
        RAISE EXCEPTION
            'entrada_produto_pesagem: vinculo e snapshots de lote sao imutaveis.';
    END IF;

    IF NEW.codigo_entrada_produto_lote IS NULL THEN
        IF NEW.numero_lote_snapshot IS NOT NULL
           OR NEW.data_fabricacao_snapshot IS NOT NULL
           OR NEW.data_vencimento_snapshot IS NOT NULL THEN
            RAISE EXCEPTION
                'entrada_produto_pesagem: snapshots exigem codigo_entrada_produto_lote.';
        END IF;

        RETURN NEW;
    END IF;

    SELECT *
      INTO v_lote
      FROM homologacao.entrada_produto_lote
     WHERE codigo_entrada_produto_lote =
           NEW.codigo_entrada_produto_lote;

    IF NOT FOUND THEN
        RAISE EXCEPTION
            'entrada_produto_pesagem: lote % inexistente.',
            NEW.codigo_entrada_produto_lote;
    END IF;

    IF v_lote.codigo_entrada_produto_item
       <> NEW.codigo_entrada_produto_item THEN
        RAISE EXCEPTION
            'entrada_produto_pesagem: lote pertence a outro item de entrada.';
    END IF;

    IF NEW.numero_lote_snapshot IS NULL
       OR NEW.data_fabricacao_snapshot IS NULL
       OR NEW.data_vencimento_snapshot IS NULL THEN
        RAISE EXCEPTION
            'entrada_produto_pesagem: snapshots completos sao obrigatorios no fluxo por lote.';
    END IF;

    IF btrim(NEW.numero_lote_snapshot) <> btrim(v_lote.numero_lote)
       OR NEW.data_fabricacao_snapshot <> v_lote.data_fabricacao
       OR NEW.data_vencimento_snapshot <> v_lote.data_vencimento THEN
        RAISE EXCEPTION
            'entrada_produto_pesagem: snapshots divergem do lote referenciado.';
    END IF;

    IF v_lote.status_lote IN ('ENVIANDO_SAP', 'CONFIRMADO_SAP') THEN
        IF TG_OP = 'INSERT'
           OR (
               TG_OP = 'UPDATE'
               AND (
                   NEW.peso_liquido_kg
                       IS DISTINCT FROM OLD.peso_liquido_kg
                   OR NEW.status_pesagem
                       IS DISTINCT FROM OLD.status_pesagem
                   OR NEW.situacao_entrada_produto_pesagem
                       IS DISTINCT FROM OLD.situacao_entrada_produto_pesagem
                   OR NEW.codigo_entrada_produto_lote
                       IS DISTINCT FROM OLD.codigo_entrada_produto_lote
                   OR NEW.numero_lote_snapshot
                       IS DISTINCT FROM OLD.numero_lote_snapshot
                   OR NEW.data_fabricacao_snapshot
                       IS DISTINCT FROM OLD.data_fabricacao_snapshot
                   OR NEW.data_vencimento_snapshot
                       IS DISTINCT FROM OLD.data_vencimento_snapshot
               )
           ) THEN
            RAISE EXCEPTION
                'entrada_produto_pesagem: alteracao operacional bloqueada para lote em envio ou confirmado SAP.';
        END IF;
    END IF;

    RETURN NEW;
END $$;

CREATE FUNCTION homologacao.fn_entrada_produto_lote_recalcular_peso(
    p_codigo_entrada_produto_lote bigint
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = homologacao, pg_temp
AS $$
BEGIN
    IF p_codigo_entrada_produto_lote IS NULL THEN
        RETURN;
    END IF;

    UPDATE homologacao.entrada_produto_lote l
       SET peso_liquido_total_kg = COALESCE(
               (
                   SELECT sum(p.peso_liquido_kg)
                   FROM homologacao.entrada_produto_pesagem p
                   WHERE p.codigo_entrada_produto_lote =
                         p_codigo_entrada_produto_lote
                     AND p.situacao_entrada_produto_pesagem = true
                     AND p.status_pesagem = 'VALIDA'
               ),
               0
           )::numeric(14,3),
           alterado_em = clock_timestamp(),
           alterado_por = 'TRIGGER_PESO_LOTE'
     WHERE l.codigo_entrada_produto_lote =
           p_codigo_entrada_produto_lote;
END $$;

CREATE FUNCTION homologacao.fn_entrada_produto_pesagem_recalcular_lote()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = homologacao, pg_temp
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        PERFORM homologacao.fn_entrada_produto_lote_recalcular_peso(
            NEW.codigo_entrada_produto_lote
        );
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        PERFORM homologacao.fn_entrada_produto_lote_recalcular_peso(
            OLD.codigo_entrada_produto_lote
        );
        RETURN OLD;
    END IF;

    IF NEW.codigo_entrada_produto_lote
       IS DISTINCT FROM OLD.codigo_entrada_produto_lote THEN
        PERFORM homologacao.fn_entrada_produto_lote_recalcular_peso(
            OLD.codigo_entrada_produto_lote
        );
        PERFORM homologacao.fn_entrada_produto_lote_recalcular_peso(
            NEW.codigo_entrada_produto_lote
        );
    ELSE
        PERFORM homologacao.fn_entrada_produto_lote_recalcular_peso(
            NEW.codigo_entrada_produto_lote
        );
    END IF;

    RETURN NEW;
END $$;

REVOKE ALL
ON FUNCTION homologacao.fn_entrada_produto_lote_proteger_update()
FROM PUBLIC;

REVOKE ALL
ON FUNCTION homologacao.fn_entrada_produto_pesagem_validar_lote_snapshot()
FROM PUBLIC;

REVOKE ALL
ON FUNCTION homologacao.fn_entrada_produto_lote_recalcular_peso(bigint)
FROM PUBLIC;

REVOKE ALL
ON FUNCTION homologacao.fn_entrada_produto_pesagem_recalcular_lote()
FROM PUBLIC;

CREATE TRIGGER trg_entrada_produto_lote_proteger_update
BEFORE INSERT OR UPDATE
ON homologacao.entrada_produto_lote
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_entrada_produto_lote_proteger_update();

CREATE TRIGGER trg_entrada_produto_pesagem_validar_lote
BEFORE INSERT OR UPDATE OR DELETE
ON homologacao.entrada_produto_pesagem
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_entrada_produto_pesagem_validar_lote_snapshot();

CREATE TRIGGER trg_entrada_produto_pesagem_recalcular_lote
AFTER INSERT OR DELETE OR UPDATE OF
    codigo_entrada_produto_lote,
    peso_liquido_kg,
    status_pesagem,
    situacao_entrada_produto_pesagem
ON homologacao.entrada_produto_pesagem
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_entrada_produto_pesagem_recalcular_lote();

-- As tres tabelas base ja devem possuir a matriz HML confirmada no PREFLIGHT.
-- A PROPOSTA nao modifica ACLs anteriores; aplica a matriz somente ao novo lote.
GRANT SELECT, INSERT, UPDATE
ON TABLE homologacao.entrada_produto_lote
TO :"role_aplicacao_q";

REVOKE DELETE, TRUNCATE, REFERENCES, TRIGGER
ON TABLE homologacao.entrada_produto_lote
FROM :"role_aplicacao_q";

DO $$
DECLARE
    v_tabela text;
    v_coluna text;
    v_sequence text;
BEGIN
    FOR v_tabela, v_coluna IN
        SELECT *
        FROM (VALUES
            ('homologacao.entrada_produto_lote', 'codigo_entrada_produto_lote')
        ) AS alvo(tabela, coluna)
    LOOP
        v_sequence := pg_get_serial_sequence(v_tabela, v_coluna);

        IF v_sequence IS NULL THEN
            RAISE EXCEPTION
                'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_REPROVADA_SEQUENCE_NAO_RESOLVIDA tabela=% coluna=%',
                v_tabela,
                v_coluna;
        END IF;

        EXECUTE format(
            'GRANT USAGE, SELECT ON SEQUENCE %s TO %I',
            v_sequence::regclass,
            current_setting('fugapet_q.role_aplicacao')
        );

        EXECUTE format(
            'REVOKE UPDATE ON SEQUENCE %s FROM %I',
            v_sequence::regclass,
            current_setting('fugapet_q.role_aplicacao')
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
    v_sequences_conformes integer;
    v_owners_conformes integer;
    v_public_privilegios integer;
    v_lancamento_hash_depois text;
    v_item_hash_depois text;
    v_pesagem_hash_depois text;
    v_catalogo_fora_escopo_hash_depois text;
BEGIN
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
    FROM pg_trigger
    WHERE NOT tgisinternal
      AND tgname IN (
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

    IF v_colunas_lote <> 23
       OR v_colunas_pesagem <> 4
       OR v_constraints_lote <> 16
       OR v_constraints_pesagem <> 2
       OR v_indices <> 8
       OR v_triggers <> 3
       OR v_funcoes <> 4 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_ESTRUTURA colunas_lote=% colunas_pesagem=% constraints_lote=% constraints_pesagem=% indices=% triggers=% funcoes=%',
            v_colunas_lote,
            v_colunas_pesagem,
            v_constraints_lote,
            v_constraints_pesagem,
            v_indices,
            v_triggers,
            v_funcoes;
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
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_GRANTS_TABELAS';
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
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_GRANTS_SEQUENCES conformes=%/4',
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
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_OWNER objetos_conformes=%/8',
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
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_PUBLIC_PRIVILEGIOS total=%',
            v_public_privilegios;
    END IF;

    SELECT md5(COALESCE(string_agg(md5(to_jsonb(t)::text), '' ORDER BY codigo_entrada_produto_lancamento), ''))
      INTO v_lancamento_hash_depois
      FROM homologacao.entrada_produto_lancamento t;

    SELECT md5(COALESCE(string_agg(md5(to_jsonb(t)::text), '' ORDER BY codigo_entrada_produto_item), ''))
      INTO v_item_hash_depois
      FROM homologacao.entrada_produto_item t;

    SELECT md5(COALESCE(string_agg(
        md5((to_jsonb(t) - ARRAY[
            'codigo_entrada_produto_lote',
            'numero_lote_snapshot',
            'data_fabricacao_snapshot',
            'data_vencimento_snapshot'
        ]::text[])::text),
        '' ORDER BY codigo_entrada_produto_pesagem
    ), ''))
      INTO v_pesagem_hash_depois
      FROM homologacao.entrada_produto_pesagem t;

    IF current_setting('search_path')
       <> current_setting('fugapet_p043.catalogo_search_path_antes') THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_SEARCH_PATH_DIVERGENTE search_path_antes=% search_path_atual=%',
            current_setting('fugapet_p043.catalogo_search_path_antes'),
            current_setting('search_path');
    END IF;

    WITH componentes AS (
        SELECT
            'REL|' || c.relname || '|' || c.relkind::text || '|' ||
            pg_get_userbyid(c.relowner) || '|' || COALESCE(c.relacl::text, '<NULL>')
                AS componente
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'homologacao'
          AND c.relkind IN ('r', 'p')
          AND c.relname NOT IN (
              'entrada_produto_lancamento',
              'entrada_produto_item',
              'entrada_produto_pesagem',
              'entrada_produto_lote'
          )

        UNION ALL

        SELECT
            'COL|' || c.table_name || '|' || c.ordinal_position || '|' ||
            c.column_name || '|' || c.data_type || '|' ||
            COALESCE(c.character_maximum_length::text, '') || '|' ||
            COALESCE(c.numeric_precision::text, '') || '|' ||
            COALESCE(c.numeric_scale::text, '') || '|' ||
            c.is_nullable || '|' || COALESCE(c.column_default, '')
        FROM information_schema.columns c
        WHERE c.table_schema = 'homologacao'
          AND c.table_name NOT IN (
              'entrada_produto_lancamento',
              'entrada_produto_item',
              'entrada_produto_pesagem',
              'entrada_produto_lote'
          )

        UNION ALL

        SELECT
            'CON|' || rel.relname || '|' || con.conname || '|' ||
            pg_get_constraintdef(con.oid, true)
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace n ON n.oid = rel.relnamespace
        WHERE n.nspname = 'homologacao'
          AND rel.relname NOT IN (
              'entrada_produto_lancamento',
              'entrada_produto_item',
              'entrada_produto_pesagem',
              'entrada_produto_lote'
          )

        UNION ALL

        SELECT
            'IDX|' || i.tablename || '|' || i.indexname || '|' || i.indexdef
        FROM pg_indexes i
        WHERE i.schemaname = 'homologacao'
          AND i.tablename NOT IN (
              'entrada_produto_lancamento',
              'entrada_produto_item',
              'entrada_produto_pesagem',
              'entrada_produto_lote'
          )

        UNION ALL

        SELECT
            'TRG|' || rel.relname || '|' || t.tgname || '|' ||
            pg_get_triggerdef(t.oid, true)
        FROM pg_trigger t
        JOIN pg_class rel ON rel.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = rel.relnamespace
        WHERE NOT t.tgisinternal
          AND n.nspname = 'homologacao'
          AND rel.relname NOT IN (
              'entrada_produto_lancamento',
              'entrada_produto_item',
              'entrada_produto_pesagem',
              'entrada_produto_lote'
          )
    )
    SELECT md5(COALESCE(string_agg(componente, E'\n' ORDER BY componente), ''))
      INTO v_catalogo_fora_escopo_hash_depois
      FROM componentes;

    IF v_catalogo_fora_escopo_hash_depois
       <> current_setting('fugapet_p043.catalogo_fora_escopo_hash_antes') THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_CATALOGO_FORA_ESCOPO_DIVERGENTE hash_antes=% hash_depois=%',
            current_setting('fugapet_p043.catalogo_fora_escopo_hash_antes'),
            v_catalogo_fora_escopo_hash_depois;
    END IF;

    IF v_lancamento_hash_depois <> current_setting('fugapet_p043.lancamento_hash_antes')
       OR v_item_hash_depois <> current_setting('fugapet_p043.item_hash_antes')
       OR v_pesagem_hash_depois <> current_setting('fugapet_p043.pesagem_hash_antes')
       OR (SELECT count(*) FROM homologacao.entrada_produto_lancamento)
          <> current_setting('fugapet_p043.lancamento_qtd_antes')::bigint
       OR (SELECT count(*) FROM homologacao.entrada_produto_item)
          <> current_setting('fugapet_p043.item_qtd_antes')::bigint
       OR (SELECT count(*) FROM homologacao.entrada_produto_pesagem)
          <> current_setting('fugapet_p043.pesagem_qtd_antes')::bigint THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_DADOS_HISTORICOS_ALTERADOS';
    END IF;

    IF (
        SELECT count(*)
        FROM homologacao.entrada_produto_lote
    ) <> 0 THEN
        RAISE EXCEPTION
            'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_POSTCHECK_REPROVADO_LOTE_INSERIDO_INDEVIDAMENTE';
    END IF;

    RAISE NOTICE 'CLASSIFICACAO_OFICIAL=PROPOSTA_043_HML_APLICADA_COM_SUCESSO';
    RAISE NOTICE 'PROPOSTA 043 OK: estrutura e matriz de grants normalizadas sem inserir dados ou alterar historico.';
END $$;

COMMIT;

\echo 'OK - APLICACAO 043 HML concluida'
