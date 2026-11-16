\set ON_ERROR_STOP on
-- ============================================================
-- 063_cancelamento_exige_sem_movimento_sap_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- VALIDACAO (SOMENTE LEITURA, exceto V3/V4 em subtransacao desfeita)
--
-- V3 e V4 sao os testes que importam, e vem em PAR de proposito:
--   V3 prova que a guarda BARRA o que precisa barrar;
--   V4 prova que ela NAO barrou o resto.
-- So V3 passaria com uma funcao que recusa tudo -- que e o jeito mais facil de
-- "passar no teste" e quebrar a operacao inteira.
--
-- Ambos chamam a FUNCAO DE VERDADE sobre caixas REAIS, dentro de subtransacoes
-- revertidas ao final. Nenhuma linha persiste.
-- ============================================================

\echo ''
\echo '=== 063 VALIDACAO ==='

-- V1 - a condicao nova esta no corpo.
DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_hu_caixa_cancelar';

    IF v_def NOT LIKE '%CONFIRMADO_SAP%' THEN
        RAISE EXCEPTION 'V1 FALHOU: a funcao nao consulta CONFIRMADO_SAP.';
    END IF;
    RAISE NOTICE 'V1=OK  a funcao passou a olhar as etapas SAP';
END
$$;

-- V2 - as protecoes antigas continuam.
DO $$
DECLARE v_def text; v_faltando text := '';
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_hu_caixa_cancelar';

    IF v_def NOT LIKE '%handling_unit_external_id IS NULL%' THEN v_faltando := v_faltando || 'guarda-HU '; END IF;
    IF v_def NOT LIKE '%CANCELAMENTO_EXIGE_USUARIO_TERMINAL_MOTIVO%' THEN v_faltando := v_faltando || 'validacao-entrada '; END IF;
    IF v_def NOT LIKE '%fn_hu_caixa_inserir_evento%' THEN v_faltando := v_faltando || 'auditoria '; END IF;

    IF length(v_faltando) > 0 THEN
        RAISE EXCEPTION 'V2 FALHOU: sumiram do corpo: %', v_faltando;
    END IF;
    RAISE NOTICE 'V2=OK  guarda de HU, validacao de entrada e auditoria intactas';
END
$$;

-- V3/V4 - CAPACIDADE, em PAR, sobre a MESMA caixa real.
--
-- Por que a mesma caixa nos dois: isola a variavel. A unica diferenca entre as
-- duas rodadas e a existencia da etapa CONFIRMADO_SAP. Se V4 cancela e V3 nao,
-- a causa so pode ser a clausula nova.
--
-- Por que a etapa e INSERIDA aqui em vez de reaproveitar a caixa 1398: ela esta
-- CANCELADA, e o gatilho fn_hu_caixa_validar_update recusa CANCELADA -> ERRO_SAP.
-- Forcar aquele estado exigiria desligar outra guarda, o que o teste nao faz.
DO $$
DECLARE
    v_caixa    bigint;
    v_terminal text;
    v_sem      boolean;
    v_com      boolean;
BEGIN
    SELECT c.codigo_hu_caixa, c.terminal
      INTO v_caixa, v_terminal
      FROM desenvolvimento.hu_caixa c
     WHERE c.handling_unit_external_id IS NULL
       AND c.status_hu_caixa IN (
           'EM_PESAGEM','FINALIZADA_LOCAL','PREVIEW_HU_GERADO',
           'AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO','ERRO_SAP','BLOQUEADA')
       AND NOT EXISTS (SELECT 1 FROM desenvolvimento.hu_caixa_etapa_sap e
                        WHERE e.codigo_hu_caixa = c.codigo_hu_caixa
                          AND e.status_etapa = 'CONFIRMADO_SAP')
     ORDER BY c.codigo_hu_caixa DESC LIMIT 1;

    IF v_caixa IS NULL THEN
        RAISE EXCEPTION 'V3/V4 NAO PODEM RODAR: nao ha caixa cancelavel para exercitar. A guarda fica NAO COMPROVADA -- nao trate como sucesso.';
    END IF;

    -- V4: SEM etapa confirmada, o cancelamento legitimo tem de continuar passando.
    BEGIN
        v_sem := desenvolvimento.fn_hu_caixa_cancelar(
            v_caixa, 1, v_terminal, 'VALIDACAO 063 - deve cancelar');
        RAISE EXCEPTION 'V4_DESFAZER';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLERRM <> 'V4_DESFAZER' THEN RAISE; END IF;
    END;

    -- V3: a MESMA caixa, agora com um 261 confirmado, tem de ser recusada.
    BEGIN
        -- A etapa e inserida COMPLETA porque as proprias restricoes da tabela exigem
        -- isso de um CONFIRMADO_SAP: claim, documento, ano de 4 digitos, confirmado_em,
        -- HTTP 2xx e origem do 261. Nenhuma restricao e afrouxada para o teste passar.
        INSERT INTO desenvolvimento.hu_caixa_etapa_sap
            (codigo_hu_caixa_etapa_sap, codigo_hu_caixa, etapa_sap, status_etapa,
             correlation_id, numero_tentativa, claim_token, claim_obtido_em, iniciado_em,
             material_document, material_document_year, confirmado_em, http_status,
             origem_261, usuario_operacao, terminal_operacao)
        VALUES (gen_random_uuid(), v_caixa, '261', 'CONFIRMADO_SAP',
                gen_random_uuid(), 1, gen_random_uuid(), now(), now(),
                '4900000000', '2026', now(), 201,
                'NOVA_ORQUESTRACAO', 1, v_terminal);

        v_com := desenvolvimento.fn_hu_caixa_cancelar(
            v_caixa, 1, v_terminal, 'VALIDACAO 063 - nao deve cancelar');

        RAISE EXCEPTION 'V3_DESFAZER';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLERRM <> 'V3_DESFAZER' THEN RAISE; END IF;
    END;

    IF v_sem IS DISTINCT FROM true THEN
        RAISE EXCEPTION 'V4 FALHOU: a caixa % SEM movimento SAP nao pode ser cancelada (resposta=%). A guarda passou do ponto e quebrou o cancelamento legitimo.', v_caixa, v_sem;
    END IF;
    RAISE NOTICE 'V4=OK  caixa % segue cancelavel quando nao ha etapa confirmada', v_caixa;

    IF v_com IS DISTINCT FROM false THEN
        RAISE EXCEPTION 'V3 FALHOU: a MESMA caixa % FOI cancelada mesmo com 261 CONFIRMADO_SAP (resposta=%).', v_caixa, v_com;
    END IF;
    RAISE NOTICE 'V3=OK  a mesma caixa % recusou o cancelamento com o 261 confirmado', v_caixa;
END
$$;

-- V5 - nada persistiu: o retrato de status tem de bater com o do PREFLIGHT.
\echo ''
\echo '-- distribuicao de status das caixas (inalterada pelos testes) --'
SELECT status_hu_caixa, count(*) AS caixas
  FROM desenvolvimento.hu_caixa GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '=== 063 VALIDACAO: todos os checks passaram ==='
\echo ''
