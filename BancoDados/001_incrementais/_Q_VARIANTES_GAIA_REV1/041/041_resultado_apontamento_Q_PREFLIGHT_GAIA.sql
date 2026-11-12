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
-- 041_resultado_apontamento_DEV_PREFLIGHT_GAIA.sql
-- Projeto: FugaPET_Dev
-- Banco esperado: fuga_jales_local_homologacao_q_v1_2
-- Schema: homologacao
-- Escopo: persistencia das definicoes e resultados do apontamento.
--
-- SOMENTE LEITURA. NAO EXECUTA DDL/DML PERSISTENTE.
-- Classificacoes:
--   PACOTE_NAO_APLICADO
--   PACOTE_COMPLETAMENTE_APLICADO
--   PACOTE_PARCIAL_INCOMPATIVEL
-- ============================================================================
SET search_path TO homologacao, public;

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_tabelas_039 integer;
    v_indices_039 integer;
    v_permissoes_039 integer;
    v_vinculos_039 integer;
    v_rotas_0050 integer;
    v_rotas_0060 integer;
    v_rotas_conflitantes integer;
    v_rotas_especificas integer;
    v_pares_ambiguos integer;
    v_tabelas_041 integer;
    v_homonimos integer;
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
    v_tipo_atual text;
    v_notnull_atual boolean;
    v_identity_atual text;
    v_colunas_esperadas integer;
    v_colunas_atuais integer;
    v_default text;
    v_def text;
    v_seq text;
    v_qtd_definicoes bigint;
    v_qtd_resultados bigint;
    v_qtd_itens bigint;
    r record;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION 'PREFLIGHT 041: banco incorreto: %. Esperado fuga_jales_local_homologacao_q_v1_2.', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 041: schema homologacao inexistente.';
    END IF;

    IF current_setting('server_version_num')::integer < 100000 THEN
        RAISE EXCEPTION 'PREFLIGHT 041: PostgreSQL % nao suporta identity conforme o pacote.', version();
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_app' AND rolsuper = false
    ) THEN
        RAISE EXCEPTION 'PREFLIGHT 041: papel fugapet_q_app inexistente ou superusuario.';
    END IF;

    IF NOT has_database_privilege('fugapet_q_app', current_database(), 'CONNECT') THEN
        RAISE EXCEPTION 'PREFLIGHT 041: fugapet_q_app sem CONNECT no banco DEV.';
    END IF;

    IF NOT has_schema_privilege('fugapet_q_app', 'homologacao', 'USAGE') THEN
        RAISE EXCEPTION 'PREFLIGHT 041: fugapet_q_app sem USAGE no schema homologacao.';
    END IF;

    -- Dependencia 039: objetos, indices, permissoes e grants essenciais.
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

    SELECT count(*) INTO v_permissoes_039
    FROM homologacao.permissao
    WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
      AND rotina_permissao = 'CONTROLE_APONTAMENTOS'
      AND acao_permissao IN ('VISUALIZAR', 'INICIAR', 'FINALIZAR')
      AND situacao_permissao = true;

    SELECT count(*) INTO v_vinculos_039
    FROM homologacao.perfil_acesso pa
    JOIN homologacao.perfil_permissao pp
      ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
    JOIN homologacao.permissao p
      ON p.codigo_permissao = pp.codigo_permissao
    WHERE pa.nome_perfil_acesso = 'Administrador'
      AND p.modulo_permissao = 'PROCESSO_PRODUCAO'
      AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS'
      AND p.acao_permissao IN ('VISUALIZAR', 'INICIAR', 'FINALIZAR')
      AND p.situacao_permissao = true
      AND pp.situacao_perfil_permissao = true;

    IF v_tabelas_039 <> 3 OR v_indices_039 <> 13
       OR v_permissoes_039 <> 3 OR v_vinculos_039 <> 3 THEN
        RAISE EXCEPTION
            'PREFLIGHT 041: DEPENDENCIA_039_INCOMPATIVEL. tabelas=%, indices=%, permissoes=%, vinculos=%.',
            v_tabelas_039, v_indices_039, v_permissoes_039, v_vinculos_039;
    END IF;

    IF NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_configuracao', 'SELECT')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_apontamento', 'SELECT')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_apontamento', 'INSERT')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_apontamento', 'UPDATE')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_evento', 'SELECT')
       OR NOT has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_evento', 'INSERT') THEN
        RAISE EXCEPTION 'PREFLIGHT 041: DEPENDENCIA_039_INCOMPATIVEL. Grants essenciais ausentes.';
    END IF;

    IF has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_configuracao', 'DELETE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_apontamento', 'DELETE')
       OR has_table_privilege('fugapet_q_app', 'homologacao.operacao_producao_evento', 'DELETE') THEN
        RAISE EXCEPTION 'PREFLIGHT 041: DEPENDENCIA_039_INCOMPATIVEL. DELETE concedido indevidamente.';
    END IF;

    -- Dependencia 040: rotas 0050/0060 corretas, sem conflito, especificidade ou empate.
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
        WHERE operacao_normalizada IN ('50', '60')
          AND centro = '' AND tipo_ordem = '' AND sequencia_sap = ''
          AND suboperacao_sap = '' AND centro_trabalho = ''
    ), especificas AS (
        SELECT * FROM cfg
        WHERE operacao_normalizada IN ('50', '60')
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
        WHERE a.operacao_normalizada IN ('50', '60')
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
            'PREFLIGHT 041: DEPENDENCIA_040_INCOMPATIVEL. 0050=%, 0060=%, conflitos=%, especificas=%, ambiguos=%.',
            v_rotas_0050, v_rotas_0060, v_rotas_conflitantes, v_rotas_especificas, v_pares_ambiguos;
    END IF;

    -- Objetos homonimos em schemas nao sistemicos sao bloqueados.
    SELECT count(*) INTO v_homonimos
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE c.relname IN (
        'operacao_resultado_definicao',
        'operacao_resultado_registro',
        'operacao_resultado_registro_item'
    )
      AND n.nspname <> 'homologacao'
      AND n.nspname NOT IN ('pg_catalog', 'information_schema')
      AND n.nspname NOT LIKE 'pg_toast%';

    IF v_homonimos <> 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Existem % objetos homonimos fora do schema homologacao.', v_homonimos;
    END IF;

    SELECT c.oid, pg_get_constraintdef(c.oid, true)
      INTO v_constraint_oid, v_constraint_def
      FROM pg_constraint c
     WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
       AND c.conname = 'ck_operacao_config_tipo_processo'
       AND c.contype = 'c'
       AND c.convalidated = true;

    IF v_constraint_oid IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Constraint ck_operacao_config_tipo_processo inexistente.';
    END IF;

    SELECT array_agg(x.match[1] ORDER BY x.match[1])
      INTO v_tipos_atuais
      FROM regexp_matches(v_constraint_def, '''([^'']+)''', 'g') AS x(match);

    IF position('tipo_processo' in lower(v_constraint_def)) = 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Constraint de tipos nao referencia tipo_processo.';
    END IF;

    SELECT string_agg(DISTINCT tipo_processo, ', ' ORDER BY tipo_processo)
      INTO v_incompativeis
      FROM homologacao.operacao_producao_configuracao
     WHERE tipo_processo IS NULL
        OR btrim(tipo_processo) = ''
        OR tipo_processo <> ALL(v_tipos_041);

    IF v_incompativeis IS NOT NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 041: existem tipos de processo incompatíveis com a nova constraint: %.', v_incompativeis;
    END IF;

    SELECT count(*) INTO v_tabelas_041
    FROM information_schema.tables
    WHERE table_schema = 'homologacao'
      AND table_name IN (
          'operacao_resultado_definicao',
          'operacao_resultado_registro',
          'operacao_resultado_registro_item'
      );

    IF v_tabelas_041 BETWEEN 1 AND 2 THEN
        RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Apenas % de 3 tabelas do 041 existem.', v_tabelas_041;
    END IF;

    IF v_tabelas_041 = 0 THEN
        IF v_tipos_atuais IS DISTINCT FROM v_tipos_antigos THEN
            RAISE EXCEPTION
                'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Tabelas 041 ausentes, mas constraint atual nao corresponde ao estado anterior esperado. Definicao: %.',
                v_constraint_def;
        END IF;

        RAISE NOTICE 'PREFLIGHT OK: PACOTE_NAO_APLICADO - dependencias 039/040 integras, constraint anterior compativel e nenhuma tabela 041 existente.';
        RETURN;
    END IF;

    IF v_tipos_atuais IS DISTINCT FROM v_tipos_041 THEN
        RAISE EXCEPTION
            'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Tabelas 041 existem, mas a constraint de tipos nao corresponde ao 041. Definicao: %.',
            v_constraint_def;
    END IF;

    -- Validacao exata das colunas das tres tabelas.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'codigo_definicao',      'bigint',                    true,  'd'),
            ('operacao_resultado_definicao', 'codigo_configuracao',   'bigint',                    true,  ''),
            ('operacao_resultado_definicao', 'codigo_item',           'character varying(50)',     true,  ''),
            ('operacao_resultado_definicao', 'tipo',                  'character varying(40)',     true,  ''),
            ('operacao_resultado_definicao', 'medida',                'character varying(30)',     true,  ''),
            ('operacao_resultado_definicao', 'referencia',            'numeric(18,3)',              true,  ''),
            ('operacao_resultado_definicao', 'ordem_exibicao',        'integer',                    true,  ''),
            ('operacao_resultado_definicao', 'obrigatorio',           'boolean',                    true,  ''),
            ('operacao_resultado_definicao', 'ativo',                 'boolean',                    true,  ''),
            ('operacao_resultado_definicao', 'criado_em',             'timestamp with time zone',   true,  ''),
            ('operacao_resultado_definicao', 'criado_por',            'character varying(80)',      true,  ''),
            ('operacao_resultado_definicao', 'alterado_em',           'timestamp with time zone',   false, ''),
            ('operacao_resultado_definicao', 'alterado_por',          'character varying(80)',      false, ''),

            ('operacao_resultado_registro', 'codigo_resultado',       'bigint',                    true,  'd'),
            ('operacao_resultado_registro', 'codigo_apontamento',     'bigint',                    true,  ''),
            ('operacao_resultado_registro', 'usuario',                'character varying(80)',      true,  ''),
            ('operacao_resultado_registro', 'estacao',                'character varying(80)',      true,  ''),
            ('operacao_resultado_registro', 'registrado_em',          'timestamp with time zone',   true,  ''),
            ('operacao_resultado_registro', 'mensagem_resumo',        'text',                      true,  ''),
            ('operacao_resultado_registro', 'criado_em',              'timestamp with time zone',   true,  ''),

            ('operacao_resultado_registro_item', 'codigo_resultado_item', 'bigint',                  true,  'd'),
            ('operacao_resultado_registro_item', 'codigo_resultado',      'bigint',                  true,  ''),
            ('operacao_resultado_registro_item', 'codigo_definicao',      'bigint',                  true,  ''),
            ('operacao_resultado_registro_item', 'codigo_item',           'character varying(50)',   true,  ''),
            ('operacao_resultado_registro_item', 'tipo',                  'character varying(40)',   true,  ''),
            ('operacao_resultado_registro_item', 'medida',                'character varying(30)',   true,  ''),
            ('operacao_resultado_registro_item', 'referencia',            'numeric(18,3)',            true,  ''),
            ('operacao_resultado_registro_item', 'resultado',             'numeric(18,3)',            false, ''),
            ('operacao_resultado_registro_item', 'ordem_exibicao',        'integer',                  true,  ''),
            ('operacao_resultado_registro_item', 'obrigatorio',           'boolean',                  true,  ''),
            ('operacao_resultado_registro_item', 'criado_em',             'timestamp with time zone', true,  '')
        ) AS e(tabela, coluna, tipo, not_null, identity_kind)
    LOOP
        SELECT format_type(a.atttypid, a.atttypmod), a.attnotnull, a.attidentity::text
          INTO v_tipo_atual, v_notnull_atual, v_identity_atual
          FROM pg_attribute a
         WHERE a.attrelid = to_regclass('homologacao.' || r.tabela)
           AND a.attname = r.coluna
           AND a.attnum > 0
           AND NOT a.attisdropped;

        IF v_tipo_atual IS NULL
           OR v_tipo_atual <> r.tipo
           OR v_notnull_atual IS DISTINCT FROM r.not_null
           OR v_identity_atual IS DISTINCT FROM r.identity_kind THEN
            RAISE EXCEPTION
                'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Coluna %.% esperada tipo=%, not_null=%, identity=%; atual tipo=%, not_null=%, identity=%.',
                r.tabela, r.coluna, r.tipo, r.not_null, r.identity_kind,
                v_tipo_atual, v_notnull_atual, v_identity_atual;
        END IF;
    END LOOP;

    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 13),
            ('operacao_resultado_registro', 7),
            ('operacao_resultado_registro_item', 11)
        ) AS e(tabela, qtd)
    LOOP
        SELECT count(*) INTO v_colunas_atuais
        FROM pg_attribute
        WHERE attrelid = to_regclass('homologacao.' || r.tabela)
          AND attnum > 0 AND NOT attisdropped;
        IF v_colunas_atuais <> r.qtd THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Tabela % possui % colunas; esperado %.', r.tabela, v_colunas_atuais, r.qtd;
        END IF;
    END LOOP;

    -- Defaults obrigatorios.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'obrigatorio',    'true'),
            ('operacao_resultado_definicao', 'ativo',          'true'),
            ('operacao_resultado_definicao', 'criado_em',      'now()'),
            ('operacao_resultado_registro',  'estacao',        ''''''),
            ('operacao_resultado_registro',  'registrado_em',  'now()'),
            ('operacao_resultado_registro',  'mensagem_resumo',''''''),
            ('operacao_resultado_registro',  'criado_em',      'now()'),
            ('operacao_resultado_registro_item', 'criado_em',  'now()')
        ) AS e(tabela, coluna, trecho)
    LOOP
        SELECT pg_get_expr(d.adbin, d.adrelid)
          INTO v_default
          FROM pg_attrdef d
          JOIN pg_attribute a ON a.attrelid = d.adrelid AND a.attnum = d.adnum
         WHERE d.adrelid = to_regclass('homologacao.' || r.tabela)
           AND a.attname = r.coluna;
        IF v_default IS NULL OR position(r.trecho in lower(v_default)) = 0 THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Default %.% incorreto: %.', r.tabela, r.coluna, v_default;
        END IF;
    END LOOP;

    -- PKs.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'pk_operacao_resultado_definicao', 'codigo_definicao'),
            ('operacao_resultado_registro', 'pk_operacao_resultado_registro', 'codigo_resultado'),
            ('operacao_resultado_registro_item', 'pk_operacao_resultado_registro_item', 'codigo_resultado_item')
        ) AS e(tabela, constraint_name, coluna)
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            WHERE c.conrelid = to_regclass('homologacao.' || r.tabela)
              AND c.conname = r.constraint_name
              AND c.contype = 'p'
              AND cardinality(c.conkey) = 1
              AND a.attname = r.coluna
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. PK % ausente/incorreta.', r.constraint_name;
        END IF;
    END LOOP;

    -- FKs RESTRICT.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'fk_resultado_definicao_configuracao', 'codigo_configuracao', 'operacao_producao_configuracao', 'codigo_configuracao'),
            ('operacao_resultado_registro', 'fk_resultado_registro_apontamento', 'codigo_apontamento', 'operacao_producao_apontamento', 'codigo_apontamento'),
            ('operacao_resultado_registro_item', 'fk_resultado_item_resultado', 'codigo_resultado', 'operacao_resultado_registro', 'codigo_resultado'),
            ('operacao_resultado_registro_item', 'fk_resultado_item_definicao', 'codigo_definicao', 'operacao_resultado_definicao', 'codigo_definicao')
        ) AS e(tabela, constraint_name, coluna, tabela_ref, coluna_ref)
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            JOIN pg_attribute ar ON ar.attrelid = c.confrelid AND ar.attnum = c.confkey[1]
            WHERE c.conrelid = to_regclass('homologacao.' || r.tabela)
              AND c.conname = r.constraint_name
              AND c.contype = 'f'
              AND c.confrelid = to_regclass('homologacao.' || r.tabela_ref)
              AND c.confdeltype = 'r'
              AND cardinality(c.conkey) = 1
              AND cardinality(c.confkey) = 1
              AND a.attname = r.coluna
              AND ar.attname = r.coluna_ref
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. FK % ausente/incorreta ou sem ON DELETE RESTRICT.', r.constraint_name;
        END IF;
    END LOOP;

    -- Unique constraints de historico.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_registro', 'uq_operacao_resultado_registro_apontamento', ARRAY['codigo_apontamento']::text[]),
            ('operacao_resultado_registro_item', 'uq_operacao_resultado_item_codigo', ARRAY['codigo_resultado','codigo_item']::text[])
        ) AS e(tabela, constraint_name, colunas)
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint c
            WHERE c.conrelid = to_regclass('homologacao.' || r.tabela)
              AND c.conname = r.constraint_name
              AND c.contype = 'u'
              AND (
                  SELECT array_agg(a.attname ORDER BY u.ord)
                  FROM unnest(c.conkey) WITH ORDINALITY AS u(attnum, ord)
                  JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = u.attnum
              ) = r.colunas
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Unique constraint % ausente/incorreta.', r.constraint_name;
        END IF;
    END LOOP;

    -- Checks nomeados e conteudo essencial.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'ck_resultado_definicao_codigo_item_nao_vazio', ARRAY['codigo_item','btrim']::text[]),
            ('operacao_resultado_definicao', 'ck_resultado_definicao_tipo_nao_vazio', ARRAY['tipo','btrim']::text[]),
            ('operacao_resultado_definicao', 'ck_resultado_definicao_medida_nao_vazia', ARRAY['medida','btrim']::text[]),
            ('operacao_resultado_definicao', 'ck_resultado_definicao_referencia_nao_negativa', ARRAY['referencia','>=','0']::text[]),
            ('operacao_resultado_definicao', 'ck_resultado_definicao_ordem_positiva', ARRAY['ordem_exibicao','>','0']::text[]),
            ('operacao_resultado_registro_item', 'ck_resultado_item_referencia_nao_negativa', ARRAY['referencia','>=','0']::text[]),
            ('operacao_resultado_registro_item', 'ck_resultado_item_resultado_nao_negativo', ARRAY['resultado','is null','>=']::text[]),
            ('operacao_resultado_registro_item', 'ck_resultado_item_ordem_positiva', ARRAY['ordem_exibicao','>','0']::text[]),
            ('operacao_resultado_registro_item', 'ck_resultado_item_obrigatorio_preenchido', ARRAY['obrigatorio','resultado','is not null']::text[])
        ) AS e(tabela, constraint_name, trechos)
    LOOP
        SELECT lower(pg_get_constraintdef(c.oid, true)) INTO v_def
        FROM pg_constraint c
        WHERE c.conrelid = to_regclass('homologacao.' || r.tabela)
          AND c.conname = r.constraint_name
          AND c.contype = 'c'
       AND c.convalidated = true;
        IF v_def IS NULL OR EXISTS (
            SELECT 1 FROM unnest(r.trechos) t WHERE position(lower(t) in v_def) = 0
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Check % ausente/incorreto: %.', r.constraint_name, v_def;
        END IF;
    END LOOP;

    -- Indices exigidos.
    FOR r IN
        SELECT * FROM (VALUES
            ('uq_resultado_definicao_ativa_item', true,  ARRAY['codigo_configuracao','codigo_item','where','ativo']::text[]),
            ('uq_resultado_definicao_ativa_ordem', true, ARRAY['codigo_configuracao','ordem_exibicao','where','ativo']::text[]),
            ('ix_resultado_definicao_config_ativo', false, ARRAY['codigo_configuracao','ativo']::text[]),
            ('ix_resultado_definicao_tipo_medida', false, ARRAY['tipo','medida','where','ativo']::text[]),
            ('ix_resultado_registro_item_resultado', false, ARRAY['codigo_resultado']::text[])
        ) AS e(index_name, unico, trechos)
    LOOP
        SELECT lower(indexdef) INTO v_def
        FROM pg_indexes
        WHERE schemaname = 'homologacao' AND indexname = r.index_name;
        IF v_def IS NULL
           OR (r.unico AND position('create unique index' in v_def) = 0)
           OR (NOT r.unico AND position('create unique index' in v_def) > 0)
           OR EXISTS (SELECT 1 FROM unnest(r.trechos) t WHERE position(lower(t) in v_def) = 0) THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Indice % ausente/incorreto: %.', r.index_name, v_def;
        END IF;
    END LOOP;

    -- Grants minimos e ausencia de privilegios de escrita indevidos.
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
        RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Matriz de grants das tabelas 041 divergente.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='homologacao'
          AND c.relname IN ('operacao_resultado_definicao','operacao_resultado_registro','operacao_resultado_registro_item')
          AND pg_get_userbyid(c.relowner)='fugapet_q_app'
    ) THEN
        RAISE EXCEPTION 'PREFLIGHT 041: fugapet_q_app nao pode ser owner das tabelas 041.';
    END IF;

    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_resultado_definicao', 'codigo_definicao'),
            ('operacao_resultado_registro', 'codigo_resultado'),
            ('operacao_resultado_registro_item', 'codigo_resultado_item')
        ) AS e(tabela, coluna)
    LOOP
        v_seq := pg_get_serial_sequence('homologacao.' || r.tabela, r.coluna);
        IF v_seq IS NULL
           OR NOT has_sequence_privilege('fugapet_q_app', v_seq, 'USAGE')
           OR NOT has_sequence_privilege('fugapet_q_app', v_seq, 'SELECT')
           OR has_sequence_privilege('fugapet_q_app', v_seq, 'UPDATE') THEN
            RAISE EXCEPTION 'PREFLIGHT 041: PACOTE_PARCIAL_INCOMPATIVEL. Grants da sequence de %.% divergentes.', r.tabela, r.coluna;
        END IF;
    END LOOP;

    SELECT count(*) INTO v_qtd_definicoes FROM homologacao.operacao_resultado_definicao;
    SELECT count(*) INTO v_qtd_resultados FROM homologacao.operacao_resultado_registro;
    SELECT count(*) INTO v_qtd_itens FROM homologacao.operacao_resultado_registro_item;

    RAISE NOTICE
        'PREFLIGHT OK: PACOTE_COMPLETAMENTE_APLICADO - estrutura 041 compativel. Registros atuais: definicoes=%, resultados=%, itens=%.',
        v_qtd_definicoes, v_qtd_resultados, v_qtd_itens;
END $$;

\echo 'PREFLIGHT 041 - CONSTRAINT ATUAL DE TIPO_PROCESSO'
SELECT c.conname, c.convalidated, pg_get_constraintdef(c.oid, true) AS definicao
FROM pg_constraint c
WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
  AND c.conname = 'ck_operacao_config_tipo_processo';

\echo 'PREFLIGHT 041 - ROTAS 040'
WITH cfg AS (
    SELECT
        codigo_configuracao,
        COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
        operacao_sap,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo,
        criado_por
    FROM homologacao.operacao_producao_configuracao
    WHERE ativo = true
)
SELECT * FROM cfg WHERE operacao_normalizada IN ('50','60') ORDER BY operacao_normalizada;

ROLLBACK;
\echo 'OK - PREFLIGHT 041 DEV concluido sem alteracao persistente'
