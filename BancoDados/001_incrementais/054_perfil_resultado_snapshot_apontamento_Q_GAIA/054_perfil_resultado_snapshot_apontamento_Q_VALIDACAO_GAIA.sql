\set ON_ERROR_STOP on
-- ============================================================
-- 054_perfil_resultado_snapshot_apontamento_Q_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente Q | Schema homologacao
-- Incremento 054 - Validacao pos-migracao (SOMENTE LEITURA)
-- Autoridade: FUGAPET-Q-CONTROLE-APONTAMENTOS-GOLDEN-PROFILE-MIGRATION-093C-M2B
--
-- VALIDACAO (SOMENTE LEITURA). NAO DML. NAO DDL. NAO SAP.
--   Adjudica separadamente (M2B):
--     MIGRATION_054_VALID                -> estrutura da coluna + ausencia de backfill.
--     RESULT_PROFILE_NORMALIZED_CONTRACT -> saude do contrato normalizado (independente).
--   ROW_COUNT_AFTER e emitido para confronto MANUAL com ROW_COUNT_BEFORE do PREFLIGHT
--   (sem estado inter-arquivo, sem tabela auxiliar — nao ha esse mecanismo no projeto).
-- ============================================================

DO $$
DECLARE
    v_data_type   text;
    v_is_nullable text;
    v_default     text;
    v_fk          bigint;
    v_idx         bigint;
    v_row_count_after       bigint;
    v_non_null_snapshot     bigint;
    v_profile_total         text := 'N/A';
    v_profile_active        text := 'N/A';
    v_profile_without_route text := 'N/A';
    v_def_without_profile   text := 'N/A';
    v_health_tables_ok      boolean := false;
    v_n1 bigint; v_n2 bigint; v_n3 bigint; v_n4 bigint;
    v_normalized text;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '054 VALIDACAO: database incorreto: %', current_database();
    END IF;

    -- -------- ESTRUTURA (MIGRATION_054_VALID) --------
    SELECT data_type, is_nullable, column_default
      INTO v_data_type, v_is_nullable, v_default
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'operacao_producao_apontamento'
       AND column_name = 'codigo_perfil_resultado';

    IF NOT FOUND THEN
        RAISE EXCEPTION '054 VALIDACAO: coluna codigo_perfil_resultado ausente. MIGRATION_054_VALID=NAO';
    END IF;
    IF v_data_type <> 'bigint' THEN
        RAISE EXCEPTION '054 VALIDACAO: tipo inesperado: % (esperado bigint). MIGRATION_054_VALID=NAO', v_data_type;
    END IF;
    IF v_is_nullable <> 'YES' THEN
        RAISE EXCEPTION '054 VALIDACAO: nullable inesperado: % (esperado YES). MIGRATION_054_VALID=NAO', v_is_nullable;
    END IF;
    IF v_default IS NOT NULL THEN
        RAISE EXCEPTION '054 VALIDACAO: default inesperado: % (esperado NONE). MIGRATION_054_VALID=NAO', v_default;
    END IF;

    SELECT count(*)
      INTO v_fk
      FROM information_schema.key_column_usage kcu
      JOIN information_schema.table_constraints tc
        ON tc.constraint_name = kcu.constraint_name
       AND tc.constraint_schema = kcu.constraint_schema
     WHERE kcu.table_schema = 'homologacao'
       AND kcu.table_name = 'operacao_producao_apontamento'
       AND kcu.column_name = 'codigo_perfil_resultado'
       AND tc.constraint_type = 'FOREIGN KEY';
    IF v_fk <> 0 THEN
        RAISE EXCEPTION '054 VALIDACAO: FK inesperada (esperado 0). MIGRATION_054_VALID=NAO';
    END IF;

    SELECT count(*)
      INTO v_idx
      FROM pg_index i
      JOIN pg_class c ON c.oid = i.indrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
      JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY (i.indkey)
     WHERE n.nspname = 'homologacao'
       AND c.relname = 'operacao_producao_apontamento'
       AND a.attname = 'codigo_perfil_resultado';
    IF v_idx <> 0 THEN
        RAISE EXCEPTION '054 VALIDACAO: index inesperado (esperado 0). MIGRATION_054_VALID=NAO';
    END IF;

    RAISE NOTICE 'COLUMN_AFTER=PRESENTE';
    RAISE NOTICE 'COLUMN_NAME=codigo_perfil_resultado';
    RAISE NOTICE 'DATA_TYPE=%', v_data_type;
    RAISE NOTICE 'IS_NULLABLE=%', v_is_nullable;
    RAISE NOTICE 'DEFAULT=NONE';
    RAISE NOTICE 'FK=NONE';
    RAISE NOTICE 'INDEX_SPECIFIC_COLUMN=NONE';

    -- -------- CARDINALIDADE + SEM BACKFILL --------
    EXECUTE 'SELECT count(*) FROM homologacao.operacao_producao_apontamento' INTO v_row_count_after;
    EXECUTE 'SELECT count(*) FROM homologacao.operacao_producao_apontamento WHERE codigo_perfil_resultado IS NOT NULL'
       INTO v_non_null_snapshot;

    RAISE NOTICE 'ROW_COUNT_AFTER=%', v_row_count_after;
    RAISE NOTICE 'NON_NULL_PROFILE_SNAPSHOT_ROWS=%', v_non_null_snapshot;

    IF v_non_null_snapshot <> 0 THEN
        RAISE EXCEPTION '054 VALIDACAO: backfill inesperado (NON_NULL_PROFILE_SNAPSHOT_ROWS=%, esperado 0). MIGRATION_054_VALID=NAO', v_non_null_snapshot;
    END IF;

    RAISE NOTICE 'MIGRATION_054_VALID=SIM';

    -- -------- SAUDE DO CONTRATO NORMALIZADO (independente) --------
    IF to_regclass('homologacao.operacao_resultado_perfil') IS NOT NULL
       AND to_regclass('homologacao.operacao_producao_configuracao') IS NOT NULL
       AND to_regclass('homologacao.operacao_resultado_definicao') IS NOT NULL THEN

        v_health_tables_ok := true;

        EXECUTE 'SELECT count(*) FROM homologacao.operacao_resultado_perfil' INTO v_n1;
        EXECUTE 'SELECT count(*) FROM homologacao.operacao_resultado_perfil WHERE ativo = true' INTO v_n2;
        EXECUTE $q$
            SELECT count(*) FROM homologacao.operacao_resultado_perfil p
             WHERE p.ativo = true
               AND NOT EXISTS (
                   SELECT 1 FROM homologacao.operacao_producao_configuracao c
                    WHERE c.codigo_configuracao = p.codigo_configuracao_rota
               )
        $q$ INTO v_n3;
        EXECUTE $q$
            SELECT count(*) FROM homologacao.operacao_resultado_definicao d
             WHERE d.ativo = true
               AND NOT EXISTS (
                   SELECT 1 FROM homologacao.operacao_resultado_perfil p
                    WHERE p.codigo_perfil_resultado = d.codigo_perfil_resultado
                      AND p.ativo = true
               )
        $q$ INTO v_n4;

        v_profile_total         := v_n1::text;
        v_profile_active        := v_n2::text;
        v_profile_without_route := v_n3::text;
        v_def_without_profile   := v_n4::text;
    END IF;

    RAISE NOTICE 'PROFILE_ROWS_TOTAL=%', v_profile_total;
    RAISE NOTICE 'PROFILE_ROWS_ACTIVE=% (qualquer cardinalidade legitima)', v_profile_active;
    RAISE NOTICE 'PROFILE_WITHOUT_ROUTE_COUNT=%', v_profile_without_route;
    RAISE NOTICE 'DEFINITION_WITHOUT_PROFILE_COUNT=%', v_def_without_profile;

    IF v_health_tables_ok AND v_n3 = 0 AND v_n4 = 0 THEN
        v_normalized := 'PASS';
    ELSE
        v_normalized := 'FAIL';
    END IF;
    RAISE NOTICE 'RESULT_PROFILE_NORMALIZED_CONTRACT=%', v_normalized;
END
$$;

\echo '054_VALIDACAO_CONCLUIDO'
\echo 'COLUMN=codigo_perfil_resultado'
\echo 'TYPE=bigint'
\echo 'NULLABLE=YES'
\echo 'DEFAULT=NONE'
\echo 'FK=NONE'
\echo 'INDEX_SPECIFIC_COLUMN=NONE'
\echo 'BACKFILL=NAO'
\echo 'SAP_WRITE=0'
