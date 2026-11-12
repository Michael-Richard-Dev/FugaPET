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
-- 035_modelo_etiqueta_validar_HML.sql
-- Projeto FugaPET_HML  |  Schema: homologacao
-- Objeto: Cadastro de Modelo de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 035
-- Finalidade: validacao
--
-- EXECUCAO CONTROLADA.
-- Nao executar automaticamente.
-- Nao altera dados reais automaticamente.
-- Nao contem PRD.
-- ============================================================
\set ON_ERROR_STOP on
SET search_path TO homologacao;


-- =========================================================
-- PARTE A — validacao estatica dos objetos e dos dados.
-- =========================================================
DO $$
DECLARE
    v_falta text := '';
    v_def text;
    v_expr text;
    v_pred text;
    v_norm text;
    v_idx_oid oid;
    v_idx_basico_ok boolean;
    v_idx_versao_ok boolean;
    v_func_oid oid;
    v_trigger_oid oid;
    v_trigger_def text;
    v_trigger_catalogo_ok boolean;
    v_trigger_coluna_ok boolean;
    v_dups int; v_nome int; v_obs int; v_dpi int; v_larg int; v_alt int; v_orf int;
BEGIN
    -- Indice global: validacao por catalogo + expressao tolerante a formatacao.
    SELECT i.indexrelid,
           i.indisunique = true
           AND i.indisvalid = true
           AND i.indpred IS NULL
           AND i.indnkeyatts = 2,
           pg_get_expr(i.indexprs, i.indrelid, true),
           pg_get_expr(i.indpred, i.indrelid, true)
      INTO v_idx_oid, v_idx_basico_ok, v_expr, v_pred
      FROM pg_index i
     WHERE i.indexrelid = to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global')
       AND i.indrelid = 'homologacao.modelo_etiqueta'::regclass;

    IF v_idx_oid IS NULL OR NOT coalesce(v_idx_basico_ok, false) THEN
        v_falta := v_falta || 'indice global ausente/nao unique/invalido/com WHERE/tabela incorreta; ';
    ELSE
        SELECT EXISTS (
            SELECT 1
              FROM pg_index i
              JOIN pg_attribute a ON a.attrelid = i.indrelid
             WHERE i.indexrelid = v_idx_oid
               AND a.attname = 'versao'
               AND (' ' || i.indkey::text || ' ') LIKE ('% ' || a.attnum::text || ' %')
        ) INTO v_idx_versao_ok;

        v_norm := regexp_replace(regexp_replace(lower(coalesce(v_expr, '')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');

        IF coalesce(v_pred, '') <> '' THEN
            v_falta := v_falta || 'indice global possui predicado WHERE, deveria ser global; ';
        END IF;

        IF v_norm NOT LIKE '%upper%'
           OR v_norm NOT LIKE '%trim%'
           OR v_norm NOT LIKE '%nome_modelo_etiqueta%'
           OR NOT coalesce(v_idx_versao_ok, false) THEN
            v_falta := v_falta || 'indice global com composicao divergente para upper(trim(nome_modelo_etiqueta)) + versao; ';
        END IF;

        IF coalesce(obj_description(v_idx_oid, 'pg_class'), '') NOT LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            v_falta := v_falta || 'indice global sem comentario de propriedade do pacote 035; ';
        END IF;
    END IF;

    IF to_regclass('homologacao.uq_modelo_etiqueta_nome_versao') IS NOT NULL THEN
        v_falta := v_falta || 'indice parcial antigo ainda existe; ';
    END IF;

    -- Funcao: validacao por catalogo. A semantica e comprovada nos testes transacionais da Parte B.
    SELECT p.oid INTO v_func_oid
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'homologacao'
       AND p.proname = 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa'
       AND p.pronargs = 0
     LIMIT 1;

    IF v_func_oid IS NULL THEN
        v_falta := v_falta || 'funcao ausente; ';
    ELSE
        IF coalesce(obj_description(v_func_oid, 'pg_proc'), '') NOT LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            v_falta := v_falta || 'funcao sem comentario de propriedade do pacote 035; ';
        END IF;
    END IF;

    -- Trigger: validacao por catalogos, sem depender do texto retornado por pg_get_triggerdef.
    SELECT t.oid,
           pg_get_triggerdef(t.oid, true),
           nt.nspname = 'homologacao'
           AND c.relname = 'modelo_etiqueta'
           AND t.tgname = 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND NOT t.tgisinternal
           AND t.tgenabled <> 'D'
           AND (t.tgtype::int & 1) = 1      -- FOR EACH ROW
           AND (t.tgtype::int & 2) = 2      -- BEFORE
           AND (t.tgtype::int & 16) = 16    -- UPDATE
           AND (t.tgtype::int & 4) = 0      -- nao INSERT
           AND (t.tgtype::int & 8) = 0      -- nao DELETE
           AND (t.tgtype::int & 32) = 0     -- nao TRUNCATE
           AND (t.tgtype::int & 64) = 0     -- nao INSTEAD OF
           AND nf.nspname = 'homologacao'
           AND p.proname = 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND p.pronargs = 0
      INTO v_trigger_oid, v_trigger_def, v_trigger_catalogo_ok
      FROM pg_trigger t
      JOIN pg_class c ON c.oid = t.tgrelid
      JOIN pg_namespace nt ON nt.oid = c.relnamespace
      JOIN pg_proc p ON p.oid = t.tgfoid
      JOIN pg_namespace nf ON nf.oid = p.pronamespace
     WHERE nt.nspname = 'homologacao'
       AND c.relname = 'modelo_etiqueta'
       AND t.tgname = 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
     LIMIT 1;

    IF v_trigger_oid IS NULL THEN
        v_falta := v_falta || 'trigger ausente; ';
    ELSE
        SELECT EXISTS (
            SELECT 1
              FROM pg_trigger t
              JOIN pg_attribute a ON a.attrelid = t.tgrelid
             WHERE t.oid = v_trigger_oid
               AND a.attname = 'situacao_modelo_etiqueta'
               AND (' ' || t.tgattr::text || ' ') LIKE ('% ' || a.attnum::text || ' %')
        ) INTO v_trigger_coluna_ok;

        IF NOT coalesce(v_trigger_catalogo_ok, false)
           OR NOT coalesce(v_trigger_coluna_ok, false) THEN
            v_falta := v_falta || 'trigger com definicao catalografica divergente. Esperado: BEFORE UPDATE OF situacao_modelo_etiqueta, FOR EACH ROW, funcao homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa(). Definicao informativa: '
                       || coalesce(v_trigger_def, '<indisponivel>') || '; ';
        END IF;

        IF coalesce(obj_description(v_trigger_oid, 'pg_trigger'), '') NOT LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            v_falta := v_falta || 'trigger sem comentario de propriedade do pacote 035; ';
        END IF;
    END IF;

    -- Checks novos: validacao por catalogo/conbin com normalizacao tolerante e sem depender de formatacao exata.
    SELECT pg_get_expr(c.conbin, c.conrelid, true) INTO v_def
      FROM pg_constraint c
     WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass
       AND c.conname='ck_modelo_etiqueta_nome_tamanho'
       AND c.contype='c'
       AND c.convalidated;
    v_norm := regexp_replace(regexp_replace(lower(coalesce(v_def,'')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');
    IF v_def IS NULL
       OR v_norm NOT LIKE '%char_length%'
       OR v_norm NOT LIKE '%trim%'
       OR v_norm NOT LIKE '%nome_modelo_etiqueta%'
       OR v_norm NOT LIKE '%2%'
       OR v_norm NOT LIKE '%80%'
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass AND c.conname='ck_modelo_etiqueta_nome_tamanho' AND coalesce(obj_description(c.oid,'pg_constraint'),'') LIKE '%FugaPET incremental 035 modelo_etiqueta%') THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_nome_tamanho ausente/divergente/sem propriedade; ';
    END IF;

    SELECT pg_get_expr(c.conbin, c.conrelid, true) INTO v_def
      FROM pg_constraint c
     WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass
       AND c.conname='ck_modelo_etiqueta_observacao_tamanho'
       AND c.contype='c'
       AND c.convalidated;
    v_norm := regexp_replace(regexp_replace(lower(coalesce(v_def,'')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');
    IF v_def IS NULL
       OR v_norm NOT LIKE '%observacao%'
       OR v_norm NOT LIKE '%isnull%'
       OR v_norm NOT LIKE '%char_length%'
       OR v_norm NOT LIKE '%trim%'
       OR v_norm NOT LIKE '%255%'
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass AND c.conname='ck_modelo_etiqueta_observacao_tamanho' AND coalesce(obj_description(c.oid,'pg_constraint'),'') LIKE '%FugaPET incremental 035 modelo_etiqueta%') THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_observacao_tamanho ausente/divergente/sem propriedade; ';
    END IF;

    SELECT pg_get_expr(c.conbin, c.conrelid, true) INTO v_def
      FROM pg_constraint c
     WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass
       AND c.conname='ck_modelo_etiqueta_dpi_positivo'
       AND c.contype='c'
       AND c.convalidated;
    v_norm := regexp_replace(regexp_replace(lower(coalesce(v_def,'')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');
    IF v_def IS NULL
       OR v_norm NOT LIKE '%dpi%'
       OR v_norm NOT LIKE '%isnull%'
       OR v_norm NOT LIKE '%dpi>0%'
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass AND c.conname='ck_modelo_etiqueta_dpi_positivo' AND coalesce(obj_description(c.oid,'pg_constraint'),'') LIKE '%FugaPET incremental 035 modelo_etiqueta%') THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_dpi_positivo ausente/divergente/sem propriedade; ';
    END IF;

    SELECT pg_get_expr(c.conbin, c.conrelid, true) INTO v_def
      FROM pg_constraint c
     WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass
       AND c.conname='ck_modelo_etiqueta_largura_positiva'
       AND c.contype='c'
       AND c.convalidated;
    v_norm := regexp_replace(regexp_replace(lower(coalesce(v_def,'')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');
    IF v_def IS NULL
       OR v_norm NOT LIKE '%largura_mm%'
       OR v_norm NOT LIKE '%isnull%'
       OR v_norm NOT LIKE '%largura_mm>0%'
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass AND c.conname='ck_modelo_etiqueta_largura_positiva' AND coalesce(obj_description(c.oid,'pg_constraint'),'') LIKE '%FugaPET incremental 035 modelo_etiqueta%') THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_largura_positiva ausente/divergente/sem propriedade; ';
    END IF;

    SELECT pg_get_expr(c.conbin, c.conrelid, true) INTO v_def
      FROM pg_constraint c
     WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass
       AND c.conname='ck_modelo_etiqueta_altura_positiva'
       AND c.contype='c'
       AND c.convalidated;
    v_norm := regexp_replace(regexp_replace(lower(coalesce(v_def,'')), '[[:space:]\(\)]', '', 'g'), '::[a-z0-9_\.]+', '', 'g');
    IF v_def IS NULL
       OR v_norm NOT LIKE '%altura_mm%'
       OR v_norm NOT LIKE '%isnull%'
       OR v_norm NOT LIKE '%altura_mm>0%'
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass AND c.conname='ck_modelo_etiqueta_altura_positiva' AND coalesce(obj_description(c.oid,'pg_constraint'),'') LIKE '%FugaPET incremental 035 modelo_etiqueta%') THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_altura_positiva ausente/divergente/sem propriedade; ';
    END IF;

    -- Checks antigos preservados.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_versao' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_versao ausente; ';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_zpl_nao_vazio' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_zpl_nao_vazio ausente; ';
    END IF;

    -- Dados inconsistentes devem ser zero.
    SELECT count(*) INTO v_dups FROM (SELECT 1 FROM homologacao.modelo_etiqueta GROUP BY upper(trim(nome_modelo_etiqueta)), versao HAVING count(*) > 1) d;
    SELECT count(*) INTO v_nome FROM homologacao.modelo_etiqueta WHERE nome_modelo_etiqueta IS NULL OR char_length(trim(nome_modelo_etiqueta)) NOT BETWEEN 2 AND 80;
    SELECT count(*) INTO v_obs FROM homologacao.modelo_etiqueta WHERE observacao IS NOT NULL AND char_length(trim(observacao)) > 255;
    SELECT count(*) INTO v_dpi FROM homologacao.modelo_etiqueta WHERE dpi IS NOT NULL AND dpi <= 0;
    SELECT count(*) INTO v_larg FROM homologacao.modelo_etiqueta WHERE largura_mm IS NOT NULL AND largura_mm <= 0;
    SELECT count(*) INTO v_alt FROM homologacao.modelo_etiqueta WHERE altura_mm IS NOT NULL AND altura_mm <= 0;
    SELECT count(*) INTO v_orf FROM homologacao.etiqueta e JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta=e.codigo_modelo_etiqueta WHERE e.situacao_etiqueta=true AND m.situacao_modelo_etiqueta=false;
    IF (v_dups + v_nome + v_obs + v_dpi + v_larg + v_alt + v_orf) > 0 THEN
        v_falta := v_falta || format('dados inconsistentes: dup=%s nome=%s obs=%s dpi=%s largura=%s altura=%s etiqueta_ativa_modelo_inativo=%s; ', v_dups, v_nome, v_obs, v_dpi, v_larg, v_alt, v_orf);
    END IF;

    IF v_falta <> '' THEN
        RAISE EXCEPTION 'VALIDACAO 035 PARTE A FALHOU (homologacao): %', v_falta;
    END IF;

    RAISE NOTICE 'VALIDACAO 035 PARTE A OK (homologacao).';
END $$;

-- =========================================================
-- PARTE B — testes transacionais. Tudo e revertido por ROLLBACK.
-- =========================================================
BEGIN;
DO $$
DECLARE
    v_id bigint;
    v_id_inativa bigint;
    v_falhou boolean;
BEGIN
    -- Modelo ativo base para testes.
    INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta)
    VALUES ('___VALIDACAO_035___', 900001, '^XA^XZ', true)
    RETURNING codigo_modelo_etiqueta INTO v_id;

    -- Mesmo nome com versao diferente deve ser permitido.
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta)
        VALUES ('___VALIDACAO_035___', 900002, '^XA^XZ', true);
        RAISE NOTICE 'OK: mesmo nome com versao diferente permitido.';
    EXCEPTION WHEN others THEN
        RAISE EXCEPTION 'FALHA: mesmo nome com versao diferente foi bloqueado (%).', SQLERRM;
    END;

    -- Duplicidade nome+versao deve falhar.
    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta)
        VALUES ('___VALIDACAO_035___', 900001, '^XA^XZ', true);
    EXCEPTION WHEN unique_violation THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION 'FALHA: duplicidade nome+versao nao foi bloqueada.';
    END IF;
    RAISE NOTICE 'OK: duplicidade nome+versao bloqueada.';

    -- Checks.
    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta)
        VALUES ('A', 900003, '^XA^XZ', true);
    EXCEPTION WHEN check_violation THEN v_falhou := true; END;
    IF NOT v_falhou THEN RAISE EXCEPTION 'FALHA: nome invalido nao foi bloqueado.'; END IF;
    RAISE NOTICE 'OK: nome invalido bloqueado.';

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta, dpi)
        VALUES ('___VALIDACAO_035_DPI___', 900004, '^XA^XZ', true, 0);
    EXCEPTION WHEN check_violation THEN v_falhou := true; END;
    IF NOT v_falhou THEN RAISE EXCEPTION 'FALHA: dpi<=0 nao foi bloqueado.'; END IF;
    RAISE NOTICE 'OK: dpi invalido bloqueado.';

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta, observacao)
        VALUES ('___VALIDACAO_035_OBS___', 900005, '^XA^XZ', true, repeat('X', 256));
    EXCEPTION WHEN check_violation THEN v_falhou := true; END;
    IF NOT v_falhou THEN RAISE EXCEPTION 'FALHA: observacao >255 nao foi bloqueada.'; END IF;
    RAISE NOTICE 'OK: observacao acima de 255 bloqueada.';

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta, largura_mm)
        VALUES ('___VALIDACAO_035_LARG___', 900006, '^XA^XZ', true, 0);
    EXCEPTION WHEN check_violation THEN v_falhou := true; END;
    IF NOT v_falhou THEN RAISE EXCEPTION 'FALHA: largura_mm<=0 nao foi bloqueada.'; END IF;
    RAISE NOTICE 'OK: largura invalida bloqueada.';

    v_falhou := false;
    BEGIN
        INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta, altura_mm)
        VALUES ('___VALIDACAO_035_ALT___', 900007, '^XA^XZ', true, 0);
    EXCEPTION WHEN check_violation THEN v_falhou := true; END;
    IF NOT v_falhou THEN RAISE EXCEPTION 'FALHA: altura_mm<=0 nao foi bloqueada.'; END IF;
    RAISE NOTICE 'OK: altura invalida bloqueada.';

    -- Etiqueta ativa deve bloquear inativacao do modelo.
    INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta, descricao_etiqueta, situacao_etiqueta)
    VALUES (v_id, '___VAL035_ATIVA___', 'validacao ativa', 'OUTRA', 'validacao', true);

    v_falhou := false;
    BEGIN
        UPDATE homologacao.modelo_etiqueta SET situacao_modelo_etiqueta = false WHERE codigo_modelo_etiqueta = v_id;
    EXCEPTION WHEN others THEN
        v_falhou := true;
    END;
    IF NOT v_falhou THEN
        RAISE EXCEPTION 'FALHA: etiqueta ativa nao bloqueou a inativacao do modelo.';
    END IF;
    RAISE NOTICE 'OK: etiqueta ativa bloqueia a inativacao do modelo.';

    -- Etiqueta inativa NAO deve bloquear inativacao do modelo.
    INSERT INTO homologacao.modelo_etiqueta (nome_modelo_etiqueta, versao, conteudo_zpl, situacao_modelo_etiqueta)
    VALUES ('___VALIDACAO_035_INATIVA___', 900008, '^XA^XZ', true)
    RETURNING codigo_modelo_etiqueta INTO v_id_inativa;

    INSERT INTO homologacao.etiqueta (codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta, descricao_etiqueta, situacao_etiqueta)
    VALUES (v_id_inativa, '___VAL035_INATIVA___', 'validacao inativa', 'OUTRA', 'validacao', false);

    BEGIN
        UPDATE homologacao.modelo_etiqueta SET situacao_modelo_etiqueta = false WHERE codigo_modelo_etiqueta = v_id_inativa;
        IF NOT EXISTS (SELECT 1 FROM homologacao.modelo_etiqueta WHERE codigo_modelo_etiqueta = v_id_inativa AND situacao_modelo_etiqueta = false) THEN
            RAISE EXCEPTION 'modelo temporario nao ficou inativo apos UPDATE permitido';
        END IF;
        RAISE NOTICE 'OK: etiqueta inativa nao bloqueia a inativacao do modelo.';
    EXCEPTION WHEN others THEN
        RAISE EXCEPTION 'FALHA: etiqueta inativa bloqueou indevidamente a inativacao do modelo (%).', SQLERRM;
    END;

    RAISE NOTICE 'VALIDACAO 035 PARTE B OK (homologacao): testes transacionais passaram e serao revertidos.';
END $$;
ROLLBACK;

-- Confirmacao final: nenhum dado de teste ficou gravado.
SELECT 'modelo_etiqueta_dados_teste_restantes' AS verificacao, count(*) AS qtd
  FROM homologacao.modelo_etiqueta
 WHERE nome_modelo_etiqueta LIKE '\_\_\_VALIDACAO\_035%' ESCAPE '\';

SELECT 'etiqueta_dados_teste_restantes' AS verificacao, count(*) AS qtd
  FROM homologacao.etiqueta
 WHERE codigo_interno LIKE '\_\_\_VAL035%' ESCAPE '\';

SELECT 'OK - regras de banco do Cadastro de Modelo de Etiqueta validadas (HML)' AS resultado;
