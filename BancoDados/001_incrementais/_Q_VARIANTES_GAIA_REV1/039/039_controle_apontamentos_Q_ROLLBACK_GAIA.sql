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
-- 039_controle_apontamentos_HML_ROLLBACK_GAIA.sql
-- Projeto FugaPET_HML  |  Schema: homologacao
--
-- EXECUCAO CONTROLADA - NAO EXECUTAR AUTOMATICAMENTE. DESTRUTIVO.
-- Remove os objetos criados pelo 039. GUARDA DE SEGURANCA: por padrao NAO derruba tabelas com dados.
-- Para forcar mesmo com dados: SET fugapet.forcar_drop_039 = '1';
-- ============================================================
SET search_path TO homologacao;

BEGIN;

-- Timeouts conservadores (padrao dos incrementais aprovados). LOCAL = valem so nesta transacao.
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';

-- Guarda de ambiente HML: impede execucao acidental em outro banco/schema.
DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION 'Ambiente incorreto para o pacote: banco atual=%, esperado=fuga_jales_local_homologacao_q_v1_2. Execucao bloqueada.', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'Ambiente incorreto para o pacote: schema homologacao inexistente. Execucao bloqueada.';
    END IF;
END $$;

DO $$
DECLARE
    v_forcar boolean := coalesce(current_setting('fugapet.forcar_drop_039', true) = '1', false);
    v_qtd_evento bigint := 0;
    v_qtd_apont  bigint := 0;
    v_qtd_config bigint := 0;
BEGIN
    IF to_regclass('homologacao.operacao_producao_evento') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM homologacao.operacao_producao_evento' INTO v_qtd_evento;
    END IF;
    IF to_regclass('homologacao.operacao_producao_apontamento') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM homologacao.operacao_producao_apontamento' INTO v_qtd_apont;
    END IF;
    IF to_regclass('homologacao.operacao_producao_configuracao') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM homologacao.operacao_producao_configuracao' INTO v_qtd_config;
    END IF;

    IF (v_qtd_evento > 0 OR v_qtd_apont > 0 OR v_qtd_config > 0) AND NOT v_forcar THEN
        RAISE EXCEPTION
            'ROLLBACK ABORTADO: existem dados (% eventos / % apontamentos / % configuracoes). Para forcar: SET fugapet.forcar_drop_039 = ''1'';',
            v_qtd_evento, v_qtd_apont, v_qtd_config;
    END IF;

    -- Ordem: filha antes da mae (FK evento -> apontamento).
    DROP TABLE IF EXISTS homologacao.operacao_producao_evento;
    DROP TABLE IF EXISTS homologacao.operacao_producao_apontamento;
    DROP TABLE IF EXISTS homologacao.operacao_producao_configuracao;
    RAISE NOTICE 'ROLLBACK 039 concluido: tabelas removidas (indices/constraints caem junto).';
END $$;

-- Permissoes criadas pelo 039. Colunas REAIS: modulo_permissao / rotina_permissao / acao_permissao.
-- Remove primeiro os vinculos em perfil_permissao (FK RESTRICT) e depois as permissoes.
DO $$
BEGIN
    IF to_regclass('homologacao.permissao') IS NULL THEN
        RETURN;
    END IF;

    IF to_regclass('homologacao.perfil_permissao') IS NOT NULL THEN
        DELETE FROM homologacao.perfil_permissao pp
         USING homologacao.permissao p
         WHERE pp.codigo_permissao = p.codigo_permissao
           AND p.modulo_permissao = 'PROCESSO_PRODUCAO'
           AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS';
        RAISE NOTICE 'ROLLBACK 039: vinculos perfil_permissao de CONTROLE_APONTAMENTOS removidos.';
    END IF;

    DELETE FROM homologacao.permissao
     WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
       AND rotina_permissao = 'CONTROLE_APONTAMENTOS';
    RAISE NOTICE 'ROLLBACK 039: permissoes CONTROLE_APONTAMENTOS removidas.';
END $$;

COMMIT;
