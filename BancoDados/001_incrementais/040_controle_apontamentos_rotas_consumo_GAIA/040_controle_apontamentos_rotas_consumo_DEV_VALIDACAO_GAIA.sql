\set ON_ERROR_STOP on
-- ============================================================
-- 040_controle_apontamentos_rotas_consumo_DEV_VALIDACAO_GAIA.sql
-- Validacao transacional das rotas globais 0050/0060.
-- ============================================================
SET search_path TO desenvolvimento, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_colunas_ausentes text;
BEGIN
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO 040: tabela desenvolvimento.operacao_producao_configuracao nao existe.';
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
            ('ativo')
      ) AS c(coluna)
      WHERE NOT EXISTS (
            SELECT 1
              FROM information_schema.columns ic
             WHERE ic.table_schema = 'desenvolvimento'
               AND ic.table_name = 'operacao_producao_configuracao'
               AND ic.column_name = c.coluna
      );

    IF v_colunas_ausentes IS NOT NULL THEN
        RAISE EXCEPTION 'VALIDACAO 040: estrutura de operacao_producao_configuracao incompleta. Colunas ausentes: %.', v_colunas_ausentes;
    END IF;
END $$;

\echo 'VALIDACAO 040 - ROTAS GLOBAIS ATIVAS NORMALIZADAS PARA 0050/0060'
WITH cfg AS (
    SELECT
        COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
        centro,
        tipo_ordem,
        sequencia_sap,
        operacao_sap,
        suboperacao_sap,
        centro_trabalho,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo
    FROM desenvolvimento.operacao_producao_configuracao
    WHERE ativo = true
)
SELECT
    operacao_normalizada,
    operacao_sap,
    tipo_processo,
    tela_destino,
    exige_operacao_anterior,
    centro,
    tipo_ordem,
    sequencia_sap,
    suboperacao_sap,
    centro_trabalho
FROM cfg
WHERE operacao_normalizada IN ('50','60')
  AND COALESCE(centro, '') = ''
  AND COALESCE(tipo_ordem, '') = ''
  AND COALESCE(sequencia_sap, '') = ''
  AND COALESCE(suboperacao_sap, '') = ''
  AND COALESCE(centro_trabalho, '') = ''
ORDER BY operacao_normalizada, operacao_sap;

\echo 'VALIDACAO 040 - CONFIGURACOES ESPECIFICAS ATIVAS PARA 0050/0060'
WITH cfg AS (
    SELECT
        COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
        centro,
        tipo_ordem,
        sequencia_sap,
        operacao_sap,
        suboperacao_sap,
        centro_trabalho,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo
    FROM desenvolvimento.operacao_producao_configuracao
    WHERE ativo = true
)
SELECT
    operacao_normalizada,
    operacao_sap,
    tipo_processo,
    tela_destino,
    exige_operacao_anterior,
    centro,
    tipo_ordem,
    sequencia_sap,
    suboperacao_sap,
    centro_trabalho
FROM cfg
WHERE operacao_normalizada IN ('50','60')
  AND NOT (
        COALESCE(centro, '') = ''
    AND COALESCE(tipo_ordem, '') = ''
    AND COALESCE(sequencia_sap, '') = ''
    AND COALESCE(suboperacao_sap, '') = ''
    AND COALESCE(centro_trabalho, '') = ''
  )
ORDER BY operacao_normalizada, operacao_sap, centro, tipo_ordem, sequencia_sap, suboperacao_sap, centro_trabalho;

DO $$
DECLARE
    v_0050_global_correta integer;
    v_0060_global_correta integer;
    v_0050_global_total integer;
    v_0060_global_total integer;
    v_conflitos_globais integer;
    v_conflitos_especificos integer;
BEGIN
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
    SELECT
        count(*) FILTER (WHERE operacao_normalizada = '50'),
        count(*) FILTER (WHERE operacao_normalizada = '60'),
        count(*) FILTER (
            WHERE operacao_normalizada = '50'
              AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
              AND tela_destino = 'ProcessoConsumoMaterialForm'
              AND exige_operacao_anterior IS FALSE
        ),
        count(*) FILTER (
            WHERE operacao_normalizada = '60'
              AND tipo_processo = 'CONSUMO_QUIMICOS'
              AND tela_destino = 'ProcessoConsumoMaterialForm'
              AND exige_operacao_anterior IS TRUE
        ),
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
      INTO v_0050_global_total, v_0060_global_total, v_0050_global_correta, v_0060_global_correta, v_conflitos_globais
      FROM globais;

    IF v_0050_global_total <> 1 THEN
        RAISE EXCEPTION 'VALIDACAO 040: esperado exatamente 1 rota global ativa normalizada para 0050, encontrado %.', v_0050_global_total;
    END IF;

    IF v_0060_global_total <> 1 THEN
        RAISE EXCEPTION 'VALIDACAO 040: esperado exatamente 1 rota global ativa normalizada para 0060, encontrado %.', v_0060_global_total;
    END IF;

    IF v_0050_global_correta <> 1 THEN
        RAISE EXCEPTION 'VALIDACAO 040: rota global 0050 incorreta. Esperado CONSUMO_MATERIA_PRIMA, ProcessoConsumoMaterialForm, exige_operacao_anterior=false.';
    END IF;

    IF v_0060_global_correta <> 1 THEN
        RAISE EXCEPTION 'VALIDACAO 040: rota global 0060 incorreta. Esperado CONSUMO_QUIMICOS, ProcessoConsumoMaterialForm, exige_operacao_anterior=true.';
    END IF;

    IF v_conflitos_globais > 0 THEN
        RAISE EXCEPTION 'VALIDACAO 040: existe rota global ativa conflitante para 0050/0060.';
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
      INTO v_conflitos_especificos
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

    IF v_conflitos_especificos > 0 THEN
        RAISE EXCEPTION 'VALIDACAO 040: existe configuracao especifica ativa conflitante para 0050/0060 (%).', v_conflitos_especificos;
    END IF;

    RAISE NOTICE 'VALIDACAO 040 OK: 0050 -> CONSUMO_MATERIA_PRIMA, sem anterior local.';
    RAISE NOTICE 'VALIDACAO 040 OK: 0060 -> CONSUMO_QUIMICOS, exige conclusao da 0050.';
    RAISE NOTICE 'VALIDACAO 040 OK: duplicidades normalizadas e conflitos globais/especificos ausentes.';
END $$;

ROLLBACK;

\echo 'OK - regras de roteamento inicial 040 validadas (DEV)'
