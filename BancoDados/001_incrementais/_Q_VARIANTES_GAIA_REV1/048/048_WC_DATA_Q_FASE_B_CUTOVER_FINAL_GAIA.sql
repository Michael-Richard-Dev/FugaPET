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
-- GATE 048-WC-DATA-06-FASE-B-REV3
-- CUTOVER FINAL WORK CENTER + PERFIL RESULTADO
--
-- MODALIDADE DESTE ARTEFATO:
--   PREPARADO SOMENTE.
--   NAO EXECUTAR SEM AUTORIZACAO EXPLICITA DE HERA.
--
-- BASE:
--   Fase A ja instalada.
--
-- CONTRATO:
--   ROTA       = Plant + WorkCenter
--   PERFIL     = codigo_configuracao_rota + ordem_ocorrencia_workcenter
--   DEFINICAO  = codigo_perfil_resultado -> operacao_resultado_definicao
--
-- REV2:
--   Corrige a REV1 exigindo, ANTES do DROP dos UNIQUEs legados,
--   que TODAS as definicoes ATIVAS sobreviventes estejam com
--   codigo_perfil_resultado preenchido.
--
-- REV3:
--   Preserva integralmente a logica funcional da REV2 e adiciona
--   blindagem concorrencial da massa operacional auditada.
--   As quatro tabelas operacionais recebem SHARE ROW EXCLUSIVE
--   antes do primeiro snapshot/fingerprint, sob lock_timeout=5s.
--
-- NAO TRATA:
--   semi_acabado_consumo
--   semi_acabado_consumo_item
--   registrar_consumo_semi_acabado
--   SAP

\set ON_ERROR_STOP on

BEGIN ISOLATION LEVEL REPEATABLE READ;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SET LOCAL search_path = pg_catalog, homologacao;

SELECT '============================================================';
SELECT 'FUGAPET|GATE=048-WC-DATA-06-FASE-B-REV3';
SELECT 'CUTOVER_FINAL_WORK_CENTER_PERFIL_RESULTADO';
SELECT '============================================================';
SELECT concat('TRANSACTION_ISOLATION=',current_setting('transaction_isolation'));

-- Evita cutover concorrente sobre os objetos de configuracao.
LOCK TABLE homologacao.operacao_producao_configuracao
    IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE homologacao.operacao_resultado_perfil
    IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE homologacao.operacao_resultado_definicao
    IN SHARE ROW EXCLUSIVE MODE;

-- Bloqueia writers concorrentes sobre toda a massa operacional
-- auditada pelos fingerprints/prechecks durante o cutover.
-- SHARE ROW EXCLUSIVE permite leitura (ACCESS SHARE), mas conflita
-- com ROW EXCLUSIVE adquirido por INSERT/UPDATE/DELETE.
LOCK TABLE homologacao.operacao_producao_apontamento
    IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE homologacao.operacao_producao_apontamento_processo
    IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE homologacao.operacao_resultado_registro
    IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE homologacao.operacao_resultado_registro_item
    IN SHARE ROW EXCLUSIVE MODE;

-- ============================================================
-- 1. SNAPSHOT/FINGERPRINT PRE-CUTOVER
-- ============================================================

CREATE TEMP TABLE pg_temp.gate_048_wc_data06_rev2_pre (
    chave text PRIMARY KEY,
    valor text NOT NULL
) ON COMMIT DROP;

INSERT INTO pg_temp.gate_048_wc_data06_rev2_pre(chave,valor)
WITH a AS (
    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(to_jsonb(t)::text,'|' ORDER BY codigo_apontamento),
            ''
        ))
    ) AS v
    FROM homologacao.operacao_producao_apontamento t
)
SELECT 'MASSA_APONTAMENTO',v FROM a;

INSERT INTO pg_temp.gate_048_wc_data06_rev2_pre(chave,valor)
WITH p AS (
    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(
                to_jsonb(t)::text,
                '|' ORDER BY codigo_apontamento_processo
            ),
            ''
        ))
    ) AS v
    FROM homologacao.operacao_producao_apontamento_processo t
)
SELECT 'MASSA_VINCULO_048',v FROM p;

INSERT INTO pg_temp.gate_048_wc_data06_rev2_pre(chave,valor)
WITH r AS (
    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(to_jsonb(t)::text,'|' ORDER BY codigo_resultado),
            ''
        ))
    ) AS v
    FROM homologacao.operacao_resultado_registro t
)
SELECT 'MASSA_RESULTADO_REGISTRO',v FROM r;

INSERT INTO pg_temp.gate_048_wc_data06_rev2_pre(chave,valor)
WITH i AS (
    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(
                to_jsonb(t)::text,
                '|' ORDER BY codigo_resultado_item
            ),
            ''
        ))
    ) AS v
    FROM homologacao.operacao_resultado_registro_item t
)
SELECT 'MASSA_RESULTADO_ITEM',v FROM i;

INSERT INTO pg_temp.gate_048_wc_data06_rev2_pre(chave,valor)
SELECT
    'DEFINICOES_TOTAL',
    count(*)::text
FROM homologacao.operacao_resultado_definicao;

INSERT INTO pg_temp.gate_048_wc_data06_rev2_pre(chave,valor)
SELECT
    'CONFIGS_FORA_5_11',
    concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(to_jsonb(t)::text,'|' ORDER BY codigo_configuracao),
            ''
        ))
    )
FROM homologacao.operacao_producao_configuracao t
WHERE codigo_configuracao NOT BETWEEN 5 AND 11;

SELECT concat(
    'FINGERPRINT_PRE|',chave,'=',valor
)
FROM pg_temp.gate_048_wc_data06_rev2_pre
ORDER BY chave;

-- ============================================================
-- 2. PRECHECK FASE A + BASELINE PRE-CUTOVER
-- ============================================================

DO $precheck$
DECLARE
    v_total integer;
BEGIN
    -- --------------------------------------------------------
    -- 2.1 Estrutura Fase A
    -- --------------------------------------------------------

    IF to_regclass('homologacao.operacao_resultado_perfil') IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: operacao_resultado_perfil ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='operacao_resultado_definicao'
          AND column_name='codigo_perfil_resultado'
          AND data_type='bigint'
          AND is_nullable='YES'
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: codigo_perfil_resultado ausente/divergente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='operacao_producao_configuracao'
          AND column_name='operacao_sap'
          AND is_nullable='YES'
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: operacao_sap nao esta nullable';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_producao_configuracao'::regclass
          AND conname='ck_operacao_config_operacao_nao_vazia'
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: check antigo operacao_sap ainda existe';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_producao_configuracao'::regclass
          AND conname='ck_operacao_config_operacao_legado_valida'
          AND contype='c'
          AND convalidated IS TRUE
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: check legado operacao_sap ausente/invalido';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_producao_configuracao'::regclass
          AND conname='ck_operacao_config_plant_wc_consistente'
          AND contype='c'
          AND convalidated IS TRUE
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: check Plant+WorkCenter ausente/invalido';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_resultado_definicao'::regclass
          AND conname='fk_resultado_definicao_perfil'
          AND contype='f'
          AND convalidated IS TRUE
          AND confrelid=
              'homologacao.operacao_resultado_perfil'::regclass
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: FK definicao->perfil ausente/invalida';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_resultado_perfil'::regclass
          AND conname='fk_resultado_perfil_configuracao_rota'
          AND contype='f'
          AND convalidated IS TRUE
          AND confrelid=
              'homologacao.operacao_producao_configuracao'::regclass
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: FK perfil->rota ausente/invalida';
    END IF;

    IF to_regclass(
        'homologacao.uq_operacao_config_ativa_plant_wc'
    ) IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: UNIQUE Plant+WorkCenter ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_perfil_ativo_ocorrencia'
    ) IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: UNIQUE perfil rota+ocorrencia ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_item'
    ) IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: UNIQUE definicao perfil+item ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_ordem'
    ) IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: UNIQUE definicao perfil+ordem ausente';
    END IF;

    -- Os UNIQUEs legados DEVEM existir antes do cutover REV2.
    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_item'
    ) IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: UNIQUE legado item ja ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_ordem'
    ) IS NULL THEN
        RAISE EXCEPTION
            'STOP PRECHECK_FASE_A: UNIQUE legado ordem ja ausente';
    END IF;

    -- Nenhum perfil funcional preexistente.
    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_perfil;

    IF v_total <> 0 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: PERFIS_FUNCIONAIS esperado=0 encontrado=%',
            v_total;
    END IF;

    -- --------------------------------------------------------
    -- 2.2 Configs 5..11 exatamente na baseline pre-cutover
    -- --------------------------------------------------------

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_configuracao
    WHERE codigo_configuracao BETWEEN 5 AND 11;

    IF v_total <> 7 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: configs 5..11 esperado=7 encontrado=%',
            v_total;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=5
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0050'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='CONSUMO_MATERIA_PRIMA'
          AND tela_destino='ProcessoConsumoMaterialForm'
          AND exige_operacao_anterior IS FALSE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config5 divergiu';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=6
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0060'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='CONSUMO_QUIMICOS'
          AND tela_destino='ProcessoConsumoMaterialForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config6 divergiu';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=7
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0070'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config7 divergiu';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=8
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0090'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config8 divergiu';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=9
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0100'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config9 divergiu';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=10
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0105'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config10 divergiu';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=11
          AND centro=''
          AND tipo_ordem=''
          AND sequencia_sap=''
          AND operacao_sap='0140'
          AND suboperacao_sap=''
          AND centro_trabalho=''
          AND tipo_processo='SEMI_ACABADO'
          AND tela_destino='ProcessoSemiAcabadoForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'STOP PRECHECK: config11 divergiu';
    END IF;

    -- Nenhum Plant+WorkCenter ja preenchido em qualquer config.
    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_configuracao
    WHERE btrim(centro)<>''
       OR btrim(centro_trabalho)<>'';

    IF v_total <> 0 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: Plant+WorkCenter inesperado total=%',
            v_total;
    END IF;

    -- Nenhuma duplicidade Plant+WorkCenter ativa.
    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE ativo IS TRUE
          AND btrim(centro)<>''
          AND btrim(centro_trabalho)<>''
        GROUP BY centro,centro_trabalho
        HAVING count(*)>1
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK: duplicidade Plant+WorkCenter ativa';
    END IF;

    -- --------------------------------------------------------
    -- 2.3 Config10 sem massa / defs / vinculos
    -- --------------------------------------------------------

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_definicao
    WHERE codigo_configuracao=10;

    IF v_total <> 0 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: config10 definicoes=% esperado=0',
            v_total;
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_apontamento
    WHERE operacao='0105';

    IF v_total <> 0 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: 0105 apontamentos=% esperado=0',
            v_total;
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_registro r
    JOIN homologacao.operacao_producao_apontamento a
      ON a.codigo_apontamento=r.codigo_apontamento
    WHERE a.operacao='0105';

    IF v_total <> 0 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: 0105 registros resultado=% esperado=0',
            v_total;
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_apontamento_processo p
    JOIN homologacao.operacao_producao_apontamento a
      ON a.codigo_apontamento=p.codigo_apontamento
    WHERE a.operacao='0105';

    IF v_total <> 0 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: 0105 vinculos048=% esperado=0',
            v_total;
    END IF;

    -- --------------------------------------------------------
    -- 2.4 Definicoes: somente a definicao ativa congelada
    -- --------------------------------------------------------

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
        RAISE EXCEPTION
            'STOP PRECHECK: definicao1 divergiu da baseline 5.000';
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_definicao
    WHERE ativo IS TRUE;

    IF v_total <> 1 THEN
        RAISE EXCEPTION
            'STOP PRECHECK: definicoes ativas esperado=1 encontrado=%',
            v_total;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao
        WHERE ativo IS TRUE
          AND codigo_definicao<>1
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK: definicao ativa inesperada';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao
        WHERE codigo_configuracao IN (8,9,10)
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION
            'STOP PRECHECK: definicao funcional WC9/WC10 foi criada sem autorizacao';
    END IF;
END
$precheck$;

SELECT 'PRECHECK_FASE_A=OK';
SELECT 'PRECHECK_BASELINE_CUTOVER=OK';
SELECT 'PERFIS_FUNCIONAIS_PRE=0';
SELECT 'DEFINICOES_ATIVAS_ESPERADAS_PRE=1';

-- ============================================================
-- 3. CUTOVER — ROTAS CANONICAS
-- ============================================================

DO $rotas$
DECLARE
    v_count integer;
BEGIN
    UPDATE homologacao.operacao_producao_configuracao
       SET centro='3007',
           centro_trabalho='5'
     WHERE codigo_configuracao=5
       AND centro=''
       AND centro_trabalho=''
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CUTOVER config5 row_count=% esperado=1',v_count;
    END IF;

    UPDATE homologacao.operacao_producao_configuracao
       SET centro='3007',
           centro_trabalho='6'
     WHERE codigo_configuracao=6
       AND centro=''
       AND centro_trabalho=''
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CUTOVER config6 row_count=% esperado=1',v_count;
    END IF;

    UPDATE homologacao.operacao_producao_configuracao
       SET centro='3007',
           centro_trabalho='7'
     WHERE codigo_configuracao=7
       AND centro=''
       AND centro_trabalho=''
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CUTOVER config7 row_count=% esperado=1',v_count;
    END IF;

    UPDATE homologacao.operacao_producao_configuracao
       SET centro='3007',
           centro_trabalho='9'
     WHERE codigo_configuracao=8
       AND centro=''
       AND centro_trabalho=''
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CUTOVER config8 row_count=% esperado=1',v_count;
    END IF;

    UPDATE homologacao.operacao_producao_configuracao
       SET centro='3007',
           centro_trabalho='10'
     WHERE codigo_configuracao=9
       AND centro=''
       AND centro_trabalho=''
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CUTOVER config9 row_count=% esperado=1',v_count;
    END IF;

    UPDATE homologacao.operacao_producao_configuracao
       SET centro='3007',
           centro_trabalho='100'
     WHERE codigo_configuracao=11
       AND centro=''
       AND centro_trabalho=''
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count<>1 THEN
        RAISE EXCEPTION 'CUTOVER config11 row_count=% esperado=1',v_count;
    END IF;
END
$rotas$;

SELECT 'ROTAS_CUTOVER_UPDATE_COUNT=6';

-- ============================================================
-- 4. CRIAR EXATAMENTE 4 PERFIS FUNCIONAIS
-- ============================================================

INSERT INTO homologacao.operacao_resultado_perfil
    (codigo_configuracao_rota, ordem_ocorrencia_workcenter)
VALUES
    (7,1),
    (8,1),
    (9,1),
    (9,2);

DO $perfil_check$
DECLARE
    v_total integer;
BEGIN
    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_perfil;

    IF v_total<>4 THEN
        RAISE EXCEPTION
            'CUTOVER perfis total=% esperado=4',
            v_total;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_perfil
        WHERE codigo_configuracao_rota=7
          AND ordem_ocorrencia_workcenter=1
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'CUTOVER perfil config7 occ1 ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_perfil
        WHERE codigo_configuracao_rota=8
          AND ordem_ocorrencia_workcenter=1
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'CUTOVER perfil config8 occ1 ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_perfil
        WHERE codigo_configuracao_rota=9
          AND ordem_ocorrencia_workcenter=1
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'CUTOVER perfil config9 occ1 ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_perfil
        WHERE codigo_configuracao_rota=9
          AND ordem_ocorrencia_workcenter=2
          AND ativo IS TRUE
    ) THEN
        RAISE EXCEPTION 'CUTOVER perfil config9 occ2 ausente';
    END IF;
END
$perfil_check$;

SELECT 'PERFIS_FUNCIONAIS_CRIADOS=4';

-- ============================================================
-- 5. REPARENT SOMENTE DA DEFINICAO 1
--    codigo_configuracao legado permanece = 7.
-- ============================================================

DO $reparent$
DECLARE
    v_perfil bigint;
    v_count integer;
BEGIN
    SELECT codigo_perfil_resultado
      INTO STRICT v_perfil
    FROM homologacao.operacao_resultado_perfil
    WHERE codigo_configuracao_rota=7
      AND ordem_ocorrencia_workcenter=1
      AND ativo IS TRUE;

    UPDATE homologacao.operacao_resultado_definicao
       SET codigo_perfil_resultado=v_perfil
     WHERE codigo_definicao=1
       AND codigo_configuracao=7
       AND codigo_perfil_resultado IS NULL
       AND codigo_item='TEMPO_OPERACAO'
       AND tipo='TEMPO'
       AND medida='MINUTO'
       AND referencia=5.000
       AND ordem_exibicao=1
       AND obrigatorio IS TRUE
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    IF v_count<>1 THEN
        RAISE EXCEPTION
            'CUTOVER reparent definicao1 row_count=% esperado=1',
            v_count;
    END IF;
END
$reparent$;

SELECT 'DEFINICAO_1_REPARENTADA=SIM';
SELECT 'DEFINICAO_1_REFERENCIA_ESPERADA=5.000';

-- ============================================================
-- 6. REV2 CRITICO
--    FAIL-CLOSED ANTES DE REMOVER UNIQUEs LEGADOS
-- ============================================================

DO $pre_drop$
DECLARE
    v_sem_perfil integer;
    v_total_defs bigint;
    v_total_pre bigint;
BEGIN
    SELECT count(*) INTO v_sem_perfil
    FROM homologacao.operacao_resultado_definicao
    WHERE ativo IS TRUE
      AND codigo_perfil_resultado IS NULL;

    IF v_sem_perfil<>0 THEN
        RAISE EXCEPTION
            'STOP PRE-DROP: DEFINICOES_ATIVAS_SEM_PERFIL=% esperado=0',
            v_sem_perfil;
    END IF;

    SELECT count(*) INTO v_total_defs
    FROM homologacao.operacao_resultado_definicao;

    SELECT valor::bigint INTO STRICT v_total_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='DEFINICOES_TOTAL';

    IF v_total_defs<>v_total_pre THEN
        RAISE EXCEPTION
            'STOP PRE-DROP: total definicoes mudou pre=% pos=%',
            v_total_pre,
            v_total_defs;
    END IF;

    -- A definicao 1 continua integralmente preservada.
    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao d
        JOIN homologacao.operacao_resultado_perfil p
          ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
        WHERE d.codigo_definicao=1
          AND d.codigo_configuracao=7
          AND p.codigo_configuracao_rota=7
          AND p.ordem_ocorrencia_workcenter=1
          AND p.ativo IS TRUE
          AND d.codigo_item='TEMPO_OPERACAO'
          AND d.tipo='TEMPO'
          AND d.medida='MINUTO'
          AND d.referencia=5.000
          AND d.ordem_exibicao=1
          AND d.obrigatorio IS TRUE
          AND d.ativo IS TRUE
    ) THEN
        RAISE EXCEPTION
            'STOP PRE-DROP: definicao1 nao esta integralmente preservada';
    END IF;

    -- Nenhuma definicao funcional foi inventada para WC9/WC10.
    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao d
        JOIN homologacao.operacao_resultado_perfil p
          ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
        WHERE p.ativo IS TRUE
          AND (
               (p.codigo_configuracao_rota=8
                AND p.ordem_ocorrencia_workcenter=1)
            OR (p.codigo_configuracao_rota=9
                AND p.ordem_ocorrencia_workcenter IN (1,2))
          )
    ) THEN
        RAISE EXCEPTION
            'STOP PRE-DROP: definicao WC9/WC10 criada sem autorizacao';
    END IF;

    -- Novos UNIQUEs por perfil obrigatoriamente continuam presentes.
    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_item'
    ) IS NULL
       OR to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_ordem'
    ) IS NULL
    THEN
        RAISE EXCEPTION
            'STOP PRE-DROP: UNIQUE novo por perfil ausente';
    END IF;
END
$pre_drop$;

SELECT 'DEFINICOES_ATIVAS_SEM_PERFIL_PRE_DROP=0';
SELECT 'PRE_DROP_UNIQUES_LEGADOS=AUTORIZADO';

-- ============================================================
-- 7. REMOVER APENAS UNIQUEs LEGADOS POR codigo_configuracao
-- ============================================================

DROP INDEX homologacao.uq_resultado_definicao_ativa_item;
DROP INDEX homologacao.uq_resultado_definicao_ativa_ordem;

SELECT 'UNIQUE_LEGADO_ITEM_TRATAMENTO=REMOVIDO_NO_CUTOVER';
SELECT 'UNIQUE_LEGADO_ORDEM_TRATAMENTO=REMOVIDO_NO_CUTOVER';

-- ============================================================
-- 8. CONFIG10 — LEGADO INATIVO, SEM MAPEAMENTO 3007+10
-- ============================================================

DO $config10$
DECLARE
    v_count integer;
BEGIN
    -- Revalida imediatamente antes da desativacao.
    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao
        WHERE codigo_configuracao=10
    ) THEN
        RAISE EXCEPTION
            'STOP config10: definicao surgiu durante cutover';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_apontamento
        WHERE operacao='0105'
    ) THEN
        RAISE EXCEPTION
            'STOP config10: apontamento 0105 surgiu durante cutover';
    END IF;

    UPDATE homologacao.operacao_producao_configuracao
       SET ativo=false
     WHERE codigo_configuracao=10
       AND operacao_sap='0105'
       AND centro=''
       AND centro_trabalho=''
       AND tipo_processo='RESULTADO_APONTAMENTO'
       AND tela_destino='ProcessoResultadoApontamentoForm'
       AND exige_operacao_anterior IS TRUE
       AND ativo IS TRUE;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    IF v_count<>1 THEN
        RAISE EXCEPTION
            'CUTOVER config10 desativacao row_count=% esperado=1',
            v_count;
    END IF;
END
$config10$;

SELECT 'CONFIG10_CUTOVER=LEGADO_INATIVO_SEM_MAPEAMENTO_3007_10';

-- ============================================================
-- 9. POSTCHECK FINAL FAIL-CLOSED — AINDA ANTES DO COMMIT
-- ============================================================

DO $postcheck$
DECLARE
    v_total integer;
    v_pre text;
    v_post text;
BEGIN
    -- --------------------------------------------------------
    -- 9.1 Exatamente 6 rotas canonicas ativas e IDs exatos
    -- --------------------------------------------------------

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_producao_configuracao
    WHERE ativo IS TRUE
      AND (centro,centro_trabalho) IN (
          ('3007','5'),
          ('3007','6'),
          ('3007','7'),
          ('3007','9'),
          ('3007','10'),
          ('3007','100')
      );

    IF v_total<>6 THEN
        RAISE EXCEPTION
            'POSTCHECK rotas canonicas ativas=% esperado=6',
            v_total;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=5
          AND centro='3007'
          AND centro_trabalho='5'
          AND tipo_processo='CONSUMO_MATERIA_PRIMA'
          AND tela_destino='ProcessoConsumoMaterialForm'
          AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'POSTCHECK config5'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=6
          AND centro='3007'
          AND centro_trabalho='6'
          AND tipo_processo='CONSUMO_QUIMICOS'
          AND tela_destino='ProcessoConsumoMaterialForm'
          AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'POSTCHECK config6'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=7
          AND centro='3007'
          AND centro_trabalho='7'
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'POSTCHECK config7'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=8
          AND centro='3007'
          AND centro_trabalho='9'
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'POSTCHECK config8'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=9
          AND centro='3007'
          AND centro_trabalho='10'
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'POSTCHECK config9'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=11
          AND centro='3007'
          AND centro_trabalho='100'
          AND tipo_processo='SEMI_ACABADO'
          AND tela_destino='ProcessoSemiAcabadoForm'
          AND ativo IS TRUE
    ) THEN RAISE EXCEPTION 'POSTCHECK config11'; END IF;

    -- Nenhuma config ativa duplicada por Plant+WorkCenter.
    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE ativo IS TRUE
          AND btrim(centro)<>''
          AND btrim(centro_trabalho)<>''
        GROUP BY centro,centro_trabalho
        HAVING count(*)>1
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK duplicidade Plant+WorkCenter';
    END IF;

    -- Config10 preservada fisicamente como legado inativo.
    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_producao_configuracao
        WHERE codigo_configuracao=10
          AND operacao_sap='0105'
          AND centro=''
          AND centro_trabalho=''
          AND tipo_processo='RESULTADO_APONTAMENTO'
          AND tela_destino='ProcessoResultadoApontamentoForm'
          AND exige_operacao_anterior IS TRUE
          AND ativo IS FALSE
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK config10 nao ficou legado inativo esperado';
    END IF;

    -- --------------------------------------------------------
    -- 9.2 Exatamente quatro perfis ativos previstos
    -- --------------------------------------------------------

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_perfil
    WHERE ativo IS TRUE;

    IF v_total<>4 THEN
        RAISE EXCEPTION
            'POSTCHECK perfis ativos=% esperado=4',
            v_total;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_perfil
        WHERE ativo IS TRUE
          AND (codigo_configuracao_rota,ordem_ocorrencia_workcenter)
              NOT IN ((7,1),(8,1),(9,1),(9,2))
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK perfil ativo extra';
    END IF;

    -- --------------------------------------------------------
    -- 9.3 Definicao 1 preservada e associada config7/occ1
    -- --------------------------------------------------------

    IF NOT EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao d
        JOIN homologacao.operacao_resultado_perfil p
          ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
        WHERE d.codigo_definicao=1
          AND d.codigo_configuracao=7
          AND p.codigo_configuracao_rota=7
          AND p.ordem_ocorrencia_workcenter=1
          AND p.ativo IS TRUE
          AND d.codigo_item='TEMPO_OPERACAO'
          AND d.tipo='TEMPO'
          AND d.medida='MINUTO'
          AND d.referencia=5.000
          AND d.ordem_exibicao=1
          AND d.obrigatorio IS TRUE
          AND d.ativo IS TRUE
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK definicao1/referencia5.000 divergente';
    END IF;

    SELECT count(*) INTO v_total
    FROM homologacao.operacao_resultado_definicao
    WHERE ativo IS TRUE
      AND codigo_perfil_resultado IS NULL;

    IF v_total<>0 THEN
        RAISE EXCEPTION
            'POSTCHECK definicoes ativas sem perfil=% esperado=0',
            v_total;
    END IF;

    -- Nenhuma definicao WC9/WC10 inventada.
    IF EXISTS (
        SELECT 1
        FROM homologacao.operacao_resultado_definicao d
        JOIN homologacao.operacao_resultado_perfil p
          ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
        WHERE p.ativo IS TRUE
          AND (
               (p.codigo_configuracao_rota=8
                AND p.ordem_ocorrencia_workcenter=1)
            OR (p.codigo_configuracao_rota=9
                AND p.ordem_ocorrencia_workcenter IN (1,2))
          )
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK definicao WC9/WC10 indevida';
    END IF;

    -- --------------------------------------------------------
    -- 9.4 UNIQUEs: antigos ausentes, novos presentes
    -- --------------------------------------------------------

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_item'
    ) IS NOT NULL THEN
        RAISE EXCEPTION
            'POSTCHECK UNIQUE legado item ainda existe';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_ordem'
    ) IS NOT NULL THEN
        RAISE EXCEPTION
            'POSTCHECK UNIQUE legado ordem ainda existe';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_item'
    ) IS NULL THEN
        RAISE EXCEPTION
            'POSTCHECK UNIQUE novo perfil/item ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_ordem'
    ) IS NULL THEN
        RAISE EXCEPTION
            'POSTCHECK UNIQUE novo perfil/ordem ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_resultado_perfil_ativo_ocorrencia'
    ) IS NULL THEN
        RAISE EXCEPTION
            'POSTCHECK UNIQUE perfil rota/ocorrencia ausente';
    END IF;

    IF to_regclass(
        'homologacao.uq_operacao_config_ativa_plant_wc'
    ) IS NULL THEN
        RAISE EXCEPTION
            'POSTCHECK UNIQUE Plant+WorkCenter ausente';
    END IF;

    -- --------------------------------------------------------
    -- 9.5 Relacao legado definicao->config permanece
    -- --------------------------------------------------------

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='operacao_resultado_definicao'
          AND column_name='codigo_configuracao'
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK codigo_configuracao legado ausente/divergente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_resultado_definicao'::regclass
          AND conname='fk_resultado_definicao_configuracao'
          AND contype='f'
          AND convalidated IS TRUE
          AND confrelid=
              'homologacao.operacao_producao_configuracao'::regclass
    ) THEN
        RAISE EXCEPTION
            'POSTCHECK FK legado definicao->config ausente/divergente';
    END IF;

    -- --------------------------------------------------------
    -- 9.6 Nenhuma rota fora de 5..11 foi alterada
    -- --------------------------------------------------------

    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(to_jsonb(t)::text,'|' ORDER BY codigo_configuracao),
            ''
        ))
    )
    INTO v_post
    FROM homologacao.operacao_producao_configuracao t
    WHERE codigo_configuracao NOT BETWEEN 5 AND 11;

    SELECT valor INTO STRICT v_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='CONFIGS_FORA_5_11';

    IF v_post IS DISTINCT FROM v_pre THEN
        RAISE EXCEPTION
            'POSTCHECK configs fora 5..11 alteradas pre=% post=%',
            v_pre,
            v_post;
    END IF;

    -- --------------------------------------------------------
    -- 9.7 Nenhuma definicao perdida/criada
    -- --------------------------------------------------------

    SELECT count(*)::text INTO v_post
    FROM homologacao.operacao_resultado_definicao;

    SELECT valor INTO STRICT v_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='DEFINICOES_TOTAL';

    IF v_post IS DISTINCT FROM v_pre THEN
        RAISE EXCEPTION
            'POSTCHECK total definicoes alterado pre=% post=%',
            v_pre,
            v_post;
    END IF;

    -- --------------------------------------------------------
    -- 9.8 Fingerprints da massa operacional inalterados
    -- --------------------------------------------------------

    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(to_jsonb(t)::text,'|' ORDER BY codigo_apontamento),
            ''
        ))
    )
    INTO v_post
    FROM homologacao.operacao_producao_apontamento t;

    SELECT valor INTO STRICT v_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='MASSA_APONTAMENTO';

    IF v_post IS DISTINCT FROM v_pre THEN
        RAISE EXCEPTION
            'POSTCHECK massa apontamento alterada pre=% post=%',
            v_pre,v_post;
    END IF;

    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(
                to_jsonb(t)::text,
                '|' ORDER BY codigo_apontamento_processo
            ),
            ''
        ))
    )
    INTO v_post
    FROM homologacao.operacao_producao_apontamento_processo t;

    SELECT valor INTO STRICT v_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='MASSA_VINCULO_048';

    IF v_post IS DISTINCT FROM v_pre THEN
        RAISE EXCEPTION
            'POSTCHECK massa vinculo048 alterada pre=% post=%',
            v_pre,v_post;
    END IF;

    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(to_jsonb(t)::text,'|' ORDER BY codigo_resultado),
            ''
        ))
    )
    INTO v_post
    FROM homologacao.operacao_resultado_registro t;

    SELECT valor INTO STRICT v_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='MASSA_RESULTADO_REGISTRO';

    IF v_post IS DISTINCT FROM v_pre THEN
        RAISE EXCEPTION
            'POSTCHECK massa resultado registro alterada pre=% post=%',
            v_pre,v_post;
    END IF;

    SELECT concat(
        count(*), ':',
        md5(COALESCE(
            string_agg(
                to_jsonb(t)::text,
                '|' ORDER BY codigo_resultado_item
            ),
            ''
        ))
    )
    INTO v_post
    FROM homologacao.operacao_resultado_registro_item t;

    SELECT valor INTO STRICT v_pre
    FROM pg_temp.gate_048_wc_data06_rev2_pre
    WHERE chave='MASSA_RESULTADO_ITEM';

    IF v_post IS DISTINCT FROM v_pre THEN
        RAISE EXCEPTION
            'POSTCHECK massa resultado item alterada pre=% post=%',
            v_pre,v_post;
    END IF;
END
$postcheck$;

SELECT 'POSTCHECK_PRE_COMMIT=OK';
SELECT 'ROTAS_CANONICAS_ATIVAS=6';
SELECT 'PERFIS_FUNCIONAIS=4';
SELECT 'DEFINICOES_ATIVAS_SEM_PERFIL=0';
SELECT 'MASSA_OPERACIONAL_ALTERADA=NAO';

-- ============================================================
-- 10. COMMIT UNICO
-- ============================================================

COMMIT;

-- ============================================================
-- 11. EVIDENCIA POS-COMMIT
-- ============================================================

SELECT 'GATE_048_WC_DATA_06_FASE_B_REV4=COMMIT_OK';

SELECT concat(
    'ROTA_CANONICA',
    '|CONFIG=',codigo_configuracao,
    '|PLANT=',centro,
    '|WORKCENTER=',centro_trabalho,
    '|TIPO_PROCESSO=',tipo_processo,
    '|TELA=',tela_destino,
    '|ATIVO=',ativo::text
)
FROM homologacao.operacao_producao_configuracao
WHERE codigo_configuracao IN (5,6,7,8,9,11)
ORDER BY codigo_configuracao;

SELECT concat(
    'CONFIG10',
    '|ATIVO=',ativo::text,
    '|CENTRO=',centro,
    '|WORKCENTER=',centro_trabalho,
    '|OPERACAO_LEGADO=',COALESCE(operacao_sap,'<NULL>')
)
FROM homologacao.operacao_producao_configuracao
WHERE codigo_configuracao=10;

SELECT concat(
    'PERFIL_RESULTADO',
    '|CONFIG_ROTA=',codigo_configuracao_rota,
    '|OCORRENCIA=',ordem_ocorrencia_workcenter,
    '|CODIGO_PERFIL=',codigo_perfil_resultado,
    '|ATIVO=',ativo::text
)
FROM homologacao.operacao_resultado_perfil
ORDER BY codigo_configuracao_rota,ordem_ocorrencia_workcenter;

SELECT concat(
    'DEFINICAO_1',
    '|CODIGO_CONFIGURACAO_LEGADO=',d.codigo_configuracao,
    '|CODIGO_PERFIL=',d.codigo_perfil_resultado,
    '|CONFIG_ROTA=',p.codigo_configuracao_rota,
    '|OCORRENCIA=',p.ordem_ocorrencia_workcenter,
    '|CODIGO_ITEM=',d.codigo_item,
    '|TIPO=',d.tipo,
    '|MEDIDA=',d.medida,
    '|REFERENCIA=',d.referencia,
    '|ORDEM=',d.ordem_exibicao,
    '|OBRIGATORIO=',d.obrigatorio::text,
    '|ATIVO=',d.ativo::text
)
FROM homologacao.operacao_resultado_definicao d
JOIN homologacao.operacao_resultado_perfil p
  ON p.codigo_perfil_resultado=d.codigo_perfil_resultado
WHERE d.codigo_definicao=1;

SELECT concat(
    'UNIQUE_LEGADO_ITEM=',
    CASE
      WHEN to_regclass(
        'homologacao.uq_resultado_definicao_ativa_item'
      ) IS NULL
      THEN 'AUSENTE'
      ELSE 'PRESENTE'
    END
);

SELECT concat(
    'UNIQUE_LEGADO_ORDEM=',
    CASE
      WHEN to_regclass(
        'homologacao.uq_resultado_definicao_ativa_ordem'
      ) IS NULL
      THEN 'AUSENTE'
      ELSE 'PRESENTE'
    END
);

SELECT concat(
    'UNIQUE_NOVO_PERFIL_ITEM=',
    CASE
      WHEN to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_item'
      ) IS NOT NULL
      THEN 'PRESENTE'
      ELSE 'AUSENTE'
    END
);

SELECT concat(
    'UNIQUE_NOVO_PERFIL_ORDEM=',
    CASE
      WHEN to_regclass(
        'homologacao.uq_resultado_definicao_ativa_perfil_ordem'
      ) IS NOT NULL
      THEN 'PRESENTE'
      ELSE 'AUSENTE'
    END
);

SELECT concat(
    'CODIGO_CONFIGURACAO_LEGADO_PRESERVADO=',
    CASE
      WHEN EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='operacao_resultado_definicao'
          AND column_name='codigo_configuracao'
      )
      THEN 'SIM'
      ELSE 'NAO'
    END
);

SELECT concat(
    'FK_LEGADA_PRESERVADA=',
    CASE
      WHEN EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid=
              'homologacao.operacao_resultado_definicao'::regclass
          AND conname='fk_resultado_definicao_configuracao'
          AND contype='f'
          AND convalidated IS TRUE
      )
      THEN 'SIM'
      ELSE 'NAO'
    END
);
