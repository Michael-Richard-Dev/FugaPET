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
-- 026_h26_cadastro_setor_regras_banco_HML_PROPOSTA_GAIA.sql
-- Projeto FugaPET_HML
-- Banco PostgreSQL - Schema homologacao
-- Objeto: Cadastro de Setor
--
-- OBJETIVO
--   Incremental 026 para endurecer regras de banco do Cadastro de Setor
--   em HOMOLOGACAO, sem alterar diretamente a baseline.
--
-- IMPORTANTE
--   1. NAO executar em PRODUCAO.
--   2. Executar primeiro o preflight 026.
--   3. Executar dentro de janela controlada e com snapshot/backup.
--   4. Este script bloqueia inativacao logica de setor em uso.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

-- ============================================================
-- 1. Preflight bloqueante de dados atuais
-- ============================================================

DO $$
DECLARE
    v_qtd integer;
BEGIN
    SELECT count(*) INTO v_qtd
      FROM homologacao.setor
     WHERE nome_setor IS NULL
        OR char_length(trim(nome_setor)) < 2
        OR char_length(trim(nome_setor)) > 80;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % setores com nome_setor fora do limite funcional 2..80.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.setor
     WHERE descricao_setor IS NOT NULL
       AND char_length(trim(descricao_setor)) > 255;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % setores com descricao_setor maior que 255 caracteres.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM (
            SELECT upper(trim(nome_setor)) AS nome_normalizado, count(*) AS qtd
              FROM homologacao.setor
             WHERE situacao_setor = true
             GROUP BY upper(trim(nome_setor))
            HAVING count(*) > 1
           ) d;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % nomes de setor ativos duplicados por upper(trim(nome_setor)).', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.usuario u
      JOIN homologacao.setor s ON s.codigo_setor = u.codigo_setor_padrao
     WHERE u.situacao_usuario = true
       AND s.situacao_setor = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % usuarios ativos com setor padrao inativo.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.usuario_setor us
      JOIN homologacao.setor s ON s.codigo_setor = us.codigo_setor
     WHERE us.situacao_usuario_setor = true
       AND s.situacao_setor = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % vinculos usuario_setor ativos com setor inativo.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.balanca b
      JOIN homologacao.setor s ON s.codigo_setor = b.codigo_setor
     WHERE b.situacao_balanca = true
       AND s.situacao_setor = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas ativas vinculadas a setor inativo.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.tara t
      JOIN homologacao.setor s ON s.codigo_setor = t.codigo_setor
     WHERE t.situacao_tara = true
       AND s.situacao_setor = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % taras ativas vinculadas a setor inativo.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.parametro_operacao po
      JOIN homologacao.setor s ON s.codigo_setor = po.codigo_setor
     WHERE po.situacao_parametro_operacao = true
       AND s.situacao_setor = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % parametros operacionais ativos vinculados a setor inativo.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.entrada_produto_lancamento epl
      JOIN homologacao.setor s ON s.codigo_setor = epl.codigo_setor
     WHERE epl.situacao_entrada_produto_lancamento = true
       AND epl.status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO')
       AND s.situacao_setor = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % lancamentos operacionais ativos com setor inativo.', v_qtd;
    END IF;
END $$;

-- ============================================================
-- 2. CHECKs funcionais de tamanho
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_setor_nome_tamanho_funcional'
           AND conrelid = 'homologacao.setor'::regclass
    ) THEN
        ALTER TABLE homologacao.setor
            ADD CONSTRAINT ck_setor_nome_tamanho_funcional
            CHECK (char_length(trim(nome_setor)) BETWEEN 2 AND 80)
            NOT VALID;

        ALTER TABLE homologacao.setor
            VALIDATE CONSTRAINT ck_setor_nome_tamanho_funcional;
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_setor_descricao_tamanho_funcional'
           AND conrelid = 'homologacao.setor'::regclass
    ) THEN
        ALTER TABLE homologacao.setor
            ADD CONSTRAINT ck_setor_descricao_tamanho_funcional
            CHECK (descricao_setor IS NULL OR char_length(trim(descricao_setor)) <= 255)
            NOT VALID;

        ALTER TABLE homologacao.setor
            VALIDATE CONSTRAINT ck_setor_descricao_tamanho_funcional;
    END IF;
END $$;

COMMENT ON CONSTRAINT ck_setor_nome_tamanho_funcional ON homologacao.setor IS
    'Limite funcional: nome_setor deve ter de 2 a 80 caracteres uteis apos trim.';

COMMENT ON CONSTRAINT ck_setor_descricao_tamanho_funcional ON homologacao.setor IS
    'Limite funcional: descricao_setor deve ter ate 255 caracteres uteis apos trim.';

-- ============================================================
-- 3. Funcao de diagnostico de dependencias ativas do setor
-- ============================================================

CREATE OR REPLACE FUNCTION homologacao.fn_setor_dependencias_ativas(p_codigo_setor bigint)
RETURNS TABLE (
    tipo_dependencia text,
    quantidade bigint
)
LANGUAGE sql
STABLE
AS $$
    SELECT 'USUARIO_SETOR_PADRAO_ATIVO'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM homologacao.usuario u
     WHERE u.codigo_setor_padrao = p_codigo_setor
       AND u.situacao_usuario = true

    UNION ALL

    SELECT 'USUARIO_SETOR_ATIVO'::text,
           count(*)::bigint
      FROM homologacao.usuario_setor us
     WHERE us.codigo_setor = p_codigo_setor
       AND us.situacao_usuario_setor = true

    UNION ALL

    SELECT 'BALANCA_ATIVA'::text,
           count(*)::bigint
      FROM homologacao.balanca b
     WHERE b.codigo_setor = p_codigo_setor
       AND b.situacao_balanca = true

    UNION ALL

    SELECT 'TARA_ATIVA'::text,
           count(*)::bigint
      FROM homologacao.tara t
     WHERE t.codigo_setor = p_codigo_setor
       AND t.situacao_tara = true

    UNION ALL

    SELECT 'PARAMETRO_OPERACAO_ATIVO'::text,
           count(*)::bigint
      FROM homologacao.parametro_operacao po
     WHERE po.codigo_setor = p_codigo_setor
       AND po.situacao_parametro_operacao = true

    UNION ALL

    SELECT 'ENTRADA_PRODUTO_LANCAMENTO_OPERACIONAL'::text,
           count(*)::bigint
      FROM homologacao.entrada_produto_lancamento epl
     WHERE epl.codigo_setor = p_codigo_setor
       AND epl.situacao_entrada_produto_lancamento = true
       AND epl.status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO');
$$;

COMMENT ON FUNCTION homologacao.fn_setor_dependencias_ativas(bigint) IS
    'Retorna contadores de vinculos ativos/operacionais que impedem inativacao logica de setor.';

-- ============================================================
-- 4. View de diagnostico para Service/tela antes de inativar
-- ============================================================

CREATE OR REPLACE VIEW homologacao.vw_setor_diagnostico_inativacao AS
SELECT
    s.codigo_setor,
    s.nome_setor,
    s.situacao_setor,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'USUARIO_SETOR_PADRAO_ATIVO'), 0)::bigint AS usuarios_ativos_setor_padrao,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'USUARIO_SETOR_ATIVO'), 0)::bigint AS usuario_setor_ativos,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'BALANCA_ATIVA'), 0)::bigint AS balancas_ativas,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'TARA_ATIVA'), 0)::bigint AS taras_ativas,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'PARAMETRO_OPERACAO_ATIVO'), 0)::bigint AS parametros_operacao_ativos,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'ENTRADA_PRODUTO_LANCAMENTO_OPERACIONAL'), 0)::bigint AS lancamentos_operacionais,
    COALESCE(sum(d.quantidade), 0)::bigint AS total_dependencias_ativas,
    (COALESCE(sum(d.quantidade), 0) = 0) AS pode_inativar
FROM homologacao.setor s
LEFT JOIN LATERAL homologacao.fn_setor_dependencias_ativas(s.codigo_setor) d ON true
GROUP BY
    s.codigo_setor,
    s.nome_setor,
    s.situacao_setor;

COMMENT ON VIEW homologacao.vw_setor_diagnostico_inativacao IS
    'Diagnostico de dependencias que impedem inativacao logica de setor.';

-- ============================================================
-- 5. Trigger de bloqueio de inativacao logica em uso
-- ============================================================

CREATE OR REPLACE FUNCTION homologacao.fn_bloquear_inativacao_setor_em_uso()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_dependencias text;
BEGIN
    IF OLD.situacao_setor = true AND NEW.situacao_setor = false THEN
        SELECT string_agg(format('%s=%s', tipo_dependencia, quantidade), ', ' ORDER BY tipo_dependencia)
          INTO v_dependencias
          FROM homologacao.fn_setor_dependencias_ativas(NEW.codigo_setor)
         WHERE quantidade > 0;

        IF v_dependencias IS NOT NULL THEN
            RAISE EXCEPTION 'Setor "%" nao pode ser inativado porque possui dependencias ativas: %.',
                NEW.nome_setor,
                v_dependencias
            USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION homologacao.fn_bloquear_inativacao_setor_em_uso() IS
    'Bloqueia UPDATE que tente inativar setor com dependencias ativas/operacionais.';

DROP TRIGGER IF EXISTS trg_setor_bloquear_inativacao_em_uso ON homologacao.setor;

CREATE TRIGGER trg_setor_bloquear_inativacao_em_uso
BEFORE UPDATE OF situacao_setor ON homologacao.setor
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_bloquear_inativacao_setor_em_uso();

COMMIT;

SELECT *
FROM homologacao.vw_setor_diagnostico_inativacao
ORDER BY nome_setor;
