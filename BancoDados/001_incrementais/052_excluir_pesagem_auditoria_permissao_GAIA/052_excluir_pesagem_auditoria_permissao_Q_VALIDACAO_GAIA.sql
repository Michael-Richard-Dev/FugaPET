\set ON_ERROR_STOP on
-- ============================================================
-- 052_excluir_pesagem_auditoria_permissao_Q_VALIDACAO_GAIA.sql
-- Projeto FugaPET | Ambiente Q | Schema homologacao
-- Incremento 052 - Excluir Pesagem
--
-- VALIDACAO FORMAL READ-ONLY POS-MIGRATION
--
-- Nao executa UPDATE/INSERT/DELETE/DDL.
-- Nao repete o teste funcional do STEP17 para nao consumir
-- novos valores da sequence de auditoria.
-- ============================================================

BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;

SET LOCAL search_path TO homologacao, pg_catalog;

\echo '============================================================'
\echo '052_VALIDACAO_IDENTIDADE'
\echo '============================================================'

SELECT
    current_database() AS database_atual,
    current_user AS db_principal,
    current_schema() AS schema_atual,
    current_setting('transaction_read_only') AS transaction_read_only;

\echo '============================================================'
\echo '052_VALIDACAO_PERMISSAO'
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

SELECT
    count(*) FILTER (
        WHERE situacao_permissao = true
    ) AS permission_active_count,
    count(*) AS permission_total_count
FROM homologacao.permissao
WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
  AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
  AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM';

SELECT count(*) AS profile_grant_count
FROM homologacao.perfil_permissao pp
JOIN homologacao.permissao p
  ON p.codigo_permissao = pp.codigo_permissao
WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
  AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
  AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM';

\echo '============================================================'
\echo '052_VALIDACAO_AUDITORIA_GENERICA'
\echo '============================================================'

SELECT
    pg_get_userbyid(p.proowner) AS owner,
    p.prosecdef AS security_definer,
    p.proconfig,
    p.proacl,
    has_function_privilege(
        'fugapet_q_app',
        p.oid,
        'EXECUTE'
    ) AS runtime_direct_execute,
    (
        pg_get_functiondef(p.oid)
        LIKE '%v_codigo_usuario := COALESCE(%v_app_usuario_id,%'
    ) AS app_usuario_primary_signature,
    (
        pg_get_functiondef(p.oid)
        LIKE '%u.situacao_usuario = true%'
        AND pg_get_functiondef(p.oid)
        LIKE '%u.bloqueado_usuario = false%'
    ) AS app_usuario_validation_signature
FROM pg_proc p
JOIN pg_namespace n
  ON n.oid = p.pronamespace
WHERE n.nspname = 'homologacao'
  AND p.proname = 'fn_registrar_log_alteracao_cadastral'
  AND p.prokind = 'f';

SELECT
    CASE
        WHEN x.grantee = 0 THEN 'PUBLIC'
        ELSE pg_get_userbyid(x.grantee)
    END AS grantee_name,
    x.privilege_type,
    x.is_grantable
FROM pg_proc p
JOIN pg_namespace n
  ON n.oid = p.pronamespace
CROSS JOIN LATERAL aclexplode(
    COALESCE(
        p.proacl,
        acldefault('f', p.proowner)
    )
) x
WHERE n.nspname = 'homologacao'
  AND p.proname = 'fn_registrar_log_alteracao_cadastral'
  AND p.prokind = 'f'
ORDER BY grantee_name, x.privilege_type;

\echo '============================================================'
\echo '052_VALIDACAO_TRIGGER_LOTE'
\echo '============================================================'

SELECT
    t.tgname,
    pg_get_triggerdef(t.oid, true) AS trigger_ddl
FROM pg_trigger t
JOIN pg_class c
  ON c.oid = t.tgrelid
JOIN pg_namespace n
  ON n.oid = c.relnamespace
WHERE n.nspname = 'homologacao'
  AND c.relname = 'entrada_produto_lote'
  AND t.tgname =
      'trg_entrada_produto_lote_log_alteracao_cadastral'
  AND NOT t.tgisinternal;

\echo '============================================================'
\echo '052_VALIDACAO_SAP_GUARD'
\echo '============================================================'

SELECT
    p.proname,
    pg_get_function_identity_arguments(p.oid) AS identity_arguments,
    pg_get_function_result(p.oid) AS function_result,
    pg_get_userbyid(p.proowner) AS owner,
    p.prosecdef AS security_definer,
    p.proconfig,
    p.proacl,
    has_function_privilege(
        'fugapet_q_app',
        p.oid,
        'EXECUTE'
    ) AS runtime_execute
FROM pg_proc p
JOIN pg_namespace n
  ON n.oid = p.pronamespace
WHERE n.nspname = 'homologacao'
  AND p.proname = 'fn_entrada_produto_sap_guard_counts'
  AND p.prokind = 'f';

SET LOCAL ROLE fugapet_q_app;

SELECT
    current_user AS effective_runtime_role,
    session_user AS session_user;

SELECT *
FROM homologacao.fn_entrada_produto_sap_guard_counts(
    '64aa1c3a-c84b-4346-8358-b993e1c76e20'::uuid
);

RESET ROLE;

\echo '============================================================'
\echo '052_VALIDACAO_PRIVILEGIOS_RUNTIME'
\echo '============================================================'

SELECT *
FROM (
    VALUES
      (
        'entrada_produto_lancamento',
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_lancamento',
            'UPDATE'
        ),
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_lancamento',
            'DELETE'
        )
      ),
      (
        'entrada_produto_item',
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_item',
            'UPDATE'
        ),
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_item',
            'DELETE'
        )
      ),
      (
        'entrada_produto_lote',
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_lote',
            'UPDATE'
        ),
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_lote',
            'DELETE'
        )
      ),
      (
        'entrada_produto_pesagem',
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_pesagem',
            'UPDATE'
        ),
        has_table_privilege(
            'fugapet_q_app',
            'homologacao.entrada_produto_pesagem',
            'DELETE'
        )
      )
) AS x(table_name, can_update, can_delete);

SELECT
    has_table_privilege(
        'fugapet_q_app',
        'homologacao.integracao_sap_outbox',
        'SELECT'
    ) AS outbox_raw_select,
    has_table_privilege(
        'fugapet_q_app',
        'homologacao.integracao_sap_tentativa',
        'SELECT'
    ) AS tentativa_raw_select,
    has_table_privilege(
        'fugapet_q_app',
        'homologacao.log_alteracao_cadastral',
        'INSERT'
    ) AS raw_log_insert;

\echo '============================================================'
\echo '052_VALIDACAO_MASSA_2_2_2_2'
\echo '============================================================'

SELECT
    l.codigo_entrada_produto_lancamento AS lancamento_id,
    i.codigo_entrada_produto_item AS item_id,
    lo.codigo_entrada_produto_lote AS lote_id,
    p.codigo_entrada_produto_pesagem AS pesagem_id,
    l.numero_pedido,
    i.material,
    lo.numero_lote,
    lo.correlation_id,
    p.peso_bruto_kg,
    p.peso_tara_kg,
    p.peso_liquido_kg,
    p.status_pesagem,
    p.situacao_entrada_produto_pesagem,
    lo.peso_liquido_total_kg,
    lo.status_lote,
    i.quantidade_recebida,
    i.status_item,
    i.situacao_entrada_produto_item,
    l.status_lancamento,
    l.situacao_entrada_produto_lancamento
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

\echo '============================================================'
\echo '052_VALIDACAO_AUDIT_SEQUENCE'
\echo '============================================================'

SELECT
    last_value AS audit_sequence_last_value,
    is_called AS audit_sequence_is_called
FROM homologacao.log_alteracao_cadastral_codigo_log_alteracao_cadastral_seq;

SELECT
    max(codigo_log_alteracao_cadastral) AS max_log_id,
    count(*) AS log_row_count
FROM homologacao.log_alteracao_cadastral;

\echo '============================================================'
\echo '052_VALIDACAO_ASSERTIONS'
\echo '============================================================'

DO $check$
DECLARE
    v_permission_active bigint;
    v_permission_total bigint;
    v_profile_grant bigint;
    v_lote_trigger bigint;
    v_guard bigint;
    v_outbox bigint;
    v_tentativa bigint;
    v_mass bigint;
    v_mass_ok boolean;
    v_seq bigint;
BEGIN
    SELECT
        count(*) FILTER (WHERE situacao_permissao = true),
        count(*)
      INTO v_permission_active, v_permission_total
      FROM homologacao.permissao
     WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_permission_active <> 1 OR v_permission_total <> 1 THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL permission active=% total=%',
            v_permission_active,
            v_permission_total;
    END IF;

    SELECT count(*)
      INTO v_profile_grant
      FROM homologacao.perfil_permissao pp
      JOIN homologacao.permissao p
        ON p.codigo_permissao = pp.codigo_permissao
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_profile_grant <> 0 THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL profile_grant=%',
            v_profile_grant;
    END IF;

    IF (
        SELECT p.prosecdef
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'homologacao'
          AND p.proname =
              'fn_registrar_log_alteracao_cadastral'
          AND p.prokind = 'f'
    ) IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL audit_security_definer';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_proc p
        JOIN pg_namespace n
          ON n.oid = p.pronamespace
        WHERE n.nspname = 'homologacao'
          AND p.proname =
              'fn_registrar_log_alteracao_cadastral'
          AND p.prokind = 'f'
          AND p.proconfig IS NOT NULL
          AND 'search_path=pg_catalog' = ANY(p.proconfig)
    ) THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL audit_search_path';
    END IF;

    IF has_function_privilege(
        'fugapet_q_app',
        'homologacao.fn_registrar_log_alteracao_cadastral()',
        'EXECUTE'
    ) THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL runtime_direct_audit_execute';
    END IF;

    SELECT count(*)
      INTO v_lote_trigger
      FROM pg_trigger t
      JOIN pg_class c ON c.oid = t.tgrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'homologacao'
       AND c.relname = 'entrada_produto_lote'
       AND t.tgname =
           'trg_entrada_produto_lote_log_alteracao_cadastral'
       AND NOT t.tgisinternal;

    IF v_lote_trigger <> 1 THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL lote_trigger=%',
            v_lote_trigger;
    END IF;

    SELECT count(*)
      INTO v_guard
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname =
           'fn_entrada_produto_sap_guard_counts'
       AND p.prokind = 'f';

    IF v_guard <> 1 THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL sap_guard=%',
            v_guard;
    END IF;

    IF NOT has_function_privilege(
        'fugapet_q_app',
        'homologacao.fn_entrada_produto_sap_guard_counts(uuid)',
        'EXECUTE'
    ) THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL runtime_guard_execute';
    END IF;

    IF has_table_privilege(
        'fugapet_q_app',
        'homologacao.integracao_sap_outbox',
        'SELECT'
    )
    OR has_table_privilege(
        'fugapet_q_app',
        'homologacao.integracao_sap_tentativa',
        'SELECT'
    )
    OR has_table_privilege(
        'fugapet_q_app',
        'homologacao.log_alteracao_cadastral',
        'INSERT'
    ) THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL raw_runtime_privilege';
    END IF;

    IF has_table_privilege(
        'fugapet_q_app',
        'homologacao.entrada_produto_lancamento',
        'DELETE'
    )
    OR has_table_privilege(
        'fugapet_q_app',
        'homologacao.entrada_produto_item',
        'DELETE'
    )
    OR has_table_privilege(
        'fugapet_q_app',
        'homologacao.entrada_produto_lote',
        'DELETE'
    )
    OR has_table_privilege(
        'fugapet_q_app',
        'homologacao.entrada_produto_pesagem',
        'DELETE'
    ) THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL runtime_delete';
    END IF;

    SELECT g.outbox_matches, g.tentativa_matches
      INTO v_outbox, v_tentativa
      FROM homologacao.fn_entrada_produto_sap_guard_counts(
          '64aa1c3a-c84b-4346-8358-b993e1c76e20'::uuid
      ) g;

    IF v_outbox <> 0 OR v_tentativa <> 0 THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL guard outbox=% tentativa=%',
            v_outbox,
            v_tentativa;
    END IF;

    SELECT
        count(*),
        bool_and(
            l.numero_pedido = '4500000005'
            AND i.material = '1000111'
            AND lo.numero_lote = '123456789'
            AND p.peso_bruto_kg = 10.000::numeric
            AND p.peso_tara_kg = 0.050::numeric
            AND p.peso_liquido_kg = 9.950::numeric
            AND p.status_pesagem = 'VALIDA'
            AND p.situacao_entrada_produto_pesagem = true
            AND lo.peso_liquido_total_kg = 9.950::numeric
            AND lo.status_lote = 'FINALIZADO_LOCAL'
            AND i.quantidade_recebida = 9.950::numeric
            AND i.status_item = 'FINALIZADO_LOCAL'
            AND i.situacao_entrada_produto_item = true
            AND l.status_lancamento = 'FINALIZADO_LOCAL'
            AND l.situacao_entrada_produto_lancamento = true
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
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL mass_2_2_2_2';
    END IF;

    SELECT last_value
      INTO v_seq
      FROM homologacao.log_alteracao_cadastral_codigo_log_alteracao_cadastral_seq;

    IF v_seq < 425 THEN
        RAISE EXCEPTION
            '052_VALIDACAO_FAIL audit_sequence_last_value=% expected>=425',
            v_seq;
    END IF;
END
$check$;

SELECT
    'PASS'::text AS validacao_052_result,
    1::integer AS permission_exact_count,
    0::integer AS profile_grant_count,
    'SIM'::text AS audit_security_definer,
    'SIM'::text AS lote_update_audit,
    'SIM'::text AS sap_guard_function,
    'NAO'::text AS delete_privilege_granted,
    'NAO'::text AS outbox_payload_exposed,
    'NAO'::text AS massa_2_2_2_2_changed,
    0::integer AS db_write,
    0::integer AS sap_write;

COMMIT;
