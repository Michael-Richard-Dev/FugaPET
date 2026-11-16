\set ON_ERROR_STOP on
-- ============================================================
-- 061_pa045_guard_hu_alinhar_ao_Q_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 061 - Alinhar fn_pa_045_guard_hu_pos_101 ao QAS110
-- PREFLIGHT (SOMENTE LEITURA)
--
-- POR QUE ESTE INCREMENTO EXISTE
-- Pesar uma caixa no DEV110 falhava com "Nao foi possivel registrar a caixa no
-- banco". O log local (logs/pa_box_register_120e.log) deu a causa exata:
--   Stage=SALVAR_PREVIEW
--   P0001: PA045_HU_EXIGE_101_CONFIRMADO codigo_hu_caixa=1368 status_destino=PREVIEW_HU_GERADO
--
-- O gatilho trg_045_guard_hu_pos_101 exige movimento 101 CONFIRMADO antes de a HU
-- avancar. No DEV ele dispara ja no PREVIEW_HU_GERADO -- que e o PRIMEIRO passo,
-- antes de qualquer tentativa de 101. O pipeline que rodaria o 261 e o 101 so e
-- chamado DEPOIS, na tela, e nunca chega a ser alcancado porque o erro aborta a
-- finalizacao. Resultado: o fluxo trava em si mesmo.
--
-- O codigo C# das quatro pecas do fluxo (ProcessoProdutoAcabadoForm,
-- ProdutoAcabadoHuService, ProdutoAcabadoController, ProdutoAcabadoRepositorio) e
-- IDENTICO ao do QAS110 -- verificado arquivo a arquivo, normalizando o namespace.
-- A divergencia esta SOMENTE no corpo desta funcao de banco.
--
-- ORIGEM. O corpo novo foi extraido do banco de homologacao do Q
-- (fuga_jales_local_homologacao_q_v1_2), em leitura, com o papel fugapet_q_app.
-- Nada inferido: e o texto que o proprio servidor devolve em pg_get_functiondef.
--
-- A DIFERENCA, EM UMA LINHA
--   DEV   : IF NEW.status NOT IN ('PREVIEW_HU_GERADO','AGUARDANDO_AUTORIZACAO_SAP',
--                                 'PRONTA_PARA_ENVIO','ENVIANDO_SAP')  -> guarda 4 transicoes
--   QAS110: IF NOT (OLD.status='PRONTA_PARA_ENVIO' AND NEW.status='ENVIANDO_SAP')
--                                                                      -> guarda 1 transicao
--
-- CONSEQUENCIA, DITA SEM RODEIO: este incremento deixa a guarda MENOS estrita no
-- DEV. A protecao contra "HU sem 101 confirmado" passa a valer apenas no instante
-- do envio, e nao mais ao gerar o preview. E uma reducao de escopo da guarda, nao
-- uma correcao de bug dela -- e a justificativa e alinhar o DEV ao ambiente de
-- homologacao, que e a referencia operacional.
-- ============================================================

\echo ''
\echo '=== 061 PREFLIGHT ==='

SELECT current_database() AS banco, current_user AS papel, current_schema() AS schema_corrente;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '061 PREFLIGHT: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regprocedure('desenvolvimento.fn_pa_045_guard_hu_pos_101()') IS NULL THEN
        RAISE EXCEPTION '061 PREFLIGHT: a funcao nao existe neste banco.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger t
          JOIN pg_class c ON c.oid = t.tgrelid
          JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'desenvolvimento'
           AND c.relname = 'hu_caixa'
           AND t.tgname = 'trg_045_guard_hu_pos_101'
           AND NOT t.tgisinternal) THEN
        RAISE EXCEPTION '061 PREFLIGHT: o gatilho trg_045_guard_hu_pos_101 nao existe.';
    END IF;

    RAISE NOTICE 'ALVO=OK  funcao e gatilho presentes';
END
$$;

-- 1. A condicao ATUAL guarda o preview? (esperado: sim, e por isso o fluxo trava)
\echo ''
\echo '-- a guarda atual alcanca PREVIEW_HU_GERADO? --'
SELECT pg_get_functiondef(p.oid) LIKE '%PREVIEW_HU_GERADO%' AS guarda_o_preview,
       md5(pg_get_functiondef(p.oid))                        AS hash_atual,
       length(pg_get_functiondef(p.oid))                     AS tamanho
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'desenvolvimento' AND p.proname = 'fn_pa_045_guard_hu_pos_101';

-- 2. Quem esta preso por causa disso.
\echo ''
\echo '-- caixas paradas em FINALIZADA_LOCAL (nao avancaram para o preview) --'
SELECT codigo_hu_caixa, numero_ordem_producao, material, numero_caixa, peso_liquido
  FROM desenvolvimento.hu_caixa
 WHERE status_hu_caixa = 'FINALIZADA_LOCAL'
 ORDER BY codigo_hu_caixa;

\echo ''
\echo '-- etapas dessas caixas (261/101 pendentes = nunca tentadas) --'
SELECT e.codigo_hu_caixa, e.etapa_sap, e.status_etapa
  FROM desenvolvimento.hu_caixa_etapa_sap e
  JOIN desenvolvimento.hu_caixa c ON c.codigo_hu_caixa = e.codigo_hu_caixa
 WHERE c.status_hu_caixa = 'FINALIZADA_LOCAL'
 ORDER BY e.codigo_hu_caixa, e.etapa_sap;

-- 3. Retrato dos estados, para comparar depois.
\echo ''
\echo '-- distribuicao de status das caixas --'
SELECT status_hu_caixa, count(*) AS caixas
  FROM desenvolvimento.hu_caixa GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '=== 061 PREFLIGHT concluido: nada foi escrito ==='
\echo ''
