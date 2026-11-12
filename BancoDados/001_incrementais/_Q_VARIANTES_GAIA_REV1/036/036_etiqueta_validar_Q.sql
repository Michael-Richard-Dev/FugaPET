\set ON_ERROR_STOP on
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
-- 036_etiqueta_validar_HML.sql
-- Projeto FugaPET | Schema: homologacao
-- Objeto: Cadastro de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 036
-- Finalidade: validacao semantica e testes transacionais
-- ============================================================

BEGIN;
SET LOCAL search_path TO homologacao, public;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_comment constant text := 'FugaPET incremental 036 etiqueta';
    v_falta text := '';
    v_expr text;
    v_pred text;
    v_norm text;
    v_def text;
    v_idx_oid oid;
    v_func_oid oid;
    v_trigger_oid oid;
    v_ok boolean;
    v_bloqueado boolean;
    v_prefixo text := 'TST036_' || txid_current()::text || '_';
    v_modelo bigint;
    v_etiqueta_base bigint;
    v_etiqueta_livre bigint;
    v_etiqueta_com_vinculo bigint;
    v_produto bigint;
BEGIN
    -- Indice global: UNIQUE, tabela correta, expressao upper(trim(codigo_interno)), sem WHERE, comentario 036.
    SELECT i.indexrelid, pg_get_expr(i.indexprs, i.indrelid, true), pg_get_expr(i.indpred, i.indrelid, true)
      INTO v_idx_oid, v_expr, v_pred
      FROM pg_index i
     WHERE i.indexrelid = to_regclass('homologacao.uq_etiqueta_codigo_interno_global')
       AND i.indrelid = 'homologacao.etiqueta'::regclass
       AND i.indisunique = true
       AND i.indisvalid = true;

    IF v_idx_oid IS NULL THEN
        v_falta := v_falta || 'indice global ausente/nao unique/invalido/tabela incorreta; ';
    ELSE
        v_norm := regexp_replace(regexp_replace(lower(coalesce(v_expr, '')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');
        IF coalesce(v_pred, '') <> '' THEN
            v_falta := v_falta || 'indice global possui predicado WHERE; ';
        END IF;
        IF v_norm NOT LIKE '%upper%' OR (v_norm NOT LIKE '%trim%' AND v_norm NOT LIKE '%btrim%') OR v_norm NOT LIKE '%codigo_interno%' THEN
            v_falta := v_falta || 'indice global com expressao divergente para upper(trim(codigo_interno)); ';
        END IF;
        IF coalesce(obj_description(v_idx_oid, 'pg_class'), '') <> v_comment THEN
            v_falta := v_falta || 'indice global sem comentario de propriedade 036; ';
        END IF;
    END IF;

    IF to_regclass('homologacao.uq_etiqueta_codigo') IS NOT NULL THEN
        v_falta := v_falta || 'indice parcial antigo uq_etiqueta_codigo ainda existe; ';
    END IF;
    IF to_regclass('homologacao.uq_etiqueta_codigo_interno_ativo') IS NOT NULL THEN
        v_falta := v_falta || 'indice parcial historico uq_etiqueta_codigo_interno_ativo ainda existe; ';
    END IF;

    -- Funcao.
    SELECT p.oid INTO v_func_oid
      FROM pg_proc p JOIN pg_namespace ns ON ns.oid = p.pronamespace
     WHERE ns.nspname = 'homologacao'
       AND p.proname = 'fn_bloqueia_inativar_etiqueta_com_produto_ativo'
       AND p.pronargs = 0
     LIMIT 1;
    IF v_func_oid IS NULL THEN
        v_falta := v_falta || 'funcao de bloqueio ausente; ';
    ELSIF coalesce(obj_description(v_func_oid, 'pg_proc'), '') <> v_comment THEN
        v_falta := v_falta || 'funcao de bloqueio sem comentario de propriedade 036; ';
    END IF;

    -- Trigger por catalogos PostgreSQL, sem depender de pg_get_triggerdef.
    SELECT tg.oid INTO v_trigger_oid
      FROM pg_trigger tg
      JOIN pg_class tbl ON tbl.oid = tg.tgrelid
      JOIN pg_namespace nst ON nst.oid = tbl.relnamespace
      JOIN pg_proc p ON p.oid = tg.tgfoid
      JOIN pg_namespace nsp ON nsp.oid = p.pronamespace
      JOIN pg_attribute a ON a.attrelid = tbl.oid AND a.attname = 'situacao_etiqueta' AND a.attnum > 0 AND NOT a.attisdropped
     WHERE nst.nspname = 'homologacao'
       AND tbl.relname = 'etiqueta'
       AND tg.tgname = 'trg_bloqueia_inativar_etiqueta_com_produto_ativo'
       AND tg.tgisinternal = false
       AND tg.tgenabled <> 'D'
       AND (tg.tgtype & 2) = 2      -- BEFORE
       AND (tg.tgtype & 16) = 16    -- UPDATE
       AND (tg.tgtype & 1) = 1      -- FOR EACH ROW
       AND a.attnum = ANY(tg.tgattr)
       AND p.proname = 'fn_bloqueia_inativar_etiqueta_com_produto_ativo'
       AND nsp.nspname = 'homologacao'
     LIMIT 1;
    IF v_trigger_oid IS NULL THEN
        v_falta := v_falta || 'trigger de bloqueio ausente ou com definicao catalografica divergente; ';
    ELSIF coalesce(obj_description(v_trigger_oid, 'pg_trigger'), '') <> v_comment THEN
        v_falta := v_falta || 'trigger de bloqueio sem comentario de propriedade 036; ';
    END IF;

    -- Constraints novas com validacao semantica tolerante.
    SELECT pg_get_constraintdef(c.oid, true) INTO v_def
      FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
     WHERE ns.nspname='homologacao' AND t.relname='etiqueta' AND c.conname='ck_etiqueta_codigo_tamanho' AND c.contype='c'
       AND obj_description(c.oid, 'pg_constraint') = v_comment;
    IF v_def IS NULL OR v_def !~* 'codigo_interno' OR v_def !~* '(char_length|length)' OR v_def !~* '(trim|btrim)' OR v_def !~* '1' OR v_def !~* '80' THEN
        v_falta := v_falta || 'ck_etiqueta_codigo_tamanho ausente ou semanticamente divergente; ';
    END IF;

    SELECT pg_get_constraintdef(c.oid, true) INTO v_def
      FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
     WHERE ns.nspname='homologacao' AND t.relname='etiqueta' AND c.conname='ck_etiqueta_nome_tamanho' AND c.contype='c'
       AND obj_description(c.oid, 'pg_constraint') = v_comment;
    IF v_def IS NULL OR v_def !~* 'nome_etiqueta' OR v_def !~* '(char_length|length)' OR v_def !~* '(trim|btrim)' OR v_def !~* '2' OR v_def !~* '80' THEN
        v_falta := v_falta || 'ck_etiqueta_nome_tamanho ausente ou semanticamente divergente; ';
    END IF;

    SELECT pg_get_constraintdef(c.oid, true) INTO v_def
      FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
     WHERE ns.nspname='homologacao' AND t.relname='etiqueta' AND c.conname='ck_etiqueta_descricao_tamanho' AND c.contype='c'
       AND obj_description(c.oid, 'pg_constraint') = v_comment;
    IF v_def IS NULL OR v_def !~* 'descricao_etiqueta' OR v_def !~* '255' OR v_def !~* 'IS NULL' THEN
        v_falta := v_falta || 'ck_etiqueta_descricao_tamanho ausente ou semanticamente divergente; ';
    END IF;

    -- Objetos preexistentes preservados.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace WHERE ns.nspname='homologacao' AND t.relname='etiqueta' AND c.conname='ck_etiqueta_codigo_nao_vazio') THEN
        v_falta := v_falta || 'ck_etiqueta_codigo_nao_vazio ausente; ';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace WHERE ns.nspname='homologacao' AND t.relname='etiqueta' AND c.conname='ck_etiqueta_tipo') THEN
        v_falta := v_falta || 'ck_etiqueta_tipo ausente; ';
    END IF;

    -- Dados devem permanecer consistentes.
    IF EXISTS (SELECT 1 FROM (SELECT upper(trim(codigo_interno)) FROM homologacao.etiqueta GROUP BY upper(trim(codigo_interno)) HAVING COUNT(*) > 1) d) THEN
        v_falta := v_falta || 'existem duplicidades globais de codigo interno; ';
    END IF;
    IF EXISTS (SELECT 1 FROM homologacao.etiqueta WHERE codigo_interno IS NULL OR trim(codigo_interno) = '' OR char_length(trim(codigo_interno)) > 80) THEN
        v_falta := v_falta || 'existem codigos internos invalidos; ';
    END IF;
    IF EXISTS (SELECT 1 FROM homologacao.etiqueta WHERE nome_etiqueta IS NULL OR char_length(trim(nome_etiqueta)) < 2 OR char_length(trim(nome_etiqueta)) > 80) THEN
        v_falta := v_falta || 'existem nomes de etiqueta invalidos; ';
    END IF;
    IF EXISTS (SELECT 1 FROM homologacao.etiqueta WHERE descricao_etiqueta IS NOT NULL AND char_length(trim(descricao_etiqueta)) > 255) THEN
        v_falta := v_falta || 'existem descricoes de etiqueta invalidas; ';
    END IF;
    IF EXISTS (SELECT 1 FROM homologacao.etiqueta WHERE tipo_etiqueta IS NULL OR tipo_etiqueta NOT IN ('CAIXA','PALETE','HU','INTERNA','OUTRA')) THEN
        v_falta := v_falta || 'existem tipos de etiqueta invalidos; ';
    END IF;
    IF EXISTS (SELECT 1 FROM homologacao.etiqueta e JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta=e.codigo_modelo_etiqueta WHERE e.situacao_etiqueta=true AND m.situacao_modelo_etiqueta=false) THEN
        v_falta := v_falta || 'existem etiquetas ativas com modelo inativo; ';
    END IF;
    IF EXISTS (SELECT 1 FROM homologacao.produto_etiqueta pe JOIN homologacao.etiqueta e ON e.codigo_etiqueta=pe.codigo_etiqueta WHERE pe.situacao_produto_etiqueta=true AND e.situacao_etiqueta=false) THEN
        v_falta := v_falta || 'existem produtos ativos vinculados a etiqueta inativa; ';
    END IF;

    IF v_falta <> '' THEN
        RAISE EXCEPTION 'VALIDACAO 036 PARTE A FALHOU (homologacao): %', v_falta;
    END IF;
    RAISE NOTICE 'OK - Parte A validada por catalogos PostgreSQL e consistencia de dados';

    -- Massa transacional. Tudo sera revertido pelo ROLLBACK do script.
    INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta)
    VALUES (v_prefixo || 'MODELO', 936001, '^XA^XZ', true)
    RETURNING codigo_modelo_etiqueta INTO v_modelo;

    INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, descricao_etiqueta, tipo_etiqueta, situacao_etiqueta)
    VALUES (v_modelo, v_prefixo || 'CODIGO_BASE', v_prefixo || 'Etiqueta Base', 'Descricao valida', 'CAIXA', true)
    RETURNING codigo_etiqueta INTO v_etiqueta_base;

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, lower(v_prefixo || 'CODIGO_BASE'), v_prefixo || 'Duplicada', 'CAIXA');
    EXCEPTION WHEN unique_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'Falha: duplicidade global nao foi bloqueada.';
    END IF;
    RAISE NOTICE 'OK: duplicidade global bloqueada.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, '  ' || lower(v_prefixo || 'CODIGO_BASE') || '  ', v_prefixo || 'Duplicada Espaco', 'CAIXA');
    EXCEPTION WHEN unique_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'Falha: variacao de maiusculas/minusculas e espacos externos nao foi bloqueada.';
    END IF;
    RAISE NOTICE 'OK: variacao de maiusculas/minusculas e espacos externos bloqueada.';

    INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
    VALUES (v_modelo, v_prefixo || 'CODIGO_DIFERENTE', v_prefixo || 'Etiqueta Livre', 'OUTRA')
    RETURNING codigo_etiqueta INTO v_etiqueta_livre;
    RAISE NOTICE 'OK: codigo interno diferente permitido.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, '   ', v_prefixo || 'Codigo Vazio', 'CAIXA');
    EXCEPTION WHEN check_violation OR not_null_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN RAISE EXCEPTION 'Falha: codigo vazio nao foi bloqueado.'; END IF;
    RAISE NOTICE 'OK: codigo vazio bloqueado.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, repeat('A', 81), v_prefixo || 'Codigo Longo', 'CAIXA');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN RAISE EXCEPTION 'Falha: codigo acima de 80 nao foi bloqueado.'; END IF;
    RAISE NOTICE 'OK: codigo acima de 80 bloqueado.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, v_prefixo || 'NOME_CURTO', 'A', 'CAIXA');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN RAISE EXCEPTION 'Falha: nome com menos de 2 nao foi bloqueado.'; END IF;
    RAISE NOTICE 'OK: nome com menos de 2 bloqueado.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, v_prefixo || 'NOME_LONGO', repeat('N', 81), 'CAIXA');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN RAISE EXCEPTION 'Falha: nome acima de 80 nao foi bloqueado.'; END IF;
    RAISE NOTICE 'OK: nome acima de 80 bloqueado.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, descricao_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, v_prefixo || 'DESC_LONGA', v_prefixo || 'Descricao Longa', repeat('D', 256), 'CAIXA');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN RAISE EXCEPTION 'Falha: descricao acima de 255 nao foi bloqueada.'; END IF;
    RAISE NOTICE 'OK: descricao acima de 255 bloqueada.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta)
        VALUES (v_modelo, v_prefixo || 'TIPO_INVALIDO', v_prefixo || 'Tipo Invalido', 'INVALIDO');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN RAISE EXCEPTION 'Falha: tipo invalido nao foi bloqueado por ck_etiqueta_tipo.'; END IF;
    RAISE NOTICE 'OK: tipo invalido bloqueado por ck_etiqueta_tipo.';

    UPDATE homologacao.etiqueta SET situacao_etiqueta = false WHERE codigo_etiqueta = v_etiqueta_livre;
    RAISE NOTICE 'OK: etiqueta sem produto ativo pode ser inativada.';

    UPDATE homologacao.etiqueta SET situacao_etiqueta = true WHERE codigo_etiqueta = v_etiqueta_livre;
    RAISE NOTICE 'OK: reativacao false -> true permitida.';

    INSERT INTO homologacao.produto_referencia (codigo_produto, descricao_produto_referencia, origem, situacao_produto_referencia)
    VALUES (v_prefixo || 'PRODUTO', 'Produto transacional 036', 'LOCAL', true)
    RETURNING codigo_produto_referencia INTO v_produto;

    INSERT INTO homologacao.produto_etiqueta (codigo_produto_referencia, codigo_etiqueta, tipo_etiqueta, principal, situacao_produto_etiqueta)
    VALUES (v_produto, v_etiqueta_livre, 'CAIXA', false, false);

    UPDATE homologacao.etiqueta SET situacao_etiqueta = false WHERE codigo_etiqueta = v_etiqueta_livre;
    RAISE NOTICE 'OK: produto inativo nao bloqueia a inativacao da etiqueta.';

    UPDATE homologacao.etiqueta SET situacao_etiqueta = true WHERE codigo_etiqueta = v_etiqueta_livre;

    INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta, situacao_etiqueta)
    VALUES (v_modelo, v_prefixo || 'COM_VINCULO_ATIVO', v_prefixo || 'Vinculo Ativo', 'CAIXA', true)
    RETURNING codigo_etiqueta INTO v_etiqueta_com_vinculo;

    INSERT INTO homologacao.produto_etiqueta (codigo_produto_referencia, codigo_etiqueta, tipo_etiqueta, principal, situacao_produto_etiqueta)
    VALUES (v_produto, v_etiqueta_com_vinculo, 'CAIXA', false, true);

    v_bloqueado := false;
    BEGIN
        UPDATE homologacao.etiqueta SET situacao_etiqueta = false WHERE codigo_etiqueta = v_etiqueta_com_vinculo;
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM ILIKE '%produtos ativos%' THEN
            v_bloqueado := true;
        ELSE
            RAISE;
        END IF;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'Falha: etiqueta com produto_etiqueta ativo nao foi bloqueada.';
    END IF;
    RAISE NOTICE 'OK: produto ativo bloqueia a inativacao da etiqueta.';

    UPDATE homologacao.etiqueta SET descricao_etiqueta = 'Atualizacao sem alterar situacao' WHERE codigo_etiqueta = v_etiqueta_base;
    RAISE NOTICE 'OK: atualizacao sem alterar situacao_etiqueta permitida.';

    RAISE NOTICE 'OK - Parte B validada com massa transacional prefixo=%', v_prefixo;
END $$;

-- Diagnosticos opcionais; nao sao criterio unico de validacao.
SELECT pg_get_indexdef('homologacao.uq_etiqueta_codigo_interno_global'::regclass) AS diagnostico_indice_global;
SELECT pg_get_triggerdef(tg.oid) AS diagnostico_trigger
  FROM pg_trigger tg
  JOIN pg_class tbl ON tbl.oid = tg.tgrelid
  JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
 WHERE ns.nspname = 'homologacao'
   AND tbl.relname = 'etiqueta'
   AND tg.tgname = 'trg_bloqueia_inativar_etiqueta_com_produto_ativo';

ROLLBACK;

-- Confirmacao pos-rollback: nenhum registro transacional TST036 deve permanecer gravado.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM homologacao.modelo_etiqueta WHERE nome_modelo_etiqueta LIKE 'TST036_%')
       OR EXISTS (SELECT 1 FROM homologacao.etiqueta WHERE codigo_interno LIKE 'TST036_%' OR nome_etiqueta LIKE 'TST036_%')
       OR EXISTS (SELECT 1 FROM homologacao.produto_referencia WHERE codigo_produto LIKE 'TST036_%') THEN
        RAISE EXCEPTION 'Falha: existem residuos transacionais TST036 apos ROLLBACK.';
    END IF;

    RAISE NOTICE 'OK: residuos zero apos ROLLBACK.';
    RAISE NOTICE 'OK - regras de banco do Cadastro de Etiqueta validadas (HML)';
END $$;
