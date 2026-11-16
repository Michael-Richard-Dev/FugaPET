\set ON_ERROR_STOP on
-- ============================================================
-- 063_cancelamento_exige_sem_movimento_sap_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 063 - fn_hu_caixa_cancelar recusa caixa com movimento SAP confirmado
-- PROPOSTA (ESCRITA) - transacao unica
--
-- Substitui o corpo da funcao. A UNICA diferenca em relacao ao corpo atual e a
-- clausula NOT EXISTS no WHERE do UPDATE. Nada mais muda: mesma assinatura,
-- mesmas validacoes de entrada, mesmos status elegiveis, mesmo evento de
-- auditoria, mesmo retorno booleano.
--
-- Leia o PREFLIGHT antes: ele mostra a lacuna e lista as caixas afetadas.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '063 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    IF to_regprocedure('desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text)') IS NULL THEN
        RAISE EXCEPTION '063 ABORTADO: a funcao nao existe; nada a substituir.';
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
       -- 063: movimento SAP ja confirmado (261 ou 101) exige ESTORNO antes de a
       -- caixa deixar de existir logicamente aqui. Cancelar por cima cria consumo
       -- orfao no SAP e trava toda caixa seguinte em BLOCK_RECONCILIATION_REQUIRED.
       -- A condicao vive DENTRO do UPDATE de proposito: verificacao previa teria
       -- corrida entre conferir e cancelar.
       AND NOT EXISTS (
           SELECT 1 FROM desenvolvimento.hu_caixa_etapa_sap e
            WHERE e.codigo_hu_caixa = p_codigo
              AND e.status_etapa = 'CONFIRMADO_SAP'
       )
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

-- ------------------------------------------------------------
-- GUARDA DE SAIDA
-- Nao basta "a funcao foi substituida". Exige que a condicao nova esteja la E que
-- as protecoes antigas tenham sobrevivido: um corpo que so cancelasse nada
-- passaria num teste de existencia e quebraria a operacao inteira.
-- ------------------------------------------------------------
DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_hu_caixa_cancelar';

    IF v_def NOT LIKE '%CONFIRMADO_SAP%' OR v_def NOT LIKE '%hu_caixa_etapa_sap%' THEN
        RAISE EXCEPTION '063 ABORTADO: a condicao nova nao foi aplicada.';
    END IF;

    IF v_def NOT LIKE '%handling_unit_external_id IS NULL%' THEN
        RAISE EXCEPTION '063 ABORTADO: a protecao de HU sumiu do corpo.';
    END IF;

    IF v_def NOT LIKE '%CANCELAMENTO_EXIGE_USUARIO_TERMINAL_MOTIVO%' THEN
        RAISE EXCEPTION '063 ABORTADO: a validacao de usuario/terminal/motivo sumiu.';
    END IF;

    IF v_def NOT LIKE '%fn_hu_caixa_inserir_evento%' THEN
        RAISE EXCEPTION '063 ABORTADO: a auditoria do cancelamento sumiu.';
    END IF;

    IF v_def NOT LIKE '%EM_PESAGEM%' OR v_def NOT LIKE '%PRONTA_PARA_ENVIO%' THEN
        RAISE EXCEPTION '063 ABORTADO: a lista de status elegiveis foi alterada.';
    END IF;

    IF v_def LIKE '%homologacao.%' THEN
        RAISE EXCEPTION '063 ABORTADO: sobrou referencia ao schema homologacao.';
    END IF;

    RAISE NOTICE 'SAIDA=OK  guarda nova presente e protecoes antigas intactas';
END
$$;

COMMIT;

\echo ''
\echo '=== 063 PROPOSTA aplicado. Rode o VALIDACAO. ==='
\echo ''
