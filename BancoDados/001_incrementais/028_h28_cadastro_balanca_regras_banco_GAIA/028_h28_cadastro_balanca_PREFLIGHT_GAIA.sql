-- ============================================================
-- 028_h28_cadastro_balanca_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Balanca
--
-- OBJETIVO
--   Validar dados atuais antes da aplicacao do incremental 028.
--   Este script nao altera dados nem cria objetos.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento') THEN
        RAISE EXCEPTION 'Schema desenvolvimento nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.balanca') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.balanca nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.setor') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.setor nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.entrada_produto_pesagem') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.entrada_produto_pesagem nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.hu_caixa') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.hu_caixa nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.hu_caixa_pesagem') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.hu_caixa_pesagem nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.pesagem_entrada_item') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.pesagem_entrada_item nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.pesagem_entrada_item_leitura') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.pesagem_entrada_item_leitura nao existe.';
    END IF;
END $$;

WITH validacoes AS (
    SELECT 'nome_balanca_fora_limite_2_80' AS validacao,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.balanca
     WHERE nome_balanca IS NULL
        OR char_length(trim(nome_balanca)) < 2
        OR char_length(trim(nome_balanca)) > 80

    UNION ALL
    SELECT 'identificacao_local_maior_120', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE identificacao_local IS NOT NULL
       AND char_length(trim(identificacao_local)) > 120

    UNION ALL
    SELECT 'porta_serial_maior_50', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE porta_serial IS NOT NULL
       AND char_length(trim(porta_serial)) > 50

    UNION ALL
    SELECT 'tipo_conexao_invalido', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE tipo_conexao NOT IN ('SERIAL', 'TCP_IP', 'USB', 'MANUAL')

    UNION ALL
    SELECT 'paridade_invalida', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE paridade IS NOT NULL
       AND upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE')

    UNION ALL
    SELECT 'stop_bits_invalido', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE stop_bits IS NOT NULL
       AND stop_bits NOT IN (1, 1.5, 2)

    UNION ALL
    SELECT 'flow_control_invalido', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE flow_control IS NOT NULL
       AND length(trim(flow_control)) > 0
       AND upper(trim(flow_control)) NOT IN ('NONE', 'XON_XOFF', 'RTS_CTS', 'DTR_DSR')

    UNION ALL
    SELECT 'protocolo_maior_50', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE protocolo IS NOT NULL
       AND char_length(trim(protocolo)) > 50

    UNION ALL
    SELECT 'observacao_maior_255', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE observacao IS NOT NULL
       AND char_length(trim(observacao)) > 255

    UNION ALL
    SELECT 'tcp_ip_sem_ip_ou_porta', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE tipo_conexao = 'TCP_IP'
       AND (endereco_ip IS NULL OR porta_tcp IS NULL OR porta_tcp NOT BETWEEN 1 AND 65535)

    UNION ALL
    SELECT 'serial_sem_campos_obrigatorios', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE tipo_conexao = 'SERIAL'
       AND (
              porta_serial IS NULL OR length(trim(porta_serial)) = 0
           OR baud_rate IS NULL OR baud_rate <= 0
           OR data_bits IS NULL OR data_bits <= 0
           OR paridade IS NULL OR upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE')
           OR stop_bits IS NULL OR stop_bits NOT IN (1, 1.5, 2)
       )

    UNION ALL
    SELECT 'usb_sem_identificacao_local', count(*)::bigint
      FROM desenvolvimento.balanca
     WHERE tipo_conexao = 'USB'
       AND (identificacao_local IS NULL OR length(trim(identificacao_local)) = 0)

    UNION ALL
    SELECT 'balancas_ativas_duplicadas_setor_nome', count(*)::bigint
      FROM (
            SELECT codigo_setor, upper(trim(nome_balanca)) AS nome_normalizado
              FROM desenvolvimento.balanca
             WHERE situacao_balanca = true
             GROUP BY codigo_setor, upper(trim(nome_balanca))
            HAVING count(*) > 1
      ) d

    UNION ALL
    SELECT 'entrada_produto_pesagem_ativa_balanca_inativa', count(*)::bigint
      FROM desenvolvimento.entrada_produto_pesagem epp
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = epp.codigo_balanca
     WHERE epp.situacao_entrada_produto_pesagem = true
       AND b.situacao_balanca = false

    UNION ALL
    SELECT 'hu_caixa_ativa_balanca_inativa', count(*)::bigint
      FROM desenvolvimento.hu_caixa hc
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = hc.codigo_balanca
     WHERE hc.situacao_hu_caixa = true
       AND b.situacao_balanca = false

    UNION ALL
    SELECT 'hu_caixa_pesagem_ativa_balanca_inativa', count(*)::bigint
      FROM desenvolvimento.hu_caixa_pesagem hcp
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = hcp.codigo_balanca
     WHERE hcp.situacao_hu_caixa_pesagem = true
       AND b.situacao_balanca = false

    UNION ALL
    SELECT 'pesagem_entrada_item_ativa_balanca_inativa', count(*)::bigint
      FROM desenvolvimento.pesagem_entrada_item pei
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = pei.codigo_balanca
     WHERE pei.situacao_pesagem_entrada_item = true
       AND b.situacao_balanca = false

    UNION ALL
    SELECT 'pesagem_entrada_item_leitura_ativa_balanca_inativa', count(*)::bigint
      FROM desenvolvimento.pesagem_entrada_item_leitura peil
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = peil.codigo_balanca
     WHERE peil.situacao_pesagem_entrada_item_leitura = true
       AND b.situacao_balanca = false
)
SELECT *
FROM validacoes
ORDER BY validacao;

DO $$
DECLARE
    v_bloqueantes bigint;
BEGIN
    WITH validacoes AS (
        SELECT count(*)::bigint AS quantidade
          FROM desenvolvimento.balanca
         WHERE nome_balanca IS NULL
            OR char_length(trim(nome_balanca)) < 2
            OR char_length(trim(nome_balanca)) > 80
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE identificacao_local IS NOT NULL AND char_length(trim(identificacao_local)) > 120
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE porta_serial IS NOT NULL AND char_length(trim(porta_serial)) > 50
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE tipo_conexao NOT IN ('SERIAL', 'TCP_IP', 'USB', 'MANUAL')
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE paridade IS NOT NULL AND upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE')
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE stop_bits IS NOT NULL AND stop_bits NOT IN (1, 1.5, 2)
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE flow_control IS NOT NULL AND length(trim(flow_control)) > 0 AND upper(trim(flow_control)) NOT IN ('NONE', 'XON_XOFF', 'RTS_CTS', 'DTR_DSR')
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE protocolo IS NOT NULL AND char_length(trim(protocolo)) > 50
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE observacao IS NOT NULL AND char_length(trim(observacao)) > 255
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE tipo_conexao = 'TCP_IP' AND (endereco_ip IS NULL OR porta_tcp IS NULL OR porta_tcp NOT BETWEEN 1 AND 65535)
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE tipo_conexao = 'SERIAL' AND (porta_serial IS NULL OR length(trim(porta_serial)) = 0 OR baud_rate IS NULL OR baud_rate <= 0 OR data_bits IS NULL OR data_bits <= 0 OR paridade IS NULL OR upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE') OR stop_bits IS NULL OR stop_bits NOT IN (1, 1.5, 2))
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.balanca WHERE tipo_conexao = 'USB' AND (identificacao_local IS NULL OR length(trim(identificacao_local)) = 0)
        UNION ALL SELECT count(*)::bigint FROM (SELECT codigo_setor, upper(trim(nome_balanca)) FROM desenvolvimento.balanca WHERE situacao_balanca = true GROUP BY codigo_setor, upper(trim(nome_balanca)) HAVING count(*) > 1) d
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.entrada_produto_pesagem epp JOIN desenvolvimento.balanca b ON b.codigo_balanca = epp.codigo_balanca WHERE epp.situacao_entrada_produto_pesagem = true AND b.situacao_balanca = false
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.hu_caixa hc JOIN desenvolvimento.balanca b ON b.codigo_balanca = hc.codigo_balanca WHERE hc.situacao_hu_caixa = true AND b.situacao_balanca = false
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.hu_caixa_pesagem hcp JOIN desenvolvimento.balanca b ON b.codigo_balanca = hcp.codigo_balanca WHERE hcp.situacao_hu_caixa_pesagem = true AND b.situacao_balanca = false
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.pesagem_entrada_item pei JOIN desenvolvimento.balanca b ON b.codigo_balanca = pei.codigo_balanca WHERE pei.situacao_pesagem_entrada_item = true AND b.situacao_balanca = false
        UNION ALL SELECT count(*)::bigint FROM desenvolvimento.pesagem_entrada_item_leitura peil JOIN desenvolvimento.balanca b ON b.codigo_balanca = peil.codigo_balanca WHERE peil.situacao_pesagem_entrada_item_leitura = true AND b.situacao_balanca = false
    )
    SELECT COALESCE(sum(quantidade), 0) INTO v_bloqueantes FROM validacoes;

    IF v_bloqueantes > 0 THEN
        RAISE EXCEPTION 'PREFLIGHT 028 BALANCA REPROVADO: existem % inconsistencias bloqueantes.', v_bloqueantes;
    END IF;
END $$;

SELECT 'PREFLIGHT 028 BALANCA APROVADO - SEM INCONSISTENCIAS BLOQUEANTES' AS resultado_preflight;
