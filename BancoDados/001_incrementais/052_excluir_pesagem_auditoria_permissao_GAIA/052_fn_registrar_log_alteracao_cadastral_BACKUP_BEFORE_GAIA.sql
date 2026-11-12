CREATE OR REPLACE FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_tabela text := TG_TABLE_NAME;
    v_coluna_codigo text := 'codigo_' || TG_TABLE_NAME;

    -- coluna de situação dinâmica: situacao_<tabela>
    v_coluna_situacao text := 'situacao_' || TG_TABLE_NAME;
    v_situacao_anterior boolean;
    v_situacao_nova boolean;

    -- padrão por tabela
    v_coluna_criado_por_tabela text := TG_TABLE_NAME || '_criado_por';
    v_coluna_atualizado_por_tabela text := TG_TABLE_NAME || '_atualizado_por';
    v_coluna_excluido_por_tabela text := TG_TABLE_NAME || '_excluido_por';

    -- padrão legado
    v_coluna_criado_por text := 'criado_por';
    v_coluna_atualizado_por text := 'atualizado_por';
    v_coluna_excluido_por text := 'excluido_por';

    v_dados_anteriores jsonb;
    v_dados_novos jsonb;
    v_codigo_registro bigint;
    v_codigo_usuario bigint;
    v_operacao varchar(20);
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_operacao := 'INSERT';
        v_dados_anteriores := NULL;
        v_dados_novos := to_jsonb(NEW) - 'senha_hash';

    ELSIF TG_OP = 'UPDATE' THEN
        v_dados_anteriores := to_jsonb(OLD) - 'senha_hash';
        v_dados_novos := to_jsonb(NEW) - 'senha_hash';

        v_situacao_anterior := NULLIF(to_jsonb(OLD) ->> v_coluna_situacao, '')::boolean;
        v_situacao_nova := NULLIF(to_jsonb(NEW) ->> v_coluna_situacao, '')::boolean;

        IF v_situacao_anterior = true AND v_situacao_nova = false THEN
            v_operacao := 'DELETE_LOGICO';

        ELSIF v_situacao_anterior = false AND v_situacao_nova = true THEN
            v_operacao := 'REATIVACAO';

        ELSE
            v_operacao := 'UPDATE';
        END IF;

    ELSIF TG_OP = 'DELETE' THEN
        v_operacao := 'DELETE_LOGICO';
        v_dados_anteriores := to_jsonb(OLD) - 'senha_hash';
        v_dados_novos := NULL;

    ELSE
        RETURN COALESCE(NEW, OLD);
    END IF;

    v_codigo_registro := COALESCE(
        NULLIF(v_dados_novos ->> v_coluna_codigo, '')::bigint,
        NULLIF(v_dados_anteriores ->> v_coluna_codigo, '')::bigint
    );

    v_codigo_usuario := COALESCE(
        NULLIF(v_dados_novos ->> v_coluna_atualizado_por_tabela, '')::bigint,
        NULLIF(v_dados_novos ->> v_coluna_criado_por_tabela, '')::bigint,
        NULLIF(v_dados_novos ->> v_coluna_excluido_por_tabela, '')::bigint,
        NULLIF(v_dados_novos ->> v_coluna_atualizado_por, '')::bigint,
        NULLIF(v_dados_novos ->> v_coluna_criado_por, '')::bigint,
        NULLIF(v_dados_novos ->> v_coluna_excluido_por, '')::bigint,

        NULLIF(v_dados_anteriores ->> v_coluna_atualizado_por_tabela, '')::bigint,
        NULLIF(v_dados_anteriores ->> v_coluna_criado_por_tabela, '')::bigint,
        NULLIF(v_dados_anteriores ->> v_coluna_excluido_por_tabela, '')::bigint,
        NULLIF(v_dados_anteriores ->> v_coluna_atualizado_por, '')::bigint,
        NULLIF(v_dados_anteriores ->> v_coluna_criado_por, '')::bigint,
        NULLIF(v_dados_anteriores ->> v_coluna_excluido_por, '')::bigint
    );

    -- fallback para DELETE físico ou operação sem usuário explícito
    v_codigo_usuario := COALESCE(
        v_codigo_usuario,
        NULLIF(current_setting('app.usuario_id', true), '')::bigint
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
        ) VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            now(),
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
        ) VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            now(),
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
        ) VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            now(),
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
        ) VALUES (
            v_tabela,
            v_codigo_registro,
            v_operacao,
            v_dados_anteriores,
            v_dados_novos,
            v_codigo_usuario,
            v_codigo_usuario,
            now(),
            true
        );
    END IF;

    RETURN COALESCE(NEW, OLD);
END;
$function$


-- ============================================================
-- BASELINE SECURITY / OWNER / ACL
-- Capturado antes do Incremento 052
-- ============================================================

ALTER FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
    SECURITY INVOKER;

ALTER FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
    RESET ALL;

ALTER FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
    OWNER TO fugapet_q_owner;

GRANT EXECUTE
ON FUNCTION homologacao.fn_registrar_log_alteracao_cadastral()
TO PUBLIC;

