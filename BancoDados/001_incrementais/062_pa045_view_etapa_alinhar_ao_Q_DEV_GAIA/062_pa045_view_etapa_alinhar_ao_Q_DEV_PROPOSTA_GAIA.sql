\set ON_ERROR_STOP on
-- ============================================================
-- 062_pa045_view_etapa_alinhar_ao_Q_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 062 - Expor a coluna "etapa" na vw_pa_045_etapa_estado_runtime
-- PROPOSTA (ESCRITA) - transacao unica
--
-- Reescreve a view com o corpo do QAS110. A unica diferenca em relacao ao que o
-- DEV ja tem e a ULTIMA linha do SELECT: "etapa_sap AS etapa". Todas as demais
-- colunas seguem com o mesmo nome, tipo e posicao -- o proprio CREATE OR REPLACE
-- VIEW recusaria a operacao se nao fosse assim, o que e uma guarda do Postgres a
-- nosso favor.
--
-- NAO toca na tabela hu_caixa_etapa_sap, nao altera uma linha de dado sequer e
-- nao concede nem revoga privilegio: o REPLACE preserva os grants existentes (o
-- PREFLIGHT os registra e a VALIDACAO confere que continuam la).
--
-- Leia o PREFLIGHT antes: ele mostra a contradicao "etapas=2; 261=nao; 101=nao"
-- e lista as caixas presas.
-- ============================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '062 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;
    IF to_regclass('desenvolvimento.vw_pa_045_etapa_estado_runtime') IS NULL THEN
        RAISE EXCEPTION '062 ABORTADO: a view nao existe; nada a substituir.';
    END IF;
    RAISE NOTICE 'GUARDAS=OK';
END
$$;

-- Corpo identico ao do QAS110, com o schema adaptado.
CREATE OR REPLACE VIEW desenvolvimento.vw_pa_045_etapa_estado_runtime AS
SELECT codigo_hu_caixa_etapa_sap,
       codigo_hu_caixa,
       etapa_sap,
       status_etapa,
       correlation_id,
       numero_tentativa,
       claim_obtido_em          AS lease_obtido_em,
       claim_expira_em          AS lease_expira_em,
       recovery_claim_obtido_em AS recovery_lease_obtido_em,
       recovery_claim_expira_em AS recovery_lease_expira_em,
       material_document,
       material_document_year,
       http_status,
       iniciado_em,
       confirmado_em,
       falhou_em,
       indeterminado_em,
       request_sanitizado,
       response_sanitizado,
       erro_sanitizado,
       status_reconciliacao,
       reconciliado_em,
       pode_reprocessar,
       origem_261,
       referencia_origem_261,
       usuario_operacao,
       terminal_operacao,
       criado_em,
       atualizado_em,
       etapa_sap                AS etapa
  FROM desenvolvimento.hu_caixa_etapa_sap;

-- ------------------------------------------------------------
-- GUARDA DE SAIDA
-- Nao basta "a view foi substituida". Exige que a coluna exista E que ela
-- carregue o valor certo: uma view que expusesse "etapa" sempre nula passaria
-- num teste de existencia e deixaria o pipeline exatamente tao quebrado quanto
-- antes, agora com um sintoma pior de diagnosticar.
-- ------------------------------------------------------------
DO $$
DECLARE
    v_divergentes bigint;
    v_nulas       bigint;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema='desenvolvimento'
           AND table_name='vw_pa_045_etapa_estado_runtime'
           AND column_name='etapa') THEN
        RAISE EXCEPTION '062 ABORTADO: a coluna "etapa" nao foi criada.';
    END IF;

    SELECT count(*) FILTER (WHERE etapa IS DISTINCT FROM etapa_sap),
           count(*) FILTER (WHERE etapa IS NULL)
      INTO v_divergentes, v_nulas
      FROM desenvolvimento.vw_pa_045_etapa_estado_runtime;

    IF v_divergentes > 0 THEN
        RAISE EXCEPTION '062 ABORTADO: "etapa" diverge de "etapa_sap" em % linha(s).', v_divergentes;
    END IF;

    RAISE NOTICE 'SAIDA=OK  coluna "etapa" presente e igual a "etapa_sap" (nulas=%)', v_nulas;
END
$$;

-- As demais colunas nao podem ter mudado de nome nem de posicao.
DO $$
DECLARE v_total integer;
BEGIN
    SELECT count(*) INTO v_total
      FROM information_schema.columns
     WHERE table_schema='desenvolvimento' AND table_name='vw_pa_045_etapa_estado_runtime';

    IF v_total <> 30 THEN
        RAISE EXCEPTION '062 ABORTADO: a view ficou com % colunas; esperado 30 (29 + etapa).', v_total;
    END IF;

    IF (SELECT column_name FROM information_schema.columns
         WHERE table_schema='desenvolvimento' AND table_name='vw_pa_045_etapa_estado_runtime'
           AND ordinal_position=30) <> 'etapa' THEN
        RAISE EXCEPTION '062 ABORTADO: "etapa" nao ficou na ultima posicao.';
    END IF;

    RAISE NOTICE 'FORMATO=OK  30 colunas, "etapa" por ultimo';
END
$$;

COMMIT;

\echo ''
\echo '=== 062 PROPOSTA aplicado. Rode o VALIDACAO. ==='
\echo ''
