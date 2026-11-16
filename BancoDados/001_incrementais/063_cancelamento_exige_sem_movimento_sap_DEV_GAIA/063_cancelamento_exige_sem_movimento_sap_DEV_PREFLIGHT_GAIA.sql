\set ON_ERROR_STOP on
-- ============================================================
-- 063_cancelamento_exige_sem_movimento_sap_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 063 - fn_hu_caixa_cancelar recusa caixa com movimento SAP confirmado
-- PREFLIGHT (SOMENTE LEITURA)
--
-- POR QUE ESTE INCREMENTO EXISTE
-- Em 16/11/2026, no DEV110, a caixa 1398 da OP 1002762:
--   16:50  etapa 261 -> HTTP 201, documento de material 4900019863/2026 CONFIRMADO
--   16:50  etapa 101 -> HTTP 400 (12/007, vida util remanescente insuficiente)
--   depois  a caixa foi CANCELADA pelo operador
--
-- O SAP consumiu materia-prima; o nosso lado ficou sem caixa correspondente. A
-- partir dai TODA caixa nova da OP passou a bloquear em
-- "BLOCK_RECONCILIATION_REQUIRED", porque o alocador compara o WithdrawnQuantity
-- fresco do SAP com o consumo que o banco tem como confirmado -- e o banco conta
-- zero, ja que so conta caixas CONFIRMADA_SAP.
--
-- A guarda de reconciliacao fez o certo. O que faltou foi IMPEDIR a criacao da
-- divergencia: a fn_hu_caixa_cancelar aceita cancelar a partir de ERRO_SAP e so
-- protege contra HU ja criada (handling_unit_external_id IS NULL). Ela NAO olha
-- as etapas SAP, entao um 261 confirmado nao impede o cancelamento.
--
-- O QUE MUDA
-- Acrescenta UMA condicao ao WHERE do UPDATE: nenhuma etapa da caixa pode estar
-- em CONFIRMADO_SAP. Vale para 261 e para 101 -- os dois representam lancamento
-- real no SAP, e os dois exigem ESTORNO antes de a caixa deixar de existir
-- logicamente aqui.
--
-- ESCOPO E LIMITE, DITOS SEM RODEIO:
--   * A condicao entra no proprio UPDATE, nao em verificacao previa: evita a
--     corrida entre "conferi" e "cancelei".
--   * A funcao continua devolvendo false (nao lanca excecao). Lancar faria a tela
--     cair no catch generico "Tente novamente", que esconde o motivo e convida a
--     repetir. O texto explicativo e responsabilidade da camada C#.
--   * Caixas JA canceladas, como a 1398, NAO sao tocadas. Este incremento previne
--     casos futuros; nao conserta a divergencia existente, que depende do estorno
--     do 4900019863/2026 no SAP.
--   * Nao mexe em hu_caixa_etapa_sap, nao altera dado e nao mexe em gatilho.
-- ============================================================

\echo ''
\echo '=== 063 PREFLIGHT ==='

SELECT current_database() AS banco, current_user AS papel, current_schema() AS schema_corrente;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '063 PREFLIGHT: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regprocedure('desenvolvimento.fn_hu_caixa_cancelar(bigint,bigint,text,text)') IS NULL THEN
        RAISE EXCEPTION '063 PREFLIGHT: a funcao nao existe neste banco.';
    END IF;

    RAISE NOTICE 'ALVO=OK  funcao presente';
END
$$;

-- 1. A funcao atual ignora as etapas SAP? (esperado: sim -- e a lacuna)
\echo ''
\echo '-- a funcao atual consulta hu_caixa_etapa_sap? --'
SELECT pg_get_functiondef(p.oid) LIKE '%hu_caixa_etapa_sap%' AS olha_as_etapas,
       pg_get_functiondef(p.oid) LIKE '%handling_unit_external_id IS NULL%' AS protege_hu,
       md5(pg_get_functiondef(p.oid)) AS hash_atual
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'desenvolvimento' AND p.proname = 'fn_hu_caixa_cancelar';

-- 2. O estrago ja feito: caixas canceladas que tinham movimento SAP confirmado.
\echo ''
\echo '-- caixas CANCELADAS com etapa confirmada (divergencia ja existente) --'
SELECT c.codigo_hu_caixa, c.numero_ordem_producao, c.numero_caixa, c.status_hu_caixa,
       e.etapa_sap, e.material_document, e.material_document_year
  FROM desenvolvimento.hu_caixa c
  JOIN desenvolvimento.hu_caixa_etapa_sap e ON e.codigo_hu_caixa = c.codigo_hu_caixa
 WHERE c.status_hu_caixa = 'CANCELADA'
   AND e.status_etapa = 'CONFIRMADO_SAP'
 ORDER BY c.codigo_hu_caixa, e.etapa_sap;

-- 3. Caixas VIVAS que a nova guarda passaria a proteger.
\echo ''
\echo '-- caixas nao canceladas com etapa confirmada (serao protegidas) --'
SELECT c.codigo_hu_caixa, c.status_hu_caixa,
       string_agg(e.etapa_sap || '=' || e.status_etapa, ' | ' ORDER BY e.etapa_sap) AS etapas
  FROM desenvolvimento.hu_caixa c
  JOIN desenvolvimento.hu_caixa_etapa_sap e ON e.codigo_hu_caixa = c.codigo_hu_caixa
 WHERE c.status_hu_caixa <> 'CANCELADA'
   AND EXISTS (SELECT 1 FROM desenvolvimento.hu_caixa_etapa_sap x
                WHERE x.codigo_hu_caixa = c.codigo_hu_caixa AND x.status_etapa = 'CONFIRMADO_SAP')
 GROUP BY 1,2 ORDER BY 1;

\echo ''
\echo '=== 063 PREFLIGHT concluido: nada foi escrito ==='
\echo ''
