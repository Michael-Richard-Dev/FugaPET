-- ============================================================
-- 029_entrada_doc_material_sap_DEV_ROLLBACK_GAIA.sql
-- Projeto FugaPET_Dev
-- Ambiente DESENVOLVIMENTO (DEV)
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Entrada de Produto - rastreabilidade do Documento de Material SAP (movimento 101)
-- Responsavel tecnico: Equipe FugaPET (proposta para Gaia Dados)
-- Data: 2026-06-25
-- Pacote: incremental 029
--
-- OBJETIVO
--   Rollback tecnico do incremental 029. Remove SOMENTE os objetos criados pela
--   PROPOSTA 029 (colunas, comentarios e constraints). Nao toca em nenhum objeto
--   fora do escopo do 029.
--
-- SEGURANCA DE DADOS
--   O rollback REMOVE COLUNAS. Se houver rastreabilidade preenchida, o script
--   BLOQUEIA (RAISE EXCEPTION) para nao apagar dados. Para forcar a remocao mesmo
--   com dados, e necessario backup e ajuste manual documentado (ver README, secao
--   "Rollback com dados preenchidos").
--
-- AVISO: NAO executar automaticamente. Revisao Gaia Dados obrigatoria.
-- ============================================================

SET search_path TO desenvolvimento;

-- Preflight proprio do rollback: bloqueia se houver dados nas colunas do 029
DO $$
DECLARE
    v_qtd_lanc integer := 0;
    v_qtd_item integer := 0;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento') THEN
        RAISE EXCEPTION 'Falha ROLLBACK: schema desenvolvimento nao existe.';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'desenvolvimento' AND table_name = 'entrada_produto_lancamento'
                  AND column_name = 'documento_material_sap') THEN
        EXECUTE 'SELECT count(*) FROM desenvolvimento.entrada_produto_lancamento
                  WHERE documento_material_sap IS NOT NULL
                     OR exercicio_documento_material_sap IS NOT NULL
                     OR enviado_sap_em IS NOT NULL'
           INTO v_qtd_lanc;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'desenvolvimento' AND table_name = 'entrada_produto_item'
                  AND column_name = 'documento_material_item') THEN
        EXECUTE 'SELECT count(*) FROM desenvolvimento.entrada_produto_item
                  WHERE documento_material_item IS NOT NULL'
           INTO v_qtd_item;
    END IF;

    IF (v_qtd_lanc + v_qtd_item) > 0 THEN
        RAISE EXCEPTION 'ROLLBACK BLOQUEADO: % lancamento(s) e % item(ns) possuem rastreabilidade de documento material preenchida. Remover as colunas apagaria dados. Faca backup e ajuste manual documentado (README).', v_qtd_lanc, v_qtd_item;
    END IF;

    RAISE NOTICE 'PREFLIGHT ROLLBACK 029 (DEV) OK: sem dados preenchidos, remocao segura.';
END $$;

BEGIN;

-- Constraints criadas pela PROPOSTA 029
ALTER TABLE desenvolvimento.entrada_produto_lancamento
    DROP CONSTRAINT IF EXISTS ck_entrada_lancamento_exercicio_material_formato;
ALTER TABLE desenvolvimento.entrada_produto_lancamento
    DROP CONSTRAINT IF EXISTS ck_entrada_lancamento_documento_material_par;

-- Colunas criadas pela PROPOSTA 029
ALTER TABLE desenvolvimento.entrada_produto_lancamento
    DROP COLUMN IF EXISTS documento_material_sap,
    DROP COLUMN IF EXISTS exercicio_documento_material_sap,
    DROP COLUMN IF EXISTS enviado_sap_em;

ALTER TABLE desenvolvimento.entrada_produto_item
    DROP COLUMN IF EXISTS documento_material_item;

COMMIT;

SELECT 'ROLLBACK 029 (DEV) EXECUTADO - OBJETOS DO 029 REMOVIDOS; DADOS FORA DO ESCOPO PRESERVADOS' AS resultado_rollback;
