-- ============================================================
-- 002_validar_banco_desenvolvimento_v1_1.sql
-- Executar após o 000 consolidado e o bootstrap do administrador.
-- O script não altera dados: apenas valida e consulta.
-- ============================================================

SET search_path TO desenvolvimento;

DO $$
DECLARE
    v_precision integer;
    v_scale integer;
    v_total integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento') THEN
        RAISE EXCEPTION 'Schema desenvolvimento nao existe.';
    END IF;

    SELECT numeric_precision, numeric_scale
      INTO v_precision, v_scale
      FROM information_schema.columns
     WHERE table_schema = 'desenvolvimento'
       AND table_name = 'tara'
       AND column_name = 'peso_kg';

    IF v_precision IS NULL OR v_precision <> 14 OR v_scale <> 3 THEN
        RAISE EXCEPTION 'tara.peso_kg deve ser numeric(14,3). Encontrado: precision %, scale %.', v_precision, v_scale;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'tara'
           AND column_name = 'peso_grama'
    ) THEN
        RAISE EXCEPTION 'Coluna obsoleta tara.peso_grama encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'sap_pedido_compra'
           AND column_name = 'tipo_pedido'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra.tipo_pedido ausente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'sap_pedido_compra_item'
           AND column_name = 'peso_item'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra_item.peso_item ausente.';
    END IF;

    SELECT count(*) INTO v_total
      FROM information_schema.tables
     WHERE table_schema = 'desenvolvimento'
       AND table_name IN (
           'pesagem_entrada_item',
           'pesagem_entrada_item_leitura',
           'norma_embalagem_cache',
           'norma_embalagem_item_cache',
           'hu_caixa',
           'hu_palete',
           'hu_palete_item',
           'integracao_sap_int012_log',
           'versao_banco'
       );

    IF v_total <> 9 THEN
        RAISE EXCEPTION 'Objetos principais incompletos. Esperado 9 tabelas, encontradas %.', v_total;
    END IF;

    SELECT count(*) INTO v_total
      FROM information_schema.views
     WHERE table_schema = 'desenvolvimento'
       AND table_name IN (
           'vw_normas_embalagem_cache_status',
           'vw_hu_caixas_sem_palete',
           'vw_hu_palete_com_caixas',
           'vw_integracao_int012_erros',
           'vw_integracao_int012_reprocessar',
           'vw_paletizacao_resumo'
       );

    IF v_total <> 6 THEN
        RAISE EXCEPTION 'Views INT012 incompletas. Esperado 6, encontradas %.', v_total;
    END IF;
END $$;


-- Confirma ambiente lógico, nome do sistema e versão.
DO $$
DECLARE
    v_ambiente text;
    v_sistema_nome text;
    v_versao text;
BEGIN
    SELECT valor
      INTO v_ambiente
      FROM desenvolvimento.configuracao_geral
     WHERE chave = 'AMBIENTE_BANCO'
       AND situacao_configuracao_geral = true;

    IF v_ambiente IS DISTINCT FROM 'DESENVOLVIMENTO' THEN
        RAISE EXCEPTION 'Ambiente incorreto. Esperado DESENVOLVIMENTO, encontrado %.', v_ambiente;
    END IF;

    SELECT valor
      INTO v_sistema_nome
      FROM desenvolvimento.configuracao_geral
     WHERE chave = 'SISTEMA_NOME'
       AND situacao_configuracao_geral = true;

    IF v_sistema_nome IS DISTINCT FROM 'Fuga Couros - Desenvolvimento Local' THEN
        RAISE EXCEPTION 'Nome do sistema incorreto. Esperado Fuga Couros - Desenvolvimento Local, encontrado %.', v_sistema_nome;
    END IF;

    SELECT versao
      INTO v_versao
      FROM desenvolvimento.versao_banco
     ORDER BY aplicado_em DESC, codigo_versao_banco DESC
     LIMIT 1;

    IF v_versao IS DISTINCT FROM '1.1' THEN
        RAISE EXCEPTION 'Versao incorreta. Esperado 1.1, encontrado %.', v_versao;
    END IF;
END $$;

-- Resumo de objetos por tipo.
SELECT 'TABELAS' AS tipo, count(*) AS quantidade
FROM information_schema.tables
WHERE table_schema = 'desenvolvimento'
UNION ALL
SELECT 'VIEWS', count(*)
FROM information_schema.views
WHERE table_schema = 'desenvolvimento'
UNION ALL
SELECT 'FUNCOES', count(*)
FROM information_schema.routines
WHERE routine_schema = 'desenvolvimento';

-- Confirma padrão da tara.
SELECT
    column_name,
    data_type,
    numeric_precision,
    numeric_scale,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'desenvolvimento'
  AND table_name = 'tara'
  AND column_name = 'peso_kg';

-- Confirma versão aplicada.
SELECT *
FROM desenvolvimento.versao_banco
ORDER BY aplicado_em DESC, codigo_versao_banco DESC;

-- Confirma administrador e perfil sem expor o hash completo.
SELECT
    u.login_usuario,
    length(u.senha_hash) AS tamanho_hash,
    left(u.senha_hash, 4) AS prefixo_hash,
    u.deve_trocar_senha,
    u.bloqueado_usuario,
    u.situacao_usuario,
    string_agg(pa.nome_perfil_acesso, ', ' ORDER BY pa.nome_perfil_acesso) AS perfis
FROM desenvolvimento.usuario u
LEFT JOIN desenvolvimento.usuario_perfil up
       ON up.codigo_usuario = u.codigo_usuario
      AND up.situacao_usuario_perfil = true
LEFT JOIN desenvolvimento.perfil_acesso pa
       ON pa.codigo_perfil_acesso = up.codigo_perfil_acesso
WHERE lower(trim(u.login_usuario)) = 'admin'
GROUP BY
    u.login_usuario,
    u.senha_hash,
    u.deve_trocar_senha,
    u.bloqueado_usuario,
    u.situacao_usuario;

-- Permissões complementares esperadas.
SELECT
    p.modulo_permissao,
    p.rotina_permissao,
    p.acao_permissao,
    count(pp.codigo_perfil_permissao) AS total_vinculos_ativos
FROM desenvolvimento.permissao p
LEFT JOIN desenvolvimento.perfil_permissao pp
       ON pp.codigo_permissao = p.codigo_permissao
      AND pp.situacao_perfil_permissao = true
WHERE p.situacao_permissao = true
  AND (
      (p.modulo_permissao = 'ETIQUETA' AND p.rotina_permissao IN ('CAMPO_ETIQUETA', 'MAPEAMENTO_CAMPO_ETIQUETA'))
      OR (p.modulo_permissao = 'PROCESSO_PRODUCAO' AND p.rotina_permissao = 'LEITURA_PRODUCAO' AND p.acao_permissao = 'FINALIZAR')
  )
GROUP BY p.modulo_permissao, p.rotina_permissao, p.acao_permissao
ORDER BY p.modulo_permissao, p.rotina_permissao, p.acao_permissao;

-- Views principais devem abrir sem erro; em banco novo o retorno vazio é normal.
SELECT * FROM desenvolvimento.vw_normas_embalagem_cache_status LIMIT 1;
SELECT * FROM desenvolvimento.vw_hu_caixas_sem_palete LIMIT 1;
SELECT * FROM desenvolvimento.vw_hu_palete_com_caixas LIMIT 1;
SELECT * FROM desenvolvimento.vw_integracao_int012_erros LIMIT 1;
SELECT * FROM desenvolvimento.vw_integracao_int012_reprocessar LIMIT 1;
SELECT * FROM desenvolvimento.vw_paletizacao_resumo LIMIT 1;

-- ============================================================
-- Validações adicionais DESENVOLVIMENTO V1.1 - ciclos 018 a 022
-- ============================================================

DO $$
DECLARE
    v_integracao text;
    v_qtd_permissoes_entrada integer;
BEGIN
    SELECT valor
      INTO v_integracao
      FROM desenvolvimento.configuracao_geral
     WHERE upper(trim(chave)) = 'INTEGRACAO_SAP_ATIVA'
       AND situacao_configuracao_geral = true
     ORDER BY codigo_configuracao_geral DESC
     LIMIT 1;

    IF v_integracao IS DISTINCT FROM 'true' THEN
        RAISE EXCEPTION 'INTEGRACAO_SAP_ATIVA deveria estar true no desenvolvimento v1.1. Valor encontrado: %', v_integracao;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'log_integracao_sap'
    ) THEN
        RAISE EXCEPTION 'Tabela log_integracao_sap nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'sap_pedido_compra_item'
          AND column_name = 'centro'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra_item.centro nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'sap_pedido_compra_item'
          AND column_name = 'deposito'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra_item.deposito nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'sap_pedido_compra_item'
          AND column_name = 'grupo_material'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra_item.grupo_material nao encontrada.';
    END IF;

    SELECT COUNT(*)
      INTO v_qtd_permissoes_entrada
      FROM desenvolvimento.permissao
     WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
       AND rotina_permissao = 'ENTRADA_PRODUTO'
       AND acao_permissao IN (
            'CONSULTAR',
            'EXECUTAR',
            'FINALIZAR',
            'CANCELAR',
            'PESO_MANUAL',
            'SINCRONIZAR_CACHE',
            'ENVIAR_SAP'
       )
       AND situacao_permissao = true;

    IF v_qtd_permissoes_entrada <> 7 THEN
        RAISE EXCEPTION 'Permissoes de ENTRADA_PRODUTO incompletas. Esperado 7, encontrado %.', v_qtd_permissoes_entrada;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_lancamento'
    ) THEN
        RAISE EXCEPTION 'Tabela entrada_produto_lancamento nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_item'
    ) THEN
        RAISE EXCEPTION 'Tabela entrada_produto_item nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_pesagem'
    ) THEN
        RAISE EXCEPTION 'Tabela entrada_produto_pesagem nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_lancamento'
          AND column_name = 'status_lancamento'
    ) THEN
        RAISE EXCEPTION 'Coluna entrada_produto_lancamento.status_lancamento nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_item'
          AND column_name = 'status_item'
    ) THEN
        RAISE EXCEPTION 'Coluna entrada_produto_item.status_item nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name = 'status_pesagem'
    ) THEN
        RAISE EXCEPTION 'Coluna entrada_produto_pesagem.status_pesagem nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name = 'payload_balanca'
    ) THEN
        RAISE EXCEPTION 'Coluna entrada_produto_pesagem.payload_balanca nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'entrada_produto_pesagem'
          AND column_name = 'pesado_em'
    ) THEN
        RAISE EXCEPTION 'Coluna entrada_produto_pesagem.pesado_em nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.views
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'vw_entrada_produto_item_resumo'
    ) THEN
        RAISE EXCEPTION 'View vw_entrada_produto_item_resumo nao encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.views
        WHERE table_schema = 'desenvolvimento'
          AND table_name = 'vw_entrada_produto_lancamento_resumo'
    ) THEN
        RAISE EXCEPTION 'View vw_entrada_produto_lancamento_resumo nao encontrada.';
    END IF;
END $$;

SELECT 'DESENVOLVIMENTO V1.1 VALIDADO COM SUCESSO' AS resultado_validacao;
