\set ON_ERROR_STOP on
-- ============================================================
-- 060_entrada_documento_material_colunas_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 060 - Colunas do Documento de Material na Entrada
-- PREFLIGHT (SOMENTE LEITURA)
--
-- POR QUE ESTE INCREMENTO EXISTE
-- A consulta D2 do P5 (EvidenciaRecebimentoLocalServico.ConsultaD2) le
-- entrada_produto_lancamento.enviado_sap_em. No DEV essa coluna NAO existe, e o
-- Postgres responde 42703 (column does not exist). O servico trata excecao como
-- evidencia INDISPONIVEL -- fail-closed correto -- e o P5 bloqueia o envio com a
-- mensagem generica "nao foi possivel comprovar". A causa real fica invisivel,
-- porque RegistrarDiagnostico e um no-op nesta linha.
--
-- As quatro colunas sao as que REGISTRAM o resultado do movimento 101. Nunca foram
-- criadas no DEV pelo motivo obvio: este ambiente nunca enviou ao SAP. Agora vai.
--
-- ORIGEM. Extraidas do banco de homologacao do Q
-- (fuga_jales_local_homologacao_q_v1_2, schema homologacao), via pg_attribute /
-- format_type / pg_attrdef, com o papel fugapet_q_app, somente leitura e com
-- autorizacao expressa do usuario. Nada inferido do uso.
--
--   entrada_produto_lancamento.documento_material_sap            varchar(20)  NULL
--   entrada_produto_lancamento.exercicio_documento_material_sap  varchar(4)   NULL
--   entrada_produto_lancamento.enviado_sap_em                    timestamptz  NULL
--   entrada_produto_item.documento_material_item                 varchar(20)  NULL
--
-- Nenhuma tem DEFAULT nem NOT NULL no Q. Sao, portanto, aditivas e sem reescrita de
-- tabela: ADD COLUMN de coluna anulavel sem default e operacao de catalogo.
--
-- DIFERENCA DECLARADA: no Q elas ocupam as posicoes 17/18/19 e 17. No DEV, ADD COLUMN
-- anexa ao fim, entao a POSICAO ORDINAL pode diferir. Posicao de coluna nao faz parte
-- do contrato (o codigo acessa por nome); a diferenca fica aqui registrada, nao
-- escondida. Nenhum tipo, nulidade ou default diverge.
-- ============================================================

\echo ''
\echo '=== 060 PREFLIGHT ==='

SELECT current_database() AS banco, current_user AS papel, current_schema() AS schema_corrente;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '060 PREFLIGHT: alvo errado. Conectado em %.', current_database();
    END IF;
    RAISE NOTICE 'ALVO=OK';
END
$$;

-- 1. O estado que motiva o incremento: as 4 colunas estao ausentes.
\echo ''
\echo '-- colunas-alvo presentes hoje (esperado: 0 linhas) --'
SELECT table_name, column_name, data_type
  FROM information_schema.columns
 WHERE table_schema = 'desenvolvimento'
   AND ((table_name = 'entrada_produto_lancamento'
         AND column_name IN ('documento_material_sap','exercicio_documento_material_sap','enviado_sap_em'))
     OR (table_name = 'entrada_produto_item'
         AND column_name = 'documento_material_item'))
 ORDER BY table_name, column_name;

-- 2. As tabelas-alvo existem.
DO $$
DECLARE
    v_faltando text;
BEGIN
    SELECT string_agg(t.nome, ', ') INTO v_faltando
      FROM (VALUES ('entrada_produto_lancamento'), ('entrada_produto_item')) AS t(nome)
     WHERE to_regclass('desenvolvimento.' || t.nome) IS NULL;

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION '060 PREFLIGHT: tabelas ausentes: %.', v_faltando;
    END IF;
    RAISE NOTICE 'TABELAS=OK';
END
$$;

-- 3. Volume afetado. ADD COLUMN anulavel sem default nao reescreve a tabela, mas o
--    numero fica registrado para dimensionar a janela.
\echo ''
\echo '-- volume das tabelas --'
SELECT 'entrada_produto_lancamento' AS tabela, count(*) AS linhas
  FROM desenvolvimento.entrada_produto_lancamento
UNION ALL
SELECT 'entrada_produto_item', count(*) FROM desenvolvimento.entrada_produto_item;

-- 4. Posicao ordinal atual, para comparar depois com a diferenca declarada.
\echo ''
\echo '-- maior posicao ordinal hoje (as novas virao depois desta) --'
SELECT table_name, max(ordinal_position) AS ultima_posicao
  FROM information_schema.columns
 WHERE table_schema = 'desenvolvimento'
   AND table_name IN ('entrada_produto_lancamento','entrada_produto_item')
 GROUP BY table_name
 ORDER BY table_name;

\echo ''
\echo '=== 060 PREFLIGHT concluido: nada foi escrito ==='
\echo ''
