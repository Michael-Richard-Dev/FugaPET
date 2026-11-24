\set ON_ERROR_STOP on
-- ============================================================
-- 072_correcao_resultado_apontamento_Q_PREFLIGHT_GAIA.sql
-- FugaPET | Ambientes QAS110 e QAS130 | Schema homologacao
-- Incremento 072 (Q) - PREFLIGHT (SOMENTE LEITURA) - nao escreve nada
-- ============================================================

\echo '== 072 PREFLIGHT =='

SELECT current_database() AS banco, current_user AS usuario,
       CASE current_database()
            WHEN 'fuga_jales_local_homologacao_q_v1_2'    THEN 'ALVO OK - QAS110'
            WHEN 'fuga_jales_local_homologacao_q130_v1_3' THEN 'ALVO OK - QAS130'
            ELSE 'ALVO ERRADO - NAO APLIQUE' END AS veredito;

\echo '-- as tabelas do 041 existem? sem elas nao ha o que ajustar'
SELECT to_regclass('homologacao.operacao_resultado_registro')::text      AS registro,
       to_regclass('homologacao.operacao_resultado_registro_item')::text AS registro_item;

\echo '-- a unicidade por apontamento ainda esta la? (e ela que impede a correcao)'
SELECT conname, pg_get_constraintdef(oid) AS definicao
  FROM pg_constraint
 WHERE conrelid = to_regclass('homologacao.operacao_resultado_registro')
   AND contype = 'u';

\echo '-- quantos resultados ja gravados, e quantos apontamentos tem mais de um'
DO $contagem$
DECLARE
    registros bigint := 0;
    duplicados bigint := 0;
BEGIN
    IF to_regclass('homologacao.operacao_resultado_registro') IS NULL THEN
        RAISE NOTICE 'operacao_resultado_registro ainda nao existe: aplique o 041 antes do 072.';
        RETURN;
    END IF;

    EXECUTE 'SELECT count(*) FROM homologacao.operacao_resultado_registro' INTO registros;
    EXECUTE $q$
        SELECT count(*) FROM (
            SELECT codigo_apontamento
              FROM homologacao.operacao_resultado_registro
             GROUP BY codigo_apontamento HAVING count(*) > 1) d
    $q$ INTO duplicados;

    RAISE NOTICE '% resultado(s) gravado(s); % apontamento(s) ja com mais de um', registros, duplicados;
END
$contagem$;

\echo '== Fim do PREFLIGHT. Nada foi escrito. =='
