\set ON_ERROR_STOP on
-- ============================================================
-- 063_cancelamento_exige_sem_movimento_sap_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- ROLLBACK (ESCRITA) - transacao unica
--
-- Devolve a funcao ao corpo anterior ao 063: sem a clausula que olha as etapas.
--
-- EFEITO PRATICO, DITO SEM RODEIO: volta a ser possivel cancelar uma caixa cujo
-- 261 ja foi confirmado no SAP, criando consumo orfao e travando toda caixa
-- seguinte da OP em BLOCK_RECONCILIATION_REQUIRED -- exatamente o que aconteceu
-- com a caixa 1398 em 16/11/2026. Isto e a volta ao estado anterior, nao uma
-- avaria, mas nao reverta sem motivo.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '063 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    RAISE NOTICE 'GUARDAS=OK';
END
$$;

CREATE OR REPLACE FUNCTION desenvolvimento.fn_hu_caixa_cancelar(
    p_codigo bigint, p_usuario bigint, p_terminal text, p_motivo text)
 RETURNS boolean
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'desenvolvimento'
AS $function$
DECLARE v desenvolvimento.hu_caixa%ROWTYPE; v_fim timestamptz:=clock_timestamp();
BEGIN
    IF p_usuario IS NULL
       OR char_length(btrim(COALESCE(p_terminal,'')))=0
       OR char_length(btrim(COALESCE(p_motivo,'')))=0 THEN
        RAISE EXCEPTION 'CANCELAMENTO_EXIGE_USUARIO_TERMINAL_MOTIVO';
    END IF;
    UPDATE desenvolvimento.hu_caixa
       SET status_hu_caixa='CANCELADA',
           cancelado_em=v_fim,
           cancelado_por=p_usuario,
           motivo_cancelamento=btrim(p_motivo)
     WHERE codigo_hu_caixa=p_codigo
       AND terminal=upper(btrim(p_terminal))
       AND handling_unit_external_id IS NULL
       AND status_hu_caixa IN (
           'EM_PESAGEM','FINALIZADA_LOCAL','PREVIEW_HU_GERADO',
           'AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO',
           'ERRO_SAP','BLOQUEADA'
       )
    RETURNING * INTO v;
    IF NOT FOUND THEN RETURN false; END IF;
    PERFORM desenvolvimento.fn_hu_caixa_inserir_evento(
        v.codigo_hu_caixa,v.correlation_id,'CANCELAMENTO_LOCAL',
        'LOCAL://produto-acabado/hu-caixa/cancelar','LOCAL',0,NULL,
        NULL,NULL,NULL,NULL,'CANCELADO',NULL,'',NULL,
        v.motivo_cancelamento,false,v_fim,v_fim,p_usuario,p_terminal
    );
    RETURN true;
END
$function$;

DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_hu_caixa_cancelar';

    IF v_def LIKE '%CONFIRMADO_SAP%' THEN
        RAISE EXCEPTION '063 ROLLBACK ABORTADO: a clausula do 063 continua no corpo.';
    END IF;
    IF v_def NOT LIKE '%handling_unit_external_id IS NULL%' THEN
        RAISE EXCEPTION '063 ROLLBACK ABORTADO: a protecao de HU nao foi restaurada.';
    END IF;

    RAISE NOTICE 'SAIDA=OK  063 revertido; o cancelamento volta a ignorar as etapas SAP';
END
$$;

COMMIT;

\echo ''
\echo '=== 063 revertido. Cancelar caixa com 261 confirmado volta a ser possivel. ==='
\echo ''
