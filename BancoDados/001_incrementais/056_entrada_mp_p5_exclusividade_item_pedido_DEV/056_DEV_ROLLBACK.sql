-- FUGAPET / ENTRADA MP / P5 / 056 / ROLLBACK
-- PORT DEV: variante da linha DEV, derivada do pacote Q de mesmo numero. Mudancas:
--   database fuga_jales_local_homologacao_q_v1_2 -> fuga_jales_local_desenvolvimento
--   schema   homologacao                         -> desenvolvimento
--   fugapet_q_app   -> fugapet_dev_app  (existe no DEV, rolsuper=false rolcanlogin=true)
--   fugapet_q_owner -> postgres         (no DEV tudo pertence a postgres; nao ha owner dedicado)
-- Delta de estrutura, guardas e post-checks preservados. O nome do gate Q no cabecalho
-- e mantido de proposito: registra a procedencia do pacote.
-- Reverte a proposta. So e seguro enquanto nenhum recebimento tiver usado a trava:
-- apagar a tabela com linhas descarta a prova de exclusividade ja concedida.
-- Confira a contagem na VALIDACAO antes de executar.
BEGIN;

DROP TABLE IF EXISTS desenvolvimento.entrada_mp_p5_exclusividade_item_pedido;

COMMIT;
