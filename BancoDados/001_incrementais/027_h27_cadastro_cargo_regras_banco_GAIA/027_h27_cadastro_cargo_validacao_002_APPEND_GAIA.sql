-- ============================================================
-- 027_h27_cadastro_cargo_validacao_002_APPEND_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Cargo
--
-- OBJETIVO
--   Bloco complementar para append ao 002_validar_banco_desenvolvimento_v1_1.sql
--   apos aplicacao do incremental 027.
--   Este script NAO altera dados: apenas valida objetos e inconsistencias.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
DECLARE
    v_qtd integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname = 'desenvolvimento'
           AND indexname = 'uq_cargo_nome'
    ) THEN
        RAISE EXCEPTION 'Indice uq_cargo_nome nao encontrado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'desenvolvimento'
          AND c.relname = 'cargo'
          AND t.tgname = 'trg_cargo_log_alteracao_cadastral'
          AND NOT t.tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_cargo_log_alteracao_cadastral nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'desenvolvimento'
          AND c.relname = 'cargo'
          AND t.tgname = 'trg_cargo_atualizado_em'
          AND NOT t.tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_cargo_atualizado_em nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_cargo_nome_nao_vazio'
           AND conrelid = 'desenvolvimento.cargo'::regclass
    ) THEN
        RAISE EXCEPTION 'CHECK ck_cargo_nome_nao_vazio nao encontrado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_cargo_nome_tamanho_funcional'
           AND conrelid = 'desenvolvimento.cargo'::regclass
           AND convalidated = true
    ) THEN
        RAISE EXCEPTION 'CHECK ck_cargo_nome_tamanho_funcional nao encontrado ou nao validado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_cargo_descricao_tamanho_funcional'
           AND conrelid = 'desenvolvimento.cargo'::regclass
           AND convalidated = true
    ) THEN
        RAISE EXCEPTION 'CHECK ck_cargo_descricao_tamanho_funcional nao encontrado ou nao validado.';
    END IF;

    IF to_regprocedure('desenvolvimento.fn_cargo_dependencias_ativas(bigint)') IS NULL THEN
        RAISE EXCEPTION 'Funcao fn_cargo_dependencias_ativas(bigint) nao encontrada.';
    END IF;

    IF to_regprocedure('desenvolvimento.fn_cargo_bloquear_inativacao_em_uso()') IS NULL THEN
        RAISE EXCEPTION 'Funcao fn_cargo_bloquear_inativacao_em_uso() nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.views
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'vw_cargo_diagnostico_inativacao'
    ) THEN
        RAISE EXCEPTION 'View vw_cargo_diagnostico_inativacao nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'desenvolvimento'
          AND c.relname = 'cargo'
          AND t.tgname = 'trg_cargo_bloquear_inativacao_em_uso'
          AND NOT t.tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_cargo_bloquear_inativacao_em_uso nao encontrada.';
    END IF;

    SELECT count(*) INTO v_qtd
      FROM (
            SELECT upper(trim(nome_cargo))
              FROM desenvolvimento.cargo
             WHERE situacao_cargo = true
             GROUP BY upper(trim(nome_cargo))
            HAVING count(*) > 1
      ) duplicados;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % grupos de cargos ativos duplicados por upper(trim(nome_cargo)).', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.usuario u
      JOIN desenvolvimento.cargo c ON c.codigo_cargo = u.codigo_cargo
     WHERE u.situacao_usuario = true
       AND c.situacao_cargo = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % usuarios ativos vinculados a cargos inativos.', v_qtd;
    END IF;
END $$;

SELECT 'CADASTRO DE CARGO DEV VALIDADO COM SUCESSO' AS resultado_validacao_cargo;

SELECT *
FROM desenvolvimento.vw_cargo_diagnostico_inativacao
ORDER BY nome_cargo;
