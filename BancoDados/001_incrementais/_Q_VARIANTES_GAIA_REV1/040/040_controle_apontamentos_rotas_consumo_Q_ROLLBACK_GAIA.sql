\set ON_ERROR_STOP on
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
-- 040_controle_apontamentos_rotas_consumo_HML_ROLLBACK_GAIA.sql
-- Remove somente as rotas globais introduzidas pelo pacote 040.
-- NAO executar no fluxo normal.
-- ============================================================
SET search_path TO homologacao, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- Guarda de ambiente HML: impede execucao acidental em outro banco/schema.
DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION 'Ambiente incorreto para o pacote: banco atual=%, esperado=fuga_jales_local_homologacao_q_v1_2. Execucao bloqueada.', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'Ambiente incorreto para o pacote: schema homologacao inexistente. Execucao bloqueada.';
    END IF;
END $$;

DELETE FROM homologacao.operacao_producao_configuracao
 WHERE criado_por = 'FugaPET incremental 040 rotas consumo'
   AND COALESCE(centro, '') = ''
   AND COALESCE(tipo_ordem, '') = ''
   AND COALESCE(sequencia_sap, '') = ''
   AND COALESCE(suboperacao_sap, '') = ''
   AND COALESCE(centro_trabalho, '') = ''
   AND (
        (
            COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') = '50'
        AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
        AND tela_destino = 'ProcessoConsumoMaterialForm'
        AND exige_operacao_anterior IS FALSE
        )
     OR (
            COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') = '60'
        AND tipo_processo = 'CONSUMO_QUIMICOS'
        AND tela_destino = 'ProcessoConsumoMaterialForm'
        AND exige_operacao_anterior IS TRUE
        )
   );

COMMIT;

\echo 'OK - ROLLBACK 040 HML concluido'
