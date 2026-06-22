-- ============================================================
-- 026_h26_cadastro_setor_regras_banco_ROLLBACK_GAIA.sql
-- Rollback do incremental proposto para regras de banco do Cadastro de Setor.
-- NAO executar automaticamente.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

DROP TRIGGER IF EXISTS trg_setor_bloquear_inativacao_em_uso ON desenvolvimento.setor;
DROP FUNCTION IF EXISTS desenvolvimento.fn_bloquear_inativacao_setor_em_uso();
DROP VIEW IF EXISTS desenvolvimento.vw_setor_diagnostico_inativacao;
DROP FUNCTION IF EXISTS desenvolvimento.fn_setor_dependencias_ativas(bigint);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'desenvolvimento.setor'::regclass
          AND conname = 'ck_setor_nome_tamanho_funcional'
    ) THEN
        ALTER TABLE desenvolvimento.setor DROP CONSTRAINT ck_setor_nome_tamanho_funcional;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'desenvolvimento.setor'::regclass
          AND conname = 'ck_setor_descricao_tamanho_funcional'
    ) THEN
        ALTER TABLE desenvolvimento.setor DROP CONSTRAINT ck_setor_descricao_tamanho_funcional;
    END IF;
END $$;

COMMIT;
