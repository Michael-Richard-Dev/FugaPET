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

BEGIN;

SET LOCAL search_path TO homologacao, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- Congela os objetos do contrato durante o grant.
LOCK TABLE homologacao.permissao IN SHARE MODE;
LOCK TABLE homologacao.perfil_acesso IN SHARE MODE;
LOCK TABLE homologacao.perfil_permissao IN SHARE ROW EXCLUSIVE MODE;

\echo '============================================================'
\echo '053_PROPOSTA_PRECHECK'
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
    v_duplicate_groups bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '053 PROPOSTA: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION '053 PROPOSTA: schema incorreto: %', current_schema();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '053 PROPOSTA: principal inesperado: %', current_user;
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
            '053 PROPOSTA: permission total=% active=%, esperado 1/1',
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
            '053 PROPOSTA: perfis divergentes total=% admin=% supervisor=% operador=% consulta=%',
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
            '053 PROPOSTA: existem % links previos; esperado 0',
            v_existing_links;
    END IF;

    SELECT count(*)
      INTO v_duplicate_groups
      FROM (
          SELECT codigo_perfil_acesso, codigo_permissao
          FROM homologacao.perfil_permissao
          GROUP BY codigo_perfil_acesso, codigo_permissao
          HAVING count(*) > 1
      ) d;

    IF v_duplicate_groups <> 0 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: tabela perfil_permissao ja possui % grupos duplicados',
            v_duplicate_groups;
    END IF;
END
$check$;

\echo '============================================================'
\echo '053_PROPOSTA_INSERT'
\echo '============================================================'

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
    WHERE situacao_perfil_acesso = true
      AND nome_perfil_acesso IN (
          'Administrador',
          'Supervisor de Produção',
          'Operador de Balança',
          'Consulta'
      )
),
inserted AS (
    INSERT INTO homologacao.perfil_permissao (
        codigo_perfil_acesso,
        codigo_permissao,
        situacao_perfil_permissao
    )
    SELECT
        tp.codigo_perfil_acesso,
        prm.codigo_permissao,
        true
    FROM target_profiles tp
    CROSS JOIN target_permission prm
    WHERE NOT EXISTS (
        SELECT 1
        FROM homologacao.perfil_permissao pp
        WHERE pp.codigo_perfil_acesso = tp.codigo_perfil_acesso
          AND pp.codigo_permissao = prm.codigo_permissao
    )
    RETURNING
        codigo_perfil_permissao,
        codigo_perfil_acesso,
        codigo_permissao,
        situacao_perfil_permissao
)
SELECT
    count(*) AS inserted_count
FROM inserted
\gset

SELECT :inserted_count::bigint AS inserted_count;

SELECT (:inserted_count::bigint = 4) AS inserted_count_ok
\gset

\if :inserted_count_ok
    \echo 'INSERTED_COUNT_OK=SIM'
\else
    \echo 'INSERTED_COUNT_OK=NAO'
    \quit 41
\endif

\echo '============================================================'
\echo '053_PROPOSTA_POSTCHECK'
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

DO $check$
DECLARE
    v_link_count bigint;
    v_active_link_count bigint;
    v_duplicate_link_count bigint;
    v_admin_effective bigint;
    v_fdomingos_effective bigint;
    v_mass bigint;
    v_mass_ok boolean;
BEGIN
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
            '053 PROPOSTA: postcheck links total=% active=%, esperado 4/4',
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
            '053 PROPOSTA: duplicate_link_count=%',
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
            '053 PROPOSTA: efetividade admin=% f.domingos=%, esperado 1/1',
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
        RAISE EXCEPTION '053 PROPOSTA: massa 2/2/2/2 divergiu';
    END IF;
END
$check$;

COMMIT;

\echo '============================================================'
\echo '053_PROPOSTA_APLICADA'
\echo '============================================================'
\echo 'TARGET_PROFILE_COUNT=4'
\echo 'PERMISSION_PROFILE_LINK_COUNT=4'
\echo 'DUPLICATE_LINK_COUNT=0'
\echo 'ADMIN_EFFECTIVE=SIM'
\echo 'F_DOMINGOS_EFFECTIVE=SIM'
\echo 'MASSA_2_2_2_2_CHANGED=NAO'
\echo 'SEQUENCE_RESET=NAO'
\echo 'SAP_WRITE=0'
