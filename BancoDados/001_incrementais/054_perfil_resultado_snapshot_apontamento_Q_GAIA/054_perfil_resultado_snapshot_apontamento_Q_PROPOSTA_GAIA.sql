\set ON_ERROR_STOP on
-- ============================================================
-- 054_perfil_resultado_snapshot_apontamento_Q_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente Q | Schema homologacao
-- Incremento 054 - Snapshot do perfil de resultado no apontamento
-- Autoridade: FUGAPET-Q-CONTROLE-APONTAMENTOS-GOLDEN-PROFILE-MIGRATION-093C-M1
--             (contrato DB Gaia V6 - Gate 093C)
--
-- OBJETIVO:
--   Adicionar a coluna codigo_perfil_resultado (bigint, NULL) em
--   homologacao.operacao_producao_apontamento como SNAPSHOT/storage do
--   perfil de resultado resolvido para a ocorrencia daquele apontamento.
--
-- CONTRATO NORMALIZADO (inalterado):
--   A resolucao autoritativa do perfil permanece em
--   homologacao.operacao_resultado_perfil, por
--   (codigo_configuracao_rota + ordem_ocorrencia_workcenter).
--   Esta coluna e APENAS snapshot; NAO e fonte de resolucao.
--   PROIBIDO adicionar operacao_producao_configuracao.codigo_perfil_resultado.
--
-- REGRAS:
--   SEM default. SEM backfill. SEM index. SEM FK (neste incremento).
--   SEM IF NOT EXISTS (fail-closed se o estado fisico divergir).
--   NAO tocar outra tabela / grants / owner / search_path persistente.
--   NAO DML. NAO SAP.
-- ============================================================

BEGIN;

SET LOCAL search_path TO homologacao, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- ------------------------------------------------------------
-- 0. FAIL-CLOSED ENVIRONMENT / BASELINE GUARDS
-- ------------------------------------------------------------
DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '054 ABORTADO: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION '054 ABORTADO: schema incorreto: %', current_schema();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '054 ABORTADO: principal administrativo inesperado: %', current_user;
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento') IS NULL THEN
        RAISE EXCEPTION '054 ABORTADO: tabela homologacao.operacao_producao_apontamento ausente.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'homologacao'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_perfil_resultado'
    ) THEN
        RAISE EXCEPTION '054 ABORTADO: coluna codigo_perfil_resultado ja existe (estado fisico divergente).';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- 1. DELTA AUTORIZADO (unico)
-- ------------------------------------------------------------
ALTER TABLE homologacao.operacao_producao_apontamento
    ADD COLUMN codigo_perfil_resultado bigint NULL;

-- ------------------------------------------------------------
-- 2. POST-CHECK (fail-closed)
-- ------------------------------------------------------------
DO $$
DECLARE
    v_data_type text;
    v_is_nullable text;
    v_default text;
BEGIN
    SELECT data_type, is_nullable, column_default
      INTO v_data_type, v_is_nullable, v_default
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'operacao_producao_apontamento'
       AND column_name = 'codigo_perfil_resultado';

    IF NOT FOUND THEN
        RAISE EXCEPTION '054 POSTCHECK: coluna nao criada.';
    END IF;

    IF v_data_type <> 'bigint' THEN
        RAISE EXCEPTION '054 POSTCHECK: tipo inesperado: % (esperado bigint)', v_data_type;
    END IF;

    IF v_is_nullable <> 'YES' THEN
        RAISE EXCEPTION '054 POSTCHECK: nullable inesperado: % (esperado YES)', v_is_nullable;
    END IF;

    IF v_default IS NOT NULL THEN
        RAISE EXCEPTION '054 POSTCHECK: default inesperado: % (esperado NONE)', v_default;
    END IF;
END
$$;

COMMIT;

\echo '054_PROPOSTA_CONCLUIDO'
\echo 'TABLE=homologacao.operacao_producao_apontamento'
\echo 'COLUMN_ADDED=codigo_perfil_resultado bigint NULL'
\echo 'DEFAULT=NONE'
\echo 'BACKFILL=NAO'
\echo 'INDEX=NAO'
\echo 'FK=NAO'
\echo 'DML=0'
\echo 'SAP_WRITE=0'
