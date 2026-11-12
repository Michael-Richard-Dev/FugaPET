# Próximo passo único — breakpoint, sem novo POST deliberado

**Máquina:** NT-TI-256
**Instância:** Visual Studio 2026 executando o **HML** (`FugaPET_Dev`)
**Objetivo:** rastrear qual caminho o botão **ENVIAR CAIXA SAP** percorre (pipeline 261→101→HU
via gate, ou HU-only). Apenas observar o fluxo — **não** disparar deliberadamente um novo POST SAP.

---

## Coloque os breakpoints nestes quatro pontos, nesta ordem

### 1. Handler do Click do botão "ENVIAR CAIXA SAP"
- **Arquivo:** `Tela/Processo/ProcessoProdutoAcabadoForm.cs`
- **Linha 618:**
  ```csharp
  _enviarCaixaSapButton.Click += async (_, _) => await SolicitarEnvioCaixaSapAsync();
  ```

### 2. Primeira linha de `SolicitarEnvioCaixaSapAsync()`
- **Arquivo:** `Tela/Processo/ProcessoProdutoAcabadoForm.cs`
- **Linha 701** (primeira linha executável do método):
  ```csharp
  if (_envioCaixaSapEmAndamento || _enviarCaixaSapButton is null)
  ```

### 3. Primeira linha de `SolicitarEnvioCaixaPipelineAsync()`
- **Arquivo:** `Tela/Processo/ProcessoProdutoAcabadoForm.cs`
- **Linha 851** (primeira linha executável do método):
  ```csharp
  if (!_controller.PipelinePaDisponivel)
  ```

### 4. Primeira linha de `ProdutoAcabadoController.EnviarCaixaPipelineAsync(...)`
- **Arquivo:** `Controle/Processo/ProdutoAcabadoController.cs`
- **Linha 162** (primeira linha executável do método):
  ```csharp
  ArgumentNullException.ThrowIfNull(origem);
  ```

---

## O que observar (fluxo esperado)

1. **BP #1 → BP #2:** ao clicar no botão, o handler chama `SolicitarEnvioCaixaSapAsync()`.
2. Dentro de `SolicitarEnvioCaixaSapAsync()`, logo após os guards, há o desvio de rota:
   ```csharp
   // roteia para o pipeline SOMENTE quando o gate está habilitado
   if (_controller.PipelinePaGateHabilitado)
   {
       await SolicitarEnvioCaixaPipelineAsync();
       return;
   }
   ```
   - Se `PipelinePaGateHabilitado == true` → cai no **BP #3** (pipeline).
   - Se `PipelinePaGateHabilitado == false` → **não** entra no pipeline (segue o caminho HU-only
     homologado; os BP #3 e #4 não são atingidos). Isso é o comportamento correto quando o gate
     `FUGAPET_SAP_PA_PIPELINE_ENABLED` está desligado.
3. **BP #3:** em `SolicitarEnvioCaixaPipelineAsync()`, avalie `_controller.PipelinePaDisponivel`:
   - `false` → bloqueia exibindo `_controller.PipelinePaMotivo` no `statusLabel` e **retorna sem POST**
     (não há fallback silencioso para HU-only).
   - `true` → segue e chama `_controller.EnviarCaixaPipelineAsync(...)` → cai no **BP #4**.
4. **BP #4:** em `EnviarCaixaPipelineAsync(...)`, o Controller ainda é fail-closed:
   ```csharp
   if (!_composicaoPipeline.Disponivel || _composicaoPipeline.Orquestrador is null) { /* Bloqueada, zero POST */ }
   // e o builder bloqueia se faltarem campos 261/101 (DEPENDENCIA_ARES/GAIA) — também zero POST.
   ```

### Variáveis úteis para inspecionar (Watch)
- `_controller.PipelinePaGateHabilitado` — o gate `FUGAPET_SAP_PA_PIPELINE_ENABLED`.
- `_controller.PipelinePaDisponivel` — gate + store 045 persistente disponível.
- `_controller.PipelinePaMotivo` — `PIPELINE_DESABILITADO` / `DEPENDENCIA_GAIA_PERSISTENCIA_PIPELINE` / `PIPELINE_COMPOSTO`.

> **Sem novo POST deliberado:** com qualquer dependência ausente (gate, store 045, config SAP/CPI,
> campos 261/101), o fluxo **bloqueia antes de qualquer chamada externa**. Os breakpoints servem
> apenas para confirmar qual ramo é executado — nenhum POST SAP/CPI é disparado de propósito.

---

> Observação: os números de linha acima são do projeto **HML**. No projeto **DEV** (`Fuga_Couros`)
> os mesmos quatro métodos existem, porém em linhas diferentes — use os mesmos nomes de método
> (`SolicitarEnvioCaixaSapAsync`, `SolicitarEnvioCaixaPipelineAsync`, `EnviarCaixaPipelineAsync`)
> para posicionar os breakpoints.
