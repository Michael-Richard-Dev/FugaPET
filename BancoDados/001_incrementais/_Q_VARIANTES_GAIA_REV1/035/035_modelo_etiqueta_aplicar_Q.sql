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
-- 035_modelo_etiqueta_aplicar_HML.sql
-- Projeto FugaPET_HML  |  Schema: homologacao
-- Objeto: Cadastro de Modelo de Etiqueta
-- Responsavel pelo projeto: Michael Richard
-- Incremental: 035
-- Finalidade: aplicacao
--
-- EXECUCAO CONTROLADA.
-- Nao executar automaticamente.
-- Nao altera dados reais automaticamente.
-- Nao contem PRD.
-- ============================================================
\set ON_ERROR_STOP on
SET search_path TO homologacao;


BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- 0) Preflight embutido: aborta antes de qualquer DROP/CREATE se houver dados invalidos
--    ou objetos finais preexistentes sem comentario de propriedade do pacote 035.
DO $$
DECLARE
    v_dups int; v_nome int; v_obs int; v_dpi int; v_larg int; v_alt int; v_orf int;
    v_unowned text;
BEGIN
    IF to_regclass('homologacao.modelo_etiqueta') IS NULL OR to_regclass('homologacao.etiqueta') IS NULL THEN
        RAISE EXCEPTION 'APLICAR 035 abortado: tabelas modelo_etiqueta/etiqueta ausentes no schema homologacao.';
    END IF;

    SELECT count(*) INTO v_dups FROM (SELECT 1 FROM homologacao.modelo_etiqueta GROUP BY upper(trim(nome_modelo_etiqueta)), versao HAVING count(*) > 1) d;
    SELECT count(*) INTO v_nome FROM homologacao.modelo_etiqueta WHERE nome_modelo_etiqueta IS NULL OR char_length(trim(nome_modelo_etiqueta)) NOT BETWEEN 2 AND 80;
    SELECT count(*) INTO v_obs FROM homologacao.modelo_etiqueta WHERE observacao IS NOT NULL AND char_length(trim(observacao)) > 255;
    SELECT count(*) INTO v_dpi FROM homologacao.modelo_etiqueta WHERE dpi IS NOT NULL AND dpi <= 0;
    SELECT count(*) INTO v_larg FROM homologacao.modelo_etiqueta WHERE largura_mm IS NOT NULL AND largura_mm <= 0;
    SELECT count(*) INTO v_alt FROM homologacao.modelo_etiqueta WHERE altura_mm IS NOT NULL AND altura_mm <= 0;
    SELECT count(*) INTO v_orf FROM homologacao.etiqueta e JOIN homologacao.modelo_etiqueta m ON m.codigo_modelo_etiqueta = e.codigo_modelo_etiqueta WHERE e.situacao_etiqueta = true AND m.situacao_modelo_etiqueta = false;

    IF (v_dups + v_nome + v_obs + v_dpi + v_larg + v_alt + v_orf) > 0 THEN
        RAISE EXCEPTION 'APLICAR 035 abortado: inconsistencias (dup=% nome=% obs=% dpi=% largura=% altura=% etiqueta_ativa_modelo_inativo=%). Rode o preflight.', v_dups, v_nome, v_obs, v_dpi, v_larg, v_alt, v_orf;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_versao' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        RAISE EXCEPTION 'APLICAR 035 abortado: check antigo ck_modelo_etiqueta_versao ausente.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_zpl_nao_vazio' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        RAISE EXCEPTION 'APLICAR 035 abortado: check antigo ck_modelo_etiqueta_zpl_nao_vazio ausente.';
    END IF;

    WITH objetos AS (
        SELECT 'INDICE' AS tipo, 'uq_modelo_etiqueta_nome_versao_global' AS nome,
               obj_description(to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global'), 'pg_class') AS comentario,
               to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global') IS NOT NULL AS presente
        UNION ALL
        SELECT 'FUNCAO', 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa',
               (SELECT obj_description(p.oid, 'pg_proc') FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='homologacao' AND p.proname='fn_bloqueia_inativar_modelo_com_etiqueta_ativa' AND p.pronargs=0 LIMIT 1),
               to_regprocedure('homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()') IS NOT NULL
        UNION ALL
        SELECT 'TRIGGER', 'trg_bloqueia_inativar_modelo_com_etiqueta_ativa',
               (SELECT obj_description(t.oid, 'pg_trigger') FROM pg_trigger t WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa' AND t.tgrelid='homologacao.modelo_etiqueta'::regclass AND NOT t.tgisinternal LIMIT 1),
               EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa' AND t.tgrelid='homologacao.modelo_etiqueta'::regclass AND NOT t.tgisinternal)
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
        RAISE EXCEPTION 'APLICAR 035 abortado: objetos finais existem sem comentario de propriedade do pacote 035: %', v_unowned;
    END IF;
END $$;

-- 1) Recriar checks pertencentes ao pacote para garantir definicao correta.
DO $$
BEGIN

    IF EXISTS (
        SELECT 1 FROM pg_constraint c
         WHERE c.conrelid = 'homologacao.modelo_etiqueta'::regclass
           AND c.conname = 'ck_modelo_etiqueta_nome_tamanho'
           AND coalesce(obj_description(c.oid, 'pg_constraint'), '') LIKE '%FugaPET incremental 035 modelo_etiqueta%'
    ) THEN
        ALTER TABLE homologacao.modelo_etiqueta DROP CONSTRAINT ck_modelo_etiqueta_nome_tamanho;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_nome_tamanho' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        ALTER TABLE homologacao.modelo_etiqueta ADD CONSTRAINT ck_modelo_etiqueta_nome_tamanho CHECK (char_length(trim(nome_modelo_etiqueta)) BETWEEN 2 AND 80);
        COMMENT ON CONSTRAINT ck_modelo_etiqueta_nome_tamanho ON homologacao.modelo_etiqueta IS 'FugaPET incremental 035 modelo_etiqueta';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint c
         WHERE c.conrelid = 'homologacao.modelo_etiqueta'::regclass
           AND c.conname = 'ck_modelo_etiqueta_observacao_tamanho'
           AND coalesce(obj_description(c.oid, 'pg_constraint'), '') LIKE '%FugaPET incremental 035 modelo_etiqueta%'
    ) THEN
        ALTER TABLE homologacao.modelo_etiqueta DROP CONSTRAINT ck_modelo_etiqueta_observacao_tamanho;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_observacao_tamanho' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        ALTER TABLE homologacao.modelo_etiqueta ADD CONSTRAINT ck_modelo_etiqueta_observacao_tamanho CHECK (observacao IS NULL OR char_length(trim(observacao)) <= 255);
        COMMENT ON CONSTRAINT ck_modelo_etiqueta_observacao_tamanho ON homologacao.modelo_etiqueta IS 'FugaPET incremental 035 modelo_etiqueta';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint c
         WHERE c.conrelid = 'homologacao.modelo_etiqueta'::regclass
           AND c.conname = 'ck_modelo_etiqueta_dpi_positivo'
           AND coalesce(obj_description(c.oid, 'pg_constraint'), '') LIKE '%FugaPET incremental 035 modelo_etiqueta%'
    ) THEN
        ALTER TABLE homologacao.modelo_etiqueta DROP CONSTRAINT ck_modelo_etiqueta_dpi_positivo;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_dpi_positivo' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        ALTER TABLE homologacao.modelo_etiqueta ADD CONSTRAINT ck_modelo_etiqueta_dpi_positivo CHECK (dpi IS NULL OR dpi > 0);
        COMMENT ON CONSTRAINT ck_modelo_etiqueta_dpi_positivo ON homologacao.modelo_etiqueta IS 'FugaPET incremental 035 modelo_etiqueta';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint c
         WHERE c.conrelid = 'homologacao.modelo_etiqueta'::regclass
           AND c.conname = 'ck_modelo_etiqueta_largura_positiva'
           AND coalesce(obj_description(c.oid, 'pg_constraint'), '') LIKE '%FugaPET incremental 035 modelo_etiqueta%'
    ) THEN
        ALTER TABLE homologacao.modelo_etiqueta DROP CONSTRAINT ck_modelo_etiqueta_largura_positiva;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_largura_positiva' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        ALTER TABLE homologacao.modelo_etiqueta ADD CONSTRAINT ck_modelo_etiqueta_largura_positiva CHECK (largura_mm IS NULL OR largura_mm > 0);
        COMMENT ON CONSTRAINT ck_modelo_etiqueta_largura_positiva ON homologacao.modelo_etiqueta IS 'FugaPET incremental 035 modelo_etiqueta';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint c
         WHERE c.conrelid = 'homologacao.modelo_etiqueta'::regclass
           AND c.conname = 'ck_modelo_etiqueta_altura_positiva'
           AND coalesce(obj_description(c.oid, 'pg_constraint'), '') LIKE '%FugaPET incremental 035 modelo_etiqueta%'
    ) THEN
        ALTER TABLE homologacao.modelo_etiqueta DROP CONSTRAINT ck_modelo_etiqueta_altura_positiva;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_modelo_etiqueta_altura_positiva' AND conrelid='homologacao.modelo_etiqueta'::regclass) THEN
        ALTER TABLE homologacao.modelo_etiqueta ADD CONSTRAINT ck_modelo_etiqueta_altura_positiva CHECK (altura_mm IS NULL OR altura_mm > 0);
        COMMENT ON CONSTRAINT ck_modelo_etiqueta_altura_positiva ON homologacao.modelo_etiqueta IS 'FugaPET incremental 035 modelo_etiqueta';
    END IF;

END $$;

-- 2) Substituir indice parcial antigo pelo indice unico global.
DO $$
BEGIN
    IF to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global') IS NOT NULL THEN
        IF coalesce(obj_description(to_regclass('homologacao.uq_modelo_etiqueta_nome_versao_global'), 'pg_class'), '') LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            DROP INDEX homologacao.uq_modelo_etiqueta_nome_versao_global;
        ELSE
            RAISE EXCEPTION 'Indice global ja existe sem comentario de propriedade do pacote 035.';
        END IF;
    END IF;
END $$;

DROP INDEX IF EXISTS homologacao.uq_modelo_etiqueta_nome_versao;

CREATE UNIQUE INDEX uq_modelo_etiqueta_nome_versao_global
    ON homologacao.modelo_etiqueta (upper(trim(nome_modelo_etiqueta)), versao);
COMMENT ON INDEX homologacao.uq_modelo_etiqueta_nome_versao_global IS 'FugaPET incremental 035 modelo_etiqueta';

-- 3) Criar/substituir funcao. Se existir sem comentario do pacote, aborta.
DO $$
DECLARE v_comment text;
BEGIN
    IF to_regprocedure('homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()') IS NOT NULL THEN
        SELECT obj_description(p.oid, 'pg_proc') INTO v_comment
          FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'homologacao'
           AND p.proname = 'fn_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND p.pronargs = 0
         LIMIT 1;
        IF coalesce(v_comment, '') NOT LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            RAISE EXCEPTION 'Funcao ja existe sem comentario de propriedade do pacote 035.';
        END IF;
    END IF;
END $$;

CREATE OR REPLACE FUNCTION homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa()
RETURNS trigger AS $fn$
BEGIN
    IF OLD.situacao_modelo_etiqueta = true AND NEW.situacao_modelo_etiqueta = false THEN
        IF EXISTS (
            SELECT 1
              FROM homologacao.etiqueta e
             WHERE e.codigo_modelo_etiqueta = OLD.codigo_modelo_etiqueta
               AND e.situacao_etiqueta = true
        ) THEN
            RAISE EXCEPTION 'Nao e possivel inativar este modelo porque existem etiquetas ativas vinculadas a ele.';
        END IF;
    END IF;
    RETURN NEW;
END;
$fn$ LANGUAGE plpgsql;
COMMENT ON FUNCTION homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa() IS 'FugaPET incremental 035 modelo_etiqueta';

-- 4) Recriar trigger somente se ausente ou se ja pertencer ao pacote 035.
DO $$
DECLARE v_comment text;
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_trigger t
         WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND t.tgrelid='homologacao.modelo_etiqueta'::regclass
           AND NOT t.tgisinternal
    ) THEN
        SELECT obj_description(t.oid, 'pg_trigger') INTO v_comment
          FROM pg_trigger t
         WHERE t.tgname='trg_bloqueia_inativar_modelo_com_etiqueta_ativa'
           AND t.tgrelid='homologacao.modelo_etiqueta'::regclass
           AND NOT t.tgisinternal
         LIMIT 1;
        IF coalesce(v_comment, '') NOT LIKE '%FugaPET incremental 035 modelo_etiqueta%' THEN
            RAISE EXCEPTION 'Trigger ja existe sem comentario de propriedade do pacote 035.';
        END IF;
        DROP TRIGGER trg_bloqueia_inativar_modelo_com_etiqueta_ativa ON homologacao.modelo_etiqueta;
    END IF;
END $$;

CREATE TRIGGER trg_bloqueia_inativar_modelo_com_etiqueta_ativa
    BEFORE UPDATE OF situacao_modelo_etiqueta ON homologacao.modelo_etiqueta
    FOR EACH ROW
    EXECUTE FUNCTION homologacao.fn_bloqueia_inativar_modelo_com_etiqueta_ativa();
COMMENT ON TRIGGER trg_bloqueia_inativar_modelo_com_etiqueta_ativa ON homologacao.modelo_etiqueta IS 'FugaPET incremental 035 modelo_etiqueta';

COMMIT;

SELECT 'OK - APLICACAO 035 HML concluida' AS resultado;
