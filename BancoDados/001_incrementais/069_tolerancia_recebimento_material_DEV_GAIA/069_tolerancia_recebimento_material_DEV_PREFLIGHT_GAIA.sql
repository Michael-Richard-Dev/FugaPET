\set ON_ERROR_STOP on
-- ============================================================
-- 069_tolerancia_recebimento_material_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 069 - Tolerancia de pesagem por material no Recebimento
-- PREFLIGHT (SOMENTE LEITURA) - nao escreve nada
--
-- Rode ANTES da PROPOSTA e leia a saida. Ele responde tres perguntas:
--   1. Estou no banco certo?
--   2. O papel da aplicacao existe (senao o GRANT falha no fim da transacao)?
--   3. A tabela ja existe (reaplicacao) e, se existe, com que forma?
-- ============================================================

\echo '== 069 PREFLIGHT =='

SELECT current_database() AS banco,
       current_user       AS usuario,
       CASE WHEN current_database() = 'fuga_jales_local_desenvolvimento'
            THEN 'ALVO OK'
            ELSE 'ALVO ERRADO - NAO APLIQUE'
       END AS veredito;

\echo '-- papel da aplicacao (a PROPOSTA faz GRANT para ele)'
SELECT rolname FROM pg_roles WHERE rolname = 'fugapet_dev_app';

\echo '-- a tabela ja existe?'
SELECT to_regclass('desenvolvimento.tolerancia_recebimento_material') AS tabela;

\echo '-- se existir, as colunas atuais (para comparar com o que a PROPOSTA cria)'
SELECT column_name, data_type, numeric_precision, numeric_scale, is_nullable, column_default
  FROM information_schema.columns
 WHERE table_schema = 'desenvolvimento'
   AND table_name   = 'tolerancia_recebimento_material'
 ORDER BY ordinal_position;

\echo '-- restricoes atuais, se houver'
SELECT conname, pg_get_constraintdef(oid) AS definicao
  FROM pg_constraint
 WHERE conrelid = to_regclass('desenvolvimento.tolerancia_recebimento_material')
 ORDER BY conname;

\echo '-- quantas linhas (reaplicacao nao pode apagar cadastro existente)'
-- SQL DINAMICO de proposito. Um CASE com SELECT count(*) na tabela seria analisado
-- pelo Postgres mesmo quando o ramo nao e escolhido, e falharia com 42P01 justamente
-- no caso MAIS COMUM deste preflight: a tabela ainda nao existe.
DO $contagem$
DECLARE
    linhas bigint;
BEGIN
    IF to_regclass('desenvolvimento.tolerancia_recebimento_material') IS NULL THEN
        RAISE NOTICE 'tabela ainda nao existe (primeira aplicacao)';
        RETURN;
    END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.tolerancia_recebimento_material' INTO linhas;
    RAISE NOTICE '% linha(s) ja cadastradas', linhas;
END
$contagem$;

\echo '== Fim do PREFLIGHT. Nada foi escrito. =='
