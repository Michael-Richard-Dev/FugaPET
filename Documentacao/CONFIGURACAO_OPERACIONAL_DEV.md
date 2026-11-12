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
