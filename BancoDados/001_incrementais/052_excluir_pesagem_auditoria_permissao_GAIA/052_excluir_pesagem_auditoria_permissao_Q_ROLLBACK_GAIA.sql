\set ON_ERROR_STOP on
-- ============================================================
-- 052_excluir_pesagem_auditoria_permissao_Q_ROLLBACK_GAIA.sql
-- Projeto FugaPET | Ambiente Q | Schema homologacao
-- Incremento 052 - Excluir Pesagem
--
-- ROLLBACK FORMAL / FAIL-CLOSED
--
-- IMPORTANTE:
--   O Gate 052 proibe DELETE.
--   Portanto a permissao EXCLUIR_PESAGEM NAO e removida fisicamente.
--   O rollback desativa a permissao (situacao_permissao=false).
--   Isso restaura o comportamento de "nao autorizavel", mas NAO
--   restaura o estado fisico original "linha ausente".
--
--   Funcoes/trigger/ACLs sao restaurados ao baseline pre-052.
--   A funcao generica original e restaurada a partir do backup
--   versionado no mesmo diretorio deste script.
--
-- PROIBIDO:
--   DELETE
--   TRUNCATE
--   sequence reset / setval / sequence reposition
--   SAP
--   profile grant
-- ============================================================

BEGIN;

SET LOCAL search_path TO homologacao, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- ============================================================
-- 0. FAIL-CLOSED ENVIRONMENT / CURRENT 052 STATE
-- ============================================================

DO $check$
DECLARE
    v_permission_active bigint;
    v_permission_total bigint;
    v_profile_grant bigint;
    v_lote_trigger bigint;
    v_guard bigint;
    v_mass bigint;
    v_mass_ok boolean;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: database incorreto: %',
            current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: schema incorreto: %',
            current_schema();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: principal inesperado: %',
            current_user;
    END IF;

    SELECT
        count(*) FILTER (WHERE p.situacao_permissao = true),
        count(*)
      INTO v_permission_active, v_permission_total
      FROM homologacao.permissao p
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_permission_active <> 1 OR v_permission_total <> 1 THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: permissao active=% total=%, esperado 1/1.',
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
            '052 ROLLBACK ABORTADO: profile grant inesperado=%.',
            v_profile_grant;
    END IF;

    IF (
        SELECT p.prosecdef
        FROM pg_proc p
        JOIN pg_namespace n
          ON n.oid = p.pronamespace
        WHERE n.nspname = 'homologacao'
          AND p.proname = 'fn_registrar_log_alteracao_cadastral'
          AND p.prokind = 'f'
    ) IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: auditoria generica nao esta no estado 052.';
    END IF;

    SELECT count(*)
      INTO v_lote_trigger
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

    IF v_lote_trigger <> 1 THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: lote audit trigger count=%, esperado 1.',
            v_lote_trigger;
    END IF;

    SELECT count(*)
      INTO v_guard
      FROM pg_proc p
      JOIN pg_namespace n
        ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname = 'fn_entrada_produto_sap_guard_counts'
       AND p.prokind = 'f';

    IF v_guard <> 1 THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: SAP guard count=%, esperado 1.',
            v_guard;
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
            '052 ROLLBACK ABORTADO: runtime possui DELETE.';
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
            '052 ROLLBACK ABORTADO: massa 2/2/2/2 divergiu.';
    END IF;
END
$check$;

-- ============================================================
-- 1. REMOVER SOMENTE OBJETOS CRIADOS PELO 052
-- ============================================================

DROP TRIGGER
    trg_entrada_produto_lote_log_alteracao_cadastral
ON homologacao.entrada_produto_lote;

DROP FUNCTION homologacao.fn_entrada_produto_sap_guard_counts(uuid);

-- ============================================================
-- 2. RESTAURAR AUDITORIA GENERICA EXATA DO BASELINE PRE-052
--
-- \ir resolve caminho relativo ao arquivo rollback atual.
-- O backup foi materializado/read-only antes da migration.
-- SHA-256 aprovado do backup:
-- CF19969065F0B76C2E12D6F4635387D5F87D20770AD6D609C316658DC6035468
-- ============================================================

\ir 052_fn_registrar_log_alteracao_cadastral_BACKUP_BEFORE_GAIA.sql

-- ============================================================
-- 3. PERMISSAO
--
-- DELETE e proibido pelo Gate.
-- Portanto desativar, sem remocao fisica e sem profile grant.
-- ============================================================

UPDATE homologacao.permissao
SET
    situacao_permissao = false,
    permissao_atualizado_em = date_trunc('minute', now())
WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
  AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
  AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
  AND situacao_permissao = true;

DO $check$
DECLARE
    v_permission_active bigint;
    v_permission_total bigint;
    v_profile_grant bigint;
    v_lote_trigger bigint;
    v_guard bigint;
    v_mass bigint;
    v_mass_ok boolean;
BEGIN
    SELECT
        count(*) FILTER (WHERE p.situacao_permissao = true),
        count(*)
      INTO v_permission_active, v_permission_total
      FROM homologacao.permissao p
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_permission_active <> 0 OR v_permission_total <> 1 THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: permissao apos rollback active=% total=%, esperado 0/1.',
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
            '052 ROLLBACK ABORTADO: profile grant apareceu=%.',
            v_profile_grant;
    END IF;

    IF (
        SELECT p.prosecdef
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'homologacao'
          AND p.proname = 'fn_registrar_log_alteracao_cadastral'
          AND p.prokind = 'f'
    ) IS DISTINCT FROM false THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: auditoria generica nao restaurou SECURITY INVOKER.';
    END IF;

    IF (
        SELECT p.proconfig
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'homologacao'
          AND p.proname = 'fn_registrar_log_alteracao_cadastral'
          AND p.prokind = 'f'
    ) IS NOT NULL THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: proconfig da auditoria nao restaurou baseline.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
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
          AND x.grantee = 0
          AND x.privilege_type = 'EXECUTE'
    ) THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: PUBLIC EXECUTE baseline nao restaurado.';
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

    IF v_lote_trigger <> 0 THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: lote trigger residual=%.',
            v_lote_trigger;
    END IF;

    SELECT count(*)
      INTO v_guard
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname = 'fn_entrada_produto_sap_guard_counts'
       AND p.prokind = 'f';

    IF v_guard <> 0 THEN
        RAISE EXCEPTION
            '052 ROLLBACK ABORTADO: SAP guard residual=%.',
            v_guard;
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
            '052 ROLLBACK ABORTADO: runtime DELETE apareceu.';
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
            '052 ROLLBACK ABORTADO: massa 2/2/2/2 alterada.';
    END IF;
END
$check$;

COMMIT;

\echo '============================================================'
\echo '052_ROLLBACK_CONCLUIDO'
\echo '============================================================'
\echo 'PERMISSION_PHYSICAL_STATE_RESTORED=NAO'
\echo 'PERMISSION_ACTIVE=NAO'
\echo 'PERMISSION_ROW_PRESERVED=SIM'
\echo 'PROFILE_GRANT_CREATED=NAO'
\echo 'GENERIC_AUDIT_BASELINE_RESTORED=SIM'
\echo 'LOTE_UPDATE_AUDIT_REMOVED=SIM'
\echo 'SAP_GUARD_REMOVED=SIM'
\echo 'DELETE_EXECUTED=NAO'
\echo 'DELETE_PRIVILEGE_GRANTED=NAO'
\echo 'SEQUENCE_RESET=NAO'
\echo 'MASSA_2_2_2_2_CHANGED=NAO'
\echo 'SAP_WRITE=0'
