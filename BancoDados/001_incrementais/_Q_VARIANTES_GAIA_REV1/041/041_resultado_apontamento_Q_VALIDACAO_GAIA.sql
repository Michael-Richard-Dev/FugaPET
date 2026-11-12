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
-- 041_resultado_apontamento_DEV_VALIDACAO_GAIA.sql
-- Validacao estrutural e funcional transacional do incremental 041 DEV.
-- Os cenarios usam IDs negativos explicitos para nao avancar sequences identity.
-- Todos os dados de teste sao descartados por ROLLBACK interno.
-- ============================================================================
SET search_path TO homologacao, public;

-- Snapshots de integridade antes dos testes.
SELECT set_config(
    'fugapet_v041.config_hash_antes',
    md5(COALESCE(string_agg(md5(row_to_json(t)::text), '' ORDER BY codigo_configuracao), '')),
    false
) FROM homologacao.operacao_producao_configuracao t;
SELECT set_config(
    'fugapet_v041.apont_hash_antes',
    md5(COALESCE(string_agg(md5(row_to_json(t)::text), '' ORDER BY codigo_apontamento), '')),
    false
) FROM homologacao.operacao_producao_apontamento t;
SELECT set_config(
    'fugapet_v041.evento_hash_antes',
    md5(COALESCE(string_agg(md5(row_to_json(t)::text), '' ORDER BY codigo_evento_apontamento), '')),
    false
) FROM homologacao.operacao_producao_evento t;
SELECT set_config('fugapet_v041.def_qtd_antes', count(*)::text, false)
FROM homologacao.operacao_resultado_definicao;
SELECT set_config('fugapet_v041.reg_qtd_antes', count(*)::text, false)
FROM homologacao.operacao_resultado_registro;
SELECT set_config('fugapet_v041.item_qtd_antes', count(*)::text, false)
FROM homologacao.operacao_resultado_registro_item;

DO $$
DECLARE
    v_constraint_def text;
    v_tipos text[];
    v_tipos_041 constant text[] := ARRAY[
        'CONSUMO_MATERIA_PRIMA',
        'CONSUMO_QUIMICOS',
        'CONSUMO_SEMI_ACABADO',
        'PRODUTO_ACABADO',
        'RESULTADO_APONTAMENTO',
        'SEMI_ACABADO',
        'SEM_DESTINO_CONFIGURADO'
    ]::text[];
    v_seq text;
    v_tipo_atual text;
    v_notnull_atual boolean;
    v_identity_atual text;
    v_colunas_atuais integer;
    v_default text;
    r record;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2'
       OR to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO 041: ambiente DEV incorreto.';
    END IF;

    IF to_regclass('homologacao.operacao_resultado_definicao') IS NULL
       OR to_regclass('homologacao.operacao_resultado_registro') IS NULL
       OR to_regclass('homologacao.operacao_resultado_registro_item') IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO 041: uma ou mais tabelas 041 nao existem.';
    END IF;

    -- Tipos, tamanhos, nulabilidade e identity das colunas.
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
           AND a.attnum > 0 AND NOT a.attisdropped;

        IF v_tipo_atual IS NULL OR v_tipo_atual <> r.tipo
           OR v_notnull_atual IS DISTINCT FROM r.not_null
           OR v_identity_atual IS DISTINCT FROM r.identity_kind THEN
            RAISE EXCEPTION
                'VALIDACAO 041: coluna %.% divergente. esperado tipo=%, not_null=%, identity=%; atual tipo=%, not_null=%, identity=%.',
                r.tabela, r.coluna, r.tipo, r.not_null, r.identity_kind,
                v_tipo_atual, v_notnull_atual, v_identity_atual;
        END IF;
    END LOOP;

    FOR r IN SELECT * FROM (VALUES
        ('operacao_resultado_definicao',13),
        ('operacao_resultado_registro',7),
        ('operacao_resultado_registro_item',11)
    ) AS e(tabela,qtd)
    LOOP
        SELECT count(*) INTO v_colunas_atuais FROM pg_attribute
        WHERE attrelid=to_regclass('homologacao.' || r.tabela)
          AND attnum>0 AND NOT attisdropped;
        IF v_colunas_atuais <> r.qtd THEN
            RAISE EXCEPTION 'VALIDACAO 041: tabela % possui % colunas; esperado %.', r.tabela, v_colunas_atuais, r.qtd;
        END IF;
    END LOOP;

    FOR r IN SELECT * FROM (VALUES
        ('operacao_resultado_definicao','obrigatorio','true'),
        ('operacao_resultado_definicao','ativo','true'),
        ('operacao_resultado_definicao','criado_em','now()'),
        ('operacao_resultado_registro','estacao',$q$''$q$),
        ('operacao_resultado_registro','registrado_em','now()'),
        ('operacao_resultado_registro','mensagem_resumo',$q$''$q$),
        ('operacao_resultado_registro','criado_em','now()'),
        ('operacao_resultado_registro_item','criado_em','now()')
    ) AS e(tabela,coluna,trecho)
    LOOP
        SELECT pg_get_expr(d.adbin,d.adrelid) INTO v_default
        FROM pg_attrdef d JOIN pg_attribute a
          ON a.attrelid=d.adrelid AND a.attnum=d.adnum
        WHERE d.adrelid=to_regclass('homologacao.' || r.tabela)
          AND a.attname=r.coluna;
        IF v_default IS NULL OR position(r.trecho in lower(v_default))=0 THEN
            RAISE EXCEPTION 'VALIDACAO 041: default %.% divergente: %.', r.tabela, r.coluna, v_default;
        END IF;
    END LOOP;

    SELECT pg_get_constraintdef(c.oid, true)
      INTO v_constraint_def
      FROM pg_constraint c
     WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
       AND c.conname = 'ck_operacao_config_tipo_processo'
       AND c.contype = 'c'
       AND c.convalidated = true;

    SELECT array_agg(x.match[1] ORDER BY x.match[1]) INTO v_tipos
    FROM regexp_matches(v_constraint_def, '''([^'']+)''', 'g') AS x(match);

    IF v_constraint_def IS NULL
       OR position('tipo_processo' in lower(v_constraint_def)) = 0
       OR position('btrim' in lower(v_constraint_def)) = 0
       OR v_tipos IS DISTINCT FROM v_tipos_041 THEN
        RAISE EXCEPTION 'VALIDACAO 041: constraint de tipo_processo divergente: %.', v_constraint_def;
    END IF;

    -- Estruturas principais: PK, FKs RESTRICT e unique constraints.
    IF (SELECT count(*) FROM pg_constraint
        WHERE conname IN (
            'pk_operacao_resultado_definicao',
            'pk_operacao_resultado_registro',
            'pk_operacao_resultado_registro_item'
        ) AND contype = 'p') <> 3 THEN
        RAISE EXCEPTION 'VALIDACAO 041: PKs esperadas nao encontradas.';
    END IF;

    IF (SELECT count(*) FROM pg_constraint
        WHERE conname IN (
            'fk_resultado_definicao_configuracao',
            'fk_resultado_registro_apontamento',
            'fk_resultado_item_resultado',
            'fk_resultado_item_definicao'
        ) AND contype = 'f' AND confdeltype = 'r') <> 4 THEN
        RAISE EXCEPTION 'VALIDACAO 041: FKs ON DELETE RESTRICT esperadas nao encontradas.';
    END IF;

    IF (SELECT count(*) FROM pg_constraint
        WHERE conname IN (
            'uq_operacao_resultado_registro_apontamento',
            'uq_operacao_resultado_item_codigo'
        ) AND contype = 'u') <> 2 THEN
        RAISE EXCEPTION 'VALIDACAO 041: unique constraints de registro/item ausentes.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='homologacao' AND indexname='uq_resultado_definicao_ativa_item' AND indexdef ILIKE 'CREATE UNIQUE INDEX%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='homologacao' AND indexname='uq_resultado_definicao_ativa_ordem' AND indexdef ILIKE 'CREATE UNIQUE INDEX%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='homologacao' AND indexname='ix_resultado_definicao_config_ativo')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='homologacao' AND indexname='ix_resultado_definicao_tipo_medida')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='homologacao' AND indexname='ix_resultado_registro_item_resultado') THEN
        RAISE EXCEPTION 'VALIDACAO 041: indices esperados ausentes.';
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
        RAISE EXCEPTION 'VALIDACAO 041: grants das tabelas divergentes.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='homologacao'
          AND c.relname IN ('operacao_resultado_definicao','operacao_resultado_registro','operacao_resultado_registro_item')
          AND pg_get_userbyid(c.relowner)='fugapet_q_app'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 041: fugapet_q_app nao pode ser owner das tabelas 041.';
    END IF;

    FOR r IN SELECT * FROM (VALUES
        ('operacao_resultado_definicao','codigo_definicao'),
        ('operacao_resultado_registro','codigo_resultado'),
        ('operacao_resultado_registro_item','codigo_resultado_item')
    ) AS e(tabela,coluna)
    LOOP
        v_seq := pg_get_serial_sequence('homologacao.' || r.tabela, r.coluna);
        IF v_seq IS NULL
           OR NOT has_sequence_privilege('fugapet_q_app', v_seq, 'USAGE')
           OR NOT has_sequence_privilege('fugapet_q_app', v_seq, 'SELECT')
           OR has_sequence_privilege('fugapet_q_app', v_seq, 'UPDATE') THEN
            RAISE EXCEPTION 'VALIDACAO 041: grants de sequence divergentes para %.%.', r.tabela, r.coluna;
        END IF;
    END LOOP;

    RAISE NOTICE 'VALIDACAO 041 ESTRUTURAL OK: constraint, tabelas, PKs, FKs RESTRICT, uniques, indices e grants confirmados.';
END $$;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_bloqueou boolean;
BEGIN
    IF EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao WHERE codigo_configuracao IN (-41901,-41902,-41903)
    ) OR EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_apontamento WHERE codigo_apontamento = -41920
    ) OR EXISTS (
        SELECT 1 FROM homologacao.operacao_resultado_definicao WHERE codigo_definicao BETWEEN -41919 AND -41910
    ) OR EXISTS (
        SELECT 1 FROM homologacao.operacao_resultado_registro WHERE codigo_resultado BETWEEN -41939 AND -41930
    ) OR EXISTS (
        SELECT 1 FROM homologacao.operacao_resultado_registro_item WHERE codigo_resultado_item BETWEEN -41949 AND -41940
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 041: IDs negativos reservados para teste ja existem. Interrompendo.';
    END IF;

    -- Novos tipos aceitos.
    INSERT INTO homologacao.operacao_producao_configuracao
        (codigo_configuracao, centro, tipo_ordem, sequencia_sap, operacao_sap,
         suboperacao_sap, centro_trabalho, tipo_processo, tela_destino,
         exige_operacao_anterior, ativo, criado_por)
    VALUES
        (-41901, '', '', '', '9911', '', '', 'CONSUMO_SEMI_ACABADO', 'VALIDACAO_041', false, true, 'VALIDACAO_041'),
        (-41902, '', '', '', '9912', '', '', 'RESULTADO_APONTAMENTO', 'VALIDACAO_041', false, true, 'VALIDACAO_041');

    -- Tipo invalido bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_producao_configuracao
            (codigo_configuracao, centro, tipo_ordem, sequencia_sap, operacao_sap,
             suboperacao_sap, centro_trabalho, tipo_processo, tela_destino,
             exige_operacao_anterior, ativo, criado_por)
        VALUES (-41903, '', '', '', '9913', '', '', 'TIPO_INVALIDO_041', 'VALIDACAO_041', false, true, 'VALIDACAO_041');
    EXCEPTION WHEN check_violation THEN
        v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: tipo de processo invalido foi aceito.'; END IF;

    -- Definicoes validas obrigatoria e opcional.
    INSERT INTO homologacao.operacao_resultado_definicao
        (codigo_definicao, codigo_configuracao, codigo_item, tipo, medida,
         referencia, ordem_exibicao, obrigatorio, ativo, criado_por)
    VALUES
        (-41910, -41902, 'ITEM_OBRIGATORIO_041', 'TEMPO_TESTE', 'MINUTO_TESTE', 5.000, 1, true, true, 'VALIDACAO_041'),
        (-41911, -41902, 'ITEM_OPCIONAL_041', 'TEMPO_TESTE', 'MINUTO_TESTE', 0.000, 2, false, true, 'VALIDACAO_041');

    -- Duplicidade ativa por codigo_item bloqueada.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_definicao
            (codigo_definicao, codigo_configuracao, codigo_item, tipo, medida,
             referencia, ordem_exibicao, obrigatorio, ativo, criado_por)
        VALUES (-41912, -41902, 'ITEM_OBRIGATORIO_041', 'OUTRO', 'UNIDADE', 1, 3, true, true, 'VALIDACAO_041');
    EXCEPTION WHEN unique_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: duplicidade ativa de codigo_item foi aceita.'; END IF;

    -- Duplicidade ativa por ordem_exibicao bloqueada.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_definicao
            (codigo_definicao, codigo_configuracao, codigo_item, tipo, medida,
             referencia, ordem_exibicao, obrigatorio, ativo, criado_por)
        VALUES (-41913, -41902, 'ITEM_ORDEM_DUP_041', 'OUTRO', 'UNIDADE', 1, 1, true, true, 'VALIDACAO_041');
    EXCEPTION WHEN unique_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: duplicidade ativa de ordem_exibicao foi aceita.'; END IF;

    -- Referencia negativa bloqueada.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_definicao
            (codigo_definicao, codigo_configuracao, codigo_item, tipo, medida,
             referencia, ordem_exibicao, obrigatorio, ativo, criado_por)
        VALUES (-41914, -41902, 'ITEM_NEGATIVO_041', 'OUTRO', 'UNIDADE', -1, 4, true, true, 'VALIDACAO_041');
    EXCEPTION WHEN check_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: referencia negativa foi aceita.'; END IF;

    -- Definicao orfa bloqueada.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_definicao
            (codigo_definicao, codigo_configuracao, codigo_item, tipo, medida,
             referencia, ordem_exibicao, obrigatorio, ativo, criado_por)
        VALUES (-41915, -999999999, 'ITEM_ORFAO_041', 'OUTRO', 'UNIDADE', 1, 5, true, true, 'VALIDACAO_041');
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: definicao orfa foi aceita.'; END IF;

    -- Apontamento de teste valido do 039, usando ID negativo explicito.
    INSERT INTO homologacao.operacao_producao_apontamento
        (codigo_apontamento, numero_ordem, item_ordem, produto, sequencia,
         operacao, suboperacao, descricao_operacao, centro_trabalho,
         tipo_processo, tela_destino, status, usuario_inicio, estacao_inicio,
         codigo_barras_inicio, correlation_id, idempotency_key)
    VALUES
        (-41920, 'OP_VALIDACAO_041', '0010', 'PRODUTO_TESTE', '0099',
         '9912', '', 'VALIDACAO 041', '', 'RESULTADO_APONTAMENTO',
         'ProcessoResultadoApontamentoForm', 'EM_ANDAMENTO', 'VALIDACAO_041',
         'ESTACAO_TESTE', 'CODIGO_TESTE_041', 'CORR_VALIDACAO_041', 'IDEMP_VALIDACAO_041');

    -- Resultado orfao bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro
            (codigo_resultado, codigo_apontamento, usuario, estacao, mensagem_resumo)
        VALUES (-41931, -999999999, 'VALIDACAO_041', 'ESTACAO_TESTE', 'ORFAO');
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: resultado orfao foi aceito.'; END IF;

    INSERT INTO homologacao.operacao_resultado_registro
        (codigo_resultado, codigo_apontamento, usuario, estacao, mensagem_resumo)
    VALUES (-41930, -41920, 'VALIDACAO_041', 'ESTACAO_TESTE', 'RESULTADO TESTE');

    -- Um unico cabecalho por apontamento.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro
            (codigo_resultado, codigo_apontamento, usuario, estacao, mensagem_resumo)
        VALUES (-41932, -41920, 'VALIDACAO_041', 'ESTACAO_TESTE', 'DUPLICADO');
    EXCEPTION WHEN unique_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: segundo cabecalho para o mesmo apontamento foi aceito.'; END IF;

    -- Item opcional nulo aceito.
    INSERT INTO homologacao.operacao_resultado_registro_item
        (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
         tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
    VALUES
        (-41940, -41930, -41911, 'ITEM_OPCIONAL_041', 'TEMPO_TESTE',
         'MINUTO_TESTE', 0, NULL, 2, false);

    -- Item obrigatorio nulo bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro_item
            (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
             tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
        VALUES
            (-41941, -41930, -41910, 'ITEM_OBRIGATORIO_041', 'TEMPO_TESTE',
             'MINUTO_TESTE', 5, NULL, 1, true);
    EXCEPTION WHEN check_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: item obrigatorio nulo foi aceito.'; END IF;

    -- Item obrigatorio valido.
    INSERT INTO homologacao.operacao_resultado_registro_item
        (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
         tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
    VALUES
        (-41942, -41930, -41910, 'ITEM_OBRIGATORIO_041', 'TEMPO_TESTE',
         'MINUTO_TESTE', 5, 5, 1, true);

    -- Resultado negativo bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro_item
            (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
             tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
        VALUES
            (-41943, -41930, -41910, 'ITEM_NEGATIVO_RESULT_041', 'TEMPO_TESTE',
             'MINUTO_TESTE', 5, -1, 3, true);
    EXCEPTION WHEN check_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: resultado negativo foi aceito.'; END IF;

    -- Item duplicado no mesmo resultado bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro_item
            (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
             tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
        VALUES
            (-41944, -41930, -41910, 'ITEM_OBRIGATORIO_041', 'TEMPO_TESTE',
             'MINUTO_TESTE', 5, 6, 1, true);
    EXCEPTION WHEN unique_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: item duplicado no resultado foi aceito.'; END IF;

    -- Item orfao de resultado bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro_item
            (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
             tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
        VALUES
            (-41945, -999999999, -41910, 'ITEM_ORFAO_RESULTADO_041', 'TEMPO_TESTE',
             'MINUTO_TESTE', 5, 5, 4, true);
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: item orfao de resultado foi aceito.'; END IF;

    -- Item orfao de definicao bloqueado.
    v_bloqueou := false;
    BEGIN
        INSERT INTO homologacao.operacao_resultado_registro_item
            (codigo_resultado_item, codigo_resultado, codigo_definicao, codigo_item,
             tipo, medida, referencia, resultado, ordem_exibicao, obrigatorio)
        VALUES
            (-41946, -41930, -999999999, 'ITEM_ORFAO_DEFINICAO_041', 'TEMPO_TESTE',
             'MINUTO_TESTE', 5, 5, 5, true);
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: item orfao de definicao foi aceito.'; END IF;

    -- ON DELETE RESTRICT confirmado por comportamento.
    v_bloqueou := false;
    BEGIN
        DELETE FROM homologacao.operacao_producao_configuracao WHERE codigo_configuracao = -41902;
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: exclusao da configuracao pai nao foi bloqueada.'; END IF;

    v_bloqueou := false;
    BEGIN
        DELETE FROM homologacao.operacao_producao_apontamento WHERE codigo_apontamento = -41920;
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: exclusao do apontamento pai nao foi bloqueada.'; END IF;

    v_bloqueou := false;
    BEGIN
        DELETE FROM homologacao.operacao_resultado_registro WHERE codigo_resultado = -41930;
    EXCEPTION WHEN foreign_key_violation THEN v_bloqueou := true;
    END;
    IF NOT v_bloqueou THEN RAISE EXCEPTION 'VALIDACAO 041: exclusao do resultado pai nao foi bloqueada.'; END IF;

    RAISE NOTICE 'VALIDACAO 041 FUNCIONAL OK: novos tipos aceitos, invalido bloqueado, unicidades/checks/FKs RESTRICT confirmados e opcional nulo aceito.';
    RAISE NOTICE 'VALIDACAO 041: descartando integralmente os dados de teste por rollback interno intencional.';
END $$;

ROLLBACK;

DO $$
DECLARE
    v_hash text;
    v_qtd bigint;
BEGIN
    SELECT md5(COALESCE(string_agg(md5(row_to_json(t)::text), '' ORDER BY codigo_configuracao), ''))
      INTO v_hash FROM homologacao.operacao_producao_configuracao t;
    IF v_hash <> current_setting('fugapet_v041.config_hash_antes') THEN
        RAISE EXCEPTION 'VALIDACAO 041: configuracoes 039/040 divergiram apos rollback.';
    END IF;

    SELECT md5(COALESCE(string_agg(md5(row_to_json(t)::text), '' ORDER BY codigo_apontamento), ''))
      INTO v_hash FROM homologacao.operacao_producao_apontamento t;
    IF v_hash <> current_setting('fugapet_v041.apont_hash_antes') THEN
        RAISE EXCEPTION 'VALIDACAO 041: apontamentos 039 divergiram apos rollback.';
    END IF;

    SELECT md5(COALESCE(string_agg(md5(row_to_json(t)::text), '' ORDER BY codigo_evento_apontamento), ''))
      INTO v_hash FROM homologacao.operacao_producao_evento t;
    IF v_hash <> current_setting('fugapet_v041.evento_hash_antes') THEN
        RAISE EXCEPTION 'VALIDACAO 041: eventos 039 divergiram apos rollback.';
    END IF;

    SELECT count(*) INTO v_qtd FROM homologacao.operacao_resultado_definicao;
    IF v_qtd <> current_setting('fugapet_v041.def_qtd_antes')::bigint THEN
        RAISE EXCEPTION 'VALIDACAO 041: residuos em operacao_resultado_definicao.';
    END IF;
    SELECT count(*) INTO v_qtd FROM homologacao.operacao_resultado_registro;
    IF v_qtd <> current_setting('fugapet_v041.reg_qtd_antes')::bigint THEN
        RAISE EXCEPTION 'VALIDACAO 041: residuos em operacao_resultado_registro.';
    END IF;
    SELECT count(*) INTO v_qtd FROM homologacao.operacao_resultado_registro_item;
    IF v_qtd <> current_setting('fugapet_v041.item_qtd_antes')::bigint THEN
        RAISE EXCEPTION 'VALIDACAO 041: residuos em operacao_resultado_registro_item.';
    END IF;

    IF EXISTS (SELECT 1 FROM homologacao.operacao_producao_configuracao WHERE codigo_configuracao IN (-41901,-41902,-41903))
       OR EXISTS (SELECT 1 FROM homologacao.operacao_producao_apontamento WHERE codigo_apontamento = -41920)
       OR EXISTS (SELECT 1 FROM homologacao.operacao_resultado_definicao WHERE criado_por = 'VALIDACAO_041')
       OR EXISTS (SELECT 1 FROM homologacao.operacao_resultado_registro WHERE usuario = 'VALIDACAO_041') THEN
        RAISE EXCEPTION 'VALIDACAO 041: residuos identificados pelos marcadores de teste.';
    END IF;

    RAISE NOTICE 'VALIDACAO 041 POS-ROLLBACK OK: zero residuos; incrementais 039/040 preservados.';
END $$;

\echo 'OK - persistencia de resultado do apontamento 041 validada (DEV)'
