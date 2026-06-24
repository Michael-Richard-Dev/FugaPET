-- ============================================================
-- 028_h28_cadastro_balanca_regras_banco_PROPOSTA_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Balanca
--
-- OBJETIVO
--   Incremental proposto por Gaia Dados para endurecer regras de banco do
--   Cadastro de Balanca sem alterar diretamente a baseline V1.1.
--
-- DECISAO FUNCIONAL
--   - nome_balanca: minimo 2 e maximo 80 caracteres uteis apos trim.
--   - identificacao_local: maximo 120 caracteres uteis.
--   - endereco_ip e porta_tcp obrigatorios somente para TCP_IP.
--   - porta_serial, baud_rate, data_bits, paridade e stop_bits obrigatorios somente para SERIAL.
--   - USB exige identificacao_local.
--   - MANUAL nao exige dados fisicos.
--   - balanca em uso operacional nao pode ser inativada.
--   - aplicacao deve validar antes, mas o banco deve bloquear bypass por SQL direto.
--
-- IMPORTANTE
--   1. Proposta para revisao. NAO executar automaticamente.
--   2. Executar primeiro em DESENVOLVIMENTO.
--   3. Executar dentro de janela controlada e com backup/snapshot.
--   4. Para HML, ajustar schema para homologacao antes da aplicacao.
-- ============================================================

SET search_path TO desenvolvimento;

BEGIN;

-- ============================================================
-- 1. Preflight de dados atuais
-- ============================================================

DO $$
DECLARE
    v_qtd integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento') THEN
        RAISE EXCEPTION 'Schema desenvolvimento nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.balanca') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.balanca nao existe.';
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

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE nome_balanca IS NULL
        OR char_length(trim(nome_balanca)) < 2
        OR char_length(trim(nome_balanca)) > 80;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com nome_balanca fora do limite funcional 2..80.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE identificacao_local IS NOT NULL
       AND char_length(trim(identificacao_local)) > 120;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com identificacao_local maior que 120 caracteres.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE porta_serial IS NOT NULL
       AND char_length(trim(porta_serial)) > 50;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com porta_serial maior que 50 caracteres.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE tipo_conexao NOT IN ('SERIAL', 'TCP_IP', 'USB', 'MANUAL');
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com tipo_conexao invalido.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE paridade IS NOT NULL
       AND upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE');
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com paridade invalida.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE stop_bits IS NOT NULL
       AND stop_bits NOT IN (1, 1.5, 2);
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com stop_bits invalido.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE flow_control IS NOT NULL
       AND length(trim(flow_control)) > 0
       AND upper(trim(flow_control)) NOT IN ('NONE', 'XON_XOFF', 'RTS_CTS', 'DTR_DSR');
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com flow_control invalido.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE protocolo IS NOT NULL
       AND char_length(trim(protocolo)) > 50;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com protocolo maior que 50 caracteres.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE observacao IS NOT NULL
       AND char_length(trim(observacao)) > 255;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas com observacao maior que 255 caracteres.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE tipo_conexao = 'TCP_IP'
       AND (endereco_ip IS NULL OR porta_tcp IS NULL OR porta_tcp NOT BETWEEN 1 AND 65535);
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas TCP_IP sem endereco_ip/porta_tcp validos.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE tipo_conexao = 'SERIAL'
       AND (
              porta_serial IS NULL OR length(trim(porta_serial)) = 0
           OR baud_rate IS NULL OR baud_rate <= 0
           OR data_bits IS NULL OR data_bits <= 0
           OR paridade IS NULL OR upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE')
           OR stop_bits IS NULL OR stop_bits NOT IN (1, 1.5, 2)
       );
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas SERIAL sem campos obrigatorios validos.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.balanca
     WHERE tipo_conexao = 'USB'
       AND (identificacao_local IS NULL OR length(trim(identificacao_local)) = 0);
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas USB sem identificacao_local.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM (
            SELECT codigo_setor, upper(trim(nome_balanca)) AS nome_normalizado
              FROM desenvolvimento.balanca
             WHERE situacao_balanca = true
             GROUP BY codigo_setor, upper(trim(nome_balanca))
            HAVING count(*) > 1
      ) d;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % balancas ativas duplicadas por setor + upper(trim(nome_balanca)).', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.entrada_produto_pesagem epp
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = epp.codigo_balanca
     WHERE epp.situacao_entrada_produto_pesagem = true
       AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % pesagens de entrada ativas vinculadas a balancas inativas.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.hu_caixa hc
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = hc.codigo_balanca
     WHERE hc.situacao_hu_caixa = true
       AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % HUs de caixa ativas vinculadas a balancas inativas.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.hu_caixa_pesagem hcp
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = hcp.codigo_balanca
     WHERE hcp.situacao_hu_caixa_pesagem = true
       AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % pesagens de HU caixa ativas vinculadas a balancas inativas.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.pesagem_entrada_item pei
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = pei.codigo_balanca
     WHERE pei.situacao_pesagem_entrada_item = true
       AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % pesagens de entrada item ativas vinculadas a balancas inativas.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.pesagem_entrada_item_leitura peil
      JOIN desenvolvimento.balanca b ON b.codigo_balanca = peil.codigo_balanca
     WHERE peil.situacao_pesagem_entrada_item_leitura = true
       AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Existem % leituras de pesagem de entrada item ativas vinculadas a balancas inativas.', v_qtd;
    END IF;
END $$;

-- ============================================================
-- 2. CHECKs funcionais e tecnicos
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_nome_tamanho_funcional' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_nome_tamanho_funcional CHECK (char_length(trim(nome_balanca)) BETWEEN 2 AND 80) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_nome_tamanho_funcional;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_identificacao_local_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_identificacao_local_tamanho CHECK (identificacao_local IS NULL OR char_length(trim(identificacao_local)) <= 120) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_identificacao_local_tamanho;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_porta_serial_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_porta_serial_tamanho CHECK (porta_serial IS NULL OR char_length(trim(porta_serial)) <= 50) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_porta_serial_tamanho;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_paridade_valores' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_paridade_valores CHECK (paridade IS NULL OR upper(trim(paridade)) IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE')) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_paridade_valores;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_stop_bits_valores' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_stop_bits_valores CHECK (stop_bits IS NULL OR stop_bits IN (1, 1.5, 2)) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_stop_bits_valores;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_flow_control_valores' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_flow_control_valores CHECK (flow_control IS NULL OR length(trim(flow_control)) = 0 OR upper(trim(flow_control)) IN ('NONE', 'XON_XOFF', 'RTS_CTS', 'DTR_DSR')) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_flow_control_valores;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_protocolo_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_protocolo_tamanho CHECK (protocolo IS NULL OR char_length(trim(protocolo)) <= 50) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_protocolo_tamanho;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_observacao_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_observacao_tamanho CHECK (observacao IS NULL OR char_length(trim(observacao)) <= 255) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_observacao_tamanho;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_tcp_ip_campos_obrigatorios' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_tcp_ip_campos_obrigatorios CHECK (tipo_conexao <> 'TCP_IP' OR (endereco_ip IS NOT NULL AND porta_tcp IS NOT NULL AND porta_tcp BETWEEN 1 AND 65535)) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_tcp_ip_campos_obrigatorios;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_serial_campos_obrigatorios' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_serial_campos_obrigatorios CHECK (tipo_conexao <> 'SERIAL' OR (porta_serial IS NOT NULL AND length(trim(porta_serial)) > 0 AND baud_rate IS NOT NULL AND baud_rate > 0 AND data_bits IS NOT NULL AND data_bits > 0 AND paridade IS NOT NULL AND upper(trim(paridade)) IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE') AND stop_bits IS NOT NULL AND stop_bits IN (1, 1.5, 2))) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_serial_campos_obrigatorios;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_balanca_usb_identificacao_obrigatoria' AND conrelid = 'desenvolvimento.balanca'::regclass) THEN
        ALTER TABLE desenvolvimento.balanca ADD CONSTRAINT ck_balanca_usb_identificacao_obrigatoria CHECK (tipo_conexao <> 'USB' OR (identificacao_local IS NOT NULL AND length(trim(identificacao_local)) > 0)) NOT VALID;
        ALTER TABLE desenvolvimento.balanca VALIDATE CONSTRAINT ck_balanca_usb_identificacao_obrigatoria;
    END IF;
END $$;

COMMENT ON CONSTRAINT ck_balanca_nome_tamanho_funcional ON desenvolvimento.balanca IS 'Limite funcional: nome_balanca deve ter de 2 a 80 caracteres uteis apos trim.';
COMMENT ON CONSTRAINT ck_balanca_identificacao_local_tamanho ON desenvolvimento.balanca IS 'Limite funcional: identificacao_local deve ter ate 120 caracteres uteis apos trim.';
COMMENT ON CONSTRAINT ck_balanca_porta_serial_tamanho ON desenvolvimento.balanca IS 'Limite funcional: porta_serial deve ter ate 50 caracteres uteis apos trim.';
COMMENT ON CONSTRAINT ck_balanca_paridade_valores ON desenvolvimento.balanca IS 'Paridade permitida: NONE, EVEN, ODD, MARK ou SPACE.';
COMMENT ON CONSTRAINT ck_balanca_stop_bits_valores ON desenvolvimento.balanca IS 'Stop bits permitidos: 1, 1.5 ou 2.';
COMMENT ON CONSTRAINT ck_balanca_flow_control_valores ON desenvolvimento.balanca IS 'Flow control permitido quando preenchido: NONE, XON_XOFF, RTS_CTS ou DTR_DSR.';
COMMENT ON CONSTRAINT ck_balanca_protocolo_tamanho ON desenvolvimento.balanca IS 'Limite funcional: protocolo deve ter ate 50 caracteres uteis apos trim.';
COMMENT ON CONSTRAINT ck_balanca_observacao_tamanho ON desenvolvimento.balanca IS 'Limite funcional: observacao deve ter ate 255 caracteres uteis apos trim.';
COMMENT ON CONSTRAINT ck_balanca_tcp_ip_campos_obrigatorios ON desenvolvimento.balanca IS 'Tipo TCP_IP exige endereco_ip e porta_tcp valida entre 1 e 65535.';
COMMENT ON CONSTRAINT ck_balanca_serial_campos_obrigatorios ON desenvolvimento.balanca IS 'Tipo SERIAL exige porta_serial, baud_rate, data_bits, paridade e stop_bits validos.';
COMMENT ON CONSTRAINT ck_balanca_usb_identificacao_obrigatoria ON desenvolvimento.balanca IS 'Tipo USB exige identificacao_local.';

-- ============================================================
-- 3. Funcao de diagnostico de dependencias ativas da balanca
-- ============================================================

CREATE OR REPLACE FUNCTION desenvolvimento.fn_balanca_dependencias_ativas(p_codigo_balanca bigint)
RETURNS TABLE (
    tipo_dependencia text,
    quantidade bigint
)
LANGUAGE sql
STABLE
AS $$
    SELECT 'ENTRADA_PRODUTO_PESAGEM_ATIVA'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.entrada_produto_pesagem epp
     WHERE epp.codigo_balanca = p_codigo_balanca
       AND epp.situacao_entrada_produto_pesagem = true

    UNION ALL
    SELECT 'HU_CAIXA_ATIVA'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.hu_caixa hc
     WHERE hc.codigo_balanca = p_codigo_balanca
       AND hc.situacao_hu_caixa = true

    UNION ALL
    SELECT 'HU_CAIXA_PESAGEM_ATIVA'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.hu_caixa_pesagem hcp
     WHERE hcp.codigo_balanca = p_codigo_balanca
       AND hcp.situacao_hu_caixa_pesagem = true

    UNION ALL
    SELECT 'PESAGEM_ENTRADA_ITEM_ATIVA'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.pesagem_entrada_item pei
     WHERE pei.codigo_balanca = p_codigo_balanca
       AND pei.situacao_pesagem_entrada_item = true

    UNION ALL
    SELECT 'PESAGEM_ENTRADA_ITEM_LEITURA_ATIVA'::text AS tipo_dependencia,
           count(*)::bigint AS quantidade
      FROM desenvolvimento.pesagem_entrada_item_leitura peil
     WHERE peil.codigo_balanca = p_codigo_balanca
       AND peil.situacao_pesagem_entrada_item_leitura = true;
$$;

COMMENT ON FUNCTION desenvolvimento.fn_balanca_dependencias_ativas(bigint) IS
    'Retorna contadores de vinculos operacionais ativos que impedem inativacao logica de balanca.';

-- ============================================================
-- 4. View de diagnostico para Service/tela antes de inativar
-- ============================================================

CREATE OR REPLACE VIEW desenvolvimento.vw_balanca_diagnostico_inativacao AS
SELECT
    b.codigo_balanca,
    b.codigo_setor,
    s.nome_setor,
    b.nome_balanca,
    b.tipo_conexao,
    b.situacao_balanca,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'ENTRADA_PRODUTO_PESAGEM_ATIVA'), 0)::bigint AS entrada_produto_pesagens_ativas,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'HU_CAIXA_ATIVA'), 0)::bigint AS hu_caixas_ativas,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'HU_CAIXA_PESAGEM_ATIVA'), 0)::bigint AS hu_caixa_pesagens_ativas,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'PESAGEM_ENTRADA_ITEM_ATIVA'), 0)::bigint AS pesagem_entrada_item_ativas,
    COALESCE(sum(d.quantidade) FILTER (WHERE d.tipo_dependencia = 'PESAGEM_ENTRADA_ITEM_LEITURA_ATIVA'), 0)::bigint AS pesagem_entrada_item_leituras_ativas,
    COALESCE(sum(d.quantidade), 0)::bigint AS total_dependencias_ativas,
    (COALESCE(sum(d.quantidade), 0) = 0) AS pode_inativar
FROM desenvolvimento.balanca b
JOIN desenvolvimento.setor s ON s.codigo_setor = b.codigo_setor
LEFT JOIN LATERAL desenvolvimento.fn_balanca_dependencias_ativas(b.codigo_balanca) d ON true
GROUP BY
    b.codigo_balanca,
    b.codigo_setor,
    s.nome_setor,
    b.nome_balanca,
    b.tipo_conexao,
    b.situacao_balanca;

COMMENT ON VIEW desenvolvimento.vw_balanca_diagnostico_inativacao IS
    'Diagnostico de dependencias operacionais que impedem inativacao logica de balanca.';

-- ============================================================
-- 5. Trigger de bloqueio de inativacao logica em uso
-- ============================================================

CREATE OR REPLACE FUNCTION desenvolvimento.fn_balanca_bloquear_inativacao_em_uso()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_dependencias text;
BEGIN
    IF OLD.situacao_balanca = true AND NEW.situacao_balanca = false THEN
        SELECT string_agg(format('%s=%s', tipo_dependencia, quantidade), ', ' ORDER BY tipo_dependencia)
          INTO v_dependencias
          FROM desenvolvimento.fn_balanca_dependencias_ativas(NEW.codigo_balanca)
         WHERE quantidade > 0;

        IF v_dependencias IS NOT NULL THEN
            RAISE EXCEPTION 'Balanca "%" nao pode ser inativada porque possui dependencias operacionais ativas: %.',
                NEW.nome_balanca,
                v_dependencias
            USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION desenvolvimento.fn_balanca_bloquear_inativacao_em_uso() IS
    'Bloqueia UPDATE que tente inativar balanca com registros operacionais ativos vinculados.';

DROP TRIGGER IF EXISTS trg_balanca_bloquear_inativacao_em_uso ON desenvolvimento.balanca;

CREATE TRIGGER trg_balanca_bloquear_inativacao_em_uso
BEFORE UPDATE OF situacao_balanca ON desenvolvimento.balanca
FOR EACH ROW
EXECUTE FUNCTION desenvolvimento.fn_balanca_bloquear_inativacao_em_uso();

COMMIT;

-- ============================================================
-- Consultas pos-aplicacao sugeridas
-- ============================================================

SELECT *
FROM desenvolvimento.vw_balanca_diagnostico_inativacao
ORDER BY nome_setor, nome_balanca;
