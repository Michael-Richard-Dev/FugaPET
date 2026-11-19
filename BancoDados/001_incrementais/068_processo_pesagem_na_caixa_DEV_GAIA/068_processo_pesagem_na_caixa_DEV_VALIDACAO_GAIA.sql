\set ON_ERROR_STOP on
-- ============================================================
-- 068_processo_pesagem_na_caixa_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 068 - VALIDACAO (SOMENTE LEITURA, ZERO ESCRITA)
--
-- POR QUE ESTA VALIDACAO NAO ESCREVE NADA
--
-- A primeira versao provava a regra INSERINDO caixas e mudando o status delas. Passava
-- AQUI por duas condicoes que valem so no DEV, e falhou nos dois bancos do Q:
--
--   QAS110: UPDATE_DIRETO_HU_CAIXA_BLOQUEADO_USE_FUNCOES_CONTROLADAS. No DEV o papel
--           postgres e DONO de hu_caixa e o gatilho o deixa passar; aqui nao e.
--   QAS130: SEM_CAIXA_MODELO. O banco nao tem caixa nenhuma para servir de molde.
--
-- A correcao nao foi contornar as duas coisas, e sim parar de precisar delas: O
-- PREDICADO DO INDICE E A REGRA. Esta validacao LE o predicado real do catalogo e o
-- avalia contra todas as combinacoes de processo x status. Nao insere, nao atualiza,
-- nao depende de dado existente nem de ser dono da tabela — e, por ler o predicado do
-- banco em vez de uma copia escrita aqui, nao pode divergir do que esta aplicado.
-- ============================================================

\echo '== 068 VALIDACAO =='

SELECT current_database() AS banco;

\echo '-- a coluna existe, com CHECK (esperado: t e 1)'
SELECT EXISTS (
    SELECT 1 FROM information_schema.columns
     WHERE table_schema='desenvolvimento' AND table_name='hu_caixa' AND column_name='processo_pesagem'
) AS coluna_existe;
SELECT count(*) AS checks_do_processo FROM pg_constraint
 WHERE conrelid='desenvolvimento.hu_caixa'::regclass AND conname='ck_hu_caixa_processo_pesagem';

\echo '-- o indice e UNICO e PARCIAL, e conhece o processo (esperado: t t t)'
SELECT i.indisunique AS unico,
       pg_get_expr(i.indpred, i.indrelid) IS NOT NULL AS parcial,
       pg_get_expr(i.indpred, i.indrelid) ILIKE '%processo_pesagem%' AS conhece_o_processo
  FROM pg_index i
 WHERE i.indexrelid = 'desenvolvimento.uq_hu_caixa_terminal_ativo'::regclass;

\echo '-- privilegio de COLUNA do papel deste banco na coluna nova (esperado: INSERT e SELECT)'
SELECT privilege_type
  FROM information_schema.role_column_grants
 WHERE table_schema='desenvolvimento' AND table_name='hu_caixa'
   AND column_name='processo_pesagem'
   AND grantee = 'fugapet_dev_app'
 ORDER BY 1;

\echo 'caixas ja nascidas com processo registrado (0 logo apos aplicar; cresce com as novas)'
SELECT count(*) AS caixas_com_processo
  FROM desenvolvimento.hu_caixa WHERE processo_pesagem IS NOT NULL;

\echo '-- PROVA DA REGRA: o predicado REAL do indice, avaliado combinacao a combinacao'
DO $prova$
DECLARE
    v_predicado text;
    v_sql       text;
    v_erros     integer;
    v_linhas    text;
BEGIN
    SELECT pg_get_expr(i.indpred, i.indrelid) INTO v_predicado
      FROM pg_index i
     WHERE i.indexrelid = 'desenvolvimento.uq_hu_caixa_terminal_ativo'::regclass;

    IF v_predicado IS NULL THEN
        RAISE EXCEPTION 'FALHA: o indice nao e parcial; nao ha predicado para avaliar.';
    END IF;

    -- "ocupa" = a linha entra no indice e portanto TOMA o terminal, impedindo outra.
    -- Caixa a caixa (e processo nulo): ocupa tudo que nao esta confirmado nem cancelado.
    -- Envio unico: ocupa so o que exige decisao humana na balanca ou no envio.
    v_sql := format($f$
        SELECT count(*) FILTER (WHERE (%1$s) IS DISTINCT FROM esperado),
               string_agg(
                   format('processo=%%s status=%%s esperado=%%s obtido=%%s',
                          COALESCE(processo_pesagem,'<nulo>'), status_hu_caixa,
                          esperado, (%1$s)),
                   E'\n') FILTER (WHERE (%1$s) IS DISTINCT FROM esperado)
          FROM (VALUES
              -- CAIXA_A_CAIXA: regra historica, intocada
              ('CAIXA_A_CAIXA'::varchar, 'EM_PESAGEM'::varchar,                 true),
              ('CAIXA_A_CAIXA',          'FINALIZADA_LOCAL',                    true),
              ('CAIXA_A_CAIXA',          'PREVIEW_HU_GERADO',                   true),
              ('CAIXA_A_CAIXA',          'AGUARDANDO_AUTORIZACAO_SAP',          true),
              ('CAIXA_A_CAIXA',          'PRONTA_PARA_ENVIO',                   true),
              ('CAIXA_A_CAIXA',          'ENVIANDO_SAP',                        true),
              ('CAIXA_A_CAIXA',          'ERRO_SAP',                            true),
              ('CAIXA_A_CAIXA',          'INDETERMINADO_TIMEOUT',               true),
              ('CAIXA_A_CAIXA',          'BLOQUEADA',                           true),
              ('CAIXA_A_CAIXA',          'CONFIRMADA_SAP',                      false),
              ('CAIXA_A_CAIXA',          'CANCELADA',                           false),

              -- Processo NAO REGISTRADO: tratado como caixa a caixa
              (NULL,                     'AGUARDANDO_AUTORIZACAO_SAP',          true),
              (NULL,                     'CONFIRMADA_SAP',                      false),

              -- ENVIO_UNICO: pendente NAO ocupa; so o que exige decisao humana
              ('ENVIO_UNICO',            'EM_PESAGEM',                          true),
              ('ENVIO_UNICO',            'ENVIANDO_SAP',                        true),
              ('ENVIO_UNICO',            'FINALIZADA_LOCAL',                    false),
              ('ENVIO_UNICO',            'PREVIEW_HU_GERADO',                   false),
              ('ENVIO_UNICO',            'AGUARDANDO_AUTORIZACAO_SAP',          false),
              ('ENVIO_UNICO',            'PRONTA_PARA_ENVIO',                   false),
              ('ENVIO_UNICO',            'CONFIRMADA_SAP',                      false),
              ('ENVIO_UNICO',            'CANCELADA',                           false)
          ) AS t(processo_pesagem, status_hu_caixa, esperado)
    $f$, v_predicado);

    EXECUTE v_sql INTO v_erros, v_linhas;

    IF v_erros > 0 THEN
        RAISE EXCEPTION 'FALHA: % combinacao(oes) divergem do esperado:%s', v_erros, E'\n' || v_linhas;
    END IF;

    RAISE NOTICE 'OK: as 21 combinacoes de processo x status batem com a regra.';
    RAISE NOTICE 'OK: caixa a caixa ocupa o terminal com QUALQUER pendente (regra intocada).';
    RAISE NOTICE 'OK: envio unico NAO ocupa com pendente de envio — e o que destrava o processo.';
    RAISE NOTICE 'OK: envio unico OCUPA com caixa aberta na balanca ou envio em curso.';
END
$prova$;

\echo '-- nada foi escrito por esta validacao (esperado: 0)'
SELECT count(*) AS resto FROM desenvolvimento.hu_caixa WHERE terminal = 'VALIDACAO-068';
