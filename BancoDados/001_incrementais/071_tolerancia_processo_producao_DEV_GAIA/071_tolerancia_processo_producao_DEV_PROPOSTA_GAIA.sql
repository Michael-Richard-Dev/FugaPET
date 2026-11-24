\set ON_ERROR_STOP on
-- ============================================================
-- 071_tolerancia_processo_producao_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 071 - Tolerancia por PRODUTO nos processos de producao
-- PROPOSTA (ESCRITA) - transacao unica
--
-- O QUE CRIA, em duas partes:
--   1. tolerancia_processo_producao - a decisao do usuario sobre quanto a pesagem
--      de um produto pode divergir do previsto, POR PROCESSO;
--   2. liberacao_tolerancia_processo - o log das liberacoes gerenciais desse
--      estouro, e das tentativas recusadas.
--
-- POR QUE O PROCESSO ENTRA NA CHAVE (decisao do usuario em 24/11/2026): um
-- semi-acabado produzido numa OP e consumido em outra, entao o mesmo codigo de
-- material aparece nas DUAS telas. "Quanto posso produzir a mais" nao e a mesma
-- decisao que "quanto posso consumir a mais", e uma chave so por material
-- obrigaria as duas a terem o mesmo numero.
--
-- POR QUE NAO REUSA tolerancia_recebimento_material (069): la a quantidade de
-- referencia vem do PEDIDO DE COMPRA; aqui vem da ORDEM DE PRODUCAO. Sao
-- grandezas de origens diferentes, e misturar as duas numa tabela so exigiria uma
-- coluna de "tipo" que o codigo teria de checar em todo lugar. A MATEMATICA, essa
-- sim, e compartilhada: existe uma classe de regra so para os dois (RegraTolerancia).
--
-- POR QUE UM LOG SEPARADO DO 070: liberacao_tolerancia_recebimento identifica o
-- caso por pedido+item, NOT NULL. Aqui o caso e ordem+operacao+componente. Enfiar
-- os dois na mesma tabela exigiria afrouxar colunas que hoje garantem integridade
-- de linhas JA GRAVADAS e auditadas. A PERMISSAO, essa sim, e a mesma
-- (LIBERAR_TOLERANCIA): quem pode liberar e o mesmo gerente.
--
-- ============================================================
-- AS TABELAS NAO TEM, E NAO DEVEM TER, COLUNA DE SENHA.
-- Mesma decisao do 070: as senhas vivem como hash BCrypt, e quem autorizou e
-- provado pela IDENTIDADE do usuario. Leia o cabecalho do 070 antes de mudar isso.
-- ============================================================
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $guarda$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '071 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_dev_app') THEN
        RAISE EXCEPTION '071 ABORTADO: papel fugapet_dev_app nao existe.';
    END IF;

    -- A tela LE o catalogo local (066). Sem ele ela abriria vazia e o usuario nao
    -- teria o que cadastrar — melhor abortar aqui do que entregar uma tela inutil.
    IF to_regclass('desenvolvimento.produto_catalogo_centro') IS NULL THEN
        RAISE EXCEPTION '071 ABORTADO: produto_catalogo_centro (066) ausente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM desenvolvimento.permissao
         WHERE modulo_permissao='PROCESSO_PRODUCAO'
           AND rotina_permissao='ENTRADA_PRODUTO'
           AND acao_permissao='LIBERAR_TOLERANCIA') THEN
        RAISE EXCEPTION '071 ABORTADO: permissao LIBERAR_TOLERANCIA (070) ausente. Aplique o 070 antes.';
    END IF;
END
$guarda$;

-- ------------------------------------------------------------
-- 1. A tolerancia por produto e processo
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS desenvolvimento.tolerancia_processo_producao
(
    codigo_tolerancia      bigserial     NOT NULL,
    centro                 text          NOT NULL,

    -- CONSUMO ou SEMI_ACABADO. Lista fechada no banco de proposito: um processo
    -- escrito errado criaria uma tolerancia que NUNCA seria encontrada pela tela,
    -- e o produto ficaria bloqueado sem que ninguem entendesse por que.
    processo               text          NOT NULL,
    material               text          NOT NULL,

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

    CONSTRAINT pk_tolerancia_processo_producao
        PRIMARY KEY (codigo_tolerancia),

    -- Um produto tem no maximo UMA tolerancia POR PROCESSO. Duas linhas fariam a
    -- tela escolher entre elas, e nao ha criterio para isso.
    CONSTRAINT uq_tolerancia_processo_producao
        UNIQUE (centro, processo, material),

    CONSTRAINT ck_tolerancia_processo_valido
        CHECK (processo IN ('CONSUMO', 'SEMI_ACABADO')),

    -- Acima de 100 o limite inferior ficaria NEGATIVO, o que nao significa nada.
    -- Zero e valido e significa exigencia exata.
    CONSTRAINT ck_tolerancia_processo_percentual
        CHECK (percentual_tolerancia >= 0 AND percentual_tolerancia <= 100),

    CONSTRAINT ck_tolerancia_processo_centro
        CHECK (btrim(centro) <> ''),

    CONSTRAINT ck_tolerancia_processo_material
        CHECK (btrim(material) <> '')
);

COMMENT ON TABLE desenvolvimento.tolerancia_processo_producao IS
    'Quanto a pesagem de um produto pode divergir do previsto da ordem de producao, POR PROCESSO (Consumo ou Semi-Acabado). O catalogo de produtos NAO mora aqui - vem do SAP (produto_catalogo_centro, incremento 066). O Recebimento tem a sua propria tabela (069).';

COMMENT ON COLUMN desenvolvimento.tolerancia_processo_producao.processo IS
    'CONSUMO ou SEMI_ACABADO. Faz parte da chave: o mesmo material e produzido num processo e consumido no outro, com decisoes diferentes.';

COMMENT ON COLUMN desenvolvimento.tolerancia_processo_producao.percentual_tolerancia IS
    'Percentual unico, valido para mais E para menos. 10.00 = o pesado pode ficar entre 90% e 110% do previsto. Zero = exigencia exata.';

COMMENT ON COLUMN desenvolvimento.tolerancia_processo_producao.situacao IS
    'false = tolerancia inativa. As telas tratam inativa como NAO CADASTRADA, e nao como "sem limite".';

-- Busca da tela: lista por centro e processo, ordenada por material.
CREATE INDEX IF NOT EXISTS ix_tolerancia_processo_centro
    ON desenvolvimento.tolerancia_processo_producao (centro, processo, material);

-- ------------------------------------------------------------
-- 2. Log das liberacoes gerenciais na producao
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS desenvolvimento.liberacao_tolerancia_processo
(
    codigo_liberacao             bigserial     NOT NULL,

    processo                     text          NOT NULL,
    numero_ordem                 text          NOT NULL,
    -- Operacao e componente identificam O QUE estourou dentro da ordem. No
    -- Semi-Acabado nao ha componente: a ordem produz um material so.
    operacao                     text          NULL,
    material                     text          NOT NULL,

    -- Os numeros que justificam a liberacao, congelados no momento dela. A
    -- tolerancia cadastrada pode mudar depois, e o log tem de continuar
    -- explicando a decisao tomada COM os valores de entao.
    quantidade_prevista          numeric(18,3) NULL,
    quantidade_apurada           numeric(18,3) NULL,
    limite_aplicado              numeric(18,3) NULL,
    percentual_tolerancia        numeric(5,2)  NULL,
    tipo_estouro                 text          NOT NULL,

    resultado                    text          NOT NULL,
    motivo_recusa                text          NULL,

    -- Quem AUTORIZOU. O codigo fica nulo quando o login digitado nem existe — e
    -- essa linha, com resultado RECUSADA, e justamente a que interessa auditar.
    codigo_usuario_autorizador   bigint        NULL,
    login_autorizador            text          NOT NULL,

    -- Quem OPERAVA a balanca. Sem isto o log diria quem liberou, mas nao para quem.
    codigo_usuario_operador      bigint        NULL,
    login_operador               text          NULL,

    terminal                     text          NULL,
    ocorrido_em                  timestamptz   NOT NULL DEFAULT clock_timestamp(),

    CONSTRAINT pk_liberacao_tolerancia_processo
        PRIMARY KEY (codigo_liberacao),

    CONSTRAINT ck_liberacao_processo_valido
        CHECK (processo IN ('CONSUMO', 'SEMI_ACABADO')),

    CONSTRAINT ck_liberacao_processo_tipo
        CHECK (tipo_estouro IN ('ACIMA_DO_TETO', 'ABAIXO_DO_PISO')),

    CONSTRAINT ck_liberacao_processo_resultado
        CHECK (resultado IN ('AUTORIZADA', 'RECUSADA')),

    CONSTRAINT ck_liberacao_processo_ordem
        CHECK (btrim(numero_ordem) <> ''),

    CONSTRAINT ck_liberacao_processo_login
        CHECK (btrim(login_autorizador) <> '')
);

COMMENT ON TABLE desenvolvimento.liberacao_tolerancia_processo IS
    'Log das liberacoes gerenciais de estouro de tolerancia no Consumo e no Semi-Acabado, e das tentativas RECUSADAS. NAO guarda senha: as senhas do sistema vivem como hash BCrypt e quem autorizou e provado pela identidade do usuario. O Recebimento tem o seu log proprio (070).';

COMMENT ON COLUMN desenvolvimento.liberacao_tolerancia_processo.operacao IS
    'Operacao da ordem, quando o processo a tem. Nulo no Semi-Acabado, que lanca pela ordem inteira.';

COMMENT ON COLUMN desenvolvimento.liberacao_tolerancia_processo.resultado IS
    'AUTORIZADA ou RECUSADA. A recusa e gravada de proposito: um log so de sucessos nao mostra tentativa de adivinhar credencial.';

CREATE INDEX IF NOT EXISTS ix_liberacao_processo_ordem
    ON desenvolvimento.liberacao_tolerancia_processo (numero_ordem, material);

CREATE INDEX IF NOT EXISTS ix_liberacao_processo_ocorrido
    ON desenvolvimento.liberacao_tolerancia_processo (ocorrido_em DESC);

-- ------------------------------------------------------------
-- 3. Permissoes de banco
-- ------------------------------------------------------------
-- A tolerancia e cadastro: a tela cria, altera e inativa.
GRANT SELECT, INSERT, UPDATE, DELETE
    ON desenvolvimento.tolerancia_processo_producao TO fugapet_dev_app;
GRANT USAGE, SELECT
    ON SEQUENCE desenvolvimento.tolerancia_processo_producao_codigo_tolerancia_seq TO fugapet_dev_app;

-- O log e so SELECT e INSERT: liberacao gravada nao se reescreve nem se apaga.
GRANT SELECT, INSERT
    ON desenvolvimento.liberacao_tolerancia_processo TO fugapet_dev_app;
GRANT USAGE, SELECT
    ON SEQUENCE desenvolvimento.liberacao_tolerancia_processo_codigo_liberacao_seq TO fugapet_dev_app;

COMMIT;

\echo '== 071 aplicado. Rode a VALIDACAO. =='
\echo '== LEMBRE: as tabelas nascem VAZIAS. Enquanto um produto nao tiver tolerancia =='
\echo '== cadastrada no processo, a pesagem dele fica BLOQUEADA nessa tela.          =='
