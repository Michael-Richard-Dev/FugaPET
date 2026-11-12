# R8 — integração offline D2/D3/B5

Gate: FUGAPET-ENTRADA-MP-P5-R8-CSHARP-D2D3-B5-INTEGRATION.
Base preservada: 3c542c0c54e0c1ca2689105182459ce9ae82ee74.
Branch: codex/entrada-mp-p5-r8-integracao.

## Resultado e limite da entrega

PREPARACAO_CSHARP_OFFLINE_PARCIAL_COM_DISPATCH_BLOQUEADO.
Não é implementação SQL B5, autorização de POST nem prova transacional PostgreSQL.
Não há composição do novo backend em Program/controller. As barreiras ROH do R7B
continuam intocadas. Executor Npgsql declara FencingAtomicoDisponivel=false e
verifica essa trava mais MutacoesHomologadas=false ANTES de abrir conexão.
Nenhuma combinação de cinco flags de dependências habilita esse executor real.

O adaptador compõe as operações numa única sessão transacional compartilhada.
As três mutações legadas exigem IContratoLegadoP5R8 privilegiado, ainda sem
implementação SQL homologada: não há UPDATE legado alternativo novo em produção.
Os testes fornecem APENAS dentro do assembly de testes uma sessão simulada com
recibos e rollback. Os recibos simulados não são evidência de escrita no banco.

## Autoridades verificadas

| Pacote original | SHA256 | Estado |
| --- | --- | --- |
| FUGAPET_ENTRADA_MP_P5_R6E1_RAISE_FORMAT_QA_NAO_APLICAR.zip | 1F6FC1BA580BE896A7C64CF1DE3A45CCA1BECA7B640848E577841D3540781E96 | integração estática; nove entradas; nenhum SQL executado |
| FUGAPET_ENTRADA_MP_P5_R8_B5_CONTRATO_BLOQUEANTE_NAO_APLICAR.zip | 7AD86D1EE449913F73F861546C7B6063F4BC3766F1F65CF6990C04C9B3C34D8A | BLOCKED_CONTRACT_ONLY_NO_SAFE_TRIGGER_DELTA |

O SQL B5 é um contrato bloqueante, não migration aplicável. Não instalar 04_R6B,
não modificar R6E.1, não ampliar grants para contornar helper privado.

## Matriz D2/D3/B5

| Fronteira | Implementação/validação R8 | Dependência que mantém operação bloqueada |
| --- | --- | --- |
| D2 parametrizado | Reutilizado D2 R5B e view segura; três parâmetros tipados; FK local positiva e igual ao cache continua obrigatória | SELECT final R5B não projeta FK local. Cache não substitui FK. Nenhuma alteração de SQL autorizada por inferência |
| Bootstrap | Ausência de guarda permanece BLOCK. Reservar R6E.1 já oferece bootstrap sob lock | Sem contrato conjunto D2/primeira reserva homologado não remover bloqueio circular no C# |
| Reserva | fn_reservar + contrato legado obrigatório na MESMA sessão/tx; falha em carimbo/legado impede commit | Função privilegiada de classificação/fence/carimbo/reserva legada ainda ausente |
| ENVIO_INICIADO | Contrato de retorno R6E.1 corrigido no adaptador R8; commit antes da segunda transação de selo | Versão de claim não é parâmetro dos mutadores R6E.1; verificação da view não é fencing atômico |
| Selo | confirmar_fronteira em transação posterior; selo já emitido nunca autoriza segundo dispatch | Executor real fechado; falta prova B5/roles/autorização operacional |
| R3B | Pedido legado tipado exige tentativa, token, correlação, versão, geração separada e estado anterior; cancelamento P5 na mesma sessão | Contrato privilegiado deve comparar fence sob lock; não aceitar compensação apenas por PK/status |
| Resultado | resultado_http + confirmação legada por recibo de cobertura 1/1 e documento/ano/item iguais; rollback simulado se legado falha | Função SQL conjunta e prova persistida integral ainda ausentes. Não inferir conclusão por EXISTS ou só estado P5 |
| Resultado incerto | Não restaura ERRO_SAP nem libera reserva; documento incerto não é descartado ao montar parâmetros | Reconciliação operacional não homologada |
| Retorno tardio | Chama contrato append-only existente; exige escalonamento, nunca confirma legado automaticamente | Classificação/versão atômicas continuam pendentes; reconsulta positiva conservadoramente BLOCK |
| Restart | Consulta por PO/item/lançamento/tentativa/correlação exatos; sem ORDER BY/LIMIT 1 | Retomada operacional da UI permanece antiga e ROH bloqueado; não substituir por maior PK |
| PROVIDER/RECOVERY | Objetos/executores distintos e papel declarado; mesmo objeto/papéis invertidos rejeitados | Isso NÃO atesta roles/credenciais efetivas diferentes; Janus/Gaia devem homologar identidades reais |
| Recovery | API R8 retorna bloqueio explícito antes de SQL, RecoveryR6BAprovado=false | Falta sincronização privilegiada P5+legado; não reutilizar provider para recovery |
| Writer/HTTP | Sem mudanças: ROH/desconhecido/misto bloqueados; HIBE confiável e ManufacturingOrder preservados com HTTP falso | Nenhuma nova autorização de ROH criada |

## B5-01 — classificação positiva

Caminho atual: A_Product/ProductType consultado → preparador → classificação do
domínio → vínculo por referência ao item do request → preflight/writer/cliente.
Essa evidência C# é efêmera, NÃO é classificação autoritativa persistida no banco.
IContratoLegadoP5R8.ValidarEscopoPersistidoAsync exige o vínculo de escopo,
FK do item SAP positiva, ProductType=ROH e referência autoritativa não vazia.
A implementação privilegiada deverá verificar a origem real; dados fornecidos
pelo chamador, ZZ1 ou ausência de guarda jamais constituem essa prova.
Sem armazenamento/validador DB homologado, não existe implementação concreta.

## B5-02 — identidade e fences

ClaimP5 separa VersaoGuarda e GeracaoRecovery. RecoveryP5 mantém Token e Geracao
exclusivos de recuperação; não são usados como claim de envio. Tokens não entram
em logs/UI/ToString. PedidoLegadoP5R8 tem ToString constante redigido.
A leitura da view verifica geração e versão positiva, mas é APENAS snapshot.
Não elimina corrida entre leitura e mutação no READ COMMITTED. Por isso executor
Npgsql NÃO oferece a capacidade FencingAtomicoDisponivel. Apenas o fake de teste
a oferece para simular a futura transação privilegiada; isso não valida PostgreSQL.

## B5-03 — cobertura e resultado

ReciboLegadoP5R8 exige exatamente um item associado e coberto e documento/ano/item
iguais ao resultado validado. Zero itens, dois itens ou cobertura incompleta
causam rollback simulado. ROH multi-item/misto continua bloqueado como documento
inteiro, sem split. Reconsulta após commit ambíguo de resultado positivo retorna
BLOCK até existir prova conjunta de persistência P5+itens+cabeçalho; estado P5
CONFIRMADO isolado, até com documento igual, não basta.
O default IBackendP5 de reconsulta também foi tornado conservador. Backend antigo
não ganha falsa confirmação por esse novo ponto de extensão.

## B5-04 — status realmente escritos

| Fluxo legado R7B preservado | Escrita real no source |
| --- | --- |
| Criar cabeçalho/item/lote | FINALIZADO_LOCAL |
| Reserva de envio | cabeçalho FINALIZADO_LOCAL/ERRO_SAP → ENVIADO_SAP |
| R3B pré-POST | cabeçalho ENVIADO_SAP → status anterior capturado |
| Sucesso SAP | item/cabeçalho → CONFIRMADO_SAP, rastreabilidade e usuário |
| Falha conclusiva no caminho legado | item/cabeçalho → ERRO_SAP conforme controller |
| Timeout/5xx/inconclusivo R7B | mantém reserva; exige reconciliação |
| Exclusão da última pesagem válida | lote FINALIZADO_LOCAL → CANCELADO, sob guardas próprias |

Não foi criado UPDATE de lote ENVIANDO_SAP, nem confirmador SAP de lote, nem
status novo. A dependência TransicaoLoteHomologada significa homologar a decisão
contratual sobre esses lotes e seus writers, NÃO obrigar uma transição fictícia.
Gaia deve proteger os UPDATEs de cabeçalho/item efetivamente usados e preservar
043/HIBE/ManufacturingOrder. Nada do R8 altera esses fluxos operacionais.

## Segurança e QA

Sem novas credenciais, arquivos de configuração operacional, ENV ou grants.
Fixtures usam IDs/UUIDs gerados e referências sintéticas; transporte fake local.
Nenhuma regressão ampla R7B repetida. Build e TRX reais pertencem ao R8.
Eventos transacionais simulados comprovam ordem de chamadas e rollback do fake,
não locks, WAL, SSI, privileges ou COMMIT PostgreSQL real.
