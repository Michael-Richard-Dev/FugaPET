-- ============================================================
-- 033_modelo_etiqueta_regras_banco_PROPOSTA_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Modelo de Etiqueta
--
-- OBJETIVO
--   Endurecer no banco as regras do Cadastro de Modelo de Etiqueta, alinhando ao padrão
--   Setor/Cargo/Tara/Tipo de Tara. A aplicação já valida; o banco deve bloquear bypass por SQL direto.
--
-- DECISÃO FUNCIONAL
--   - (nome_modelo_etiqueta, versao): ÚNICO por (upper(trim(nome)), versao) INDEPENDENTE da situação
--     (não pode existir o mesmo nome+versão ativo e inativo). Mesmo nome com versão diferente = OK.
--   - nome_modelo_etiqueta: mínimo 2 e máximo 80 caracteres úteis após trim.
--   - versao: inteiro > 0.
--   - dpi: inteiro > 0 (obrigatório na aplicação; no banco pode ser NULL por compatibilidade — CHECK só valida se informado).
--   - largura_mm / altura_mm: opcionais; quando informadas, > 0 (duas casas decimais são responsabilidade da aplicação/tipo numeric).
--   - observacao: máximo 255.
--   - conteudo_zpl: obrigatório (não vazio) — preservado exatamente como digitado pela aplicação (sem Trim).
--   - Inativação bloqueada quando existe etiqueta ATIVA vinculada (etiqueta.situacao_etiqueta = true).
--
-- IMPORTANTE
--   1. PROPOSTA para revisão. NÃO EXECUTAR automaticamente (Richard valida).
--   2. Executar primeiro em DESENVOLVIMENTO, com backup/snapshot.
--   3. Se o preflight acusar duplicados por nome+versão, RESOLVER antes (a aplicação orienta a reativar
--      o existente); o índice único global só cria após os dados consistentes.
--   4. Para HML, ajustar o schema (homologacao) antes de aplicar.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

-- ============================================================
-- 1. Preflight: tabelas + duplicados (nome+versão) + tamanhos/valores
-- ============================================================
DO $$
DECLARE
    v_dups integer;
    v_val  integer;
BEGIN
    IF to_regclass('desenvolvimento.modelo_etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.modelo_etiqueta nao existe.';
    END IF;
    IF to_regclass('desenvolvimento.etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.etiqueta nao existe.';
    END IF;

    SELECT count(*) INTO v_dups
      FROM (
        SELECT upper(trim(nome_modelo_etiqueta)) AS nome, versao
          FROM desenvolvimento.modelo_etiqueta
         GROUP BY upper(trim(nome_modelo_etiqueta)), versao
        HAVING count(*) > 1
      ) d;
    IF v_dups > 0 THEN
        RAISE EXCEPTION 'Existem % combinacoes nome+versao de modelo_etiqueta duplicadas. Resolva antes (ver README).', v_dups;
    END IF;

    SELECT count(*) INTO v_val
      FROM desenvolvimento.modelo_etiqueta
     WHERE nome_modelo_etiqueta IS NULL
        OR char_length(trim(nome_modelo_etiqueta)) < 2
        OR char_length(trim(nome_modelo_etiqueta)) > 80
        OR char_length(coalesce(trim(observacao), '')) > 255
        OR versao <= 0
        OR (dpi IS NOT NULL AND dpi <= 0)
        OR (largura_mm IS NOT NULL AND largura_mm <= 0)
        OR (altura_mm IS NOT NULL AND altura_mm <= 0)
        OR coalesce(trim(conteudo_zpl), '') = '';
    IF v_val > 0 THEN
        RAISE EXCEPTION 'Existem % modelos com nome/observacao/versao/dpi/dimensoes/zpl fora das regras.', v_val;
    END IF;
END $$;

-- ============================================================
-- 2. Checks de tamanho e valores
-- ============================================================
ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_nome_tamanho
    CHECK (char_length(trim(nome_modelo_etiqueta)) BETWEEN 2 AND 80);

ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_observacao_tamanho
    CHECK (observacao IS NULL OR char_length(trim(observacao)) <= 255);

ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_versao_positiva
    CHECK (versao > 0);

ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_dpi_positivo
    CHECK (dpi IS NULL OR dpi > 0);

ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_largura_positiva
    CHECK (largura_mm IS NULL OR largura_mm > 0);

ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_altura_positiva
    CHECK (altura_mm IS NULL OR altura_mm > 0);

ALTER TABLE desenvolvimento.modelo_etiqueta
    ADD CONSTRAINT ck_modelo_etiqueta_zpl_nao_vazio
    CHECK (conteudo_zpl IS NOT NULL AND char_length(trim(conteudo_zpl)) > 0);

-- ============================================================
-- 3. Índice UNICO GLOBAL por (upper(trim(nome)), versao) (independe da situação)
--    Observação: se hoje existir um índice parcial WHERE situacao_modelo_etiqueta
--    (uq_modelo_etiqueta_nome_versao), ele deve ser REMOVIDO/substituído por este global.
-- ============================================================
CREATE UNIQUE INDEX uq_modelo_etiqueta_nome_versao_global
    ON desenvolvimento.modelo_etiqueta (upper(trim(nome_modelo_etiqueta)), versao);

-- ============================================================
-- 4. Proteção de inativação: bloquear inativar modelo com etiqueta ATIVA vinculada
--    (a aplicação já faz UPDATE atômico com NOT EXISTS; este trigger é a mesma regra no banco).
-- ============================================================
CREATE OR REPLACE FUNCTION desenvolvimento.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()
RETURNS trigger AS $$
BEGIN
    IF OLD.situacao_modelo_etiqueta = true AND NEW.situacao_modelo_etiqueta = false THEN
        IF EXISTS (
            SELECT 1 FROM desenvolvimento.etiqueta e
             WHERE e.codigo_modelo_etiqueta = OLD.codigo_modelo_etiqueta
               AND e.situacao_etiqueta = true
        ) THEN
            RAISE EXCEPTION 'Nao e possivel inativar este modelo: existem etiquetas ativas vinculadas.';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_bloqueia_inativar_modelo_com_etiqueta_ativa
    BEFORE UPDATE ON desenvolvimento.modelo_etiqueta
    FOR EACH ROW
    EXECUTE FUNCTION desenvolvimento.fn_bloqueia_inativar_modelo_com_etiqueta_ativa();

COMMIT;

-- ============================================================
-- ROLLBACK (manual se necessário):
--   DROP TRIGGER IF EXISTS trg_bloqueia_inativar_modelo_com_etiqueta_ativa ON desenvolvimento.modelo_etiqueta;
--   DROP FUNCTION IF EXISTS desenvolvimento.fn_bloqueia_inativar_modelo_com_etiqueta_ativa();
--   DROP INDEX IF EXISTS desenvolvimento.uq_modelo_etiqueta_nome_versao_global;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_zpl_nao_vazio;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_altura_positiva;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_largura_positiva;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_dpi_positivo;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_versao_positiva;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_observacao_tamanho;
--   ALTER TABLE desenvolvimento.modelo_etiqueta DROP CONSTRAINT IF EXISTS ck_modelo_etiqueta_nome_tamanho;
-- ============================================================
