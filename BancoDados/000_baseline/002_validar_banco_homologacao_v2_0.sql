-- ============================================================
-- 002_validar_banco_homologacao_v2_0.sql
-- Executar após o 000 consolidado e o bootstrap do administrador.
-- O script não altera dados: apenas valida e consulta.
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_precision integer;
    v_scale integer;
    v_total integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'homologacao') THEN
        RAISE EXCEPTION 'Schema homologacao nao existe.';
    END IF;

    SELECT numeric_precision, numeric_scale
      INTO v_precision, v_scale
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'tara'
       AND column_name = 'peso_kg';

    IF v_precision IS NULL OR v_precision <> 14 OR v_scale <> 3 THEN
        RAISE EXCEPTION 'tara.peso_kg deve ser numeric(14,3). Encontrado: precision %, scale %.', v_precision, v_scale;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'homologacao'
           AND table_name = 'tara'
           AND column_name = 'peso_grama'
    ) THEN
        RAISE EXCEPTION 'Coluna obsoleta tara.peso_grama encontrada.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'homologacao'
           AND table_name = 'sap_pedido_compra'
           AND column_name = 'tipo_pedido'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra.tipo_pedido ausente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'homologacao'
           AND table_name = 'sap_pedido_compra_item'
           AND column_name = 'peso_item'
    ) THEN
        RAISE EXCEPTION 'Coluna sap_pedido_compra_item.peso_item ausente.';
    END IF;

    SELECT count(*) INTO v_total
      FROM information_schema.tables
     WHERE table_schema = 'homologacao'
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
     WHERE table_schema = 'homologacao'
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

-- Resumo de objetos por tipo.
SELECT 'TABELAS' AS tipo, count(*) AS quantidade
FROM information_schema.tables
WHERE table_schema = 'homologacao'
UNION ALL
SELECT 'VIEWS', count(*)
FROM information_schema.views
WHERE table_schema = 'homologacao'
UNION ALL
SELECT 'FUNCOES', count(*)
FROM information_schema.routines
WHERE routine_schema = 'homologacao';

-- Confirma padrão da tara.
SELECT
    column_name,
    data_type,
    numeric_precision,
    numeric_scale,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'homologacao'
  AND table_name = 'tara'
  AND column_name = 'peso_kg';

-- Confirma versão aplicada.
SELECT *
FROM homologacao.versao_banco
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
FROM homologacao.usuario u
LEFT JOIN homologacao.usuario_perfil up
       ON up.codigo_usuario = u.codigo_usuario
      AND up.situacao_usuario_perfil = true
LEFT JOIN homologacao.perfil_acesso pa
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
FROM homologacao.permissao p
LEFT JOIN homologacao.perfil_permissao pp
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
SELECT * FROM homologacao.vw_normas_embalagem_cache_status LIMIT 1;
SELECT * FROM homologacao.vw_hu_caixas_sem_palete LIMIT 1;
SELECT * FROM homologacao.vw_hu_palete_com_caixas LIMIT 1;
SELECT * FROM homologacao.vw_integracao_int012_erros LIMIT 1;
SELECT * FROM homologacao.vw_integracao_int012_reprocessar LIMIT 1;
SELECT * FROM homologacao.vw_paletizacao_resumo LIMIT 1;
