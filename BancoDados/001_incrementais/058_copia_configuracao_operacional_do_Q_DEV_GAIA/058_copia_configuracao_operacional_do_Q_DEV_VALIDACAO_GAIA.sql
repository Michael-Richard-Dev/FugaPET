\set ON_ERROR_STOP on
-- ============================================================
-- 058_copia_configuracao_operacional_do_Q_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 058 - Validacao pos-aplicacao (SOMENTE LEITURA)
--
-- Adjudica MIGRATION_058_VALID por comparacao BIDIRECIONAL (EXCEPT nos dois
-- sentidos) entre o que ficou no DEV e o conteudo extraido do Q, linha por linha e
-- coluna por coluna, nas colunas de NEGOCIO. As colunas de auditoria criado_em e
-- criado_por ficam de fora da comparacao de igualdade, porque o PREFLIGHT declara
-- que elas diferem de proposito; criado_por e verificado em separado, pelo que se
-- espera dele aqui.
--
--   SOMENTE LEITURA. NAO DML. NAO DDL. NAO SAP.
-- ============================================================

BEGIN TRANSACTION READ ONLY;

SET LOCAL search_path TO desenvolvimento, pg_catalog;

DO $$
DECLARE
    v_n    bigint;
    v_dif  text := '';
    v_chk  text;
    r      record;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '058 VALIDACAO: database incorreto: %', current_database();
    END IF;

    -- ================================================================
    -- A) operacao_producao_configuracao, 10 linhas, conteudo do Q
    -- ================================================================
    FOR r IN
        WITH contrato(id, centro, tipo_ordem, sequencia_sap, operacao_sap,
                      suboperacao_sap, centro_trabalho, tipo_processo, tela_destino,
                      exige, ativo) AS (
            VALUES
              -- O Q tem exige_operacao_anterior = FALSE na config 5. As outras nove sao true.
              ( 5::bigint, '3007', '', '', '0050', '', '3007045', 'CONSUMO_MATERIA_PRIMA', 'ProcessoConsumoMaterialForm',      false, true),
              ( 6::bigint, '3007', '', '', '0060', '', '3007046', 'CONSUMO_QUIMICOS',      'ProcessoConsumoMaterialForm',      true,  true),
              ( 7::bigint, '3007', '', '', '0070', '', '3007047', 'CONSUMO_MATERIA_PRIMA', 'ProcessoConsumoMaterialForm',      true,  true),
              ( 8::bigint, '3007', '', '', '0090', '', '3007001', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true),
              ( 9::bigint, '3007', '', '', '0100', '', '3007005', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true),
              (10::bigint, '',     '', '', '0105', '', '',        'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  false),
              (11::bigint, '3007', '', '', '0140', '', '3007043', 'SEMI_ACABADO',          'ProcessoSemiAcabadoForm',          true,  true),
              (12::bigint, '3007', '', '', '0040', '', '3007015', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true),
              (13::bigint, '3007', '', '', '0080', '', '3007002', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true),
              (14::bigint, '3007', '', '', '0110', '', '3007018', 'RESULTADO_APONTAMENTO', 'ProcessoResultadoApontamentoForm', true,  true)
        ),
        atual AS (
            SELECT codigo_configuracao, centro::text, tipo_ordem::text, sequencia_sap::text,
                   operacao_sap::text, suboperacao_sap::text, centro_trabalho::text,
                   tipo_processo::text, tela_destino::text,
                   exige_operacao_anterior, ativo
              FROM desenvolvimento.operacao_producao_configuracao
        )
        SELECT 'falta no DEV' AS lado, c.* FROM (SELECT * FROM contrato EXCEPT SELECT * FROM atual) c
        UNION ALL
        SELECT 'sobra no DEV' AS lado, a.* FROM (SELECT * FROM atual EXCEPT SELECT * FROM contrato) a
    LOOP
        v_dif := v_dif || format('  [config] %s: id=%s', r.lado, r.id) || chr(10);
    END LOOP;

    -- ================================================================
    -- B) operacao_resultado_perfil, 7 linhas
    -- ================================================================
    FOR r IN
        WITH contrato(p, rota, ocor, ativo) AS (
            VALUES
              (1::bigint,  7::bigint, 1, true),
              (2::bigint,  8::bigint, 1, true),
              (3::bigint,  9::bigint, 1, true),
              (4::bigint,  9::bigint, 2, true),
              (5::bigint, 12::bigint, 1, true),
              (6::bigint, 13::bigint, 1, true),
              (7::bigint, 14::bigint, 1, true)
        ),
        atual AS (
            SELECT codigo_perfil_resultado, codigo_configuracao_rota,
                   ordem_ocorrencia_workcenter, ativo
              FROM desenvolvimento.operacao_resultado_perfil
        )
        SELECT 'falta no DEV' AS lado, c.* FROM (SELECT * FROM contrato EXCEPT SELECT * FROM atual) c
        UNION ALL
        SELECT 'sobra no DEV' AS lado, a.* FROM (SELECT * FROM atual EXCEPT SELECT * FROM contrato) a
    LOOP
        v_dif := v_dif || format('  [perfil] %s: perfil=%s rota=%s ocorrencia=%s', r.lado, r.p, r.rota, r.ocor) || chr(10);
    END LOOP;

    -- ================================================================
    -- C) operacao_resultado_definicao, 6 linhas
    -- ================================================================
    FOR r IN
        WITH contrato(d, cfg, perfil, item, tipo, medida, ref, ordem, obrig, ativo) AS (
            VALUES
              (1::bigint,  7::bigint, 1::bigint, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000::numeric(18,3), 1, true, true),
              (2::bigint, 12::bigint, 5::bigint, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000::numeric(18,3), 1, true, true),
              (3::bigint, 13::bigint, 6::bigint, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000::numeric(18,3), 1, true, true),
              (4::bigint, 14::bigint, 7::bigint, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000::numeric(18,3), 1, true, true),
              (5::bigint,  8::bigint, 2::bigint, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000::numeric(18,3), 1, true, true),
              (6::bigint,  9::bigint, 3::bigint, 'TEMPO_OPERACAO', 'TEMPO', 'MINUTO', 5.000::numeric(18,3), 1, true, true)
        ),
        atual AS (
            SELECT codigo_definicao, codigo_configuracao, codigo_perfil_resultado,
                   codigo_item::text, tipo::text, medida::text, referencia,
                   ordem_exibicao, obrigatorio, ativo
              FROM desenvolvimento.operacao_resultado_definicao
        )
        SELECT 'falta no DEV' AS lado, c.* FROM (SELECT * FROM contrato EXCEPT SELECT * FROM atual) c
        UNION ALL
        SELECT 'sobra no DEV' AS lado, a.* FROM (SELECT * FROM atual EXCEPT SELECT * FROM contrato) a
    LOOP
        v_dif := v_dif || format('  [definicao] %s: def=%s cfg=%s perfil=%s', r.lado, r.d, r.cfg, r.perfil) || chr(10);
    END LOOP;

    IF v_dif <> '' THEN
        RAISE EXCEPTION
            '058 VALIDACAO: conteudo divergiu do extraido do Q. MIGRATION_058_VALID=NAO%',
            chr(10) || v_dif;
    END IF;
    RAISE NOTICE 'CONTEUDO=identico ao extraido do Q (10 configs, 7 perfis, 6 definicoes), comparacao bidirecional';

    -- ================================================================
    -- D) criado_por: marcadores de procedencia preservados
    -- ================================================================
    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao BETWEEN 7 AND 11
       AND criado_por = 'GATE_048_G_DATA_03';
    IF v_n <> 5 THEN
        RAISE EXCEPTION '058 VALIDACAO: esperadas 5 configs com procedencia GATE_048_G_DATA_03, encontradas %.', v_n;
    END IF;

    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao BETWEEN 12 AND 14
       AND criado_por = 'MIGRATION_055_ROUTE_PROFILE_094I';
    IF v_n <> 3 THEN
        RAISE EXCEPTION '058 VALIDACAO: esperadas 3 configs com procedencia do 055, encontradas %.', v_n;
    END IF;

    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_resultado_definicao
     WHERE codigo_definicao IN (5, 6)
       AND criado_por IN ('GATE_103P_OP1000173_0090_TEMPO', 'GATE_103U_OP1000173_0100_TEMPO');
    IF v_n <> 2 THEN
        RAISE EXCEPTION '058 VALIDACAO: esperadas 2 definicoes com procedencia dos gates 103P/103U, encontradas %.', v_n;
    END IF;

    -- As configs 5 e 6 mantiveram a procedencia original e ganharam a de alteracao.
    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao IN (5, 6)
       AND criado_por = 'FugaPET incremental 040 rotas consumo'
       AND alterado_por = 'MIGRATION_058_COPIA_CONFIG_Q'
       AND alterado_em IS NOT NULL;
    IF v_n <> 2 THEN
        RAISE EXCEPTION '058 VALIDACAO: configs 5 e 6 sem a auditoria esperada (criado_por original + alterado_por do 058); encontradas %.', v_n;
    END IF;
    RAISE NOTICE 'PROCEDENCIA=preservada (040, GATE_048_G_DATA_03, 055, 103P, 103U) e alteracao de 5/6 registrada';

    -- ================================================================
    -- E) Indice, integridade e sequences
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname = 'desenvolvimento'
           AND indexname = 'uq_operacao_config_ativa_plant_wc'
           AND indexdef LIKE '%WHERE%'
    ) THEN
        RAISE EXCEPTION '058 VALIDACAO: uq_operacao_config_ativa_plant_wc ausente ou sem filtro parcial. MIGRATION_058_VALID=NAO';
    END IF;
    RAISE NOTICE 'INDICE_PLANT_WC=presente e parcial';

    -- As duas CHECKs alinhadas ao Q no bloco 0B da PROPOSTA.
    SELECT pg_get_constraintdef(oid) INTO v_chk
      FROM pg_constraint
     WHERE conrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
       AND conname = 'ck_operacao_config_tipo_processo';
    IF v_chk IS NULL OR position('RESULTADO_APONTAMENTO' in v_chk) = 0 THEN
        RAISE EXCEPTION
            '058 VALIDACAO: ck_operacao_config_tipo_processo nao permite RESULTADO_APONTAMENTO. MIGRATION_058_VALID=NAO';
    END IF;
    IF position('CONSUMO_SEMI_ACABADO' in v_chk) > 0 THEN
        RAISE EXCEPTION
            '058 VALIDACAO: ck_operacao_config_tipo_processo permite CONSUMO_SEMI_ACABADO, contra o desvio declarado (a tela nao existe nesta linha). MIGRATION_058_VALID=NAO';
    END IF;
    RAISE NOTICE 'CHECK_TIPO_PROCESSO=permite RESULTADO_APONTAMENTO e nao permite CONSUMO_SEMI_ACABADO';

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.operacao_producao_configuracao'::regclass
           AND conname = 'ck_operacao_config_plant_wc_consistente'
    ) THEN
        RAISE EXCEPTION '058 VALIDACAO: ck_operacao_config_plant_wc_consistente ausente. MIGRATION_058_VALID=NAO';
    END IF;
    RAISE NOTICE 'CHECK_PLANT_WC=presente';

    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_resultado_perfil p
      LEFT JOIN desenvolvimento.operacao_producao_configuracao c
        ON c.codigo_configuracao = p.codigo_configuracao_rota
     WHERE c.codigo_configuracao IS NULL;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 VALIDACAO: % perfis sem rota. MIGRATION_058_VALID=NAO', v_n;
    END IF;

    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_resultado_definicao d
      LEFT JOIN desenvolvimento.operacao_resultado_perfil p
        ON p.codigo_perfil_resultado = d.codigo_perfil_resultado
     WHERE d.codigo_perfil_resultado IS NOT NULL
       AND p.codigo_perfil_resultado IS NULL;
    IF v_n <> 0 THEN
        RAISE EXCEPTION '058 VALIDACAO: % definicoes sem perfil. MIGRATION_058_VALID=NAO', v_n;
    END IF;
    RAISE NOTICE 'INTEGRIDADE=0 perfis sem rota, 0 definicoes sem perfil';

    -- As sequences precisam estar adiante do maior id, ou o proximo INSERT colide.
    FOR r IN
        SELECT * FROM (VALUES
            ('operacao_producao_configuracao', 'codigo_configuracao',     14::bigint),
            ('operacao_resultado_perfil',      'codigo_perfil_resultado',  7::bigint),
            ('operacao_resultado_definicao',   'codigo_definicao',         6::bigint)
        ) AS t(tabela, coluna, esperado)
    LOOP
        EXECUTE format('SELECT last_value FROM %s', pg_get_serial_sequence('desenvolvimento.' || r.tabela, r.coluna))
          INTO v_n;
        IF v_n < r.esperado THEN
            RAISE EXCEPTION
                '058 VALIDACAO: sequence de % esta em %, abaixo do maior id %; o proximo INSERT colidiria. MIGRATION_058_VALID=NAO',
                r.tabela, v_n, r.esperado;
        END IF;
    END LOOP;
    RAISE NOTICE 'SEQUENCES=adiante do maior id (14, 7, 6)';

    -- ================================================================
    -- F) O runtime continua somente com SELECT nas duas tabelas do 057
    -- ================================================================
    IF has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_perfil', 'INSERT')
       OR has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_perfil', 'UPDATE')
       OR has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_perfil', 'DELETE')
       OR has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_definicao', 'INSERT')
       OR has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_definicao', 'UPDATE')
       OR has_table_privilege('fugapet_dev_app', 'desenvolvimento.operacao_resultado_definicao', 'DELETE') THEN
        RAISE EXCEPTION '058 VALIDACAO: runtime com escrita nas tabelas de resultado. MIGRATION_058_VALID=NAO';
    END IF;
    RAISE NOTICE 'GRANT=runtime somente SELECT';

    RAISE NOTICE 'MIGRATION_058_VALID=SIM';
END
$$;

COMMIT;

\echo '058_VALIDACAO_CONCLUIDO'
\echo 'CONFIGS=10  PERFIS=7  DEFINICOES=6'
\echo 'COMPARACAO=bidirecional contra o conteudo extraido do Q'
\echo 'SAP_WRITE=0'
