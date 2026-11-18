\set ON_ERROR_STOP on
-- ============================================================
-- 065_vinculo_produto_etiqueta_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 065 - Vinculo de produto acabado (centro 3007) a etiqueta e processo
-- PROPOSTA (ESCRITA) - transacao unica
--
-- O QUE CRIA: uma tabela de VINCULO. O catalogo de produtos continua vindo do SAP;
-- aqui so mora a decisao do usuario sobre cada produto — qual etiqueta usar (ou
-- nenhuma) e como pesar.
--
-- TRES REGRAS ESTAO NO BANCO, nao so na tela, porque sao de integridade:
--   1. UNIQUE (centro, produto): um produto tem no maximo UM vinculo, logo nunca
--      duas etiquetas. Era o pedido explicito do usuario ("apenas um pra um").
--   2. CHECK sem_etiqueta x codigo_etiqueta: "sem etiqueta" e uma ESCOLHA gravada,
--      diferente de "ainda nao configurado", que e a AUSENCIA de linha. Sem isso os
--      dois estados ficariam indistinguiveis e a tela nao teria como cobrar decisao.
--   3. CHECK processo_pesagem: so os dois processos que existem no dominio.
--
-- ATENCAO ao processo ENVIO_UNICO: a pesagem que junta todas as caixas num unico
-- envio ao SAP AINDA NAO EXISTE no sistema (18/11/2026). A coluna aceita o valor
-- porque o usuario precisa cadastrar a intencao antes; quem for implementar a
-- pesagem e que decide o que fazer com ele. NADA no runtime deve supor que um
-- produto marcado assim ja tenha caminho de envio.
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '065 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.etiqueta') IS NULL THEN
        RAISE EXCEPTION '065 ABORTADO: tabela etiqueta nao existe; a FK nasceria quebrada.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') THEN
        RAISE EXCEPTION '065 ABORTADO: papel fugapet_dev_app nao existe.';
    END IF;
END
$$;

CREATE TABLE IF NOT EXISTS desenvolvimento.produto_etiqueta_vinculo
(
    codigo_vinculo     bigserial    NOT NULL,
    centro             text         NOT NULL,
    produto            text         NOT NULL,

    -- Retrato do SAP no momento do vinculo. Serve para a tela mostrar a linha mesmo
    -- com o SAP fora do ar, e para auditar o que o usuario via quando decidiu.
    -- NAO e fonte de verdade: a verdade do cadastro do produto e sempre o SAP.
    descricao_produto  text         NULL,
    tipo_produto       text         NULL,

    codigo_etiqueta    bigint       NULL,
    sem_etiqueta       boolean      NOT NULL DEFAULT false,
    processo_pesagem   text         NOT NULL,

    situacao           boolean      NOT NULL DEFAULT true,
    criado_em          timestamptz  NOT NULL DEFAULT now(),
    criado_por         bigint       NULL,
    atualizado_em      timestamptz  NULL,
    atualizado_por     bigint       NULL,

    CONSTRAINT pk_produto_etiqueta_vinculo
        PRIMARY KEY (codigo_vinculo),

    CONSTRAINT uq_produto_etiqueta_vinculo_produto
        UNIQUE (centro, produto),

    CONSTRAINT fk_produto_etiqueta_vinculo_etiqueta
        FOREIGN KEY (codigo_etiqueta)
        REFERENCES desenvolvimento.etiqueta (codigo_etiqueta),

    CONSTRAINT ck_produto_etiqueta_vinculo_etiqueta
        CHECK ((sem_etiqueta AND codigo_etiqueta IS NULL)
            OR (NOT sem_etiqueta AND codigo_etiqueta IS NOT NULL)),

    CONSTRAINT ck_produto_etiqueta_vinculo_processo
        CHECK (processo_pesagem IN ('CAIXA_A_CAIXA', 'ENVIO_UNICO')),

    CONSTRAINT ck_produto_etiqueta_vinculo_centro
        CHECK (btrim(centro) <> ''),

    CONSTRAINT ck_produto_etiqueta_vinculo_produto_vazio
        CHECK (btrim(produto) <> '')
);

COMMENT ON TABLE desenvolvimento.produto_etiqueta_vinculo IS
    'Decisao do usuario por produto acabado do centro: qual etiqueta e qual processo de pesagem. O catalogo de produtos NAO mora aqui - vem do SAP (A_ProductPlant/A_Product).';

COMMENT ON COLUMN desenvolvimento.produto_etiqueta_vinculo.sem_etiqueta IS
    'true = o usuario decidiu que este produto NAO leva etiqueta. Ausencia de linha = ainda nao decidido.';

COMMENT ON COLUMN desenvolvimento.produto_etiqueta_vinculo.processo_pesagem IS
    'CAIXA_A_CAIXA (implementado) ou ENVIO_UNICO (cadastravel, SEM caminho de envio em 18/11/2026).';

-- Busca da tela: lista por centro, ordenada por produto.
CREATE INDEX IF NOT EXISTS ix_produto_etiqueta_vinculo_centro
    ON desenvolvimento.produto_etiqueta_vinculo (centro, produto);

-- "Quais produtos usam esta etiqueta?" - pergunta da tela de etiqueta ao inativar.
CREATE INDEX IF NOT EXISTS ix_produto_etiqueta_vinculo_etiqueta
    ON desenvolvimento.produto_etiqueta_vinculo (codigo_etiqueta)
    WHERE codigo_etiqueta IS NOT NULL;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON desenvolvimento.produto_etiqueta_vinculo TO fugapet_dev_app;
GRANT USAGE, SELECT
    ON SEQUENCE desenvolvimento.produto_etiqueta_vinculo_codigo_vinculo_seq TO fugapet_dev_app;

COMMIT;

\echo '== 065 aplicado. Rode a VALIDACAO. =='
