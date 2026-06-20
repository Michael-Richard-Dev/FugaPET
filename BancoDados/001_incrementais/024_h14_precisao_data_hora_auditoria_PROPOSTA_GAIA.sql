-- ============================================================================
-- FugaPET_Dev - H14 - PROPOSTA DE INCREMENTAL PARA O GAIA DADOS
-- PRECISAO INTEGRAL DE DATA/HORA PARA EVENTOS INDUSTRIAIS E AUDITORIA
-- NAO EXECUTAR SEM REVISAO E APROVACAO DO GAIA DADOS.
-- Alvo: fuga_jales_local_desenvolvimento / schema desenvolvimento.
--
-- PRINCIPIOS:
--   - nao alterar o baseline consolidado;
--   - nao reescrever timestamps historicos ja truncados;
--   - manter o tipo timestamptz e os indices existentes;
--   - usar clock_timestamp() quando a ordem real das acoes importa;
--   - a UI pode continuar exibindo somente minuto/segundo.
-- ============================================================================

BEGIN;
SET LOCAL search_path TO desenvolvimento, public;

DO $$
DECLARE
    v_objeto text;
    v_obrigatorios text[] := ARRAY[
        'auditoria_acao_usuario.auditoria_acao_usuario_criado_em',
        'log_alteracao_cadastral.log_alteracao_cadastral_criado_em',
        'sap_sincronizacao_execucao.iniciado_em',
        'sap_sincronizacao_execucao.sap_sincronizacao_execucao_criado_em',
        'pesagem_entrada_item_leitura.capturado_em',
        'entrada_produto_pesagem.pesado_em',
        'versao_banco.aplicado_em'
    ];
BEGIN
    IF to_regprocedure('desenvolvimento.fn_definir_atualizado_em()') IS NULL THEN
        RAISE EXCEPTION 'Funcao fn_definir_atualizado_em nao encontrada.';
    END IF;

    FOREACH v_objeto IN ARRAY v_obrigatorios
    LOOP
        IF NOT EXISTS (
            SELECT 1
              FROM information_schema.columns
             WHERE table_schema = 'desenvolvimento'
               AND table_name = split_part(v_objeto, '.', 1)
               AND column_name = split_part(v_objeto, '.', 2)
               AND data_type = 'timestamp with time zone'
        ) THEN
            RAISE EXCEPTION 'H14 nao aplicado: coluna obrigatoria ausente ou nao timestamptz: %.', v_objeto;
        END IF;
    END LOOP;
END $$;

-- A funcao anterior convertia o instante para texto sem fracao de segundo.
-- clock_timestamp() preserva o instante real de cada UPDATE, inclusive dentro
-- da mesma transacao.
CREATE OR REPLACE FUNCTION desenvolvimento.fn_definir_atualizado_em()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_coluna_tabela text := TG_TABLE_NAME || '_atualizado_em';
BEGIN
    IF to_jsonb(NEW) ? v_coluna_tabela THEN
        NEW := jsonb_populate_record(
            NEW,
            jsonb_build_object(v_coluna_tabela, clock_timestamp())
        );
    END IF;

    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION desenvolvimento.fn_definir_atualizado_em() IS
    'Preenche <tabela>_atualizado_em com clock_timestamp(), preservando precisao integral e ordem real dos UPDATEs.';

-- ============================================================================
-- AUDITORIA E LOGIN
-- Login e atualizado pelo repositorio C# com clock_timestamp().
-- ============================================================================

ALTER TABLE desenvolvimento.log_alteracao_cadastral
    ALTER COLUMN log_alteracao_cadastral_criado_em
    SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.auditoria_acao_usuario
    ALTER COLUMN auditoria_acao_usuario_criado_em
    SET DEFAULT clock_timestamp();

-- log_integracao_sap.registrado_em_utc ja usa now() sem truncamento.

-- ============================================================================
-- SINCRONIZACAO E CACHE SAP
-- ============================================================================

ALTER TABLE desenvolvimento.sap_sincronizacao_execucao
    ALTER COLUMN iniciado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_sincronizacao_execucao_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_payload_recebido
    ALTER COLUMN recebido_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_payload_recebido_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_centro
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_centro_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_deposito
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_deposito_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_unidade_medida
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_unidade_medida_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_produto
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_produto_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_lote
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_lote_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_fornecedor
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_fornecedor_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_pedido_compra
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_pedido_compra_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_pedido_compra_item
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_pedido_compra_item_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_ordem_producao
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_ordem_producao_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_ordem_producao_item
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_ordem_producao_item_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_ordem_producao_componente
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_ordem_producao_componente_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_reserva_item
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_reserva_item_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.sap_norma_embalagem
    ALTER COLUMN sincronizado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN sap_norma_embalagem_criado_em
        SET DEFAULT clock_timestamp();

-- ============================================================================
-- CONSULTAS, MOVIMENTACOES E INTEGRACOES INT012
-- ============================================================================

ALTER TABLE desenvolvimento.norma_embalagem_cache
    ALTER COLUMN ultima_consulta_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN norma_embalagem_cache_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.norma_embalagem_item_cache
    ALTER COLUMN norma_embalagem_item_cache_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.norma_embalagem_sincronizacao
    ALTER COLUMN iniciado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN norma_embalagem_sincronizacao_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.norma_embalagem_consulta_log
    ALTER COLUMN consulta_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN norma_embalagem_consulta_log_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa
    ALTER COLUMN hu_caixa_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa_pesagem
    ALTER COLUMN pesado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN hu_caixa_pesagem_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa_integracao_sap
    ALTER COLUMN processado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN hu_caixa_integracao_sap_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_caixa_etiqueta
    ALTER COLUMN hu_caixa_etiqueta_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_palete
    ALTER COLUMN hu_palete_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_palete_item
    ALTER COLUMN hu_palete_item_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_palete_integracao_sap
    ALTER COLUMN processado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN hu_palete_integracao_sap_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.hu_palete_etiqueta
    ALTER COLUMN hu_palete_etiqueta_criado_em SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.integracao_sap_int012_log
    ALTER COLUMN integracao_sap_int012_log_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.integracao_sap_int012_mensagem
    ALTER COLUMN integracao_sap_int012_mensagem_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.integracao_sap_int012_reprocessamento
    ALTER COLUMN solicitado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN integracao_sap_int012_reprocessamento_criado_em
        SET DEFAULT clock_timestamp();

-- ============================================================================
-- PESAGENS E LEITURAS
-- ============================================================================

ALTER TABLE desenvolvimento.pesagem_entrada_item
    ALTER COLUMN pesagem_entrada_item_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.pesagem_entrada_item_leitura
    ALTER COLUMN capturado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN pesagem_entrada_item_leitura_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.entrada_produto_lancamento
    ALTER COLUMN iniciado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN entrada_produto_lancamento_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.entrada_produto_item
    ALTER COLUMN entrada_produto_item_criado_em
        SET DEFAULT clock_timestamp();

ALTER TABLE desenvolvimento.entrada_produto_pesagem
    ALTER COLUMN pesado_em SET DEFAULT clock_timestamp(),
    ALTER COLUMN entrada_produto_pesagem_criado_em
        SET DEFAULT clock_timestamp();

-- ============================================================================
-- VERSAO DO BANCO
-- ============================================================================

ALTER TABLE desenvolvimento.versao_banco
    ALTER COLUMN aplicado_em SET DEFAULT clock_timestamp();

COMMIT;

-- ============================================================================
-- VALIDACAO POS-APLICACAO PARA O GAIA/USUARIO
-- Executar somente depois da aplicacao autorizada.
-- ============================================================================
--
-- SELECT table_name, column_name, column_default
--   FROM information_schema.columns
--  WHERE table_schema = 'desenvolvimento'
--    AND (
--        (table_name = 'entrada_produto_pesagem' AND column_name = 'pesado_em')
--        OR (table_name = 'pesagem_entrada_item_leitura' AND column_name = 'capturado_em')
--        OR (table_name = 'auditoria_acao_usuario' AND column_name = 'auditoria_acao_usuario_criado_em')
--        OR (table_name = 'sap_sincronizacao_execucao' AND column_name = 'iniciado_em')
--        OR (table_name = 'versao_banco' AND column_name = 'aplicado_em')
--    )
--  ORDER BY table_name, column_name;
--
-- Este incremental nao altera dados historicos. Registros anteriores podem
-- continuar com segundos/microssegundos zerados.
