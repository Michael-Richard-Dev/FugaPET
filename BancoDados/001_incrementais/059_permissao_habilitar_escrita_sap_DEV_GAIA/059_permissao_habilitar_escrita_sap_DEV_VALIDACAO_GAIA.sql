\set ON_ERROR_STOP on
-- ============================================================
-- 059_permissao_habilitar_escrita_sap_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 059 - Permissao HABILITAR_ESCRITA_SAP
-- VALIDACAO (SOMENTE LEITURA)
--
-- Cada bloco e uma afirmacao que FALHA quando falsa. Nenhum teste aqui e vacuo:
-- nao basta "a permissao existe", e preciso que ela esteja na tripla que o codigo
-- consulta, ativa, concedida ao Administrador, e NAO concedida a mais ninguem.
-- ============================================================

\echo ''
\echo '=== 059 VALIDACAO ==='

-- V1 - as duas permissoes existem, na tripla exata, ativas.
DO $$
DECLARE
    v_faltando text;
BEGIN
    SELECT string_agg(esperado.rotina, ', ') INTO v_faltando
      FROM (VALUES ('ENTRADA_PRODUTO'), ('CONTROLE_APONTAMENTOS')) AS esperado(rotina)
     WHERE NOT EXISTS (
            SELECT 1 FROM desenvolvimento.permissao p
             WHERE p.modulo_permissao = 'PROCESSO_PRODUCAO'
               AND p.rotina_permissao = esperado.rotina
               AND p.acao_permissao   = 'HABILITAR_ESCRITA_SAP'
               AND p.situacao_permissao IS TRUE);

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION 'V1 FALHOU: HABILITAR_ESCRITA_SAP ausente ou inativa nas rotinas: %.', v_faltando;
    END IF;
    RAISE NOTICE 'V1=OK  as 2 permissoes existem na tripla exata e estao ativas';
END
$$;

-- V2 - nao foram duplicadas (o PROPOSTA e idempotente; isto prova).
DO $$
DECLARE
    v_dup text;
BEGIN
    SELECT string_agg(format('%s x%s', rotina_permissao, qtd), ', ') INTO v_dup
      FROM (SELECT rotina_permissao, count(*) AS qtd
              FROM desenvolvimento.permissao
             WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
               AND acao_permissao   = 'HABILITAR_ESCRITA_SAP'
             GROUP BY rotina_permissao
            HAVING count(*) > 1) d;

    IF v_dup IS NOT NULL THEN
        RAISE EXCEPTION 'V2 FALHOU: permissao duplicada: %.', v_dup;
    END IF;
    RAISE NOTICE 'V2=OK  sem duplicatas';
END
$$;

-- V3 - o Administrador tem as duas, ativas.
DO $$
DECLARE
    v_qtd integer;
BEGIN
    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.perfil_permissao pp
      JOIN desenvolvimento.perfil_acesso pa ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
      JOIN desenvolvimento.permissao p      ON p.codigo_permissao = pp.codigo_permissao
     WHERE p.acao_permissao = 'HABILITAR_ESCRITA_SAP'
       AND pa.nome_perfil_acesso = 'Administrador'
       AND pp.situacao_perfil_permissao IS TRUE
       AND pa.situacao_perfil_acesso IS TRUE;

    IF v_qtd <> 2 THEN
        RAISE EXCEPTION 'V3 FALHOU: Administrador deveria ter 2 concessoes ativas; tem %.', v_qtd;
    END IF;
    RAISE NOTICE 'V3=OK  Administrador com as 2 concessoes ativas';
END
$$;

-- V4 - ninguem mais tem. Esta e a afirmacao de SEGURANCA do pacote.
DO $$
DECLARE
    v_outros text;
BEGIN
    SELECT string_agg(DISTINCT pa.nome_perfil_acesso, ', ') INTO v_outros
      FROM desenvolvimento.perfil_permissao pp
      JOIN desenvolvimento.perfil_acesso pa ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
      JOIN desenvolvimento.permissao p      ON p.codigo_permissao = pp.codigo_permissao
     WHERE p.acao_permissao = 'HABILITAR_ESCRITA_SAP'
       AND pa.nome_perfil_acesso <> 'Administrador';

    IF v_outros IS NOT NULL THEN
        RAISE EXCEPTION 'V4 FALHOU: outros perfis com escrita SAP: %.', v_outros;
    END IF;
    RAISE NOTICE 'V4=OK  nenhum outro perfil recebeu HABILITAR_ESCRITA_SAP';
END
$$;

-- V5 - o que ja existia continua como estava. ENVIAR_SAP nao foi tocado:
--      segue com os perfis 1 (Administrador) e 3 (Operador de Balanca).
--      A comparacao e por CODIGO, nao por nome: nome acentuado dependeria da
--      codificacao do cliente e tornaria o teste fragil pelo motivo errado.
DO $$
DECLARE
    v_enviar text;
BEGIN
    SELECT string_agg(DISTINCT pp.codigo_perfil_acesso::text, ', '
                      ORDER BY pp.codigo_perfil_acesso::text)
      INTO v_enviar
      FROM desenvolvimento.perfil_permissao pp
      JOIN desenvolvimento.permissao p ON p.codigo_permissao = pp.codigo_permissao
     WHERE p.acao_permissao = 'ENVIAR_SAP'
       AND pp.situacao_perfil_permissao IS TRUE;

    IF v_enviar IS DISTINCT FROM '1, 3' THEN
        RAISE EXCEPTION
            'V5 FALHOU: ENVIAR_SAP mudou de maos. Esperado os perfis "1, 3"; encontrado "%".',
            COALESCE(v_enviar, '<nenhum>');
    END IF;
    RAISE NOTICE 'V5=OK  ENVIAR_SAP intacta (perfis 1 e 3)';
END
$$;

-- V6 - capacidade real, nao catalogo: a consulta que o codigo faz
--      (modulo + rotina + acao) retorna linha para o perfil do usuario admin.
DO $$
DECLARE
    v_ok boolean;
BEGIN
    SELECT EXISTS (
        SELECT 1
          FROM desenvolvimento.usuario u
          JOIN desenvolvimento.usuario_perfil up ON up.codigo_usuario = u.codigo_usuario
          JOIN desenvolvimento.perfil_permissao pp ON pp.codigo_perfil_acesso = up.codigo_perfil_acesso
          JOIN desenvolvimento.permissao p ON p.codigo_permissao = pp.codigo_permissao
         WHERE u.login_usuario = 'admin'
           AND u.situacao_usuario IS TRUE
           AND up.situacao_usuario_perfil IS TRUE
           AND pp.situacao_perfil_permissao IS TRUE
           AND p.situacao_permissao IS TRUE
           AND p.modulo_permissao = 'PROCESSO_PRODUCAO'
           AND p.rotina_permissao = 'ENTRADA_PRODUTO'
           AND p.acao_permissao   = 'HABILITAR_ESCRITA_SAP')
      INTO v_ok;

    IF NOT v_ok THEN
        RAISE EXCEPTION
            'V6 FALHOU: o usuario admin continua SEM a permissao pela cadeia '
            'usuario -> perfil -> permissao. A tela voltaria a recusar o envio.';
    END IF;
    RAISE NOTICE 'V6=OK  admin resolve HABILITAR_ESCRITA_SAP pela cadeia real';
END
$$;

-- Retrato final, para o registro.
\echo ''
\echo '-- estado final das permissoes SAP --'
SELECT p.codigo_permissao, p.rotina_permissao, p.acao_permissao,
       pa.nome_perfil_acesso, pp.situacao_perfil_permissao
  FROM desenvolvimento.permissao p
  LEFT JOIN desenvolvimento.perfil_permissao pp ON pp.codigo_permissao = p.codigo_permissao
  LEFT JOIN desenvolvimento.perfil_acesso pa    ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
 WHERE p.acao_permissao LIKE '%SAP%'
 ORDER BY p.rotina_permissao, p.acao_permissao, pa.nome_perfil_acesso;

\echo ''
\echo '=== 059 VALIDACAO: todos os checks passaram ==='
\echo ''
