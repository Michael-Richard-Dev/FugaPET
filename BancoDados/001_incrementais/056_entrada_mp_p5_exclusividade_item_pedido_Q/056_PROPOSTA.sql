-- FUGAPET / ENTRADA MP / P5 / 056 / PROPOSTA
-- Exclusividade DURAVEL por pedido+item para o recebimento ROH.
--
-- Por que esta tabela existe: a reserva atual e por LANCAMENTO e nao impede que dois
-- lancamentos distintos do MESMO item do pedido enviem o movimento 101 com a quantidade
-- integral, duplicando o recebimento no SAP. A unicidade abaixo e o que torna essa
-- duplicidade impossivel entre lancamentos, instancias e reinicializacoes.
--
-- A linha NAO e removida apos o envio. Isso e deliberado: uma falha DEPOIS do POST deixa
-- a marca gravada, de modo que um reenvio cego fica bloqueado ate reconciliacao humana.
--
-- Idempotente. Nenhuma tabela existente e alterada.

BEGIN;

CREATE TABLE IF NOT EXISTS homologacao.entrada_mp_p5_exclusividade_item_pedido (
    numero_pedido      text        NOT NULL,
    numero_item        text        NOT NULL,
    codigo_lancamento  bigint      NOT NULL,
    obtida_em          timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT pk_entrada_mp_p5_exclusividade_item_pedido
        PRIMARY KEY (numero_pedido, numero_item),
    -- O item chega normalizado em 5 digitos ('00010'); o pedido, somente digitos.
    CONSTRAINT ck_entrada_mp_p5_exclusividade_pedido_formato
        CHECK (numero_pedido ~ '^[0-9]{1,10}$'),
    CONSTRAINT ck_entrada_mp_p5_exclusividade_item_formato
        CHECK (numero_item ~ '^[0-9]{5}$'),
    CONSTRAINT ck_entrada_mp_p5_exclusividade_lancamento_positivo
        CHECK (codigo_lancamento > 0)
);

COMMENT ON TABLE homologacao.entrada_mp_p5_exclusividade_item_pedido IS
    'P5/056: unicidade duravel por pedido+item do recebimento. A linha permanece apos o '
    'envio para que falha pos-POST nao autorize reenvio cego.';

-- Consulta de quem detem a exclusividade de um lancamento (diagnostico e recuperacao).
CREATE INDEX IF NOT EXISTS ix_entrada_mp_p5_exclusividade_lancamento
    ON homologacao.entrada_mp_p5_exclusividade_item_pedido (codigo_lancamento);

-- Somente leitura e insercao. A aplicacao NAO recebe DELETE nem UPDATE: liberar uma
-- exclusividade e ato administrativo deliberado, nunca efeito colateral do fluxo.
GRANT SELECT, INSERT ON homologacao.entrada_mp_p5_exclusividade_item_pedido TO fugapet_q_app;

COMMIT;
