\set ON_ERROR_STOP on
-- ============================================================
-- 059_permissao_habilitar_escrita_sap_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 059 - Permissao HABILITAR_ESCRITA_SAP
-- PREFLIGHT (SOMENTE LEITURA)
--
-- POR QUE ESTE INCREMENTO EXISTE
-- A cerimonia de habilitacao de escrita SAP 101 (GATE 12E-E-B / 12G-B) roda SEMPRE
-- antes do POST, em ProcessoEntradaProdutoForm e em ProcessoSemiAcabadoForm, e exige
-- a permissao PROCESSO_PRODUCAO / <rotina> / HABILITAR_ESCRITA_SAP. Essa permissao
-- NUNCA foi seedada no banco do DEV: a consulta abaixo mostra que a rotina
-- ENTRADA_PRODUTO so possui ENVIAR_SAP. O efeito pratico e que o botao "Enviar SAP
-- HML" habilita, o operador confirma o envio, e a tela responde
--   "Usuario sem permissao para habilitar a escrita SAP (HABILITAR_ESCRITA_SAP)."
-- com ZERO POST. Nao e bug de codigo: e dado de seguranca faltando.
--
-- ORIGEM DOS VALORES. Nada foi inventado. A fonte e o banco de homologacao do Q
-- (fuga_jales_local_homologacao_q_v1_2, schema homologacao), lido com o papel de
-- aplicacao fugapet_q_app, somente leitura, com autorizacao expressa do usuario.
-- O que ha la, hoje:
--   codigo 95  PROCESSO_PRODUCAO | ENTRADA_PRODUTO       | HABILITAR_ESCRITA_SAP
--              descricao: "Habilitar uma tentativa temporaria de escrita SAP 101 no ambiente Q."
--   codigo 97  PROCESSO_PRODUCAO | CONTROLE_APONTAMENTOS | HABILITAR_ESCRITA_SAP
--              descricao: NULL
--   concedidas ao perfil "Administrador" e SOMENTE a ele. O perfil "Operador de
--   Balanca" tem ENVIAR_SAP, mas NAO tem HABILITAR_ESCRITA_SAP.
--
-- DIFERENCAS DECLARADAS em relacao ao Q:
--   1. Os CODIGOS nao sao replicados. O DEV tem 95 permissoes (max = 95) e os codigos
--      95/97 do Q ja estao ocupados ou nao correspondem. Este pacote usa a sequence
--      desenvolvimento.permissao_codigo_permissao_seq. Codigo e chave tecnica; a
--      identidade da permissao e a tripla (modulo, rotina, acao), que e replicada
--      byte a byte.
--   2. A descricao do codigo 95 do Q diz "no ambiente Q". No DEV isso seria falso,
--      entao o texto e adaptado para "no ambiente DEV". E a UNICA adaptacao de
--      conteudo, e ela esta aqui declarada.
--   3. A concessao de perfil replica a decisao do Q: APENAS "Administrador".
--      Conceder a "Operador de Balanca" e uma decisao de politica que o Q NAO tomou;
--      este pacote nao a toma por conta propria. Se for desejada, e outro incremento.
--
-- ESTE ARQUIVO NAO ESCREVE NADA. Rode-o antes do PROPOSTA e leia a saida.
-- ============================================================

\echo ''
\echo '=== 059 PREFLIGHT ==='

-- ------------------------------------------------------------
-- 1. Identidade do alvo. Errar de banco aqui seria conceder
--    privilegio de escrita SAP no ambiente errado.
-- ------------------------------------------------------------
SELECT current_database() AS banco,
       current_user       AS papel,
       current_schema()   AS schema_corrente,
       version()          AS versao;

DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION
            '059 PREFLIGHT: alvo errado. Esperado fuga_jales_local_desenvolvimento, conectado em %.',
            current_database();
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento') THEN
        RAISE EXCEPTION '059 PREFLIGHT: schema desenvolvimento ausente.';
    END IF;

    RAISE NOTICE 'ALVO=OK  banco e schema conferem';
END
$$;

-- ------------------------------------------------------------
-- 2. O estado que motiva o incremento: a rotina tem ENVIAR_SAP
--    e NAO tem HABILITAR_ESCRITA_SAP.
-- ------------------------------------------------------------
\echo ''
\echo '-- permissoes atuais das rotinas envolvidas --'
SELECT codigo_permissao, modulo_permissao, rotina_permissao,
       acao_permissao, situacao_permissao
  FROM desenvolvimento.permissao
 WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
   AND rotina_permissao IN ('ENTRADA_PRODUTO', 'CONTROLE_APONTAMENTOS')
 ORDER BY rotina_permissao, acao_permissao;

\echo ''
\echo '-- HABILITAR_ESCRITA_SAP ja existe? (esperado: 0 linhas) --'
SELECT codigo_permissao, modulo_permissao, rotina_permissao
  FROM desenvolvimento.permissao
 WHERE acao_permissao = 'HABILITAR_ESCRITA_SAP';

-- ------------------------------------------------------------
-- 3. Perfis e o perfil-alvo. O PROPOSTA resolve "Administrador"
--    pelo NOME, nao por codigo fixo; aqui confirmamos que ele
--    existe, esta ativo e e unico.
-- ------------------------------------------------------------
\echo ''
\echo '-- perfis de acesso --'
SELECT codigo_perfil_acesso, nome_perfil_acesso,
       perfil_sistema, situacao_perfil_acesso
  FROM desenvolvimento.perfil_acesso
 ORDER BY codigo_perfil_acesso;

DO $$
DECLARE
    v_qtd integer;
BEGIN
    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.perfil_acesso
     WHERE nome_perfil_acesso = 'Administrador'
       AND situacao_perfil_acesso IS TRUE;

    IF v_qtd <> 1 THEN
        RAISE EXCEPTION
            '059 PREFLIGHT: esperava exatamente 1 perfil ativo chamado Administrador; encontrei %.',
            v_qtd;
    END IF;

    RAISE NOTICE 'PERFIL=OK  Administrador ativo e unico';
END
$$;

-- ------------------------------------------------------------
-- 4. Quem fica com a permissao DEPOIS. Prova que nenhum outro
--    perfil ganha escrita SAP por efeito colateral.
-- ------------------------------------------------------------
\echo ''
\echo '-- quem ja tem ENVIAR_SAP hoje (referencia; NAO sera alterado) --'
SELECT pa.nome_perfil_acesso, p.acao_permissao, pp.situacao_perfil_permissao
  FROM desenvolvimento.perfil_permissao pp
  JOIN desenvolvimento.perfil_acesso pa
    ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
  JOIN desenvolvimento.permissao p
    ON p.codigo_permissao = pp.codigo_permissao
 WHERE p.acao_permissao LIKE '%SAP%'
 ORDER BY pa.nome_perfil_acesso, p.acao_permissao;

-- ------------------------------------------------------------
-- 5. A sequence precisa estar a frente do maior codigo em uso.
--    Linhas inseridas fora dela no passado deixam a sequence
--    atrasada e o INSERT falharia por PK duplicada.
-- ------------------------------------------------------------
\echo ''
\echo '-- sequences x maior codigo em uso --'
SELECT 'permissao' AS tabela,
       (SELECT max(codigo_permissao) FROM desenvolvimento.permissao) AS maior_em_uso,
       last_value AS sequence_last_value,
       is_called  AS sequence_is_called
  FROM desenvolvimento.permissao_codigo_permissao_seq
UNION ALL
SELECT 'perfil_permissao',
       (SELECT max(codigo_perfil_permissao) FROM desenvolvimento.perfil_permissao),
       last_value, is_called
  FROM desenvolvimento.perfil_permissao_codigo_perfil_permissao_seq;

DO $$
DECLARE
    v_max   bigint;
    v_seq   bigint;
    v_called boolean;
BEGIN
    SELECT max(codigo_permissao) INTO v_max FROM desenvolvimento.permissao;
    SELECT last_value, is_called INTO v_seq, v_called
      FROM desenvolvimento.permissao_codigo_permissao_seq;

    -- Proximo valor que a sequence entregaria.
    IF (CASE WHEN v_called THEN v_seq + 1 ELSE v_seq END) <= COALESCE(v_max, 0) THEN
        RAISE EXCEPTION
            '059 PREFLIGHT: sequence de permissao atrasada (proximo valor % <= maior em uso %). '
            'Corrija com setval ANTES do PROPOSTA.',
            (CASE WHEN v_called THEN v_seq + 1 ELSE v_seq END), v_max;
    END IF;

    RAISE NOTICE 'SEQUENCE=OK  proximo valor esta a frente do maior codigo em uso';
END
$$;

\echo ''
\echo '=== 059 PREFLIGHT concluido: nada foi escrito ==='
\echo ''
