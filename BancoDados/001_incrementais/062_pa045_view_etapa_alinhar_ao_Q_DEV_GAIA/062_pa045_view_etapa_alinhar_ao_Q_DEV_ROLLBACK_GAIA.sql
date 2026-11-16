\set ON_ERROR_STOP on
-- ============================================================
-- 062_pa045_view_etapa_alinhar_ao_Q_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- ROLLBACK (ESCRITA) - transacao unica
--
-- Devolve a view ao formato que o DEV tinha antes do 062: sem a coluna "etapa".
--
-- EFEITO PRATICO, DITO SEM RODEIO: o ENVIAR CAIXA SAP volta a falhar com
-- "Vinculo 045 inconsistente (etapas=2; 261=nao; 101=nao)" e o pipeline
-- 261 -> 101 -> HU volta a ser inalcancavel. Isto e a volta ao estado anterior,
-- nao uma avaria -- mas e bom saber antes de rodar.
--
-- CREATE OR REPLACE VIEW nao REMOVE colunas, entao o rollback precisa de DROP +
-- CREATE. Por isso ele tambem perde os privilegios da view: o DROP os leva
-- junto. Confira no PREFLIGHT do 062 quais eram e reconceda-os depois.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '062 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    RAISE NOTICE 'GUARDAS=OK';
END
$$;

DROP VIEW IF EXISTS desenvolvimento.vw_pa_045_etapa_estado_runtime;

CREATE VIEW desenvolvimento.vw_pa_045_etapa_estado_runtime AS
SELECT codigo_hu_caixa_etapa_sap,
       codigo_hu_caixa,
       etapa_sap,
       status_etapa,
       correlation_id,
       numero_tentativa,
       claim_obtido_em          AS lease_obtido_em,
       claim_expira_em          AS lease_expira_em,
       recovery_claim_obtido_em AS recovery_lease_obtido_em,
       recovery_claim_expira_em AS recovery_lease_expira_em,
       material_document,
       material_document_year,
       http_status,
       iniciado_em,
       confirmado_em,
       falhou_em,
       indeterminado_em,
       request_sanitizado,
       response_sanitizado,
       erro_sanitizado,
       status_reconciliacao,
       reconciliado_em,
       pode_reprocessar,
       origem_261,
       referencia_origem_261,
       usuario_operacao,
       terminal_operacao,
       criado_em,
       atualizado_em
  FROM desenvolvimento.hu_caixa_etapa_sap;

DO $$
DECLARE v_total integer;
BEGIN
    SELECT count(*) INTO v_total
      FROM information_schema.columns
     WHERE table_schema='desenvolvimento' AND table_name='vw_pa_045_etapa_estado_runtime';

    IF v_total <> 29 THEN
        RAISE EXCEPTION '062 ROLLBACK ABORTADO: a view ficou com % colunas; esperado 29.', v_total;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='desenvolvimento'
                  AND table_name='vw_pa_045_etapa_estado_runtime'
                  AND column_name='etapa') THEN
        RAISE EXCEPTION '062 ROLLBACK ABORTADO: a coluna "etapa" continua la.';
    END IF;

    RAISE NOTICE 'SAIDA=OK  062 revertido; a view voltou as 29 colunas';
END
$$;

COMMIT;

\echo ''
\echo '=== 062 revertido. O ENVIAR CAIXA SAP volta a bloquear no vinculo 045. ==='
\echo '=== Reconceda os privilegios listados no PREFLIGHT: o DROP os removeu. ==='
\echo ''
