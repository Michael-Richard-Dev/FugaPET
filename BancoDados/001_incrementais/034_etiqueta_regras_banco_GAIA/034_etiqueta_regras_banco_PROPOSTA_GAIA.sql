-- ============================================================
-- 034_etiqueta_regras_banco_PROPOSTA_GAIA.sql
-- PROPOSTA - NAO EXECUTAR
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Etiqueta
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

DO $$
DECLARE
    v_dups integer;
    v_invalidos integer;
BEGIN
    IF to_regclass('desenvolvimento.etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.etiqueta nao existe.';
    END IF;
    IF to_regclass('desenvolvimento.produto_etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.produto_etiqueta nao existe.';
    END IF;

    SELECT count(*) INTO v_dups
      FROM (
        SELECT upper(trim(codigo_interno))
          FROM desenvolvimento.etiqueta
         GROUP BY upper(trim(codigo_interno))
        HAVING count(*) > 1
      ) d;
    IF v_dups > 0 THEN
        RAISE EXCEPTION 'Existem % codigos internos de etiqueta duplicados, incluindo ativos e inativos. Resolva antes.', v_dups;
    END IF;

    SELECT count(*) INTO v_invalidos
      FROM desenvolvimento.etiqueta
     WHERE coalesce(trim(codigo_interno), '') = ''
        OR char_length(trim(codigo_interno)) > 80
        OR char_length(trim(nome_etiqueta)) < 2
        OR char_length(trim(nome_etiqueta)) > 80
        OR char_length(coalesce(trim(descricao_etiqueta), '')) > 255;
    IF v_invalidos > 0 THEN
        RAISE EXCEPTION 'Existem % etiquetas com codigo/nome/descricao fora das regras propostas.', v_invalidos;
    END IF;
END $$;

ALTER TABLE desenvolvimento.etiqueta
    DROP CONSTRAINT IF EXISTS ck_etiqueta_codigo_nao_vazio;

ALTER TABLE desenvolvimento.etiqueta
    ADD CONSTRAINT ck_etiqueta_codigo_tamanho
    CHECK (char_length(trim(codigo_interno)) BETWEEN 1 AND 80);

ALTER TABLE desenvolvimento.etiqueta
    ADD CONSTRAINT ck_etiqueta_nome_tamanho
    CHECK (char_length(trim(nome_etiqueta)) BETWEEN 2 AND 80);

ALTER TABLE desenvolvimento.etiqueta
    ADD CONSTRAINT ck_etiqueta_descricao_tamanho
    CHECK (descricao_etiqueta IS NULL OR char_length(trim(descricao_etiqueta)) <= 255);

DROP INDEX IF EXISTS desenvolvimento.uq_etiqueta_codigo_interno_ativo;
DROP INDEX IF EXISTS desenvolvimento.uq_etiqueta_codigo_interno_global;

CREATE UNIQUE INDEX uq_etiqueta_codigo_interno_global
    ON desenvolvimento.etiqueta (upper(trim(codigo_interno)));

CREATE OR REPLACE FUNCTION desenvolvimento.fn_bloqueia_inativar_etiqueta_com_produto_ativo()
RETURNS trigger AS $$
BEGIN
    IF OLD.situacao_etiqueta = true AND NEW.situacao_etiqueta = false THEN
        IF EXISTS (
            SELECT 1
              FROM desenvolvimento.produto_etiqueta pe
             WHERE pe.codigo_etiqueta = OLD.codigo_etiqueta
               AND pe.situacao_produto_etiqueta = true
        ) THEN
            RAISE EXCEPTION 'Nao e possivel inativar esta etiqueta: existem produtos ativos vinculados.';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_bloqueia_inativar_etiqueta_com_produto_ativo ON desenvolvimento.etiqueta;

CREATE TRIGGER trg_bloqueia_inativar_etiqueta_com_produto_ativo
    BEFORE UPDATE ON desenvolvimento.etiqueta
    FOR EACH ROW
    EXECUTE FUNCTION desenvolvimento.fn_bloqueia_inativar_etiqueta_com_produto_ativo();

COMMIT;
