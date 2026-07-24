# Diagnóstico de Responsividade / DPI / Escala — WinForms FugaPET

**Rodada:** 1 (diagnóstico; sem correção, sem redesenho, sem migração em massa).
**Ambiente analisado:** `C:\Visual Studio 2026\Projetos\Fuga\Fuga_Couros` (DEV).
**HML:** não analisado nem alterado. **Sem commit. Nenhum layout alterado.**

> Este documento é um levantamento por inspeção de código (`.csproj`, `Program.cs`, `*.Designer.cs`, `*.cs`).
> Onde uma tela ainda **não** foi validada na matriz física de resoluções, isso é dito explicitamente —
> nenhuma tela é declarada "validada" sem inspeção/teste correspondente.

---

## 1. Configuração global atual de DPI

| Item | Valor encontrado | Observação |
|---|---|---|
| `OutputType` / `TargetFramework` | `WinExe` / `net10.0-windows` | WinForms SDK |
| `ApplicationHighDpiMode` (csproj) | **não definido** | usa o **default do SDK** |
| **HighDpiMode efetivo** | **`SystemAware`** | default do gerador `ApplicationConfiguration.Initialize()` quando a propriedade não é definida |
| `ApplicationDefaultFont` (csproj) | **não definido** | cada Form define sua própria `Font` |
| `Application.SetHighDpiMode(...)` em código | **ausente** | nenhum override manual |
| `app.manifest` (dpiAware / dpiAwareness) | **inexistente** | nenhum manifesto no projeto |
| `HighDpiMode.PerMonitorV2` | **não usado** em lugar nenhum | — |
| Handler `DpiChanged` / `OnDpiChanged` | **inexistente em todo o projeto** | ninguém reage a troca de monitor/escala |
| Configurações duplicadas/conflitantes | Nenhuma no nível global | o conflito está no nível de Form (ver §3) |

**Conclusão do nível global:** o app roda em **`SystemAware`**. Isso significa que a escala é decidida **uma vez**,
pela DPI do monitor primário no start, e nos demais monitores / trocas de escala o Windows aplica **stretch de
bitmap** (borra e desalinha). É a causa mais provável de "quebra em resoluções/escalas diferentes" relatada em
campo, especialmente **multi-monitor com escalas diferentes** e **janela iniciada no monitor secundário**.

---

## 2. Inventário de telas

**Total:** 29 `Form` + 3 `UserControl` = **32 superfícies**.

Propriedades globais observadas:
- **`AutoScaleMode = Font` em 100% dos Forms** (consistente).
- **`AutoScaleDimensions` INCONSISTENTE:** 15 superfícies em `(7F, 15F)` e 14 em `(7F, 16F)` — dois baselines
  de fonte de design coexistem (telas desenhadas em máquinas/DPI de design diferentes). Ver §3 (risco sistêmico).
- **21 Forms** são `FormBorderStyle.None` + `WindowState.Maximized` (barra de título custom) — telas principais.
- **`DpiChanged` não é tratado em nenhuma tela.**

Design size predominante das telas principais: **1366×720** (`ClientSize`), com `MinimumSize` **1180×648**
nas telas maduras/operacionais.

### Tabela por tela (design size, MinimumSize, baseline de fonte, mecanismo de layout)

Mecanismo: **RL** = `rootLayout` (TableLayoutPanel Dock Fill header/body/footer) · **TLP** = TableLayoutPanel ·
**MAN** = escala manual em runtime (`SetBounds`/`ApplyScaledFont`/`LayoutXxxCard`) · **ANC** = Anchor/Dock só do Designer.

| Form | Client | MinSize | ASD | Mecanismo | Estado janela |
|---|---|---|---|---|---|
| CamposEtiquetaForm | 1366×720 | 1180×648 | 7,16 | RL + TLP (30/35/35) + fontes clamp | Max / None |
| CargoForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| SetorForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| TipoTaraForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| TaraForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| EtiquetaForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| ModeloEtiquetaForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| PerfilAcessoForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| PermissaoForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| CadastroUsuarioForm | 1366×720 | 1180×648 | 7,16 | RL + MAN | Max / None |
| BalancaForm | 1366×720 | 1180×680 | 7,15 | RL + MAN | Max / None |
| ProcessoEntradaProdutoForm | 1366×720 | 1180×648 | 7,16 | ANC + Resize (sem SetBounds) | Max / None |
| ProcessoConsumoMaterialForm | 1366×720 | 1180×648 | 7,16 | ANC + Resize | Max / None |
| ProcessoSemiAcabadoForm | 1366×720 | 1180×648 | 7,16 | ANC + Resize | Max / None |
| ProcessoProdutoAcabadoForm | 1366×720 | 1180×648 | 7,16 | ANC + Resize | Max / None |
| ProcessoConsumoMaterialHistoricoForm | 1184×721 | 1180×648 | 7,15 | RL | Max / None |
| ConsultaEtiquetaForm | 1366×720 | 1180×648 | 7,15 | RL | Max / None |
| ConsultaHistoricoForm | 1366×720 | 1180×648 | 7,15 | RL + Resize | Max / None |
| ConsultaIntegracaoSapForm | 1366×720 | 1180×648 | 7,15 | RL + Resize | Max / None |
| ConsultaOrdemProducaoForm | 1366×720 | 1180×648 | 7,15 | RL | Max / None |
| PainelInicialForm | 1366×720 | 1180×648 | 7,15 | RL + AutoScroll | Max / None |
| DiagnosticoConsumoSap261Form | 980×640 | (nenhum via RL) | 7,15 | RL | Normal |
| LoginForm | 920×560 | — | 7,15 | ANC | Normal |
| TrocaSenhaObrigatoriaForm | 460×350 | — | 7,15 | ANC | Normal |
| BalanceTestForm | 520×320 | — | 7,15 | ANC | Normal |
| TesteZebraForm | 620×390 | — | 7,15 | ANC | Normal |
| ConfirmarReimpressaoEtiquetaForm | (dialog) | — | 7,15 (a confirmar) | ANC | Normal |
| PesagemMultiplaItemForm | (dialog) | — | (a confirmar) | ANC | Normal |
| SelecaoTaraPesagemForm | (dialog) | — | (a confirmar) | ANC | Normal |
| CadastroForm (UserControl) | — | — | 7,15 | RL/TLP dentro do PainelInicial | — |
| ProcessoProducaoForm (UserControl) | — | — | 7,15 | — | — |
| SegurancaForm (UserControl) | — | — | 7,15 | RL/TLP | — |

> "(a confirmar)" = tamanho não capturado no scan agregado; exige leitura pontual do Designer na próxima rodada.

---

## 3. Padrões conflitantes detectados

1. **Dois baselines de `AutoScaleDimensions` (7×15 vs 7×16).** Com `AutoScaleMode.Font`, o runtime escala pela
   razão `fonte-atual / AutoScaleDimensions`. Baselines diferentes entre telas produzem **fatores de escala
   ligeiramente diferentes por tela** → inconsistência de tamanho/altura de controles entre telas no mesmo PC.
   *É o problema sistêmico nº 1 e o mais barato de padronizar.*

2. **Escala MANUAL em runtime sobre `AutoScaleMode.Font` (risco de dupla escala).** 11 telas combinam a escala
   automática do WinForms com um motor manual (`LayoutXxxCard` + `SetBounds(control, Scale(x), Scale(y), …)` +
   `ApplyScaledFont(base, contentScale, min, max)`): **BalancaForm, CadastroUsuarioForm, CargoForm, EtiquetaForm,
   ModeloEtiquetaForm, PerfilAcessoForm, PermissaoForm, SetorForm, TaraForm, TipoTaraForm** (e parte da CamposEtiqueta
   só para fontes). Hoje, em `SystemAware`, esse motor é **proporcional ao container já escalado** e funciona nos
   níveis testados — mas é **frágil** e é exatamente o cenário que pode **dobrar a escala** se `PerMonitorV2` for
   ligado sem cuidado (o `DpiChanged` re-dispara o auto-scale enquanto o `Resize` re-dispara o layout manual).

3. **Controles do Designer reposicionados novamente em runtime.** Nas 11 telas acima, os controles internos dos
   cards são posicionados no Designer e **reposicionados por `SetBounds` no `Resize`** — duplicidade de fonte da
   verdade de layout.

4. **Ausência total de tratamento de `DpiChanged`.** Nenhuma tela reage a mudança de DPI por monitor. Sob
   `SystemAware` isso é aceitável (há stretch); sob `PerMonitorV2` seria obrigatório.

5. **Telas maduras já usam TableLayoutPanel percentual (bom), mas nem todas.** As operacionais (Processo*) usam
   Anchor/Dock (sem `rootLayout`), e os diálogos pequenos usam posições do Designer sem container responsivo.

6. **Diálogos pequenos sem `MinimumSize`** (Login, TrocaSenha, ConfirmarReimpressao, SelecaoTara, PesagemMultipla,
   BalanceTest, TesteZebra). Em `WindowState.Normal` eles não maximizam; dependem só de `AutoScaleMode.Font` +
   Anchor. Sob DPI alto podem cortar conteúdo denso.

7. **Não há evidência de suposição fixa de 1920×1080** no design (o design é 1366×720), o que é **positivo** —
   as telas cabem em 1366×768 @100%. O risco real é DPI alto (125/150%) e multi-monitor, não a resolução baixa.

---

## 4. Matriz oficial de teste (recomendada)

| Resolução | Escala | Observações obrigatórias |
|---|---|---|
| 1366×768 | 100% | menor área suportada; barra de tarefas visível |
| 1600×900 | 100% | — |
| 1600×900 | 125% | primeira escala fracionária |
| 1920×1080 | 100% | baseline "confortável" |
| 1920×1080 | 125% | escala mais comum em notebooks novos |
| 1920×1080 | 150% | escala agressiva |
| 2560×1440 | 125% | alta densidade |
| 2560×1440 | 150% | alta densidade + escala agressiva |

Cenários transversais: **barra de tarefas visível**; **barra de tarefas com altura ampliada**; **1 monitor**;
**2 monitores**; **monitores com escalas diferentes**; **janela iniciada no monitor secundário**.

Critérios de aprovação por tela: sem corte, sem sobreposição, sem área vazia grande, footer dentro da
`WorkingArea`, botões principais visíveis, sem borrão de bitmap ao trocar de monitor.

---

## 5. Padrão responsivo oficial recomendado (FugaPET)

**Referência de ouro: `CamposEtiquetaForm`** — shell **container-based** (TableLayout percentual + cards Dock Fill) e, crucialmente, **sem re-escala manual de fonte** (0 `ApplyScaledFont`/`contentScale`). Ainda posiciona alguns internos por `SetBounds`, mas não empilha uma segunda escala de fonte sobre o `AutoScaleMode.Font`:

```
rootLayout : TableLayoutPanel (Dock=Fill)
  ├─ Row0 header  → 52px  (Absolute)
  ├─ Row1 body    → 100%  (Percent)
  │     contentPanel(Dock=Fill) → contentLayout(TLP 1col 100%, Padding 24)
  │        └─ bodyLayout (TLP): colunas 30% / 35% / 35% (= 100%), 1 linha 100%
  │              └─ cards (RoundedPanel) Dock=Fill + Margin  ← ConfigureCard()
  └─ Row2 footer  → 36px  (Absolute)
        footerBarLayout (TLP 6 colunas %: 16.6+16.6+26.8+20+10+10 = 100)
```

**Regras do padrão:**
- `rootLayout` com `Dock = Fill`; header/body/footer via `TableLayoutPanel` (linhas Absolute p/ header/footer, Percent p/ body).
- body em `TableLayoutPanel` com **colunas percentuais somando 100** e cards `Dock = Fill` + `Margin`.
- **nenhuma coordenada absoluta dependente da resolução** para posicionar cards/painéis.
- `MinimumSize` coerente (padrão atual **1180×648**).
- `AutoScroll` **somente** no conteúdo que precisa rolar (ex.: lista), nunca no root.
- fontes com **escala única** e `Math.Clamp` (uma passada só — sem re-escalar por cima do auto-scale).
- inputs com altura coerente (padronizar altura de `TextBox`/`ComboBox`).
- labels com `AutoSize`/`AutoEllipsis`.
- botões sempre visíveis; footer sempre dentro da `WorkingArea`.
- suportar 100/125/150%.
- **`AutoScaleDimensions` único** para todo o app (escolher 7×16 — baseline do gerador atual mais recente).

**Sobre `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`:**
Recomendado **como destino**, porém **NÃO aplicar globalmente agora**. As 11 telas com escala manual (§3.2) têm
risco real de **dupla escala** sob PerMonitorV2 (auto-scale do `DpiChanged` + relayout manual do `Resize`).
Caminho seguro: **piloto em 1 tela container-based**, validar a matriz, e só então avaliar a virada global —
migrando primeiro as telas manuais para o padrão container-based (que é DPI-neutro por construção).

---

## 6. Riscos de dupla escala (resumo)

**Grupo de risco de dupla escala de FONTE (9):** CargoForm, SetorForm, TipoTaraForm, TaraForm, EtiquetaForm,
ModeloEtiquetaForm, PerfilAcessoForm, PermissaoForm (8, via `ApplyScaledFont`) + CadastroUsuarioForm
(via `new Font(... * contentScale)`) — re-escalam FONTE manualmente sobre o `AutoScaleMode.Font`.

**`BalancaForm`** faz `SetBounds` manual mas **não** re-escala fonte → risco de layout, não de dupla escala de fonte.

Hoje estáveis nos níveis testados; **não ligar PerMonitorV2 sem migrar essas telas ou sem neutralizar o
relayout/refont manual durante `DpiChanged`.**

---

## 7. Classificação A/B/C/D

- **A — responsiva e consistente (1):** `CamposEtiquetaForm` (rootLayout + TableLayout percentual + cards Dock Fill). Ainda usa `SetBounds` para posicionar internos dinâmicos, **mas NÃO re-escala fonte manualmente** (0 `ApplyScaledFont`/`contentScale`) — é o único sem dupla escala de fonte.
- **B — parcialmente responsiva (19):** as 10 de cadastro com escala manual (Cargo, Setor, TipoTara, Tara, Etiqueta, ModeloEtiqueta, PerfilAcesso, Permissao, CadastroUsuario, Balanca) + 4 operacionais anchor-based (ProcessoEntradaProduto, ProcessoConsumoMaterial, ProcessoSemiAcabado, ProcessoProdutoAcabado) + 4 consultas (ConsultaEtiqueta, ConsultaHistorico, ConsultaIntegracaoSap, ConsultaOrdemProducao) + PainelInicial. *Funcionam/validadas nos níveis comuns, mas com escala manual e/ou baseline 7×15/7×16 misto.*
- **C — dependente de resolução (8):** diálogos `WindowState.Normal` sem container responsivo e (em geral) sem `MinimumSize`: Login, TrocaSenhaObrigatoria, ConfirmarReimpressaoEtiquetaForm, SelecaoTaraPesagemForm, PesagemMultiplaItemForm, BalanceTestForm, TesteZebraForm, DiagnosticoConsumoSap261Form.
- **D — alto risco de quebra (0 confirmados na config atual):** nenhuma **confirmada** como D em `SystemAware`. Porém o **grupo de 10 telas com escala manual** é o cohort de **maior risco na virada para PerMonitorV2** (D-risco condicional). A confirmação definitiva de D exige a **matriz física** (rodada 2).

> Observação honesta: a fronteira B/C/D só fecha com a **matriz física** da §4. Esta classificação é por
> inspeção estrutural, não por validação em tela.

**Contagem:** A=1 · B=19 · C=8 · D=0 (confirmados) · (10 em D-risco condicional a PerMonitorV2) · UserControls (3) herdam do host.

---

## 8. Telas prioritárias

1. **Padronizar `AutoScaleDimensions`** (7×16 único) — impacto global, baixo risco, sem redesenho.
2. **Grupo de escala manual (10 telas)** — maior risco na virada DPI; migrar gradualmente para container-based.
3. **Diálogos C (8 telas)** — adicionar `MinimumSize` + revisar Anchor; baixo esforço, reduz corte em DPI alto.
4. **Operacionais Processo* (4)** — anchor-based; validar na matriz antes de mexer (restrição funcional).

---

## 9. Plano incremental de correção (próximas rodadas — NÃO nesta)

1. **Rodada 2 — matriz física:** rodar a §4 nas telas piloto e no grupo de risco; registrar evidência real (print por resolução).
2. **Rodada 3 — padronização barata e global:** unificar `AutoScaleDimensions` (7×16) e adicionar `MinimumSize` aos diálogos C. Sem redesenho.
3. **Rodada 4 — piloto PerMonitorV2:** ligar PerMonitorV2 **só experimentalmente** e validar a tela piloto container-based na matriz (com especial atenção a dupla escala).
4. **Rodada 5+ — migração gradual das 10 telas manuais** para o padrão container-based (uma por vez, cada uma revalidada), removendo o `SetBounds`/`ApplyScaledFont` manual.
5. **Rodada final — virada global de PerMonitorV2** + tratamento de `DpiChanged` onde necessário.

Cada rodada: uma tela/mudança por vez, com revalidação; nunca migração em massa.

---

## 10. Primeira tela piloto recomendada

**`CargoForm`** — é a tela de cadastro **mais simples** (Nome + Situação + Descrição), já validada funcionalmente,
`rootLayout` presente, baixo raio de impacto. É o alvo ideal para **converter do padrão manual → container-based**
e depois servir de cobaia da matriz DPI/PerMonitorV2, sem risco para fluxos operacionais.

**Referência de destino do layout:** `CamposEtiquetaForm` (padrão de ouro da §5).

*(Alternativa de piloto puramente de DPI, sem redesenho: usar `CamposEtiquetaForm` — que já é container-based —
para validar PerMonitorV2 isoladamente antes de tocar nas telas manuais.)*

---

## 11. Arquivos que seriam alterados na próxima fase (previsão — nada alterado agora)

- **Padronização global de baseline (rodada 3):** os `*.Designer.cs` das telas em `(7F,15F)` para `(7F,16F)` — mudança de 1 linha por tela, sem redesenho. (Somente após decisão explícita.)
- **Piloto Cargo (rodada 4+):** `Tela/Cadastro/CargoForm.cs` + `CargoForm.Designer.cs` (converter internals para TableLayoutPanel; remover `SetBounds`/`ApplyScaledFont` manuais).
- **PerMonitorV2 (rodada final):** `FugaPET_Dev.csproj` (`<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`).
- **Diálogos C:** `MinimumSize` nos respectivos `*.Designer.cs`.

Nenhum desses arquivos foi alterado nesta rodada de diagnóstico.

---

## 12. Testes diagnósticos criados

`FugaPET_Dev.Tests/Responsividade/ResponsividadeDiagnosticoTests.cs` — testes de **análise** (leem propriedades
reais dos `*.Designer.cs`, não strings arbitrárias), todos passando, que **travam invariantes** e documentam os
riscos sem alterar comportamento:
- todos os Forms usam `AutoScaleMode.Font`;
- nenhum código define HighDpiMode/manifest/dpiAware (config global = default `SystemAware`);
- nenhum handler `DpiChanged` existe (gap para PerMonitorV2);
- telas maduras declaram `MinimumSize`;
- `CamposEtiquetaForm`: percentuais de coluna do body somam 100 e do footer somam 100 (padrão de ouro);
- `AutoScaleDimensions` de todo Form pertence ao conjunto conhecido {7×15, 7×16} e **os dois baselines coexistem** (documenta a inconsistência).

---

### Confirmações
- **Nenhum layout foi alterado.** Somente leitura + criação deste relatório e de testes de análise.
- **HML não foi analisado nem alterado.**
- **Nenhum SQL executado; sem migration; sem alteração de regra de negócio/SAP/banco.**
- **Sem commit.**
