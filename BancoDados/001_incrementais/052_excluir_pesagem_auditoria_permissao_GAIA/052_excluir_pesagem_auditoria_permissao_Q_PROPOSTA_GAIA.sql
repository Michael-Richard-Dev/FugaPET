\set ON_ERROR_STOP on
-- ============================================================
-- 052_excluir_pesagem_auditoria_permissao_Q_PROPOSTA_GAIA.sql
-- Projeto FugaPET | Ambiente Q | Schema homologacao
-- Incremento 052 - Excluir Pesagem
--
-- OBJETIVO:
--   A. Seed PROCESSO_PRODUCAO/ENTRADA_PRODUTO/EXCLUIR_PESAGEM
--   B. app.usuario_id prioritario e validado na auditoria generica
--   C. auditoria generica SECURITY DEFINER com search_path fixo
--   D. AFTER UPDATE audit em entrada_produto_lote
--   E. SAP guard minimo por correlation_id sem expor payload
--
-- REGRAS:
--   PHYSICAL DELETE = PROIBIDO
--   SEM profile grant
--   SEM SELECT bruto de outbox/tentativa ao runtime
--   SEM alteracao da massa operacional 2/2/2/2
--   SEM reset de sequence
--   SEM SAP
-- ============================================================

BEGIN;

SET LOCAL search_path TO homologacao, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- ============================================================
-- 0. FAIL-CLOSED ENVIRONMENT / BASELINE GUARDS
-- ============================================================

DO $$
DECLARE
    v_permission_count bigint;
    v_mass_count bigint;
    v_mass_ok boolean;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            '052 ABORTADO: database incorreto: %',
            current_database();
    END IF;

    IF current_schema() <> 'homologacao' THEN
        RAISE EXCEPTION
            '052 ABORTADO: schema incorreto: %',
            current_schema();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION
            '052 ABORTADO: principal administrativo inesperado: %',
            current_user;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = 'fugapet_q_owner'
          AND rolsuper = false
          AND rolcanlogin = false
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: fugapet_q_owner ausente ou fora do baseline aprovado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = 'fugapet_q_app'
          AND rolsuper = false
          AND rolcanlogin = true
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: fugapet_q_app ausente ou fora do baseline aprovado.';
    END IF;

    IF to_regclass('homologacao.entrada_produto_lancamento') IS NULL
       OR to_regclass('homologacao.entrada_produto_item') IS NULL
       OR to_regclass('homologacao.entrada_produto_lote') IS NULL
       OR to_regclass('homologacao.entrada_produto_pesagem') IS NULL
       OR to_regclass('homologacao.integracao_sap_outbox') IS NULL
       OR to_regclass('homologacao.integracao_sap_tentativa') IS NULL
       OR to_regclass('homologacao.log_alteracao_cadastral') IS NULL
       OR to_regclass('homologacao.permissao') IS NULL
       OR to_regclass('homologacao.usuario') IS NULL THEN
        RAISE EXCEPTION
            '052 ABORTADO: objeto obrigatorio ausente.';
    END IF;

    IF NOT has_table_privilege(
        'fugapet_q_owner',
        'homologacao.log_alteracao_cadastral',
        'INSERT'
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: owner sem INSERT em log_alteracao_cadastral.';
    END IF;

    IF NOT has_table_privilege(
        'fugapet_q_owner',
        'homologacao.usuario',
        'SELECT'
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: owner sem SELECT em usuario.';
    END IF;

    IF NOT has_table_privilege(
        'fugapet_q_owner',
        'homologacao.integracao_sap_outbox',
        'SELECT'
    )
    OR NOT has_table_privilege(
        'fugapet_q_owner',
        'homologacao.integracao_sap_tentativa',
        'SELECT'
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: owner sem SELECT em outbox/tentativa.';
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
            '052 ABORTADO: runtime possui DELETE em tabela Entrada.';
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
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: runtime possui SELECT bruto em outbox/tentativa.';
    END IF;

    SELECT count(*)
      INTO v_permission_count
      FROM homologacao.permissao p
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_permission_count > 1 THEN
        RAISE EXCEPTION
            '052 ABORTADO: existem % permissoes EXCLUIR_PESAGEM.',
            v_permission_count;
    END IF;

    IF v_permission_count = 1
       AND NOT EXISTS (
           SELECT 1
             FROM homologacao.permissao p
            WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
              AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
              AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM'
              AND p.situacao_permissao = true
       ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: permissao EXCLUIR_PESAGEM existe inativa; nao reativar implicitamente.';
    END IF;

    SELECT
        count(*),
        bool_and(
            l.numero_pedido = '4500000005'
            AND i.material = '1000111'
            AND lo.numero_lote = '123456789'
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
      INTO v_mass_count, v_mass_ok
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

    IF v_mass_count <> 1 OR COALESCE(v_mass_ok, false) = false THEN
        RAISE EXCEPTION
            '052 ABORTADO: massa operacional 2/2/2/2 divergiu. count=%, ok=%',
            v_mass_count,
            v_mass_ok;
    END IF;
END $$;

-- ============================================================
-- 1. PERMISSAO - IDEMPOTENTE / SEM PROFILE GRANT
-- ============================================================

INSERT INTO homologacao.permissao (
    modulo_permissao,
    rotina_permissao,
    acao_permissao,
    descricao_permissao,
    situacao_permissao
)
SELECT
    'PROCESSO_PRODUCAO',
    'ENTRADA_PRODUTO',
    'EXCLUIR_PESAGEM',
    'Excluir logicamente uma pesagem local da Entrada de Produto.',
    true
WHERE NOT EXISTS (
    SELECT 1
      FROM homologacao.permissao p
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM'
);

DO $$
DECLARE
    v_count bigint;
BEGIN
    SELECT count(*)
      INTO v_count
      FROM homologacao.permissao p
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM'
       AND p.situacao_permissao = true;

    IF v_count <> 1 THEN
        RAISE EXCEPTION
            '052 ABORTADO: permission exact count apos seed = %, esperado 1.',
            v_count;
    END IF;
END $$;

-- ============================================================
-- 2. AUDITORIA GENERICA
--    app.usuario_id, quando definido, e a identidade primaria.
--    Valor definido porem invalido => fail-closed.
-- ============================================================

CREATE OR REPLACE FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO pg_catalog
AS $function$
DECLARE
    v_tabela text := TG_TABLE_NAME;
    v_coluna_codigo text := 'codigo_' || TG_TABLE_NAME;
    v_coluna_situacao text := 'situacao_' || TG_TABLE_NAME;

    v_situacao_anterior boolean;
    v_situacao_nova boolean;

    v_coluna_criado_por_tabela text :=
        TG_TABLE_NAME || '_criado_por';
    v_coluna_atualizado_por_tabela text :=
        TG_TABLE_NAME || '_atualizado_por';
    v_coluna_excluido_por_tabela text :=
        TG_TABLE_NAME || '_excluido_por';

    v_coluna_criado_por text := 'criado_por';
    v_coluna_atualizado_por text := 'atualizado_por';
    v_coluna_excluido_por text := 'excluido_por';

    v_dados_anteriores jsonb;
    v_dados_novos jsonb;
    v_codigo_registro bigint;
    v_codigo_usuario bigint;
    v_app_usuario_raw text;
    v_app_usuario_id bigint;
    v_operacao varchar(20);
BEGIN
    IF TG_TABLE_SCHEMA <> 'homologacao' THEN
        RAISE EXCEPTION
            'AUDITORIA_CONTEXTO_NAO_AUTORIZADO schema=% tabela=% trigger=%',
            TG_TABLE_SCHEMA,
            TG_TABLE_NAME,
            TG_NAME;
    END IF;

    v_app_usuario_raw :=
        NULLIF(pg_catalog.btrim(
            pg_catalog.current_setting('app.usuario_id', true)
        ), '');

    IF v_app_usuario_raw IS NOT NULL THEN
        IF v_app_usuario_raw !~ '^[0-9]+$' THEN
            RAISE EXCEPTION
                'AUDITORIA_APP_USUARIO_ID_INVALIDO valor=%',
                v_app_usuario_raw;
        END IF;

        BEGIN
            v_app_usuario_id := v_app_usuario_raw::bigint;
        EXCEPTION
            WHEN numeric_value_out_of_range THEN
                RAISE EXCEPTION
                    'AUDITORIA_APP_USUARIO_ID_FORA_INTERVALO valor=%',
                    v_app_usuario_raw;
        END;

        IF NOT EXISTS (
            SELECT 1
              FROM homologacao.usuario u
             WHERE u.codigo_usuario = v_app_usuario_id
               AND u.situacao_usuario = true
               AND u.bloqueado_usuario = false
        ) THEN
            RAISE EXCEPTION
                'AUDITORIA_APP_USUARIO_INATIVO_OU_BLOQUEADO codigo_usuario=%',
                v_app_usuario_id;
        END IF;
    END IF;

    IF TG_OP = 'INSERT' THEN
        v_operacao := 'INSERT';
        v_dados_anteriores := NULL;
        v_dados_novos := pg_catalog.to_jsonb(NEW) - 'senha_hash';

    ELSIF TG_OP = 'UPDATE' THEN
        v_dados_anteriores :=
            pg_catalog.to_jsonb(OLD) - 'senha_hash';
        v_dados_novos :=
            pg_catalog.to_jsonb(NEW) - 'senha_hash';

        v_situacao_anterior :=
            NULLIF(
                v_dados_anteriores ->> v_coluna_situacao,
                ''
            )::boolean;

        v_situacao_nova :=
            NULLIF(
                v_dados_novos ->> v_coluna_situacao,
                ''
            )::boolean;

        IF v_situacao_anterior = true
           AND v_situacao_nova = false THEN
            v_operacao := 'DELETE_LOGICO';
        ELSIF v_situacao_anterior = false
              AND v_situacao_nova = true THEN
            v_operacao := 'REATIVACAO';
        ELSE
            v_operacao := 'UPDATE';
        END IF;

    ELSIF TG_OP = 'DELETE' THEN
        v_operacao := 'DELETE_LOGICO';
        v_dados_anteriores :=
            pg_catalog.to_jsonb(OLD) - 'senha_hash';
        v_dados_novos := NULL;

    ELSE
        RETURN COALESCE(NEW, OLD);
    END IF;

    v_codigo_registro := COALESCE(
        NULLIF(
            v_dados_novos ->> v_coluna_codigo,
            ''
        )::bigint,
        NULLIF(
            v_dados_anteriores ->> v_coluna_codigo,
            ''
        )::bigint
    );

    -- app.usuario_id validado possui precedencia absoluta.
    v_codigo_usuario := COALESCE(
        v_app_usuario_id,

        NULLIF(
            v_dados_novos ->> v_coluna_atualizado_por_tabela,
            ''
        )::bigint,
        NULLIF(
            v_dados_novos ->> v_coluna_criado_por_tabela,
            ''
        )::bigint,
        NULLIF(
            v_dados_novos ->> v_coluna_excluido_por_tabela,
            ''
        )::bigint,

        NULLIF(
            v_dados_novos ->> v_coluna_atualizado_por,
            ''
        )::bigint,
        NULLIF(
            v_dados_novos ->> v_coluna_criado_por,
            ''
        )::bigint,
        NULLIF(
            v_dados_novos ->> v_coluna_excluido_por,
            ''
        )::bigint,

        NULLIF(
            v_dados_anteriores ->> v_coluna_atualizado_por_tabela,
            ''
        )::bigint,
        NULLIF(
            v_dados_anteriores ->> v_coluna_criado_por_tabela,
            ''
        )::bigint,
        NULLIF(
            v_dados_anteriores ->> v_coluna_excluido_por_tabela,
            ''
        )::bigint,

        NULLIF(
            v_dados_anteriores ->> v_coluna_atualizado_por,
            ''
        )::bigint,
        NULLIF(
            v_dados_anteriores ->> v_coluna_criado_por,
            ''
        )::bigint,
        NULLIF(
            v_dados_anteriores ->> v_coluna_excluido_por,
            ''
        )::bigint
    );

    IF v_codigo_registro IS NULL THEN
        RETURN COALESCE(NEW, OLD);
    END IF;

    IF v_operacao = 'INSERT' THEN
        INSERT INTO homologacao.log_alteracao_cadastral (
            tabela,
            codigo_registro,
            operacao,
            dados_anteriores,
            dados_novos,
            codigo_usuario,
            log_alteracao_cadastral_criado_por,
            log_alteracao_cadastral_criado_em,
            situacao_log_alteracao_cadastral
        )
        VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            pg_catalog.now(),
            true
        );

    ELSIF v_operacao = 'UPDATE' THEN
        INSERT INTO homologacao.log_alteracao_cadastral (
            tabela,
            codigo_registro,
            operacao,
            dados_anteriores,
            dados_novos,
            codigo_usuario,
            log_alteracao_cadastral_atualizado_por,
            log_alteracao_cadastral_atualizado_em,
            situacao_log_alteracao_cadastral
        )
        VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            pg_catalog.now(),
            true
        );

    ELSIF v_operacao = 'DELETE_LOGICO' THEN
        INSERT INTO homologacao.log_alteracao_cadastral (
            tabela,
            codigo_registro,
            operacao,
            dados_anteriores,
            dados_novos,
            codigo_usuario,
            log_alteracao_cadastral_excluido_por,
            log_alteracao_cadastral_excluido_em,
            situacao_log_alteracao_cadastral
        )
        VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            pg_catalog.now(),
            true
        );

    ELSIF v_operacao = 'REATIVACAO' THEN
        INSERT INTO homologacao.log_alteracao_cadastral (
            tabela,
            codigo_registro,
            operacao,
            dados_anteriores,
            dados_novos,
            codigo_usuario,
            log_alteracao_cadastral_atualizado_por,
            log_alteracao_cadastral_atualizado_em,
            situacao_log_alteracao_cadastral
        )
        VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            pg_catalog.now(),
            true
        );
    END IF;

    RETURN COALESCE(NEW, OLD);
END
$function$;

ALTER FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
    OWNER TO fugapet_q_owner;

REVOKE ALL
ON FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
FROM PUBLIC;

-- Trigger functions are not exposed as direct runtime API.
-- Owner retains implicit EXECUTE.

-- ============================================================
-- 3. AUDITORIA AFTER UPDATE DO LOTE
-- ============================================================

DROP TRIGGER IF EXISTS
    trg_entrada_produto_lote_log_alteracao_cadastral
ON homologacao.entrada_produto_lote;

CREATE TRIGGER trg_entrada_produto_lote_log_alteracao_cadastral
AFTER UPDATE
ON homologacao.entrada_produto_lote
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_registrar_log_alteracao_cadastral();

-- ============================================================
-- 4. SAP GUARD MINIMO
-- ============================================================

CREATE OR REPLACE FUNCTION homologacao.fn_entrada_produto_sap_guard_counts(
    p_correlation_id uuid
)
RETURNS TABLE (
    outbox_matches bigint,
    tentativa_matches bigint
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO pg_catalog, homologacao
AS $function$
BEGIN
    IF p_correlation_id IS NULL THEN
        RAISE EXCEPTION
            'ENTRADA_PRODUTO_SAP_GUARD_CORRELATION_ID_OBRIGATORIO';
    END IF;

    RETURN QUERY
    SELECT
        (
            SELECT pg_catalog.count(*)
            FROM homologacao.integracao_sap_outbox o
            WHERE o.correlation_id = p_correlation_id
        )::bigint,
        (
            SELECT pg_catalog.count(*)
            FROM homologacao.integracao_sap_tentativa t
            JOIN homologacao.integracao_sap_outbox o
              ON o.codigo_outbox = t.codigo_outbox
            WHERE o.correlation_id = p_correlation_id
        )::bigint;
END
$function$;

ALTER FUNCTION homologacao.fn_entrada_produto_sap_guard_counts(uuid)
    OWNER TO fugapet_q_owner;

REVOKE ALL
ON FUNCTION homologacao.fn_entrada_produto_sap_guard_counts(uuid)
FROM PUBLIC;

GRANT EXECUTE
ON FUNCTION homologacao.fn_entrada_produto_sap_guard_counts(uuid)
TO fugapet_q_app;

-- ============================================================
-- 5. FINAL FAIL-CLOSED VALIDATION INSIDE SAME TRANSACTION
-- ============================================================

DO $$
DECLARE
    v_permission_count bigint;
    v_lote_trigger_count bigint;
    v_mass_count bigint;
    v_mass_ok boolean;
    v_guard_owner text;
    v_guard_security_definer boolean;
    v_guard_search_path text[];
    v_audit_owner text;
    v_audit_security_definer boolean;
    v_audit_search_path text[];
BEGIN
    SELECT count(*)
      INTO v_permission_count
      FROM homologacao.permissao p
     WHERE upper(btrim(p.modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(p.rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(p.acao_permissao)) = 'EXCLUIR_PESAGEM'
       AND p.situacao_permissao = true;

    IF v_permission_count <> 1 THEN
        RAISE EXCEPTION
            '052 ABORTADO: permission exact count final = %, esperado 1.',
            v_permission_count;
    END IF;

    SELECT count(*)
      INTO v_lote_trigger_count
      FROM pg_trigger t
      JOIN pg_class c
        ON c.oid = t.tgrelid
      JOIN pg_namespace n
        ON n.oid = c.relnamespace
     WHERE n.nspname = 'homologacao'
       AND c.relname = 'entrada_produto_lote'
       AND t.tgname =
           'trg_entrada_produto_lote_log_alteracao_cadastral'
       AND NOT t.tgisinternal
       AND (t.tgtype & 16) = 16;

    IF v_lote_trigger_count <> 1 THEN
        RAISE EXCEPTION
            '052 ABORTADO: trigger de auditoria do lote ausente/duplicado.';
    END IF;

    SELECT
        pg_get_userbyid(p.proowner),
        p.prosecdef,
        p.proconfig
      INTO
        v_audit_owner,
        v_audit_security_definer,
        v_audit_search_path
      FROM pg_proc p
      JOIN pg_namespace n
        ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname =
           'fn_registrar_log_alteracao_cadastral'
       AND p.prokind = 'f'
       AND pg_get_function_identity_arguments(p.oid) = '';

    IF v_audit_owner <> 'fugapet_q_owner'
       OR v_audit_security_definer IS DISTINCT FROM true
       OR v_audit_search_path IS NULL
       OR NOT (
           'search_path=pg_catalog' =
           ANY(v_audit_search_path)
       ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: hardening da auditoria divergente. owner=%, secdef=%, config=%',
            v_audit_owner,
            v_audit_security_definer,
            v_audit_search_path;
    END IF;

    IF has_function_privilege(
        'fugapet_q_app',
        'homologacao.fn_registrar_log_alteracao_cadastral()',
        'EXECUTE'
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: runtime possui EXECUTE direto na trigger function generica.';
    END IF;

    SELECT
        pg_get_userbyid(p.proowner),
        p.prosecdef,
        p.proconfig
      INTO
        v_guard_owner,
        v_guard_security_definer,
        v_guard_search_path
      FROM pg_proc p
      JOIN pg_namespace n
        ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname =
           'fn_entrada_produto_sap_guard_counts'
       AND p.prokind = 'f'
       AND pg_get_function_identity_arguments(p.oid) =
           'p_correlation_id uuid';

    IF v_guard_owner <> 'fugapet_q_owner'
       OR v_guard_security_definer IS DISTINCT FROM true
       OR v_guard_search_path IS NULL
       OR NOT (
           'search_path=pg_catalog, homologacao' =
           ANY(v_guard_search_path)
       ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: hardening SAP guard divergente. owner=%, secdef=%, config=%',
            v_guard_owner,
            v_guard_security_definer,
            v_guard_search_path;
    END IF;

    IF NOT has_function_privilege(
        'fugapet_q_app',
        'homologacao.fn_entrada_produto_sap_guard_counts(uuid)',
        'EXECUTE'
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: runtime sem EXECUTE no SAP guard.';
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
    ) THEN
        RAISE EXCEPTION
            '052 ABORTADO: runtime recebeu SELECT bruto em SAP tables.';
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
            '052 ABORTADO: DELETE privilege apareceu no runtime.';
    END IF;

    SELECT
        count(*),
        bool_and(
            l.numero_pedido = '4500000005'
            AND i.material = '1000111'
            AND lo.numero_lote = '123456789'
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
      INTO v_mass_count, v_mass_ok
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

    IF v_mass_count <> 1 OR COALESCE(v_mass_ok, false) = false THEN
        RAISE EXCEPTION
            '052 ABORTADO: massa 2/2/2/2 alterada dentro da migration.';
    END IF;
END $$;

COMMIT;

\echo '============================================================'
\echo '052_PROPOSTA_APLICADA'
\echo '============================================================'
\echo 'PROFILE_GRANT_CREATED=NAO'
\echo 'DELETE_PRIVILEGE_GRANTED=NAO'
\echo 'OUTBOX_PAYLOAD_EXPOSED=NAO'
\echo 'MASSA_2_2_2_2_CHANGED=NAO'
\echo 'SAP_WRITE=0'
