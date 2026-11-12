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
\echo '053_VALIDACAO_PROFILE_GRANTS'
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
    pa.codigo_perfil_acesso,
    pa.nome_perfil_acesso,
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
GROUP BY pa.codigo_perfil_acesso, pa.nome_perfil_acesso
ORDER BY pa.codigo_perfil_acesso;

\echo '============================================================'
\echo '053_VALIDACAO_EFFECTIVE_USERS'
\echo '============================================================'

WITH target_permission AS (
    SELECT codigo_permissao
    FROM homologacao.permissao
    WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
      AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
      AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
      AND situacao_permissao = true
)
SELECT DISTINCT
    u.codigo_usuario,
    u.login_usuario,
    u.nome_usuario,
    pa.codigo_perfil_acesso,
    pa.nome_perfil_acesso
FROM homologacao.usuario u
JOIN homologacao.usuario_perfil up
  ON up.codigo_usuario = u.codigo_usuario
 AND up.situacao_usuario_perfil = true
JOIN homologacao.perfil_acesso pa
  ON pa.codigo_perfil_acesso = up.codigo_perfil_acesso
 AND pa.situacao_perfil_acesso = true
JOIN homologacao.perfil_permissao pp
  ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
 AND pp.situacao_perfil_permissao = true
JOIN target_permission tp
  ON tp.codigo_permissao = pp.codigo_permissao
WHERE u.situacao_usuario = true
  AND u.bloqueado_usuario = false
ORDER BY u.codigo_usuario, pa.codigo_perfil_acesso;

\echo '============================================================'
\echo '053_VALIDACAO_ASSERTIONS'
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
    v_link_count bigint;
    v_active_link_count bigint;
    v_duplicate_link_count bigint;
    v_admin_effective bigint;
    v_fdomingos_effective bigint;
    v_mass bigint;
    v_mass_ok boolean;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '053 VALIDACAO: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION '053 VALIDACAO: schema incorreto: %', current_schema();
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
            '053 VALIDACAO: permission total=% active=%, esperado 1/1',
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
        RAISE EXCEPTION '053 VALIDACAO: conjunto de perfis divergiu';
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
      INTO v_link_count, v_active_link_count
      FROM homologacao.perfil_permissao pp
      JOIN target_permission tp
        ON tp.codigo_permissao = pp.codigo_permissao
      JOIN homologacao.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
       AND pa.situacao_perfil_acesso = true;

    IF v_link_count <> 4 OR v_active_link_count <> 4 THEN
        RAISE EXCEPTION
            '053 VALIDACAO: links total=% active=%, esperado 4/4',
            v_link_count, v_active_link_count;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM homologacao.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
    )
    SELECT COALESCE(sum(cnt - 1), 0)
      INTO v_duplicate_link_count
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

    IF v_duplicate_link_count <> 0 THEN
        RAISE EXCEPTION
            '053 VALIDACAO: duplicate_link_count=%',
            v_duplicate_link_count;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM homologacao.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    ),
    effective AS (
        SELECT DISTINCT u.login_usuario
        FROM homologacao.usuario u
        JOIN homologacao.usuario_perfil up
          ON up.codigo_usuario = u.codigo_usuario
         AND up.situacao_usuario_perfil = true
        JOIN homologacao.perfil_acesso pa
          ON pa.codigo_perfil_acesso = up.codigo_perfil_acesso
         AND pa.situacao_perfil_acesso = true
        JOIN homologacao.perfil_permissao pp
          ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
         AND pp.situacao_perfil_permissao = true
        JOIN target_permission tp
          ON tp.codigo_permissao = pp.codigo_permissao
        WHERE u.situacao_usuario = true
          AND u.bloqueado_usuario = false
    )
    SELECT
        count(*) FILTER (WHERE login_usuario = 'admin'),
        count(*) FILTER (WHERE login_usuario = 'f.domingos')
      INTO v_admin_effective, v_fdomingos_effective
      FROM effective;

    IF v_admin_effective <> 1 OR v_fdomingos_effective <> 1 THEN
        RAISE EXCEPTION
            '053 VALIDACAO: efetividade admin=% f.domingos=%, esperado 1/1',
            v_admin_effective, v_fdomingos_effective;
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
        RAISE EXCEPTION '053 VALIDACAO: massa 2/2/2/2 divergiu';
    END IF;
END
$check$;

SELECT
    'PASS'::text AS validacao_053_result,
    4::integer AS target_profile_count,
    4::integer AS permission_profile_link_count,
    0::integer AS duplicate_link_count,
    'SIM'::text AS admin_effective,
    'SIM'::text AS f_domingos_effective,
    'NAO'::text AS massa_2_2_2_2_changed,
    0::integer AS db_write,
    0::integer AS sap_write;

COMMIT;
