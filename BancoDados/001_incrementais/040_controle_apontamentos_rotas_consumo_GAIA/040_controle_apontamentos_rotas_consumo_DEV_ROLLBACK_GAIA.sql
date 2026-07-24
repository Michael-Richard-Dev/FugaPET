\set ON_ERROR_STOP on
-- ============================================================
-- 040_controle_apontamentos_rotas_consumo_DEV_ROLLBACK_GAIA.sql
-- Remove somente as rotas globais introduzidas pelo pacote 040.
-- NAO executar no fluxo normal.
-- ============================================================
SET search_path TO desenvolvimento, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DELETE FROM desenvolvimento.operacao_producao_configuracao
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

\echo 'OK - ROLLBACK 040 DEV concluido'
