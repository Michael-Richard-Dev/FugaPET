-- ============================================================
-- 032_tara_regras_banco_PROPOSTA_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Tara
--
-- OBJETIVO
--   Endurecer no banco as regras do Cadastro de Tara, alinhando ao padrão Setor/Cargo/Tipo de Tara.
--   A aplicação já valida; o banco deve bloquear bypass por SQL direto.
--
-- DECISÃO FUNCIONAL
--   - nome_tara: ÚNICO por (codigo_setor, codigo_tipo_tara, upper(trim(nome_tara))) INDEPENDENTE da situação
--     (não pode existir a mesma tara ativa e inativa no mesmo setor+tipo). Mesmo nome em outro setor/tipo = OK.
--   - nome_tara: mínimo 2 e máximo 80 caracteres úteis após trim.
--   - tamanho: máximo 80; observacao: máximo 255.
--   - peso_kg > 0 (opcional — aplicar somente se o Richard aprovar; pode haver taras legadas com 0).
--
-- IMPORTANTE
--   1. PROPOSTA para revisão. NÃO executar automaticamente (Richard valida).
--   2. Executar primeiro em DESENVOLVIMENTO, com backup/snapshot.
--   3. Se o preflight acusar duplicados por setor+tipo+nome, RESOLVER antes (a aplicação orienta a reativar
--      o existente); o índice único global só cria após os dados consistentes.
--   4. Para HML, ajustar o schema (homologacao) antes de aplicar.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

-- ============================================================
-- 1. Preflight: tabelas + duplicados (setor+tipo+nome) + tamanhos
-- ============================================================
DO $$
DECLARE
    v_dups integer;
    v_tam  integer;
BEGIN
    IF to_regclass('desenvolvimento.tara') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.tara nao existe.';
    END IF;

    SELECT count(*) INTO v_dups
      FROM (
        SELECT codigo_setor, codigo_tipo_tara, upper(trim(nome_tara))
          FROM desenvolvimento.tara
         GROUP BY codigo_setor, codigo_tipo_tara, upper(trim(nome_tara))
        HAVING count(*) > 1
      ) d;
    IF v_dups > 0 THEN
        RAISE EXCEPTION 'Existem % combinacoes setor+tipo+nome de tara duplicadas. Resolva antes (ver README).', v_dups;
    END IF;

    SELECT count(*) INTO v_tam
      FROM desenvolvimento.tara
     WHERE nome_tara IS NULL
        OR char_length(trim(nome_tara)) < 2
        OR char_length(trim(nome_tara)) > 80
        OR char_length(coalesce(trim(tamanho), '')) > 80
        OR char_length(coalesce(trim(observacao), '')) > 255;
    IF v_tam > 0 THEN
        RAISE EXCEPTION 'Existem % taras com nome/tamanho/observacao fora dos limites (2..80 / <=80 / <=255).', v_tam;
    END IF;
END $$;

-- ============================================================
-- 2. Checks de tamanho
-- ============================================================
ALTER TABLE desenvolvimento.tara
    ADD CONSTRAINT ck_tara_nome_tamanho
    CHECK (char_length(trim(nome_tara)) BETWEEN 2 AND 80);

ALTER TABLE desenvolvimento.tara
    ADD CONSTRAINT ck_tara_tamanho_tamanho
    CHECK (tamanho IS NULL OR char_length(trim(tamanho)) <= 80);

ALTER TABLE desenvolvimento.tara
    ADD CONSTRAINT ck_tara_observacao_tamanho
    CHECK (observacao IS NULL OR char_length(trim(observacao)) <= 255);

-- Opcional (aplicar só se o Richard aprovar — pode haver dados legados com 0):
-- ALTER TABLE desenvolvimento.tara
--     ADD CONSTRAINT ck_tara_peso_kg_positivo CHECK (peso_kg > 0);

-- ============================================================
-- 3. Índice UNICO GLOBAL por setor+tipo+upper(trim(nome)) (independe da situação)
--    Observação: se hoje existir um índice parcial WHERE situacao_tara (uq_tara_setor_tipo_nome),
--    ele deve ser REMOVIDO/substituído por este global. Validar em preflight/manual antes.
-- ============================================================
CREATE UNIQUE INDEX uq_tara_setor_tipo_nome_global
    ON desenvolvimento.tara (codigo_setor, codigo_tipo_tara, upper(trim(nome_tara)));

COMMIT;

-- ============================================================
-- ROLLBACK (manual se necessário):
--   DROP INDEX IF EXISTS desenvolvimento.uq_tara_setor_tipo_nome_global;
--   ALTER TABLE desenvolvimento.tara DROP CONSTRAINT IF EXISTS ck_tara_observacao_tamanho;
--   ALTER TABLE desenvolvimento.tara DROP CONSTRAINT IF EXISTS ck_tara_tamanho_tamanho;
--   ALTER TABLE desenvolvimento.tara DROP CONSTRAINT IF EXISTS ck_tara_nome_tamanho;
-- ============================================================
