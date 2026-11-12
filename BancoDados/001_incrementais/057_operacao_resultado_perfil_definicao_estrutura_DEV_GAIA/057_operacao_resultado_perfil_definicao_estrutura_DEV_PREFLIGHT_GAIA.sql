\set ON_ERROR_STOP on
-- ============================================================
-- 057_operacao_resultado_perfil_definicao_estrutura_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 057 - Estrutura de operacao_resultado_perfil e operacao_resultado_definicao
-- PREFLIGHT (SOMENTE LEITURA)
--
-- ORIGEM DESTE PACOTE. Este incremento nao e a traducao de um pacote do Q: ele NAO
-- EXISTE na linha Q. O DDL destas duas tabelas nao esta em nenhum pacote deste
-- repositorio -- nem DEV nem Q. Os pacotes 054 e 055 apenas as REFERENCIAM, e
-- portanto pressupoem uma migracao que nunca foi versionada aqui.
--
-- A estrutura abaixo foi EXTRAIDA DO BANCO DE HOMOLOGACAO DO Q
-- (fuga_jales_local_homologacao_q_v1_2, schema homologacao, PostgreSQL 18.4), com
-- autorizacao expressa do usuario, em transacao READ ONLY e com o papel de
-- aplicacao fugapet_q_app -- menor privilegio, somente leitura. A fonte foi o
-- catalogo: pg_attribute, pg_attrdef, pg_constraint via pg_get_constraintdef,
-- pg_indexes via indexdef, e information_schema.role_table_grants. Nada foi
-- inferido do uso nem reconstruido por engenharia reversa.
--
-- DIFERENCAS DECLARADAS em relacao ao objeto real do Q:
--   schema  homologacao      -> desenvolvimento
--   dono    fugapet_q_owner  -> postgres   (convencao do DEV: tudo pertence a postgres)
--   grant   fugapet_q_app    -> fugapet_dev_app, SELECT apenas, identico ao Q
--   As constraints NOT NULL que o catalogo do Q lista com contype 'n' sao recurso do
--   PostgreSQL 18 (not-null constraints nomeadas em pg_constraint). O DEV roda 15.5,
--   onde NOT NULL e atributo de coluna: foram escritas como NOT NULL de coluna, que e
--   o mesmo efeito. Nenhuma outra constraint, indice, default ou tipo foi alterado.
--   A ordem das colunas foi preservada, inclusive codigo_perfil_resultado por ultimo
--   em operacao_resultado_definicao, onde ela foi acrescentada depois no Q.
--
-- ESCOPO: SOMENTE ESTRUTURA. Nenhuma linha e copiada do Q. As duas tabelas nascem
--   vazias. A tabela operacao_resultado_definicao tem 1 linha no Q; essa linha e
--   configuracao de negocio daquele ambiente e NAO e portada aqui.
--
--   NAO DML. NAO DDL neste arquivo. NAO SAP.
-- ============================================================

BEGIN TRANSACTION READ ONLY;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_existentes text[];
    v_colisoes   text[];
    v_tipo_fk    text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '057 PREFLIGHT: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '057 PREFLIGHT: principal administrativo inesperado: %', current_user;
    END IF;

    IF to_regnamespace('desenvolvimento') IS NULL THEN
        RAISE EXCEPTION '057 PREFLIGHT: schema desenvolvimento ausente.';
    END IF;

    RAISE NOTICE 'DATABASE=%  SCHEMA=desenvolvimento  PRINCIPAL=%', current_database(), current_user;

    -- -------- As duas tabelas NAO devem existir --------
    SELECT array_agg(table_name ORDER BY table_name)
      INTO v_existentes
      FROM information_schema.tables
     WHERE table_schema = 'desenvolvimento'
       AND table_name IN ('operacao_resultado_perfil', 'operacao_resultado_definicao');

    IF COALESCE(array_length(v_existentes, 1), 0) > 0 THEN
        RAISE EXCEPTION
            '057 PREFLIGHT: objetos-alvo ja existentes: %. MIGRATION_PRECONDITION=NAO',
            array_to_string(v_existentes, ', ');
    END IF;
    RAISE NOTICE 'TARGET_TABLES_PRESENT=NAO';

    -- -------- A tabela referenciada pelas duas FKs deve existir, com o mesmo tipo --------
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NULL THEN
        RAISE EXCEPTION
            '057 PREFLIGHT: desenvolvimento.operacao_producao_configuracao ausente; as duas FKs nao podem ser criadas.';
    END IF;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_tipo_fk
      FROM pg_attribute a
     WHERE a.attrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
       AND a.attname = 'codigo_configuracao'
       AND NOT a.attisdropped;

    IF v_tipo_fk IS NULL THEN
        RAISE EXCEPTION '057 PREFLIGHT: coluna codigo_configuracao ausente na tabela referenciada.';
    END IF;

    IF v_tipo_fk <> 'bigint' THEN
        RAISE EXCEPTION
            '057 PREFLIGHT: codigo_configuracao e % no DEV, mas bigint no Q; as FKs nao casariam.',
            v_tipo_fk;
    END IF;
    RAISE NOTICE 'FK_REFERENCIADA=operacao_producao_configuracao(codigo_configuracao) bigint OK';

    -- -------- O papel de aplicacao deve existir, para o GRANT SELECT --------
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_dev_app') THEN
        RAISE EXCEPTION '057 PREFLIGHT: papel fugapet_dev_app ausente; o GRANT SELECT nao poderia ser aplicado.';
    END IF;
    RAISE NOTICE 'PAPEL_APLICACAO=fugapet_dev_app presente';

    -- -------- Colisao de nome de indice e de constraint --------
    SELECT array_agg(indexname ORDER BY indexname)
      INTO v_colisoes
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

    IF COALESCE(array_length(v_colisoes, 1), 0) > 0 THEN
        RAISE EXCEPTION
            '057 PREFLIGHT: nomes de indice ja em uso: %. MIGRATION_PRECONDITION=NAO',
            array_to_string(v_colisoes, ', ');
    END IF;
    RAISE NOTICE 'INDEX_NAME_COLLISION=NAO';

    SELECT array_agg(conname ORDER BY conname)
      INTO v_colisoes
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND conname IN (
           'ck_resultado_perfil_ordem_ocorrencia',
           'fk_resultado_perfil_configuracao_rota',
           'ck_resultado_definicao_codigo_item_nao_vazio',
           'ck_resultado_definicao_medida_nao_vazia',
           'ck_resultado_definicao_ordem_positiva',
           'ck_resultado_definicao_referencia_nao_negativa',
           'ck_resultado_definicao_tipo_nao_vazio',
           'fk_resultado_definicao_configuracao',
           'fk_resultado_definicao_perfil'
       );

    IF COALESCE(array_length(v_colisoes, 1), 0) > 0 THEN
        RAISE EXCEPTION
            '057 PREFLIGHT: nomes de constraint ja em uso: %. MIGRATION_PRECONDITION=NAO',
            array_to_string(v_colisoes, ', ');
    END IF;
    RAISE NOTICE 'CONSTRAINT_NAME_COLLISION=NAO';

    RAISE NOTICE 'MIGRATION_PRECONDITION=PASS';

    -- -------- Diagnostico: o 054 ja criou a coluna que aponta para o perfil --------
    RAISE NOTICE 'DEPENDENTE_054=operacao_producao_apontamento.codigo_perfil_resultado existe: %',
        EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema = 'desenvolvimento'
               AND table_name = 'operacao_producao_apontamento'
               AND column_name = 'codigo_perfil_resultado'
        );

    -- -------- Diagnostico: o 055 precisa de MUITO mais que estas tabelas --------
    RAISE NOTICE 'DEPENDENTE_055=este incremento cria a ESTRUTURA que o 055 exige, mas NAO a massa de';
    RAISE NOTICE '               configuracao (CONFIG5..CONFIG11, vinculos historicos, perfis 1..4) que';
    RAISE NOTICE '               o preflight do 055 tambem exige. Linhas hoje em';
    RAISE NOTICE '               operacao_producao_configuracao: %',
        (SELECT count(*) FROM desenvolvimento.operacao_producao_configuracao);
END
$$;

COMMIT;

\echo '057_PREFLIGHT_CONCLUIDO'
\echo 'TARGET_TABLES=desenvolvimento.operacao_resultado_perfil, desenvolvimento.operacao_resultado_definicao'
\echo 'TARGET_TABLES_PRESENT=NAO'
\echo 'LINHAS_COPIADAS_DO_Q=0'
\echo 'DML=0'
\echo 'DDL=0'
\echo 'SAP_WRITE=0'
