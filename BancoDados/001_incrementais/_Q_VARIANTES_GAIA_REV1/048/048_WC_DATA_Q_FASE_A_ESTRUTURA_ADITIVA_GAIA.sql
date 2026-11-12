-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- FUGAPET — HML
-- GATE 048-WC-DATA-06 REV1
-- FASE A — ESTRUTURA ADITIVA
-- PREPARADO SOMENTE. NAO EXECUTAR SEM AUTORIZACAO HERA.
--
-- Objetivo:
--   1) tornar Plant+WorkCenter a chave funcional futura da rota;
--   2) criar perfil Resultado por ocorrencia do Work Center;
--   3) preparar operacao_resultado_definicao para reparent futuro;
--   4) NAO migrar rotas, NAO criar perfis de negocio, NAO desativar config 10.
--
-- Esperado antes da execucao:
--   configs 5..11 exatamente na baseline congelada;
--   definicao 1 = TEMPO_OPERACAO/TEMPO/MINUTO/5.000/ordem1/obrigatorio/ativo;
--   configs 9 e 10 sem definicoes/massa/vinculos;
--   nenhum Plant+WorkCenter ativo preenchido duplicado;
--   novos objetos ainda ausentes.

\set ON_ERROR_STOP on

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SET LOCAL search_path = pg_catalog, homologacao;

-- ============================================================
-- 1. PRECHECK FAIL-CLOSED — BASELINE EXATA
-- ============================================================

DO $precheck$
DECLARE
    v_total integer;
BEGIN
    -- configs 5..11 existem exatamente uma vez
    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_configuracao
    WHERE codigo_configuracao BETWEEN 5 AND 11;

    IF v_total <> 7 THEN
        RAISE EXCEPTION 'STOP: esperado 7 configs 5..11; encontrado %', v_total;
    END IF;

    -- baseline exata de cada rota
    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=5 AND operacao_sap='0050'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='CONSUMO_MATERIA_PRIMA'
          AND tela_destino='ProcessoConsumoMaterialForm'
          AND exige_operacao_anterior IS FALSE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 5 divergiu da baseline'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=6 AND operacao_sap='0060'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='CONSUMO_QUIMICOS'
          AND tela_destino='ProcessoConsumoMaterialForm'
          AND exige_operacao_anterior IS TRUE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 6 divergiu da baseline'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=7 AND operacao_sap='0070'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 7 divergiu da baseline'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=8 AND operacao_sap='0090'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 8 divergiu da baseline'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=9 AND operacao_sap='0100'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 9 divergiu da baseline'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=10 AND operacao_sap='0105'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 10 divergiu da baseline'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=11 AND operacao_sap='0140'
          AND centro='' AND tipo_ordem='' AND sequencia_sap=''
          AND suboperacao_sap='' AND centro_trabalho=''
          AND tipo_processo='SEMI_ACABADO'
          AND tela_destino='ProcessoSemiAcabadoForm'
          AND exige_operacao_anterior IS TRUE AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'STOP: config 11 divergiu da baseline'; END IF;

    -- definicao 1 exata
    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao
        WHERE codigo_definicao=1
          AND codigo_configuracao=7
          AND codigo_item='TEMPO_OPERACAO'
          AND tipo='TEMPO'
          AND medida='MINUTO'
          AND referencia=5.000
          AND ordem_exibicao=1
          AND obrigatorio IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP: definicao 1 divergiu da baseline 5.000';
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_definicao
    WHERE codigo_configuracao IN (9,10);
    IF v_total <> 0 THEN
        RAISE EXCEPTION 'STOP: configs 9/10 possuem definicoes inesperadas: %', v_total;
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_apontamento
    WHERE operacao IN ('0100','0105');
    IF v_total <> 0 THEN
        RAISE EXCEPTION 'STOP: 0100/0105 possuem apontamentos inesperados: %', v_total;
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_registro r
    JOIN homologacao.operacao_producao_apontamento a
      ON a.codigo_apontamento=r.codigo_apontamento
    WHERE a.operacao IN ('0100','0105');
    IF v_total <> 0 THEN
        RAISE EXCEPTION 'STOP: 0100/0105 possuem registros resultado inesperados: %', v_total;
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_apontamento_processo p
    JOIN homologacao.operacao_producao_apontamento a
      ON a.codigo_apontamento=p.codigo_apontamento
    WHERE a.operacao IN ('0100','0105');
    IF v_total <> 0 THEN
        RAISE EXCEPTION 'STOP: 0100/0105 possuem vinculos 048 inesperados: %', v_total;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE ativo IS TRUE
          AND btrim(centro)<>''
          AND btrim(centro_trabalho)<>''
        GROUP BY centro,centro_trabalho
        HAVING count(*)>1
    ) THEN
        RAISE EXCEPTION 'STOP: duplicidade Plant+WorkCenter ativa ja existente';
    END IF;

    -- Fase A deve ser primeira aplicacao
    IF to_regclass('homologacao.operacao_resultado_perfil') IS NOT NULL THEN
        RAISE EXCEPTION 'STOP: operacao_resultado_perfil ja existe';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='operacao_resultado_definicao'
          AND column_name='codigo_perfil_resultado'
    ) THEN
        RAISE EXCEPTION 'STOP: codigo_perfil_resultado ja existe';
    END IF;
END
$precheck$;

-- ============================================================
-- 2. ROTA CANONICA — DELTA DATA-02
-- ============================================================

ALTER TABLE homologacao.operacao_producao_configuracao
    ALTER COLUMN operacao_sap DROP NOT NULL;

ALTER TABLE homologacao.operacao_producao_configuracao
    DROP CONSTRAINT ck_operacao_config_operacao_nao_vazia;

ALTER TABLE homologacao.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_operacao_legado_valida
    CHECK (
        operacao_sap IS NULL
        OR length(btrim(operacao_sap)) > 0
    );

ALTER TABLE homologacao.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_plant_wc_consistente
    CHECK (
        (
            btrim(centro) = ''
            AND btrim(centro_trabalho) = ''
        )
        OR
        (
            btrim(centro) <> ''
            AND btrim(centro_trabalho) <> ''
            AND centro = btrim(centro)
            AND centro_trabalho = btrim(centro_trabalho)
        )
    );

CREATE UNIQUE INDEX uq_operacao_config_ativa_plant_wc
    ON homologacao.operacao_producao_configuracao
       (centro, centro_trabalho)
    WHERE ativo IS TRUE
      AND btrim(centro) <> ''
      AND btrim(centro_trabalho) <> '';

-- ============================================================
-- 3. PERFIL RESULTADO POR OCORRENCIA
-- ============================================================

CREATE TABLE homologacao.operacao_resultado_perfil (
    codigo_perfil_resultado       bigint GENERATED BY DEFAULT AS IDENTITY,
    codigo_configuracao_rota      bigint NOT NULL,
    ordem_ocorrencia_workcenter   integer NOT NULL,
    ativo                         boolean NOT NULL DEFAULT true,
    criado_em                     timestamptz NOT NULL DEFAULT now(),
    criado_por                    varchar(80) NOT NULL DEFAULT current_user,
    alterado_em                   timestamptz NULL,
    alterado_por                  varchar(80) NULL,

    CONSTRAINT operacao_resultado_perfil_pkey
        PRIMARY KEY (codigo_perfil_resultado),

    CONSTRAINT fk_resultado_perfil_configuracao_rota
        FOREIGN KEY (codigo_configuracao_rota)
        REFERENCES homologacao.operacao_producao_configuracao(codigo_configuracao)
        ON DELETE RESTRICT,

    CONSTRAINT ck_resultado_perfil_ordem_ocorrencia
        CHECK (ordem_ocorrencia_workcenter > 0)
);

CREATE UNIQUE INDEX uq_resultado_perfil_ativo_ocorrencia
    ON homologacao.operacao_resultado_perfil
       (codigo_configuracao_rota, ordem_ocorrencia_workcenter)
    WHERE ativo IS TRUE;

CREATE INDEX ix_resultado_perfil_rota_ativo
    ON homologacao.operacao_resultado_perfil
       (codigo_configuracao_rota, ativo);

GRANT SELECT ON homologacao.operacao_resultado_perfil
TO fugapet_q_app;

-- ============================================================
-- 4. DEFINICAO — PREPARACAO ADITIVA
-- ============================================================

ALTER TABLE homologacao.operacao_resultado_definicao
    ADD COLUMN codigo_perfil_resultado bigint NULL;

ALTER TABLE homologacao.operacao_resultado_definicao
    ADD CONSTRAINT fk_resultado_definicao_perfil
    FOREIGN KEY (codigo_perfil_resultado)
    REFERENCES homologacao.operacao_resultado_perfil(codigo_perfil_resultado)
    ON DELETE RESTRICT
    NOT VALID;

ALTER TABLE homologacao.operacao_resultado_definicao
    VALIDATE CONSTRAINT fk_resultado_definicao_perfil;

CREATE UNIQUE INDEX uq_resultado_definicao_ativa_perfil_item
    ON homologacao.operacao_resultado_definicao
       (codigo_perfil_resultado, codigo_item)
    WHERE ativo IS TRUE
      AND codigo_perfil_resultado IS NOT NULL;

CREATE UNIQUE INDEX uq_resultado_definicao_ativa_perfil_ordem
    ON homologacao.operacao_resultado_definicao
       (codigo_perfil_resultado, ordem_exibicao)
    WHERE ativo IS TRUE
      AND codigo_perfil_resultado IS NOT NULL;

CREATE INDEX ix_resultado_definicao_perfil_ativo
    ON homologacao.operacao_resultado_definicao
       (codigo_perfil_resultado, ativo);

-- As FKs/uniques antigos por codigo_configuracao permanecem.
-- Nenhum objeto antigo e removido nesta fase.

-- ============================================================
-- 5. POSTCHECK FAIL-CLOSED
-- ============================================================

DO $postcheck$
DECLARE
    v_total integer;
BEGIN
    IF to_regclass('homologacao.operacao_resultado_perfil') IS NULL THEN
        RAISE EXCEPTION 'POSTCHECK: tabela perfil ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='operacao_resultado_definicao'
          AND column_name='codigo_perfil_resultado'
          AND is_nullable='YES'
    ) THEN
        RAISE EXCEPTION 'POSTCHECK: codigo_perfil_resultado ausente ou nao nullable';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid='homologacao.operacao_resultado_definicao'::regclass
          AND conname='fk_resultado_definicao_perfil'
          AND convalidated IS TRUE
    ) THEN
        RAISE EXCEPTION 'POSTCHECK: FK definicao->perfil nao validada';
    END IF;

    IF to_regclass('homologacao.uq_operacao_config_ativa_plant_wc') IS NULL THEN
        RAISE EXCEPTION 'POSTCHECK: unique Plant+WorkCenter ausente';
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_perfil;
    IF v_total <> 0 THEN
        RAISE EXCEPTION 'POSTCHECK: Fase A criou perfis indevidamente: %', v_total;
    END IF;

    -- nenhuma rota migrada/desativada
    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao BETWEEN 5 AND 11
          AND (centro<>'' OR centro_trabalho<>'' OR ativo IS NOT TRUE)
    ) THEN
        RAISE EXCEPTION 'POSTCHECK: Fase A alterou rota 5..11';
    END IF;

    -- definicao 1 intacta e ainda sem perfil
    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao
        WHERE codigo_definicao=1
          AND codigo_configuracao=7
          AND codigo_perfil_resultado IS NULL
          AND codigo_item='TEMPO_OPERACAO'
          AND tipo='TEMPO'
          AND medida='MINUTO'
          AND referencia=5.000
          AND ordem_exibicao=1
          AND obrigatorio IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'POSTCHECK: definicao 1 foi alterada';
    END IF;
END
$postcheck$;

COMMIT;

-- Evidencia esperada apos execucao futura:
SELECT 'GATE_048_WC_DATA_06_FASE_A=COMMIT_OK';
SELECT 'ROTAS_MIGRADAS=0';
SELECT 'PERFIS_CRIADOS=0';
SELECT 'CONFIG10_DESATIVADA=NAO';
SELECT 'DEFINICAO_1_REPARENTADA=NAO';
