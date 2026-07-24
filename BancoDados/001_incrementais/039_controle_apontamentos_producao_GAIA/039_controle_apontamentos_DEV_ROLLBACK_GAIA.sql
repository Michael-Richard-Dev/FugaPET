\set ON_ERROR_STOP on
-- ============================================================
-- 039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql
-- Projeto FugaPET_Dev  |  Schema: desenvolvimento
--
-- EXECUCAO CONTROLADA - NAO EXECUTAR AUTOMATICAMENTE. DESTRUTIVO.
-- Remove os objetos criados pelo 039. GUARDA DE SEGURANCA: por padrao NAO derruba tabelas com dados.
-- Para forcar mesmo com dados: SET fugapet.forcar_drop_039 = '1';
-- ============================================================
SET search_path TO desenvolvimento;

BEGIN;

-- Timeouts conservadores (padrao dos incrementais aprovados). LOCAL = valem so nesta transacao.
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';

DO $$
DECLARE
    v_forcar boolean := coalesce(current_setting('fugapet.forcar_drop_039', true) = '1', false);
    v_qtd_evento bigint := 0;
    v_qtd_apont  bigint := 0;
    v_qtd_config bigint := 0;
BEGIN
    IF to_regclass('desenvolvimento.operacao_producao_evento') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_evento' INTO v_qtd_evento;
    END IF;
    IF to_regclass('desenvolvimento.operacao_producao_apontamento') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_apontamento' INTO v_qtd_apont;
    END IF;
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_configuracao' INTO v_qtd_config;
    END IF;

    IF (v_qtd_evento > 0 OR v_qtd_apont > 0 OR v_qtd_config > 0) AND NOT v_forcar THEN
        RAISE EXCEPTION
            'ROLLBACK ABORTADO: existem dados (% eventos / % apontamentos / % configuracoes). Para forcar: SET fugapet.forcar_drop_039 = ''1'';',
            v_qtd_evento, v_qtd_apont, v_qtd_config;
    END IF;

    -- Ordem: filha antes da mae (FK evento -> apontamento).
    DROP TABLE IF EXISTS desenvolvimento.operacao_producao_evento;
    DROP TABLE IF EXISTS desenvolvimento.operacao_producao_apontamento;
    DROP TABLE IF EXISTS desenvolvimento.operacao_producao_configuracao;
    RAISE NOTICE 'ROLLBACK 039 concluido: tabelas removidas (indices/constraints caem junto).';
END $$;

-- Permissoes criadas pelo 039. Colunas REAIS: modulo_permissao / rotina_permissao / acao_permissao.
-- Remove primeiro os vinculos em perfil_permissao (FK RESTRICT) e depois as permissoes.
DO $$
BEGIN
    IF to_regclass('desenvolvimento.permissao') IS NULL THEN
        RETURN;
    END IF;

    IF to_regclass('desenvolvimento.perfil_permissao') IS NOT NULL THEN
        DELETE FROM desenvolvimento.perfil_permissao pp
         USING desenvolvimento.permissao p
         WHERE pp.codigo_permissao = p.codigo_permissao
           AND p.modulo_permissao = 'PROCESSO_PRODUCAO'
           AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS';
        RAISE NOTICE 'ROLLBACK 039: vinculos perfil_permissao de CONTROLE_APONTAMENTOS removidos.';
    END IF;

    DELETE FROM desenvolvimento.permissao
     WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
       AND rotina_permissao = 'CONTROLE_APONTAMENTOS';
    RAISE NOTICE 'ROLLBACK 039: permissoes CONTROLE_APONTAMENTOS removidas.';
END $$;

COMMIT;
