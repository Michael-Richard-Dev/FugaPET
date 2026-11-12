-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================
-- 035_modelo_etiqueta_rollback_HML.sql
-- Projeto FugaPET_HML  |  Schema: homologacao
-- Objeto: Cadastro de Modelo de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 035
-- Finalidade: rollback
--
-- EXECUCAO CONTROLADA.
-- Nao executar automaticamente.
-- Nao altera dados reais automaticamente.
-- Nao contem PRD.
-- ============================================================
\set ON_ERROR_STOP on
SET search_path TO homologacao;


BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- Remove somente objetos comprovadamente pertencentes ao pacote 035,
-- identificados por comentario contendo: FugaPET incremental 035 modelo_etiqueta
DO $$
DECLARE
    v_comment text;
    v_func_oid oid;
    v_has_dep boolean;
    v_con text;
BEGIN
    -- Trigger primeiro.
    SELECT obj_description(t.oid, 'pg_trigger') INTO v_comment
      FROM pg_trigger t
     WHERE t.tgname = 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
       AND t.tgrelid = to_regclass('homologacao.modelo_etiqueta')
       AND NOT t.tgisinternal
     LIMIT 1;
    IF v_comment LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
        EXECUTE 'DROP TRIGGER trg_bloqueia_inativar_modelo_com_etiqueta_ativa ON homologacao.modelo_etiqueta';
    ELSIF v_comment IS NOT NULL THEN
        RAISE NOTICE 'Trigger existente nao removido: sem comentario do pacote 035.';
    END IF;

    -- Constraints novas do pacote.
    FOREACH v_con IN ARRAY ARRAY['ck_modelo_etiqueta_nome_tamanho', 'ck_modelo_etiqueta_observacao_tamanho', 'ck_modelo_etiqueta_dpi_positivo', 'ck_modelo_etiqueta_largura_positiva', 'ck_modelo_etiqueta_altura_positiva']
    LOOP
        SELECT obj_description(c.oid, 'pg_constraint') INTO v_comment
          FROM pg_constraint c
         WHERE c.conrelid = to_regclass('homologacao.modelo_etiqueta')
           AND c.conname = v_con
         LIMIT 1;
        IF v_comment LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            EXECUTE format('ALTER TABLE homologacao.modelo_etiqueta DROP CONSTRAINT %I', v_con);
        ELSIF v_comment IS NOT NULL THEN
            RAISE NOTICE 'Constraint % nao removida: sem comentario do pacote 035.', v_con;
        END IF;
    END LOOP;

    -- Indice global do pacote.
    IF to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global') IS NOT NULL THEN
        v_comment := obj_description(to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global'), 'pg_class');
        IF v_comment LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            DROP INDEX homologacao.uq_modelo_etiqueta_nome_versao_global;
        ELSE
            RAISE NOTICE 'Indice global nao removido: sem comentario do pacote 035.';
        END IF;
    END IF;

    -- Funcao do pacote, somente se ainda nao houver dependencia.
    SELECT p.oid INTO v_func_oid
      FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname = 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa'
       AND p.pronargs = 0
     LIMIT 1;

    IF v_func_oid IS NOT NULL THEN
        v_comment := obj_description(v_func_oid, 'pg_proc');
        IF v_comment LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            SELECT EXISTS (
                SELECT 1
                  FROM pg_depend d
                 WHERE d.refobjid = v_func_oid
                   AND d.deptype IN ('n', 'a')
            ) INTO v_has_dep;
            IF NOT v_has_dep THEN
                EXECUTE 'DROP FUNCTION homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()';
            ELSE
                RAISE NOTICE 'Funcao nao removida: possui dependencias externas.';
            END IF;
        ELSE
            RAISE NOTICE 'Funcao existente nao removida: sem comentario do pacote 035.';
        END IF;
    END IF;
END $$;

-- Restaura o indice parcial antigo, se ausente.
DO $$
BEGIN
    IF to_regclass('homologacao.uq_modelo_etiqueta_nome_versao') IS NULL THEN
        CREATE UNIQUE INDEX uq_modelo_etiqueta_nome_versao
            ON homologacao.modelo_etiqueta (upper(trim(nome_modelo_etiqueta)), versao)
            WHERE situacao_modelo_etiqueta = true;
        COMMENT ON INDEX homologacao.uq_modelo_etiqueta_nome_versao IS 'FugaPET incremental 035 rollback restaurou indice parcial antigo';
    END IF;
END $$;

-- Nao remover, nao recriar e nao alterar os checks historicos:
-- ck_modelo_etiqueta_versao
-- ck_modelo_etiqueta_zpl_nao_vazio
-- ck_modelo_etiqueta_nome_nao_vazio

COMMIT;

SELECT 'OK - ROLLBACK 035 HML concluido sem alterar dados reais' AS resultado;
