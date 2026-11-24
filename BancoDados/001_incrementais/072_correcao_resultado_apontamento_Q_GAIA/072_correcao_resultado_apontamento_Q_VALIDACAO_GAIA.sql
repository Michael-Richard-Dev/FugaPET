\set ON_ERROR_STOP on
-- ============================================================
-- 072_correcao_resultado_apontamento_Q_VALIDACAO_GAIA.sql
-- FugaPET | Ambientes QAS110 e QAS130 | Schema homologacao
-- Incremento 072 (Q) - VALIDACAO (SOMENTE LEITURA) - nao escreve nada
--
-- Prova pela definicao REAL lida do catalogo. Nenhuma linha e inserida: um teste
-- que grava para depois apagar deixaria sequencia furada e, numa tabela de
-- auditoria append-only, lixo que ninguem saberia distinguir de dado real.
-- ============================================================

\echo '== 072 VALIDACAO =='

SELECT current_database() AS banco, current_user AS usuario;

\echo '-- 1. a unicidade por apontamento SAIU'
SELECT CASE WHEN EXISTS (
         SELECT 1 FROM pg_constraint
          WHERE conrelid = to_regclass('homologacao.operacao_resultado_registro')
            AND conname = 'uq_operacao_resultado_registro_apontamento')
       THEN 'FALHOU: a restricao continua la; a correcao seguiria impossivel'
       ELSE 'OK: um apontamento pode ter mais de um resultado' END AS veredito;

\echo '-- 2. nenhuma OUTRA unicidade sobrou sobre codigo_apontamento'
-- Tirar a restricao nomeada e deixar outra equivalente seria corrigir no papel.
SELECT CASE WHEN EXISTS (
         SELECT 1
           FROM pg_index i
          WHERE i.indrelid = to_regclass('homologacao.operacao_resultado_registro')
            AND i.indisunique
            AND pg_get_indexdef(i.indexrelid) ILIKE '%(codigo_apontamento)%')
       THEN 'FALHOU: ainda existe indice unico por codigo_apontamento'
       ELSE 'OK: nenhuma unicidade por apontamento' END AS veredito;

\echo '-- 3. a chave primaria continua de pe (append-only precisa de identidade)'
SELECT CASE WHEN pg_get_constraintdef(oid) = 'PRIMARY KEY (codigo_resultado)'
            THEN 'OK: PK por codigo_resultado'
            ELSE 'FALHOU: ' || pg_get_constraintdef(oid) END AS veredito
  FROM pg_constraint
 WHERE conrelid = to_regclass('homologacao.operacao_resultado_registro')
   AND contype = 'p';

\echo '-- 4. o vinculo com o apontamento continua obrigatorio e protegido'
SELECT CASE WHEN EXISTS (
         SELECT 1 FROM pg_constraint
          WHERE conrelid = to_regclass('homologacao.operacao_resultado_registro')
            AND contype = 'f'
            AND pg_get_constraintdef(oid) ILIKE '%operacao_producao_apontamento%'
            AND pg_get_constraintdef(oid) ILIKE '%ON DELETE RESTRICT%')
       THEN 'OK: FK para o apontamento com ON DELETE RESTRICT'
       ELSE 'FALHOU: o vinculo com o apontamento mudou' END AS veredito;

\echo '-- 5. o indice de busca por apontamento existe (a UNIQUE tambem servia de indice)'
SELECT CASE WHEN EXISTS (
         SELECT 1 FROM pg_indexes
          WHERE schemaname='homologacao'
            AND tablename='operacao_resultado_registro'
            AND indexname='ix_resultado_registro_apontamento')
       THEN 'OK: indice de busca presente'
       ELSE 'FALHOU: sem indice, a tela passaria a varrer a tabela' END AS veredito;

\echo '-- 6. os itens continuam amarrados ao registro (nada ficou orfao)'
SELECT CASE WHEN EXISTS (
         SELECT 1 FROM pg_constraint
          WHERE conrelid = to_regclass('homologacao.operacao_resultado_registro_item')
            AND contype = 'f'
            AND pg_get_constraintdef(oid) ILIKE '%operacao_resultado_registro%')
       THEN 'OK: itens amarrados ao registro'
       ELSE 'FALHOU: vinculo item-registro ausente' END AS veredito;

\echo '-- 7. retrato dos dados: quantos apontamentos ja tem correcao'
SELECT count(*) AS apontamentos_com_mais_de_um_resultado
  FROM (SELECT codigo_apontamento
          FROM homologacao.operacao_resultado_registro
         GROUP BY codigo_apontamento HAVING count(*) > 1) d;

\echo '-- 8. o papel da aplicacao continua podendo INSERIR (a correcao e um INSERT)'
DO $privilegios$
DECLARE
    papel text := CASE current_database()
        WHEN 'fuga_jales_local_homologacao_q_v1_2'    THEN 'fugapet_q_app'
        WHEN 'fuga_jales_local_homologacao_q130_v1_3' THEN 'fugapet_q130_app'
    END;
BEGIN
    IF papel IS NULL THEN
        RAISE EXCEPTION 'FALHOU: banco nao reconhecido (%).', current_database();
    END IF;

    IF NOT has_table_privilege(papel, 'homologacao.operacao_resultado_registro', 'INSERT')
       OR NOT has_table_privilege(papel, 'homologacao.operacao_resultado_registro_item', 'INSERT') THEN
        RAISE EXCEPTION 'FALHOU: % nao pode inserir resultado.', papel;
    END IF;

    RAISE NOTICE 'OK: % pode registrar a correcao.', papel;
END
$privilegios$;

\echo '== Fim da VALIDACAO. Nada foi escrito. =='
