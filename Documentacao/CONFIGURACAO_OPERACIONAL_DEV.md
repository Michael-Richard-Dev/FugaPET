# Configuração operacional — linha DEV (DEV110)

Levantado em 2026-11-12 a partir da instalação em `C:\FugaPET_DEV110`. Existe
porque os três `configuracao.*.json` reais são `gitignored` e a configuração em
uso não se reconstruía do repositório.

**Nenhuma credencial é registrada aqui.**

## Situação

O `configuracao.sap.exemplo.json` já cobre exatamente as 19 chaves que o
`LeitorConfiguracaoSap` desta linha lê — não falta nenhuma, e chaves da linha Q
(`ambiente.perfil`, `purchase_order_base_url`,
`pa_material_document_write_enabled`, `taras_fisicas_embalagem_palete`,
`pallet_write_habilitado`) **não** são lidas aqui e não devem ser acrescentadas.

O `configuracao.sap.json` em uso é mínimo e idêntico entre o fonte e a pasta
publicada: declara só `base_url`, `usuario`, `senha`, `sap_client` `110` e
`timeout_segundos` `30`. Os demais endpoints e todas as flags de escrita ficam
nos padrões do leitor (escrita desligada) ou vêm por variável de ambiente.

Host S/4 de DEV: `vhfufds4ci.sap.fugacouros.com.br:44300`, apontando para
`odata4/.../api_purchaseorder_2/srvd_a2x/sap/purchaseorder/0001`.

## Terminais

| Máquina | Terminal | Banco local | Impressora (fila do Windows) | Setor | Balança | Etiqueta | Tara |
|---|---|---|---|---|---|---|---|
| `NT-TI-256` | `TERMINAL_TI_TESTE` | `LOCAL` | `Zebra Rede 200.97` | 1 | 1 | 1 | 1 |
| `TERM-03` | `TERMINAL_FRIGOSUL_03` | `LOCAL_FRIGOSUL_03` | `ZDesigner GK420t` | 1 | 1 | 1 | 1 |

`impressora_padrao` é o nome da fila instalada no Windows daquela máquina. A
Zebra de rede usada nos testes de etiqueta está em `192.168.200.97`.

**Divergência conhecida:** o `configuracao.terminal.json` do fonte ainda aponta
`ZDesigner ZT230-200dpi ZPL SERIAL` para `NT-TI-256`, enquanto a instalação
publicada já usa `Zebra Rede 200.97`. Os arquivos reais do fonte não foram
alterados nesta rodada — só os `.exemplo.json` rastreados e este documento.

## Credenciais — somente por variável de ambiente

| Variável | Uso |
|---|---|
| `FUGAPET_SAP_USERNAME` / `FUGAPET_SAP_PASSWORD` | S/4 |
| `FUGAPET_SAP_PACKAGING_USERNAME` / `FUGAPET_SAP_PACKAGING_PASSWORD` | norma de embalagem (CPI) — credenciais próprias, nunca as do S/4 |

A instalação atual traz `usuario` e `senha` dentro de `configuracao.sap.json`.
Esse arquivo não é versionado e não deve ser copiado para o repositório.

A configuração da linha Q está documentada em
`Documentacao/CONFIGURACAO_OPERACIONAL_Q.md` do projeto `QAS110-Ciclo2`.

## Artefatos externos exigidos pela suíte de testes

Levantado em 2026-11-12. **91 das 136 falhas da suíte não eram defeito de
código** — eram dois artefatos que a suíte exige e que não estavam no lugar.

### Dois ZIPs de SQL, apontados por variável de ambiente

`EntradaMpP5R8CAbiTests` e `EntradaMpP5R8DAbiTests` leem um ZIP cujo
**SHA-256 é verificado dentro do teste**. Sem a variável, as classes falham
inteiras com `InvalidOperationException` — 77 testes.

| Variável | Arquivo | SHA-256 exigido pelo teste |
|---|---|---|
| `FUGAPET_R8C_SQL_PACKAGE` | `FUGAPET_ENTRADA_MP_P5_R8B1_SQL_V_ATIVOS_FIX_NAO_APLICAR.zip` | `9D12BCD1…DD5FB1CC8` |
| `FUGAPET_R8D_SQL_PACKAGE` | `FUGAPET_ENTRADA_MP_P5_R8C_SQL_ABI_CANDIDATO_NAO_APLICAR.zip` | `099ABCE0…C92F7FB0B` |

**Atenção aos nomes: eles não batem.** O teste chamado `R8D` consome o ZIP do
**R8C**, e o teste chamado `R8C` consome o do **R8B1**. A mensagem de erro do
`R8D` diz literalmente "Informe o ZIP SQL R8C offline", o que reforça a
confusão. Case por hash, nunca por nome.

Os dois arquivos vivem em
`C:\Visual Studio 2026\Projetos\Fuga\FugaPET\Backups\artefatos_abi_testes\`
e as variáveis estão fixadas no escopo de usuário do Windows. São binários de
~20 KB marcados `NAO_APLICAR`; por isso ficam fora do repositório, e o caminho
chega ao teste por variável.

### Pasta `_Q_VARIANTES_GAIA_REV1`

`ModeloEtiquetaMigracao035Tests` e `ProcessoSemiAcabadoTests` leem as variantes
Q dos pacotes 035 e 037 em
`BancoDados/001_incrementais/_Q_VARIANTES_GAIA_REV1/`. A pasta não existia nesta
linha — 14 testes falhavam com `DirectoryNotFoundException`.

Os 83 arquivos foram trazidos da linha Q, onde existiam em duas cópias
byte-idênticas (`FugaPET/QAS110-Ciclo2` e `Fuga_Couros_HML_Q/FugaPet_HML`) e
**não estavam versionados em nenhuma das duas** — foi exatamente por isso que o
DEV nunca os recebeu. Agora estão versionados aqui, para a suíte ser
reprodutível num clone novo.

### O que sobra depois disso

45 falhas, das quais **27 falham por desenho**:
`DirectExeAutoConfigQ120GTests`, `PostgresSecretCompositionQ20DTests`,
`AmbienteQIsolationTests`, `LeitorConfiguracaoSapTests`,
`PaletizacaoSafeStartup122CTests` e `ConfiguracaoSchemaBancoTests` afirmam que
esta linha lê o banco (`192.168.3.226`) e o SAP (`vhfufqs4ci…`) **do Q**. O
`LeitorConfiguracaoBancoPostgreSql` do DEV lê `FUGAPET_DEV_*`, não
`FUGAPET_Q_*`, justamente para o DEV não conectar no banco de homologação do Q.
Deixar esses testes verdes exigiria reintroduzir esse defeito. As outras 18 são
auditorias de texto-fonte de telas, vermelhas desde antes.
