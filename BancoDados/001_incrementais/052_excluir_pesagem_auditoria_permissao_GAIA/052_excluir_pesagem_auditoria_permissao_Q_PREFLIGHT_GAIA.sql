\set ON_ERROR_STOP on
-- ============================================================
-- 052_excluir_pesagem_auditoria_permissao_Q_PREFLIGHT_GAIA.sql
-- Projeto FugaPET | Ambiente Q | Schema homologacao
-- Incremento 052 - Excluir Pesagem
-- PREFLIGHT / BACKUP MATERIAL - READ ONLY
-- ============================================================

BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;

SET LOCAL search_path TO homologacao, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

\echo '============================================================'
\echo '052_PREFLIGHT_IDENTITY'
\echo '============================================================'
SELECT
    current_database() AS database_atual,
    current_user AS db_principal,
    session_user AS session_user,
    current_schema() AS schema_atual,
    current_setting('search_path') AS search_path,
    current_setting('transaction_read_only') AS transaction_read_only;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: schema incorreto: %', current_schema();
    END IF;

    IF current_setting('transaction_read_only') <> 'on' THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: transacao nao esta READ ONLY.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_app') THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: role fugapet_q_app inexistente.';
    END IF;

    IF to_regclass('homologacao.entrada_produto_lancamento') IS NULL
       OR to_regclass('homologacao.entrada_produto_item') IS NULL
       OR to_regclass('homologacao.entrada_produto_lote') IS NULL
       OR to_regclass('homologacao.entrada_produto_pesagem') IS NULL
       OR to_regclass('homologacao.integracao_sap_outbox') IS NULL
       OR to_regclass('homologacao.integracao_sap_tentativa') IS NULL
       OR to_regclass('homologacao.log_alteracao_cadastral') IS NULL
       OR to_regclass('homologacao.permissao') IS NULL THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: objeto obrigatorio ausente.';
    END IF;
END $$;

\echo '============================================================'
\echo '052_BACKUP_FUNCTION_AUDIT'
\echo '============================================================'
SELECT
    p.oid AS function_oid,
    n.nspname AS function_schema,
    p.proname AS function_name,
    pg_get_userbyid(p.proowner) AS function_owner,
    p.prosecdef AS security_definer,
    p.proacl AS function_acl,
    pg_get_functiondef(p.oid) AS function_ddl
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'homologacao'
  AND p.proname = 'fn_registrar_log_alteracao_cadastral'
  AND p.prokind = 'f';

\echo '============================================================'
\echo '052_BACKUP_TRIGGER_OPERATIONAL'
\echo '============================================================'
SELECT
    c.relname AS table_name,
    t.tgname AS trigger_name,
    pg_get_triggerdef(t.oid, true) AS trigger_ddl
FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'homologacao'
  AND NOT t.tgisinternal
  AND c.relname IN (
      'entrada_produto_lancamento',
      'entrada_produto_item',
      'entrada_produto_lote',
      'entrada_produto_pesagem'
  )
ORDER BY c.relname, t.tgname;

\echo '============================================================'
\echo '052_BACKUP_PERMISSION_CURRENT'
\echo '============================================================'
SELECT
    codigo_permissao,
    modulo_permissao,
    rotina_permissao,
    acao_permissao,
    descricao_permissao,
    situacao_permissao,
    permissao_criado_por,
    permissao_criado_em,
    permissao_atualizado_por,
    permissao_atualizado_em
FROM homologacao.permissao
WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
  AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
ORDER BY codigo_permissao;

\echo '============================================================'
\echo '052_TARGET_PERMISSION_COUNT_BEFORE'
\echo '============================================================'
SELECT count(*) AS permission_exact_count_before
FROM homologacao.permissao
WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
  AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
  AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
  AND situacao_permissao = true;

DO $$
DECLARE
    v_count bigint;
BEGIN
    SELECT count(*)
      INTO v_count
      FROM homologacao.permissao
     WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
       AND situacao_permissao = true;

    IF v_count NOT IN (0,1) THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: permissao EXCLUIR_PESAGEM possui % linhas ativas.', v_count;
    END IF;
END $$;

\echo '============================================================'
\echo '052_BACKUP_RUNTIME_GRANTS'
\echo '============================================================'
SELECT
    table_schema,
    table_name,
    privilege_type,
    is_grantable
FROM information_schema.role_table_grants
WHERE grantee = 'fugapet_q_app'
  AND table_schema = 'homologacao'
  AND table_name IN (
      'entrada_produto_lancamento',
      'entrada_produto_item',
      'entrada_produto_lote',
      'entrada_produto_pesagem',
      'integracao_sap_outbox',
      'integracao_sap_tentativa',
      'log_alteracao_cadastral',
      'usuario',
      'usuario_perfil',
      'perfil_acesso',
      'perfil_permissao',
      'permissao'
  )
ORDER BY table_name, privilege_type;

\echo '============================================================'
\echo '052_BACKUP_RUNTIME_FUNCTION_GRANTS'
\echo '============================================================'
SELECT
    routine_schema,
    routine_name,
    privilege_type,
    is_grantable
FROM information_schema.role_routine_grants
WHERE grantee = 'fugapet_q_app'
  AND routine_schema = 'homologacao'
ORDER BY routine_name, privilege_type;

\echo '============================================================'
\echo '052_RUNTIME_DELETE_PRIVILEGES_BEFORE'
\echo '============================================================'
SELECT *
FROM (
    VALUES
      ('entrada_produto_lancamento',
       has_table_privilege('fugapet_q_app','homologacao.entrada_produto_lancamento','DELETE')),
      ('entrada_produto_item',
       has_table_privilege('fugapet_q_app','homologacao.entrada_produto_item','DELETE')),
      ('entrada_produto_lote',
       has_table_privilege('fugapet_q_app','homologacao.entrada_produto_lote','DELETE')),
      ('entrada_produto_pesagem',
       has_table_privilege('fugapet_q_app','homologacao.entrada_produto_pesagem','DELETE'))
) AS x(table_name, can_delete);

DO $$
BEGIN
    IF has_table_privilege('fugapet_q_app','homologacao.entrada_produto_lancamento','DELETE')
       OR has_table_privilege('fugapet_q_app','homologacao.entrada_produto_item','DELETE')
       OR has_table_privilege('fugapet_q_app','homologacao.entrada_produto_lote','DELETE')
       OR has_table_privilege('fugapet_q_app','homologacao.entrada_produto_pesagem','DELETE') THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: runtime possui DELETE em tabela operacional.';
    END IF;
END $$;

\echo '============================================================'
\echo '052_DIRECT_SAP_PAYLOAD_SELECT_BEFORE'
\echo '============================================================'
SELECT
    has_table_privilege('fugapet_q_app','homologacao.integracao_sap_outbox','SELECT') AS outbox_select,
    has_table_privilege('fugapet_q_app','homologacao.integracao_sap_tentativa','SELECT') AS tentativa_select;

\echo '============================================================'
\echo '052_BACKUP_MASS_2_2_2_2'
\echo '============================================================'
SELECT
    l.codigo_entrada_produto_lancamento AS lancamento_id,
    i.codigo_entrada_produto_item AS item_id,
    lo.codigo_entrada_produto_lote AS lote_id,
    p.codigo_entrada_produto_pesagem AS pesagem_id,
    l.numero_pedido AS po,
    i.material,
    lo.numero_lote AS batch,
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
  ON i.codigo_entrada_produto_lancamento = l.codigo_entrada_produto_lancamento
JOIN homologacao.entrada_produto_lote lo
  ON lo.codigo_entrada_produto_item = i.codigo_entrada_produto_item
JOIN homologacao.entrada_produto_pesagem p
  ON p.codigo_entrada_produto_item = i.codigo_entrada_produto_item
 AND p.codigo_entrada_produto_lote = lo.codigo_entrada_produto_lote
WHERE l.codigo_entrada_produto_lancamento = 2
  AND i.codigo_entrada_produto_item = 2
  AND lo.codigo_entrada_produto_lote = 2
  AND p.codigo_entrada_produto_pesagem = 2;

DO $$
DECLARE
    v_count bigint;
    v_ok boolean;
BEGIN
    SELECT
        count(*),
        bool_and(
            l.numero_pedido = '4500000005'
            AND i.material = '1000111'
            AND lo.numero_lote = '123456789'
            AND p.peso_liquido_kg = 9.950::numeric
            AND p.status_pesagem = 'VALIDA'
            AND p.situacao_entrada_produto_pesagem = true
            AND lo.status_lote = 'FINALIZADO_LOCAL'
            AND i.status_item = 'FINALIZADO_LOCAL'
            AND i.situacao_entrada_produto_item = true
            AND i.quantidade_recebida = 9.950::numeric
            AND l.status_lancamento = 'FINALIZADO_LOCAL'
            AND l.situacao_entrada_produto_lancamento = true
        )
      INTO v_count, v_ok
      FROM homologacao.entrada_produto_lancamento l
      JOIN homologacao.entrada_produto_item i
        ON i.codigo_entrada_produto_lancamento = l.codigo_entrada_produto_lancamento
      JOIN homologacao.entrada_produto_lote lo
        ON lo.codigo_entrada_produto_item = i.codigo_entrada_produto_item
      JOIN homologacao.entrada_produto_pesagem p
        ON p.codigo_entrada_produto_item = i.codigo_entrada_produto_item
       AND p.codigo_entrada_produto_lote = lo.codigo_entrada_produto_lote
     WHERE l.codigo_entrada_produto_lancamento = 2
       AND i.codigo_entrada_produto_item = 2
       AND lo.codigo_entrada_produto_lote = 2
       AND p.codigo_entrada_produto_pesagem = 2;

    IF v_count <> 1 OR COALESCE(v_ok,false) = false THEN
        RAISE EXCEPTION '052 PREFLIGHT ABORTADO: massa 2/2/2/2 divergiu. count=%, ok=%', v_count, v_ok;
    END IF;
END $$;

\echo '============================================================'
\echo '052_OUTBOX_TENTATIVA_MASS'
\echo '============================================================'
WITH alvo AS (
    SELECT correlation_id
    FROM homologacao.entrada_produto_lote
    WHERE codigo_entrada_produto_lote = 2
)
SELECT
    (SELECT count(*)
       FROM homologacao.integracao_sap_outbox o
       JOIN alvo a ON a.correlation_id = o.correlation_id) AS outbox_matches,
    (SELECT count(*)
       FROM homologacao.integracao_sap_tentativa t
       JOIN homologacao.integracao_sap_outbox o
         ON o.codigo_outbox = t.codigo_outbox
       JOIN alvo a ON a.correlation_id = o.correlation_id) AS tentativa_matches;

\echo '============================================================'
\echo '052_EXISTING_GUARD_FUNCTION'
\echo '============================================================'
SELECT
    p.oid,
    p.proname,
    pg_get_function_identity_arguments(p.oid) AS identity_arguments,
    pg_get_userbyid(p.proowner) AS owner,
    p.prosecdef AS security_definer,
    p.proacl,
    pg_get_functiondef(p.oid) AS function_ddl
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'homologacao'
  AND p.proname = 'fn_entrada_produto_sap_guard_counts'
  AND p.prokind = 'f';

\echo '============================================================'
\echo '052_PREFLIGHT_RESULT'
\echo '============================================================'
SELECT
    'PASS'::text AS preflight,
    0::integer AS db_write,
    0::integer AS sap_write,
    'NAO'::text AS mass_changed;

COMMIT;

\echo '052_PREFLIGHT_FINISHED'
