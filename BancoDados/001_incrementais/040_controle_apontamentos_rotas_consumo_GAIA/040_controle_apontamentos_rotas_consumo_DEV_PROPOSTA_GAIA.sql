\set ON_ERROR_STOP on
-- ============================================================
-- 040_controle_apontamentos_rotas_consumo_DEV_PROPOSTA_GAIA.sql
-- FugaPET_Dev | Schema: desenvolvimento
-- Rotas globais por operacao SAP:
--   0050 -> CONSUMO_MATERIA_PRIMA -> ProcessoConsumoMaterialForm | exige anterior local = false
--   0060 -> CONSUMO_QUIMICOS       -> ProcessoConsumoMaterialForm | exige anterior local = true
-- NAO executar automaticamente.
-- ============================================================
SET search_path TO desenvolvimento, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_colunas_ausentes text;
    v_total_global_0050 integer;
    v_total_global_0060 integer;
    v_global_conflitante integer;
    v_especifica_conflitante integer;
BEGIN
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NULL THEN
        RAISE EXCEPTION 'APLICACAO 040: tabela desenvolvimento.operacao_producao_configuracao nao existe. Aplique/valide o pacote 039 antes.';
    END IF;

    SELECT string_agg(c.coluna, ', ' ORDER BY c.coluna)
      INTO v_colunas_ausentes
      FROM (VALUES
            ('centro'),
            ('tipo_ordem'),
            ('sequencia_sap'),
            ('operacao_sap'),
            ('suboperacao_sap'),
            ('centro_trabalho'),
            ('tipo_processo'),
            ('tela_destino'),
            ('exige_operacao_anterior'),
            ('ativo'),
            ('criado_por')
      ) AS c(coluna)
      WHERE NOT EXISTS (
            SELECT 1
              FROM information_schema.columns ic
             WHERE ic.table_schema = 'desenvolvimento'
               AND ic.table_name = 'operacao_producao_configuracao'
               AND ic.column_name = c.coluna
      );

    IF v_colunas_ausentes IS NOT NULL THEN
        RAISE EXCEPTION 'APLICACAO 040: estrutura de operacao_producao_configuracao incompleta. Colunas ausentes: %.', v_colunas_ausentes;
    END IF;

    WITH cfg AS (
        SELECT
            COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
            tipo_processo,
            tela_destino,
            exige_operacao_anterior,
            COALESCE(centro, '') AS centro,
            COALESCE(tipo_ordem, '') AS tipo_ordem,
            COALESCE(sequencia_sap, '') AS sequencia_sap,
            COALESCE(suboperacao_sap, '') AS suboperacao_sap,
            COALESCE(centro_trabalho, '') AS centro_trabalho
        FROM desenvolvimento.operacao_producao_configuracao
        WHERE ativo = true
    ), globais AS (
        SELECT *
          FROM cfg
         WHERE operacao_normalizada IN ('50','60')
           AND centro = ''
           AND tipo_ordem = ''
           AND sequencia_sap = ''
           AND suboperacao_sap = ''
           AND centro_trabalho = ''
    )
    SELECT
        count(*) FILTER (WHERE operacao_normalizada = '50'),
        count(*) FILTER (WHERE operacao_normalizada = '60'),
        count(*) FILTER (
            WHERE NOT (
                    operacao_normalizada = '50'
                AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
                AND tela_destino = 'ProcessoConsumoMaterialForm'
                AND exige_operacao_anterior IS FALSE
            )
              AND NOT (
                    operacao_normalizada = '60'
                AND tipo_processo = 'CONSUMO_QUIMICOS'
                AND tela_destino = 'ProcessoConsumoMaterialForm'
                AND exige_operacao_anterior IS TRUE
            )
        )
      INTO v_total_global_0050, v_total_global_0060, v_global_conflitante
      FROM globais;

    IF v_total_global_0050 > 1 OR v_total_global_0060 > 1 THEN
        RAISE EXCEPTION 'APLICACAO 040: duplicidade de rota global normalizada encontrada. 0050=%, 0060=%. Proposta bloqueada.', v_total_global_0050, v_total_global_0060;
    END IF;

    IF v_global_conflitante > 0 THEN
        RAISE EXCEPTION 'APLICACAO 040: rota global ativa para 0050/0060 com tipo_processo, tela_destino ou exige_operacao_anterior conflitante. Proposta bloqueada.';
    END IF;

    WITH cfg AS (
        SELECT
            COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
            tipo_processo,
            tela_destino,
            exige_operacao_anterior,
            COALESCE(centro, '') AS centro,
            COALESCE(tipo_ordem, '') AS tipo_ordem,
            COALESCE(sequencia_sap, '') AS sequencia_sap,
            COALESCE(suboperacao_sap, '') AS suboperacao_sap,
            COALESCE(centro_trabalho, '') AS centro_trabalho
        FROM desenvolvimento.operacao_producao_configuracao
        WHERE ativo = true
    ), especificas AS (
        SELECT *
          FROM cfg
         WHERE operacao_normalizada IN ('50','60')
           AND NOT (
                    centro = ''
                AND tipo_ordem = ''
                AND sequencia_sap = ''
                AND suboperacao_sap = ''
                AND centro_trabalho = ''
           )
    )
    SELECT count(*)
      INTO v_especifica_conflitante
      FROM especificas
     WHERE NOT (
                operacao_normalizada = '50'
            AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
            AND tela_destino = 'ProcessoConsumoMaterialForm'
            AND exige_operacao_anterior IS FALSE
         )
       AND NOT (
                operacao_normalizada = '60'
            AND tipo_processo = 'CONSUMO_QUIMICOS'
            AND tela_destino = 'ProcessoConsumoMaterialForm'
            AND exige_operacao_anterior IS TRUE
         );

    IF v_especifica_conflitante > 0 THEN
        RAISE EXCEPTION 'APLICACAO 040: configuracao especifica ativa conflitante para 0050/0060 encontrada (%). Proposta bloqueada.', v_especifica_conflitante;
    END IF;
END $$;

INSERT INTO desenvolvimento.operacao_producao_configuracao
    (centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho,
     tipo_processo, tela_destino, exige_operacao_anterior, ativo, criado_por)
SELECT '', '', '', '0050', '', '',
       'CONSUMO_MATERIA_PRIMA', 'ProcessoConsumoMaterialForm', false, true, 'FugaPET incremental 040 rotas consumo'
WHERE NOT EXISTS (
    SELECT 1
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE ativo = true
       AND COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') = '50'
       AND COALESCE(centro, '') = ''
       AND COALESCE(tipo_ordem, '') = ''
       AND COALESCE(sequencia_sap, '') = ''
       AND COALESCE(suboperacao_sap, '') = ''
       AND COALESCE(centro_trabalho, '') = ''
       AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
       AND tela_destino = 'ProcessoConsumoMaterialForm'
       AND exige_operacao_anterior IS FALSE
);

INSERT INTO desenvolvimento.operacao_producao_configuracao
    (centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho,
     tipo_processo, tela_destino, exige_operacao_anterior, ativo, criado_por)
SELECT '', '', '', '0060', '', '',
       'CONSUMO_QUIMICOS', 'ProcessoConsumoMaterialForm', true, true, 'FugaPET incremental 040 rotas consumo'
WHERE NOT EXISTS (
    SELECT 1
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE ativo = true
       AND COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') = '60'
       AND COALESCE(centro, '') = ''
       AND COALESCE(tipo_ordem, '') = ''
       AND COALESCE(sequencia_sap, '') = ''
       AND COALESCE(suboperacao_sap, '') = ''
       AND COALESCE(centro_trabalho, '') = ''
       AND tipo_processo = 'CONSUMO_QUIMICOS'
       AND tela_destino = 'ProcessoConsumoMaterialForm'
       AND exige_operacao_anterior IS TRUE
);

COMMIT;

\echo 'OK - APLICACAO 040 DEV concluida'
