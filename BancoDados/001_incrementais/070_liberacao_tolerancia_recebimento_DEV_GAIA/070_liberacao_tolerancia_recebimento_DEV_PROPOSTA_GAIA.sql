\set ON_ERROR_STOP on
-- ============================================================
-- 070_liberacao_tolerancia_recebimento_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 070 - Liberacao gerencial do estouro de tolerancia no Recebimento
-- PROPOSTA (ESCRITA) - transacao unica
--
-- O QUE CRIA, em tres partes:
--   1. a TABELA DE LOG das liberacoes (e das tentativas recusadas);
--   2. a PERMISSAO nova (PROCESSO_PRODUCAO / ENTRADA_PRODUTO / LIBERAR_TOLERANCIA);
--   3. o PERFIL "Gerente", com essa permissao e NADA MAIS.
--
-- POR QUE UM PERFIL NOVO: o sistema nao tinha nivel gerencial — os perfis eram
-- Administrador, Supervisor de Producao, Operador de Balanca e Consulta. Conceder
-- a acao a um perfil existente daria a liberacao a TODOS os seus membros,
-- presentes e futuros, quando o usuario quer UMA OU DUAS pessoas. Perfil isolado
-- e o que permite escolher pessoa a pessoa, por usuario_perfil.
--
-- ============================================================
-- A TABELA NAO TEM, E NAO DEVE TER, COLUNA DE SENHA.
--
-- O pedido original dizia "saber o usuario, senha, pedido, material, data e hora".
-- Tudo menos a senha esta aqui. Senha em texto no banco seria o UNICO lugar do
-- sistema com credencial legivel — as senhas vivem como hash BCrypt, e e isso que
-- SenhaServico existe para garantir. Alem disso ela nao prova nada a quem le o log
-- depois: quem autorizou e provado pela IDENTIDADE do usuario, nao pela senha.
-- Guardar a senha so criaria um alvo. Se alguem for acrescentar essa coluna um dia,
-- leia este paragrafo primeiro.
-- ============================================================
--
-- AS TENTATIVAS RECUSADAS TAMBEM SAO GRAVADAS (resultado = 'RECUSADA'), com o
-- login digitado. E o que denuncia alguem tentando adivinhar credencial de
-- gerente — um log que so registra sucesso nao mostra ataque nenhum.
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '070 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_dev_app') THEN
        RAISE EXCEPTION '070 ABORTADO: papel fugapet_dev_app nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.permissao') IS NULL
       OR to_regclass('desenvolvimento.perfil_acesso') IS NULL
       OR to_regclass('desenvolvimento.perfil_permissao') IS NULL THEN
        RAISE EXCEPTION '070 ABORTADO: tabelas de seguranca ausentes.';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- 1. Log das liberacoes
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS desenvolvimento.liberacao_tolerancia_recebimento
(
    codigo_liberacao             bigserial     NOT NULL,

    numero_pedido                text          NOT NULL,
    numero_item                  text          NOT NULL,
    material                     text          NOT NULL,

    -- Os numeros que justificam a liberacao, congelados no momento dela. Guardados
    -- aqui de proposito: a tolerancia cadastrada pode mudar depois, e o log tem de
    -- continuar explicando a decisao que foi tomada COM os valores de entao.
    quantidade_pedida            numeric(18,3) NULL,
    quantidade_apurada           numeric(18,3) NULL,
    limite_aplicado              numeric(18,3) NULL,
    percentual_tolerancia        numeric(5,2)  NULL,
    tipo_estouro                 text          NOT NULL,

    resultado                    text          NOT NULL,
    motivo_recusa                text          NULL,

    -- Quem AUTORIZOU. O codigo fica nulo quando o login digitado nem existe —
    -- e essa linha, com resultado RECUSADA, e justamente a que interessa auditar.
    codigo_usuario_autorizador   bigint        NULL,
    login_autorizador            text          NOT NULL,

    -- Quem OPERAVA a balanca. Sem isto o log diria quem liberou, mas nao para quem.
    codigo_usuario_operador      bigint        NULL,
    login_operador               text          NULL,

    terminal                     text          NULL,
    ocorrido_em                  timestamptz   NOT NULL DEFAULT clock_timestamp(),

    CONSTRAINT pk_liberacao_tolerancia_recebimento
        PRIMARY KEY (codigo_liberacao),

    CONSTRAINT ck_liberacao_tolerancia_tipo
        CHECK (tipo_estouro IN ('ACIMA_DO_TETO', 'ABAIXO_DO_PISO')),

    CONSTRAINT ck_liberacao_tolerancia_resultado
        CHECK (resultado IN ('AUTORIZADA', 'RECUSADA')),

    CONSTRAINT ck_liberacao_tolerancia_pedido
        CHECK (btrim(numero_pedido) <> ''),

    CONSTRAINT ck_liberacao_tolerancia_login
        CHECK (btrim(login_autorizador) <> '')
);

COMMENT ON TABLE desenvolvimento.liberacao_tolerancia_recebimento IS
    'Log das liberacoes gerenciais de estouro de tolerancia no Recebimento, e das tentativas RECUSADAS. NAO guarda senha: as senhas do sistema vivem como hash BCrypt e quem autorizou e provado pela identidade do usuario.';

COMMENT ON COLUMN desenvolvimento.liberacao_tolerancia_recebimento.resultado IS
    'AUTORIZADA ou RECUSADA. A recusa e gravada de proposito: um log so de sucessos nao mostra tentativa de adivinhar credencial.';

COMMENT ON COLUMN desenvolvimento.liberacao_tolerancia_recebimento.quantidade_apurada IS
    'O total que estourou, congelado. A tolerancia cadastrada pode mudar depois; o log tem de continuar explicando a decisao de entao.';

CREATE INDEX IF NOT EXISTS ix_liberacao_tolerancia_pedido
    ON desenvolvimento.liberacao_tolerancia_recebimento (numero_pedido, numero_item);

CREATE INDEX IF NOT EXISTS ix_liberacao_tolerancia_ocorrido
    ON desenvolvimento.liberacao_tolerancia_recebimento (ocorrido_em DESC);

-- ------------------------------------------------------------
-- 2. A permissao nova
-- ------------------------------------------------------------
-- Idempotente por WHERE NOT EXISTS: a tabela nao tem unicidade em
-- (modulo, rotina, acao), entao ON CONFLICT nao se aplica.
INSERT INTO desenvolvimento.permissao
       (modulo_permissao, rotina_permissao, acao_permissao, descricao_permissao, situacao_permissao)
SELECT 'PROCESSO_PRODUCAO', 'ENTRADA_PRODUTO', 'LIBERAR_TOLERANCIA',
       'Liberar, com credencial propria, uma pesagem que estourou a tolerancia do material.', true
 WHERE NOT EXISTS (
     SELECT 1 FROM desenvolvimento.permissao
      WHERE modulo_permissao='PROCESSO_PRODUCAO'
        AND rotina_permissao='ENTRADA_PRODUTO'
        AND acao_permissao='LIBERAR_TOLERANCIA');

-- ------------------------------------------------------------
-- 3. O perfil Gerente, com essa permissao e nada mais
-- ------------------------------------------------------------
INSERT INTO desenvolvimento.perfil_acesso
       (nome_perfil_acesso, descricao_perfil_acesso, perfil_sistema, situacao_perfil_acesso)
SELECT 'Gerente',
       'Nivel gerencial: libera estouro de tolerancia no Recebimento. Vincule apenas as pessoas que respondem por essa decisao.',
       false, true
 WHERE NOT EXISTS (
     SELECT 1 FROM desenvolvimento.perfil_acesso WHERE nome_perfil_acesso='Gerente');

INSERT INTO desenvolvimento.perfil_permissao
       (codigo_perfil_acesso, codigo_permissao, situacao_perfil_permissao)
SELECT pa.codigo_perfil_acesso, p.codigo_permissao, true
  FROM desenvolvimento.perfil_acesso pa
 CROSS JOIN desenvolvimento.permissao p
 WHERE pa.nome_perfil_acesso='Gerente'
   AND p.modulo_permissao='PROCESSO_PRODUCAO'
   AND p.rotina_permissao='ENTRADA_PRODUTO'
   AND p.acao_permissao='LIBERAR_TOLERANCIA'
   AND NOT EXISTS (
       SELECT 1 FROM desenvolvimento.perfil_permissao pp
        WHERE pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
          AND pp.codigo_permissao     = p.codigo_permissao);

-- ------------------------------------------------------------
-- 4. Permissoes de banco
-- ------------------------------------------------------------
-- SELECT e INSERT apenas: liberacao gravada nao se reescreve nem se apaga.
GRANT SELECT, INSERT
    ON desenvolvimento.liberacao_tolerancia_recebimento TO fugapet_dev_app;
GRANT USAGE, SELECT
    ON SEQUENCE desenvolvimento.liberacao_tolerancia_recebimento_codigo_liberacao_seq TO fugapet_dev_app;

COMMIT;

\echo '== 070 aplicado. Rode a VALIDACAO. =='
\echo '== LEMBRE: o perfil Gerente nasce SEM usuarios. Vincule as pessoas em Seguranca. =='
