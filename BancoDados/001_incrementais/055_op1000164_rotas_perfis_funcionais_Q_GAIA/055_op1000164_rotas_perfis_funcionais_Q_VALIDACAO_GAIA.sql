\set ON_ERROR_STOP on

-- ============================================================================
-- FugaPET Q - Migration 055
-- OP1000164 - Rotas funcionais Plant+WorkCenter + perfis próprios
-- VALIDACAO GAIA
--
-- OFFLINE PACKAGE. ESTE ARQUIVO NAO FOI EXECUTADO POR GAIA.
-- READ-ONLY / FAIL-CLOSED.
-- ============================================================================

BEGIN TRANSACTION
ISOLATION LEVEL REPEATABLE READ
READ ONLY;

SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='90s';
SET LOCAL search_path=pg_catalog, homologacao;

DO $validation$
DECLARE
    v_count integer;

    -- DECISAO FUNCIONAL CONGELADA 094I-A1.
    v_exige_0040 boolean := true;
    v_exige_0080 boolean := true;
    v_exige_0110 boolean := true;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '055 VALIDACAO: database incorreto: %', current_database();
    END IF;

    -- Migration 054 continua materializada.
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
        RAISE EXCEPTION '055 VALIDACAO: storage 054 ausente/divergente';
    END IF;

    -- Nove rotas manuais: exatas por Plant+WorkCenter e sem ambiguidade.
    SELECT count(*) INTO v_count
      FROM (
        VALUES
          ('3007'::varchar,'3007015'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar),
          ('3007'::varchar,'3007045'::varchar,'CONSUMO_MATERIA_PRIMA'::varchar,'ProcessoConsumoMaterialForm'::varchar),
          ('3007'::varchar,'3007046'::varchar,'CONSUMO_QUIMICOS'::varchar,'ProcessoConsumoMaterialForm'::varchar),
          ('3007'::varchar,'3007047'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar),
          ('3007'::varchar,'3007002'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar),
          ('3007'::varchar,'3007001'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar),
          ('3007'::varchar,'3007005'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar),
          ('3007'::varchar,'3007018'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar),
          ('3007'::varchar,'3007043'::varchar,'SEMI_ACABADO'::varchar,'ProcessoSemiAcabadoForm'::varchar)
      ) AS expected(centro,wc,tipo,tela)
      JOIN LATERAL (
        SELECT count(*) AS cnt
          FROM homologacao.operacao_producao_configuracao c
         WHERE c.ativo IS TRUE
           AND c.centro=expected.centro
           AND c.centro_trabalho=expected.wc
           AND c.tipo_processo=expected.tipo
           AND c.tela_destino=expected.tela
      ) actual ON actual.cnt=1;
    IF v_count <> 9 THEN
        RAISE EXCEPTION '055 VALIDACAO: ACTIVE_MANUAL_ROUTES_TOTAL esperado=9 obtido=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM (
        SELECT centro,centro_trabalho
          FROM homologacao.operacao_producao_configuracao
         WHERE ativo IS TRUE
           AND centro='3007'
           AND centro_trabalho IN (
             '3007015','3007045','3007046','3007047','3007002',
             '3007001','3007005','3007018','3007043'
           )
         GROUP BY centro,centro_trabalho
        HAVING count(*)>1
      ) d;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 VALIDACAO: AMBIGUOUS_MANUAL_ROUTES=%', v_count;
    END IF;

    -- IDs existentes preservados e apenas WC funcionalizado.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao c
      JOIN (
        VALUES
          (5::bigint,  '0050'::varchar,'3007045'::varchar,'CONSUMO_MATERIA_PRIMA'::varchar,'ProcessoConsumoMaterialForm'::varchar,false),
          (6::bigint,  '0060'::varchar,'3007046'::varchar,'CONSUMO_QUIMICOS'::varchar,'ProcessoConsumoMaterialForm'::varchar,true),
          (7::bigint,  '0070'::varchar,'3007047'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar,true),
          (8::bigint,  '0090'::varchar,'3007001'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar,true),
          (9::bigint,  '0100'::varchar,'3007005'::varchar,'RESULTADO_APONTAMENTO'::varchar,'ProcessoResultadoApontamentoForm'::varchar,true),
          (11::bigint, '0140'::varchar,'3007043'::varchar,'SEMI_ACABADO'::varchar,'ProcessoSemiAcabadoForm'::varchar,true)
      ) AS e(id,op,wc,tipo,tela,exige)
        ON c.codigo_configuracao=e.id
       AND c.operacao_sap=e.op
       AND c.centro_trabalho=e.wc
       AND c.tipo_processo=e.tipo
       AND c.tela_destino=e.tela
       AND c.exige_operacao_anterior=e.exige
     WHERE c.centro='3007'
       AND c.tipo_ordem=''
       AND c.sequencia_sap=''
       AND c.suboperacao_sap=''
       AND c.ativo IS TRUE;
    IF v_count <> 6 THEN
        RAISE EXCEPTION '055 VALIDACAO: existing config IDs/attributes divergiram; count=%', v_count;
    END IF;

    -- Novas configs por provenance + rota literal + decisao funcional congelada.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao c
      JOIN (
        VALUES
          ('0040'::varchar,'3007015'::varchar,v_exige_0040),
          ('0080'::varchar,'3007002'::varchar,v_exige_0080),
          ('0110'::varchar,'3007018'::varchar,v_exige_0110)
      ) e(op,wc,exige)
        ON c.operacao_sap=e.op
       AND c.centro_trabalho=e.wc
       AND c.exige_operacao_anterior=e.exige
     WHERE c.ativo IS TRUE
       AND c.centro='3007'
       AND c.tipo_ordem=''
       AND c.sequencia_sap=''
       AND c.suboperacao_sap=''
       AND c.tipo_processo='RESULTADO_APONTAMENTO'
       AND c.tela_destino='ProcessoResultadoApontamentoForm';
    IF v_count <> 3 THEN
        RAISE EXCEPTION '055 VALIDACAO: new configs esperado=3 obtido=%', v_count;
    END IF;

    -- Exatamente 3 perfis novos, um occurrence=1 por nova config.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao=p.codigo_configuracao_rota
     WHERE c.operacao_sap IN ('0040','0080','0110')
       AND c.ativo IS TRUE
       AND p.ativo IS TRUE
       AND p.ordem_ocorrencia_workcenter=1;
    IF v_count <> 3 THEN
        RAISE EXCEPTION '055 VALIDACAO: NEW_RESULT_PROFILES esperado=3 obtido=%', v_count;
    END IF;

    -- Nenhum perfil adicional/occurrence=2 nas novas rotas, inclusive 0110.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao=p.codigo_configuracao_rota
     WHERE c.operacao_sap IN ('0040','0080','0110')
       AND p.ativo IS TRUE
       AND p.ordem_ocorrencia_workcenter <> 1;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 VALIDACAO: perfil adicional indevido nas novas rotas; count=%', v_count;
    END IF;

    -- Cada novo perfil possui exatamente uma definicao funcional exata.
    SELECT count(*) INTO v_count
      FROM (
        SELECT p.codigo_perfil_resultado
          FROM homologacao.operacao_resultado_perfil p
          JOIN homologacao.operacao_producao_configuracao c
            ON c.codigo_configuracao=p.codigo_configuracao_rota
          JOIN homologacao.operacao_resultado_definicao d
            ON d.codigo_perfil_resultado=p.codigo_perfil_resultado
           AND d.codigo_configuracao=c.codigo_configuracao
         WHERE c.operacao_sap IN ('0040','0080','0110')
           AND p.ordem_ocorrencia_workcenter=1
           AND p.ativo IS TRUE
           AND d.codigo_item='TEMPO_OPERACAO'
           AND d.tipo='TEMPO'
           AND d.medida='MINUTO'
           AND d.referencia=5.000
           AND d.ordem_exibicao=1
           AND d.obrigatorio IS TRUE
           AND d.ativo IS TRUE
         GROUP BY p.codigo_perfil_resultado
        HAVING count(*)=1
      ) d;
    IF v_count <> 3 THEN
        RAISE EXCEPTION '055 VALIDACAO: perfis novos com definicao exata esperado=3 obtido=%', v_count;
    END IF;

    -- Perfis 1..4 e Profile1 definition preservados.
    IF EXISTS (
        SELECT codigo_perfil_resultado,codigo_configuracao_rota,
               ordem_ocorrencia_workcenter,ativo
          FROM homologacao.operacao_resultado_perfil
         WHERE codigo_perfil_resultado BETWEEN 1 AND 4
        EXCEPT
        VALUES
          (1::bigint,7::bigint,1::integer,true),
          (2::bigint,8::bigint,1::integer,true),
          (3::bigint,9::bigint,1::integer,true),
          (4::bigint,9::bigint,2::integer,true)
    ) OR EXISTS (
        VALUES
          (1::bigint,7::bigint,1::integer,true),
          (2::bigint,8::bigint,1::integer,true),
          (3::bigint,9::bigint,1::integer,true),
          (4::bigint,9::bigint,2::integer,true)
        EXCEPT
        SELECT codigo_perfil_resultado,codigo_configuracao_rota,
               ordem_ocorrencia_workcenter,ativo
          FROM homologacao.operacao_resultado_perfil
         WHERE codigo_perfil_resultado BETWEEN 1 AND 4
    ) THEN
        RAISE EXCEPTION '055 VALIDACAO: EXISTING_PROFILE_1_4_CHANGED';
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
        RAISE EXCEPTION '055 VALIDACAO: definition1/profile1 alterada';
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao
     WHERE codigo_perfil_resultado IN (2,3,4);
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 VALIDACAO: profiles2..4 ganharam definicao indevida; count=%', v_count;
    END IF;

    -- Integridade.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      LEFT JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao=p.codigo_configuracao_rota
     WHERE c.codigo_configuracao IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 VALIDACAO: PROFILE_WITHOUT_ROUTE_COUNT=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao d
      LEFT JOIN homologacao.operacao_resultado_perfil p
        ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
     WHERE d.codigo_perfil_resultado IS NOT NULL
       AND p.codigo_perfil_resultado IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 VALIDACAO: DEFINITION_WITHOUT_PROFILE_COUNT=%', v_count;
    END IF;
END
$validation$;

-- Evidencia legivel / IDs gerados dinamicamente.
SELECT
    c.operacao_sap,
    c.codigo_configuracao,
    c.centro,
    c.centro_trabalho,
    c.tipo_processo,
    c.tela_destino,
    c.exige_operacao_anterior,
    c.ativo
FROM homologacao.operacao_producao_configuracao c
WHERE c.ativo IS TRUE
  AND (c.centro,c.centro_trabalho) IN (
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
ORDER BY c.centro_trabalho;

SELECT
    c.operacao_sap,
    c.codigo_configuracao,
    p.codigo_perfil_resultado,
    p.ordem_ocorrencia_workcenter,
    p.ativo,
    count(d.codigo_definicao) AS definition_count
FROM homologacao.operacao_producao_configuracao c
JOIN homologacao.operacao_resultado_perfil p
  ON p.codigo_configuracao_rota=c.codigo_configuracao
LEFT JOIN homologacao.operacao_resultado_definicao d
  ON d.codigo_perfil_resultado=p.codigo_perfil_resultado
WHERE c.operacao_sap IN ('0040','0080','0110')
GROUP BY
    c.operacao_sap,c.codigo_configuracao,p.codigo_perfil_resultado,
    p.ordem_ocorrencia_workcenter,p.ativo
ORDER BY c.operacao_sap;

SELECT 'ACTIVE_MANUAL_ROUTES_TOTAL=9';
SELECT 'AMBIGUOUS_MANUAL_ROUTES=0';
SELECT 'NEW_RESULT_PROFILES=3';
SELECT 'NEW_RESULT_DEFINITIONS=3';
SELECT 'EXISTING_PROFILE_1_4_CHANGED=NAO';
SELECT 'PROFILE_WITHOUT_ROUTE_COUNT=0';
SELECT 'DEFINITION_WITHOUT_PROFILE_COUNT=0';
SELECT 'VALIDATION_055_STATUS=PASS';

ROLLBACK;
