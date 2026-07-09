-- ============================================================
-- 031_tipo_tara_regras_banco_PROPOSTA_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Tipo de Tara
--
-- OBJETIVO
--   Endurecer no banco as regras do Cadastro de Tipo de Tara, alinhando ao
--   padrao de Setor/Cargo. A aplicacao ja valida; o banco deve bloquear bypass.
--
-- DECISAO FUNCIONAL
--   - nome_tipo_tara: UNICO GLOBAL por upper(trim(nome_tipo_tara)) INDEPENDENTE
--     da situacao (nao pode existir "Teste3" Ativo e "Teste3" Inativo ao mesmo tempo).
--   - nome_tipo_tara: minimo 2 e maximo 80 caracteres uteis apos trim.
--   - descricao_tipo_tara: maximo 255 caracteres uteis apos trim.
--   - tipo_tara com Tara ATIVA vinculada nao pode ser inativado.
--
-- IMPORTANTE
--   1. PROPOSTA para revisao. NAO executar automaticamente (Richard valida).
--   2. Executar primeiro em DESENVOLVIMENTO, com backup/snapshot.
--   3. Se o preflight acusar duplicados globais, RESOLVER os registros ANTES
--      (a aplicacao ja orienta a reativar o existente); o indice unico so cria
--      apos os dados estarem consistentes.
--   4. Para HML, ajustar o schema (homologacao) antes de aplicar.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

-- ============================================================
-- 1. Preflight: schema/tabelas + duplicados globais + tamanhos
-- ============================================================
DO $$
DECLARE
    v_dups   integer;
    v_tam    integer;
    v_desc   integer;
BEGIN
    IF to_regclass('desenvolvimento.tipo_tara') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.tipo_tara nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.tara') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.tara nao existe.';
    END IF;

    -- Duplicados GLOBAIS (independente da situacao) — bloqueiam o indice unico.
    SELECT count(*) INTO v_dups
      FROM (
        SELECT upper(trim(nome_tipo_tara))
          FROM desenvolvimento.tipo_tara
         GROUP BY upper(trim(nome_tipo_tara))
        HAVING count(*) > 1
      ) d;
    IF v_dups > 0 THEN
        RAISE EXCEPTION 'Existem % nome(s) de tipo_tara duplicados (global). Resolva antes (reative/renomeie) — ver consulta de diagnostico no README.', v_dups;
    END IF;

    SELECT count(*) INTO v_tam
      FROM desenvolvimento.tipo_tara
     WHERE nome_tipo_tara IS NULL
        OR char_length(trim(nome_tipo_tara)) < 2
        OR char_length(trim(nome_tipo_tara)) > 80;
    IF v_tam > 0 THEN
        RAISE EXCEPTION 'Existem % tipo_tara com nome fora de 2..80.', v_tam;
    END IF;

    SELECT count(*) INTO v_desc
      FROM desenvolvimento.tipo_tara
     WHERE descricao_tipo_tara IS NOT NULL
       AND char_length(trim(descricao_tipo_tara)) > 255;
    IF v_desc > 0 THEN
        RAISE EXCEPTION 'Existem % tipo_tara com descricao acima de 255.', v_desc;
    END IF;
END $$;

-- ============================================================
-- 2. Checks de tamanho (nome 2..80, descricao <=255)
-- ============================================================
ALTER TABLE desenvolvimento.tipo_tara
    ADD CONSTRAINT ck_tipo_tara_nome_tamanho
    CHECK (char_length(trim(nome_tipo_tara)) BETWEEN 2 AND 80);

ALTER TABLE desenvolvimento.tipo_tara
    ADD CONSTRAINT ck_tipo_tara_descricao_tamanho
    CHECK (descricao_tipo_tara IS NULL OR char_length(trim(descricao_tipo_tara)) <= 255);

-- ============================================================
-- 3. Indice UNICO GLOBAL por upper(trim(nome_tipo_tara)) (independe da situacao)
-- ============================================================
CREATE UNIQUE INDEX uq_tipo_tara_nome_global
    ON desenvolvimento.tipo_tara (upper(trim(nome_tipo_tara)));

-- ============================================================
-- 4. Bloqueio de inativacao com Tara ATIVA vinculada (trigger)
-- ============================================================
CREATE OR REPLACE FUNCTION desenvolvimento.fn_tipo_tara_bloqueia_inativacao()
RETURNS trigger AS $$
BEGIN
    IF OLD.situacao_tipo_tara = true
       AND NEW.situacao_tipo_tara = false
       AND EXISTS (
            SELECT 1 FROM desenvolvimento.tara
             WHERE codigo_tipo_tara = OLD.codigo_tipo_tara
               AND situacao_tara = true
       )
    THEN
        RAISE EXCEPTION 'Nao e possivel inativar este tipo de tara: existem taras ativas vinculadas a ele.';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_tipo_tara_bloqueia_inativacao ON desenvolvimento.tipo_tara;
CREATE TRIGGER trg_tipo_tara_bloqueia_inativacao
    BEFORE UPDATE OF situacao_tipo_tara ON desenvolvimento.tipo_tara
    FOR EACH ROW
    EXECUTE FUNCTION desenvolvimento.fn_tipo_tara_bloqueia_inativacao();

COMMIT;

-- ============================================================
-- ROLLBACK (executar manualmente se necessario):
--   DROP TRIGGER IF EXISTS trg_tipo_tara_bloqueia_inativacao ON desenvolvimento.tipo_tara;
--   DROP FUNCTION IF EXISTS desenvolvimento.fn_tipo_tara_bloqueia_inativacao();
--   DROP INDEX IF EXISTS desenvolvimento.uq_tipo_tara_nome_global;
--   ALTER TABLE desenvolvimento.tipo_tara DROP CONSTRAINT IF EXISTS ck_tipo_tara_descricao_tamanho;
--   ALTER TABLE desenvolvimento.tipo_tara DROP CONSTRAINT IF EXISTS ck_tipo_tara_nome_tamanho;
-- ============================================================
