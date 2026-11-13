\set ON_ERROR_STOP on
-- ============================================================
-- 051_operacao_producao_evento_resultado_text_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 051 - operacao_producao_evento.resultado: varchar(40) -> text
-- PREFLIGHT (SOMENTE LEITURA)
--
-- PORT DEV: variante da linha DEV, derivada do pacote Q de mesmo numero
-- (_Q_VARIANTES_GAIA_REV1/051). Mudancas, todas declaradas:
--   database  fuga_jales_local_homologacao_q_v1_2 -> fuga_jales_local_desenvolvimento
--   schema    homologacao                         -> desenvolvimento
--   principal fugapet_q_owner                     -> postgres
--       O DEV nao tem papel owner dedicado: tudo no banco pertence a postgres, e
--       o cluster so tem postgres e fugapet_dev_app. A guarda do Q que exigia
--       current_user = 'fugapet_q_owner' virou current_user = 'postgres'.
--       A guarda session_user <> papel de aplicacao foi mantida em espirito:
--       fugapet_dev_app nao pode executar DDL.
--   Os meta-comandos \gset, \if, \elif, \else, \quit do psql foram TRADUZIDOS
--       para SQL puro. Nao ha psql nesta maquina, e um pacote de migracao nao deve
--       depender de um cliente especifico. Mesma decisao ja tomada no 053 e no 058.
--       A traducao e mais segura: \quit abandonaria a sessao com a transacao
--       aberta, enquanto RAISE EXCEPTION aborta de forma deterministica.
--   Contrato, verificacoes e ordem das etapas sao os mesmos do original.
--   ACRESCIMO: um ALTER COLUMN ... SET DEFAULT ''::text depois da troca de tipo.
--       Medido nesta linha: ALTER COLUMN ... TYPE text NAO reescreve o default -- o
--       tipo vira text e o default permanece ''::character varying. Como o
--       post-check do proprio pacote Q exige ''::text, o script original abortaria
--       nesse ponto. O acrescimo fecha o contrato que a VALIDACAO do Q adjudica.
--
-- POR QUE ESTE INCREMENTO E NECESSARIO (medido nesta linha, 13/11/2026):
--   A coluna guarda o resultado da auditoria de apontamento. O maior valor que o
--   codigo pode gerar hoje e SEM_PERMISSAO_ + o nome da acao exigida. A maior acao
--   em PermissoesSistema e MAPEAMENTO_CAMPO_ETIQUETA, o que da
--   'SEM_PERMISSAO_MAPEAMENTO_CAMPO_ETIQUETA' = 39 caracteres: UM caractere abaixo
--   do limite de 40. Qualquer acao nova com 27 ou mais caracteres estoura a coluna.
--   O estouro nao seria cosmetico: parte dos eventos e gravada DENTRO da mesma
--   transacao da transicao de estado, entao o erro derrubaria uma operacao
--   legitima do chao de fabrica.
--
--   SOMENTE LEITURA. NAO DML. NAO DDL. NAO SAP.
-- ============================================================

BEGIN TRANSACTION READ ONLY;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '2min';
SET LOCAL search_path = pg_catalog, desenvolvimento;

DO $$
DECLARE
    v_contrato_exato boolean;
    v_ja_aplicado    boolean;
    v_linhas         bigint;
    v_max            text;
    v_hash           text;
    v_acima_40       bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '051 PREFLIGHT: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '051 PREFLIGHT: principal administrativo inesperado: %', current_user;
    END IF;

    IF session_user = 'fugapet_dev_app' THEN
        RAISE EXCEPTION '051 PREFLIGHT: o papel de aplicacao nao pode executar DDL.';
    END IF;

    IF to_regnamespace('desenvolvimento') IS NULL THEN
        RAISE EXCEPTION '051 PREFLIGHT: schema desenvolvimento ausente.';
    END IF;

    IF to_regclass('desenvolvimento.operacao_producao_evento') IS NULL THEN
        RAISE EXCEPTION '051 PREFLIGHT: tabela operacao_producao_evento ausente.';
    END IF;

    RAISE NOTICE 'DATABASE=%  SCHEMA=desenvolvimento  PRINCIPAL=%', current_database(), current_user;
    RAISE NOTICE 'TABELA=PRESENTE';

    -- -------- Contrato de partida: varchar(40) NOT NULL DEFAULT '' --------
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado'
           AND data_type = 'character varying'
           AND character_maximum_length = 40
           AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::character varying', '[[:space:]]', '', 'g')
    ) INTO v_contrato_exato;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'desenvolvimento'
           AND table_name = 'operacao_producao_evento'
           AND column_name = 'resultado'
           AND data_type = 'text'
           AND is_nullable = 'NO'
           AND regexp_replace(coalesce(column_default, ''), '[[:space:]]', '', 'g')
               = regexp_replace(quote_literal(''::text) || '::text', '[[:space:]]', '', 'g')
    ) INTO v_ja_aplicado;

    IF v_ja_aplicado THEN
        RAISE NOTICE 'CLASSIFICACAO=JA_APLICADO (coluna ja e text); a PROPOSTA sera no-op';
    ELSIF v_contrato_exato THEN
        RAISE NOTICE 'CLASSIFICACAO=APLICAVEL (varchar(40) NOT NULL DEFAULT '''')';
    ELSE
        RAISE EXCEPTION
            '051 PREFLIGHT: contrato atual divergente; nao e varchar(40) NOT NULL DEFAULT '''' nem text. Abortado.';
    END IF;

    -- -------- Evidencia dos dados, para o antes/depois da PROPOSTA --------
    SELECT count(*),
           coalesce(max(length(resultado))::text, '<NULL>'),
           md5(coalesce(string_agg(codigo_evento_apontamento::text || chr(31) || resultado,
                                   chr(30) ORDER BY codigo_evento_apontamento), ''))
      INTO v_linhas, v_max, v_hash
      FROM desenvolvimento.operacao_producao_evento;

    RAISE NOTICE 'LINHAS=%  MAIOR_RESULTADO=%  HASH=%', v_linhas, v_max, v_hash;

    SELECT count(*) INTO v_acima_40
      FROM desenvolvimento.operacao_producao_evento
     WHERE length(resultado) > 40;
    RAISE NOTICE 'LINHAS_ACIMA_DE_40=% (impossivel sob varchar(40); diagnostico)', v_acima_40;

    RAISE NOTICE 'MIGRATION_PRECONDITION=PASS';
    RAISE NOTICE 'NATUREZA=alargamento de tipo; nenhum valor existente pode ser truncado';
END
$$;

COMMIT;

\echo '051_PREFLIGHT_CONCLUIDO'
\echo 'CONTRATO=desenvolvimento.operacao_producao_evento.resultado varchar(40) -> text'
\echo 'DML=0'
\echo 'DDL=0'
\echo 'SAP_WRITE=0'
