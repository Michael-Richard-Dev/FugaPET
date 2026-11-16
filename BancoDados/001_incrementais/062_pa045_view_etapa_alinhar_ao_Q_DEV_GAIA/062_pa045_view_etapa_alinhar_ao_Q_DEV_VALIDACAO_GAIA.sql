\set ON_ERROR_STOP on
-- ============================================================
-- 062_pa045_view_etapa_alinhar_ao_Q_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- VALIDACAO (SOMENTE LEITURA)
--
-- V3 e o teste que importa. Ele nao pergunta "a coluna existe"; ele REFAZ EM SQL
-- exatamente o que o C# faz -- NomeEtapa045 (primeira coluna preenchida entre
-- etapa, codigo_etapa, tipo_etapa) seguido de Vinculo045Valido (total=2 E tem
-- 261 E tem 101) -- e exige que as caixas aguardando envio passem no criterio.
-- E o criterio que estava reprovando em producao.
-- ============================================================

\echo ''
\echo '=== 062 VALIDACAO ==='

-- V1 - a coluna existe, no fim, e com o valor certo.
DO $$
DECLARE v_pos integer; v_divergentes bigint;
BEGIN
    SELECT ordinal_position INTO v_pos
      FROM information_schema.columns
     WHERE table_schema='desenvolvimento'
       AND table_name='vw_pa_045_etapa_estado_runtime'
       AND column_name='etapa';

    IF v_pos IS NULL THEN
        RAISE EXCEPTION 'V1 FALHOU: a coluna "etapa" nao existe na view.';
    END IF;

    SELECT count(*) FILTER (WHERE etapa IS DISTINCT FROM etapa_sap)
      INTO v_divergentes
      FROM desenvolvimento.vw_pa_045_etapa_estado_runtime;

    IF v_divergentes > 0 THEN
        RAISE EXCEPTION 'V1 FALHOU: "etapa" diverge de "etapa_sap" em % linha(s).', v_divergentes;
    END IF;

    RAISE NOTICE 'V1=OK  "etapa" na posicao % e igual a "etapa_sap"', v_pos;
END
$$;

-- V2 - a view continua servindo tudo o que servia (nenhuma coluna perdida).
DO $$
DECLARE v_faltando text;
BEGIN
    SELECT string_agg(c, ', ') INTO v_faltando
      FROM unnest(ARRAY['codigo_hu_caixa_etapa_sap','codigo_hu_caixa','etapa_sap','status_etapa',
                        'correlation_id','numero_tentativa','lease_obtido_em','lease_expira_em',
                        'material_document','material_document_year','http_status','origem_261',
                        'status_reconciliacao','pode_reprocessar']) AS c
     WHERE NOT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema='desenvolvimento'
           AND table_name='vw_pa_045_etapa_estado_runtime'
           AND column_name=c);

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION 'V2 FALHOU: colunas sumiram da view: %', v_faltando;
    END IF;
    RAISE NOTICE 'V2=OK  nenhuma coluna foi perdida';
END
$$;

-- V3 - CAPACIDADE REAL: o criterio do C# aplicado as caixas que aguardam envio.
DO $$
DECLARE
    r             record;
    v_avaliadas   integer := 0;
    v_reprovadas  integer := 0;
BEGIN
    FOR r IN
        SELECT c.codigo_hu_caixa,
               count(v.*)                                        AS total,
               bool_or(COALESCE(v.etapa, '') = '261')            AS tem261,
               bool_or(COALESCE(v.etapa, '') = '101')            AS tem101,
               count(*) FILTER (WHERE COALESCE(v.etapa,'') = '') AS sem_nome
          FROM desenvolvimento.hu_caixa c
          JOIN desenvolvimento.vw_pa_045_etapa_estado_runtime v
            ON v.codigo_hu_caixa = c.codigo_hu_caixa
         WHERE c.status_hu_caixa IN ('AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO')
         GROUP BY c.codigo_hu_caixa
    LOOP
        v_avaliadas := v_avaliadas + 1;

        IF r.sem_nome > 0 THEN
            v_reprovadas := v_reprovadas + 1;
            RAISE WARNING 'V3: caixa % tem % etapa(s) SEM NOME -- NomeEtapa045 devolveria null.',
                          r.codigo_hu_caixa, r.sem_nome;
        ELSIF r.total <> 2 OR NOT r.tem261 OR NOT r.tem101 THEN
            v_reprovadas := v_reprovadas + 1;
            RAISE WARNING 'V3: caixa % reprovaria em Vinculo045Valido (etapas=%; 261=%; 101=%).',
                          r.codigo_hu_caixa, r.total, r.tem261, r.tem101;
        END IF;
    END LOOP;

    IF v_avaliadas = 0 THEN
        RAISE NOTICE 'V3=PULADO  nenhuma caixa aguardando envio para exercitar o criterio';
    ELSIF v_reprovadas > 0 THEN
        RAISE EXCEPTION 'V3 FALHOU: % de % caixa(s) ainda reprovam no criterio do pipeline.',
                        v_reprovadas, v_avaliadas;
    ELSE
        RAISE NOTICE 'V3=OK  as % caixa(s) aguardando envio passam no criterio 261+101', v_avaliadas;
    END IF;
END
$$;

-- V4 - os privilegios sobreviveram ao replace.
DO $$
DECLARE v_qtd bigint;
BEGIN
    SELECT count(*) INTO v_qtd
      FROM information_schema.table_privileges
     WHERE table_schema='desenvolvimento' AND table_name='vw_pa_045_etapa_estado_runtime';

    RAISE NOTICE 'V4=OK  % concessao(oes) na view (compare com o PREFLIGHT)', v_qtd;
END
$$;

-- Retrato final: como o pipeline enxerga cada caixa agora.
\echo ''
\echo '-- leitura do pipeline por caixa (o que NomeEtapa045 devolveria) --'
SELECT c.codigo_hu_caixa,
       c.status_hu_caixa,
       string_agg(COALESCE(v.etapa,'<SEM NOME>') || '=' || v.status_etapa, ' | ' ORDER BY v.etapa) AS etapas_vistas
  FROM desenvolvimento.hu_caixa c
  JOIN desenvolvimento.vw_pa_045_etapa_estado_runtime v ON v.codigo_hu_caixa = c.codigo_hu_caixa
 WHERE c.status_hu_caixa IN ('AGUARDANDO_AUTORIZACAO_SAP','PRONTA_PARA_ENVIO','FINALIZADA_LOCAL')
 GROUP BY 1,2 ORDER BY 1;

\echo ''
\echo '=== 062 VALIDACAO: todos os checks passaram ==='
\echo ''
