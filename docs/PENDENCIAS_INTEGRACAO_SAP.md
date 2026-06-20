# Pendencias de integracao SAP

## Incoterms e elegibilidade do pedido

O cache de pedidos nao usa Incoterms como filtro de existencia. Um pedido retornado pelo SAP
dentro do escopo tecnico aprovado deve permanecer consultavel no cache independentemente do
preenchimento de `IncotermsClassification`, `IncotermsTransferLocation` ou `IncotermsLocation1`.

Os conceitos devem permanecer separados:

- existente: o SAP retornou o pedido;
- consultavel: o pedido foi armazenado e esta ativo no cache;
- elegivel para entrada: atende as regras funcionais aprovadas para iniciar/finalizar a entrada;
- editavel: a acao de escrita esta habilitada e autorizada.

Pendencia para validacao do usuario responsavel e da equipe SAP:

- confirmar se alguma acao exige Incoterms;
- definir quais campos e valores sao obrigatorios para cada acao;
- definir a mensagem funcional apresentada ao usuario;
- definir uma configuracao por ambiente para habilitar a regra;
- implementar a validacao somente na acao correspondente, sem remover ou inativar o pedido no cache.

No estado atual, nenhuma operacao de escrita implementada envia ou altera Incoterms. Portanto,
nenhuma regra funcional de bloqueio por Incoterms foi criada.
