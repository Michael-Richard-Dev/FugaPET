\set ON_ERROR_STOP on
-- ============================================================
-- 054_perfil_resultado_snapshot_apontamento_Q_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente Q | Schema homologacao
-- Incremento 054 - Snapshot do perfil de resultado no apontamento
-- Autoridade: FUGAPET-Q-CONTROLE-APONTAMENTOS-GOLDEN-PROFILE-MIGRATION-093C-M2B
--             (contrato DB Gaia V6 - Gate 093C / M2A)
--
-- PREFLIGHT (SOMENTE LEITURA): confirma pre-condicoes e emite evidencia.
--   NAO altera dados nem estrutura. NAO DML. NAO DDL. NAO SAP.
--
-- Distincao semantica (M2B):
--   MIGRATION_PRECONDITION                -> fail-closed real p/ o ADD COLUMN nullable.
--   RESULT_PROFILE_NORMALIZED_CONTRACT_PRE -> DIAGNOSTICO; FALHA NAO bloqueia a PROPOSTA.
--   A cardinalidade de perfis ativos e diagnostico; NAO ha guard por "= 4".
-- ============================================================

DO $$
DECLARE
    v_row_count_before      bigint;
    v_profile_total         text := 'N/A';
    v_profile_active        text := 'N/A';
    v_profile_without_route text := 'N/A';
    v_def_without_profile   text := 'N/A';
    v_health_tables_ok      boolean := false;
    v_n1 bigint; v_n2 bigint; v_n3 bigint; v_n4 bigint;
    v_normalized_pre        text;
BEGIN
    -- -------- MIGRATION_PRECONDITION (fail-closed real) --------
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '054 PREFLIGHT: database incorreto: %', current_database();
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento') IS NULL THEN
        RAISE EXCEPTION '054 PREFLIGHT: tabela homologacao.operacao_producao_apontamento ausente.';
    END IF;

    -- A coluna-alvo NAO deve existir ainda (fail-closed contra estado divergente).
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_perfil_resultado'
    ) THEN
        RAISE EXCEPTION '054 PREFLIGHT: coluna codigo_perfil_resultado ja existe.';
    END IF;

    RAISE NOTICE 'MIGRATION_PRECONDITION=PASS';

    -- -------- ROW_COUNT_BEFORE (evidencia; N >= 0, sem cardinalidade fixa) --------
    EXECUTE 'SELECT count(*) FROM homologacao.operacao_producao_apontamento' INTO v_row_count_before;
    RAISE NOTICE 'ROW_COUNT_BEFORE=%', v_row_count_before;

    -- -------- Diagnostico do contrato normalizado (NAO bloqueia) --------
    IF to_regclass('homologacao.operacao_resultado_perfil') IS NOT NULL
       AND to_regclass('homologacao.operacao_producao_configuracao') IS NOT NULL
       AND to_regclass('homologacao.operacao_resultado_definicao') IS NOT NULL THEN

        v_health_tables_ok := true;

        EXECUTE 'SELECT count(*) FROM homologacao.operacao_resultado_perfil' INTO v_n1;
        EXECUTE 'SELECT count(*) FROM homologacao.operacao_resultado_perfil WHERE ativo = true' INTO v_n2;

        -- perfis ativos cuja rota (codigo_configuracao_rota) nao existe na configuracao
        EXECUTE $q$
            SELECT count(*) FROM homologacao.operacao_resultado_perfil p
             WHERE p.ativo = true
               AND NOT EXISTS (
                   SELECT 1 FROM homologacao.operacao_producao_configuracao c
                    WHERE c.codigo_configuracao = p.codigo_configuracao_rota
               )
        $q$ INTO v_n3;

        -- definicoes ativas sem perfil ativo correspondente
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
    RAISE NOTICE 'PROFILE_ROWS_ACTIVE=% (diagnostico; sem guard de cardinalidade)', v_profile_active;
    RAISE NOTICE 'PROFILE_WITHOUT_ROUTE_COUNT=%', v_profile_without_route;
    RAISE NOTICE 'DEFINITION_WITHOUT_PROFILE_COUNT=%', v_def_without_profile;

    IF v_health_tables_ok AND v_n3 = 0 AND v_n4 = 0 THEN
        v_normalized_pre := 'PASS';
    ELSE
        v_normalized_pre := 'FAIL';
    END IF;
    RAISE NOTICE 'RESULT_PROFILE_NORMALIZED_CONTRACT_PRE=% (diagnostico; NAO bloqueia a PROPOSTA 054)', v_normalized_pre;
END
$$;

\echo '054_PREFLIGHT_CONCLUIDO'
\echo 'TARGET_TABLE=homologacao.operacao_producao_apontamento'
\echo 'TARGET_COLUMN_PRESENT=NAO'
\echo 'DML=0'
\echo 'DDL=0'
\echo 'SAP_WRITE=0'
