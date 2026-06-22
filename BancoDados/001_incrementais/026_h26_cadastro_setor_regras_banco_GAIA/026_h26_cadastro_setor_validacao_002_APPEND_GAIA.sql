-- ============================================================
-- 026_h26_cadastro_setor_validacao_002_APPEND_GAIA.sql
-- Bloco proposto para acrescentar ao 002_validar_banco_desenvolvimento_v1_1.sql
-- NAO altera dados. Apenas valida postura do Cadastro de Setor.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
DECLARE
    v_total integer;
BEGIN
    -- uq_setor_nome
    IF NOT EXISTS (
        SELECT 1
          FROM pg_indexes
         WHERE schemaname = 'desenvolvimento'
           AND tablename = 'setor'
           AND indexname = 'uq_setor_nome'
    ) THEN
        RAISE EXCEPTION 'Indice uq_setor_nome nao encontrado.';
    END IF;

    -- Trigger de log cadastral
    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgrelid = 'desenvolvimento.setor'::regclass
           AND tgname = 'trg_setor_log_alteracao_cadastral'
           AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_setor_log_alteracao_cadastral nao encontrada.';
    END IF;

    -- Trigger atualizado_em
    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgrelid = 'desenvolvimento.setor'::regclass
           AND tgname = 'trg_setor_atualizado_em'
           AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_setor_atualizado_em nao encontrada.';
    END IF;

    -- CHECK nome nao vazio
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.setor'::regclass
           AND conname = 'ck_setor_nome_nao_vazio'
           AND contype = 'c'
    ) THEN
        RAISE EXCEPTION 'Constraint ck_setor_nome_nao_vazio nao encontrada.';
    END IF;

    -- CHECKs funcionais do incremental proposto
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.setor'::regclass
           AND conname = 'ck_setor_nome_tamanho_funcional'
           AND contype = 'c'
    ) THEN
        RAISE EXCEPTION 'Constraint ck_setor_nome_tamanho_funcional nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.setor'::regclass
           AND conname = 'ck_setor_descricao_tamanho_funcional'
           AND contype = 'c'
    ) THEN
        RAISE EXCEPTION 'Constraint ck_setor_descricao_tamanho_funcional nao encontrada.';
    END IF;

    -- Funcao, view e trigger de bloqueio de inativacao
    IF NOT EXISTS (
        SELECT 1
          FROM pg_proc p
          JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'desenvolvimento'
           AND p.proname = 'fn_setor_dependencias_ativas'
    ) THEN
        RAISE EXCEPTION 'Funcao fn_setor_dependencias_ativas nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM information_schema.views
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'vw_setor_diagnostico_inativacao'
    ) THEN
        RAISE EXCEPTION 'View vw_setor_diagnostico_inativacao nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgrelid = 'desenvolvimento.setor'::regclass
           AND tgname = 'trg_setor_bloquear_inativacao_em_uso'
           AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_setor_bloquear_inativacao_em_uso nao encontrada.';
    END IF;

    -- Duplicidade ativa por upper(trim(nome_setor))
    SELECT count(*)
      INTO v_total
      FROM (
            SELECT upper(trim(nome_setor)) AS nome_normalizado, count(*) AS qtd
              FROM desenvolvimento.setor
             WHERE situacao_setor = true
             GROUP BY upper(trim(nome_setor))
            HAVING count(*) > 1
           ) d;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % nomes de setor ativos duplicados por upper(trim(nome_setor)).', v_total;
    END IF;

    -- Usuario ativo com setor padrao inativo
    SELECT count(*)
      INTO v_total
      FROM desenvolvimento.usuario u
      JOIN desenvolvimento.setor s ON s.codigo_setor = u.codigo_setor_padrao
     WHERE u.situacao_usuario = true
       AND s.situacao_setor = false;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % usuarios ativos com setor padrao inativo.', v_total;
    END IF;

    -- usuario_setor ativo apontando para setor inativo
    SELECT count(*)
      INTO v_total
      FROM desenvolvimento.usuario_setor us
      JOIN desenvolvimento.setor s ON s.codigo_setor = us.codigo_setor
     WHERE us.situacao_usuario_setor = true
       AND s.situacao_setor = false;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % vinculos usuario_setor ativos com setor inativo.', v_total;
    END IF;

    -- Balanca ativa vinculada a setor inativo
    SELECT count(*)
      INTO v_total
      FROM desenvolvimento.balanca b
      JOIN desenvolvimento.setor s ON s.codigo_setor = b.codigo_setor
     WHERE b.situacao_balanca = true
       AND s.situacao_setor = false;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % balancas ativas vinculadas a setor inativo.', v_total;
    END IF;

    -- Tara ativa vinculada a setor inativo
    SELECT count(*)
      INTO v_total
      FROM desenvolvimento.tara t
      JOIN desenvolvimento.setor s ON s.codigo_setor = t.codigo_setor
     WHERE t.situacao_tara = true
       AND s.situacao_setor = false;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % taras ativas vinculadas a setor inativo.', v_total;
    END IF;

    -- Parametro operacional ativo vinculado a setor inativo
    SELECT count(*)
      INTO v_total
      FROM desenvolvimento.parametro_operacao po
      JOIN desenvolvimento.setor s ON s.codigo_setor = po.codigo_setor
     WHERE po.situacao_parametro_operacao = true
       AND s.situacao_setor = false;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % parametros de operacao ativos vinculados a setor inativo.', v_total;
    END IF;

    -- Lancamento operacional vinculado a setor inativo
    SELECT count(*)
      INTO v_total
      FROM desenvolvimento.entrada_produto_lancamento epl
      JOIN desenvolvimento.setor s ON s.codigo_setor = epl.codigo_setor
     WHERE epl.situacao_entrada_produto_lancamento = true
       AND epl.status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO')
       AND s.situacao_setor = false;

    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % lancamentos operacionais vinculados a setor inativo.', v_total;
    END IF;
END $$;

SELECT 'CADASTRO DE SETOR VALIDADO COM SUCESSO' AS resultado_validacao_setor;

SELECT *
FROM desenvolvimento.vw_setor_diagnostico_inativacao
ORDER BY nome_setor;
