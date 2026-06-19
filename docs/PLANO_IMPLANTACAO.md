# Plano de Implantação — FugaPET

**Produto:** FugaPET — Sistema de Produção e Rastreabilidade PET
**Empresa:** FUGA COUROS S.A.

> Documento vivo. Descreve as etapas de implantação do FugaPET no ambiente industrial.

## 1. Objetivo

Implantar o FugaPET para operação de pesagem, produção e rastreabilidade PET nos terminais
de chão de fábrica, com integração ao banco PostgreSQL e ao SAP (cache local; integração real
em etapa futura — hoje há contrato interno com mock).

## 2. Ambientes

| Ambiente     | Banco (schema)   | Uso                                   |
|--------------|------------------|---------------------------------------|
| Demonstração | `homologacao`    | Telas com dados simulados (faixa)     |
| Homologação  | `homologacao`    | Validação funcional com banco real    |
| Produção     | `producao`       | Operação real                         |

Configuração por terminal via `configuracao.banco.json` e `configuracao.terminal.json`
(modelos `*.exemplo.json`). Senhas preferencialmente por variável de ambiente.

## 3. Pré-requisitos por terminal

- Windows com .NET runtime compatível (`net10.0-windows`).
- Conectividade com o PostgreSQL (homologação/produção).
- Balança configurada (SERIAL/TCP_IP/USB/MANUAL) e impressora Zebra padrão do terminal.
- Ícone/identidade do FugaPET aplicados (título da janela usa `MarcaProduto.NomeCompleto`).

## 4. Etapas de implantação

1. **Preparação do banco** — aplicar baseline + incrementais (`BancoDados/`), confirmar schema.
2. **Bootstrap de acesso** — usuário administrador inicial, perfis e permissões essenciais.
3. **Configuração do terminal** — `configuracao.terminal.json` (balança, impressora padrão).
4. **Validação em homologação** — fluxos de cadastro, pesagem e etiquetas com banco real.
5. **Treinamento do operador** — telas principais e mensagens do FugaPET.
6. **Go-live em produção** — `modo_demonstracao = false`, schema `producao`.

## 5. Integração SAP

- Hoje: **contrato interno** (`IIntegracaoSapServico`) com **implementação mock** (`EhSimulado = true`).
- Cache SAP local somente leitura (`vw_sap_cache_status`) monitorado em `StatusCacheSapLocalServico`.
- Futuro: implementar `IIntegracaoSapServico` real e trocar em `FabricaIntegracaoSap` — sem mudar telas/serviços.

## 6. Rollback

- Operação por terminal: reverter `configuracao.*.json` e voltar `modo_demonstracao`.
- Banco: scripts incrementais são forward-only; rollback via restore controlado.

## 7. Checklist de go-live

- [ ] Banco aplicado e validado no schema correto
- [ ] Administrador e permissões essenciais ativos
- [ ] Balança e impressora do terminal testadas
- [ ] Títulos/mensagens exibindo o nome FugaPET
- [ ] `modo_demonstracao = false` em produção
