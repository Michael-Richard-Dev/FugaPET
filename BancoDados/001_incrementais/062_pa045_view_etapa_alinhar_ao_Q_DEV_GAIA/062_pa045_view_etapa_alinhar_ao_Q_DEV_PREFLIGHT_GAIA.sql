\set ON_ERROR_STOP on
-- ============================================================
-- 062_pa045_view_etapa_alinhar_ao_Q_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 062 - Expor a coluna "etapa" na vw_pa_045_etapa_estado_runtime
-- PREFLIGHT (SOMENTE LEITURA)
--
-- POR QUE ESTE INCREMENTO EXISTE
-- Apertar ENVIAR CAIXA SAP no DEV110 devolvia, em 16/11/2026:
--   Etapa: Bloqueada / ERRO LOCAL
--   "Vinculo 045 inconsistente (etapas=2; 261=nao; 101=nao).
--    Esperado exatamente 261 + 101. Envio bloqueado; nenhum POST executado."
--
-- Repare na contradicao: ele ACHA duas etapas e nao reconhece NENHUMA. O
-- orquestrador le as etapas por esta view e descobre o nome de cada uma em
-- ProdutoAcabadoPipeline045Orquestrador.NomeEtapa045, que procura, NESTA ordem,
-- as colunas "etapa", "codigo_etapa" e "tipo_etapa" -- e NUNCA "etapa_sap".
-- Sem nenhuma delas, o nome e null, tem261=false, tem101=false, e o pipeline
-- para ANTES do 261. Por isso as etapas ficavam PENDENTE com numero_tentativa=0
-- e o logs/pa045_runtime_trace.log nunca era criado: o trace so e gravado dentro
-- de ExecutarEtapaAsync, que jamais era alcancado.
--
-- ORIGEM. O QAS110 nao tem esse problema porque a MESMA view, la, termina com
-- uma coluna a mais: "etapa_sap AS etapa". Fora essa linha, as duas definicoes
-- sao identicas. Texto extraido em leitura do banco do Q
-- (fuga_jales_local_homologacao_q_v1_2) com pg_get_viewdef; nada inferido.
--
-- O CODIGO C# E IDENTICO nos dois ambientes: os 54 arquivos do subsistema
-- Produto Acabado foram comparados um a um, normalizando o namespace. A
-- divergencia e SOMENTE esta coluna.
--
-- ESCOPO. Acrescenta UMA coluna derivada ao FIM da view. Nao altera tabela, nao
-- altera dado, nao cria indice, nao mexe em funcao nem em gatilho. Nenhuma
-- coluna existente muda de nome, tipo ou posicao -- requisito do proprio
-- CREATE OR REPLACE VIEW, que recusa qualquer outra alteracao.
-- ============================================================

\echo ''
\echo '=== 062 PREFLIGHT ==='

SELECT current_database() AS banco, current_user AS papel, current_schema() AS schema_corrente;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '062 PREFLIGHT: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.vw_pa_045_etapa_estado_runtime') IS NULL THEN
        RAISE EXCEPTION '062 PREFLIGHT: a view nao existe neste banco.';
    END IF;

    RAISE NOTICE 'ALVO=OK  view presente';
END
$$;

-- 1. A coluna "etapa" esta mesmo ausente? (esperado: ausente -- e a causa)
\echo ''
\echo '-- a view expoe alguma coluna que NomeEtapa045 procura? --'
SELECT
    bool_or(column_name = 'etapa')        AS tem_etapa,
    bool_or(column_name = 'codigo_etapa') AS tem_codigo_etapa,
    bool_or(column_name = 'tipo_etapa')   AS tem_tipo_etapa,
    bool_or(column_name = 'etapa_sap')    AS tem_etapa_sap
  FROM information_schema.columns
 WHERE table_schema = 'desenvolvimento'
   AND table_name   = 'vw_pa_045_etapa_estado_runtime';

-- 2. As caixas presas por causa disso: tem o vinculo 261+101 correto na TABELA,
--    e ainda assim o pipeline nao as reconhece.
\echo ''
\echo '-- caixas aguardando envio e as etapas que elas de fato possuem --'
SELECT c.codigo_hu_caixa,
       c.numero_ordem_producao,
       c.status_hu_caixa,
       string_agg(e.etapa_sap || '=' || e.status_etapa, ' | ' ORDER BY e.etapa_sap) AS etapas_na_tabela,
       count(e.*) AS qtd
  FROM desenvolvimento.hu_caixa c
  LEFT JOIN desenvolvimento.hu_caixa_etapa_sap e ON e.codigo_hu_caixa = c.codigo_hu_caixa
 WHERE c.status_hu_caixa IN ('AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO','FINALIZADA_LOCAL')
 GROUP BY 1,2,3
 ORDER BY 1;

-- 3. Retrato da view, para comparar depois.
\echo ''
\echo '-- total de colunas da view (antes) --'
SELECT count(*) AS colunas
  FROM information_schema.columns
 WHERE table_schema = 'desenvolvimento'
   AND table_name   = 'vw_pa_045_etapa_estado_runtime';

\echo ''
\echo '-- privilegios atuais da view (devem sobreviver ao replace) --'
SELECT grantee, string_agg(privilege_type, ',' ORDER BY privilege_type) AS privilegios
  FROM information_schema.table_privileges
 WHERE table_schema = 'desenvolvimento'
   AND table_name   = 'vw_pa_045_etapa_estado_runtime'
 GROUP BY grantee ORDER BY grantee;

\echo ''
\echo '=== 062 PREFLIGHT concluido: nada foi escrito ==='
\echo ''
