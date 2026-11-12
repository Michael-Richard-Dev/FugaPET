\set ON_ERROR_STOP on
-- ============================================================
-- FugaPET | Ambiente Q | Schema homologacao
-- Incremento 053 - EXCLUIR_PESAGEM para todos os perfis ativos atuais
-- Autoridade: FUGAPET-Q-ENTRADA-DELETE-WEIGHING-PERMISSION-GRANT-053-R1
--
-- Chave natural da permissao:
--   PROCESSO_PRODUCAO / ENTRADA_PRODUTO / EXCLUIR_PESAGEM
--
-- Perfis esperados:
--   Administrador
--   Supervisor de Produção
--   Operador de Balança
--   Consulta
--
-- Nao hardcodar codigo_permissao.
-- Nao alterar usuarios/composicao de perfil/outras permissoes.
-- Nao DELETE.
-- Nao resetar sequence.
-- Nao SAP.
-- ============================================================

-- ROLLBACK LOGICO.
-- Como DELETE e proibido, os quatro links criados pelo Incremento 053
-- sao desativados e preservados fisicamente.
-- Nao executar sem autorizacao expressa.

BEGIN;

SET LOCAL search_path TO homologacao, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

LOCK TABLE homologacao.permissao IN SHARE MODE;
LOCK TABLE homologacao.perfil_acesso IN SHARE MODE;
LOCK TABLE homologacao.perfil_permissao IN SHARE ROW EXCLUSIVE MODE;

DO $check$
DECLARE
    v_links bigint;
    v_active_links bigint;
    v_duplicates bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '053 ROLLBACK: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION '053 ROLLBACK: schema incorreto: %', current_schema();
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM homologacao.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    )
    SELECT
        count(*),
        count(*) FILTER (WHERE pp.situacao_perfil_permissao = true)
      INTO v_links, v_active_links
      FROM homologacao.perfil_permissao pp
      JOIN target_permission tp
        ON tp.codigo_permissao = pp.codigo_permissao
      JOIN homologacao.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
       AND pa.nome_perfil_acesso IN (
           'Administrador',
           'Supervisor de Produção',
           'Operador de Balança',
           'Consulta'
       );

    IF v_links <> 4 OR v_active_links <> 4 THEN
        RAISE EXCEPTION
            '053 ROLLBACK: estado inesperado links total=% active=%, esperado 4/4',
            v_links, v_active_links;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM homologacao.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
    )
    SELECT COALESCE(sum(cnt - 1), 0)
      INTO v_duplicates
      FROM (
          SELECT
              pp.codigo_perfil_acesso,
              pp.codigo_permissao,
              count(*) AS cnt
          FROM homologacao.perfil_permissao pp
          JOIN target_permission tp
            ON tp.codigo_permissao = pp.codigo_permissao
          GROUP BY pp.codigo_perfil_acesso, pp.codigo_permissao
          HAVING count(*) > 1
      ) d;

    IF v_duplicates <> 0 THEN
        RAISE EXCEPTION '053 ROLLBACK: duplicates=%', v_duplicates;
    END IF;
END
$check$;

WITH target_permission AS (
    SELECT codigo_permissao
    FROM homologacao.permissao
    WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
      AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
      AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
      AND situacao_permissao = true
),
target_profiles AS (
    SELECT codigo_perfil_acesso
    FROM homologacao.perfil_acesso
    WHERE nome_perfil_acesso IN (
        'Administrador',
        'Supervisor de Produção',
        'Operador de Balança',
        'Consulta'
    )
)
UPDATE homologacao.perfil_permissao pp
SET situacao_perfil_permissao = false
FROM target_permission tp,
     target_profiles pa
WHERE pp.codigo_permissao = tp.codigo_permissao
  AND pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
  AND pp.situacao_perfil_permissao = true;

DO $check$
DECLARE
    v_links bigint;
    v_active_links bigint;
BEGIN
    WITH target_permission AS (
        SELECT codigo_permissao
        FROM homologacao.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
    )
    SELECT
        count(*),
        count(*) FILTER (WHERE pp.situacao_perfil_permissao = true)
      INTO v_links, v_active_links
      FROM homologacao.perfil_permissao pp
      JOIN target_permission tp
        ON tp.codigo_permissao = pp.codigo_permissao
      JOIN homologacao.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
       AND pa.nome_perfil_acesso IN (
           'Administrador',
           'Supervisor de Produção',
           'Operador de Balança',
           'Consulta'
       );

    IF v_links <> 4 OR v_active_links <> 0 THEN
        RAISE EXCEPTION
            '053 ROLLBACK: postcheck total=% active=%, esperado 4/0',
            v_links, v_active_links;
    END IF;
END
$check$;

COMMIT;

\echo '053_ROLLBACK_CONCLUIDO'
\echo 'LINK_ROWS_PRESERVED=SIM'
\echo 'ACTIVE_LINK_COUNT=0'
\echo 'DELETE_EXECUTED=NAO'
\echo 'SEQUENCE_RESET=NAO'
\echo 'SAP_WRITE=0'
