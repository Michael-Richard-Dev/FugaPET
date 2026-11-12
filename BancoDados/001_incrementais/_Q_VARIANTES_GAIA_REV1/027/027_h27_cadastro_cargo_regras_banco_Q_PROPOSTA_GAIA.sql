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
-- 027_h27_cadastro_cargo_regras_banco_HML_PROPOSTA_GAIA.sql
-- Projeto FugaPET_HML
-- Banco PostgreSQL - Banco alvo: fuga_jales_local_homologacao_q_v1_2
-- Schema alvo: homologacao
-- Objeto: Cadastro de Cargo
--
-- OBJETIVO
--   Incremental proposto por Gaia Dados para endurecer regras de banco do
--   Cadastro de Cargo em HML sem alterar diretamente a baseline.
--
-- DECISAO FUNCIONAL APROVADA
--   - nome_cargo: minimo 2 e maximo 80 caracteres uteis apos trim.
--   - descricao_cargo: maximo 255 caracteres uteis apos trim.
--   - cargo em uso por usuario ativo nao pode ser inativado.
--   - aplicacao deve validar antes, mas o banco deve bloquear bypass por SQL direto.
--
-- IMPORTANTE
--   1. Script especifico para HML/schema homologacao.
--   2. NAO executar em producao.
--   3. Executar primeiro o preflight HML.
--   4. Executar dentro de janela controlada e com backup/snapshot.
--   5. Nao altera dados existentes.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

-- ============================================================
-- 1. Preflight de dados atuais
-- ============================================================

DO $$
DECLARE
    v_qtd integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM information_schema.schemata
         WHERE schema_name = 'homologacao'
    ) THEN
        RAISE EXCEPTION 'Schema homologacao nao existe.';
    END IF;

    IF to_regclass('homologacao.cargo') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.cargo nao existe.';
    END IF;

    IF to_regclass('homologacao.usuario') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.usuario nao existe.';
    END IF;

    SELECT count(*)
      INTO v_qtd
      FROM homologacao.cargo
     WHERE nome_cargo IS NULL
        OR char_length(trim(nome_cargo)) < 2
        OR char_length(trim(nome_cargo)) > 80;

    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % cargos com nome_cargo fora do limite funcional 2..80.', v_qtd;
    END IF;

    SELECT count(*)
      INTO v_qtd
      FROM homologacao.cargo
     WHERE descricao_cargo IS NOT NULL
       AND char_length(trim(descricao_cargo)) > 255;

    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % cargos com descricao_cargo maior que 255 caracteres.', v_qtd;
    END IF;

    SELECT count(*)
      INTO v_qtd
      FROM (
            SELECT upper(trim(nome_cargo)) AS nome_normalizado
              FROM homologacao.cargo
             WHERE situacao_cargo = true
             GROUP BY upper(trim(nome_cargo))
            HAVING count(*) > 1
      ) duplicados;

    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % nomes de cargo ativos duplicados por upper(trim(nome_cargo)).', v_qtd;
    END IF;

    SELECT count(*)
      INTO v_qtd
      FROM homologacao.usuario u
      JOIN homologacao.cargo c ON c.codigo_cargo = u.codigo_cargo
     WHERE u.situacao_usuario = true
       AND c.situacao_cargo = false;

    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % usuarios ativos vinculados a cargos inativos.', v_qtd;
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
         WHERE conname = 'ck_cargo_nome_tamanho_funcional'
           AND conrelid = 'homologacao.cargo'::regclass
    ) THEN
        ALTER TABLE homologacao.cargo
            ADD CONSTRAINT ck_cargo_nome_tamanho_funcional
            CHECK (char_length(trim(nome_cargo)) BETWEEN 2 AND 80)
            NOT VALID;

        ALTER TABLE homologacao.cargo
            VALIDATE CONSTRAINT ck_cargo_nome_tamanho_funcional;
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_cargo_descricao_tamanho_funcional'
           AND conrelid = 'homologacao.cargo'::regclass
    ) THEN
        ALTER TABLE homologacao.cargo
            ADD CONSTRAINT ck_cargo_descricao_tamanho_funcional
            CHECK (descricao_cargo IS NULL OR char_length(trim(descricao_cargo)) <= 255)
            NOT VALID;

        ALTER TABLE homologacao.cargo
            VALIDATE CONSTRAINT ck_cargo_descricao_tamanho_funcional;
    END IF;
END $$;

COMMENT ON CONSTRAINT ck_cargo_nome_tamanho_funcional ON homologacao.cargo IS
    'Limite funcional: nome_cargo deve ter de 2 a 80 caracteres uteis apos trim.';

COMMENT ON CONSTRAINT ck_cargo_descricao_tamanho_funcional ON homologacao.cargo IS
    'Limite funcional: descricao_cargo deve ter ate 255 caracteres uteis apos trim.';

-- ============================================================
-- 3. Funcao de diagnostico de dependencias ativas do cargo
-- ============================================================

CREATE OR REPLACE FUNCTION homologacao.fn_cargo_dependencias_ativas(p_codigo_cargo bigint)
RETURNS TABLE (
    tipo_dependencia text,
    quantidade bigint
)
LANGUAGE sql
STABLE
AS $$
    SELECT 'USUARIO_ATIVO'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM homologacao.usuario u
     WHERE u.codigo_cargo = p_codigo_cargo
       AND u.situacao_usuario = true;
$$;

COMMENT ON FUNCTION homologacao.fn_cargo_dependencias_ativas(bigint) IS
    'Retorna contadores de vinculos ativos que impedem inativacao logica de cargo.';

-- ============================================================
-- 4. View de diagnostico para Service/tela antes de inativar
-- ============================================================

CREATE OR REPLACE VIEW homologacao.vw_cargo_diagnostico_inativacao AS
SELECT
    c.codigo_cargo,
    c.nome_cargo,
    c.situacao_cargo,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'USUARIO_ATIVO'), 0)::bigint AS usuarios_ativos,
    COALESCE(sum(d.quantidade), 0)::bigint AS total_dependencias_ativas,
    (COALESCE(sum(d.quantidade), 0) = 0) AS pode_inativar
FROM homologacao.cargo c
LEFT JOIN LATERAL homologacao.fn_cargo_dependencias_ativas(c.codigo_cargo) d ON true
GROUP BY
    c.codigo_cargo,
    c.nome_cargo,
    c.situacao_cargo;

COMMENT ON VIEW homologacao.vw_cargo_diagnostico_inativacao IS
    'Diagnostico de dependencias que impedem inativacao logica de cargo.';

-- ============================================================
-- 5. Trigger de bloqueio de inativacao logica em uso
-- ============================================================

CREATE OR REPLACE FUNCTION homologacao.fn_cargo_bloquear_inativacao_em_uso()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_dependencias text;
BEGIN
    IF OLD.situacao_cargo = true AND NEW.situacao_cargo = false THEN
        SELECT string_agg(format('%s=%s', tipo_dependencia, quantidade), ', ' ORDER BY tipo_dependencia)
          INTO v_dependencias
          FROM homologacao.fn_cargo_dependencias_ativas(NEW.codigo_cargo)
         WHERE quantidade > 0;

        IF v_dependencias IS NOT NULL THEN
            RAISE EXCEPTION 'Cargo "%" nao pode ser inativado porque possui dependencias ativas: %.',
                NEW.nome_cargo,
                v_dependencias
            USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION homologacao.fn_cargo_bloquear_inativacao_em_uso() IS
    'Bloqueia UPDATE que tente inativar cargo com usuarios ativos vinculados.';

DROP TRIGGER IF EXISTS trg_cargo_bloquear_inativacao_em_uso ON homologacao.cargo;

CREATE TRIGGER trg_cargo_bloquear_inativacao_em_uso
BEFORE UPDATE OF situacao_cargo ON homologacao.cargo
FOR EACH ROW
EXECUTE FUNCTION homologacao.fn_cargo_bloquear_inativacao_em_uso();

COMMIT;

-- ============================================================
-- Consultas pos-aplicacao sugeridas
-- ============================================================

SELECT *
FROM homologacao.vw_cargo_diagnostico_inativacao
ORDER BY nome_cargo;
