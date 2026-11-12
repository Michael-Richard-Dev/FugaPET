-- FUGAPET / ENTRADA MP / P5 / 056 / ROLLBACK
-- Reverte a proposta. So e seguro enquanto nenhum recebimento tiver usado a trava:
-- apagar a tabela com linhas descarta a prova de exclusividade ja concedida.
-- Confira a contagem na VALIDACAO antes de executar.
BEGIN;

DROP TABLE IF EXISTS homologacao.entrada_mp_p5_exclusividade_item_pedido;

COMMIT;
