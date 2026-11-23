\set ON_ERROR_STOP on
-- ============================================================
-- 069_tolerancia_recebimento_material_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 069 - VALIDACAO (SOMENTE LEITURA) - nao escreve nada
--
-- POR QUE ESTA VALIDACAO NAO INSERE LINHAS.
--
-- A prova obvia seria tentar gravar um percentual invalido e ver o banco recusar.
-- Ela tem tres defeitos que ja nos custaram tempo no incremento 068:
--   - ESCREVE: precisa de permissao de escrita e de limpar o que criou, e um
--     rollback mal feito deixa lixo no cadastro;
--   - NAO RODA EM TODO LUGAR: no banco do Q o usuario que valida nem sempre e dono
--     da tabela, e a escrita e barrada por motivo que nada tem a ver com a regra;
--   - PODE MENTIR: uma prova escrita a mao afirma sucesso mesmo que a restricao
--     aplicada seja diferente da que se queria.
--
-- Aqui a prova LE A DEFINICAO REAL da restricao com pg_get_constraintdef e a avalia
-- contra uma tabela-verdade. Zero escrita, roda em qualquer ambiente, e nao pode
-- divergir do que esta aplicado — porque e o que esta aplicado que esta sendo lido.
-- ============================================================

\echo '== 069 VALIDACAO =='

SELECT current_database() AS banco, current_user AS usuario;

-- ------------------------------------------------------------
-- 1. A tabela existe e tem a forma esperada
-- ------------------------------------------------------------
\echo '-- 1. estrutura'
SELECT
    CASE WHEN to_regclass('desenvolvimento.tolerancia_recebimento_material') IS NULL
         THEN 'FALHOU: tabela nao existe'
         ELSE 'OK: tabela existe'
    END AS tabela;

WITH esperado(coluna, tipo, nulavel) AS (
    VALUES ('codigo_tolerancia',     'bigint',  'NO'),
           ('centro',                'text',    'NO'),
           ('material',              'text',    'NO'),
           ('percentual_tolerancia', 'numeric', 'NO'),
           ('descricao_material',    'text',    'YES'),
           ('tipo_material',         'text',    'YES'),
           ('situacao',              'boolean', 'NO'),
           ('criado_em',             'timestamp with time zone', 'NO')
)
SELECT e.coluna,
       CASE WHEN c.column_name IS NULL THEN 'FALHOU: ausente'
            WHEN c.data_type <> e.tipo THEN 'FALHOU: tipo ' || c.data_type
            WHEN c.is_nullable <> e.nulavel THEN 'FALHOU: nulavel ' || c.is_nullable
            ELSE 'OK'
       END AS veredito
  FROM esperado e
  LEFT JOIN information_schema.columns c
         ON c.table_schema = 'desenvolvimento'
        AND c.table_name   = 'tolerancia_recebimento_material'
        AND c.column_name  = e.coluna
 ORDER BY e.coluna;

-- ------------------------------------------------------------
-- 2. O CHECK do percentual, provado pela DEFINICAO REAL
-- ------------------------------------------------------------
\echo '-- 2. CHECK do percentual avaliado contra tabela-verdade'
DO $validacao$
DECLARE
    expressao  text;
    caso       record;
    resultado  boolean;
    falhas     int := 0;
BEGIN
    SELECT pg_get_expr(conbin, conrelid)
      INTO expressao
      FROM pg_constraint
     WHERE conrelid = to_regclass('desenvolvimento.tolerancia_recebimento_material')
       AND conname  = 'ck_tolerancia_recebimento_percentual';

    IF expressao IS NULL THEN
        RAISE EXCEPTION 'FALHOU: restricao ck_tolerancia_recebimento_percentual nao existe.';
    END IF;

    RAISE NOTICE 'expressao aplicada: %', expressao;

    -- aceito = o que a regra DEVE permitir. Zero e aceito de proposito: significa
    -- exigencia exata. 100 tambem: o limite inferior vai a zero, que ainda e um
    -- numero. 101 nao, porque o limite inferior ficaria negativo.
    FOR caso IN
        SELECT * FROM (VALUES
            (-0.01::numeric, false),
            (0.00::numeric,  true),
            (0.01::numeric,  true),
            (10.00::numeric, true),
            (99.99::numeric, true),
            (100.00::numeric, true),
            (100.01::numeric, false),
            (999.99::numeric, false)
        ) AS t(percentual, aceito)
    LOOP
        EXECUTE format(
            'SELECT (%s)',
            replace(expressao, 'percentual_tolerancia', caso.percentual::text))
        INTO resultado;

        IF resultado IS DISTINCT FROM caso.aceito THEN
            falhas := falhas + 1;
            RAISE WARNING 'FALHOU: percentual % deveria ser % e a restricao disse %',
                caso.percentual, caso.aceito, resultado;
        END IF;
    END LOOP;

    IF falhas > 0 THEN
        RAISE EXCEPTION 'FALHOU: % caso(s) divergentes no CHECK do percentual.', falhas;
    END IF;

    RAISE NOTICE 'OK: CHECK do percentual aceita 0..100 e recusa fora disso (8 casos).';
END
$validacao$;

-- ------------------------------------------------------------
-- 3. Um material, uma tolerancia
-- ------------------------------------------------------------
\echo '-- 3. unicidade (centro, material)'
SELECT CASE
         WHEN EXISTS (
                SELECT 1 FROM pg_constraint
                 WHERE conrelid = to_regclass('desenvolvimento.tolerancia_recebimento_material')
                   AND contype  = 'u'
                   AND pg_get_constraintdef(oid) = 'UNIQUE (centro, material)')
           THEN 'OK: UNIQUE (centro, material)'
         ELSE 'FALHOU: unicidade ausente ou com outra forma'
       END AS veredito;

-- ------------------------------------------------------------
-- 4. A aplicacao consegue usar a tabela
-- ------------------------------------------------------------
\echo '-- 4. permissoes do papel da aplicacao'
WITH necessarias(privilegio) AS (
    VALUES ('SELECT'), ('INSERT'), ('UPDATE'), ('DELETE')
)
SELECT n.privilegio,
       CASE WHEN has_table_privilege('fugapet_dev_app',
                                     'desenvolvimento.tolerancia_recebimento_material',
                                     n.privilegio)
            THEN 'OK' ELSE 'FALHOU: faltando' END AS veredito
  FROM necessarias n
 ORDER BY n.privilegio;

\echo '-- 4b. sequencia (sem ela o INSERT falha mesmo com INSERT na tabela)'
SELECT CASE
         WHEN has_sequence_privilege(
                'fugapet_dev_app',
                'desenvolvimento.tolerancia_recebimento_material_codigo_tolerancia_seq',
                'USAGE')
           THEN 'OK: USAGE na sequencia'
         ELSE 'FALHOU: sem USAGE na sequencia'
       END AS veredito;

\echo '== Fim da VALIDACAO. Nada foi escrito. =='
