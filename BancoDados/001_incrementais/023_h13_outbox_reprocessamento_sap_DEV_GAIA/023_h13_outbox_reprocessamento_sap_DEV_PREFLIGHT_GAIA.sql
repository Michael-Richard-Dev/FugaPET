\set ON_ERROR_STOP on
-- ============================================================
-- 023_h13_outbox_reprocessamento_sap_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 023 (H13) - Outbox e reprocessamento controlado da integracao SAP
--
-- ESCRITO NESTE PORT. O pacote original e um .sql solto, com PROPOSTA apenas: nao
-- tinha PREFLIGHT, VALIDACAO nem ROLLBACK. Este arquivo nao existe na linha Q.
--
-- PREFLIGHT (SOMENTE LEITURA): confirma pre-condicoes e emite evidencia.
--   NAO altera dados nem estrutura. NAO DML. NAO DDL. NAO SAP.
-- ============================================================

DO $$
DECLARE
    v_existentes  text[];
    v_colisoes    text[];
    v_fk_cascata  bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '023 PREFLIGHT: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '023 PREFLIGHT: principal administrativo inesperado: %', current_user;
    END IF;

    IF to_regnamespace('desenvolvimento') IS NULL THEN
        RAISE EXCEPTION '023 PREFLIGHT: schema desenvolvimento ausente.';
    END IF;

    RAISE NOTICE 'DATABASE=%', current_database();
    RAISE NOTICE 'SCHEMA=desenvolvimento';
    RAISE NOTICE 'PRINCIPAL=%', current_user;

    -- -------- As duas tabelas NAO devem existir (mesma guarda da PROPOSTA) --------
    SELECT array_agg(table_name ORDER BY table_name)
      INTO v_existentes
      FROM information_schema.tables
     WHERE table_schema = 'desenvolvimento'
       AND table_name IN ('integracao_sap_outbox', 'integracao_sap_tentativa');

    IF COALESCE(array_length(v_existentes, 1), 0) > 0 THEN
        RAISE EXCEPTION
            '023 PREFLIGHT: objetos-alvo ja existentes: %. MIGRATION_PRECONDITION=NAO',
            array_to_string(v_existentes, ', ');
    END IF;

    RAISE NOTICE 'TARGET_TABLES_PRESENT=NAO';

    -- -------- Colisao de nome de indice, que a PROPOSTA criaria sem IF NOT EXISTS --------
    SELECT array_agg(indexname ORDER BY indexname)
      INTO v_colisoes
      FROM pg_indexes
     WHERE schemaname = 'desenvolvimento'
       AND indexname IN (
           'ix_integracao_sap_outbox_processamento',
           'ix_integracao_sap_outbox_entidade_chave',
           'ix_integracao_sap_tentativa_outbox_data'
       );

    IF COALESCE(array_length(v_colisoes, 1), 0) > 0 THEN
        RAISE EXCEPTION
            '023 PREFLIGHT: nomes de indice ja em uso: %. MIGRATION_PRECONDITION=NAO',
            array_to_string(v_colisoes, ', ');
    END IF;

    RAISE NOTICE 'INDEX_NAME_COLLISION=NAO';

    -- -------- Colisao de nome de constraint --------
    SELECT array_agg(conname ORDER BY conname)
      INTO v_colisoes
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND conname IN (
           'uq_integracao_sap_outbox_idempotencia',
           'uq_integracao_sap_outbox_correlation',
           'uq_integracao_sap_tentativa_numero'
       );

    IF COALESCE(array_length(v_colisoes, 1), 0) > 0 THEN
        RAISE EXCEPTION
            '023 PREFLIGHT: nomes de constraint ja em uso: %. MIGRATION_PRECONDITION=NAO',
            array_to_string(v_colisoes, ', ');
    END IF;

    RAISE NOTICE 'CONSTRAINT_NAME_COLLISION=NAO';
    RAISE NOTICE 'MIGRATION_PRECONDITION=PASS';

    -- -------- Diagnostico: o incremento e estrutura preparatoria, sem GRANT --------
    RAISE NOTICE 'GRANT_PREVISTO=NENHUM (o original nao concede; estrutura preparatoria)';
    RAISE NOTICE 'OWNER_PREVISTO=% (convencao do DEV: tudo pertence a postgres)', current_user;

    -- -------- Diagnostico do consumidor a jusante (052) --------
    RAISE NOTICE 'DEPENDENTE_052=o pacote 052 exige estas duas tabelas; hoje bloqueia por ausencia';

    SELECT count(*)
      INTO v_fk_cascata
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND contype = 'f'
       AND confdeltype = 'c';
    RAISE NOTICE 'FKS_COM_CASCADE_NO_SCHEMA=% (diagnostico; a PROPOSTA proibe criar uma nova)', v_fk_cascata;
END
$$;

\echo '023_PREFLIGHT_CONCLUIDO'
\echo 'TARGET_TABLES=desenvolvimento.integracao_sap_outbox, desenvolvimento.integracao_sap_tentativa'
\echo 'TARGET_TABLES_PRESENT=NAO'
\echo 'DML=0'
\echo 'DDL=0'
\echo 'SAP_WRITE=0'
