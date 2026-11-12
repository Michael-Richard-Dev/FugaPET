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
-- 030_consumo_material_persistencia_local_ROLLBACK_GAIA.sql
-- Projeto FugaPET - Schema homologacao
-- Desfaz a PROPOSTA 030. Remove as 3 tabelas (CASCADE remove indices/triggers/FKs).
-- AVISO: APAGA os dados de consumo local. Executar somente com autorizacao Gaia Dados.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

DROP TABLE IF EXISTS homologacao.consumo_material_pesagem CASCADE;
DROP TABLE IF EXISTS homologacao.consumo_material_item CASCADE;
DROP TABLE IF EXISTS homologacao.consumo_material_lancamento CASCADE;

COMMIT;

SELECT 'ROLLBACK 030 (homologacao) CONCLUIDO - TABELAS DE CONSUMO LOCAL REMOVIDAS' AS resultado_rollback;
