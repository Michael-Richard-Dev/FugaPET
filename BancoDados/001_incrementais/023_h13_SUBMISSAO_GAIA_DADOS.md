# Submissao ao Gaia Dados - H13

## Status

Aprovado pelo Gaia Dados para desenvolvimento como estrutura preparatoria de outbox SAP.
O script nao foi executado e nao deve ser aplicado automaticamente.

## Artefato

`023_h13_outbox_reprocessamento_sap_PROPOSTA_GAIA.sql`

## Objetivo

Preparar uma integracao SAP futura confiavel por outbox transacional, sem reativar PATCH,
sem criar worker e sem alterar o fluxo operacional atual.

## Escopo proposto

- `integracao_sap_outbox`: uma pendencia por evento imutavel de negocio;
- `integracao_sap_tentativa`: historico imutavel de cada tentativa;
- indices para busca de pendencias e rastreabilidade;
- checks de estado, tentativas, HTTP, duracao e tamanho do payload;
- FK da tentativa para outbox com `ON DELETE RESTRICT`;
- nenhuma funcao, trigger, job ou worker que realize chamada SAP.

## Parecer do Gaia Dados

- estados aprovados: `PENDENTE`, `PROCESSANDO`, `PROCESSADO`,
  `ERRO_REPROCESSAVEL`, `ERRO_DEFINITIVO` e `CANCELADO`;
- `PROCESSADO` exige `processado_em`;
- estados nao processados nao permitem `processado_em`;
- `PENDENTE` e `ERRO_REPROCESSAVEL` exigem `proxima_tentativa_em`;
- `PROCESSANDO`, `PROCESSADO`, `ERRO_DEFINITIVO` e `CANCELADO` exigem
  `proxima_tentativa_em` nulo;
- estados de erro exigem mensagem;
- limite de 16 KiB para `payload_controlado` aprovado;
- unicidade de `correlation_id` por outbox aprovada;
- idempotencia principal aprovada por
  `(tipo_operacao, entidade, chave_negocio)`;
- chave de negocio deve representar evento imutavel;
- ausencia de exclusao em cascata aprovada;
- FK de tentativa permanece com `ON DELETE RESTRICT`;
- nenhuma retencao ou limpeza automatica deve ser criada neste incremento;
- outbox e tentativas devem ser preservadas como historico operacional.

## Decisoes de modelagem

### Idempotencia

A unicidade de `(tipo_operacao, entidade, chave_negocio)` e obrigatoria. A
`chave_negocio` deve identificar um evento imutavel, nao apenas uma entidade mutavel.

Exemplo para o fluxo de Entrada:

`lancamento:<codigo_entrada_produto_lancamento>:finalizacao`

Uma tentativa concorrente de criar a mesma pendencia deve tratar a violacao de unicidade
como idempotencia atendida. Nao deve criar outra outbox.

### Transacao local

Quando a aplicacao for implementada, a finalizacao local deve gravar:

1. lancamento, itens e pesagens;
2. pendencia `PENDENTE` na outbox;
3. commit unico.

Se a outbox nao puder ser criada, a transacao local inteira deve falhar. Nao pode existir
finalizacao local comprometida com a intencao SAP perdida.

Esta responsabilidade nao foi implementada no C# nesta tarefa e permanece como ressalva
obrigatoria do Gaia. Portanto, o H13 ainda nao torna a integracao confiavel por si so.

### Estados da outbox

- `PENDENTE`: pronta para processamento futuro;
- `PROCESSANDO`: reservada por um worker;
- `PROCESSADO`: concluida com `processado_em`;
- `ERRO_REPROCESSAVEL`: falha temporaria com novo agendamento;
- `ERRO_DEFINITIVO`: falha sem reprocessamento automatico;
- `CANCELADO`: cancelamento controlado e auditado pela aplicacao futura.

### Reprocessamento controlado

- reutiliza a mesma linha da outbox;
- incrementa `tentativas`;
- cria nova linha em `integracao_sap_tentativa`;
- nunca altera ou apaga tentativas anteriores;
- deve exigir permissao especifica e auditoria na futura aplicacao;
- deve possuir limite de tentativas e politica de backoff configuraveis;
- nao foi criado endpoint, tela, worker ou agendamento nesta tarefa.

### Concorrencia futura

O worker devera reservar pendencias com `FOR UPDATE SKIP LOCKED` em transacao curta.
A chamada HTTP nao deve manter lock ou transacao de banco aberta. A conclusao deve validar
que a outbox ainda esta em `PROCESSANDO` antes de alterar seu estado.

## Payload controlado

`payload_controlado` e um JSON minimo, montado por allowlist e versionado pela aplicacao.
Deve conter somente os dados necessarios para reproduzir a operacao.

Exemplo conceitual, nao contratual:

```json
{
  "versao": 1,
  "codigo_lancamento": 123,
  "numero_pedido": "4500000001",
  "itens": [
    {
      "numero_item": "10",
      "peso_liquido_kg": 9.25,
      "peso_bruto_kg": 10.5
    }
  ]
}
```

Proibido armazenar:

- usuario ou senha SAP;
- token, cookie, authorization ou headers;
- connection string;
- request ou response HTTP completo;
- stack trace;
- payload bruto recebido do SAP;
- dados pessoais ou campos que nao sejam necessarios para a operacao.

O script limita o JSON a objeto com ate 16 KiB e bloqueia marcadores sensiveis de primeiro
nivel. A aplicacao continua responsavel por allowlist e sanitizacao recursiva.

O limite e a regra de payload minimo, versionado e montado por allowlist foram aprovados
pelo Gaia.

## Relacao com logs existentes

`log_integracao_sap` continua sendo log sanitizado de diagnostico. Ele nao substitui a
outbox e nao deve ser usado como fila.

Tabelas legadas/especificas de integracao nao devem ser reutilizadas automaticamente.
Qualquer convergencia ou aposentadoria exige inventario funcional separado.

## Escrita SAP

O PATCH permanece bloqueado. O H13 cria somente estruturas persistentes para uso futuro.
Nenhum processo passa a enviar dados ao SAP após eventual aplicacao deste incremental.

## Precondicoes para futura implementacao C#

Worker, reprocessamento e escrita SAP permanecem desativados. O incremento nao cria worker,
job, trigger de envio, funcao de rede nem agendamento.

- parecer e aplicacao autorizada pelo Gaia Dados;
- probe confirmando as tabelas e constraints;
- contrato funcional do payload por `tipo_operacao`;
- permissao de reprocessamento definida em `PermissoesSistema`;
- politica configuravel de limite/backoff;
- isolamento das configuracoes SAP por ambiente;
- teste garantindo que escrita SAP permanece desativada.

## Retencao

Nao criar retencao automatica neste incremento. Outbox e tentativas sao historico
operacional. Qualquer expurgo futuro deve seguir politica formal de arquivamento, com nova
revisao, e nao uma limpeza automatica simples.

## Responsabilidade futura da aplicacao

Antes de afirmar confiabilidade da integracao, o C# deve:

1. inserir a pendencia `PENDENTE` na mesma transacao do lancamento, itens e pesagens;
2. abortar toda a finalizacao local se a criacao da outbox falhar;
3. montar `payload_controlado` por allowlist e contrato versionado;
4. tratar violacao da chave idempotente sem criar pendencia duplicada;
5. manter worker, reprocessamento e escrita SAP desativados ate nova autorizacao.

## Execucao

Nao executar automaticamente. A aplicacao do incremental pertence ao usuario responsavel
ou ao Gaia Dados. Depois de eventual aplicacao autorizada, realizar probe somente leitura
antes de afirmar que o H13 esta disponivel.
