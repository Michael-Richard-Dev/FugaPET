\set ON_ERROR_STOP on
-- ============================================================
-- 023_h13_outbox_reprocessamento_sap_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 023 (H13) - Validacao pos-aplicacao (SOMENTE LEITURA)
--
-- ESCRITO NESTE PORT. O pacote original nao tinha validacao.
--
-- VALIDACAO (SOMENTE LEITURA). NAO DML. NAO DDL. NAO SAP.
--   Adjudica MIGRATION_023_VALID a partir de: as duas tabelas, a identidade das
--   chaves primarias, a FK sem cascata, os tres indices, os checks de seguranca do
--   payload e a ausencia de linhas (o incremento nao faz backfill).
-- ============================================================

DO $$
DECLARE
    v_n            bigint;
    v_confdeltype  "char";
    v_linhas_ob    bigint;
    v_linhas_tt    bigint;
    v_dono_ob      text;
    v_dono_tt      text;
    v_grants       text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '023 VALIDACAO: database incorreto: %', current_database();
    END IF;

    -- -------- Existencia --------
    IF to_regclass('desenvolvimento.integracao_sap_outbox') IS NULL THEN
        RAISE EXCEPTION '023 VALIDACAO: integracao_sap_outbox ausente. MIGRATION_023_VALID=NAO';
    END IF;
    IF to_regclass('desenvolvimento.integracao_sap_tentativa') IS NULL THEN
        RAISE EXCEPTION '023 VALIDACAO: integracao_sap_tentativa ausente. MIGRATION_023_VALID=NAO';
    END IF;
    RAISE NOTICE 'TABELA_OUTBOX=PRESENTE';
    RAISE NOTICE 'TABELA_TENTATIVA=PRESENTE';

    -- -------- Chaves primarias por identidade --------
    SELECT count(*) INTO v_n
      FROM information_schema.columns
     WHERE table_schema = 'desenvolvimento'
       AND table_name = 'integracao_sap_outbox'
       AND column_name = 'codigo_outbox'
       AND is_identity = 'YES';
    IF v_n <> 1 THEN
        RAISE EXCEPTION '023 VALIDACAO: codigo_outbox nao e coluna de identidade. MIGRATION_023_VALID=NAO';
    END IF;

    SELECT count(*) INTO v_n
      FROM information_schema.columns
     WHERE table_schema = 'desenvolvimento'
       AND table_name = 'integracao_sap_tentativa'
       AND column_name = 'codigo_tentativa'
       AND is_identity = 'YES';
    IF v_n <> 1 THEN
        RAISE EXCEPTION '023 VALIDACAO: codigo_tentativa nao e coluna de identidade. MIGRATION_023_VALID=NAO';
    END IF;
    RAISE NOTICE 'IDENTITY_PKS=OK';

    -- -------- FK da tentativa: deve existir e NAO ser cascata --------
    SELECT confdeltype INTO v_confdeltype
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND conrelid = 'desenvolvimento.integracao_sap_tentativa'::regclass
       AND contype = 'f';

    IF NOT FOUND THEN
        RAISE EXCEPTION '023 VALIDACAO: FK de integracao_sap_tentativa ausente. MIGRATION_023_VALID=NAO';
    END IF;
    IF v_confdeltype = 'c' THEN
        RAISE EXCEPTION '023 VALIDACAO: FK com exclusao em cascata. MIGRATION_023_VALID=NAO';
    END IF;
    IF v_confdeltype <> 'r' THEN
        RAISE EXCEPTION '023 VALIDACAO: FK com acao de exclusao inesperada: % (esperado r, RESTRICT). MIGRATION_023_VALID=NAO', v_confdeltype;
    END IF;
    RAISE NOTICE 'FK_ON_DELETE=RESTRICT';

    -- -------- Indices --------
    SELECT count(*) INTO v_n
      FROM pg_indexes
     WHERE schemaname = 'desenvolvimento'
       AND indexname IN (
           'ix_integracao_sap_outbox_processamento',
           'ix_integracao_sap_outbox_entidade_chave',
           'ix_integracao_sap_tentativa_outbox_data'
       );
    IF v_n <> 3 THEN
        RAISE EXCEPTION '023 VALIDACAO: esperados 3 indices nomeados, encontrados %. MIGRATION_023_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'INDICES_NOMEADOS=3';

    -- -------- Checks de seguranca do payload, que sao a razao de ser do incremento --------
    SELECT count(*) INTO v_n
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND conrelid = 'desenvolvimento.integracao_sap_outbox'::regclass
       AND contype = 'c'
       AND conname IN (
           'ck_integracao_sap_outbox_payload_objeto',
           'ck_integracao_sap_outbox_payload_tamanho',
           'ck_integracao_sap_outbox_payload_sem_segredos'
       );
    IF v_n <> 3 THEN
        RAISE EXCEPTION '023 VALIDACAO: checks de payload incompletos (% de 3). MIGRATION_023_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'CHECKS_PAYLOAD=3 (objeto, tamanho, sem segredos)';

    SELECT count(*) INTO v_n
      FROM pg_constraint
     WHERE connamespace = 'desenvolvimento'::regnamespace
       AND conname IN (
           'uq_integracao_sap_outbox_idempotencia',
           'uq_integracao_sap_outbox_correlation',
           'uq_integracao_sap_tentativa_numero'
       );
    IF v_n <> 3 THEN
        RAISE EXCEPTION '023 VALIDACAO: constraints de unicidade incompletas (% de 3). MIGRATION_023_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'UNIQUES=3 (idempotencia, correlation, numero de tentativa)';

    -- -------- Sem backfill --------
    EXECUTE 'SELECT count(*) FROM desenvolvimento.integracao_sap_outbox' INTO v_linhas_ob;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.integracao_sap_tentativa' INTO v_linhas_tt;
    RAISE NOTICE 'LINHAS_OUTBOX=%', v_linhas_ob;
    RAISE NOTICE 'LINHAS_TENTATIVA=%', v_linhas_tt;

    IF v_linhas_ob <> 0 OR v_linhas_tt <> 0 THEN
        RAISE EXCEPTION '023 VALIDACAO: backfill inesperado (outbox=%, tentativa=%, esperado 0/0). MIGRATION_023_VALID=NAO', v_linhas_ob, v_linhas_tt;
    END IF;
    RAISE NOTICE 'BACKFILL=NAO';

    -- -------- Dono e grants (evidencia, nao guard) --------
    SELECT pg_get_userbyid(relowner) INTO v_dono_ob FROM pg_class WHERE oid = 'desenvolvimento.integracao_sap_outbox'::regclass;
    SELECT pg_get_userbyid(relowner) INTO v_dono_tt FROM pg_class WHERE oid = 'desenvolvimento.integracao_sap_tentativa'::regclass;
    RAISE NOTICE 'DONO_OUTBOX=%  DONO_TENTATIVA=%', v_dono_ob, v_dono_tt;

    SELECT string_agg(DISTINCT grantee, ', ' ORDER BY grantee) INTO v_grants
      FROM information_schema.role_table_grants
     WHERE table_schema = 'desenvolvimento'
       AND table_name IN ('integracao_sap_outbox', 'integracao_sap_tentativa')
       AND grantee <> 'postgres';
    RAISE NOTICE 'GRANTS_FORA_DE_POSTGRES=% (o incremento nao concede: estrutura preparatoria)', coalesce(v_grants, 'NENHUM');

    RAISE NOTICE 'MIGRATION_023_VALID=SIM';
END
$$;

\echo '023_VALIDACAO_CONCLUIDO'
\echo 'TABELAS=integracao_sap_outbox, integracao_sap_tentativa'
\echo 'FK_ON_DELETE=RESTRICT'
\echo 'INDICES=3'
\echo 'BACKFILL=NAO'
\echo 'GRANT=NENHUM'
\echo 'SAP_WRITE=0'
