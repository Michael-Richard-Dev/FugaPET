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
-- 036_etiqueta_preflight_HML.sql
-- Projeto FugaPET | Schema: homologacao
-- Objeto: Cadastro de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 036
-- Finalidade: preflight sem alteracao de dados
-- ============================================================

BEGIN;
SET LOCAL search_path TO homologacao, public;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- Diagnostico: duplicidade global por upper(trim(codigo_interno)).
SELECT
    'DUPLICIDADE_GLOBAL_CODIGO_INTERNO' AS diagnostico,
    upper(trim(codigo_interno)) AS codigo_normalizado,
    COUNT(*) AS quantidade,
    array_agg(codigo_etiqueta ORDER BY codigo_etiqueta) AS codigos_etiqueta
FROM homologacao.etiqueta
GROUP BY upper(trim(codigo_interno))
HAVING COUNT(*) > 1;

-- Diagnostico: codigo invalido.
SELECT
    'CODIGO_INTERNO_INVALIDO' AS diagnostico,
    codigo_etiqueta,
    codigo_interno,
    char_length(trim(coalesce(codigo_interno, ''))) AS tamanho_normalizado
FROM homologacao.etiqueta
WHERE codigo_interno IS NULL
   OR trim(codigo_interno) = ''
   OR char_length(trim(codigo_interno)) > 80
ORDER BY codigo_etiqueta;

-- Diagnostico: nome invalido.
SELECT
    'NOME_ETIQUETA_INVALIDO' AS diagnostico,
    codigo_etiqueta,
    nome_etiqueta,
    char_length(trim(coalesce(nome_etiqueta, ''))) AS tamanho_normalizado
FROM homologacao.etiqueta
WHERE nome_etiqueta IS NULL
   OR char_length(trim(nome_etiqueta)) < 2
   OR char_length(trim(nome_etiqueta)) > 80
ORDER BY codigo_etiqueta;

-- Diagnostico: descricao invalida.
SELECT
    'DESCRICAO_ETIQUETA_INVALIDA' AS diagnostico,
    codigo_etiqueta,
    char_length(trim(descricao_etiqueta)) AS tamanho_normalizado
FROM homologacao.etiqueta
WHERE descricao_etiqueta IS NOT NULL
  AND char_length(trim(descricao_etiqueta)) > 255
ORDER BY codigo_etiqueta;

-- Diagnostico: tipo invalido.
SELECT
    'TIPO_ETIQUETA_INVALIDO' AS diagnostico,
    codigo_etiqueta,
    tipo_etiqueta
FROM homologacao.etiqueta
WHERE tipo_etiqueta IS NULL
   OR tipo_etiqueta NOT IN ('CAIXA', 'PALETE', 'HU', 'INTERNA', 'OUTRA')
ORDER BY codigo_etiqueta;

-- Diagnostico: etiqueta ativa vinculada a modelo inativo.
SELECT
    'ETIQUETA_ATIVA_COM_MODELO_INATIVO' AS diagnostico,
    e.codigo_etiqueta,
    e.codigo_interno,
    e.codigo_modelo_etiqueta,
    m.situacao_modelo_etiqueta
FROM homologacao.etiqueta e
JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta = e.codigo_modelo_etiqueta
WHERE e.situacao_etiqueta = true
  AND m.situacao_modelo_etiqueta = false
ORDER BY e.codigo_etiqueta;

-- Diagnostico: produto ativo vinculado a etiqueta inativa.
SELECT
    'PRODUTO_ATIVO_COM_ETIQUETA_INATIVA' AS diagnostico,
    pe.codigo_produto_etiqueta,
    pe.codigo_produto_referencia,
    pe.codigo_etiqueta,
    pe.situacao_produto_etiqueta,
    e.situacao_etiqueta
FROM homologacao.produto_etiqueta pe
JOIN homologacao.etiqueta e ON e.codigo_etiqueta = pe.codigo_etiqueta
WHERE pe.situacao_produto_etiqueta = true
  AND e.situacao_etiqueta = false
ORDER BY pe.codigo_produto_etiqueta;

-- Diagnostico: objetos candidatos do pacote 036.
SELECT
    'OBJETO_036' AS diagnostico,
    tipo,
    objeto,
    situacao,
    comentario
FROM (
    SELECT 'CONSTRAINT' AS tipo, c.conname AS objeto, 'PRESENTE' AS situacao, obj_description(c.oid, 'pg_constraint') AS comentario
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace ns ON ns.oid = t.relnamespace
    WHERE ns.nspname = 'homologacao' AND t.relname = 'etiqueta'
      AND c.conname IN ('ck_etiqueta_codigo_tamanho','ck_etiqueta_nome_tamanho','ck_etiqueta_descricao_tamanho')
    UNION ALL
    SELECT 'INDEX', i.relname, 'PRESENTE', obj_description(i.oid, 'pg_class')
    FROM pg_class i
    JOIN pg_namespace ns ON ns.oid = i.relnamespace
    WHERE ns.nspname = 'homologacao' AND i.relkind = 'i'
      AND i.relname = 'uq_etiqueta_codigo_interno_global'
    UNION ALL
    SELECT 'FUNCTION', p.proname || '()', 'PRESENTE', obj_description(p.oid, 'pg_proc')
    FROM pg_proc p
    JOIN pg_namespace ns ON ns.oid = p.pronamespace
    WHERE ns.nspname = 'homologacao' AND p.proname = 'fn_bloqueia_inativar_etiqueta_com_produto_ativo' AND p.pronargs = 0
    UNION ALL
    SELECT 'TRIGGER', tg.tgname, 'PRESENTE', obj_description(tg.oid, 'pg_trigger')
    FROM pg_trigger tg
    JOIN pg_class t ON t.oid = tg.tgrelid
    JOIN pg_namespace ns ON ns.oid = t.relnamespace
    WHERE ns.nspname = 'homologacao' AND t.relname = 'etiqueta'
      AND tg.tgname = 'trg_bloqueia_inativar_etiqueta_com_produto_ativo'
      AND NOT tg.tgisinternal
) s
ORDER BY tipo, objeto;

DO $$
DECLARE
    v_comment constant text := 'FugaPET incremental 036 etiqueta';
    v_existentes integer := 0;
    v_comentados integer := 0;
    v_inconsistencias integer := 0;
    v_estado text;
    v_old_idx boolean;
    v_hist_idx boolean;
BEGIN
    -- Tabelas obrigatorias.
    IF to_regclass('homologacao.etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.etiqueta nao encontrada.';
    END IF;
    IF to_regclass('homologacao.modelo_etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.modelo_etiqueta nao encontrada.';
    END IF;
    IF to_regclass('homologacao.produto_referencia') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.produto_referencia nao encontrada.';
    END IF;
    IF to_regclass('homologacao.produto_etiqueta') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.produto_etiqueta nao encontrada.';
    END IF;

    -- Colunas obrigatorias e tipos esperados de forma tolerante.
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='codigo_etiqueta') THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.codigo_etiqueta nao encontrada.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='codigo_modelo_etiqueta') THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.codigo_modelo_etiqueta nao encontrada.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='codigo_interno' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.codigo_interno ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='nome_etiqueta' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.nome_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='descricao_etiqueta' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.descricao_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='tipo_etiqueta' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.tipo_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='etiqueta' AND column_name='situacao_etiqueta' AND data_type='boolean') THEN
        RAISE EXCEPTION 'Coluna homologacao.etiqueta.situacao_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='codigo_etiqueta') THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.codigo_etiqueta nao encontrada.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='situacao_produto_etiqueta' AND data_type='boolean') THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.situacao_produto_etiqueta ausente ou com tipo inesperado.';
    END IF;

    -- Colunas adicionais usadas pela massa transacional dos validadores.
    -- produto_referencia
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_referencia' AND column_name='codigo_produto_referencia' AND data_type IN ('smallint','integer','bigint')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_referencia.codigo_produto_referencia ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_referencia' AND column_name='codigo_produto' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_referencia.codigo_produto ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_referencia' AND column_name='descricao_produto_referencia' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_referencia.descricao_produto_referencia ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_referencia' AND column_name='origem' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_referencia.origem ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_referencia' AND column_name='situacao_produto_referencia' AND data_type='boolean') THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_referencia.situacao_produto_referencia ausente ou com tipo inesperado.';
    END IF;

    -- produto_etiqueta
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='codigo_produto_etiqueta' AND data_type IN ('smallint','integer','bigint')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.codigo_produto_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='codigo_produto_referencia' AND data_type IN ('smallint','integer','bigint')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.codigo_produto_referencia ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='codigo_etiqueta' AND data_type IN ('smallint','integer','bigint')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.codigo_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='tipo_etiqueta' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.tipo_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='principal' AND data_type='boolean') THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.principal ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='produto_etiqueta' AND column_name='situacao_produto_etiqueta' AND data_type='boolean') THEN
        RAISE EXCEPTION 'Coluna homologacao.produto_etiqueta.situacao_produto_etiqueta ausente ou com tipo inesperado.';
    END IF;

    -- modelo_etiqueta
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='modelo_etiqueta' AND column_name='codigo_modelo_etiqueta' AND data_type IN ('smallint','integer','bigint')) THEN
        RAISE EXCEPTION 'Coluna homologacao.modelo_etiqueta.codigo_modelo_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='modelo_etiqueta' AND column_name='nome_modelo_etiqueta' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.modelo_etiqueta.nome_modelo_etiqueta ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='modelo_etiqueta' AND column_name='versao' AND data_type IN ('smallint','integer','bigint','numeric')) THEN
        RAISE EXCEPTION 'Coluna homologacao.modelo_etiqueta.versao ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='modelo_etiqueta' AND column_name='conteudo_zpl' AND data_type IN ('character varying','text','character')) THEN
        RAISE EXCEPTION 'Coluna homologacao.modelo_etiqueta.conteudo_zpl ausente ou com tipo inesperado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='homologacao' AND table_name='modelo_etiqueta' AND column_name='situacao_modelo_etiqueta' AND data_type='boolean') THEN
        RAISE EXCEPTION 'Coluna homologacao.modelo_etiqueta.situacao_modelo_etiqueta ausente ou com tipo inesperado.';
    END IF;

    -- Objetos esperados do pacote e comentarios de propriedade.
    SELECT COUNT(*), COUNT(*) FILTER (WHERE comentario = v_comment)
      INTO v_existentes, v_comentados
    FROM (
        SELECT c.oid, obj_description(c.oid, 'pg_constraint') AS comentario
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'homologacao' AND t.relname = 'etiqueta'
          AND c.conname IN ('ck_etiqueta_codigo_tamanho','ck_etiqueta_nome_tamanho','ck_etiqueta_descricao_tamanho')
        UNION ALL
        SELECT i.oid, obj_description(i.oid, 'pg_class')
        FROM pg_class i
        JOIN pg_namespace ns ON ns.oid = i.relnamespace
        WHERE ns.nspname = 'homologacao' AND i.relkind = 'i' AND i.relname = 'uq_etiqueta_codigo_interno_global'
        UNION ALL
        SELECT p.oid, obj_description(p.oid, 'pg_proc')
        FROM pg_proc p
        JOIN pg_namespace ns ON ns.oid = p.pronamespace
        WHERE ns.nspname = 'homologacao' AND p.proname = 'fn_bloqueia_inativar_etiqueta_com_produto_ativo' AND p.pronargs = 0
        UNION ALL
        SELECT tg.oid, obj_description(tg.oid, 'pg_trigger')
        FROM pg_trigger tg
        JOIN pg_class t ON t.oid = tg.tgrelid
        JOIN pg_namespace ns ON ns.oid = t.relnamespace
        WHERE ns.nspname = 'homologacao' AND t.relname = 'etiqueta'
          AND tg.tgname = 'trg_bloqueia_inativar_etiqueta_com_produto_ativo'
          AND NOT tg.tgisinternal
    ) x;

    v_old_idx := to_regclass('homologacao.uq_etiqueta_codigo') IS NOT NULL;
    v_hist_idx := to_regclass('homologacao.uq_etiqueta_codigo_interno_ativo') IS NOT NULL;

    IF v_existentes = 0 THEN
        v_estado := 'PACOTE_NAO_APLICADO';
    ELSIF v_existentes = 6 AND v_comentados = 6 AND NOT v_old_idx AND NOT v_hist_idx THEN
        v_estado := 'PACOTE_COMPLETAMENTE_APLICADO';
    ELSE
        RAISE EXCEPTION 'ESTADO_PARCIAL_OU_INCOMPATIVEL: objetos_encontrados=%, objetos_com_comentario_036=%, indice_parcial_antigo=%, indice_historico=%', v_existentes, v_comentados, v_old_idx, v_hist_idx;
    END IF;
    RAISE NOTICE '%', v_estado;

    -- Inconsistencias de dados bloqueantes.
    SELECT COUNT(*) INTO v_inconsistencias
    FROM (
        SELECT 1 FROM (
            SELECT upper(trim(codigo_interno)) AS codigo_normalizado
            FROM homologacao.etiqueta
            GROUP BY upper(trim(codigo_interno))
            HAVING COUNT(*) > 1
        ) d
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE codigo_interno IS NULL OR trim(codigo_interno) = '' OR char_length(trim(codigo_interno)) > 80
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE nome_etiqueta IS NULL OR char_length(trim(nome_etiqueta)) < 2 OR char_length(trim(nome_etiqueta)) > 80
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE descricao_etiqueta IS NOT NULL AND char_length(trim(descricao_etiqueta)) > 255
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta WHERE tipo_etiqueta IS NULL OR tipo_etiqueta NOT IN ('CAIXA', 'PALETE', 'HU', 'INTERNA', 'OUTRA')
        UNION ALL
        SELECT 1 FROM homologacao.etiqueta e JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta = e.codigo_modelo_etiqueta WHERE e.situacao_etiqueta = true AND m.situacao_modelo_etiqueta = false
        UNION ALL
        SELECT 1 FROM homologacao.produto_etiqueta pe JOIN homologacao.etiqueta e ON e.codigo_etiqueta = pe.codigo_etiqueta WHERE pe.situacao_produto_etiqueta = true AND e.situacao_etiqueta = false
    ) s;

    IF v_inconsistencias > 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 036 HML bloqueado: existem % ocorrencias invalidas listadas nos diagnosticos acima.', v_inconsistencias;
    END IF;

    RAISE NOTICE 'OK - PREFLIGHT 036 HML concluido sem inconsistencias bloqueantes';
END $$;

ROLLBACK;
