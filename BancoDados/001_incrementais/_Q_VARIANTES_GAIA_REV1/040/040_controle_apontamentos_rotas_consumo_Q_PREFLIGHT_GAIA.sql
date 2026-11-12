\set ON_ERROR_STOP on
-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================
-- 040_controle_apontamentos_rotas_consumo_HML_PREFLIGHT_GAIA.sql
-- FugaPET_HML | Schema: homologacao
-- Revisao final GAIA: rotas globais 0050/0060 do Controle de Apontamentos.
-- NAO executa DML persistente.
-- ============================================================
SET search_path TO homologacao, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- Guarda de ambiente HML: impede execucao acidental em outro banco/schema.
DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION 'Ambiente incorreto para o pacote: banco atual=%, esperado=fuga_jales_local_homologacao_q_v1_2. Execucao bloqueada.', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'Ambiente incorreto para o pacote: schema homologacao inexistente. Execucao bloqueada.';
    END IF;
END $$;


-- Conferencia informativa de owner da tabela de configuracao quando ja existe.
SELECT 'OWNERS_040' AS grupo, n.nspname AS schema, c.relname AS objeto, pg_get_userbyid(c.relowner) AS owner
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'homologacao'
   AND c.relkind IN ('r','p')
   AND c.relname = 'operacao_producao_configuracao'
 ORDER BY c.relname;

DO $$
DECLARE
    v_colunas_ausentes text;
BEGIN
    IF to_regclass('homologacao.operacao_producao_configuracao') IS NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 040: tabela homologacao.operacao_producao_configuracao nao existe. Aplique/valide o pacote 039 antes.';
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
             WHERE ic.table_schema = 'homologacao'
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
    FROM homologacao.operacao_producao_configuracao
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
    FROM homologacao.operacao_producao_configuracao
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
        FROM homologacao.operacao_producao_configuracao
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
        FROM homologacao.operacao_producao_configuracao
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


DO $$
DECLARE
    v_ambiguas integer;
    v_lista text;
BEGIN
    WITH cfg AS (
        SELECT
            codigo_configuracao,
            COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') AS operacao_normalizada,
            COALESCE(centro, '') AS centro,
            COALESCE(tipo_ordem, '') AS tipo_ordem,
            COALESCE(sequencia_sap, '') AS sequencia_sap,
            COALESCE(suboperacao_sap, '') AS suboperacao_sap,
            COALESCE(centro_trabalho, '') AS centro_trabalho,
            (CASE WHEN COALESCE(centro, '') <> '' THEN 1 ELSE 0 END +
             CASE WHEN COALESCE(tipo_ordem, '') <> '' THEN 1 ELSE 0 END +
             CASE WHEN COALESCE(sequencia_sap, '') <> '' THEN 1 ELSE 0 END +
             CASE WHEN COALESCE(suboperacao_sap, '') <> '' THEN 1 ELSE 0 END +
             CASE WHEN COALESCE(centro_trabalho, '') <> '' THEN 1 ELSE 0 END) AS especificidade
        FROM homologacao.operacao_producao_configuracao
        WHERE ativo = true
          AND COALESCE(NULLIF(ltrim(COALESCE(operacao_sap, ''), '0'), ''), '0') IN ('50','60')
    ), pares AS (
        SELECT
            a.operacao_normalizada,
            a.especificidade,
            a.codigo_configuracao AS codigo_a,
            b.codigo_configuracao AS codigo_b
        FROM cfg a
        JOIN cfg b
          ON a.codigo_configuracao < b.codigo_configuracao
         AND a.operacao_normalizada = b.operacao_normalizada
         AND a.especificidade = b.especificidade
         AND (a.centro = b.centro OR a.centro = '' OR b.centro = '')
         AND (a.tipo_ordem = b.tipo_ordem OR a.tipo_ordem = '' OR b.tipo_ordem = '')
         AND (a.sequencia_sap = b.sequencia_sap OR a.sequencia_sap = '' OR b.sequencia_sap = '')
         AND (a.suboperacao_sap = b.suboperacao_sap OR a.suboperacao_sap = '' OR b.suboperacao_sap = '')
         AND (a.centro_trabalho = b.centro_trabalho OR a.centro_trabalho = '' OR b.centro_trabalho = '')
    )
    SELECT
        count(*),
        string_agg(format('op=%s especificidade=%s codigos=%s/%s', operacao_normalizada, especificidade, codigo_a, codigo_b), '; ' ORDER BY operacao_normalizada, especificidade, codigo_a, codigo_b)
      INTO v_ambiguas, v_lista
      FROM pares;

    IF v_ambiguas > 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 040: risco de ConfiguracaoAmbigua por empate de mesma especificidade aplicavel a 0050/0060. Pares: %', v_lista;
    END IF;
END $$;

ROLLBACK;
