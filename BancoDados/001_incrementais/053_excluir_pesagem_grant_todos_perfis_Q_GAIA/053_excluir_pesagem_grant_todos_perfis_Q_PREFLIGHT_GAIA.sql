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

BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL search_path TO homologacao, pg_catalog;

\echo '============================================================'
\echo '053_PREFLIGHT_IDENTIDADE'
\echo '============================================================'

SELECT
    current_database() AS database_atual,
    current_user AS db_principal,
    current_schema() AS schema_atual,
    current_setting('transaction_read_only') AS transaction_read_only;

\echo '============================================================'
\echo '053_PREFLIGHT_PERMISSION'
\echo '============================================================'

SELECT
    codigo_permissao,
    modulo_permissao,
    rotina_permissao,
    acao_permissao,
    descricao_permissao,
    situacao_permissao
FROM homologacao.permissao
WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
  AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
  AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM';

\echo '============================================================'
\echo '053_PREFLIGHT_TARGET_PROFILES'
\echo '============================================================'

SELECT
    codigo_perfil_acesso,
    nome_perfil_acesso,
    situacao_perfil_acesso
FROM homologacao.perfil_acesso
WHERE situacao_perfil_acesso = true
ORDER BY codigo_perfil_acesso;

\echo '============================================================'
\echo '053_PREFLIGHT_STATE_BEFORE'
\echo '============================================================'

WITH target_permission AS (
    SELECT codigo_permissao
    FROM homologacao.permissao
    WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
      AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
      AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
      AND situacao_permissao = true
)
SELECT
    pa.codigo_perfil_acesso AS codigo_perfil,
    pa.nome_perfil_acesso AS nome_perfil,
    count(pp.codigo_perfil_permissao) AS total_links,
    count(pp.codigo_perfil_permissao) FILTER (
        WHERE pp.situacao_perfil_permissao = true
    ) AS active_links
FROM homologacao.perfil_acesso pa
CROSS JOIN target_permission tp
LEFT JOIN homologacao.perfil_permissao pp
  ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
 AND pp.codigo_permissao = tp.codigo_permissao
WHERE pa.situacao_perfil_acesso = true
GROUP BY
    pa.codigo_perfil_acesso,
    pa.nome_perfil_acesso
ORDER BY pa.codigo_perfil_acesso;

\echo '============================================================'
\echo '053_PREFLIGHT_ASSERTIONS'
\echo '============================================================'

DO $check$
DECLARE
    v_permission_total bigint;
    v_permission_active bigint;
    v_profiles bigint;
    v_admin bigint;
    v_supervisor bigint;
    v_operador bigint;
    v_consulta bigint;
    v_existing_links bigint;
    v_mass bigint;
    v_mass_ok boolean;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '053 PREFLIGHT: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION '053 PREFLIGHT: schema incorreto: %', current_schema();
    END IF;

    SELECT
        count(*),
        count(*) FILTER (WHERE situacao_permissao = true)
      INTO v_permission_total, v_permission_active
      FROM homologacao.permissao
     WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_permission_total <> 1 OR v_permission_active <> 1 THEN
        RAISE EXCEPTION
            '053 PREFLIGHT: permission total=% active=%, esperado 1/1',
            v_permission_total, v_permission_active;
    END IF;

    SELECT
        count(*),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Administrador'),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Supervisor de Produção'),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Operador de Balança'),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Consulta')
      INTO v_profiles, v_admin, v_supervisor, v_operador, v_consulta
      FROM homologacao.perfil_acesso
     WHERE situacao_perfil_acesso = true;

    IF v_profiles <> 4
       OR v_admin <> 1
       OR v_supervisor <> 1
       OR v_operador <> 1
       OR v_consulta <> 1 THEN
        RAISE EXCEPTION
            '053 PREFLIGHT: perfis divergentes total=% admin=% supervisor=% operador=% consulta=%',
            v_profiles, v_admin, v_supervisor, v_operador, v_consulta;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM homologacao.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    )
    SELECT count(*)
      INTO v_existing_links
      FROM homologacao.perfil_permissao pp
      JOIN target_permission tp
        ON tp.codigo_permissao = pp.codigo_permissao
      JOIN homologacao.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
       AND pa.situacao_perfil_acesso = true;

    IF v_existing_links <> 0 THEN
        RAISE EXCEPTION
            '053 PREFLIGHT: existem % links previos; esperado 0',
            v_existing_links;
    END IF;

    SELECT
        count(*),
        bool_and(
            p.peso_liquido_kg = 9.950::numeric
            AND p.status_pesagem = 'VALIDA'
            AND p.situacao_entrada_produto_pesagem = true
        )
      INTO v_mass, v_mass_ok
      FROM homologacao.entrada_produto_lancamento l
      JOIN homologacao.entrada_produto_item i
        ON i.codigo_entrada_produto_lancamento =
           l.codigo_entrada_produto_lancamento
      JOIN homologacao.entrada_produto_lote lo
        ON lo.codigo_entrada_produto_item =
           i.codigo_entrada_produto_item
      JOIN homologacao.entrada_produto_pesagem p
        ON p.codigo_entrada_produto_item =
           i.codigo_entrada_produto_item
       AND p.codigo_entrada_produto_lote =
           lo.codigo_entrada_produto_lote
     WHERE l.codigo_entrada_produto_lancamento = 2
       AND i.codigo_entrada_produto_item = 2
       AND lo.codigo_entrada_produto_lote = 2
       AND p.codigo_entrada_produto_pesagem = 2;

    IF v_mass <> 1 OR COALESCE(v_mass_ok, false) = false THEN
        RAISE EXCEPTION '053 PREFLIGHT: massa 2/2/2/2 divergiu';
    END IF;
END
$check$;

SELECT
    'PASS'::text AS preflight_053_result,
    1::integer AS permission_exact_count,
    4::integer AS target_active_profile_count,
    0::integer AS existing_link_count,
    'NAO'::text AS massa_2_2_2_2_changed,
    0::integer AS db_write,
    0::integer AS sap_write;

COMMIT;
