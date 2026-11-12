\set ON_ERROR_STOP on
-- ============================================================
-- 023_h13_outbox_reprocessamento_sap_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 023 (H13) - Rollback
--
-- ESCRITO NESTE PORT. O pacote original nao tinha script de rollback: apenas a nota
-- final "Rollback somente com autorizacao do Gaia e sem historico: DROP TABLE na ordem
-- tentativa -> outbox". Este arquivo formaliza exatamente essa nota, com guardas.
--
-- ROLLBACK_EXECUTION = NAO_AUTORIZADO por padrao. Nao executar sem autorizacao
-- expressa. Fail-closed: as duas tabelas devem existir e estar VAZIAS. Se houver
-- qualquer linha, ela pertence a runtime posterior e o rollback ABORTA — a nota do
-- original diz "sem historico", e apagar historico de integracao nao e reversivel.
--
-- O 052 depende destas duas tabelas. Reverter o 023 com o 052 aplicado deixa o banco
-- inconsistente: por isso a guarda abaixo recusa quando a funcao do 052 existe.
--   SEM DML de negocio. NAO SAP.
-- ============================================================

BEGIN;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_linhas_ob bigint;
    v_linhas_tt bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '023 ROLLBACK: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '023 ROLLBACK: principal administrativo inesperado: %', current_user;
    END IF;

    IF to_regclass('desenvolvimento.integracao_sap_outbox') IS NULL
       OR to_regclass('desenvolvimento.integracao_sap_tentativa') IS NULL THEN
        RAISE EXCEPTION '023 ROLLBACK: uma das tabelas ja esta ausente (nada a reverter com seguranca).';
    END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.integracao_sap_outbox' INTO v_linhas_ob;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.integracao_sap_tentativa' INTO v_linhas_tt;

    IF v_linhas_ob <> 0 OR v_linhas_tt <> 0 THEN
        RAISE EXCEPTION
            '023 ROLLBACK: existem linhas (outbox=%, tentativa=%); rollback bloqueado para nao apagar historico de integracao.',
            v_linhas_ob, v_linhas_tt;
    END IF;

    -- Dependente a jusante: a funcao criada pelo 052 le estas tabelas.
    IF EXISTS (
        SELECT 1
          FROM pg_proc p
          JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'desenvolvimento'
           AND p.proname = 'fn_entrada_produto_sap_guard_counts'
    ) THEN
        RAISE EXCEPTION
            '023 ROLLBACK: desenvolvimento.fn_entrada_produto_sap_guard_counts existe, logo o 052 esta aplicado e depende destas tabelas. Reverta o 052 primeiro.';
    END IF;
END
$$;

-- Ordem obrigatoria: a tentativa referencia a outbox.
DROP TABLE desenvolvimento.integracao_sap_tentativa;
DROP TABLE desenvolvimento.integracao_sap_outbox;

DO $$
BEGIN
    IF to_regclass('desenvolvimento.integracao_sap_outbox') IS NOT NULL
       OR to_regclass('desenvolvimento.integracao_sap_tentativa') IS NOT NULL THEN
        RAISE EXCEPTION '023 ROLLBACK POSTCHECK: tabela ainda presente.';
    END IF;
END
$$;

COMMIT;

\echo '023_ROLLBACK_CONCLUIDO'
\echo 'TABELAS_REMOVIDAS=integracao_sap_tentativa, integracao_sap_outbox'
\echo 'DML=0'
\echo 'SAP_WRITE=0'
