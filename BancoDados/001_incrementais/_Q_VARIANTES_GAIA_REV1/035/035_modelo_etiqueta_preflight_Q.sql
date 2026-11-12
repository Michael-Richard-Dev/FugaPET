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
-- 035_modelo_etiqueta_preflight_HML.sql
-- Projeto FugaPET_HML  |  Schema: homologacao
-- Objeto: Cadastro de Modelo de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 035
-- Finalidade: preflight
--
-- EXECUCAO CONTROLADA.
-- Nao executar automaticamente.
-- Nao altera dados reais automaticamente.
-- Nao contem PRD.
-- ============================================================
\set ON_ERROR_STOP on
SET search_path TO homologacao;


-- 1) Estrutura minima obrigatoria.
DO $$
DECLARE
    v_falta text := '';
BEGIN
    IF to_regclass('homologacao.modelo_etiqueta') IS NULL THEN
        v_falta := v_falta || 'tabela homologacao.modelo_etiqueta ausente; ';
    END IF;

    IF to_regclass('homologacao.etiqueta') IS NULL THEN
        v_falta := v_falta || 'tabela homologacao.etiqueta ausente; ';
    END IF;

    IF v_falta <> '' THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: %', v_falta;
    END IF;

    SELECT string_agg(x.column_name, ', ' ORDER BY x.column_name)
      INTO v_falta
      FROM (VALUES ('codigo_modelo_etiqueta'), ('nome_modelo_etiqueta'), ('versao'), ('situacao_modelo_etiqueta'), ('observacao'), ('dpi'), ('largura_mm'), ('altura_mm'), ('conteudo_zpl')) AS x(column_name)
     WHERE NOT EXISTS (
        SELECT 1 FROM information_schema.columns c
         WHERE c.table_schema = 'homologacao'
           AND c.table_name = 'modelo_etiqueta'
           AND c.column_name = x.column_name
     );
    IF v_falta IS NOT NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: colunas ausentes em homologacao.modelo_etiqueta: %', v_falta;
    END IF;

    SELECT string_agg(x.column_name, ', ' ORDER BY x.column_name)
      INTO v_falta
      FROM (VALUES ('codigo_modelo_etiqueta'), ('situacao_etiqueta'), ('codigo_interno'), ('nome_etiqueta'), ('tipo_etiqueta'), ('descricao_etiqueta')) AS x(column_name)
     WHERE NOT EXISTS (
        SELECT 1 FROM information_schema.columns c
         WHERE c.table_schema = 'homologacao'
           AND c.table_name = 'etiqueta'
           AND c.column_name = x.column_name
     );
    IF v_falta IS NOT NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: colunas ausentes em homologacao.etiqueta: %', v_falta;
    END IF;
END $$;

-- 2) Diagnosticos detalhados de dados. Se houver linhas nestas consultas, o DO final abortara.
SELECT 'DUPLICIDADE_GLOBAL_NOME_VERSAO' AS diagnostico,
       upper(trim(nome_modelo_etiqueta)) AS nome_normalizado,
       versao,
       count(*) AS quantidade,
       array_agg(codigo_modelo_etiqueta ORDER BY codigo_modelo_etiqueta) AS codigos
  FROM homologacao.modelo_etiqueta
 GROUP BY upper(trim(nome_modelo_etiqueta)), versao
HAVING count(*) > 1;

SELECT 'NOME_FORA_LIMITE_2_80' AS diagnostico,
       codigo_modelo_etiqueta, nome_modelo_etiqueta, versao,
       char_length(trim(coalesce(nome_modelo_etiqueta, ''))) AS tamanho
  FROM homologacao.modelo_etiqueta
 WHERE nome_modelo_etiqueta IS NULL
    OR char_length(trim(nome_modelo_etiqueta)) NOT BETWEEN 2 AND 80
 ORDER BY codigo_modelo_etiqueta;

SELECT 'OBSERVACAO_ACIMA_255' AS diagnostico,
       codigo_modelo_etiqueta, nome_modelo_etiqueta, versao,
       char_length(trim(observacao)) AS tamanho
  FROM homologacao.modelo_etiqueta
 WHERE observacao IS NOT NULL
   AND char_length(trim(observacao)) > 255
 ORDER BY codigo_modelo_etiqueta;

SELECT 'DPI_INVALIDO' AS diagnostico,
       codigo_modelo_etiqueta, nome_modelo_etiqueta, versao, dpi
  FROM homologacao.modelo_etiqueta
 WHERE dpi IS NOT NULL AND dpi <= 0
 ORDER BY codigo_modelo_etiqueta;

SELECT 'LARGURA_INVALIDA' AS diagnostico,
       codigo_modelo_etiqueta, nome_modelo_etiqueta, versao, largura_mm
  FROM homologacao.modelo_etiqueta
 WHERE largura_mm IS NOT NULL AND largura_mm <= 0
 ORDER BY codigo_modelo_etiqueta;

SELECT 'ALTURA_INVALIDA' AS diagnostico,
       codigo_modelo_etiqueta, nome_modelo_etiqueta, versao, altura_mm
  FROM homologacao.modelo_etiqueta
 WHERE altura_mm IS NOT NULL AND altura_mm <= 0
 ORDER BY codigo_modelo_etiqueta;

SELECT 'ETIQUETA_ATIVA_EM_MODELO_INATIVO' AS diagnostico,
       m.codigo_modelo_etiqueta,
       m.nome_modelo_etiqueta,
       m.versao,
       e.codigo_etiqueta,
       e.nome_etiqueta,
       e.situacao_etiqueta
  FROM homologacao.etiqueta e
  JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta = e.codigo_modelo_etiqueta
 WHERE e.situacao_etiqueta = true
   AND m.situacao_modelo_etiqueta = false
 ORDER BY m.codigo_modelo_etiqueta, e.codigo_etiqueta;

-- 3) Diagnostico textual de objetos, seguro antes/depois da aplicacao.

-- Diagnostico textual seguro de funcao e trigger. Nao converter funcao/trigger para regclass.
SELECT
    'FUNCAO' AS tipo,
    'fn_bloqueia_inativar_modelo_com_etiqueta_ativa' AS objeto,
    CASE
        WHEN to_regprocedure('homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()') IS NOT NULL
        THEN 'PRESENTE'
        ELSE 'AUSENTE'
    END AS situacao,
    COALESCE((
        SELECT obj_description(p.oid, 'pg_proc')
          FROM pg_proc p
          JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'homologacao'
           AND p.proname = 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND p.pronargs = 0
         LIMIT 1
    ), '') AS comentario
UNION ALL
SELECT
    'TRIGGER',
    'trg_bloqueia_inativar_modelo_com_etiqueta_ativa',
    CASE
        WHEN EXISTS (
            SELECT 1
              FROM pg_trigger t
             WHERE t.tgname = 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
               AND t.tgrelid = to_regclass('homologacao.modelo_etiqueta')
               AND NOT t.tgisinternal
        )
        THEN 'PRESENTE'
        ELSE 'AUSENTE'
    END,
    COALESCE((
        SELECT obj_description(t.oid, 'pg_trigger')
          FROM pg_trigger t
         WHERE t.tgname = 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND t.tgrelid = to_regclass('homologacao.modelo_etiqueta')
           AND NOT t.tgisinternal
         LIMIT 1
    ), '');

SELECT
    'INDICE' AS tipo,
    c.relname AS objeto,
    CASE WHEN c.oid IS NULL THEN 'AUSENTE' ELSE 'PRESENTE' END AS situacao,
    COALESCE(obj_description(c.oid, 'pg_class'), '') AS comentario,
    COALESCE(pg_get_indexdef(c.oid), '') AS definicao
FROM (VALUES
    ('uq_modelo_etiqueta_nome_versao_global'),
    ('uq_modelo_etiqueta_nome_versao')
) v(nome)
LEFT JOIN pg_class c
       ON c.relname = v.nome
      AND c.relnamespace = 'homologacao'::regnamespace
ORDER BY v.nome;

SELECT
    'CONSTRAINT' AS tipo,
    v.nome AS objeto,
    CASE WHEN c.oid IS NULL THEN 'AUSENTE' ELSE 'PRESENTE' END AS situacao,
    COALESCE(obj_description(c.oid, 'pg_constraint'), '') AS comentario,
    COALESCE(pg_get_constraintdef(c.oid, true), '') AS definicao
FROM (VALUES
    ('ck_modelo_etiqueta_nome_tamanho'),
    ('ck_modelo_etiqueta_observacao_tamanho'),
    ('ck_modelo_etiqueta_dpi_positivo'),
    ('ck_modelo_etiqueta_largura_positiva'),
    ('ck_modelo_etiqueta_altura_positiva'),
    ('ck_modelo_etiqueta_versao'),
    ('ck_modelo_etiqueta_zpl_nao_vazio'),
    ('ck_modelo_etiqueta_nome_nao_vazio')
) v(nome)
LEFT JOIN pg_constraint c
       ON c.conname = v.nome
      AND c.conrelid = to_regclass('homologacao.modelo_etiqueta')
ORDER BY v.nome;


-- 4) Bloqueios finais do preflight.
DO $$
DECLARE
    v_dups int; v_nome int; v_obs int; v_dpi int; v_larg int; v_alt int; v_orf int;
    v_falta text := '';
    v_unowned text;
    v_marked_count int := 0;
    v_pkg_count int := 0;
    v_expected_count int := 8; -- indice global + funcao + trigger + 5 constraints
    v_old_partial_exists boolean;
BEGIN
    SELECT count(*) INTO v_dups FROM (
        SELECT 1 FROM homologacao.modelo_etiqueta
         GROUP BY upper(trim(nome_modelo_etiqueta)), versao HAVING count(*) > 1
    ) d;
    SELECT count(*) INTO v_nome FROM homologacao.modelo_etiqueta
     WHERE nome_modelo_etiqueta IS NULL OR char_length(trim(nome_modelo_etiqueta)) NOT BETWEEN 2 AND 80;
    SELECT count(*) INTO v_obs FROM homologacao.modelo_etiqueta
     WHERE observacao IS NOT NULL AND char_length(trim(observacao)) > 255;
    SELECT count(*) INTO v_dpi FROM homologacao.modelo_etiqueta WHERE dpi IS NOT NULL AND dpi <= 0;
    SELECT count(*) INTO v_larg FROM homologacao.modelo_etiqueta WHERE largura_mm IS NOT NULL AND largura_mm <= 0;
    SELECT count(*) INTO v_alt FROM homologacao.modelo_etiqueta WHERE altura_mm IS NOT NULL AND altura_mm <= 0;
    SELECT count(*) INTO v_orf FROM homologacao.etiqueta e
      JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta = e.codigo_modelo_etiqueta
     WHERE e.situacao_etiqueta = true AND m.situacao_modelo_etiqueta = false;

    IF (v_dups + v_nome + v_obs + v_dpi + v_larg + v_alt + v_orf) > 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: inconsistencias encontradas (dup=% nome=% obs=% dpi=% largura=% altura=% etiqueta_ativa_modelo_inativo=%). Consulte os SELECTs acima.',
            v_dups, v_nome, v_obs, v_dpi, v_larg, v_alt, v_orf;
    END IF;

    -- Checks antigos que devem permanecer. Nao cria nem altera; apenas exige os dois checks historicamente esperados.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_versao' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_versao ausente; ';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_zpl_nao_vazio' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        v_falta := v_falta || 'ck_modelo_etiqueta_zpl_nao_vazio ausente; ';
    END IF;

    -- Objetos finais com mesmo nome, mas sem comentario de propriedade do pacote, sao estado incompativel.
    WITH objetos AS (
        SELECT 'INDICE' AS tipo, 'uq_modelo_etiqueta_nome_versao_global' AS nome,
               obj_description(to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global'), 'pg_class') AS comentario,
               to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global') IS NOT NULL AS presente
        UNION ALL
        SELECT 'FUNCAO', 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa',
               (SELECT obj_description(p.oid, 'pg_proc') FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
                 WHERE n.nspname='homologacao' AND p.proname='fn_bloqueia_inativar_modelo_com_etiqueta_ativa' AND p.pronargs=0 LIMIT 1),
               to_regprocedure('homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()') IS NOT NULL
        UNION ALL
        SELECT 'TRIGGER', 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa',
               (SELECT obj_description(t.oid, 'pg_trigger') FROM pg_trigger t
                 WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
                   AND t.tgrelid='homologacao.modelo_etiqueta'::regclass AND NOT t.tgisinternal LIMIT 1),
               EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
                         AND t.tgrelid='homologacao.modelo_etiqueta'::regclass AND NOT t.tgisinternal)
        UNION ALL
        SELECT 'CONSTRAINT', c.conname, obj_description(c.oid, 'pg_constraint'), true
          FROM pg_constraint c
         WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass
           AND c.conname IN ('ck_modelo_etiqueta_nome_tamanho', 'ck_modelo_etiqueta_observacao_tamanho', 'ck_modelo_etiqueta_dpi_positivo', 'ck_modelo_etiqueta_largura_positiva', 'ck_modelo_etiqueta_altura_positiva')
    )
    SELECT string_agg(tipo || ':' || nome, ', ' ORDER BY tipo, nome)
      INTO v_unowned
      FROM objetos
     WHERE presente
       AND coalesce(comentario, '') NOT LIKE '%FugaPET incremental 035 modelo_etiqueta%';

    IF v_unowned IS NOT NULL THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: objetos finais ja existem sem propriedade do pacote 035: %. Nao e seguro sobrescrever/remover.', v_unowned;
    END IF;

    -- Diferenciar estado nao aplicado, aplicado completo e parcial.
    SELECT count(*) INTO v_pkg_count FROM (
        SELECT 1 WHERE to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global') IS NOT NULL
        UNION ALL SELECT 1 WHERE to_regprocedure('homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()') IS NOT NULL
        UNION ALL SELECT 1 WHERE EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa' AND t.tgrelid='homologacao.modelo_etiqueta'::regclass AND NOT t.tgisinternal)
        UNION ALL SELECT 1 FROM pg_constraint c WHERE c.conrelid='homologacao.modelo_etiqueta'::regclass AND c.conname IN ('ck_modelo_etiqueta_nome_tamanho', 'ck_modelo_etiqueta_observacao_tamanho', 'ck_modelo_etiqueta_dpi_positivo', 'ck_modelo_etiqueta_largura_positiva', 'ck_modelo_etiqueta_altura_positiva')
    ) s;

    v_old_partial_exists := to_regclass('homologacao.uq_modelo_etiqueta_nome_versao') IS NOT NULL;

    IF v_pkg_count > 0 AND v_pkg_count < v_expected_count THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: estado parcial detectado (% de % objetos do pacote). Revisar/rollback antes de prosseguir.', v_pkg_count, v_expected_count;
    END IF;

    IF v_pkg_count = 0 AND NOT v_old_partial_exists THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: pacote nao aplicado, mas indice parcial antigo uq_modelo_etiqueta_nome_versao tambem nao existe. Estado inesperado.';
    END IF;

    IF v_falta <> '' THEN
        RAISE EXCEPTION 'PREFLIGHT 035 abortado: %', v_falta;
    END IF;

    RAISE NOTICE 'PREFLIGHT 035 OK (homologacao). Estado: %.', CASE WHEN v_pkg_count = v_expected_count THEN 'PACOTE_COMPLETAMENTE_APLICADO' ELSE 'PACOTE_NAO_APLICADO' END;
END $$;

SELECT 'OK - PREFLIGHT 035 HML concluido sem inconsistencias bloqueantes' AS resultado;
