-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================
-- 026_h26_cadastro_setor_validacao_002_HML_APPEND_GAIA.sql
-- Projeto FugaPET_HML
-- Bloco complementar de validacao do incremental 026.
-- NAO altera dados. Apenas valida postura do Cadastro de Setor.
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_total integer;
BEGIN
    -- Confirmacao de schema correto
    IF current_schema <> 'homologacao' THEN
        RAISE EXCEPTION 'Schema incorreto. Esperado homologacao, encontrado %.', current_schema;
    END IF;

    -- uq_setor_nome
    IF NOT EXISTS (
        SELECT 1
          FROM pg_indexes
         WHERE schemaname = 'homologacao'
           AND tablename = 'setor'
           AND indexname = 'uq_setor_nome'
    ) THEN
        RAISE EXCEPTION 'Indice uq_setor_nome nao encontrado.';
    END IF;

    -- Trigger de log cadastral
    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgrelid = 'homologacao.setor'::regclass
           AND tgname = 'trg_setor_log_alteracao_cadastral'
           AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_setor_log_alteracao_cadastral nao encontrada.';
    END IF;

    -- Trigger atualizado_em
    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgrelid = 'homologacao.setor'::regclass
           AND tgname = 'trg_setor_atualizado_em'
           AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION 'Trigger trg_setor_atualizado_em nao encontrada.';
    END IF;

    -- CHECK nome nao vazio
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'homologacao.setor'::regclass
           AND conname = 'ck_setor_nome_nao_vazio'
           AND contype = 'c'
    ) THEN
        RAISE EXCEPTION 'Constraint ck_setor_nome_nao_vazio nao encontrada.';
    END IF;

    -- CHECKs funcionais do incremental 026
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'homologacao.setor'::regclass
           AND conname = 'ck_setor_nome_tamanho_funcional'
           AND contype = 'c'
    ) THEN
        RAISE EXCEPTION 'Constraint ck_setor_nome_tamanho_funcional nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'homologacao.setor'::regclass
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
         WHERE n.nspname = 'homologacao'
           AND p.proname = 'fn_setor_dependencias_ativas'
    ) THEN
        RAISE EXCEPTION 'Funcao fn_setor_dependencias_ativas nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM information_schema.views
         WHERE table_schema = 'homologacao'
           AND table_name = 'vw_setor_diagnostico_inativacao'
    ) THEN
        RAISE EXCEPTION 'View vw_setor_diagnostico_inativacao nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgrelid = 'homologacao.setor'::regclass
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
              FROM homologacao.setor
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
      FROM homologacao.usuario u
      JOIN homologacao.setor s ON s.codigo_setor = u.codigo_setor_padrao
     WHERE u.situacao_usuario = true
       AND s.situacao_setor = false;
    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % usuarios ativos com setor padrao inativo.', v_total;
    END IF;

    -- usuario_setor ativo apontando para setor inativo
    SELECT count(*)
      INTO v_total
      FROM homologacao.usuario_setor us
      JOIN homologacao.setor s ON s.codigo_setor = us.codigo_setor
     WHERE us.situacao_usuario_setor = true
       AND s.situacao_setor = false;
    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % vinculos usuario_setor ativos com setor inativo.', v_total;
    END IF;

    -- Balanca ativa vinculada a setor inativo
    SELECT count(*)
      INTO v_total
      FROM homologacao.balanca b
      JOIN homologacao.setor s ON s.codigo_setor = b.codigo_setor
     WHERE b.situacao_balanca = true
       AND s.situacao_setor = false;
    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % balancas ativas vinculadas a setor inativo.', v_total;
    END IF;

    -- Tara ativa vinculada a setor inativo
    SELECT count(*)
      INTO v_total
      FROM homologacao.tara t
      JOIN homologacao.setor s ON s.codigo_setor = t.codigo_setor
     WHERE t.situacao_tara = true
       AND s.situacao_setor = false;
    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % taras ativas vinculadas a setor inativo.', v_total;
    END IF;

    -- Parametro operacional ativo vinculado a setor inativo
    SELECT count(*)
      INTO v_total
      FROM homologacao.parametro_operacao po
      JOIN homologacao.setor s ON s.codigo_setor = po.codigo_setor
     WHERE po.situacao_parametro_operacao = true
       AND s.situacao_setor = false;
    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % parametros de operacao ativos vinculados a setor inativo.', v_total;
    END IF;

    -- Lancamento operacional vinculado a setor inativo
    SELECT count(*)
      INTO v_total
      FROM homologacao.entrada_produto_lancamento epl
      JOIN homologacao.setor s ON s.codigo_setor = epl.codigo_setor
     WHERE epl.situacao_entrada_produto_lancamento = true
       AND epl.status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO')
       AND s.situacao_setor = false;
    IF v_total > 0 THEN
        RAISE EXCEPTION 'Existem % lancamentos operacionais vinculados a setor inativo.', v_total;
    END IF;
END $$;

SELECT 'CADASTRO DE SETOR HML VALIDADO COM SUCESSO' AS resultado_validacao_setor;

SELECT *
FROM homologacao.vw_setor_diagnostico_inativacao
ORDER BY nome_setor;
