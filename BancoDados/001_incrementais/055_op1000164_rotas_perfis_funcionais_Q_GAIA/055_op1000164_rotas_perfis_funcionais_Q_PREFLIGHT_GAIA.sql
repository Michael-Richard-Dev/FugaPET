\set ON_ERROR_STOP on

-- ============================================================================
-- FugaPET Q - Migration 055
-- OP1000164 - Rotas funcionais Plant+WorkCenter + perfis próprios
-- PREFLIGHT GAIA
--
-- GATE:
-- FUGAPET-Q-CONTROLE-APONTAMENTOS-OP1000164-ROUTE-PROFILE-MIGRATION-DESIGN-094I
--
-- OFFLINE PACKAGE. ESTE ARQUIVO NAO FOI EXECUTADO POR GAIA.
-- READ-ONLY / FAIL-CLOSED.
-- ============================================================================

BEGIN TRANSACTION
ISOLATION LEVEL REPEATABLE READ
READ ONLY;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '90s';
SET LOCAL search_path = pg_catalog, homologacao;

DO $preflight$
DECLARE
    v_count integer;
    v_seq text;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '055 PRECHECK: database incorreto: %', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: schema homologacao ausente';
    END IF;

    IF to_regclass('homologacao.operacao_producao_configuracao') IS NULL
       OR to_regclass('homologacao.operacao_resultado_perfil') IS NULL
       OR to_regclass('homologacao.operacao_resultado_definicao') IS NULL
       OR to_regclass('homologacao.operacao_producao_apontamento') IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: objetos obrigatorios ausentes';
    END IF;

    -- Migration 054 aplicada: storage snapshot existe exatamente como bigint NULL.
    SELECT count(*)
      INTO v_count
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'operacao_producao_apontamento'
       AND column_name = 'codigo_perfil_resultado'
       AND data_type = 'bigint'
       AND is_nullable = 'YES'
       AND column_default IS NULL;

    IF v_count <> 1 THEN
        RAISE EXCEPTION '055 PRECHECK: storage 054 divergente; count=%', v_count;
    END IF;

    -- Geracao de IDs: config e definicao devem possuir sequence/default material.
    v_seq := pg_get_serial_sequence(
        'homologacao.operacao_producao_configuracao',
        'codigo_configuracao'
    );
    IF v_seq IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: sequence/default de codigo_configuracao ausente';
    END IF;

    v_seq := pg_get_serial_sequence(
        'homologacao.operacao_resultado_definicao',
        'codigo_definicao'
    );
    IF v_seq IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: sequence/default de codigo_definicao ausente';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_attribute a
         WHERE a.attrelid = 'homologacao.operacao_resultado_perfil'::regclass
           AND a.attname = 'codigo_perfil_resultado'
           AND a.atttypid = 'bigint'::regtype
           AND a.attnotnull
           AND a.attidentity = 'd'
           AND a.attnum > 0
           AND NOT a.attisdropped
    ) THEN
        RAISE EXCEPTION '055 PRECHECK: codigo_perfil_resultado nao e identity BY DEFAULT esperado';
    END IF;

    IF pg_get_serial_sequence(
        'homologacao.operacao_resultado_perfil',
        'codigo_perfil_resultado'
    ) IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: sequence de codigo_perfil_resultado ausente';
    END IF;

    -- Constraints/indices estruturais indispensaveis.
    IF to_regclass('homologacao.uq_operacao_config_ativa_plant_wc') IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: uq_operacao_config_ativa_plant_wc ausente';
    END IF;

    IF to_regclass('homologacao.uq_resultado_perfil_ativo_ocorrencia') IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: uq_resultado_perfil_ativo_ocorrencia ausente';
    END IF;

    IF to_regclass('homologacao.uq_resultado_definicao_ativa_perfil_item') IS NULL
       OR to_regclass('homologacao.uq_resultado_definicao_ativa_perfil_ordem') IS NULL THEN
        RAISE EXCEPTION '055 PRECHECK: indices unicos de definicao por perfil ausentes';
    END IF;

    -- Seis configs legadas: exatamente uma row e baseline funcional esperada.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao = 5
       AND centro = '3007'
       AND centro_trabalho = '5'
       AND tipo_ordem = ''
       AND sequencia_sap = ''
       AND operacao_sap = '0050'
       AND suboperacao_sap = ''
       AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
       AND tela_destino = 'ProcessoConsumoMaterialForm'
       AND exige_operacao_anterior IS FALSE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN RAISE EXCEPTION '055 PRECHECK: CONFIG5 divergiu'; END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao = 6
       AND centro = '3007'
       AND centro_trabalho = '6'
       AND tipo_ordem = ''
       AND sequencia_sap = ''
       AND operacao_sap = '0060'
       AND suboperacao_sap = ''
       AND tipo_processo = 'CONSUMO_QUIMICOS'
       AND tela_destino = 'ProcessoConsumoMaterialForm'
       AND exige_operacao_anterior IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN RAISE EXCEPTION '055 PRECHECK: CONFIG6 divergiu'; END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao = 7
       AND centro = '3007'
       AND centro_trabalho = '7'
       AND tipo_ordem = ''
       AND sequencia_sap = ''
       AND operacao_sap = '0070'
       AND suboperacao_sap = ''
       AND tipo_processo = 'RESULTADO_APONTAMENTO'
       AND tela_destino = 'ProcessoResultadoApontamentoForm'
       AND exige_operacao_anterior IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN RAISE EXCEPTION '055 PRECHECK: CONFIG7 divergiu'; END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao = 8
       AND centro = '3007'
       AND centro_trabalho = '9'
       AND tipo_ordem = ''
       AND sequencia_sap = ''
       AND operacao_sap = '0090'
       AND suboperacao_sap = ''
       AND tipo_processo = 'RESULTADO_APONTAMENTO'
       AND tela_destino = 'ProcessoResultadoApontamentoForm'
       AND exige_operacao_anterior IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN RAISE EXCEPTION '055 PRECHECK: CONFIG8 divergiu'; END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao = 9
       AND centro = '3007'
       AND centro_trabalho = '10'
       AND tipo_ordem = ''
       AND sequencia_sap = ''
       AND operacao_sap = '0100'
       AND suboperacao_sap = ''
       AND tipo_processo = 'RESULTADO_APONTAMENTO'
       AND tela_destino = 'ProcessoResultadoApontamentoForm'
       AND exige_operacao_anterior IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN RAISE EXCEPTION '055 PRECHECK: CONFIG9 divergiu'; END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE codigo_configuracao = 11
       AND centro = '3007'
       AND centro_trabalho = '100'
       AND tipo_ordem = ''
       AND sequencia_sap = ''
       AND operacao_sap = '0140'
       AND suboperacao_sap = ''
       AND tipo_processo = 'SEMI_ACABADO'
       AND tela_destino = 'ProcessoSemiAcabadoForm'
       AND exige_operacao_anterior IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN RAISE EXCEPTION '055 PRECHECK: CONFIG11 divergiu'; END IF;

    -- Operacao_sap permanece provenance unica dos seis bindings historicos.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE operacao_sap IN ('0050','0060','0070','0090','0100','0140');
    IF v_count <> 6 THEN
        RAISE EXCEPTION '055 PRECHECK: bindings historicos nao totalizam 6; total=%', v_count;
    END IF;

    -- Tres novas operacoes devem estar completamente ausentes, qualquer status/escopo.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE operacao_sap IN ('0040','0080','0110');
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PRECHECK: 0040/0080/0110 ja possuem configuracao; total=%', v_count;
    END IF;

    -- Nenhuma das nove chaves Plant+WorkCenter pode existir ativa antes do delta.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_producao_configuracao
     WHERE ativo IS TRUE
       AND centro = '3007'
       AND centro_trabalho IN (
           '3007015','3007045','3007046','3007047','3007002',
           '3007001','3007005','3007018','3007043'
       );
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PRECHECK: conflito literal Plant+WorkCenter ja existe; total=%', v_count;
    END IF;

    -- Perfis 1..4: identidade/parentesco/ocorrencia intactos.
    IF EXISTS (
        SELECT codigo_perfil_resultado, codigo_configuracao_rota,
               ordem_ocorrencia_workcenter, ativo
          FROM homologacao.operacao_resultado_perfil
         WHERE codigo_perfil_resultado BETWEEN 1 AND 4
        EXCEPT
        VALUES
          (1::bigint, 7::bigint, 1::integer, true),
          (2::bigint, 8::bigint, 1::integer, true),
          (3::bigint, 9::bigint, 1::integer, true),
          (4::bigint, 9::bigint, 2::integer, true)
    ) OR EXISTS (
        VALUES
          (1::bigint, 7::bigint, 1::integer, true),
          (2::bigint, 8::bigint, 1::integer, true),
          (3::bigint, 9::bigint, 1::integer, true),
          (4::bigint, 9::bigint, 2::integer, true)
        EXCEPT
        SELECT codigo_perfil_resultado, codigo_configuracao_rota,
               ordem_ocorrencia_workcenter, ativo
          FROM homologacao.operacao_resultado_perfil
         WHERE codigo_perfil_resultado BETWEEN 1 AND 4
    ) THEN
        RAISE EXCEPTION '055 PRECHECK: perfis 1..4 divergiram';
    END IF;

    -- Definition 1 do Profile 1 intacta e unica.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao
     WHERE codigo_definicao = 1
       AND codigo_configuracao = 7
       AND codigo_perfil_resultado = 1
       AND codigo_item = 'TEMPO_OPERACAO'
       AND tipo = 'TEMPO'
       AND medida = 'MINUTO'
       AND referencia = 5.000
       AND ordem_exibicao = 1
       AND obrigatorio IS TRUE
       AND ativo IS TRUE;
    IF v_count <> 1 THEN
        RAISE EXCEPTION '055 PRECHECK: definition1/profile1 divergiu';
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao
     WHERE codigo_perfil_resultado IN (2,3,4);
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PRECHECK: profiles 2..4 passaram a ter definicoes; total=%', v_count;
    END IF;

    -- Integridade normalizada atual.
    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_perfil p
      LEFT JOIN homologacao.operacao_producao_configuracao c
        ON c.codigo_configuracao = p.codigo_configuracao_rota
     WHERE c.codigo_configuracao IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PRECHECK: profile_without_route_count=%', v_count;
    END IF;

    SELECT count(*) INTO v_count
      FROM homologacao.operacao_resultado_definicao d
      LEFT JOIN homologacao.operacao_resultado_perfil p
        ON p.codigo_perfil_resultado = d.codigo_perfil_resultado
     WHERE d.codigo_perfil_resultado IS NOT NULL
       AND p.codigo_perfil_resultado IS NULL;
    IF v_count <> 0 THEN
        RAISE EXCEPTION '055 PRECHECK: definition_without_profile_count=%', v_count;
    END IF;
END
$preflight$;

-- Estado COMPLETO das seis rows antes da alteracao focal.
SELECT
    codigo_configuracao,
    to_jsonb(c) AS config_row_before
FROM homologacao.operacao_producao_configuracao c
WHERE codigo_configuracao IN (5,6,7,8,9,11)
ORDER BY codigo_configuracao;

-- Evidence fields.
SELECT 'CURRENT_DATABASE=' || current_database();
SELECT 'CURRENT_SCHEMA_TARGET=homologacao';
SELECT 'MIGRATION_054_STORAGE_PRESENT=SIM';
SELECT 'EXISTING_CONFIG_IDS=5,6,7,8,9,11';
SELECT 'NEW_OPERATIONS_ABSENT=0040,0080,0110';
SELECT 'TARGET_LITERAL_ROUTE_CONFLICTS=0';
SELECT 'EXISTING_PROFILES_1_4_PRESERVED=SIM';
SELECT 'PROFILE1_DEFINITION_INTACT=SIM';
SELECT 'PROFILE_WITHOUT_ROUTE_COUNT=0';
SELECT 'DEFINITION_WITHOUT_PROFILE_COUNT=0';
SELECT 'CONFIG_ID_GENERATION=NATIVE_SEQUENCE_DEFAULT';
SELECT 'PROFILE_ID_GENERATION=IDENTITY_BY_DEFAULT';
SELECT 'DEFINITION_ID_GENERATION=NATIVE_SEQUENCE_DEFAULT';
SELECT 'PREFLIGHT_STATUS=PASS';

ROLLBACK;
