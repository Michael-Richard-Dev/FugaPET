-- ============================================================
-- 030_consumo_material_persistencia_local_VALIDACAO_GAIA.sql
-- Projeto FugaPET - Schema homologacao
-- Valida a PROPOSTA 030: tabelas, PKs, FKs, indices e triggers.
-- Somente leitura (RAISE EXCEPTION em caso de falha). NAO altera dados.
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_tabela text;
    v_fk     text;
    v_ix     text;
    v_trg    text;
BEGIN
    -- 1. Tabelas existem
    FOREACH v_tabela IN ARRAY ARRAY[
        'consumo_material_lancamento', 'consumo_material_item', 'consumo_material_pesagem'
    ] LOOP
        IF to_regclass('homologacao.' || v_tabela) IS NULL THEN
            RAISE EXCEPTION 'VALIDACAO 030 FALHOU: tabela % nao existe.', v_tabela;
        END IF;
    END LOOP;

    -- 2. Chaves primarias
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'homologacao.consumo_material_lancamento'::regclass AND contype = 'p') THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: PK ausente em consumo_material_lancamento.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'homologacao.consumo_material_item'::regclass AND contype = 'p') THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: PK ausente em consumo_material_item.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'homologacao.consumo_material_pesagem'::regclass AND contype = 'p') THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: PK ausente em consumo_material_pesagem.';
    END IF;

    -- 3. Chaves estrangeiras (item -> lancamento; pesagem -> item)
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'homologacao.consumo_material_item'::regclass AND contype = 'f'
                      AND confrelid = 'homologacao.consumo_material_lancamento'::regclass) THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: FK item->lancamento ausente.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'homologacao.consumo_material_pesagem'::regclass AND contype = 'f'
                      AND confrelid = 'homologacao.consumo_material_item'::regclass) THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: FK pesagem->item ausente.';
    END IF;

    -- 4. Indices minimos
    FOREACH v_ix IN ARRAY ARRAY[
        'ix_consumo_lancamento_numero_ordem',
        'ix_consumo_lancamento_status',
        'ix_consumo_lancamento_doc_material_sap',
        'ix_consumo_item_lancamento',
        'ix_consumo_item_reserva',
        'ix_consumo_pesagem_item'
    ] LOOP
        IF to_regclass('homologacao.' || v_ix) IS NULL THEN
            RAISE EXCEPTION 'VALIDACAO 030 FALHOU: indice % ausente.', v_ix;
        END IF;
    END LOOP;

    -- 5. Triggers de atualizado_em
    FOREACH v_trg IN ARRAY ARRAY[
        'trg_consumo_material_lancamento_atualizado_em',
        'trg_consumo_material_item_atualizado_em'
    ] LOOP
        IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = v_trg AND NOT tgisinternal) THEN
            RAISE EXCEPTION 'VALIDACAO 030 FALHOU: trigger % ausente.', v_trg;
        END IF;
    END LOOP;

    -- 6. Status ENVIANDO_SAP permitido (claim atomico de envio) nas constraints de status
    IF position('ENVIANDO_SAP' IN pg_get_constraintdef(
            (SELECT oid FROM pg_constraint
              WHERE conname = 'ck_consumo_lancamento_status'
                AND conrelid = 'homologacao.consumo_material_lancamento'::regclass))) = 0 THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: ck_consumo_lancamento_status nao permite ENVIANDO_SAP.';
    END IF;
    IF position('ENVIANDO_SAP' IN pg_get_constraintdef(
            (SELECT oid FROM pg_constraint
              WHERE conname = 'ck_consumo_item_status'
                AND conrelid = 'homologacao.consumo_material_item'::regclass))) = 0 THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: ck_consumo_item_status nao permite ENVIANDO_SAP.';
    END IF;

    -- 7. Status local de pesagem permitido para consistencia com diagnostico 261
    IF position('REGISTRADA_LOCALMENTE' IN pg_get_constraintdef(
            (SELECT oid FROM pg_constraint
              WHERE conname = 'ck_consumo_pesagem_status'
                AND conrelid = 'homologacao.consumo_material_pesagem'::regclass))) = 0 THEN
        RAISE EXCEPTION 'VALIDACAO 030 FALHOU: ck_consumo_pesagem_status nao permite REGISTRADA_LOCALMENTE.';
    END IF;

    RAISE NOTICE 'VALIDACAO 030 OK: tabelas, PKs, FKs, indices, triggers e status ENVIANDO_SAP/REGISTRADA_LOCALMENTE presentes.';
END $$;

SELECT 'VALIDACAO 030 (homologacao) CONCLUIDA' AS resultado_validacao;
