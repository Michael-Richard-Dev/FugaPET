# 055 — SUPERADO PELO 058 no ambiente DEV

**Não execute este pacote no DEV.** Ele é inaplicável aqui, e isso é definitivo.
A última execução do preflight falha em `055 PRECHECK: CONFIG5 divergiu`.

## Por quê

O 055 pressupõe um estado de partida que **não existe em nenhum banco acessível**:

1. O preflight exige as seis configs legadas com `centro_trabalho` **curto**
   (`'5'`, `'6'`, `'7'`, `'9'`, `'10'`, `'100'`) e exige que nenhuma das nove chaves
   Plant+WorkCenter **longas** exista ativa. O trabalho do próprio 055 é migrar os
   curtos para os longos — ele faz exatamente esse `UPDATE`. O banco de homologação
   do Q já tem os longos: está no estado **pós-055**.

2. O preflight exige a config 7 como `RESULTADO_APONTAMENTO` /
   `ProcessoResultadoApontamentoForm`. O Q tem a config 7 como
   `CONSUMO_MATERIA_PRIMA` / `ProcessoConsumoMaterialForm`. O 055 nunca altera
   `tipo_processo` nem `tela_destino` — só `centro_trabalho`. Logo a config 7 foi
   mudada por algo **posterior** ao 055, que também não está neste repositório.

3. O Q tem as definições 5 e 6, criadas pelos gates
   `GATE_103P_OP1000173_0090_TEMPO` e `GATE_103U_OP1000173_0100_TEMPO`, ausentes
   daqui. E o preflight do 055 exige **zero** definições nos perfis 2..4 — onde
   essas duas justamente estão.

Reconstruir o baseline pré-055 exigiria inventar o `tipo_processo` antigo da
config 7, contradizendo o que o banco do Q afirma hoje. Isso não foi feito.

## O que foi feito em vez disso

O incremento **058** (`058_copia_configuracao_operacional_do_Q_DEV_GAIA`) copiou o
estado **atual** do Q, com autorização expressa do usuário, lido em transação
`READ ONLY`. Ele entrega de uma vez o resultado do 055 **e** o das mudanças
anteriores e posteriores que nunca foram versionadas aqui:

| origem (`criado_por` no Q) | o que trouxe |
| --- | --- |
| `FugaPET incremental 040 rotas consumo` | preenchimento de `centro`/`centro_trabalho` nas configs 5 e 6 |
| `GATE_048_G_DATA_03` | configs 7..11 |
| `MIGRATION_055_ROUTE_PROFILE_094I` | configs 12..14, perfis 5..7, definições 2..4 — **este pacote** |
| `GATE_103P` / `GATE_103U` | definições 5 e 6 |

Verificado depois do 058: a configuração, os perfis e as definições do DEV são
**idênticos** aos do Q em todas as colunas de negócio (diff zero).

O 057 criou antes a estrutura das duas tabelas de resultado, que também não
existia em pacote nenhum.
