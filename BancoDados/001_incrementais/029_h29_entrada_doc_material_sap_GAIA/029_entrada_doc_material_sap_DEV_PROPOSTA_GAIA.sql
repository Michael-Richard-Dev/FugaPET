-- ============================================================
-- 029_entrada_doc_material_sap_DEV_PROPOSTA_GAIA.sql
-- Projeto FugaPET_Dev
-- Ambiente DESENVOLVIMENTO (DEV)
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Entrada de Produto - rastreabilidade do Documento de Material SAP (movimento 101)
-- Responsavel tecnico: Equipe FugaPET (proposta para Gaia Dados)
-- Data: 2026-06-25
-- Pacote: incremental 029
--
-- OBJETIVO
--   Adicionar rastreabilidade do documento de material SAP criado via movimento 101
--   (API_MATERIAL_DOCUMENT_SRV) na Entrada de Produto, SEM alterar a baseline V1.1.
--   Hoje o numero/exercicio do documento so vao para log_integracao_sap (operacao
--   CRIAR_DOCUMENTO_MATERIAL_101). Estas colunas permitem amarrar o lancamento
--   confirmado ao documento SAP de forma consultavel.
--
-- ESCOPO
--   entrada_produto_lancamento:
--     - documento_material_sap            varchar(20)
--     - exercicio_documento_material_sap  varchar(4)
--     - enviado_sap_em                    timestamptz
--   entrada_produto_item (opcional):
--     - documento_material_item           varchar(20)
--   Constraints:
--     - documento e exercicio preenchidos juntos ou ambos nulos.
--     - exercicio com 4 digitos numericos quando preenchido.
--   Nenhuma coluna e NOT NULL (registros antigos e lancamentos nao enviados ficam NULL).
--
-- AVISO: NAO executar automaticamente. Revisao Gaia Dados obrigatoria.
-- Ordem de execucao: PREFLIGHT -> PROPOSTA -> VALIDACAO -> (se necessario) ROLLBACK
-- Idempotente: usa ADD COLUMN IF NOT EXISTS e ADD CONSTRAINT condicional.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

-- 0. Travas criticas reaplicadas (defesa, caso a PROPOSTA seja executada sem o PREFLIGHT)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento') THEN
        RAISE EXCEPTION 'Falha: schema desenvolvimento nao existe.';
    END IF;
    IF to_regclass('desenvolvimento.entrada_produto_lancamento') IS NULL THEN
        RAISE EXCEPTION 'Falha: tabela desenvolvimento.entrada_produto_lancamento nao existe.';
    END IF;
    IF to_regclass('desenvolvimento.entrada_produto_item') IS NULL THEN
        RAISE EXCEPTION 'Falha: tabela desenvolvimento.entrada_produto_item nao existe.';
    END IF;
END $$;

-- 1. Colunas de rastreabilidade no cabecalho do lancamento
ALTER TABLE desenvolvimento.entrada_produto_lancamento
    ADD COLUMN IF NOT EXISTS documento_material_sap            varchar(20),
    ADD COLUMN IF NOT EXISTS exercicio_documento_material_sap  varchar(4),
    ADD COLUMN IF NOT EXISTS enviado_sap_em                    timestamptz;

-- 2. Constraint: documento e exercicio andam juntos (ambos nulos ou ambos preenchidos)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_entrada_lancamento_documento_material_par'
           AND conrelid = 'desenvolvimento.entrada_produto_lancamento'::regclass
    ) THEN
        ALTER TABLE desenvolvimento.entrada_produto_lancamento
            ADD CONSTRAINT ck_entrada_lancamento_documento_material_par
            CHECK (
                (documento_material_sap IS NULL AND exercicio_documento_material_sap IS NULL)
                OR (documento_material_sap IS NOT NULL AND exercicio_documento_material_sap IS NOT NULL)
            );
    END IF;
END $$;

-- 3. Constraint: exercicio com 4 digitos numericos quando preenchido
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_entrada_lancamento_exercicio_material_formato'
           AND conrelid = 'desenvolvimento.entrada_produto_lancamento'::regclass
    ) THEN
        ALTER TABLE desenvolvimento.entrada_produto_lancamento
            ADD CONSTRAINT ck_entrada_lancamento_exercicio_material_formato
            CHECK (
                exercicio_documento_material_sap IS NULL
                OR exercicio_documento_material_sap ~ '^[0-9]{4}$'
            );
    END IF;
END $$;

-- 4. Comentarios (origem: retorno da API_MATERIAL_DOCUMENT_SRV, movimento 101)
COMMENT ON COLUMN desenvolvimento.entrada_produto_lancamento.documento_material_sap IS
    'Numero do documento de material (movimento 101) retornado pela API_MATERIAL_DOCUMENT_SRV. NULL ate CONFIRMADO_SAP.';
COMMENT ON COLUMN desenvolvimento.entrada_produto_lancamento.exercicio_documento_material_sap IS
    'Exercicio (ano fiscal) do documento de material retornado pela API_MATERIAL_DOCUMENT_SRV. NULL ate CONFIRMADO_SAP.';
COMMENT ON COLUMN desenvolvimento.entrada_produto_lancamento.enviado_sap_em IS
    'Instante (UTC) da confirmacao de envio ao SAP (documento de material movimento 101 criado).';

-- 5. (Opcional) Item do documento de material por item do lancamento
ALTER TABLE desenvolvimento.entrada_produto_item
    ADD COLUMN IF NOT EXISTS documento_material_item varchar(20);

COMMENT ON COLUMN desenvolvimento.entrada_produto_item.documento_material_item IS
    'Item do documento de material (API_MATERIAL_DOCUMENT_SRV, movimento 101) correspondente. Opcional. NULL ate CONFIRMADO_SAP.';

COMMIT;

SELECT 'PROPOSTA 029 (DEV) APLICADA - COLUNAS, COMENTARIOS E CONSTRAINTS CRIADOS; DADOS EXISTENTES PRESERVADOS' AS resultado_proposta;
