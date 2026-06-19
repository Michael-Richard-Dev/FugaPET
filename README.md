# FugaPET — Sistema de Produção e Rastreabilidade PET

> **Nome oficial do produto:** FugaPET — Sistema de Produção e Rastreabilidade PET.
> Use este nome em titulos, mensagens internas e documentacao. O nome tecnico do projeto/solution
> permanece `FugaPET_Dev` (nao ha renomeacao de solution nesta etapa). A fonte unica do nome esta
> em `MarcaProduto` (`MarcaProduto.NomeCompleto`).

Projeto Windows Forms C#/.NET (`FugaPET_Dev`) para operacao local do FugaPET, com estrutura em camadas:

- AcessoDados
- Controle
- Modelo
- Servicos
- Tela
- Relatorio
- BancoDados

## Configuracao local

Os arquivos reais de ambiente nao devem ser versionados nem enviados com credenciais ou dados de maquina.

Use os modelos abaixo para criar os arquivos locais na maquina de execucao:

- configuracao.banco.exemplo.json -> copiar como configuracao.banco.json
- configuracao.terminal.exemplo.json -> copiar como configuracao.terminal.json

A senha do PostgreSQL deve preferencialmente vir de variavel de ambiente no ambiente real.

## Build

Execute:

```powershell
dotnet build FugaPET_Dev.csproj -p:UseAppHost=false
```

## Telas ainda simuladas / bloqueadas fora do modo demonstração

As telas abaixo ainda usam dados simulados (`UsaDadosSimulados = true`), pois ainda nao foram
integradas ao banco/SAP:

- Consulta de Etiqueta (ConsultaEtiquetaForm)
- Consulta de Histórico (ConsultaHistoricoForm)
- Consulta Integração SAP (ConsultaIntegracaoSapForm)
- Consulta Ordem Produção (ConsultaOrdemProducaoForm)
- Processo Pesagem Apontamento (ProcessoPesagemApontamentoForm)
- Processo Produto Acabado (ProcessoProdutoAcabadoForm)

Comportamento por ambiente (controlado por `banco.modo_demonstracao` em configuracao.banco.json):

- **Modo demonstracao** (`modo_demonstracao = true`): as telas ABREM com dados mock e exibem uma
  faixa vermelha fixa "DADOS SIMULADOS — TELA AINDA NAO INTEGRADA AO BANCO/SAP".
- **Homologacao / producao** (`modo_demonstracao = false`, padrao — banco habilitado): essas telas
  ficam BLOQUEADAS ate a integracao real. Ao tentar abrir, aparece um aviso e a tela nao carrega.

> IMPORTANTE: se uma dessas telas "nao abrir" em homologacao, NAO e bug — esta bloqueada por
> ainda usar mock. Para visualiza-la em demonstracao, defina `modo_demonstracao = true`.
> Quando a tela for integrada de verdade, troque `UsaDadosSimulados` para `false` na tela
> (a faixa, o mock e o bloqueio somem juntos).

## Observacoes

- Nao versionar bin, obj, .vs, .claude, *.user ou arquivos reais de configuracao.
- Manter arquivos em UTF-8 com BOM e CRLF conforme .editorconfig.

## Dividas tecnicas controladas

- Namespaces legados de impressao/Zebra ja padronizados nesta alteracao isolada:
  - `Modelo/SolicitacaoImpressao.cs` -> `FugaPET_Dev.Modelo`
  - `Modelo/DadosEtiquetaProducao.cs` -> `FugaPET_Dev.Modelo`
  - `Servicos/ServicoImpressoraZebra.cs` -> `FugaPET_Dev.Servicos`
  - `Controle/ControladorImpressao.cs` -> `FugaPET_Dev.Controle`
- Padronizacao concluida: `FugaPET_Dev.Controllers` e `FugaPET_Dev.Controllers.Cadastro` migrados para `FugaPET_Dev.Controle` e `FugaPET_Dev.Controle.Cadastro` (alteracao isolada de namespace, sem mudanca funcional).
- Nao misturar renomeacoes de namespace com correcoes funcionais, pois a troca pode gerar atualizacoes em cadeia.
