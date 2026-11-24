\set ON_ERROR_STOP on
-- ============================================================
-- 071_tolerancia_processo_producao_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 071 - PREFLIGHT (SOMENTE LEITURA) - nao escreve nada
-- ============================================================

\echo '== 071 PREFLIGHT =='

SELECT current_database() AS banco, current_user AS usuario,
       CASE WHEN current_database()='fuga_jales_local_desenvolvimento'
            THEN 'ALVO OK' ELSE 'ALVO ERRADO - NAO APLIQUE' END AS veredito;

\echo '-- papel da aplicacao'
SELECT rolname FROM pg_roles WHERE rolname='fugapet_dev_app';

\echo '-- o catalogo de produtos (066) existe? a tela le dele'
SELECT to_regclass('desenvolvimento.produto_catalogo_centro')::text AS catalogo;

\echo '-- a tolerancia do RECEBIMENTO (069) continua separada e intacta'
SELECT to_regclass('desenvolvimento.tolerancia_recebimento_material')::text AS tolerancia_069;

\echo '-- as tabelas do 071 ja existem?'
SELECT to_regclass('desenvolvimento.tolerancia_processo_producao')::text  AS tolerancia_071,
       to_regclass('desenvolvimento.liberacao_tolerancia_processo')::text AS log_071;

\echo '-- a permissao de liberacao (070) existe? o 071 REUSA a mesma, nao cria outra'
SELECT codigo_permissao, situacao_permissao
  FROM desenvolvimento.permissao
 WHERE modulo_permissao='PROCESSO_PRODUCAO'
   AND rotina_permissao='ENTRADA_PRODUTO'
   AND acao_permissao='LIBERAR_TOLERANCIA';

\echo '-- quantas linhas ja gravadas (reaplicacao nao pode perder nada)'
DO $contagem$
DECLARE linhas bigint;
BEGIN
    IF to_regclass('desenvolvimento.tolerancia_processo_producao') IS NULL THEN
        RAISE NOTICE 'tolerancia_processo_producao ainda nao existe (primeira aplicacao)';
    ELSE
        EXECUTE 'SELECT count(*) FROM desenvolvimento.tolerancia_processo_producao' INTO linhas;
        RAISE NOTICE '% tolerancia(s) de processo ja cadastradas', linhas;
    END IF;

    IF to_regclass('desenvolvimento.liberacao_tolerancia_processo') IS NULL THEN
        RAISE NOTICE 'liberacao_tolerancia_processo ainda nao existe (primeira aplicacao)';
    ELSE
        EXECUTE 'SELECT count(*) FROM desenvolvimento.liberacao_tolerancia_processo' INTO linhas;
        RAISE NOTICE '% liberacao(oes) de processo ja gravadas', linhas;
    END IF;
END
$contagem$;

\echo '== Fim do PREFLIGHT. Nada foi escrito. =='
