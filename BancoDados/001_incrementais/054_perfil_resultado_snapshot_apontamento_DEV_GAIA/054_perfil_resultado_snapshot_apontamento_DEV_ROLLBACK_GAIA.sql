\set ON_ERROR_STOP on
-- PORT DEV: variante da linha DEV, derivada do pacote Q de mesmo numero. Unicas mudancas:
--   database fuga_jales_local_homologacao_q_v1_2 -> fuga_jales_local_desenvolvimento
--   schema   homologacao                         -> desenvolvimento
-- O delta de estrutura, as guardas fail-closed, os post-checks e a ausencia de
-- default/backfill/index/FK sao identicos ao original. O 054 nao tem GRANT algum,
-- portanto nada a omitir. O nome do gate Q no cabecalho e mantido de proposito: registra
-- a procedencia do pacote.
-- ============================================================
-- 054_perfil_resultado_snapshot_apontamento_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 054 - Rollback do snapshot do perfil de resultado
-- Autoridade: FUGAPET-Q-CONTROLE-APONTAMENTOS-GOLDEN-PROFILE-MIGRATION-093C-M1
--
-- ROLLBACK: remove a coluna codigo_perfil_resultado adicionada pelo 054.
-- ROLLBACK_EXECUTION = NAO_AUTORIZADO neste gate. Nao executar sem
-- autorizacao expressa. Fail-closed: a coluna deve existir e nao pode
-- conter valores nao-nulos (o 054 nao faz backfill; se houver dados,
-- eles pertencem a runtime posterior e o rollback ABORTA).
--   SEM DML de negocio. NAO SAP.
-- ============================================================

BEGIN;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_nao_nulos bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '054 ROLLBACK: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'desenvolvimento' THEN
        RAISE EXCEPTION '054 ROLLBACK: schema incorreto: %', current_schema();
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_perfil_resultado'
    ) THEN
        RAISE EXCEPTION '054 ROLLBACK: coluna codigo_perfil_resultado ausente (nada a reverter).';
    END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_apontamento WHERE codigo_perfil_resultado IS NOT NULL'
       INTO v_nao_nulos;

    IF v_nao_nulos <> 0 THEN
        RAISE EXCEPTION '054 ROLLBACK: % linhas com codigo_perfil_resultado nao-nulo; rollback bloqueado.', v_nao_nulos;
    END IF;
END
$$;

ALTER TABLE desenvolvimento.operacao_producao_apontamento
    DROP COLUMN codigo_perfil_resultado;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'operacao_producao_apontamento'
          AND column_name = 'codigo_perfil_resultado'
    ) THEN
        RAISE EXCEPTION '054 ROLLBACK POSTCHECK: coluna ainda presente.';
    END IF;
END
$$;

COMMIT;

\echo '054_ROLLBACK_CONCLUIDO'
\echo 'COLUMN_DROPPED=codigo_perfil_resultado'
\echo 'DML=0'
\echo 'SAP_WRITE=0'
