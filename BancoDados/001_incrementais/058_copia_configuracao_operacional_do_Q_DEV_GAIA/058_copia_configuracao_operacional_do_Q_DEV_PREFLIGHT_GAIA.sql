\set ON_ERROR_STOP on
-- ============================================================
-- 058_copia_configuracao_operacional_do_Q_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 058 - Copia da configuracao operacional do banco de homologacao do Q
-- PREFLIGHT (SOMENTE LEITURA)
--
-- ============================================================
-- POR QUE ESTE INCREMENTO EXISTE, E POR QUE NAO E O 055
-- ============================================================
-- O objetivo era aplicar o 055 no DEV. Ao medir, ficou provado que o 055 NAO e
-- aplicavel aqui, e que o estado que ele exige nao existe em lugar algum:
--
--   a) O preflight do 055 exige as seis configs legadas com centro_trabalho CURTO
--      ('5','6','7','9','10','100'), e exige que nenhuma das nove chaves
--      Plant+WorkCenter longas exista ativa. O trabalho do proprio 055 e migrar os
--      curtos para os longos. O banco do Q ja tem os LONGOS: ele esta no estado
--      POS-055.
--   b) Pior: o preflight do 055 exige a config 7 como RESULTADO_APONTAMENTO /
--      ProcessoResultadoApontamentoForm, e o Q tem a config 7 como
--      CONSUMO_MATERIA_PRIMA / ProcessoConsumoMaterialForm. O 055 nunca altera
--      tipo_processo nem tela_destino -- so centro_trabalho. Logo a config 7 foi
--      mudada por algo POSTERIOR ao 055, que tambem nao esta neste repositorio.
--   c) O Q tem ainda as definicoes 5 e 6, criadas pelos gates
--      GATE_103P_OP1000173_0090_TEMPO e GATE_103U_OP1000173_0100_TEMPO, igualmente
--      ausentes daqui. E o preflight do 055 exige ZERO definicoes nos perfis 2..4,
--      onde essas duas justamente estao.
--
-- Portanto o baseline pre-055 e um estado HISTORICO do Q que o Q nao tem mais.
-- Reconstrui-lo exigiria inventar o tipo_processo antigo da config 7, contradizendo
-- o que o banco do Q afirma hoje. Nao foi feito.
--
-- O caminho sem fabricar nenhum valor e copiar o estado ATUAL do Q, que foi o que o
-- usuario autorizou expressamente. Todo valor deste pacote veio do catalogo e das
-- tabelas do Q, lidos em transacao READ ONLY com o papel de aplicacao fugapet_q_app.
--
-- CONSEQUENCIA REGISTRADA: com o 058 aplicado, o 055 fica DEFINITIVAMENTE
-- inaplicavel no DEV -- seu preflight passa a falhar na checagem das nove chaves
-- Plant+WorkCenter. Isso e deliberado: o 058 SUPERA o 055, trazendo de uma vez o
-- resultado dele e o das mudancas posteriores (GATE_048_G_DATA_03, GATE_103P,
-- GATE_103U) que nunca foram versionadas aqui.
--
-- ============================================================
-- LINHAGEM RECONSTRUIDA A PARTIR DO criado_por DAS LINHAS DO Q
-- ============================================================
--   configs 5 e 6   'FugaPET incremental 040 rotas consumo'  -> o DEV TEM as linhas,
--                   mas com centro e centro_trabalho VAZIOS; nunca recebeu o
--                   preenchimento posterior desses dois campos.
--   configs 7..11   'GATE_048_G_DATA_03'                     -> ausentes no DEV.
--   configs 12..14  'MIGRATION_055_ROUTE_PROFILE_094I'       -> ausentes (e o 055).
--   perfis 1..4     'fugapet_q_owner'                        -> ausentes no DEV.
--   perfis 5..7     'postgres'                               -> ausentes (sao do 055).
--   definicao 1     'fugapet_q_owner'                        -> ausente no DEV.
--   definicoes 2..4 'MIGRATION_055_ROUTE_PROFILE_094I'       -> ausentes (sao do 055).
--   definicoes 5..6 'GATE_103P...' / 'GATE_103U...'          -> ausentes no DEV.
--
-- DIFERENCAS DECLARADAS em relacao ao Q, nas duas colunas de auditoria:
--   criado_em  -> now() no DEV. Copiar o timestamp do Q afirmaria que a linha foi
--                 criada no DEV naquela data, o que e falso.
--   criado_por -> os valores do Q que sao MARCADORES DE PROCEDENCIA sao preservados
--                 literalmente ('GATE_048_G_DATA_03', 'MIGRATION_055_ROUTE_PROFILE_094I',
--                 'GATE_103P_OP1000173_0090_TEMPO', 'GATE_103U_OP1000173_0100_TEMPO',
--                 'FugaPET incremental 040 rotas consumo'), porque identificam a
--                 mudanca de origem. Os valores que sao NOMES DE PAPEL do Q
--                 ('fugapet_q_owner', 'postgres') NAO sao copiados: fugapet_q_owner
--                 nem existe neste cluster, e afirmar que ele criou a linha seria
--                 falso. Nesses casos entra 'MIGRATION_058_COPIA_CONFIG_Q'.
--
--   SOMENTE LEITURA aqui. NAO DML. NAO DDL. NAO SAP.
-- ============================================================

BEGIN TRANSACTION READ ONLY;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_ids       bigint[];
    v_n         bigint;
    v_centro    text;
    v_wc        text;
    v_chk       text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '058 PREFLIGHT: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '058 PREFLIGHT: principal administrativo inesperado: %', current_user;
    END IF;

    RAISE NOTICE 'DATABASE=%  PRINCIPAL=%', current_database(), current_user;

    -- -------- As tres tabelas precisam existir (o 057 criou duas delas) --------
    IF to_regclass('desenvolvimento.operacao_producao_configuracao') IS NULL
       OR to_regclass('desenvolvimento.operacao_resultado_perfil') IS NULL
       OR to_regclass('desenvolvimento.operacao_resultado_definicao') IS NULL THEN
        RAISE EXCEPTION '058 PREFLIGHT: tabela-alvo ausente; o incremento 057 precisa estar aplicado.';
    END IF;
    RAISE NOTICE 'TABELAS=PRESENTES (057 aplicado)';

    -- -------- Estado de partida EXATO. Fail-closed: nao sobrescrever nada. --------
    SELECT array_agg(codigo_configuracao ORDER BY codigo_configuracao)
      INTO v_ids
      FROM desenvolvimento.operacao_producao_configuracao;

    IF v_ids IS DISTINCT FROM ARRAY[5::bigint, 6::bigint] THEN
        RAISE EXCEPTION
            '058 PREFLIGHT: esperadas exatamente as configs 5 e 6, encontradas %. O estado de partida nao e o medido; abortado para nao sobrescrever configuracao.',
            coalesce(array_to_string(v_ids, ','), '<nenhuma>');
    END IF;
    RAISE NOTICE 'CONFIGS_EXISTENTES=5,6 (como medido)';

    -- As duas precisam estar no estado do incremento 040, com centro e wc vazios:
    -- e exatamente esse vazio que o 058 preenche.
    SELECT centro, centro_trabalho INTO v_centro, v_wc
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao = 5;
    IF btrim(coalesce(v_centro, '')) <> '' OR btrim(coalesce(v_wc, '')) <> '' THEN
        RAISE EXCEPTION
            '058 PREFLIGHT: config 5 tem centro="%" e centro_trabalho="%"; esperados vazios. Alguem ja alterou esta linha.',
            v_centro, v_wc;
    END IF;

    SELECT centro, centro_trabalho INTO v_centro, v_wc
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao = 6;
    IF btrim(coalesce(v_centro, '')) <> '' OR btrim(coalesce(v_wc, '')) <> '' THEN
        RAISE EXCEPTION
            '058 PREFLIGHT: config 6 tem centro="%" e centro_trabalho="%"; esperados vazios.',
            v_centro, v_wc;
    END IF;
    RAISE NOTICE 'CONFIGS_5_6=centro e centro_trabalho vazios, como esperado';

    -- As duas tabelas do 057 devem estar vazias.
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_perfil' INTO v_n;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 PREFLIGHT: operacao_resultado_perfil tem % linhas, esperado 0.', v_n;
    END IF;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_definicao' INTO v_n;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 PREFLIGHT: operacao_resultado_definicao tem % linhas, esperado 0.', v_n;
    END IF;
    RAISE NOTICE 'PERFIL=0 linhas  DEFINICAO=0 linhas';

    -- -------- Nenhum apontamento pode referenciar perfil ainda --------
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_apontamento WHERE codigo_perfil_resultado IS NOT NULL'
      INTO v_n;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 PREFLIGHT: % apontamentos ja referenciam perfil de resultado.', v_n;
    END IF;
    RAISE NOTICE 'APONTAMENTOS_COM_PERFIL=0';

    -- -------- O indice do Q que falta aqui nao pode ja existir --------
    IF EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname = 'desenvolvimento'
           AND indexname = 'uq_operacao_config_ativa_plant_wc'
    ) THEN
        RAISE EXCEPTION '058 PREFLIGHT: indice uq_operacao_config_ativa_plant_wc ja existe.';
    END IF;
    RAISE NOTICE 'INDICE_PLANT_WC=ausente (sera criado, igual ao Q)';

    -- -------- As tres colunas de id precisam ser identity BY DEFAULT --------
    SELECT count(*) INTO v_n
      FROM pg_attribute a
      JOIN pg_class c ON c.oid = a.attrelid
      JOIN pg_namespace ns ON ns.oid = c.relnamespace AND ns.nspname = 'desenvolvimento'
     WHERE a.attidentity = 'd'
       AND (
            (c.relname = 'operacao_producao_configuracao' AND a.attname = 'codigo_configuracao')
         OR (c.relname = 'operacao_resultado_perfil'      AND a.attname = 'codigo_perfil_resultado')
         OR (c.relname = 'operacao_resultado_definicao'   AND a.attname = 'codigo_definicao')
       );
    IF v_n <> 3 THEN
        RAISE EXCEPTION
            '058 PREFLIGHT: esperadas 3 colunas IDENTITY BY DEFAULT, encontradas %. Com GENERATED ALWAYS os ids explicitos seriam recusados.',
            v_n;
    END IF;
    RAISE NOTICE 'IDENTITY_BY_DEFAULT=3 (ids explicitos permitidos)';

    -- -------- As CHECKs do DEV sao mais estreitas que as do Q --------
    -- O DEV nao permite 'RESULTADO_APONTAMENTO' em tipo_processo, que e o processo de
    -- 8 das 10 linhas do Q, e nao tem a ck_operacao_config_plant_wc_consistente.
    -- O 058 alinha as duas. Ver o bloco 0B da PROPOSTA, inclusive o desvio
    -- deliberado de NAO permitir CONSUMO_SEMI_ACABADO.
    SELECT pg_get_constraintdef(oid) INTO v_chk
      FROM pg_constraint
     WHERE conrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
       AND conname = 'ck_operacao_config_tipo_processo';

    IF v_chk IS NULL THEN
        RAISE EXCEPTION '058 PREFLIGHT: ck_operacao_config_tipo_processo ausente; estado inesperado.';
    END IF;

    IF position('RESULTADO_APONTAMENTO' in v_chk) > 0 THEN
        RAISE EXCEPTION
            '058 PREFLIGHT: ck_operacao_config_tipo_processo ja permite RESULTADO_APONTAMENTO; alguem ja a alterou.';
    END IF;
    RAISE NOTICE 'CHECK_TIPO_PROCESSO=sem RESULTADO_APONTAMENTO (sera substituida)';

    IF EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
           AND conname = 'ck_operacao_config_plant_wc_consistente'
    ) THEN
        RAISE EXCEPTION '058 PREFLIGHT: ck_operacao_config_plant_wc_consistente ja existe.';
    END IF;
    RAISE NOTICE 'CHECK_PLANT_WC=ausente (sera criada, igual ao Q)';

    RAISE NOTICE 'MIGRATION_PRECONDITION=PASS';

    -- -------- Diagnostico --------
    RAISE NOTICE 'DELTA_PREVISTO=2 CHECKs alinhadas, 2 configs atualizadas (centro e centro_trabalho), 8 inseridas (7..14),';
    RAISE NOTICE '               7 perfis inseridos (1..7), 6 definicoes inseridas (1..6), 1 indice unico parcial,';
    RAISE NOTICE '               3 sequences de identity avancadas';
    RAISE NOTICE 'EFEITO_NO_055=ficara definitivamente inaplicavel; o 058 o supera';
END
$$;

COMMIT;

\echo '058_PREFLIGHT_CONCLUIDO'
\echo 'ESTADO_DE_PARTIDA=configs 5 e 6 com centro/centro_trabalho vazios; perfil e definicao vazias'
\echo 'DML=0'
\echo 'DDL=0'
\echo 'SAP_WRITE=0'
