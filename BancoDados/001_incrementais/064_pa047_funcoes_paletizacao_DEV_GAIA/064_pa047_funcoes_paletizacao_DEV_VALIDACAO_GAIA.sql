\set ON_ERROR_STOP on
-- ============================================================
-- 064_pa047_funcoes_paletizacao_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- VALIDACAO (SOMENTE LEITURA - as duas funcoes sao STABLE e nao escrevem)
--
-- V3 e V4 sao os testes que importam: CHAMAM as funcoes com a HU real que a tela
-- usou e exigem que a caixa apareca. "A funcao existe" nao prova nada -- era
-- justamente a ausencia dela que gerava o 42883, e uma funcao que devolve vazio
-- deixaria a tela igualmente inutil, sem erro nenhum para diagnosticar.
-- ============================================================

\echo ''
\echo '=== 064 VALIDACAO ==='

-- V1 - as duas existem com a assinatura que o C# chama.
DO $$
BEGIN
    IF to_regprocedure('desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(text)') IS NULL THEN
        RAISE EXCEPTION 'V1 FALHOU: fn_pa_047_caixa_buscar_hu_exata(text) nao existe.';
    END IF;
    IF to_regprocedure('desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)') IS NULL THEN
        RAISE EXCEPTION 'V1 FALHOU: fn_pa_047_caixa_listar_intervalo_hu(text,text,text) nao existe.';
    END IF;
    RAISE NOTICE 'V1=OK  as duas assinaturas batem com a chamada da tela';
END
$$;

-- V2 - o papel da aplicacao consegue executar.
DO $$
BEGIN
    IF NOT has_function_privilege('fugapet_dev_app',
            'desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(text,text,text)', 'EXECUTE')
       OR NOT has_function_privilege('fugapet_dev_app',
            'desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(text)', 'EXECUTE') THEN
        RAISE EXCEPTION 'V2 FALHOU: fugapet_dev_app nao tem EXECUTE.';
    END IF;
    RAISE NOTICE 'V2=OK  fugapet_dev_app pode executar as duas';
END
$$;

-- V3 - CAPACIDADE: a listagem por intervalo devolve a caixa real.
DO $$
DECLARE
    v_hu       text;
    v_material text;
    v_achadas  integer;
BEGIN
    SELECT btrim(hc.handling_unit_external_id), btrim(hc.material::text)
      INTO v_hu, v_material
      FROM desenvolvimento.vw_hu_caixas_sem_palete v
      JOIN desenvolvimento.hu_caixa hc ON hc.codigo_hu_caixa=v.codigo_hu_caixa
     WHERE hc.situacao_hu_caixa IS TRUE
       AND hc.status_hu_caixa='CONFIRMADA_SAP'
       AND hc.handling_unit_external_id IS NOT NULL
       AND btrim(hc.handling_unit_external_id) ~ '^[0-9]+$'
     ORDER BY hc.codigo_hu_caixa DESC LIMIT 1;

    IF v_hu IS NULL THEN
        RAISE EXCEPTION 'V3 NAO PODE RODAR: nao ha caixa CONFIRMADA_SAP sem palete para exercitar. A funcao fica NAO COMPROVADA -- nao trate como sucesso.';
    END IF;

    SELECT count(*) INTO v_achadas
      FROM desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu(v_material, v_hu, v_hu);

    IF v_achadas <> 1 THEN
        RAISE EXCEPTION 'V3 FALHOU: intervalo [%,%] do material % devolveu % linha(s); esperado 1.',
                        v_hu, v_hu, v_material, v_achadas;
    END IF;

    RAISE NOTICE 'V3=OK  a HU % do material % foi encontrada pela listagem', v_hu, v_material;
END
$$;

-- V4 - CAPACIDADE: a busca exata devolve a MESMA caixa.
DO $$
DECLARE
    v_hu      text;
    v_achadas integer;
BEGIN
    SELECT btrim(hc.handling_unit_external_id)
      INTO v_hu
      FROM desenvolvimento.vw_hu_caixas_sem_palete v
      JOIN desenvolvimento.hu_caixa hc ON hc.codigo_hu_caixa=v.codigo_hu_caixa
     WHERE hc.situacao_hu_caixa IS TRUE
       AND hc.status_hu_caixa='CONFIRMADA_SAP'
       AND hc.handling_unit_external_id IS NOT NULL
       AND btrim(hc.handling_unit_external_id) ~ '^[0-9]+$'
     ORDER BY hc.codigo_hu_caixa DESC LIMIT 1;

    IF v_hu IS NULL THEN
        RAISE EXCEPTION 'V4 NAO PODE RODAR: nao ha caixa elegivel para exercitar a busca exata.';
    END IF;

    SELECT count(*) INTO v_achadas
      FROM desenvolvimento.fn_pa_047_caixa_buscar_hu_exata(v_hu);

    IF v_achadas <> 1 THEN
        RAISE EXCEPTION 'V4 FALHOU: a busca exata da HU % devolveu % linha(s); esperado 1.', v_hu, v_achadas;
    END IF;

    RAISE NOTICE 'V4=OK  a busca exata encontrou a HU %', v_hu;
END
$$;

-- V5 - as validacoes de entrada BARRAM de verdade, com o codigo certo.
-- Sem isto, uma funcao que aceitasse qualquer lixo passaria em V3 e V4.
DO $$
DECLARE v_erro text;
BEGIN
    BEGIN
        PERFORM * FROM desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu('', '1', '2');
        RAISE EXCEPTION 'V5 FALHOU: material vazio foi aceito.';
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS v_erro = MESSAGE_TEXT;
        IF v_erro <> 'PA047_MATERIAL_OBRIGATORIO' THEN RAISE; END IF;
    END;

    BEGIN
        PERFORM * FROM desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu('4000548', 'ABC', '2');
        RAISE EXCEPTION 'V5 FALHOU: HU inicial nao numerica foi aceita.';
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS v_erro = MESSAGE_TEXT;
        IF v_erro <> 'PA047_HU_INICIAL_INVALIDA' THEN RAISE; END IF;
    END;

    BEGIN
        PERFORM * FROM desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu('4000548', '99', '10');
        RAISE EXCEPTION 'V5 FALHOU: intervalo invertido foi aceito.';
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS v_erro = MESSAGE_TEXT;
        IF v_erro <> 'PA047_INTERVALO_HU_INVALIDO_INICIO_MAIOR_QUE_FIM' THEN RAISE; END IF;
    END;

    RAISE NOTICE 'V5=OK  entrada invalida barra com o codigo PA047_* correto';
END
$$;

-- V6 - HU inexistente devolve VAZIO, nao erro e nao linha de outra caixa.
DO $$
DECLARE v_achadas integer;
BEGIN
    SELECT count(*) INTO v_achadas
      FROM desenvolvimento.fn_pa_047_caixa_buscar_hu_exata('000000000000000');

    IF v_achadas <> 0 THEN
        RAISE EXCEPTION 'V6 FALHOU: HU inexistente devolveu % linha(s).', v_achadas;
    END IF;
    RAISE NOTICE 'V6=OK  HU inexistente devolve vazio';
END
$$;

-- Retrato: o que a tela vai enxergar como disponivel para paletizar.
\echo ''
\echo '-- caixas disponiveis para paletizacao --'
SELECT handling_unit_external_id AS hu, material, numero_ordem_producao AS op,
       lote, peso_liquido, status_hu_caixa
  FROM desenvolvimento.fn_pa_047_caixa_listar_intervalo_hu('4000548', '0', '999999999')
 ORDER BY hu;

\echo ''
\echo '=== 064 VALIDACAO: todos os checks passaram ==='
\echo ''
