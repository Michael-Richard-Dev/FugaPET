\set ON_ERROR_STOP on
-- ============================================================
-- 069_tolerancia_recebimento_material_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 069 - Tolerancia de pesagem por material no Recebimento
-- PROPOSTA (ESCRITA) - transacao unica
--
-- O QUE CRIA: a decisao do usuario sobre QUANTO a pesagem pode divergir da
-- quantidade do pedido, por material do centro. O catalogo de materiais NAO mora
-- aqui — vem do SAP, espelhado em produto_catalogo_centro (incremento 066).
--
-- A REGRA, em uma frase: recebido o item, o total LIQUIDO pesado precisa ficar
-- entre (quantidade do pedido - X%) e (quantidade do pedido + X%).
--
-- DUAS REGRAS ESTAO NO BANCO, nao so na tela, porque sao de integridade:
--   1. UNIQUE (centro, material): um material tem no maximo UMA tolerancia. Duas
--      linhas fariam a Entrada escolher entre elas, e nao ha criterio para isso.
--   2. CHECK 0 <= percentual <= 100: acima de 100 o limite inferior ficaria
--      NEGATIVO, o que nao significa nada — nenhum recebimento e "menos que zero".
--      Zero e valido e significa exigencia exata, sem folga.
--
-- POR QUE percentual E NOT NULL: a ausencia de tolerancia se expressa pela
-- AUSENCIA DE LINHA, nao por um nulo. Nulo aqui criaria um terceiro estado
-- ("cadastrado mas sem valor") que a tela teria de explicar e a Entrada, de tratar.
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '069 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_dev_app') THEN
        RAISE EXCEPTION '069 ABORTADO: papel fugapet_dev_app nao existe.';
    END IF;
END
$$;

CREATE TABLE IF NOT EXISTS desenvolvimento.tolerancia_recebimento_material
(
    codigo_tolerancia      bigserial     NOT NULL,
    centro                 text          NOT NULL,
    material               text          NOT NULL,

    -- Percentual unico, valido para MAIS e para MENOS (decisao do usuario em
    -- 23/11/2026). Dois percentuais seriam dois campos para manter e conferir, e
    -- nenhum caso real pedia assimetria.
    percentual_tolerancia  numeric(5,2)  NOT NULL,

    -- Retrato do SAP no momento do cadastro: faz a tela abrir sem rede e deixa
    -- auditar o que o usuario via quando decidiu. NAO e fonte de verdade.
    descricao_material     text          NULL,
    tipo_material          text          NULL,

    situacao               boolean       NOT NULL DEFAULT true,
    criado_em              timestamptz   NOT NULL DEFAULT now(),
    criado_por             bigint        NULL,
    atualizado_em          timestamptz   NULL,
    atualizado_por         bigint        NULL,

    CONSTRAINT pk_tolerancia_recebimento_material
        PRIMARY KEY (codigo_tolerancia),

    CONSTRAINT uq_tolerancia_recebimento_material
        UNIQUE (centro, material),

    CONSTRAINT ck_tolerancia_recebimento_percentual
        CHECK (percentual_tolerancia >= 0 AND percentual_tolerancia <= 100),

    CONSTRAINT ck_tolerancia_recebimento_centro
        CHECK (btrim(centro) <> ''),

    CONSTRAINT ck_tolerancia_recebimento_material_vazio
        CHECK (btrim(material) <> '')
);

COMMENT ON TABLE desenvolvimento.tolerancia_recebimento_material IS
    'Quanto a pesagem do Recebimento pode divergir da quantidade do pedido, por material do centro. O catalogo de materiais NAO mora aqui - vem do SAP (produto_catalogo_centro, incremento 066).';

COMMENT ON COLUMN desenvolvimento.tolerancia_recebimento_material.percentual_tolerancia IS
    'Percentual unico, valido para mais E para menos. 10.00 = o total liquido pesado pode ficar entre 90% e 110% da quantidade pedida. Zero = exigencia exata.';

COMMENT ON COLUMN desenvolvimento.tolerancia_recebimento_material.situacao IS
    'false = tolerancia inativa. A Entrada trata inativa como NAO CADASTRADA, e nao como "sem limite".';

-- Busca da tela: lista por centro, ordenada por material.
CREATE INDEX IF NOT EXISTS ix_tolerancia_recebimento_centro
    ON desenvolvimento.tolerancia_recebimento_material (centro, material);

GRANT SELECT, INSERT, UPDATE, DELETE
    ON desenvolvimento.tolerancia_recebimento_material TO fugapet_dev_app;
GRANT USAGE, SELECT
    ON SEQUENCE desenvolvimento.tolerancia_recebimento_material_codigo_tolerancia_seq TO fugapet_dev_app;

COMMIT;

\echo '== 069 aplicado. Rode a VALIDACAO. =='
