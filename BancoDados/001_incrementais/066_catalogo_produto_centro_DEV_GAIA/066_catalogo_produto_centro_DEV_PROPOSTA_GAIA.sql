\set ON_ERROR_STOP on
-- ============================================================
-- 066_catalogo_produto_centro_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 066 - Catalogo LOCAL de produtos do centro
-- PROPOSTA (ESCRITA) - transacao unica
--
-- POR QUE EXISTE: ate aqui a tela de Vinculo de Produto consultava o SAP a CADA
-- abertura. Para o centro 3007 do mandante 130 isso sao 51 chamadas: 8 paginas
-- para listar os 1521 materiais, 39 lotes para descobrir o tipo de cada um e 4
-- lotes de descricao. Pedido do usuario em 18/11/2026: guardar o catalogo aqui e
-- buscar no SAP apenas o que for NOVO.
--
-- O QUE ESTA TABELA E, E O QUE NAO E:
--   E um ESPELHO do que o SAP respondeu na ultima sincronizacao, com a data em que
--   respondeu. Serve para a tela abrir sem rede e para a sincronizacao saber o que
--   ja conhece.
--   NAO e fonte de verdade do cadastro do produto: a verdade continua no SAP.
--   Nenhuma decisao de negocio deve ler daqui como se fosse o mestre atual.
--
-- RELACAO COM produto_etiqueta_vinculo (065): sao tabelas INDEPENDENTES, de
-- proposito, e NAO ha chave estrangeira entre elas. O vinculo e decisao do usuario
-- e sobrevive ao produto sumir do catalogo; amarrar as duas faria a sincronizacao
-- poder apagar decisao de gente.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '066 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') THEN
        RAISE EXCEPTION '066 ABORTADO: papel fugapet_dev_app nao existe.';
    END IF;
END
$$;

CREATE TABLE IF NOT EXISTS desenvolvimento.produto_catalogo_centro
(
    codigo_catalogo     bigserial    NOT NULL,
    centro              text         NOT NULL,
    produto             text         NOT NULL,

    descricao           text         NULL,
    tipo_material       text         NULL,
    unidade_base        text         NULL,
    marcado_eliminacao  boolean      NOT NULL DEFAULT false,

    -- Quando o SAP confirmou que este material existe no centro. Atualizada a cada
    -- sincronizacao em que ele aparece.
    visto_em            timestamptz  NOT NULL DEFAULT now(),

    -- Quando os dados mestres (tipo, unidade, descricao) foram lidos do SAP. Fica
    -- nulo enquanto so se sabe que o material existe no centro — e esse nulo que
    -- diz a sincronizacao o que ainda falta buscar.
    mestre_lido_em      timestamptz  NULL,

    -- false = o SAP parou de listar este material neste centro. NAO se apaga a
    -- linha: pode haver vinculo decidido sobre ela, e sumir em silencio seria pior
    -- do que mostrar que sumiu.
    presente_no_sap     boolean      NOT NULL DEFAULT true,

    criado_em           timestamptz  NOT NULL DEFAULT now(),
    atualizado_em       timestamptz  NULL,

    CONSTRAINT pk_produto_catalogo_centro
        PRIMARY KEY (codigo_catalogo),

    CONSTRAINT uq_produto_catalogo_centro_produto
        UNIQUE (centro, produto),

    CONSTRAINT ck_produto_catalogo_centro_centro
        CHECK (btrim(centro) <> ''),

    CONSTRAINT ck_produto_catalogo_centro_produto
        CHECK (btrim(produto) <> '')
);

COMMENT ON TABLE desenvolvimento.produto_catalogo_centro IS
    'Espelho local dos materiais de um centro, como o SAP respondeu na ultima sincronizacao. NAO e fonte de verdade do cadastro do produto.';

COMMENT ON COLUMN desenvolvimento.produto_catalogo_centro.mestre_lido_em IS
    'Nulo = ainda falta buscar tipo/unidade/descricao no SAP. E o que torna a sincronizacao incremental.';

COMMENT ON COLUMN desenvolvimento.produto_catalogo_centro.presente_no_sap IS
    'false = o SAP parou de listar o material neste centro. A linha permanece porque pode haver vinculo decidido sobre ela.';

-- Leitura da tela: materiais de um centro, ordenados por codigo.
CREATE INDEX IF NOT EXISTS ix_produto_catalogo_centro_centro
    ON desenvolvimento.produto_catalogo_centro (centro, produto);

-- "O que ainda falta buscar?" - a pergunta que a sincronizacao faz toda vez.
CREATE INDEX IF NOT EXISTS ix_produto_catalogo_centro_pendente
    ON desenvolvimento.produto_catalogo_centro (centro)
    WHERE mestre_lido_em IS NULL;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON desenvolvimento.produto_catalogo_centro TO fugapet_dev_app;
GRANT USAGE, SELECT
    ON SEQUENCE desenvolvimento.produto_catalogo_centro_codigo_catalogo_seq TO fugapet_dev_app;

COMMIT;

\echo '== 066 aplicado. Rode a VALIDACAO. =='
