-- FUGAPET / ENTRADA MP / P5 / 056 / PREFLIGHT  (somente leitura)
-- PORT DEV: variante da linha DEV, derivada do pacote Q de mesmo numero. Mudancas:
--   database fuga_jales_local_homologacao_q_v1_2 -> fuga_jales_local_desenvolvimento
--   schema   homologacao                         -> desenvolvimento
--   fugapet_q_app   -> fugapet_dev_app  (existe no DEV, rolsuper=false rolcanlogin=true)
--   fugapet_q_owner -> postgres         (no DEV tudo pertence a postgres; nao ha owner dedicado)
-- Delta de estrutura, guardas e post-checks preservados. O nome do gate Q no cabecalho
-- e mantido de proposito: registra a procedencia do pacote.
-- Executar ANTES da proposta. Nenhuma linha deve surpreender.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT current_database()                               AS banco,
       current_user                                     AS papel_conectado,
       current_setting('port')                          AS porta,
       to_regclass('desenvolvimento.entrada_mp_p5_exclusividade_item_pedido') IS NOT NULL
                                                        AS tabela_ja_existe,
       to_regclass('desenvolvimento.entrada_produto_lancamento') IS NOT NULL
                                                        AS dependencia_lancamento_presente,
       EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_dev_app')
                                                        AS role_app_presente;

ROLLBACK;
