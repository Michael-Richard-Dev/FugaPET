-- ============================================================
-- 026_h26_cadastro_setor_regras_banco_PROPOSTA_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Setor
--
-- OBJETIVO
--   Incremental proposto por Gaia Dados para endurecer regras de banco do
--   Cadastro de Setor sem alterar diretamente a baseline V1.1.
--
-- IMPORTANTE
--   1. Proposta para revisão. NAO executar automaticamente.
--   2. Executar primeiro em DESENVOLVIMENTO.
--   3. Executar dentro de janela controlada e com backup/snapshot.
--   4. Este script bloqueia inativacao logica de setor em uso.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

-- ============================================================
-- 1. Preflight de dados atuais
-- ============================================================

DO $$
DECLARE
    v_qtd integer;
BEGIN
    SELECT count(*)
      INTO v_qtd
      FROM desenvolvimento.setor
     WHERE nome_setor IS NULL
        OR char_length(trim(nome_setor)) < 2
        OR char_length(trim(nome_setor)) > 80;

    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % setores com nome_setor fora do limite funcional 2..80.', v_qtd;
    END IF;

    SELECT count(*)
      INTO v_qtd
      FROM desenvolvimento.setor
     WHERE descricao_setor IS NOT NULL
       AND char_length(trim(descricao_setor)) > 255;

    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % setores com descricao_setor maior que 255 caracteres.', v_qtd;
    END IF;
END $$;

-- ============================================================
-- 2. CHECKs funcionais de tamanho
--
-- Decisao proposta:
--   - Validar no Service/View para mensagem amigavel.
--   - Garantir no banco para impedir bypass por SQL/importacao.
--
-- Limites propostos:
--   - nome_setor: 2 a 80 caracteres uteis apos trim.
--   - descricao_setor: ate 255 caracteres uteis apos trim.
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_setor_nome_tamanho_funcional'
           AND conrelid = 'desenvolvimento.setor'::regclass
    ) THEN
        ALTER TABLE desenvolvimento.setor
            ADD CONSTRAINT ck_setor_nome_tamanho_funcional
            CHECK (char_length(trim(nome_setor)) BETWEEN 2 AND 80)
            NOT VALID;

        ALTER TABLE desenvolvimento.setor
            VALIDATE CONSTRAINT ck_setor_nome_tamanho_funcional;
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_setor_descricao_tamanho_funcional'
           AND conrelid = 'desenvolvimento.setor'::regclass
    ) THEN
        ALTER TABLE desenvolvimento.setor
            ADD CONSTRAINT ck_setor_descricao_tamanho_funcional
            CHECK (descricao_setor IS NULL OR char_length(trim(descricao_setor)) <= 255)
            NOT VALID;

        ALTER TABLE desenvolvimento.setor
            VALIDATE CONSTRAINT ck_setor_descricao_tamanho_funcional;
    END IF;
END $$;

COMMENT ON CONSTRAINT ck_setor_nome_tamanho_funcional ON desenvolvimento.setor IS
    'Limite funcional: nome_setor deve ter de 2 a 80 caracteres uteis apos trim.';

COMMENT ON CONSTRAINT ck_setor_descricao_tamanho_funcional ON desenvolvimento.setor IS
    'Limite funcional: descricao_setor deve ter ate 255 caracteres uteis apos trim.';

-- ============================================================
-- 3. Funcao de diagnostico de dependencias ativas do setor
-- ============================================================

CREATE OR REPLACE FUNCTION desenvolvimento.fn_setor_dependencias_ativas(p_codigo_setor bigint)
RETURNS TABLE (
    tipo_dependencia text,
    quantidade bigint
)
LANGUAGE sql
STABLE
AS $$
    SELECT 'USUARIO_SETOR_PADRAO_ATIVO'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.usuario u
     WHERE u.codigo_setor_padrao = p_codigo_setor
       AND u.situacao_usuario = true

    UNION ALL

    SELECT 'USUARIO_SETOR_ATIVO'::text,
           count(*)::bigint
      FROM desenvolvimento.usuario_setor us
     WHERE us.codigo_setor = p_codigo_setor
       AND us.situacao_usuario_setor = true

    UNION ALL

    SELECT 'BALANCA_ATIVA'::text,
           count(*)::bigint
      FROM desenvolvimento.balanca b
     WHERE b.codigo_setor = p_codigo_setor
       AND b.situacao_balanca = true

    UNION ALL

    SELECT 'TARA_ATIVA'::text,
           count(*)::bigint
      FROM desenvolvimento.tara t
     WHERE t.codigo_setor = p_codigo_setor
       AND t.situacao_tara = true

    UNION ALL

    SELECT 'PARAMETRO_OPERACAO_ATIVO'::text,
           count(*)::bigint
      FROM desenvolvimento.parametro_operacao po
     WHERE po.codigo_setor = p_codigo_setor
       AND po.situacao_parametro_operacao = true

    UNION ALL

    SELECT 'ENTRADA_PRODUTO_LANCAMENTO_OPERACIONAL'::text,
           count(*)::bigint
      FROM desenvolvimento.entrada_produto_lancamento epl
     WHERE epl.codigo_setor = p_codigo_setor
       AND epl.situacao_entrada_produto_lancamento = true
       AND epl.status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO');
$$;

COMMENT ON FUNCTION desenvolvimento.fn_setor_dependencias_ativas(bigint) IS
    'Retorna contadores de vinculos ativos/operacionais que impedem inativacao logica de setor.';

-- ============================================================
-- 4. View de diagnostico para Service/tela antes de inativar
-- ============================================================

CREATE OR REPLACE VIEW desenvolvimento.vw_setor_diagnostico_inativacao AS
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
FROM desenvolvimento.setor s
LEFT JOIN LATERAL desenvolvimento.fn_setor_dependencias_ativas(s.codigo_setor) d ON true
GROUP BY
    s.codigo_setor,
    s.nome_setor,
    s.situacao_setor;

COMMENT ON VIEW desenvolvimento.vw_setor_diagnostico_inativacao IS
    'Diagnostico de dependencias que impedem inativacao logica de setor.';

-- ============================================================
-- 5. Trigger de bloqueio de inativacao logica em uso
-- ============================================================

CREATE OR REPLACE FUNCTION desenvolvimento.fn_bloquear_inativacao_setor_em_uso()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_dependencias text;
BEGIN
    IF OLD.situacao_setor = true AND NEW.situacao_setor = false THEN
        SELECT string_agg(format('%s=%s', tipo_dependencia, quantidade), ', ' ORDER BY tipo_dependencia)
          INTO v_dependencias
          FROM desenvolvimento.fn_setor_dependencias_ativas(NEW.codigo_setor)
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

COMMENT ON FUNCTION desenvolvimento.fn_bloquear_inativacao_setor_em_uso() IS
    'Bloqueia UPDATE que tente inativar setor com dependencias ativas/operacionais.';

DROP TRIGGER IF EXISTS trg_setor_bloquear_inativacao_em_uso ON desenvolvimento.setor;

CREATE TRIGGER trg_setor_bloquear_inativacao_em_uso
BEFORE UPDATE OF situacao_setor ON desenvolvimento.setor
FOR EACH ROW
EXECUTE FUNCTION desenvolvimento.fn_bloquear_inativacao_setor_em_uso();

COMMIT;

-- ============================================================
-- Consultas pos-aplicacao sugeridas
-- ============================================================

SELECT *
FROM desenvolvimento.vw_setor_diagnostico_inativacao
ORDER BY nome_setor;
