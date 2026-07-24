\set ON_ERROR_STOP on
-- ============================================================
-- 040_controle_apontamentos_rotas_consumo_DEV_PREFLIGHT_GAIA.sql
-- FugaPET_Dev | Schema: desenvolvimento
-- Revisao final GAIA: rotas globais 0050/0060 do Controle de Apontamentos.
-- NAO executa DML persistente.
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
        RAISE EXCEPTION 'PREFLIGHT 040: tabela desenvolvimento.operacao_producao_configuracao nao existe. Aplique/valide o pacote 039 antes.';
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
        RAISE EXCEPTION 'PREFLIGHT 040: estrutura de operacao_producao_configuracao incompleta. Colunas ausentes: %.', v_colunas_ausentes;
    END IF;
END $$;

\echo 'PREFLIGHT 040 - ROTAS GLOBAIS ATIVAS NORMALIZADAS PARA 0050/0060'
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
        ativo,
        criado_por
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
    centro_trabalho,
    criado_por
FROM cfg
WHERE operacao_normalizada IN ('50','60')
  AND COALESCE(centro, '') = ''
  AND COALESCE(tipo_ordem, '') = ''
  AND COALESCE(sequencia_sap, '') = ''
  AND COALESCE(suboperacao_sap, '') = ''
  AND COALESCE(centro_trabalho, '') = ''
ORDER BY operacao_normalizada, operacao_sap, tipo_processo, tela_destino, exige_operacao_anterior;

\echo 'PREFLIGHT 040 - CONFIGURACOES ESPECIFICAS ATIVAS PARA 0050/0060'
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
        ativo,
        criado_por
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
    centro_trabalho,
    criado_por
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
    v_total_global_0050 integer;
    v_total_global_0060 integer;
    v_global_conflitante integer;
    v_especifica_conflitante integer;
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

    IF v_total_global_0050 > 1 THEN
        RAISE EXCEPTION 'PREFLIGHT 040: mais de uma rota global ativa normalizada para 0050 encontrada (%). Bloqueado.', v_total_global_0050;
    END IF;

    IF v_total_global_0060 > 1 THEN
        RAISE EXCEPTION 'PREFLIGHT 040: mais de uma rota global ativa normalizada para 0060 encontrada (%). Bloqueado.', v_total_global_0060;
    END IF;

    IF v_global_conflitante > 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 040: rota global ativa para 0050/0060 com tipo_processo, tela_destino ou exige_operacao_anterior conflitante. Bloqueado.';
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
        RAISE EXCEPTION 'PREFLIGHT 040: configuracao especifica ativa conflitante para 0050/0060 encontrada (%). Pode ganhar por especificidade no repository. Bloqueado.', v_especifica_conflitante;
    END IF;

    RAISE NOTICE 'PREFLIGHT 040 OK: 0050 sem anterior local; 0060 exige conclusao anterior 0050. Pacote pode seguir conforme estado encontrado.';
END $$;

ROLLBACK;
