# Prontidão do P5/R7 no DEV110

Levantamento de 13/11/2026. Objetivo: dizer, com evidência, o que falta para o
envio 101 da Entrada deixar de ser barrado pelo R7 — **sem** desligar nenhuma
trava.

## Onde o envio para hoje

A tela responde:

> P5 R7 bloqueado: dependências e segurança não homologadas.

Origem: `Servicos/Entrada/P5/ContratosP5R7.cs`. O bloqueio é aplicado em três
pontos (`IMaterialDocumentSapServico`, `MaterialDocumentSapApiClient`,
`MaterialDocumentSapServico`) e todos chamam apenas `ExigeP5(requisicao)`.

**`MutacoesHomologadas` não é lido por nenhum deles.** Não existe configuração,
variável de ambiente ou permissão que abra este portão: ele é incondicional no
fonte para todo item que "exige P5".

E, na prática, **todo** item da Entrada exige:

```csharp
return tipo is not (Quimico or Embalagem or Semiacabado)
    || !string.IsNullOrEmpty(item.Zz1PesoEntradaMmi)
    || !string.IsNullOrEmpty(item.Zz1PesoEntradaMmiu);
```

`Zz1PesoEntradaMmi` é o campo onde a Entrada grava o peso pesado — está sempre
preenchido. Logo o bloqueio alcança matéria-prima **e** químico, sem exceção.

## Por que a trava está correta

O backend do R7/R8 **não existe em lugar nenhum**:

| onde procurei | resultado |
|---|---|
| banco do DEV (`desenvolvimento`) | só a tabela `entrada_mp_p5_exclusividade_item_pedido` (incremento 056) |
| banco do Q (`homologacao`) | **nem a tabela**; só `fn_entrada_produto_sap_guard_counts` |
| `BancoDados/` do repositório | só os incrementos 056 (DEV e Q) |

O código referencia, e não há onde buscar:

- view `vw_entrada_mp_p5_d2_recuperacao`
- `fn_entrada_mp_p5_reservar`, `_marcar_envio`, `_confirmar_fronteira`,
  `_cancelar_pre_post`, `_resultado_http`, `_registrar_retorno_tardio`,
  `_recuperar_assumir`, `_recuperar_finalizar`, `_recuperar_cancelar_reserva`
- `fn_p5_r8b_ingestar_producttype`, `_resolver_classificacao`, `_vincular_classificacao`
- `fn_p5_r8c_resolver_recibo`, `_obter_evidencia_recibo`, `_obter_vinculo_recibo`,
  `_vincular_tentativa_recibo`

Ou seja: a mensagem "dependências não homologadas" é literal. Mesmo que alguém
abrisse o portão, o R7 falharia na primeira chamada.

## O que NÃO deve ser feito

1. **Não escrever esse DDL por engenharia reversa.** São 17 funções e uma view de
   um subsistema transacional e fail-closed (reserva, fronteira, retorno tardio,
   recuperação). Inferir a semântica a partir das chamadas produziria algo que
   compila e mente.
2. **Não tirar o prefixo `homologacao.` do R7/R8.** Diferente das fontes locais do
   P5 (corrigidas em 13/11), aqui o prefixo é **contratual**: os testes de ABI
   comparam o texto do comando C# com o SQL de dentro de um pacote GAIA assinado,
   e ainda fixam o SHA-256 do ZIP. Mudar o prefixo quebra a correspondência com o
   artefato homologado. O esquema certo virá declarado pelo próprio pacote.

## O que falta, concretamente

Os dois pacotes SQL candidatos, que os testes de ABI esperam em
`C:\Visual Studio 2026\Projetos\Fuga\FugaPET\Backups\artefatos_abi_testes\`
(**a pasta não existe nesta máquina**):

| variável | arquivo | SHA-256 esperado |
|---|---|---|
| `FUGAPET_R8C_SQL_PACKAGE` | `FUGAPET_ENTRADA_MP_P5_R8B1_SQL_V_ATIVOS_FIX_NAO_APLICAR.zip` | `9D12BCD1000D7487582717B08B401BAC8966D8B779D9E3B995EF9B2DD5FB1CC8` |
| `FUGAPET_R8D_SQL_PACKAGE` | `FUGAPET_ENTRADA_MP_P5_R8C_SQL_ABI_CANDIDATO_NAO_APLICAR.zip` | `099ABCE0F27298CEAE9BE24F4D0B305D74C6A9D2E2D8B94453F8177C92F7FB0B` |

Os nomes terminam em **NAO_APLICAR**: são candidatos, não pacotes liberados.

**Efeito colateral de restaurá-los:** 77 dos 122 testes que falham hoje na suíte
falham só por causa dessa pasta ausente (`EntradaMpP5R8CAbiTests` e
`EntradaMpP5R8DAbiTests`). Restaurar os ZIPs, sem aplicar nada ao banco, derruba a
baseline de 122 para 45.

## Sequência para liberar, quando houver decisão

1. Restaurar os dois ZIPs e conferir os SHA-256 acima. Suíte volta a 45 falhas.
2. Homologar o conteúdo deles (dependências + segurança) — decisão de projeto.
3. Traduzir para incrementos GAIA do DEV (`desenvolvimento`) e do Q
   (`homologacao`), com PREFLIGHT/PROPOSTA/VALIDAÇÃO/ROLLBACK, e aplicar.
4. Só então rever `ContratosP5R7.cs`, com a homologação registrada.

Nada disso é trabalho de código solto: o passo 2 é o que a mensagem de bloqueio
está pedindo.
