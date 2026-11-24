\set ON_ERROR_STOP on
-- ============================================================
-- 070_liberacao_tolerancia_recebimento_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 070 - PREFLIGHT (SOMENTE LEITURA) - nao escreve nada
-- ============================================================

\echo '== 070 PREFLIGHT =='

SELECT current_database() AS banco, current_user AS usuario,
       CASE WHEN current_database()='fuga_jales_local_desenvolvimento'
            THEN 'ALVO OK' ELSE 'ALVO ERRADO - NAO APLIQUE' END AS veredito;

\echo '-- papel da aplicacao'
SELECT rolname FROM pg_roles WHERE rolname='fugapet_dev_app';

\echo '-- tabelas de seguranca de que o incremento depende'
SELECT to_regclass('desenvolvimento.permissao')::text        AS permissao,
       to_regclass('desenvolvimento.perfil_acesso')::text    AS perfil_acesso,
       to_regclass('desenvolvimento.perfil_permissao')::text AS perfil_permissao;

\echo '-- a tabela de log ja existe?'
SELECT to_regclass('desenvolvimento.liberacao_tolerancia_recebimento')::text AS tabela;

\echo '-- a permissao LIBERAR_TOLERANCIA ja existe?'
SELECT codigo_permissao, situacao_permissao
  FROM desenvolvimento.permissao
 WHERE modulo_permissao='PROCESSO_PRODUCAO'
   AND rotina_permissao='ENTRADA_PRODUTO'
   AND acao_permissao='LIBERAR_TOLERANCIA';

\echo '-- perfis atuais (o incremento acrescenta Gerente se faltar)'
SELECT codigo_perfil_acesso, nome_perfil_acesso, situacao_perfil_acesso
  FROM desenvolvimento.perfil_acesso ORDER BY codigo_perfil_acesso;

\echo '-- quantas liberacoes ja gravadas (reaplicacao nao pode perder log)'
DO $contagem$
DECLARE linhas bigint;
BEGIN
    IF to_regclass('desenvolvimento.liberacao_tolerancia_recebimento') IS NULL THEN
        RAISE NOTICE 'tabela ainda nao existe (primeira aplicacao)';
        RETURN;
    END IF;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.liberacao_tolerancia_recebimento' INTO linhas;
    RAISE NOTICE '% liberacao(oes) ja gravadas', linhas;
END
$contagem$;

\echo '== Fim do PREFLIGHT. Nada foi escrito. =='
