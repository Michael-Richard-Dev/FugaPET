\set ON_ERROR_STOP on
-- ============================================================
-- 071_tolerancia_processo_producao_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 071 - ROLLBACK
--
-- ATENCAO: APAGA DUAS COISAS DE NATUREZAS DIFERENTES.
--
--   1. as tolerancias cadastradas - decisoes do usuario, refazíveis na tela;
--   2. o LOG DE AUDITORIA das liberacoes - NAO refazivel. Cada linha registra
--      uma decisao gerencial de liberar um estouro, ou uma tentativa recusada.
--      E exatamente o tipo de registro que existe para ser consultado DEPOIS.
--
-- Por isso RECUSA enquanto houver linha em qualquer das duas. Para desfazer mesmo
-- assim:
--   psql -v confirmo_perder_dados=sim -f <este arquivo>
--
-- NAO remove a permissao LIBERAR_TOLERANCIA nem o perfil Gerente: eles sao do
-- incremento 070 e continuam servindo ao Recebimento. Remove-los aqui tiraria
-- acesso de pessoas por causa de um incremento que nem os criou.
--
-- EXIGE psql (a trava depende de variavel do cliente).
-- ============================================================

\if :{?confirmo_perder_dados}
\else
\set confirmo_perder_dados nao
\endif

BEGIN;

DO $$
DECLARE
    tolerancias bigint := 0;
    liberacoes  bigint := 0;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '071 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.tolerancia_processo_producao') IS NOT NULL THEN
        SELECT count(*) INTO tolerancias FROM desenvolvimento.tolerancia_processo_producao;
    END IF;

    IF to_regclass('desenvolvimento.liberacao_tolerancia_processo') IS NOT NULL THEN
        SELECT count(*) INTO liberacoes FROM desenvolvimento.liberacao_tolerancia_processo;
    END IF;

    IF (tolerancias > 0 OR liberacoes > 0) AND :'confirmo_perder_dados' <> 'sim' THEN
        RAISE EXCEPTION
            '071 ROLLBACK ABORTADO: % tolerancia(s) e % liberacao(oes) auditadas seriam perdidas. Rode com -v confirmo_perder_dados=sim se e isso mesmo.',
            tolerancias, liberacoes;
    END IF;

    IF liberacoes > 0 THEN
        RAISE WARNING '071 ROLLBACK: apagando % registro(s) de auditoria, por confirmacao explicita.', liberacoes;
    END IF;
END
$$;

DROP TABLE IF EXISTS desenvolvimento.liberacao_tolerancia_processo;
DROP TABLE IF EXISTS desenvolvimento.tolerancia_processo_producao;

COMMIT;

\echo '== 071 revertido. A permissao LIBERAR_TOLERANCIA e o perfil Gerente (070) seguem intactos. =='
