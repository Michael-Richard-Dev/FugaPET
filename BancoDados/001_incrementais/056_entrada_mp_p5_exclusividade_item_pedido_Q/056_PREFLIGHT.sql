-- FUGAPET / ENTRADA MP / P5 / 056 / PREFLIGHT  (somente leitura)
-- Executar ANTES da proposta. Nenhuma linha deve surpreender.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT current_database()                               AS banco,
       current_user                                     AS papel_conectado,
       current_setting('port')                          AS porta,
       to_regclass('homologacao.entrada_mp_p5_exclusividade_item_pedido') IS NOT NULL
                                                        AS tabela_ja_existe,
       to_regclass('homologacao.entrada_produto_lancamento') IS NOT NULL
                                                        AS dependencia_lancamento_presente,
       EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_app')
                                                        AS role_app_presente;

ROLLBACK;
