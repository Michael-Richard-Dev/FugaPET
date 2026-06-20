# Submissao ao Gaia Dados - H14

## Status

Proposta tecnica aguardando revisao e parecer do Gaia Dados. O script nao foi executado.

## Artefato

`024_h14_precisao_data_hora_auditoria_PROPOSTA_GAIA.sql`

## Objetivo

Preservar a sequencia real de eventos industriais, leituras, login, auditoria, integracoes
SAP, movimentacoes e aplicacao de versoes do banco.

## Inventario

O baseline consolidado possui 96 referencias a `date_trunc('minute', now())`, incluindo:

- defaults de cadastros;
- funcao generica `fn_definir_atualizado_em`;
- auditoria e log cadastral;
- sincronizacoes e caches SAP;
- pesagens e leituras;
- movimentacoes de HU;
- versao do banco;
- seeds e comandos pontuais do baseline.

O incremental nao substitui cegamente todas as ocorrencias. Ele prioriza eventos em que
varias acoes podem ocorrer no mesmo minuto e cuja ordenacao e relevante.

## Estrategia temporal

### `clock_timestamp()`

Proposto para eventos e auditoria porque retorna o instante real da chamada, inclusive
dentro de uma mesma transacao. Isso permite distinguir:

- leituras consecutivas;
- atualizacoes sucessivas;
- inicio e fim de integracoes;
- auditorias disparadas na mesma transacao;
- movimentacoes industriais em alta frequencia.

### `now()`

Continua aceitavel para campos que representam o instante logico da transacao. Nesta fase,
os defaults cadastrais nao prioritarios permanecem inalterados.

### Aplicacao explicita

Quando o instante vem do equipamento ou da aplicacao, ele deve continuar sendo enviado
explicitamente. Exemplo: `entrada_produto_pesagem.pesado_em` deve receber o instante real da
leitura quando disponivel. O default e apenas fallback.

## Escopo do incremental

- recria `fn_definir_atualizado_em` usando `clock_timestamp()` sem conversao textual;
- altera defaults futuros de auditoria e log cadastral;
- altera defaults de sincronizacoes, caches e logs SAP;
- altera defaults de movimentacoes de HU;
- altera defaults de pesagens e leituras;
- altera `versao_banco.aplicado_em`;
- nao altera dados historicos;
- nao altera tipos de coluna;
- nao remove ou recria indices.

## Login

`UsuarioRepositorio.AtualizarUltimoLoginAsync` foi ajustado de
`date_trunc('second', now())` para `clock_timestamp()`.

## Impacto em indices

Os indices permanecem validos porque:

- as colunas continuam sendo `timestamptz`;
- somente a expressao `DEFAULT` e alterada;
- nao ha conversao ou reescrita dos valores existentes;
- maior cardinalidade temporal tende a melhorar o desempate em ordenacoes.

Nao e proposta nenhuma recriacao de indice nesta fase.

## Impacto em testes

Testes nao devem comparar timestamps por igualdade exata com minuto arredondado. Devem:

- validar intervalos antes/depois da operacao;
- aceitar fracao de segundo;
- ordenar por timestamp e usar PK/sequencia como desempate quando necessario;
- nao assumir que registros historicos anteriores possuem precisao integral.

Foi criado teste contratual para impedir regressao para `date_trunc` nos pontos prioritarios.

## Compatibilidade e dados historicos

O incremental afeta apenas novos defaults e futuros UPDATEs pela funcao generica. Timestamps
historicos truncados permanecem inalterados, pois nao e possivel reconstruir a precisao
perdida.

A tela pode continuar formatando datas sem fracao de segundo. A precisao deve ser descartada
somente na apresentacao, nunca na persistencia.

## Pontos para parecer do Gaia

1. Aprovar `clock_timestamp()` para os eventos priorizados.
2. Aprovar a alteracao global da funcao `fn_definir_atualizado_em`.
3. Confirmar que cadastros nao prioritarios podem permanecer para uma fase posterior.
4. Confirmar que nenhum indice precisa ser recriado.
5. Confirmar que dados historicos nao devem ser atualizados artificialmente.

## Execucao

Nao executar automaticamente. A aplicacao pertence ao usuario responsavel ou ao Gaia Dados.
Depois de eventual aplicacao autorizada, realizar probe de defaults e inserir eventos de
teste sob transacao controlada com `ROLLBACK`.
