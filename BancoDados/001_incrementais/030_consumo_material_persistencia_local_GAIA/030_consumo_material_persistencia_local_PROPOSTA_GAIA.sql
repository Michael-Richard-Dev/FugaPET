-- ============================================================
-- 030_consumo_material_persistencia_local_PROPOSTA_GAIA.sql
-- Projeto FugaPET
-- Banco PostgreSQL - Schema homologacao
-- Objeto: Tela de Consumo de Materia-Prima - persistencia LOCAL do consumo
-- Responsavel tecnico: Equipe FugaPET (proposta para Gaia Dados)
-- Pacote: incremental 030
--
-- OBJETIVO
--   Persistir LOCALMENTE (sem envio SAP) o consumo pesado na Tela de Consumo de
--   Materia-Prima, com rastreabilidade por OP / componente / reserva / item da
--   reserva / deposito / lote / tara / peso bruto / peso liquido / origem /
--   usuario / status local. O lancamento nasce PENDENTE_SAP. Nenhum movimento 261
--   e criado nesta etapa; colunas SAP futuras ficam nullable.
--
-- ESCOPO (3 tabelas novas no schema homologacao)
--   consumo_material_lancamento  - cabecalho do consumo por OP
--   consumo_material_item        - componente consumido dentro da OP
--   consumo_material_pesagem     - detalhe de cada pesagem do componente
--
-- AVISO: NAO executar automaticamente. Revisao Gaia Dados obrigatoria.
-- Ordem de execucao: PROPOSTA -> VALIDACAO -> (se necessario) ROLLBACK
-- Idempotente: CREATE TABLE/INDEX IF NOT EXISTS; triggers recriadas com DROP IF EXISTS.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

-- 0. Travas: schema e funcao padrao de atualizado_em devem existir.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'homologacao') THEN
        RAISE EXCEPTION 'Falha: schema homologacao nao existe.';
    END IF;
    IF to_regprocedure('homologacao.fn_definir_atualizado_em()') IS NULL THEN
        RAISE EXCEPTION 'Falha: funcao homologacao.fn_definir_atualizado_em() nao existe (baseline).';
    END IF;
END $$;

-- 1. Cabecalho do consumo local por OP -----------------------------------------
CREATE TABLE IF NOT EXISTS homologacao.consumo_material_lancamento (
    codigo_consumo_material_lancamento        bigserial    PRIMARY KEY,
    numero_ordem                              varchar(12)  NOT NULL,
    centro                                    varchar(4),
    material_produzido                        varchar(40),
    lote_ordem                                varchar(40),
    quantidade_prevista                       numeric(18,3),
    unidade                                   varchar(3),
    status_lancamento                         varchar(30)  NOT NULL DEFAULT 'PENDENTE_SAP',
    observacao                                text,
    usuario_criacao                           varchar(120),
    -- Campos SAP futuros (nullable; preenchidos somente no envio, fora desta tarefa)
    documento_material_sap                    varchar(10),
    exercicio_documento_material_sap          varchar(4),
    confirmation_group_sap                    varchar(10),
    confirmation_count_sap                    varchar(8),
    enviado_sap_em                            timestamptz,
    consumo_material_lancamento_criado_em     timestamptz  NOT NULL DEFAULT now(),
    consumo_material_lancamento_atualizado_em timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_consumo_lancamento_status CHECK (
        status_lancamento IN ('PENDENTE_SAP', 'ENVIANDO_SAP', 'CONFIRMADO_SAP', 'FALHA_SAP', 'CANCELADO_LOCAL')
    ),
    CONSTRAINT ck_consumo_lancamento_exercicio_formato CHECK (
        exercicio_documento_material_sap IS NULL
        OR exercicio_documento_material_sap ~ '^[0-9]{4}$'
    )
);

-- 2. Componente consumido dentro da OP -----------------------------------------
CREATE TABLE IF NOT EXISTS homologacao.consumo_material_item (
    codigo_consumo_material_item        bigserial    PRIMARY KEY,
    codigo_consumo_material_lancamento  bigint       NOT NULL
        REFERENCES homologacao.consumo_material_lancamento (codigo_consumo_material_lancamento)
        ON DELETE CASCADE,
    numero_ordem                        varchar(12)  NOT NULL,
    codigo_material                     varchar(40)  NOT NULL,
    descricao_material                  varchar(255),
    centro                              varchar(4),
    deposito_consumo                    varchar(4),
    numero_reserva                      varchar(10),
    item_reserva                        varchar(8),
    lote                                varchar(40),
    quantidade_prevista                 numeric(18,3),
    quantidade_retirada_sap             numeric(18,3),
    quantidade_pendente_sap             numeric(18,3),
    quantidade_consumida_local          numeric(18,3) NOT NULL DEFAULT 0,
    unidade                             varchar(3)   NOT NULL,
    tipo_movimento_sap                  varchar(3)   NOT NULL DEFAULT '261',
    status_item                         varchar(30)  NOT NULL DEFAULT 'PENDENTE_SAP',
    consumo_material_item_criado_em     timestamptz  NOT NULL DEFAULT now(),
    consumo_material_item_atualizado_em timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_consumo_item_status CHECK (
        status_item IN ('PENDENTE_SAP', 'ENVIANDO_SAP', 'CONFIRMADO_SAP', 'FALHA_SAP', 'CANCELADO_LOCAL')
    )
);

-- 3. Detalhe de cada pesagem do componente -------------------------------------
CREATE TABLE IF NOT EXISTS homologacao.consumo_material_pesagem (
    codigo_consumo_material_pesagem     bigserial    PRIMARY KEY,
    codigo_consumo_material_item        bigint       NOT NULL
        REFERENCES homologacao.consumo_material_item (codigo_consumo_material_item)
        ON DELETE CASCADE,
    sequencia                           integer      NOT NULL,
    peso_bruto_kg                       numeric(18,3) NOT NULL,
    peso_tara_kg                        numeric(18,3) NOT NULL,
    peso_liquido_kg                     numeric(18,3) NOT NULL,
    unidade                             varchar(3)   NOT NULL DEFAULT 'KG',
    origem                              varchar(20)  NOT NULL,
    status_pesagem                      varchar(30)  NOT NULL DEFAULT 'REGISTRADA_LOCALMENTE',
    pesado_em                           timestamptz  NOT NULL,
    usuario_criacao                     varchar(120),
    consumo_material_pesagem_criado_em  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_consumo_pesagem_origem CHECK (origem IN ('BALANCA', 'MANUAL')),
    CONSTRAINT ck_consumo_pesagem_status CHECK (
        status_pesagem IN ('REGISTRADA_LOCALMENTE')
    ),
    CONSTRAINT ck_consumo_pesagem_liquido_positivo CHECK (peso_liquido_kg > 0),
    CONSTRAINT ck_consumo_pesagem_tara_nao_negativa CHECK (peso_tara_kg >= 0)
);

-- 4. Indices -------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_consumo_lancamento_numero_ordem
    ON homologacao.consumo_material_lancamento (numero_ordem);
CREATE INDEX IF NOT EXISTS ix_consumo_lancamento_status
    ON homologacao.consumo_material_lancamento (status_lancamento);
CREATE INDEX IF NOT EXISTS ix_consumo_lancamento_doc_material_sap
    ON homologacao.consumo_material_lancamento (documento_material_sap, exercicio_documento_material_sap);

CREATE INDEX IF NOT EXISTS ix_consumo_item_lancamento
    ON homologacao.consumo_material_item (codigo_consumo_material_lancamento);
CREATE INDEX IF NOT EXISTS ix_consumo_item_reserva
    ON homologacao.consumo_material_item (numero_reserva, item_reserva);

CREATE INDEX IF NOT EXISTS ix_consumo_pesagem_item
    ON homologacao.consumo_material_pesagem (codigo_consumo_material_item);

-- 5. Triggers de atualizado_em (reaproveita a funcao padrao da baseline) --------
DO $$
DECLARE
    nome_tabela text;
BEGIN
    FOREACH nome_tabela IN ARRAY ARRAY['consumo_material_lancamento', 'consumo_material_item'] LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS trg_%I_atualizado_em ON homologacao.%I', nome_tabela, nome_tabela);
        EXECUTE format(
            'CREATE TRIGGER trg_%I_atualizado_em
                 BEFORE UPDATE ON homologacao.%I
                 FOR EACH ROW
                 EXECUTE FUNCTION homologacao.fn_definir_atualizado_em()',
            nome_tabela, nome_tabela);
    END LOOP;
END $$;

-- 6. Comentarios ---------------------------------------------------------------
COMMENT ON TABLE homologacao.consumo_material_lancamento IS
    'Cabecalho do consumo LOCAL de materia-prima por OP. Nasce PENDENTE_SAP; envio SAP (261) e etapa futura.';
COMMENT ON TABLE homologacao.consumo_material_item IS
    'Componente consumido dentro da OP (rastreabilidade reserva/item/lote/deposito) com total consumido local.';
COMMENT ON TABLE homologacao.consumo_material_pesagem IS
    'Detalhe de cada pesagem (BALANCA/MANUAL) do componente, status local REGISTRADA_LOCALMENTE.';

COMMIT;

SELECT 'PROPOSTA 030 (homologacao) APLICADA - 3 TABELAS, INDICES E TRIGGERS CRIADOS' AS resultado_proposta;
