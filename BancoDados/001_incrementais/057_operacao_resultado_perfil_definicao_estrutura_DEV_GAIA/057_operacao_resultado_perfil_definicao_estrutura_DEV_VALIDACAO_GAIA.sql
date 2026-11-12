\set ON_ERROR_STOP on
-- ============================================================
-- 057_operacao_resultado_perfil_definicao_estrutura_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 057 - Validacao pos-aplicacao (SOMENTE LEITURA)
--
-- Adjudica MIGRATION_057_VALID comparando o que ficou no DEV com o DDL extraido do
-- catalogo do Q: colunas uma a uma com tipo, nulidade, default e identity; as nove
-- constraints por nome e definicao; os nove indices por nome; as FKs em RESTRICT; o
-- dono; o grant; e a ausencia de linhas.
--
--   SOMENTE LEITURA. NAO DML. NAO DDL. NAO SAP.
--
-- JANELA DE VALIDADE: esta validacao exige as duas tabelas VAZIAS, porque o 057 nao
-- carrega dado nenhum. Logo ela so passa ANTES do incremento 058, que e quem povoa
-- as tabelas com a configuracao copiada do Q. Rodada depois do 058, ela falha em
-- "operacao_resultado_perfil tem 7 linhas, esperado 0" -- e isso e o comportamento
-- correto dela, nao um defeito. Para reconferir a ESTRUTURA depois do 058, use a
-- validacao do 058, que compara conteudo, ou compare os catalogos dos dois bancos.
-- ============================================================

BEGIN TRANSACTION READ ONLY;

SET LOCAL search_path TO desenvolvimento, pg_catalog;

DO $$
DECLARE
    r        record;
    v_n      bigint;
    v_falhas text := '';
    -- Contrato de colunas extraido do Q: tabela, coluna, posicao, tipo, notnull,
    -- identity ('d' = BY DEFAULT, '' = nenhuma), default (NULL = sem default).
    v_col text[][] := ARRAY[
        ['operacao_resultado_perfil','codigo_perfil_resultado','1','bigint','t','d',NULL],
        ['operacao_resultado_perfil','codigo_configuracao_rota','2','bigint','t','',NULL],
        ['operacao_resultado_perfil','ordem_ocorrencia_workcenter','3','integer','t','',NULL],
        ['operacao_resultado_perfil','ativo','4','boolean','t','','true'],
        ['operacao_resultado_perfil','criado_em','5','timestamp with time zone','t','','now()'],
        ['operacao_resultado_perfil','criado_por','6','character varying(80)','t','','CURRENT_USER'],
        ['operacao_resultado_perfil','alterado_em','7','timestamp with time zone','f','',NULL],
        ['operacao_resultado_perfil','alterado_por','8','character varying(80)','f','',NULL],
        ['operacao_resultado_definicao','codigo_definicao','1','bigint','t','d',NULL],
        ['operacao_resultado_definicao','codigo_configuracao','2','bigint','t','',NULL],
        ['operacao_resultado_definicao','codigo_item','3','character varying(50)','t','',NULL],
        ['operacao_resultado_definicao','tipo','4','character varying(40)','t','',NULL],
        ['operacao_resultado_definicao','medida','5','character varying(30)','t','',NULL],
        ['operacao_resultado_definicao','referencia','6','numeric(18,3)','t','',NULL],
        ['operacao_resultado_definicao','ordem_exibicao','7','integer','t','',NULL],
        ['operacao_resultado_definicao','obrigatorio','8','boolean','t','','true'],
        ['operacao_resultado_definicao','ativo','9','boolean','t','','true'],
        ['operacao_resultado_definicao','criado_em','10','timestamp with time zone','t','','now()'],
        ['operacao_resultado_definicao','criado_por','11','character varying(80)','t','',NULL],
        ['operacao_resultado_definicao','alterado_em','12','timestamp with time zone','f','',NULL],
        ['operacao_resultado_definicao','alterado_por','13','character varying(80)','f','',NULL],
        ['operacao_resultado_definicao','codigo_perfil_resultado','14','bigint','f','',NULL]
    ];
    i int;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '057 VALIDACAO: database incorreto: %', current_database();
    END IF;

    IF to_regclass('desenvolvimento.operacao_resultado_perfil') IS NULL
       OR to_regclass('desenvolvimento.operacao_resultado_definicao') IS NULL THEN
        RAISE EXCEPTION '057 VALIDACAO: tabela ausente. MIGRATION_057_VALID=NAO';
    END IF;
    RAISE NOTICE 'TABELAS=PRESENTES';

    -- -------- Colunas, uma a uma --------
    FOR i IN 1 .. array_length(v_col, 1) LOOP
        SELECT a.attnum::text AS pos,
               format_type(a.atttypid, a.atttypmod) AS tipo,
               CASE WHEN a.attnotnull THEN 't' ELSE 'f' END AS nn,
               CASE WHEN a.attidentity = '' THEN '' ELSE a.attidentity::text END AS ident,
               pg_get_expr(d.adbin, d.adrelid) AS padrao
          INTO r
          FROM pg_attribute a
          LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
         WHERE a.attrelid = ('desenvolvimento.' || v_col[i][1])::regclass
           AND a.attname = v_col[i][2]
           AND NOT a.attisdropped;

        IF NOT FOUND THEN
            v_falhas := v_falhas || format('  coluna ausente: %s.%s', v_col[i][1], v_col[i][2]) || chr(10);
            CONTINUE;
        END IF;

        IF r.pos <> v_col[i][3] THEN
            v_falhas := v_falhas || format('  %s.%s posicao %s, esperado %s', v_col[i][1], v_col[i][2], r.pos, v_col[i][3]) || chr(10);
        END IF;
        IF r.tipo <> v_col[i][4] THEN
            v_falhas := v_falhas || format('  %s.%s tipo %s, esperado %s', v_col[i][1], v_col[i][2], r.tipo, v_col[i][4]) || chr(10);
        END IF;
        IF r.nn <> v_col[i][5] THEN
            v_falhas := v_falhas || format('  %s.%s notnull %s, esperado %s', v_col[i][1], v_col[i][2], r.nn, v_col[i][5]) || chr(10);
        END IF;
        IF r.ident <> v_col[i][6] THEN
            v_falhas := v_falhas || format('  %s.%s identity "%s", esperado "%s"', v_col[i][1], v_col[i][2], r.ident, v_col[i][6]) || chr(10);
        END IF;
        -- Default: comparacao por prefixo, porque o servidor reescreve a expressao
        -- (now() continua now(); 'true' vira true; CURRENT_USER vira CURRENT_USER).
        IF v_col[i][7] IS NULL AND r.padrao IS NOT NULL THEN
            v_falhas := v_falhas || format('  %s.%s tem default "%s", esperado nenhum', v_col[i][1], v_col[i][2], r.padrao) || chr(10);
        ELSIF v_col[i][7] IS NOT NULL AND (r.padrao IS NULL OR position(v_col[i][7] in r.padrao) = 0) THEN
            v_falhas := v_falhas || format('  %s.%s default "%s", esperado conter "%s"', v_col[i][1], v_col[i][2], coalesce(r.padrao,'<nenhum>'), v_col[i][7]) || chr(10);
        END IF;
    END LOOP;

    IF v_falhas <> '' THEN
        RAISE EXCEPTION '057 VALIDACAO: contrato de colunas divergiu. MIGRATION_057_VALID=NAO%', chr(10) || v_falhas;
    END IF;
    RAISE NOTICE 'COLUNAS=22 conferidas (posicao, tipo, nulidade, identity, default)';

    -- -------- Constraints por nome --------
    SELECT count(*) INTO v_n
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND conname IN (
           'operacao_resultado_perfil_pkey',
           'ck_resultado_perfil_ordem_ocorrencia',
           'fk_resultado_perfil_configuracao_rota',
           'pk_operacao_resultado_definicao',
           'ck_resultado_definicao_codigo_item_nao_vazio',
           'ck_resultado_definicao_medida_nao_vazia',
           'ck_resultado_definicao_ordem_positiva',
           'ck_resultado_definicao_referencia_nao_negativa',
           'ck_resultado_definicao_tipo_nao_vazio',
           'fk_resultado_definicao_configuracao',
           'fk_resultado_definicao_perfil'
       );
    IF v_n <> 11 THEN
        RAISE EXCEPTION '057 VALIDACAO: esperadas 11 constraints nomeadas, encontradas %. MIGRATION_057_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'CONSTRAINTS=11 (2 PK, 6 CHECK, 3 FK)';

    -- -------- Indices por nome --------
    SELECT count(*) INTO v_n
      FROM pg_indexes
     WHERE schemaname = 'desenvolvimento'
       AND indexname IN (
           'operacao_resultado_perfil_pkey',
           'ix_resultado_perfil_rota_ativo',
           'uq_resultado_perfil_ativo_ocorrencia',
           'pk_operacao_resultado_definicao',
           'ix_resultado_definicao_config_ativo',
           'ix_resultado_definicao_perfil_ativo',
           'ix_resultado_definicao_tipo_medida',
           'uq_resultado_definicao_ativa_perfil_item',
           'uq_resultado_definicao_ativa_perfil_ordem'
       );
    IF v_n <> 9 THEN
        RAISE EXCEPTION '057 VALIDACAO: esperados 9 indices, encontrados %. MIGRATION_057_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'INDICES=9';

    -- -------- Os tres indices unicos PARCIAIS precisam da clausula WHERE --------
    SELECT count(*) INTO v_n
      FROM pg_indexes
     WHERE schemaname = 'desenvolvimento'
       AND indexname IN (
           'uq_resultado_perfil_ativo_ocorrencia',
           'uq_resultado_definicao_ativa_perfil_item',
           'uq_resultado_definicao_ativa_perfil_ordem'
       )
       AND indexdef LIKE '%WHERE%';
    IF v_n <> 3 THEN
        RAISE EXCEPTION
            '057 VALIDACAO: indice unico sem o filtro parcial: % de 3 com WHERE. Um unico TOTAL impediria historico inativo. MIGRATION_057_VALID=NAO',
            v_n;
    END IF;
    RAISE NOTICE 'UNICOS_PARCIAIS=3 (preservam historico inativo)';

    -- -------- FKs em RESTRICT --------
    SELECT count(*) INTO v_n
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND contype = 'f'
       AND conrelid IN (
           'desenvolvimento.operacao_resultado_perfil'::regclass,
           'desenvolvimento.operacao_resultado_definicao'::regclass
       )
       AND confdeltype = 'r';
    IF v_n <> 3 THEN
        RAISE EXCEPTION '057 VALIDACAO: esperadas 3 FKs em RESTRICT, encontradas %. MIGRATION_057_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'FK_ON_DELETE=RESTRICT (3)';

    -- -------- Sem dado copiado do Q --------
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_perfil' INTO v_n;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '057 VALIDACAO: operacao_resultado_perfil tem % linhas, esperado 0. MIGRATION_057_VALID=NAO', v_n;
    END IF;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_definicao' INTO v_n;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '057 VALIDACAO: operacao_resultado_definicao tem % linhas, esperado 0. MIGRATION_057_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'LINHAS=0/0  (nenhuma linha copiada do Q)';

    -- -------- Dono e grant --------
    FOR r IN
        SELECT c.relname AS t, pg_get_userbyid(c.relowner) AS dono
          FROM pg_class c
         WHERE c.oid IN (
               'desenvolvimento.operacao_resultado_perfil'::regclass,
               'desenvolvimento.operacao_resultado_definicao'::regclass)
    LOOP
        IF r.dono <> 'postgres' THEN
            RAISE EXCEPTION '057 VALIDACAO: % pertence a %, esperado postgres. MIGRATION_057_VALID=NAO', r.t, r.dono;
        END IF;
    END LOOP;
    RAISE NOTICE 'DONO=postgres (convencao do DEV)';

    SELECT count(*) INTO v_n
      FROM information_schema.role_table_grants
     WHERE table_schema = 'desenvolvimento'
       AND table_name IN ('operacao_resultado_perfil', 'operacao_resultado_definicao')
       AND grantee = 'fugapet_dev_app'
       AND privilege_type <> 'SELECT';
    IF v_n <> 0 THEN
        RAISE EXCEPTION '057 VALIDACAO: runtime tem % privilegio alem de SELECT. MIGRATION_057_VALID=NAO', v_n;
    END IF;

    IF NOT has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_perfil', 'SELECT')
       OR NOT has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_definicao', 'SELECT') THEN
        RAISE EXCEPTION '057 VALIDACAO: runtime sem SELECT. MIGRATION_057_VALID=NAO';
    END IF;
    RAISE NOTICE 'GRANT=SELECT apenas, para fugapet_dev_app';

    RAISE NOTICE 'MIGRATION_057_VALID=SIM';
END
$$;

COMMIT;

\echo '057_VALIDACAO_CONCLUIDO'
\echo 'COLUNAS=22  CONSTRAINTS=11  INDICES=9'
\echo 'FK_ON_DELETE=RESTRICT'
\echo 'LINHAS=0/0'
\echo 'GRANT=SELECT'
\echo 'SAP_WRITE=0'
