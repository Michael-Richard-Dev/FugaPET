\set ON_ERROR_STOP on

-- ============================================================================
-- FugaPET Q - Migration 055
-- OP1000164 - Rotas funcionais Plant+WorkCenter + perfis próprios
-- PROPOSTA GAIA
--
-- GATE:
-- FUGAPET-Q-CONTROLE-APONTAMENTOS-OP1000164-ROUTE-PROFILE-MIGRATION-DESIGN-094I
--
-- OFFLINE PACKAGE. ESTE ARQUIVO NAO FOI EXECUTADO POR GAIA.
-- DATA/CONFIG ONLY. DDL=0.
--
-- ESCOPO EXATO:
--   6 UPDATEs logicos de centro_trabalho em configs existentes;
--   3 INSERTs de configs;
--   3 INSERTs de perfis;
--   3 INSERTs de definicoes.
-- Nenhum outro delta e autorizado.
-- ============================================================================

BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '90s';
SET LOCAL search_path = pg_catalog, homologacao;

DO $migration$
DECLARE
    v_count integer;
    v_affected integer;

    v_cfg_0040 bigint;
    v_cfg_0080 bigint;
    v_cfg_0110 bigint;

    v_profile_0040 bigint;
    v_profile_0080 bigint;
    v_profile_0110 bigint;

    -- DECISAO FUNCIONAL CONGELADA 094I-A1:
    -- predecessor tecnico e sempre requerido para as tres novas rotas manuais.
    v_exige_0040 boolean := true;
    v_exige_0080 boolean := true;
    v_exige_0110 boolean := true;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '055 PROPOSTA: database incorreto: %', current_database();
    END IF;

    -- Recheck fail-closed imediatamente antes do write.
    IF NOT EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema='homologacao'
           AND table_name='operacao_producao_apontamento'
           AND column_name='codigo_perfil_resultado'
           AND data_type='bigint'
           AND is_nullable='YES'
           AND column_default IS NULL
    ) THEN
        RAISE EXCEPTION '055 PROPOSTA: storage Migration 054 ausente/divergente';
    END IF;

    -- Baseline exata das seis configs.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao c
      JOIN (
        VALUES
          (5::bigint,  '0050'::varchar, '5'::varchar,   'CONSUMO_MATERIA_PRIMA'::varchar, 'ProcessoConsumoMaterialForm'::varchar, false),
          (6::bigint,  '0060'::varchar, '6'::varchar,   'CONSUMO_QUIMICOS'::varchar,       'ProcessoConsumoMaterialForm'::varchar, true),
          (7::bigint,  '0070'::varchar, '7'::varchar,   'RESULTADO_APONTAMENTO'::varchar,  'ProcessoResultadoApontamentoForm'::varchar, true),
          (8::bigint,  '0090'::varchar, '9'::varchar,   'RESULTADO_APONTAMENTO'::varchar,  'ProcessoResultadoApontamentoForm'::varchar, true),
          (9::bigint,  '0100'::varchar, '10'::varchar,  'RESULTADO_APONTAMENTO'::varchar,  'ProcessoResultadoApontamentoForm'::varchar, true),
          (11::bigint, '0140'::varchar, '100'::varchar, 'SEMI_ACABADO'::varchar,           'ProcessoSemiAcabadoForm'::varchar, true)
      ) AS e(id, op, wc_old, tipo, tela, exige)
        ON c.codigo_configuracao = e.id
       AND c.operacao_sap = e.op
       AND c.centro_trabalho = e.wc_old
       AND c.tipo_processo = e.tipo
       AND c.tela_destino = e.tela
       AND c.exige_operacao_anterior = e.exige
     WHERE c.centro = '3007'
       AND c.tipo_ordem = ''
       AND c.sequencia_sap = ''
       AND c.suboperacao_sap = ''
       AND c.ativo IS TRUE;

    IF v_count <> 6 THEN
        RAISE EXCEPTION '055 PROPOSTA: baseline das seis configs divergiu; count=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE operacao_sap IN ('0040','0080','0110');
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PROPOSTA: nova operacao ja possui config; count=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE ativo IS TRUE
       AND centro='3007'
       AND centro_trabalho IN (
           '3007015','3007045','3007046','3007047','3007002',
           '3007001','3007005','3007018','3007043'
       );
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PROPOSTA: conflito literal Plant+WorkCenter; count=%', v_count;
    END IF;

    -- A) Corrige SOMENTE centro_trabalho das seis configs existentes.
    UPDATE homologacao.operacao_producao_configuracao c
       SET centro_trabalho = m.wc_new
      FROM (
        VALUES
          (5::bigint,  '3007045'::varchar),
          (6::bigint,  '3007046'::varchar),
          (7::bigint,  '3007047'::varchar),
          (8::bigint,  '3007001'::varchar),
          (9::bigint,  '3007005'::varchar),
          (11::bigint, '3007043'::varchar)
      ) AS m(id, wc_new)
     WHERE c.codigo_configuracao = m.id;

    GET DIAGNOSTICS v_affected = ROW_COUNT;
    IF v_affected <> 6 THEN
        RAISE EXCEPTION '055 PROPOSTA: CONFIG_UPDATES esperado=6 obtido=%', v_affected;
    END IF;

    -- B) Tres novas configs. PK omitida: geracao nativa da tabela.
    INSERT INTO homologacao.operacao_producao_configuracao
    (
        centro,
        tipo_ordem,
        sequencia_sap,
        operacao_sap,
        suboperacao_sap,
        centro_trabalho,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo,
        criado_por
    )
    VALUES
    (
        '3007','','','0040','','3007015',
        'RESULTADO_APONTAMENTO','ProcessoResultadoApontamentoForm',
        v_exige_0040,true,'MIGRATION_055_ROUTE_PROFILE_094I'
    )
    RETURNING codigo_configuracao INTO v_cfg_0040;

    INSERT INTO homologacao.operacao_producao_configuracao
    (
        centro,
        tipo_ordem,
        sequencia_sap,
        operacao_sap,
        suboperacao_sap,
        centro_trabalho,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo,
        criado_por
    )
    VALUES
    (
        '3007','','','0080','','3007002',
        'RESULTADO_APONTAMENTO','ProcessoResultadoApontamentoForm',
        v_exige_0080,true,'MIGRATION_055_ROUTE_PROFILE_094I'
    )
    RETURNING codigo_configuracao INTO v_cfg_0080;

    INSERT INTO homologacao.operacao_producao_configuracao
    (
        centro,
        tipo_ordem,
        sequencia_sap,
        operacao_sap,
        suboperacao_sap,
        centro_trabalho,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo,
        criado_por
    )
    VALUES
    (
        '3007','','','0110','','3007018',
        'RESULTADO_APONTAMENTO','ProcessoResultadoApontamentoForm',
        v_exige_0110,true,'MIGRATION_055_ROUTE_PROFILE_094I'
    )
    RETURNING codigo_configuracao INTO v_cfg_0110;

    IF v_cfg_0040 IS NULL OR v_cfg_0080 IS NULL OR v_cfg_0110 IS NULL
       OR v_cfg_0040 = v_cfg_0080
       OR v_cfg_0040 = v_cfg_0110
       OR v_cfg_0080 = v_cfg_0110 THEN
        RAISE EXCEPTION '055 PROPOSTA: IDs gerados das novas configs invalidos';
    END IF;

    -- C) Tres perfis próprios, occurrence=1. PK identity omitida.
    INSERT INTO homologacao.operacao_resultado_perfil
        (codigo_configuracao_rota, ordem_ocorrencia_workcenter, ativo)
    VALUES
        (v_cfg_0040, 1, true)
    RETURNING codigo_perfil_resultado INTO v_profile_0040;

    INSERT INTO homologacao.operacao_resultado_perfil
        (codigo_configuracao_rota, ordem_ocorrencia_workcenter, ativo)
    VALUES
        (v_cfg_0080, 1, true)
    RETURNING codigo_perfil_resultado INTO v_profile_0080;

    INSERT INTO homologacao.operacao_resultado_perfil
        (codigo_configuracao_rota, ordem_ocorrencia_workcenter, ativo)
    VALUES
        (v_cfg_0110, 1, true)
    RETURNING codigo_perfil_resultado INTO v_profile_0110;

    IF v_profile_0040 IS NULL OR v_profile_0080 IS NULL OR v_profile_0110 IS NULL
       OR v_profile_0040 = v_profile_0080
       OR v_profile_0040 = v_profile_0110
       OR v_profile_0080 = v_profile_0110 THEN
        RAISE EXCEPTION '055 PROPOSTA: IDs gerados dos novos perfis invalidos';
    END IF;

    -- D) Uma definicao própria por perfil. PK omitida: sequence/default nativo.
    INSERT INTO homologacao.operacao_resultado_definicao
    (
        codigo_configuracao,
        codigo_item,
        tipo,
        medida,
        referencia,
        ordem_exibicao,
        obrigatorio,
        ativo,
        criado_por,
        codigo_perfil_resultado
    )
    VALUES
      (v_cfg_0040, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true,
       'MIGRATION_055_ROUTE_PROFILE_094I', v_profile_0040),
      (v_cfg_0080, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true,
       'MIGRATION_055_ROUTE_PROFILE_094I', v_profile_0080),
      (v_cfg_0110, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true,
       'MIGRATION_055_ROUTE_PROFILE_094I', v_profile_0110);

    GET DIAGNOSTICS v_affected = ROW_COUNT;
    IF v_affected <> 3 THEN
        RAISE EXCEPTION '055 PROPOSTA: DEFINITION_INSERTS esperado=3 obtido=%', v_affected;
    END IF;

    -- Nenhum perfil occurrence=2 para 0110/0115.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil
     WHERE codigo_configuracao_rota = v_cfg_0110
       AND ordem_ocorrencia_workcenter <> 1;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PROPOSTA: perfil indevido adicional para rota 0110; count=%', v_count;
    END IF;

    -- Pos-check: nove rotas manuais literais, exatamente uma ativa por chave.
    SELECT count(*) INTO v_count
      FROM (
        SELECT centro, centro_trabalho
          FROM homologacao.operacao_producao_configuracao
         WHERE ativo IS TRUE
           AND (centro, centro_trabalho) IN (
             ('3007','3007015'),
             ('3007','3007045'),
             ('3007','3007046'),
             ('3007','3007047'),
             ('3007','3007002'),
             ('3007','3007001'),
             ('3007','3007005'),
             ('3007','3007018'),
             ('3007','3007043')
           )
         GROUP BY centro, centro_trabalho
        HAVING count(*) = 1
      ) x;
    IF v_count <> 9 THEN
        RAISE EXCEPTION '055 PROPOSTA: rotas manuais resolvidas esperado=9 obtido=%', v_count;
    END IF;

    -- Três novos perfis e uma definição exata por perfil.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao = p.codigo_configuracao_rota
     WHERE c.operacao_sap IN ('0040','0080','0110')
       AND p.ordem_ocorrencia_workcenter = 1
       AND p.ativo IS TRUE;
    IF v_count <> 3 THEN
        RAISE EXCEPTION '055 PROPOSTA: NEW_RESULT_PROFILES esperado=3 obtido=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao d
      JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao = d.codigo_configuracao
     WHERE c.operacao_sap IN ('0040','0080','0110')
       AND d.codigo_item='TEMPO_OPERACAO'
       AND d.tipo='TEMPO'
       AND d.medida='MINUTO'
       AND d.referencia=5.000
       AND d.ordem_exibicao=1
       AND d.obrigatorio IS TRUE
       AND d.ativo IS TRUE
       AND d.codigo_perfil_resultado IS NOT NULL;
    IF v_count <> 3 THEN
        RAISE EXCEPTION '055 PROPOSTA: NEW_RESULT_DEFINITIONS esperado=3 obtido=%', v_count;
    END IF;

    -- Perfis legados 1..4 seguem exatamente no mesmo parentesco/ocorrencia.
    IF EXISTS (
        SELECT codigo_perfil_resultado, codigo_configuracao_rota,
               ordem_ocorrencia_workcenter, ativo
          FROM homologacao.operacao_resultado_perfil
         WHERE codigo_perfil_resultado BETWEEN 1 AND 4
        EXCEPT
        VALUES
          (1::bigint,7::bigint,1::integer,true),
          (2::bigint,8::bigint,1::integer,true),
          (3::bigint,9::bigint,1::integer,true),
          (4::bigint,9::bigint,2::integer,true)
    ) THEN
        RAISE EXCEPTION '055 PROPOSTA: existing profiles 1..4 foram alterados';
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
        RAISE EXCEPTION '055 PROPOSTA: Profile1 definition foi alterada';
    END IF;

    -- Integridade normalizada.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      LEFT JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao = p.codigo_configuracao_rota
     WHERE c.codigo_configuracao IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PROPOSTA: profile_without_route_count=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao d
      LEFT JOIN homologacao.operacao_resultado_perfil p
        ON p.codigo_perfil_resultado = d.codigo_perfil_resultado
     WHERE d.codigo_perfil_resultado IS NOT NULL
       AND p.codigo_perfil_resultado IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PROPOSTA: definition_without_profile_count=%', v_count;
    END IF;

    RAISE NOTICE 'MIGRATION_055_IDS CONFIG0040=% CONFIG0080=% CONFIG0110=% PROFILE0040=% PROFILE0080=% PROFILE0110=%',
        v_cfg_0040, v_cfg_0080, v_cfg_0110,
        v_profile_0040, v_profile_0080, v_profile_0110;
END
$migration$;

SELECT 'CONFIG_UPDATES=6';
SELECT 'CONFIG_INSERTS=3';
SELECT 'PROFILE_INSERTS=3';
SELECT 'DEFINITION_INSERTS=3';
SELECT 'OTHER_UPDATES=0';
SELECT 'OTHER_INSERTS=0';
SELECT 'DELETES=0';
SELECT 'DDL=0';
SELECT 'MIGRATION_055_PROPOSAL_STATUS=PASS';

COMMIT;
