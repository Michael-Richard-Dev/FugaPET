\set ON_ERROR_STOP on

-- ============================================================================
-- FugaPET Q - Migration 055
-- OP1000164 - Rotas funcionais Plant+WorkCenter + perfis próprios
-- ROLLBACK TECNICO GAIA
--
-- EXECUCAO NAO AUTORIZADA.
-- OFFLINE PACKAGE. ESTE ARQUIVO NAO FOI EXECUTADO POR GAIA.
--
-- Remove SOMENTE objetos de dados criados pela 055 e restaura SOMENTE
-- centro_trabalho das configs 5,6,7,8,9,11.
-- Nao reseta sequences/identity.
-- Fail-closed se houver dados operacionais dependentes.
-- ============================================================================

BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '90s';
SET LOCAL search_path = pg_catalog, homologacao;

DO $rollback$
DECLARE
    v_count integer;
    v_affected integer;

    v_cfg_0040 bigint;
    v_cfg_0080 bigint;
    v_cfg_0110 bigint;

    v_profile_0040 bigint;
    v_profile_0080 bigint;
    v_profile_0110 bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '055 ROLLBACK: database incorreto: %', current_database();
    END IF;

    -- Estado pos-055 exato das seis configs existentes.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao c
      JOIN (
        VALUES
          (5::bigint,  '0050'::varchar, '3007045'::varchar, 'CONSUMO_MATERIA_PRIMA'::varchar, 'ProcessoConsumoMaterialForm'::varchar, false),
          (6::bigint,  '0060'::varchar, '3007046'::varchar, 'CONSUMO_QUIMICOS'::varchar,       'ProcessoConsumoMaterialForm'::varchar, true),
          (7::bigint,  '0070'::varchar, '3007047'::varchar, 'RESULTADO_APONTAMENTO'::varchar,  'ProcessoResultadoApontamentoForm'::varchar, true),
          (8::bigint,  '0090'::varchar, '3007001'::varchar, 'RESULTADO_APONTAMENTO'::varchar,  'ProcessoResultadoApontamentoForm'::varchar, true),
          (9::bigint,  '0100'::varchar, '3007005'::varchar, 'RESULTADO_APONTAMENTO'::varchar,  'ProcessoResultadoApontamentoForm'::varchar, true),
          (11::bigint, '0140'::varchar, '3007043'::varchar, 'SEMI_ACABADO'::varchar,           'ProcessoSemiAcabadoForm'::varchar, true)
      ) AS e(id, op, wc_new, tipo, tela, exige)
        ON c.codigo_configuracao=e.id
       AND c.operacao_sap=e.op
       AND c.centro_trabalho=e.wc_new
       AND c.tipo_processo=e.tipo
       AND c.tela_destino=e.tela
       AND c.exige_operacao_anterior=e.exige
     WHERE c.centro='3007'
       AND c.tipo_ordem=''
       AND c.sequencia_sap=''
       AND c.suboperacao_sap=''
       AND c.ativo IS TRUE;
    IF v_count <> 6 THEN
        RAISE EXCEPTION '055 ROLLBACK: seis configs existentes nao estao no estado pos-055 esperado; count=%', v_count;
    END IF;

    -- Localiza exatamente as 3 novas configs sem assumir IDs.
    SELECT codigo_configuracao INTO STRICT v_cfg_0040
      FROM homologacao.operacao_producao_configuracao
     WHERE centro='3007'
       AND centro_trabalho='3007015'
       AND operacao_sap='0040'
       AND tipo_processo='RESULTADO_APONTAMENTO'
       AND tela_destino='ProcessoResultadoApontamentoForm'
       AND ativo IS TRUE;

    SELECT codigo_configuracao INTO STRICT v_cfg_0080
      FROM homologacao.operacao_producao_configuracao
     WHERE centro='3007'
       AND centro_trabalho='3007002'
       AND operacao_sap='0080'
       AND tipo_processo='RESULTADO_APONTAMENTO'
       AND tela_destino='ProcessoResultadoApontamentoForm'
       AND ativo IS TRUE;

    SELECT codigo_configuracao INTO STRICT v_cfg_0110
      FROM homologacao.operacao_producao_configuracao
     WHERE centro='3007'
       AND centro_trabalho='3007018'
       AND operacao_sap='0110'
       AND tipo_processo='RESULTADO_APONTAMENTO'
       AND tela_destino='ProcessoResultadoApontamentoForm'
       AND ativo IS TRUE;

    -- Localiza exatamente um perfil occurrence=1 de cada nova rota.
    SELECT codigo_perfil_resultado INTO STRICT v_profile_0040
      FROM homologacao.operacao_resultado_perfil
     WHERE codigo_configuracao_rota=v_cfg_0040
       AND ordem_ocorrencia_workcenter=1
       AND ativo IS TRUE;

    SELECT codigo_perfil_resultado INTO STRICT v_profile_0080
      FROM homologacao.operacao_resultado_perfil
     WHERE codigo_configuracao_rota=v_cfg_0080
       AND ordem_ocorrencia_workcenter=1
       AND ativo IS TRUE;

    SELECT codigo_perfil_resultado INTO STRICT v_profile_0110
      FROM homologacao.operacao_resultado_perfil
     WHERE codigo_configuracao_rota=v_cfg_0110
       AND ordem_ocorrencia_workcenter=1
       AND ativo IS TRUE;

    -- Uma e somente uma definicao exata por novo perfil.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao d
     WHERE (
            (d.codigo_configuracao=v_cfg_0040 AND d.codigo_perfil_resultado=v_profile_0040)
         OR (d.codigo_configuracao=v_cfg_0080 AND d.codigo_perfil_resultado=v_profile_0080)
         OR (d.codigo_configuracao=v_cfg_0110 AND d.codigo_perfil_resultado=v_profile_0110)
     )
       AND d.codigo_item='TEMPO_OPERACAO'
       AND d.tipo='TEMPO'
       AND d.medida='MINUTO'
       AND d.referencia=5.000
       AND d.ordem_exibicao=1
       AND d.obrigatorio IS TRUE
       AND d.ativo IS TRUE;
    IF v_count <> 3 THEN
        RAISE EXCEPTION '055 ROLLBACK: novas definicoes nao totalizam 3; count=%', v_count;
    END IF;

    -- Fail-closed: apontamento ja usou snapshot de qualquer novo perfil.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_apontamento
     WHERE codigo_perfil_resultado IN (
        v_profile_0040, v_profile_0080, v_profile_0110
     );
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 ROLLBACK BLOQUEADO: apontamentos dependem dos novos perfis; count=%', v_count;
    END IF;

    -- Fail-closed: resultados operacionais ja referenciam as novas definicoes.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_registro_item ri
      JOIN homologacao.operacao_resultado_definicao d
        ON d.codigo_definicao=ri.codigo_definicao
     WHERE (
            (d.codigo_configuracao=v_cfg_0040 AND d.codigo_perfil_resultado=v_profile_0040)
         OR (d.codigo_configuracao=v_cfg_0080 AND d.codigo_perfil_resultado=v_profile_0080)
         OR (d.codigo_configuracao=v_cfg_0110 AND d.codigo_perfil_resultado=v_profile_0110)
     );
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 ROLLBACK BLOQUEADO: resultados dependem das novas definicoes; count=%', v_count;
    END IF;

    -- 1) remove exclusivamente as 3 definicoes da 055.
    DELETE FROM homologacao.operacao_resultado_definicao d
     WHERE (
            (d.codigo_configuracao=v_cfg_0040 AND d.codigo_perfil_resultado=v_profile_0040)
         OR (d.codigo_configuracao=v_cfg_0080 AND d.codigo_perfil_resultado=v_profile_0080)
         OR (d.codigo_configuracao=v_cfg_0110 AND d.codigo_perfil_resultado=v_profile_0110)
     )
       AND d.codigo_item='TEMPO_OPERACAO'
       AND d.tipo='TEMPO'
       AND d.medida='MINUTO'
       AND d.referencia=5.000
       AND d.ordem_exibicao=1
       AND d.obrigatorio IS TRUE
       AND d.ativo IS TRUE;

    GET DIAGNOSTICS v_affected=ROW_COUNT;
    IF v_affected <> 3 THEN
        RAISE EXCEPTION '055 ROLLBACK: definitions removidas esperado=3 obtido=%', v_affected;
    END IF;

    -- 2) remove exclusivamente os 3 perfis da 055.
    DELETE FROM homologacao.operacao_resultado_perfil
     WHERE codigo_perfil_resultado IN (
        v_profile_0040, v_profile_0080, v_profile_0110
     );

    GET DIAGNOSTICS v_affected=ROW_COUNT;
    IF v_affected <> 3 THEN
        RAISE EXCEPTION '055 ROLLBACK: profiles removidos esperado=3 obtido=%', v_affected;
    END IF;

    -- 3) remove exclusivamente as 3 configs novas.
    DELETE FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao IN (
        v_cfg_0040, v_cfg_0080, v_cfg_0110
     );

    GET DIAGNOSTICS v_affected=ROW_COUNT;
    IF v_affected <> 3 THEN
        RAISE EXCEPTION '055 ROLLBACK: configs novas removidas esperado=3 obtido=%', v_affected;
    END IF;

    -- 4) restaura SOMENTE centro_trabalho das seis configs preexistentes.
    UPDATE homologacao.operacao_producao_configuracao c
       SET centro_trabalho=m.wc_old
      FROM (
        VALUES
          (5::bigint,  '5'::varchar),
          (6::bigint,  '6'::varchar),
          (7::bigint,  '7'::varchar),
          (8::bigint,  '9'::varchar),
          (9::bigint,  '10'::varchar),
          (11::bigint, '100'::varchar)
      ) AS m(id,wc_old)
     WHERE c.codigo_configuracao=m.id;

    GET DIAGNOSTICS v_affected=ROW_COUNT;
    IF v_affected <> 6 THEN
        RAISE EXCEPTION '055 ROLLBACK: configs restauradas esperado=6 obtido=%', v_affected;
    END IF;

    -- Perfis 1..4/definition1 obrigatoriamente intactos.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil
     WHERE (codigo_perfil_resultado,codigo_configuracao_rota,ordem_ocorrencia_workcenter,ativo) IN (
       (1,7,1,true),(2,8,1,true),(3,9,1,true),(4,9,2,true)
     );
    IF v_count <> 4 THEN
        RAISE EXCEPTION '055 ROLLBACK: perfis 1..4 divergiram';
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao
     WHERE codigo_definicao=1
       AND codigo_configuracao=7
       AND codigo_perfil_resultado=1
       AND codigo_item='TEMPO_OPERACAO'
       AND tipo='TEMPO'
       AND medida='MINUTO'
       AND referencia=5.000
       AND ordem_exibicao=1
       AND obrigatorio IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN
        RAISE EXCEPTION '055 ROLLBACK: definition1/profile1 divergiu';
    END IF;

    -- Nenhum orphan.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      LEFT JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao=p.codigo_configuracao_rota
     WHERE c.codigo_configuracao IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 ROLLBACK: profile_without_route_count=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao d
      LEFT JOIN homologacao.operacao_resultado_perfil p
        ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
     WHERE d.codigo_perfil_resultado IS NOT NULL
       AND p.codigo_perfil_resultado IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 ROLLBACK: definition_without_profile_count=%', v_count;
    END IF;
END
$rollback$;

SELECT 'ROLLBACK_055_TECHNICAL_STATUS=PASS';
SELECT 'SEQUENCE_RESET=NAO';
SELECT 'ROLLBACK_EXECUTION_AUTHORIZED=NAO';

COMMIT;
