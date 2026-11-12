\set ON_ERROR_STOP on
-- ============================================================
-- 058_copia_configuracao_operacional_do_Q_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 058 - Copia da configuracao operacional do banco de homologacao do Q
-- PROPOSTA (APLICA DML E UM INDICE)
--
-- A procedencia completa, a linhagem reconstruida, o motivo de o 055 nao ser
-- aplicavel e as diferencas declaradas de criado_em/criado_por estao no PREFLIGHT
-- deste mesmo pacote. Todo valor abaixo veio das tabelas do Q, lidas em transacao
-- READ ONLY com o papel de aplicacao fugapet_q_app.
--
-- ESTE E O UNICO PACOTE DESTA SERIE QUE ESCREVE CONFIGURACAO DE NEGOCIO.
-- Ele altera o roteamento de telas por operacao SAP. Em particular, ATUALIZA as
-- configs 5 e 6 que o DEV ja tinha, preenchendo centro e centro_trabalho que
-- estavam vazios -- nao e so acrescimo.
--
--   NAO SAP. NAO DELETE. Nenhuma sequence e resetada para tras.
-- ============================================================

BEGIN;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '5min';

-- ============================================================
-- 0. GUARDAS FAIL-CLOSED (repetem o PREFLIGHT, dentro da transacao)
-- ============================================================

DO $$
DECLARE
    v_ids    bigint[];
    v_n      bigint;
    v_centro text;
    v_wc     text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '058 ABORTADO: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '058 ABORTADO: principal administrativo inesperado: %', current_user;
    END IF;

    IF to_regclass('desenvolvimento.operacao_resultado_perfil') IS NULL
       OR to_regclass('desenvolvimento.operacao_resultado_definicao') IS NULL THEN
        RAISE EXCEPTION '058 ABORTADO: o incremento 057 precisa estar aplicado.';
    END IF;

    SELECT array_agg(codigo_configuracao ORDER BY codigo_configuracao)
      INTO v_ids
      FROM desenvolvimento.operacao_producao_configuracao;
    IF v_ids IS DISTINCT FROM ARRAY[5::bigint, 6::bigint] THEN
        RAISE EXCEPTION
            '058 ABORTADO: esperadas exatamente as configs 5 e 6, encontradas %.',
            coalesce(array_to_string(v_ids, ','), '<nenhuma>');
    END IF;

    FOR v_n IN SELECT unnest(ARRAY[5, 6]) LOOP
        SELECT centro, centro_trabalho INTO v_centro, v_wc
          FROM desenvolvimento.operacao_producao_configuracao
         WHERE codigo_configuracao = v_n;
        IF btrim(coalesce(v_centro, '')) <> '' OR btrim(coalesce(v_wc, '')) <> '' THEN
            RAISE EXCEPTION
                '058 ABORTADO: config % ja tem centro/centro_trabalho preenchidos ("%" / "%").',
                v_n, v_centro, v_wc;
        END IF;
    END LOOP;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_perfil' INTO v_n;
    IF v_n <> 0 THEN RAISE EXCEPTION '058 ABORTADO: operacao_resultado_perfil nao esta vazia (% linhas).', v_n; END IF;
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_definicao' INTO v_n;
    IF v_n <> 0 THEN RAISE EXCEPTION '058 ABORTADO: operacao_resultado_definicao nao esta vazia (% linhas).', v_n; END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_apontamento WHERE codigo_perfil_resultado IS NOT NULL'
      INTO v_n;
    IF v_n <> 0 THEN RAISE EXCEPTION '058 ABORTADO: % apontamentos referenciam perfil.', v_n; END IF;
END
$$;

-- ============================================================
-- 0B. ALINHAR AS CONSTRAINTS DE CHECK AO Q
--
-- Descoberto ao aplicar: a CHECK ck_operacao_config_tipo_processo do DEV NAO
-- permite 'RESULTADO_APONTAMENTO', que e o tipo_processo de 8 das 10 linhas do Q.
-- E o DEV nao tem a ck_operacao_config_plant_wc_consistente, que o Q tem.
--
-- Lista permitida hoje no DEV:
--   CONSUMO_MATERIA_PRIMA, CONSUMO_QUIMICOS, SEMI_ACABADO, PRODUTO_ACABADO,
--   SEM_DESTINO_CONFIGURADO
-- Lista do Q:
--   as cinco acima + CONSUMO_SEMI_ACABADO + RESULTADO_APONTAMENTO
--
-- DESVIO DELIBERADO DO Q, mais restritivo: entra RESULTADO_APONTAMENTO, que o dado
-- exige, e NAO entra CONSUMO_SEMI_ACABADO. Motivo: a tela de Consumo Semi-Acabado
-- foi REMOVIDA do codigo desta linha, seguindo o proprio gate do Q, de modo que
-- permitir esse valor deixaria configurar uma rota para uma tela que nao existe
-- aqui. A lista do Q esta desatualizada em relacao ao codigo do Q. Nenhuma das 10
-- linhas copiadas usa esse valor, portanto nada se perde. Ser mais restritivo que a
-- origem e a direcao fail-closed; se a tela voltar, isto volta com ela.
--
-- A clausula de nao-vazio apos btrim e a ck_operacao_config_plant_wc_consistente
-- sao APERTOS, copiados do Q na definicao exata. A diferenca em operacao_sap
-- (nullable no Q, NOT NULL aqui) NAO e tocada: alinha-la seria AFROUXAR, e nenhuma
-- linha copiada precisa disso.
-- ============================================================

DO $$
DECLARE
    v_def_atual text;
BEGIN
    SELECT pg_get_constraintdef(oid) INTO v_def_atual
      FROM pg_constraint
     WHERE conrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
       AND conname = 'ck_operacao_config_tipo_processo';

    IF v_def_atual IS NULL THEN
        RAISE EXCEPTION '058 ABORTADO: ck_operacao_config_tipo_processo ausente; estado inesperado.';
    END IF;

    -- Fail-closed: so substituir a definicao exata que foi medida.
    IF position('RESULTADO_APONTAMENTO' in v_def_atual) > 0 THEN
        RAISE EXCEPTION
            '058 ABORTADO: ck_operacao_config_tipo_processo ja permite RESULTADO_APONTAMENTO; alguem ja a alterou.';
    END IF;

    IF position('CONSUMO_MATERIA_PRIMA' in v_def_atual) = 0
       OR position('CONSUMO_QUIMICOS' in v_def_atual) = 0
       OR position('SEMI_ACABADO' in v_def_atual) = 0
       OR position('PRODUTO_ACABADO' in v_def_atual) = 0
       OR position('SEM_DESTINO_CONFIGURADO' in v_def_atual) = 0 THEN
        RAISE EXCEPTION
            '058 ABORTADO: ck_operacao_config_tipo_processo nao tem a lista medida; nao vou substituir uma definicao que nao reconheco.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
           AND conname = 'ck_operacao_config_plant_wc_consistente'
    ) THEN
        RAISE EXCEPTION '058 ABORTADO: ck_operacao_config_plant_wc_consistente ja existe.';
    END IF;

    RAISE NOTICE 'CHECKS=definicao atual reconhecida; sera substituida';
END
$$;

ALTER TABLE desenvolvimento.operacao_producao_configuracao
    DROP CONSTRAINT ck_operacao_config_tipo_processo;

ALTER TABLE desenvolvimento.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_tipo_processo
    CHECK (
        btrim((tipo_processo)::text) <> ''::text
        AND (tipo_processo)::text = ANY (ARRAY[
            'CONSUMO_MATERIA_PRIMA'::character varying,
            'CONSUMO_QUIMICOS'::character varying,
            'RESULTADO_APONTAMENTO'::character varying,
            'SEMI_ACABADO'::character varying,
            'PRODUTO_ACABADO'::character varying,
            'SEM_DESTINO_CONFIGURADO'::character varying
        ]::text[])
    );

-- Definicao copiada literalmente do Q: centro e centro_trabalho ou ambos vazios, ou
-- ambos preenchidos e sem espaco nas pontas.
ALTER TABLE desenvolvimento.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_plant_wc_consistente
    CHECK (
        (
            (btrim((centro)::text) = ''::text)
            AND (btrim((centro_trabalho)::text) = ''::text)
        )
        OR
        (
            (btrim((centro)::text) <> ''::text)
            AND (btrim((centro_trabalho)::text) <> ''::text)
            AND ((centro)::text = btrim((centro)::text))
            AND ((centro_trabalho)::text = btrim((centro_trabalho)::text))
        )
    );

-- ============================================================
-- 1. configs 5 e 6: preencher centro e centro_trabalho
--    Valores do Q: 5 -> 3007/3007045, 6 -> 3007/3007046.
--    SOMENTE estas duas colunas, mais a auditoria de alteracao.
-- ============================================================

DO $$
DECLARE
    v_afetadas bigint;
BEGIN
    UPDATE desenvolvimento.operacao_producao_configuracao c
       SET centro          = m.centro,
           centro_trabalho = m.wc,
           alterado_em     = now(),
           alterado_por    = 'MIGRATION_058_COPIA_CONFIG_Q'
      FROM (
        VALUES
          (5::bigint, '3007'::varchar, '3007045'::varchar),
          (6::bigint, '3007'::varchar, '3007046'::varchar)
      ) AS m(id, centro, wc)
     WHERE c.codigo_configuracao = m.id;

    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 2 THEN
        RAISE EXCEPTION '058 ABORTADO: esperadas 2 configs atualizadas, obtidas %.', v_afetadas;
    END IF;
    RAISE NOTICE 'CONFIGS_ATUALIZADAS=2 (5 e 6: centro e centro_trabalho)';
END
$$;

-- ============================================================
-- 2. configs 7..14: inserir com id explicito, conteudo do Q
--    A coluna e IDENTITY BY DEFAULT, logo o id explicito e aceito.
-- ============================================================

DO $$
DECLARE
    v_afetadas bigint;
BEGIN
    INSERT INTO desenvolvimento.operacao_producao_configuracao
    (
        codigo_configuracao,
        centro,
        tipo_ordem,
        sequencia_sap,
        operacao_sap,
        suboperacao_sap,
        centro_trabalho,
        tipo_processo,
        tela_destino,
        exige_operacao_anterior,
        ativo,
        criado_em,
        criado_por
    )
    VALUES
      -- GATE_048_G_DATA_03 no Q
      ( 7, '3007', '', '', '0070', '', '3007047', 'CONSUMO_MATERIA_PRIMA', 'ProcessoConsumoMaterialForm',     true,  true,  now(), 'GATE_048_G_DATA_03'),
      ( 8, '3007', '', '', '0090', '', '3007001', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true,  now(), 'GATE_048_G_DATA_03'),
      ( 9, '3007', '', '', '0100', '', '3007005', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true,  now(), 'GATE_048_G_DATA_03'),
      -- A config 10 esta INATIVA no Q, com centro e centro_trabalho vazios. Copiada
      -- como esta, para manter a sequencia de ids alinhada com o Q.
      (10, '',     '', '', '0105', '', '',        'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  false, now(), 'GATE_048_G_DATA_03'),
      (11, '3007', '', '', '0140', '', '3007043', 'SEMI_ACABADO',          'ProcessoSemiAcabadoForm',          true,  true,  now(), 'GATE_048_G_DATA_03'),
      -- MIGRATION_055_ROUTE_PROFILE_094I no Q: e o resultado do pacote 055
      (12, '3007', '', '', '0040', '', '3007015', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true,  now(), 'MIGRATION_055_ROUTE_PROFILE_094I'),
      (13, '3007', '', '', '0080', '', '3007002', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true,  now(), 'MIGRATION_055_ROUTE_PROFILE_094I'),
      (14, '3007', '', '', '0110', '', '3007018', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true,  now(), 'MIGRATION_055_ROUTE_PROFILE_094I');

    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 8 THEN
        RAISE EXCEPTION '058 ABORTADO: esperadas 8 configs inseridas, obtidas %.', v_afetadas;
    END IF;
    RAISE NOTICE 'CONFIGS_INSERIDAS=8 (7..14)';
END
$$;

-- ============================================================
-- 3. perfis de resultado 1..7, conteudo do Q
--    criado_por: no Q sao nomes de papel (fugapet_q_owner, postgres), nao
--    marcadores de procedencia; ver o PREFLIGHT. Entra o marcador deste pacote.
-- ============================================================

DO $$
DECLARE
    v_afetadas bigint;
BEGIN
    INSERT INTO desenvolvimento.operacao_resultado_perfil
    (
        codigo_perfil_resultado,
        codigo_configuracao_rota,
        ordem_ocorrencia_workcenter,
        ativo,
        criado_em,
        criado_por
    )
    VALUES
      (1,  7, 1, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      (2,  8, 1, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      (3,  9, 1, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      (4,  9, 2, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      -- perfis 5..7 sao o resultado do 055 no Q
      (5, 12, 1, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      (6, 13, 1, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      (7, 14, 1, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q');

    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 7 THEN
        RAISE EXCEPTION '058 ABORTADO: esperados 7 perfis inseridos, obtidos %.', v_afetadas;
    END IF;
    RAISE NOTICE 'PERFIS_INSERIDOS=7 (1..7)';
END
$$;

-- ============================================================
-- 4. definicoes de resultado 1..6, conteudo do Q
--    criado_por: preservados os marcadores de procedencia do Q; so a definicao 1,
--    cujo valor no Q e o papel fugapet_q_owner, recebe o marcador deste pacote.
-- ============================================================

DO $$
DECLARE
    v_afetadas bigint;
BEGIN
    INSERT INTO desenvolvimento.operacao_resultado_definicao
    (
        codigo_definicao,
        codigo_configuracao,
        codigo_perfil_resultado,
        codigo_item,
        tipo,
        medida,
        referencia,
        ordem_exibicao,
        obrigatorio,
        ativo,
        criado_em,
        criado_por
    )
    VALUES
      (1,  7, 1, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true, now(), 'MIGRATION_058_COPIA_CONFIG_Q'),
      -- definicoes 2..4: resultado do 055 no Q
      (2, 12, 5, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true, now(), 'MIGRATION_055_ROUTE_PROFILE_094I'),
      (3, 13, 6, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true, now(), 'MIGRATION_055_ROUTE_PROFILE_094I'),
      (4, 14, 7, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true, now(), 'MIGRATION_055_ROUTE_PROFILE_094I'),
      -- definicoes 5..6: gates POSTERIORES ao 055, tambem ausentes deste repositorio
      (5,  8, 2, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true, now(), 'GATE_103P_OP1000173_0090_TEMPO'),
      (6,  9, 3, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000, 1, true, true, now(), 'GATE_103U_OP1000173_0100_TEMPO');

    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 6 THEN
        RAISE EXCEPTION '058 ABORTADO: esperadas 6 definicoes inseridas, obtidas %.', v_afetadas;
    END IF;
    RAISE NOTICE 'DEFINICOES_INSERIDAS=6 (1..6)';
END
$$;

-- ============================================================
-- 5. Avancar as tres sequences de identity, para alem do maior id inserido.
--    Sem isso o proximo INSERT sem id explicito colidiria com a PK.
--    setval so para FRENTE: se a sequence ja estiver adiante, nada muda.
-- ============================================================

DO $$
DECLARE
    v_seq      text;
    v_max      bigint;
    v_atual    bigint;
    r          record;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_producao_configuracao', 'codigo_configuracao'),
            ('operacao_resultado_perfil',      'codigo_perfil_resultado'),
            ('operacao_resultado_definicao',   'codigo_definicao')
        ) AS t(tabela, coluna)
    LOOP
        v_seq := pg_get_serial_sequence('desenvolvimento.' || r.tabela, r.coluna);
        IF v_seq IS NULL THEN
            RAISE EXCEPTION '058 ABORTADO: sequence de %.% nao encontrada.', r.tabela, r.coluna;
        END IF;

        EXECUTE format('SELECT max(%I) FROM desenvolvimento.%I', r.coluna, r.tabela) INTO v_max;
        EXECUTE format('SELECT last_value FROM %s', v_seq) INTO v_atual;

        IF v_max IS NOT NULL AND v_max > v_atual THEN
            PERFORM setval(v_seq, v_max, true);
            RAISE NOTICE 'SEQUENCE %: % -> %', r.tabela, v_atual, v_max;
        ELSE
            RAISE NOTICE 'SEQUENCE %: mantida em % (max=%)', r.tabela, v_atual, coalesce(v_max, 0);
        END IF;
    END LOOP;
END
$$;

-- ============================================================
-- 6. Indice unico parcial que o Q tem e o DEV nao tinha.
--    Criado DEPOIS do DML, de proposito: assim ele e validado contra o dado real.
-- ============================================================

CREATE UNIQUE INDEX uq_operacao_config_ativa_plant_wc
    ON desenvolvimento.operacao_producao_configuracao
    USING btree (centro, centro_trabalho)
    WHERE ((ativo IS TRUE) AND (btrim((centro)::text) <> ''::text) AND (btrim((centro_trabalho)::text) <> ''::text));

-- ============================================================
-- 7. POST-CHECKS FAIL-CLOSED
-- ============================================================

DO $$
DECLARE
    v_n   bigint;
    v_ids bigint[];
BEGIN
    SELECT array_agg(codigo_configuracao ORDER BY codigo_configuracao)
      INTO v_ids FROM desenvolvimento.operacao_producao_configuracao;
    IF v_ids IS DISTINCT FROM ARRAY[5,6,7,8,9,10,11,12,13,14]::bigint[] THEN
        RAISE EXCEPTION '058 POSTCHECK: configs resultantes = %, esperado 5..14.', array_to_string(v_ids, ',');
    END IF;

    SELECT array_agg(codigo_perfil_resultado ORDER BY codigo_perfil_resultado)
      INTO v_ids FROM desenvolvimento.operacao_resultado_perfil;
    IF v_ids IS DISTINCT FROM ARRAY[1,2,3,4,5,6,7]::bigint[] THEN
        RAISE EXCEPTION '058 POSTCHECK: perfis resultantes = %, esperado 1..7.', array_to_string(v_ids, ',');
    END IF;

    SELECT array_agg(codigo_definicao ORDER BY codigo_definicao)
      INTO v_ids FROM desenvolvimento.operacao_resultado_definicao;
    IF v_ids IS DISTINCT FROM ARRAY[1,2,3,4,5,6]::bigint[] THEN
        RAISE EXCEPTION '058 POSTCHECK: definicoes resultantes = %, esperado 1..6.', array_to_string(v_ids, ',');
    END IF;

    -- Integridade normalizada: nenhum perfil sem rota, nenhuma definicao sem perfil.
    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_resultado_perfil p
      LEFT JOIN desenvolvimento.operacao_producao_configuracao c
        ON c.codigo_configuracao = p.codigo_configuracao_rota
     WHERE c.codigo_configuracao IS NULL;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 POSTCHECK: % perfis sem rota.', v_n;
    END IF;

    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_resultado_definicao d
      LEFT JOIN desenvolvimento.operacao_resultado_perfil p
        ON p.codigo_perfil_resultado = d.codigo_perfil_resultado
     WHERE d.codigo_perfil_resultado IS NOT NULL
       AND p.codigo_perfil_resultado IS NULL;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 POSTCHECK: % definicoes sem perfil.', v_n;
    END IF;

    -- Nenhuma config ativa pode repetir Plant+WorkCenter (o indice novo garante,
    -- mas a contagem fica como evidencia).
    SELECT count(*) INTO v_n
      FROM (
        SELECT centro, centro_trabalho
          FROM desenvolvimento.operacao_producao_configuracao
         WHERE ativo IS TRUE AND btrim(centro) <> '' AND btrim(centro_trabalho) <> ''
         GROUP BY 1, 2 HAVING count(*) > 1
      ) x;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 POSTCHECK: % chaves Plant+WorkCenter duplicadas entre ativas.', v_n;
    END IF;

    -- As nove chaves longas do Q devem estar ativas aqui agora.
    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE ativo IS TRUE AND centro = '3007'
       AND centro_trabalho IN ('3007015','3007045','3007046','3007047','3007002',
                               '3007001','3007005','3007018','3007043');
    IF v_n <> 9 THEN
        RAISE EXCEPTION '058 POSTCHECK: esperadas 9 chaves Plant+WorkCenter ativas, encontradas %.', v_n;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname = 'desenvolvimento' AND indexname = 'uq_operacao_config_ativa_plant_wc'
    ) THEN
        RAISE EXCEPTION '058 POSTCHECK: indice uq_operacao_config_ativa_plant_wc ausente.';
    END IF;

    -- O runtime segue sem escrita nas duas tabelas do 057.
    IF has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_perfil', 'INSERT')
       OR has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_definicao', 'INSERT') THEN
        RAISE EXCEPTION '058 POSTCHECK: runtime recebeu INSERT; esperado somente SELECT.';
    END IF;

    RAISE NOTICE 'POSTCHECK=OK  CONFIGS=10  PERFIS=7  DEFINICOES=6  CHAVES_PLANT_WC=9  INDICE=presente';
END
$$;

COMMIT;

\echo '============================================================'
\echo '058_PROPOSTA_APLICADA'
\echo '============================================================'
\echo 'CONFIGS_ATUALIZADAS=2 (5 e 6)'
\echo 'CONFIGS_INSERIDAS=8 (7..14)'
\echo 'PERFIS_INSERIDOS=7'
\echo 'DEFINICOES_INSERIDAS=6'
\echo 'INDICE_CRIADO=uq_operacao_config_ativa_plant_wc'
\echo 'DELETE=0'
\echo 'SAP_WRITE=0'
\echo 'EFEITO_NO_055=definitivamente inaplicavel; o 058 o supera'
