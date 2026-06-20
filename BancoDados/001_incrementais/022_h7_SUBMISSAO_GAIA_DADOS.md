# Submissao ao Gaia Dados - H7

## Status

Aprovado pelo Gaia Dados para desenvolvimento como incremental documental e modelo de
referencia. O script nao foi executado e nao deve ser aplicado sobre o banco desenvolvimento
V1.1 atual, pois o H7 ja esta incorporado.

## Artefato

`022_h7_rastreabilidade_completa_entrada_produto_PROPOSTA_GAIA.sql`

## Contexto

O baseline consolidado de desenvolvimento V1.1 ja possui o H7, mas nao havia um incremental
separado no repositorio. Esta proposta formaliza o modelo para auditoria e eventual aplicacao
somente em ambiente que ainda nao possua os objetos.

## Decisoes

- um pedido pode possuir varios lancamentos;
- o numero do item e unico somente dentro do lancamento;
- cada pesagem possui linha e sequencia proprias;
- lancamento, item e pesagem usam `ON DELETE RESTRICT`;
- exclusao operacional e logica por `situacao_*`;
- usuario operacional e usuario de criacao sao obrigatorios;
- timestamps usam `timestamptz` e `now()`;
- pesos usam quilogramas com tres casas decimais;
- liquido corresponde a bruto menos tara, com tolerancia de 0,001 kg;
- os triggers genericos registram atualizacao e alteracao cadastral.

## Precondicao

O script interrompe a execucao se qualquer tabela H7 ja existir. No banco de desenvolvimento
informado pelo projeto, o modelo ja esta presente. A expectativa atual e revisao documental,
nao reaplicacao.

## Parecer do Gaia Dados

- FKs entre lancamento, item e pesagem aprovadas com `ON DELETE RESTRICT`;
- FKs de usuario operacional e auditoria aprovadas com `ON DELETE RESTRICT`;
- campos de usuario operacional e `*_criado_por` aprovados como obrigatorios;
- `SET NULL` aprovado somente para referencias SAP, tara e balanca;
- timestamps `timestamptz DEFAULT now()` aprovados para preservar maior precisao;
- regra de peso aprovada: bruto maior que tara, liquido positivo e diferenca maxima de
  0,001 kg entre o liquido e o calculo bruto menos tara;
- script aprovado para arquivamento como evidencia ou uso em ambiente sem as tabelas H7.

## Responsabilidades da aplicacao

- enviar sempre o usuario operacional e os campos `*_criado_por` nos INSERTs;
- preencher `*_atualizado_por` quando houver alteracao;
- informar `pesado_em` explicitamente quando for necessario preservar o instante exato da
  leitura da balanca, em vez do instante de persistencia no banco;
- manter cada nova entrada e cada pesagem como novos registros, sem sobrescrever historico.

## Pendencias antes da homologacao

- decidir funcionalmente se o status `CANCELADO` deve exigir `finalizado_em`;
- avaliar snapshots dos dados de tara e balanca para reforcar a rastreabilidade historica;
- nao implementar essas decisoes sem validacao funcional e nova revisao do Gaia.

## Execucao

Nao executar no banco desenvolvimento V1.1 atual. Em eventual ambiente sem H7, a aplicacao
pertence ao usuario responsavel/Gaia Dados. Depois da aplicacao autorizada, realizar probe
somente leitura antes de afirmar o resultado.
