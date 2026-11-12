\set ON_ERROR_STOP on
-- ============================================================
-- 057_operacao_resultado_perfil_definicao_estrutura_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 057 - Rollback
--
-- ROLLBACK_EXECUTION = NAO_AUTORIZADO por padrao. Nao executar sem autorizacao
-- expressa.
--
-- Fail-closed. O rollback ABORTA se:
--   a) qualquer uma das duas tabelas tiver linhas -- elas pertencem a runtime ou a
--      configuracao posterior, e apagar configuracao de resultado de apontamento nao
--      e reversivel;
--   b) operacao_producao_apontamento tiver algum codigo_perfil_resultado preenchido --
--      haveria apontamento apontando para um perfil que este rollback destruiria;
--   c) o pacote 055 estiver aplicado, porque ele povoa estas duas tabelas.
--
-- Ordem obrigatoria: definicao referencia perfil, portanto definicao sai primeiro.
--   SEM DML de negocio. NAO SAP.
-- ============================================================

BEGIN;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_linhas_p   bigint;
    v_linhas_d   bigint;
    v_apontados  bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '057 ROLLBACK: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '057 ROLLBACK: principal administrativo inesperado: %', current_user;
    END IF;

    IF to_regclass('desenvolvimento.operacao_resultado_perfil') IS NULL
       OR to_regclass('desenvolvimento.operacao_resultado_definicao') IS NULL THEN
        RAISE EXCEPTION '057 ROLLBACK: uma das tabelas ja esta ausente (nada a reverter com seguranca).';
    END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_perfil' INTO v_linhas_p;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_definicao' INTO v_linhas_d;

    IF v_linhas_p <> 0 OR v_linhas_d <> 0 THEN
        RAISE EXCEPTION
            '057 ROLLBACK: existem linhas (perfil=%, definicao=%); rollback bloqueado para nao apagar configuracao de resultado de apontamento.',
            v_linhas_p, v_linhas_d;
    END IF;

    -- Dependente a montante: apontamento que referencie um perfil de resultado.
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'operacao_producao_apontamento'
           AND column_name = 'codigo_perfil_resultado'
    ) THEN
        EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_apontamento WHERE codigo_perfil_resultado IS NOT NULL'
          INTO v_apontados;
        IF v_apontados <> 0 THEN
            RAISE EXCEPTION
                '057 ROLLBACK: % apontamentos referenciam um perfil de resultado; rollback bloqueado.',
                v_apontados;
        END IF;
    END IF;

    RAISE NOTICE 'GUARDAS=OK  LINHAS=0/0  APONTAMENTOS_COM_PERFIL=0';
END
$$;

-- Ordem obrigatoria: a definicao referencia o perfil.
DROP TABLE desenvolvimento.operacao_resultado_definicao;
DROP TABLE desenvolvimento.operacao_resultado_perfil;

DO $$
BEGIN
    IF to_regclass('desenvolvimento.operacao_resultado_perfil') IS NOT NULL
       OR to_regclass('desenvolvimento.operacao_resultado_definicao') IS NOT NULL THEN
        RAISE EXCEPTION '057 ROLLBACK POSTCHECK: tabela ainda presente.';
    END IF;
    RAISE NOTICE 'ROLLBACK=CONCLUIDO';
END
$$;

COMMIT;

\echo '057_ROLLBACK_CONCLUIDO'
\echo 'TABELAS_REMOVIDAS=operacao_resultado_definicao, operacao_resultado_perfil'
\echo 'DML=0'
\echo 'SAP_WRITE=0'
