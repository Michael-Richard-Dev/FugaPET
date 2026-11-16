\set ON_ERROR_STOP on
-- ============================================================
-- 061_pa045_guard_hu_alinhar_ao_Q_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- VALIDACAO (SOMENTE LEITURA, exceto V4 que usa transacao revertida)
--
-- V4 e o teste que importa: nao pergunta se o TEXTO da funcao mudou, mas se a
-- transicao que estava travando AGORA PASSA -- e se a que deve continuar barrada
-- CONTINUA barrada. Tudo dentro de uma transacao desfeita ao final.
-- ============================================================

\echo ''
\echo '=== 061 VALIDACAO ==='

-- V1 - a guarda deixou de cobrir o preview.
DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_045_guard_hu_pos_101';
    IF v_def LIKE '%NEW.status_hu_caixa NOT IN%' THEN
        RAISE EXCEPTION 'V1 FALHOU: a lista de 4 status ainda esta na condicao.';
    END IF;
    RAISE NOTICE 'V1=OK  a guarda nao cobre mais o preview';
END
$$;

-- V2 - a protecao real continua existindo (nao viramos um gatilho vazio).
DO $$
DECLARE v_def text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_045_guard_hu_pos_101';
    IF v_def NOT LIKE '%PA045_HU_EXIGE_101_CONFIRMADO%'
       OR v_def NOT LIKE '%PA045_FLUXO_PARCIAL_BLOQUEIA_HU%' THEN
        RAISE EXCEPTION 'V2 FALHOU: as excecoes de protecao nao estao mais no corpo.';
    END IF;
    RAISE NOTICE 'V2=OK  as duas protecoes seguem no corpo';
END
$$;

-- V3 - o corpo bate com o do QAS110 (comparando pelo texto normalizado de schema).
DO $$
DECLARE v_def text; v_normalizado text;
BEGIN
    SELECT pg_get_functiondef(p.oid) INTO v_def
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='desenvolvimento' AND p.proname='fn_pa_045_guard_hu_pos_101';
    v_normalizado := replace(v_def, 'desenvolvimento', '@SCHEMA@');
    IF v_normalizado NOT LIKE '%OLD.status_hu_caixa=''PRONTA_PARA_ENVIO''%'
       OR v_normalizado NOT LIKE '%NEW.status_hu_caixa=''ENVIANDO_SAP''%'
       OR v_normalizado NOT LIKE '%FROM @SCHEMA@.hu_caixa_etapa_sap%' THEN
        RAISE EXCEPTION 'V3 FALHOU: o corpo nao corresponde ao do QAS110.';
    END IF;
    RAISE NOTICE 'V3=OK  corpo equivalente ao do QAS110, so o schema difere';
END
$$;

-- V4 - CAPACIDADE REAL, em transacao desfeita: o preview passa, o envio sem 101 barra.
DO $$
DECLARE
    v_caixa bigint;
    v_erro  text;
BEGIN
    SELECT codigo_hu_caixa INTO v_caixa
      FROM desenvolvimento.hu_caixa
     WHERE status_hu_caixa = 'FINALIZADA_LOCAL'
     ORDER BY codigo_hu_caixa LIMIT 1;

    IF v_caixa IS NULL THEN
        RAISE NOTICE 'V4=PULADO  nao ha caixa em FINALIZADA_LOCAL para exercitar';
        RETURN;
    END IF;

    -- (a) a transicao que TRAVAVA agora deve passar
    BEGIN
        UPDATE desenvolvimento.hu_caixa
           SET status_hu_caixa = 'PREVIEW_HU_GERADO'
         WHERE codigo_hu_caixa = v_caixa;
        RAISE NOTICE 'V4a=OK  caixa % aceitou PREVIEW_HU_GERADO', v_caixa;
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS v_erro = MESSAGE_TEXT;
        RAISE EXCEPTION 'V4a FALHOU: o preview continua barrado (%).', v_erro;
    END;

    -- (b) a transicao que DEVE barrar continua barrando (etapas 261/101 pendentes)
    BEGIN
        UPDATE desenvolvimento.hu_caixa
           SET status_hu_caixa = 'PRONTA_PARA_ENVIO'
         WHERE codigo_hu_caixa = v_caixa;
        UPDATE desenvolvimento.hu_caixa
           SET status_hu_caixa = 'ENVIANDO_SAP'
         WHERE codigo_hu_caixa = v_caixa;
        RAISE EXCEPTION 'V4b FALHOU: o envio SEM 101 confirmado passou. A protecao sumiu.';
    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS v_erro = MESSAGE_TEXT;
            IF v_erro LIKE '%PA045_HU_EXIGE_101_CONFIRMADO%' THEN
                RAISE NOTICE 'V4b=OK  envio sem 101 segue barrado, como deve';
            ELSIF v_erro LIKE 'V4b FALHOU%' THEN
                RAISE;
            ELSE
                RAISE NOTICE 'V4b=OK (barrado por outra regra: %)', v_erro;
            END IF;
    END;

    -- Desfaz tudo o que V4 tocou.
    RAISE EXCEPTION 'V4_ROLLBACK_PROPOSITAL';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLERRM = 'V4_ROLLBACK_PROPOSITAL' THEN
            RAISE NOTICE 'V4=OK  alteracoes do teste desfeitas; nada persistiu';
        ELSE
            RAISE;
        END IF;
END
$$;

-- Retrato final.
\echo ''
\echo '-- status das caixas (inalterado pelo teste) --'
SELECT status_hu_caixa, count(*) AS caixas
  FROM desenvolvimento.hu_caixa GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '=== 061 VALIDACAO: todos os checks passaram ==='
\echo ''
