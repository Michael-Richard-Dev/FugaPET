\set ON_ERROR_STOP on
-- ============================================================
-- 071_tolerancia_processo_producao_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 071 - VALIDACAO (SOMENTE LEITURA) - nao escreve nada
--
-- Prova pela definicao REAL lida do catalogo, avaliada contra tabela-verdade.
-- Nenhuma linha e inserida: um teste que escreve para depois apagar ja deixou
-- sequencia furada e, num banco com gente usando, deixaria lixo.
-- ============================================================

\echo '== 071 VALIDACAO =='

SELECT current_database() AS banco, current_user AS usuario;

\echo '-- 1. as duas tabelas existem'
SELECT CASE WHEN to_regclass('desenvolvimento.tolerancia_processo_producao') IS NULL
            THEN 'FALHOU: tolerancia_processo_producao ausente' ELSE 'OK: tolerancia existe' END AS t1,
       CASE WHEN to_regclass('desenvolvimento.liberacao_tolerancia_processo') IS NULL
            THEN 'FALHOU: liberacao_tolerancia_processo ausente' ELSE 'OK: log existe' END AS t2;

\echo '-- 2. colunas essenciais da tolerancia'
WITH esperado(coluna, tipo, nulavel) AS (
    VALUES ('codigo_tolerancia',     'bigint',  'NO'),
           ('centro',                'text',    'NO'),
           ('processo',              'text',    'NO'),
           ('material',              'text',    'NO'),
           ('percentual_tolerancia', 'numeric', 'NO'),
           ('situacao',              'boolean', 'NO')
)
SELECT e.coluna,
       CASE WHEN c.column_name IS NULL THEN 'FALHOU: ausente'
            WHEN c.data_type <> e.tipo THEN 'FALHOU: tipo ' || c.data_type
            WHEN c.is_nullable <> e.nulavel THEN 'FALHOU: nulavel ' || c.is_nullable
            ELSE 'OK' END AS veredito
  FROM esperado e
  LEFT JOIN information_schema.columns c
         ON c.table_schema='desenvolvimento'
        AND c.table_name='tolerancia_processo_producao'
        AND c.column_name=e.coluna
 ORDER BY e.coluna;

\echo '-- 3. a UNICIDADE inclui o PROCESSO (o ponto do incremento)'
SELECT CASE WHEN pg_get_constraintdef(oid) = 'UNIQUE (centro, processo, material)'
            THEN 'OK: unica por centro+processo+material'
            ELSE 'FALHOU: ' || pg_get_constraintdef(oid) END AS veredito
  FROM pg_constraint
 WHERE conrelid = to_regclass('desenvolvimento.tolerancia_processo_producao')
   AND conname = 'uq_tolerancia_processo_producao';

\echo '-- 4. os CHECK avaliados contra tabela-verdade'
DO $validacao$
DECLARE
    expr_proc text; expr_pct text; expr_tipo text;
    caso record; r boolean; falhas int := 0;
BEGIN
    SELECT pg_get_expr(conbin, conrelid) INTO expr_proc FROM pg_constraint
     WHERE conrelid = to_regclass('desenvolvimento.tolerancia_processo_producao')
       AND conname='ck_tolerancia_processo_valido';
    SELECT pg_get_expr(conbin, conrelid) INTO expr_pct FROM pg_constraint
     WHERE conrelid = to_regclass('desenvolvimento.tolerancia_processo_producao')
       AND conname='ck_tolerancia_processo_percentual';
    SELECT pg_get_expr(conbin, conrelid) INTO expr_tipo FROM pg_constraint
     WHERE conrelid = to_regclass('desenvolvimento.liberacao_tolerancia_processo')
       AND conname='ck_liberacao_processo_tipo';

    IF expr_proc IS NULL OR expr_pct IS NULL OR expr_tipo IS NULL THEN
        RAISE EXCEPTION 'FALHOU: algum CHECK nao existe.';
    END IF;
    RAISE NOTICE 'processo  : %', expr_proc;
    RAISE NOTICE 'percentual: %', expr_pct;
    RAISE NOTICE 'tipo      : %', expr_tipo;

    FOR caso IN SELECT * FROM (VALUES
        ('CONSUMO', true), ('SEMI_ACABADO', true),
        ('ENTRADA', false), ('consumo', false), ('', false)
    ) AS t(v, aceito) LOOP
        EXECUTE format('SELECT (%s)', replace(expr_proc, 'processo', quote_literal(caso.v))) INTO r;
        IF r IS DISTINCT FROM caso.aceito THEN
            falhas := falhas + 1; RAISE WARNING 'FALHOU processo %: esperado %, veio %', caso.v, caso.aceito, r;
        END IF;
    END LOOP;

    FOR caso IN SELECT * FROM (VALUES
        (0::numeric, true), (10, true), (100, true), (100.01, false), (-0.01, false)
    ) AS t(v, aceito) LOOP
        EXECUTE format('SELECT (%s)', replace(expr_pct, 'percentual_tolerancia', caso.v::text)) INTO r;
        IF r IS DISTINCT FROM caso.aceito THEN
            falhas := falhas + 1; RAISE WARNING 'FALHOU percentual %: esperado %, veio %', caso.v, caso.aceito, r;
        END IF;
    END LOOP;

    FOR caso IN SELECT * FROM (VALUES
        ('ACIMA_DO_TETO', true), ('ABAIXO_DO_PISO', true), ('QUALQUER', false)
    ) AS t(v, aceito) LOOP
        EXECUTE format('SELECT (%s)', replace(expr_tipo, 'tipo_estouro', quote_literal(caso.v))) INTO r;
        IF r IS DISTINCT FROM caso.aceito THEN
            falhas := falhas + 1; RAISE WARNING 'FALHOU tipo %: esperado %, veio %', caso.v, caso.aceito, r;
        END IF;
    END LOOP;

    IF falhas > 0 THEN RAISE EXCEPTION 'FALHOU: % caso(s) divergentes.', falhas; END IF;
    RAISE NOTICE 'OK: CHECK de processo, percentual e tipo corretos (13 casos).';
END
$validacao$;

\echo '-- 5. NENHUMA das duas tabelas pode ter coluna de senha (decisao, nao esquecimento)'
SELECT CASE WHEN EXISTS (
         SELECT 1 FROM information_schema.columns
          WHERE table_schema='desenvolvimento'
            AND table_name IN ('tolerancia_processo_producao','liberacao_tolerancia_processo')
            AND (column_name ILIKE '%senha%' OR column_name ILIKE '%password%' OR column_name ILIKE '%hash%'))
       THEN 'FALHOU: existe coluna de credencial - leia o cabecalho da PROPOSTA do 070'
       ELSE 'OK: nenhuma coluna de senha' END AS veredito;

\echo '-- 6. o 071 NAO criou permissao nova: reusa LIBERAR_TOLERANCIA do 070'
SELECT count(*) AS permissoes_liberar_tolerancia
  FROM desenvolvimento.permissao
 WHERE acao_permissao='LIBERAR_TOLERANCIA';

\echo '-- 7. a tolerancia do RECEBIMENTO (069) segue intacta e separada'
SELECT CASE WHEN to_regclass('desenvolvimento.tolerancia_recebimento_material') IS NULL
            THEN 'FALHOU: 069 sumiu' ELSE 'OK: 069 intacta' END AS veredito;

\echo '-- 8. permissoes do papel da aplicacao'
WITH alvo(tabela, privilegio, esperado) AS (
    VALUES ('desenvolvimento.tolerancia_processo_producao',  'SELECT', true),
           ('desenvolvimento.tolerancia_processo_producao',  'INSERT', true),
           ('desenvolvimento.tolerancia_processo_producao',  'UPDATE', true),
           ('desenvolvimento.liberacao_tolerancia_processo', 'SELECT', true),
           ('desenvolvimento.liberacao_tolerancia_processo', 'INSERT', true),
           -- O log NAO se apaga: DELETE aqui seria defeito, nao ausencia.
           ('desenvolvimento.liberacao_tolerancia_processo', 'DELETE', false)
)
SELECT a.tabela, a.privilegio,
       CASE WHEN has_table_privilege('fugapet_dev_app', a.tabela, a.privilegio) = a.esperado
            THEN 'OK' ELSE 'FALHOU' END AS veredito
  FROM alvo a ORDER BY a.tabela, a.privilegio;

SELECT CASE WHEN has_sequence_privilege('fugapet_dev_app','desenvolvimento.tolerancia_processo_producao_codigo_tolerancia_seq','USAGE')
            AND has_sequence_privilege('fugapet_dev_app','desenvolvimento.liberacao_tolerancia_processo_codigo_liberacao_seq','USAGE')
            THEN 'OK: USAGE nas duas sequencias' ELSE 'FALHOU: falta USAGE em alguma sequencia' END AS veredito;

\echo '== Fim da VALIDACAO. Nada foi escrito. =='
