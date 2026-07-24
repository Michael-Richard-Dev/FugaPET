\set ON_ERROR_STOP on
-- ============================================================
-- 039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_Dev  |  Schema: desenvolvimento
--
-- EXECUCAO CONTROLADA - NAO EXECUTAR AUTOMATICAMENTE. SOMENTE LEITURA.
-- Rodar ANTES da PROPOSTA. Reporta o estado e ABORTA (EXCEPTION) em estrutura parcial incompativel
-- ou duplicidade que quebraria os indices unicos. NUNCA reporta OK para estrutura incompativel.
-- ============================================================
SET search_path TO desenvolvimento;

-- Somente leitura, mas tambem nao pode ficar bloqueado indefinidamente esperando lock/consulta.
SET lock_timeout = '5s';
SET statement_timeout = '30s';

-- Estado dos objetos-alvo.
SELECT 'operacao_producao_configuracao' AS objeto, to_regclass('desenvolvimento.operacao_producao_configuracao') AS existe
UNION ALL
SELECT 'operacao_producao_apontamento', to_regclass('desenvolvimento.operacao_producao_apontamento')
UNION ALL
SELECT 'operacao_producao_evento', to_regclass('desenvolvimento.operacao_producao_evento');

-- Papel da aplicação DEV é obrigatório: sem ele a proposta deixaria os objetos inacessíveis ao app.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_dev_app') THEN
        RAISE EXCEPTION 'PREFLIGHT: papel fugapet_dev_app inexistente. A aplicação DEV não teria acesso aos objetos do 039.';
    END IF;
END $$;

-- Colunas atuais (conferencia visual).
SELECT table_name, column_name, data_type, is_nullable
  FROM information_schema.columns
 WHERE table_schema = 'desenvolvimento'
   AND table_name IN ('operacao_producao_configuracao','operacao_producao_apontamento','operacao_producao_evento')
 ORDER BY table_name, ordinal_position;

-- ---------- VEREDITO PRELIMINAR: nenhuma tabela / parte / todas ----------
-- PACOTE_NAO_APLICADO         -> nenhuma das tres existe: seguir para a PROPOSTA apos revisao.
-- PACOTE_PARCIAL_INCOMPATIVEL -> parte existe: ABORTA (nao aplicar por cima de estrutura parcial).
-- PACOTE_COMPLETAMENTE_APLICADO -> as tres existem: as checagens seguintes decidem a compatibilidade.
DO $$
DECLARE
    v_existentes int;
BEGIN
    SELECT count(*) INTO v_existentes
      FROM (VALUES
                ('operacao_producao_configuracao'),
                ('operacao_producao_apontamento'),
                ('operacao_producao_evento')) AS t(tabela)
     WHERE to_regclass('desenvolvimento.' || t.tabela) IS NOT NULL;

    IF v_existentes = 0 THEN
        RAISE NOTICE 'PREFLIGHT: PACOTE_NAO_APLICADO - nenhuma tabela do 039 existe. Seguir para a PROPOSTA apos revisao.';
    ELSIF v_existentes < 3 THEN
        RAISE EXCEPTION 'PREFLIGHT: PACOTE_PARCIAL_INCOMPATIVEL - apenas % de 3 tabelas do 039 existem. Resolva manualmente antes de aplicar.', v_existentes;
    ELSE
        RAISE NOTICE 'PREFLIGHT: PACOTE_COMPLETAMENTE_APLICADO (estrutural) - validando compatibilidade detalhada...';
    END IF;
END $$;

-- ---------- Validacao estrutural (nome + tipo + nullability) ----------
DO $$
DECLARE
    v_problemas text;
BEGIN
    -- operacao_producao_configuracao
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NULL THEN
        RAISE NOTICE 'PREFLIGHT: operacao_producao_configuracao ainda nao existe - sera criada pela PROPOSTA.';
    ELSE
        WITH esperado(coluna, tipo, anulavel) AS (
            VALUES
                ('codigo_configuracao','bigint','NO'),
                ('centro','character varying','NO'),
                ('tipo_ordem','character varying','NO'),
                ('sequencia_sap','character varying','NO'),
                ('operacao_sap','character varying','NO'),
                ('suboperacao_sap','character varying','NO'),
                ('centro_trabalho','character varying','NO'),
                ('tipo_processo','character varying','NO'),
                ('tela_destino','character varying','NO'),
                ('exige_operacao_anterior','boolean','NO'),
                ('ativo','boolean','NO'),
                ('criado_em','timestamp with time zone','NO'),
                ('criado_por','character varying','NO'),
                ('alterado_em','timestamp with time zone','YES'),
                ('alterado_por','character varying','YES')
        )
        SELECT string_agg(
                   e.coluna || ' (esperado ' || e.tipo || '/' || e.anulavel
                   || ', achado ' || coalesce(c.data_type,'AUSENTE') || '/' || coalesce(c.is_nullable,'-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          LEFT JOIN information_schema.columns c
                 ON c.table_schema='desenvolvimento' AND c.table_name='operacao_producao_configuracao'
                AND c.column_name=e.coluna
         WHERE c.column_name IS NULL OR c.data_type <> e.tipo OR c.is_nullable <> e.anulavel;

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT: operacao_producao_configuracao INCOMPATIVEL -> %', v_problemas;
        END IF;
    END IF;

    -- operacao_producao_apontamento
    IF to_regclass('desenvolvimento.operacao_producao_apontamento') IS NULL THEN
        RAISE NOTICE 'PREFLIGHT: operacao_producao_apontamento ainda nao existe - sera criada pela PROPOSTA.';
    ELSE
        WITH esperado(coluna, tipo, anulavel) AS (
            VALUES
                ('codigo_apontamento','bigint','NO'),
                ('numero_ordem','character varying','NO'),
                ('item_ordem','character varying','NO'),
                ('produto','character varying','NO'),
                ('sequencia','character varying','NO'),
                ('operacao','character varying','NO'),
                ('suboperacao','character varying','NO'),
                ('descricao_operacao','character varying','NO'),
                ('centro_trabalho','character varying','NO'),
                ('tipo_processo','character varying','NO'),
                ('tela_destino','character varying','NO'),
                ('status','character varying','NO'),
                ('usuario_inicio','character varying','NO'),
                ('estacao_inicio','character varying','NO'),
                ('iniciado_em','timestamp with time zone','NO'),
                ('codigo_barras_inicio','character varying','NO'),
                ('usuario_termino','character varying','YES'),
                ('estacao_termino','character varying','YES'),
                ('terminado_em','timestamp with time zone','YES'),
                ('codigo_barras_termino','character varying','YES'),
                ('correlation_id','character varying','NO'),
                ('idempotency_key','character varying','NO'),
                ('idempotency_key_termino','character varying','YES'),
                ('resultado_operacional','character varying','YES'),
                ('codigo_registro_processo','bigint','YES'),
                ('concluido_operacional_em','timestamp with time zone','YES'),
                ('mensagem_resultado_operacional','text','YES'),
                ('criado_em','timestamp with time zone','NO'),
                ('atualizado_em','timestamp with time zone','NO')
        )
        SELECT string_agg(
                   e.coluna || ' (esperado ' || e.tipo || '/' || e.anulavel
                   || ', achado ' || coalesce(c.data_type,'AUSENTE') || '/' || coalesce(c.is_nullable,'-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          LEFT JOIN information_schema.columns c
                 ON c.table_schema='desenvolvimento' AND c.table_name='operacao_producao_apontamento'
                AND c.column_name=e.coluna
         WHERE c.column_name IS NULL OR c.data_type <> e.tipo OR c.is_nullable <> e.anulavel;

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT: operacao_producao_apontamento INCOMPATIVEL -> %', v_problemas;
        END IF;
    END IF;

    -- operacao_producao_evento
    IF to_regclass('desenvolvimento.operacao_producao_evento') IS NULL THEN
        RAISE NOTICE 'PREFLIGHT: operacao_producao_evento ainda nao existe - sera criada pela PROPOSTA.';
    ELSE
        WITH esperado(coluna, tipo, anulavel) AS (
            VALUES
                ('codigo_evento_apontamento','bigint','NO'),
                ('codigo_apontamento','bigint','YES'),
                ('codigo_original','character varying','NO'),
                ('formato_codigo','character varying','NO'),
                ('ordem_producao','character varying','NO'),
                ('operacao','character varying','NO'),
                ('codigo_evento','character varying','NO'),
                ('usuario','character varying','NO'),
                ('estacao','character varying','NO'),
                ('ocorrido_em','timestamp with time zone','NO'),
                ('status_anterior','character varying','NO'),
                ('status_novo','character varying','NO'),
                ('resultado','character varying','NO'),
                ('mensagem','text','NO'),
                ('correlation_id','character varying','NO')
        )
        SELECT string_agg(
                   e.coluna || ' (esperado ' || e.tipo || '/' || e.anulavel
                   || ', achado ' || coalesce(c.data_type,'AUSENTE') || '/' || coalesce(c.is_nullable,'-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          LEFT JOIN information_schema.columns c
                 ON c.table_schema='desenvolvimento' AND c.table_name='operacao_producao_evento'
                AND c.column_name=e.coluna
         WHERE c.column_name IS NULL OR c.data_type <> e.tipo OR c.is_nullable <> e.anulavel;

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT: operacao_producao_evento INCOMPATIVEL -> %', v_problemas;
        END IF;
    END IF;
END $$;

-- ---------- Tamanho EXATO de varchar ----------
-- Nome+tipo+nulabilidade nao bastam: um varchar(10) onde se espera varchar(40) e incompativel.
DO $$
DECLARE
    v_problemas text;
BEGIN
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NULL
       AND to_regclass('desenvolvimento.operacao_producao_apontamento') IS NULL
       AND to_regclass('desenvolvimento.operacao_producao_evento') IS NULL THEN
        RETURN;
    END IF;

    WITH esperado(tabela, coluna, tamanho) AS (
        VALUES
            ('operacao_producao_configuracao','centro',10),
            ('operacao_producao_configuracao','tipo_ordem',10),
            ('operacao_producao_configuracao','sequencia_sap',10),
            ('operacao_producao_configuracao','operacao_sap',10),
            ('operacao_producao_configuracao','suboperacao_sap',10),
            ('operacao_producao_configuracao','centro_trabalho',20),
            ('operacao_producao_configuracao','tipo_processo',40),
            ('operacao_producao_configuracao','tela_destino',80),
            ('operacao_producao_configuracao','criado_por',80),
            ('operacao_producao_configuracao','alterado_por',80),
            ('operacao_producao_apontamento','numero_ordem',40),
            ('operacao_producao_apontamento','item_ordem',20),
            ('operacao_producao_apontamento','produto',80),
            ('operacao_producao_apontamento','sequencia',10),
            ('operacao_producao_apontamento','operacao',10),
            ('operacao_producao_apontamento','suboperacao',10),
            ('operacao_producao_apontamento','descricao_operacao',255),
            ('operacao_producao_apontamento','centro_trabalho',20),
            ('operacao_producao_apontamento','tipo_processo',40),
            ('operacao_producao_apontamento','tela_destino',80),
            ('operacao_producao_apontamento','status',30),
            ('operacao_producao_apontamento','usuario_inicio',80),
            ('operacao_producao_apontamento','estacao_inicio',80),
            ('operacao_producao_apontamento','codigo_barras_inicio',60),
            ('operacao_producao_apontamento','usuario_termino',80),
            ('operacao_producao_apontamento','estacao_termino',80),
            ('operacao_producao_apontamento','codigo_barras_termino',60),
            ('operacao_producao_apontamento','correlation_id',40),
            ('operacao_producao_apontamento','idempotency_key',120),
            ('operacao_producao_apontamento','idempotency_key_termino',120),
            ('operacao_producao_apontamento','resultado_operacional',40),
            ('operacao_producao_evento','codigo_original',60),
            ('operacao_producao_evento','formato_codigo',40),
            ('operacao_producao_evento','ordem_producao',40),
            ('operacao_producao_evento','operacao',10),
            ('operacao_producao_evento','codigo_evento',10),
            ('operacao_producao_evento','usuario',80),
            ('operacao_producao_evento','estacao',80),
            ('operacao_producao_evento','status_anterior',30),
            ('operacao_producao_evento','status_novo',30),
            ('operacao_producao_evento','resultado',40),
            ('operacao_producao_evento','correlation_id',40)
    )
    SELECT string_agg(
               e.tabela || '.' || e.coluna || ' (esperado varchar(' || e.tamanho
               || '), achado ' || coalesce(c.character_maximum_length::text, 'AUSENTE') || ')',
               '; ' ORDER BY e.tabela, e.coluna)
      INTO v_problemas
      FROM esperado e
      LEFT JOIN information_schema.columns c
             ON c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna
     WHERE to_regclass('desenvolvimento.' || e.tabela) IS NOT NULL
       AND (c.character_maximum_length IS NULL OR c.character_maximum_length <> e.tamanho);

    IF v_problemas IS NOT NULL THEN
        RAISE EXCEPTION 'PREFLIGHT: tamanho de varchar INCOMPATIVEL -> %', v_problemas;
    END IF;
END $$;

-- ---------- PK + IDENTITY + sequence resolvida ----------
DO $$
DECLARE
    r record;
    v_seq text;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_producao_configuracao','codigo_configuracao'),
            ('operacao_producao_apontamento','codigo_apontamento'),
            ('operacao_producao_evento','codigo_evento_apontamento')
        ) AS t(tabela, coluna)
    LOOP
        CONTINUE WHEN to_regclass('desenvolvimento.' || r.tabela) IS NULL;

        -- bigint + GENERATED BY DEFAULT AS IDENTITY
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema='desenvolvimento' AND table_name=r.tabela AND column_name=r.coluna
               AND data_type='bigint' AND is_identity='YES' AND identity_generation='BY DEFAULT') THEN
            RAISE EXCEPTION 'PREFLIGHT: %.% nao e bigint GENERATED BY DEFAULT AS IDENTITY.', r.tabela, r.coluna;
        END IF;

        -- PK correspondente
        IF NOT EXISTS (
            SELECT 1
              FROM pg_constraint pc
              JOIN pg_attribute pa ON pa.attrelid = pc.conrelid AND pa.attnum = ANY (pc.conkey)
             WHERE pc.conrelid = ('desenvolvimento.' || r.tabela)::regclass
               AND pc.contype = 'p'
               AND pa.attname = r.coluna
               AND array_length(pc.conkey, 1) = 1) THEN
            RAISE EXCEPTION 'PREFLIGHT: %.% nao e a PRIMARY KEY (simples) da tabela.', r.tabela, r.coluna;
        END IF;

        -- sequence resolvida
        v_seq := pg_get_serial_sequence('desenvolvimento.' || r.tabela, r.coluna);
        IF v_seq IS NULL THEN
            RAISE EXCEPTION 'PREFLIGHT: sequence de %.% nao resolvida por pg_get_serial_sequence.', r.tabela, r.coluna;
        END IF;
    END LOOP;
END $$;

-- ---------- Defaults relevantes ----------
DO $$
DECLARE
    v_problemas text;
BEGIN
    WITH esperado(tabela, coluna, fragmento) AS (
        VALUES
            -- status inicial e timestamps
            ('operacao_producao_apontamento','status','EM_ANDAMENTO'),
            ('operacao_producao_apontamento','iniciado_em','now()'),
            ('operacao_producao_apontamento','criado_em','now()'),
            ('operacao_producao_apontamento','atualizado_em','now()'),
            ('operacao_producao_evento','ocorrido_em','now()'),
            ('operacao_producao_configuracao','criado_em','now()'),
            -- booleanos
            ('operacao_producao_configuracao','ativo','true'),
            ('operacao_producao_configuracao','exige_operacao_anterior','false'),
            -- strings vazias previstas
            ('operacao_producao_configuracao','centro',''''''),
            ('operacao_producao_configuracao','tipo_ordem',''''''),
            ('operacao_producao_apontamento','item_ordem',''''''),
            ('operacao_producao_apontamento','correlation_id','''''')
    )
    SELECT string_agg(
               e.tabela || '.' || e.coluna || ' (esperado default contendo "' || e.fragmento
               || '", achado ' || coalesce(c.column_default, 'SEM DEFAULT') || ')',
               '; ' ORDER BY e.tabela, e.coluna)
      INTO v_problemas
      FROM esperado e
      LEFT JOIN information_schema.columns c
             ON c.table_schema='desenvolvimento' AND c.table_name=e.tabela AND c.column_name=e.coluna
     WHERE to_regclass('desenvolvimento.' || e.tabela) IS NOT NULL
       AND (c.column_default IS NULL OR position(e.fragmento in c.column_default) = 0);

    IF v_problemas IS NOT NULL THEN
        RAISE EXCEPTION 'PREFLIGHT: DEFAULT INCOMPATIVEL -> %', v_problemas;
    END IF;
END $$;

-- ---------- Constraints: existencia E definicao normalizada (pg_get_constraintdef) ----------
DO $$
DECLARE
    r record;
    v_def text;
    v_norm text;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_producao_configuracao','ck_operacao_config_tipo_processo','CONSUMO_MATERIA_PRIMA'),
            ('operacao_producao_configuracao','ck_operacao_config_operacao_nao_vazia','length'),
            ('operacao_producao_apontamento','ck_apontamento_status','EM_ANDAMENTO'),
            ('operacao_producao_apontamento','ck_apontamento_resultado_operacional','ConfirmadoSap'),
            ('operacao_producao_apontamento','ck_apontamento_termino_completo','terminado_em'),
            ('operacao_producao_apontamento','ck_apontamento_ordem_cronologica','iniciado_em')
        ) AS t(tabela, constraint_nome, fragmento)
    LOOP
        CONTINUE WHEN to_regclass('desenvolvimento.' || r.tabela) IS NULL;

        SELECT pg_get_constraintdef(pc.oid) INTO v_def
          FROM pg_constraint pc
         WHERE pc.conrelid = ('desenvolvimento.' || r.tabela)::regclass
           AND pc.conname = r.constraint_nome;

        IF v_def IS NULL THEN
            RAISE EXCEPTION 'PREFLIGHT: constraint % ausente em %.', r.constraint_nome, r.tabela;
        END IF;

        -- Nao basta o NOME: a DEFINICAO precisa conter a regra esperada.
        IF position(r.fragmento in v_def) = 0 THEN
            RAISE EXCEPTION 'PREFLIGHT: constraint % com definicao divergente em % -> %',
                r.constraint_nome, r.tabela, v_def;
        END IF;
    END LOOP;

    IF to_regclass('desenvolvimento.operacao_producao_apontamento') IS NOT NULL THEN
        SELECT pg_get_constraintdef(pc.oid) INTO v_def
          FROM pg_constraint pc
         WHERE pc.conrelid = 'desenvolvimento.operacao_producao_apontamento'::regclass
           AND pc.conname = 'ck_apontamento_status';

        v_norm := lower(regexp_replace(coalesce(v_def, ''), '\s+', ' ', 'g'));

        -- ck_apontamento_status NAO pode aceitar estados calculados de tela.
        IF v_norm LIKE '%pendente%'
           OR v_norm LIKE '%bloqueada%'
           OR v_norm LIKE '%liberada%'
           OR v_norm LIKE '%erro%' THEN
            RAISE EXCEPTION 'PREFLIGHT: ck_apontamento_status aceita estados CALCULADOS (nao persistidos) -> %', v_def;
        END IF;

        IF v_norm NOT LIKE '%em_andamento%'
           OR v_norm NOT LIKE '%aguardando_finalizacao%'
           OR v_norm NOT LIKE '%concluida%'
           OR v_norm NOT LIKE '%cancelada%' THEN
            RAISE EXCEPTION 'PREFLIGHT: ck_apontamento_status nao contem exatamente os estados persistidos esperados -> %', v_def;
        END IF;

        SELECT pg_get_constraintdef(pc.oid) INTO v_def
          FROM pg_constraint pc
         WHERE pc.conrelid = 'desenvolvimento.operacao_producao_apontamento'::regclass
           AND pc.conname = 'ck_apontamento_resultado_operacional';

        v_norm := lower(regexp_replace(coalesce(v_def, ''), '\s+', ' ', 'g'));

        IF v_norm NOT LIKE '%status%'
           OR v_norm NOT LIKE '%aguardando_finalizacao%'
           OR v_norm NOT LIKE '%concluida%'
           OR v_norm NOT LIKE '%resultado_operacional is not null%'
           OR v_norm NOT LIKE '%concluidolocalmente%'
           OR v_norm NOT LIKE '%confirmadosap%'
           OR v_norm NOT LIKE '%concluido_operacional_em is not null%' THEN
            RAISE EXCEPTION 'PREFLIGHT: ck_apontamento_resultado_operacional incompativel; exige status, AGUARDANDO_FINALIZACAO, CONCLUIDA, resultado_operacional IS NOT NULL, ConcluidoLocalmente, ConfirmadoSap e concluido_operacional_em IS NOT NULL -> %', v_def;
        END IF;
    END IF;
END $$;

-- ---------- Indices: tabela, colunas, unicidade e predicado parcial ----------
DO $$
DECLARE
    r record;
    v_def text;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_producao_configuracao','uq_operacao_config_ativa', true,
             'centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho', 'ativo'),
            ('operacao_producao_configuracao','ix_operacao_config_operacao', false, 'operacao_sap', 'ativo'),
            ('operacao_producao_apontamento','uq_apontamento_ativo_por_operacao', true,
             'numero_ordem, sequencia, operacao, suboperacao', 'AGUARDANDO_FINALIZACAO'),
            ('operacao_producao_apontamento','uq_apontamento_idempotency_inicio', true, 'idempotency_key', ''),
            ('operacao_producao_apontamento','uq_apontamento_idempotency_termino', true,
             'idempotency_key_termino', 'IS NOT NULL'),
            ('operacao_producao_apontamento','ix_apontamento_ordem', false, 'numero_ordem', ''),
            ('operacao_producao_apontamento','ix_apontamento_status', false, 'status', ''),
            ('operacao_producao_evento','ix_evento_apontamento', false, 'codigo_apontamento', ''),
            ('operacao_producao_evento','ix_evento_ordem', false, 'ordem_producao', ''),
            ('operacao_producao_evento','ix_evento_correlation', false, 'correlation_id', '')
        ) AS t(tabela, indice, unico, colunas, predicado)
    LOOP
        CONTINUE WHEN to_regclass('desenvolvimento.' || r.tabela) IS NULL;

        SELECT indexdef INTO v_def
          FROM pg_indexes
         WHERE schemaname='desenvolvimento' AND tablename=r.tabela AND indexname=r.indice;

        IF v_def IS NULL THEN
            RAISE EXCEPTION 'PREFLIGHT: indice % ausente em %.', r.indice, r.tabela;
        END IF;

        -- unicidade
        IF r.unico AND position('CREATE UNIQUE INDEX' in v_def) = 0 THEN
            RAISE EXCEPTION 'PREFLIGHT: indice % deveria ser UNIQUE -> %', r.indice, v_def;
        END IF;
        IF NOT r.unico AND position('CREATE UNIQUE INDEX' in v_def) > 0 THEN
            RAISE EXCEPTION 'PREFLIGHT: indice % NAO deveria ser UNIQUE -> %', r.indice, v_def;
        END IF;

        -- colunas
        IF position(r.colunas in v_def) = 0 THEN
            RAISE EXCEPTION 'PREFLIGHT: indice % com colunas divergentes (esperado "%") -> %',
                r.indice, r.colunas, v_def;
        END IF;

        -- predicado parcial
        IF r.predicado <> '' AND position(r.predicado in v_def) = 0 THEN
            RAISE EXCEPTION 'PREFLIGHT: indice % sem o predicado parcial esperado ("%") -> %',
                r.indice, r.predicado, v_def;
        END IF;
        IF r.predicado = '' AND position(' WHERE ' in v_def) > 0 THEN
            RAISE EXCEPTION 'PREFLIGHT: indice % nao deveria ser parcial -> %', r.indice, v_def;
        END IF;
    END LOOP;
END $$;

-- ---------- FK do evento -> apontamento (ON DELETE RESTRICT EXPLICITO) ----------
DO $$
DECLARE
    v_local_attnum smallint;
    v_ref_attnum smallint;
    v_qtd int;
    v_detalhe text;
BEGIN
    IF to_regclass('desenvolvimento.operacao_producao_evento') IS NULL THEN
        RETURN;
    END IF;

    SELECT attnum INTO v_local_attnum
      FROM pg_attribute
     WHERE attrelid = 'desenvolvimento.operacao_producao_evento'::regclass
       AND attname = 'codigo_apontamento'
       AND NOT attisdropped;

    SELECT attnum INTO v_ref_attnum
      FROM pg_attribute
     WHERE attrelid = 'desenvolvimento.operacao_producao_apontamento'::regclass
       AND attname = 'codigo_apontamento'
       AND NOT attisdropped;

    SELECT count(*) INTO v_qtd
      FROM pg_constraint pc
     WHERE pc.contype = 'f'
       AND pc.conrelid = 'desenvolvimento.operacao_producao_evento'::regclass
       AND pc.confrelid = 'desenvolvimento.operacao_producao_apontamento'::regclass
       AND pc.conkey = ARRAY[v_local_attnum]
       AND pc.confkey = ARRAY[v_ref_attnum]
       AND pc.confdeltype = 'r';

    IF v_qtd <> 1 THEN
        SELECT string_agg(
                   pc.conname || ' local=' || pc.conrelid::regclass::text
                   || ' ref=' || pc.confrelid::regclass::text
                   || ' conkey=' || pc.conkey::text
                   || ' confkey=' || pc.confkey::text
                   || ' confdeltype=' || pc.confdeltype,
                   '; ' ORDER BY pc.conname)
          INTO v_detalhe
          FROM pg_constraint pc
         WHERE pc.contype = 'f'
           AND pc.conrelid = 'desenvolvimento.operacao_producao_evento'::regclass;

        RAISE EXCEPTION 'PREFLIGHT: FK de operacao_producao_evento.codigo_apontamento deve referenciar operacao_producao_apontamento.codigo_apontamento com ON DELETE RESTRICT (confdeltype = r). Encontrado: %', coalesce(v_detalhe, 'NENHUMA FK');
    END IF;
END $$;

-- ---------- Duplicidades que quebrariam os indices unicos ----------
DO $$
BEGIN
    IF to_regclass('desenvolvimento.operacao_producao_apontamento') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1 FROM desenvolvimento.operacao_producao_apontamento
             WHERE status IN ('EM_ANDAMENTO','AGUARDANDO_FINALIZACAO')
             GROUP BY numero_ordem, sequencia, operacao, coalesce(suboperacao,'')
            HAVING count(*) > 1) THEN
            RAISE EXCEPTION 'PREFLIGHT: existe mais de um apontamento ATIVO para a mesma operacao - resolva antes do indice.';
        END IF;

        IF EXISTS (
            SELECT 1 FROM desenvolvimento.operacao_producao_apontamento
             GROUP BY idempotency_key HAVING count(*) > 1) THEN
            RAISE EXCEPTION 'PREFLIGHT: existem idempotency_key (inicio) duplicados - resolva antes do indice unico.';
        END IF;

        IF EXISTS (
            SELECT 1 FROM desenvolvimento.operacao_producao_apontamento
             WHERE idempotency_key_termino IS NOT NULL
             GROUP BY idempotency_key_termino HAVING count(*) > 1) THEN
            RAISE EXCEPTION 'PREFLIGHT: existem idempotency_key_termino duplicados - resolva antes do indice unico.';
        END IF;
    END IF;

    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NOT NULL THEN
        -- centro_trabalho FAZ PARTE da chave: a mesma operacao pode ter destino distinto por centro de trabalho.
        IF EXISTS (
            SELECT 1 FROM desenvolvimento.operacao_producao_configuracao
             WHERE ativo = true
             GROUP BY centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho
            HAVING count(*) > 1) THEN
            RAISE EXCEPTION 'PREFLIGHT: existe mais de uma configuracao ATIVA para a mesma combinacao - resolva antes do indice.';
        END IF;

        -- AMBIGUIDADE FUNCIONAL: duas configuracoes ATIVAS de mesma ESPECIFICIDADE para a mesma operacao
        -- fariam o servico bloquear em ConfiguracaoAmbigua. Detectar antes de aplicar.
        IF EXISTS (
            SELECT 1
              FROM desenvolvimento.operacao_producao_configuracao
             WHERE ativo = true
             GROUP BY operacao_sap,
                      (centro <> '')::int + (tipo_ordem <> '')::int + (sequencia_sap <> '')::int
                    + (suboperacao_sap <> '')::int + (centro_trabalho <> '')::int
            HAVING count(*) > 1) THEN
            RAISE WARNING 'PREFLIGHT: ha operacoes com mais de uma configuracao ATIVA de mesma especificidade. O inicio sera bloqueado por ConfiguracaoAmbigua ate o cadastro ser ajustado.';
        END IF;
    END IF;
END $$;

-- ---------- Estrutura de PERMISSAO esperada pela PROPOSTA ----------
DO $$
BEGIN
    IF to_regclass('desenvolvimento.permissao') IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT: tabela permissao inexistente - a PROPOSTA do 039 depende dela.';
    END IF;

    -- A PROPOSTA usa as colunas REAIS: modulo_permissao / rotina_permissao / acao_permissao /
    -- descricao_permissao / situacao_permissao. Abortar se o formato divergir.
    IF EXISTS (
        SELECT 1 FROM (VALUES
                ('modulo_permissao'), ('rotina_permissao'), ('acao_permissao'),
                ('descricao_permissao'), ('situacao_permissao')) AS req(c)
         WHERE NOT EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema='desenvolvimento' AND table_name='permissao' AND column_name=req.c)) THEN
        RAISE EXCEPTION 'PREFLIGHT: tabela permissao com colunas diferentes do esperado (modulo_permissao/rotina_permissao/acao_permissao/descricao_permissao/situacao_permissao).';
    END IF;

    IF to_regclass('desenvolvimento.perfil_permissao') IS NULL
       OR to_regclass('desenvolvimento.perfil_acesso') IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT: perfil_acesso/perfil_permissao inexistentes - o vinculo com o Administrador falharia.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM desenvolvimento.perfil_acesso WHERE nome_perfil_acesso = 'Administrador') THEN
        RAISE WARNING 'PREFLIGHT: perfil Administrador nao encontrado - o vinculo de permissoes nao sera criado.';
    END IF;
END $$;

-- ---------- VEREDITO FINAL ----------
-- So chega aqui quem passou por TODAS as checagens acima (qualquer incompatibilidade ja lancou EXCEPTION).
DO $$
DECLARE
    v_existentes int;
BEGIN
    SELECT count(*) INTO v_existentes
      FROM (VALUES
                ('operacao_producao_configuracao'),
                ('operacao_producao_apontamento'),
                ('operacao_producao_evento')) AS t(tabela)
     WHERE to_regclass('desenvolvimento.' || t.tabela) IS NOT NULL;

    IF v_existentes = 0 THEN
        RAISE NOTICE 'PREFLIGHT OK: PACOTE_NAO_APLICADO - pode seguir para a PROPOSTA apos revisao.';
    ELSE
        RAISE NOTICE 'PREFLIGHT OK: PACOTE_COMPLETAMENTE_APLICADO - estrutura existente e COMPATIVEL com o 039.';
    END IF;
END $$;
