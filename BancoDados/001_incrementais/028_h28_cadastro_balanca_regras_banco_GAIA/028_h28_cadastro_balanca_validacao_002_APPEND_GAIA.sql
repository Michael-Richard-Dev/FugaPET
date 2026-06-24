-- ============================================================
-- 028_h28_cadastro_balanca_validacao_002_APPEND_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Balanca
--
-- OBJETIVO
--   Bloco complementar para anexar/executar apos o 002_validar_banco_desenvolvimento_v1_1.sql.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
DECLARE
    v_qtd integer;
BEGIN
    IF to_regclass('desenvolvimento.balanca') IS NULL THEN
        RAISE EXCEPTION 'Falha: tabela desenvolvimento.balanca nao existe.';
    END IF;

    -- Objetos existentes/base esperados
    SELECT count(*) INTO v_qtd FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'desenvolvimento' AND c.relname = 'uq_balanca_nome_setor' AND c.relkind = 'i';
    IF v_qtd <> 1 THEN RAISE EXCEPTION 'Falha: indice uq_balanca_nome_setor nao encontrado.'; END IF;

    SELECT count(*) INTO v_qtd FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'desenvolvimento' AND c.relname = 'balanca' AND t.tgname = 'trg_balanca_log_alteracao_cadastral' AND NOT t.tgisinternal;
    IF v_qtd <> 1 THEN RAISE EXCEPTION 'Falha: trigger trg_balanca_log_alteracao_cadastral nao encontrado.'; END IF;

    SELECT count(*) INTO v_qtd FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'desenvolvimento' AND c.relname = 'balanca' AND t.tgname = 'trg_balanca_atualizado_em' AND NOT t.tgisinternal;
    IF v_qtd <> 1 THEN RAISE EXCEPTION 'Falha: trigger trg_balanca_atualizado_em nao encontrado.'; END IF;

    -- Constraints base e novas
    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_nome_nao_vazio' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_nome_nao_vazio nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_tipo_conexao' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_tipo_conexao nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_porta_tcp' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_porta_tcp nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_nome_tamanho_funcional' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_nome_tamanho_funcional nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_identificacao_local_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_identificacao_local_tamanho nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_porta_serial_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_porta_serial_tamanho nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_paridade_valores' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_paridade_valores nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_stop_bits_valores' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_stop_bits_valores nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_flow_control_valores' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_flow_control_valores nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_protocolo_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_protocolo_tamanho nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_observacao_tamanho' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_observacao_tamanho nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_tcp_ip_campos_obrigatorios' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_tcp_ip_campos_obrigatorios nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_serial_campos_obrigatorios' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_serial_campos_obrigatorios nao encontrada.'; END IF;

    PERFORM 1 FROM pg_constraint WHERE conname = 'ck_balanca_usb_identificacao_obrigatoria' AND conrelid = 'desenvolvimento.balanca'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha: constraint ck_balanca_usb_identificacao_obrigatoria nao encontrada.'; END IF;

    -- Funcao, view e trigger novos
    IF to_regprocedure('desenvolvimento.fn_balanca_dependencias_ativas(bigint)') IS NULL THEN
        RAISE EXCEPTION 'Falha: funcao fn_balanca_dependencias_ativas(bigint) nao encontrada.';
    END IF;

    IF to_regprocedure('desenvolvimento.fn_balanca_bloquear_inativacao_em_uso()') IS NULL THEN
        RAISE EXCEPTION 'Falha: funcao fn_balanca_bloquear_inativacao_em_uso() nao encontrada.';
    END IF;

    IF to_regclass('desenvolvimento.vw_balanca_diagnostico_inativacao') IS NULL THEN
        RAISE EXCEPTION 'Falha: view vw_balanca_diagnostico_inativacao nao encontrada.';
    END IF;

    SELECT count(*) INTO v_qtd
      FROM pg_trigger t
      JOIN pg_class c ON c.oid = t.tgrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'desenvolvimento'
       AND c.relname = 'balanca'
       AND t.tgname = 'trg_balanca_bloquear_inativacao_em_uso'
       AND NOT t.tgisinternal;
    IF v_qtd <> 1 THEN
        RAISE EXCEPTION 'Falha: trigger trg_balanca_bloquear_inativacao_em_uso nao encontrado.';
    END IF;

    -- Dados invalidos apos aplicacao
    SELECT count(*) INTO v_qtd
      FROM (
            SELECT codigo_setor, upper(trim(nome_balanca)) AS nome_normalizado
              FROM desenvolvimento.balanca
             WHERE situacao_balanca = true
             GROUP BY codigo_setor, upper(trim(nome_balanca))
            HAVING count(*) > 1
      ) duplicadas;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas ativas duplicadas por setor + upper(trim(nome_balanca)).', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE nome_balanca IS NULL OR char_length(trim(nome_balanca)) < 2 OR char_length(trim(nome_balanca)) > 80;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com nome fora do limite 2..80.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE identificacao_local IS NOT NULL AND char_length(trim(identificacao_local)) > 120;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com identificacao_local maior que 120.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE porta_serial IS NOT NULL AND char_length(trim(porta_serial)) > 50;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com porta_serial maior que 50.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE paridade IS NOT NULL AND upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE');
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com paridade invalida.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE stop_bits IS NOT NULL AND stop_bits NOT IN (1, 1.5, 2);
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com stop_bits invalido.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE flow_control IS NOT NULL AND length(trim(flow_control)) > 0 AND upper(trim(flow_control)) NOT IN ('NONE', 'XON_XOFF', 'RTS_CTS', 'DTR_DSR');
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com flow_control invalido.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE protocolo IS NOT NULL AND char_length(trim(protocolo)) > 50;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com protocolo maior que 50.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE observacao IS NOT NULL AND char_length(trim(observacao)) > 255;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas com observacao maior que 255.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE tipo_conexao = 'TCP_IP' AND (endereco_ip IS NULL OR porta_tcp IS NULL OR porta_tcp NOT BETWEEN 1 AND 65535);
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas TCP_IP sem endereco_ip/porta_tcp validos.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE tipo_conexao = 'SERIAL' AND (porta_serial IS NULL OR length(trim(porta_serial)) = 0 OR baud_rate IS NULL OR baud_rate <= 0 OR data_bits IS NULL OR data_bits <= 0 OR paridade IS NULL OR upper(trim(paridade)) NOT IN ('NONE', 'EVEN', 'ODD', 'MARK', 'SPACE') OR stop_bits IS NULL OR stop_bits NOT IN (1, 1.5, 2));
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas SERIAL sem campos obrigatorios validos.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.balanca WHERE tipo_conexao = 'USB' AND (identificacao_local IS NULL OR length(trim(identificacao_local)) = 0);
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % balancas USB sem identificacao_local.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.entrada_produto_pesagem epp JOIN desenvolvimento.balanca b ON b.codigo_balanca = epp.codigo_balanca WHERE epp.situacao_entrada_produto_pesagem = true AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % pesagens de entrada ativas vinculadas a balancas inativas.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.hu_caixa hc JOIN desenvolvimento.balanca b ON b.codigo_balanca = hc.codigo_balanca WHERE hc.situacao_hu_caixa = true AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % HUs caixa ativas vinculadas a balancas inativas.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.hu_caixa_pesagem hcp JOIN desenvolvimento.balanca b ON b.codigo_balanca = hcp.codigo_balanca WHERE hcp.situacao_hu_caixa_pesagem = true AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % pesagens de HU caixa ativas vinculadas a balancas inativas.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.pesagem_entrada_item pei JOIN desenvolvimento.balanca b ON b.codigo_balanca = pei.codigo_balanca WHERE pei.situacao_pesagem_entrada_item = true AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % pesagens de entrada item ativas vinculadas a balancas inativas.', v_qtd; END IF;

    SELECT count(*) INTO v_qtd FROM desenvolvimento.pesagem_entrada_item_leitura peil JOIN desenvolvimento.balanca b ON b.codigo_balanca = peil.codigo_balanca WHERE peil.situacao_pesagem_entrada_item_leitura = true AND b.situacao_balanca = false;
    IF v_qtd > 0 THEN RAISE EXCEPTION 'Falha: existem % leituras de pesagem entrada item ativas vinculadas a balancas inativas.', v_qtd; END IF;
END $$;

SELECT 'CADASTRO DE BALANCA VALIDADO COM SUCESSO' AS resultado_validacao_balanca;

SELECT *
FROM desenvolvimento.vw_balanca_diagnostico_inativacao
ORDER BY nome_setor, nome_balanca;
