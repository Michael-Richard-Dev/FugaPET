\set ON_ERROR_STOP on
-- ============================================================
-- 066_catalogo_produto_centro_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 066 - VALIDACAO (SOMENTE LEITURA)
--
-- Confere o que foi criado e PROVA as regras tentando viola-las dentro de uma
-- transacao sempre desfeita. Nenhuma linha sobra.
-- ============================================================

\echo '== 066 VALIDACAO =='

\echo '-- tabela existe (esperado: t)'
SELECT to_regclass('desenvolvimento.produto_catalogo_centro') IS NOT NULL AS tabela_existe;

\echo '-- constraints (esperado: pk, uq e 2 checks)'
SELECT conname, contype
  FROM pg_constraint
 WHERE conrelid = 'desenvolvimento.produto_catalogo_centro'::regclass
   AND contype IN ('p','u','c','f')
 ORDER BY contype, conname;

\echo '-- indices (esperado: pk + uq + 2)'
SELECT indexname FROM pg_indexes
 WHERE schemaname='desenvolvimento' AND tablename='produto_catalogo_centro'
 ORDER BY indexname;

\echo '-- privilegios do papel de aplicacao'
SELECT privilege_type
  FROM information_schema.role_table_grants
 WHERE table_schema='desenvolvimento' AND table_name='produto_catalogo_centro'
   AND grantee='fugapet_dev_app'
 ORDER BY privilege_type;

\echo '-- NAO pode haver chave estrangeira para o vinculo (as duas sao independentes)'
SELECT count(*) AS fks_para_vinculo
  FROM pg_constraint
 WHERE conrelid = 'desenvolvimento.produto_catalogo_centro'::regclass
   AND contype = 'f';

\echo '-- PROVA DAS REGRAS (tudo desfeito ao final)'
BEGIN;

    SAVEPOINT s1;
    DO $prova1$
    BEGIN
        INSERT INTO desenvolvimento.produto_catalogo_centro (centro, produto)
        VALUES ('3007', 'VALIDACAO-066');
        INSERT INTO desenvolvimento.produto_catalogo_centro (centro, produto)
        VALUES ('3007', 'VALIDACAO-066');
        RAISE EXCEPTION 'FALHA: o mesmo material entrou duas vezes no mesmo centro.';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE 'OK: material repetido no mesmo centro recusado.';
    END
    $prova1$;
    ROLLBACK TO SAVEPOINT s1;

    SAVEPOINT s2;
    DO $prova2$
    BEGIN
        INSERT INTO desenvolvimento.produto_catalogo_centro (centro, produto)
        VALUES ('   ', 'VALIDACAO-066');
        RAISE EXCEPTION 'FALHA: centro em branco foi aceito.';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK: centro em branco recusado.';
    END
    $prova2$;
    ROLLBACK TO SAVEPOINT s2;

    \echo '-- o mesmo material pode existir em CENTROS diferentes (esperado: 2 linhas)'
    SAVEPOINT s3;
    INSERT INTO desenvolvimento.produto_catalogo_centro (centro, produto)
    VALUES ('3007', 'VALIDACAO-066'), ('2000', 'VALIDACAO-066');
    SELECT count(*) AS em_dois_centros
      FROM desenvolvimento.produto_catalogo_centro WHERE produto = 'VALIDACAO-066';
    ROLLBACK TO SAVEPOINT s3;

ROLLBACK;

\echo '-- nada sobrou (esperado: 0)'
SELECT count(*) AS resto
  FROM desenvolvimento.produto_catalogo_centro WHERE produto LIKE 'VALIDACAO-066%';
