# R8B — preparação offline da evidência SAP ProductType

GATE=FUGAPET-ENTRADA-MP-P5-R8B-TRUSTED-SAP-EVIDENCE-ADAPTER

Base: `b7a1a5904fee239a2a485814db8d6626ffad7dc6` (R8A preservado).
Branch: `codex/entrada-mp-p5-r8b-procedencia-sap`.
Checkout: `FugaPET_Dev`, WinForms .NET 10 Windows.

## Decisão de arquitetura dentro do escopo autorizado

**TRUSTED_BACKEND_FOUND=NO no source da base aprovada.** Há somente projeto
WinExe/WinForms e projeto de testes na solução. A inspeção dos projetos,
composição e contratos não identificou host web/worker autorizado, ingestor
privilegiado de Product Master ou implementação de `IContratoLegadoP5R8`.
O nome `EntradaMpP5BackendR8` designa uma fronteira de repositório local, não
um servidor autenticador de procedência SAP.

`ProductMasterSapServico.ObterJsonProdutoAsync` executa GET autenticado dentro
da aplicação desktop, com a configuração do desktop. Isso não o torna coletor
autoritativo para PostgreSQL. O serviço existente e a sua projeção compartilhada
com outros processos não foram alterados. A validação `Consultado=true`,
`EhSimulado=false` ou a correspondência de material em memória não prova roles,
instalação do backend ou persistência confiável.

Aplica-se o item 4 da missão: **somente interfaces, DTOs e validações puras**.
Não existe adaptador HTTP/Npgsql operacional novo, host, DI, credencial, retry,
worker ou autorização de escrita. As interfaces são internal, como os contratos
P5 existentes. Um futuro projeto backend confiável e seu compartilhamento de
contratos exigem implementação/homologação próprias; este incremento não cria
nem declara implantável esse backend.

## Plano focal e arquivos

1. Conferir base/status, isolamento e existência de backend autorizado.
2. Escrever testes; adicionar DTOs/interfaces e implementar leitura/consistência.
3. Verificar regressões selecionadas, Release e TRX; documentar dependências ABI.
4. Criar um commit focal e ZIP QA com fontes, diff e hashes verificáveis.

Arquivos criados (nenhum arquivo existente alterado):

- `Servicos/Entrada/P5/ContratosEvidenciaProdutoSapR8B.cs`: campos SAP, contexto
  de coleta, recibo, identidade local, vínculo exato de tentativa e interfaces.
- `Servicos/Entrada/P5/LeitorEvidenciaProdutoSapR8B.cs`: `Ler`, parse puro por
  allowlist; `ExtrairOffset` preserva marcador textual, sem normalizar instantes.
- `Servicos/Entrada/P5/ValidadorEvidenciaProdutoSapR8B.cs`:
  `VerificarConsistencia` e `MesmaTentativa`; não autorizam dispatch.
- `FugaPET_Dev.Tests/IntegracaoSap/EntradaMpP5R8BEvidenciaTests.cs`:
  fixtures e verificações offline do novo contrato e do bloqueio real no writer.
- Este relatório e `docs/P5-R8B-INTERFACE-GAIA.md`.

## Matriz de procedência

| Campo | Fonte efetiva / responsável futuro | Representação C# / tratamento | Limite de prova nesta entrega |
| --- | --- | --- | --- |
| Product | Resposta A_Product.Product | Campos.Product; texto original, sem Trim/conversão numérica, preserva zeros | Deve coincidir exatamente com material e identidade DB; fixture não é GET autenticado |
| ProductType | Resposta A_Product.ProductType | Campos.ProductType; texto original sem normalização; ausente fica vazio; ROH/HIBE explícitos | Ausente/outro/divergente bloqueia; ausência de ZZ1/guarda jamais significa HIBE |
| LastChangeDateTime | Resposta A_Product.LastChangeDateTime | LastChangeDateTimeOriginal string nullable; offset textual separado quando explícito | Sem conversão para DateTime, UTC ou fuso local; não é versão imutável ou ETag |
| LastChangeDate | Resposta A_Product.LastChangeDate | LastChangeDateOriginal string nullable | Preservada sem interpretar fuso; não completa timestamp ausente |
| LastChangedByUser | Resposta A_Product.LastChangedByUser | String nullable, somente metadado controlado | Não é usuário autenticado do backend nem prova de autoria local; ToString redigido |
| ETagSap | Header ETag, somente se realmente recebido | String nullable; ausência permanece null | ARES informa NOT_AVAILABLE; nada é fabricado a partir das datas |
| Ambiente / Mandante | Configuração atestada do futuro backend | Contexto.Ambiente/Mandante | QAS110/110 informados pela HERA; não derivar do Product nem aceitar desktop como autoridade |
| OrigemTecnica | Contrato/configuração do futuro backend | API_PRODUCT_SRV/A_Product | String identifica a origem pretendida; não comprova que o GET aconteceu |
| IdentidadeBackend | Identidade de serviço atestada no futuro host | Contexto.IdentidadeBackend | Nunca usuário/senha/token; string livre recebida não autentica um coletor |
| ConsultaIniciadaEm / ConsultaConcluidaEm | Relógio do backend antes/depois do GET | DateTimeOffset, intervalo não invertido | Não usar relógio do desktop como atestação; fixtures usam instante fixo |
| CorrelacaoColetaBackend | Criada pelo backend para a coleta | Guid explícito e separado da correlação P5 | Não se afirma origem SAP; não se deriva de header/campo inexistente |
| ReferenciaEvidencia / VersaoEvidencia | Futuro armazenamento privilegiado Gaia | Recibo tipado | Não geradas pelo parser; recibo em memória não é confirmação PostgreSQL |
| RegistradaEmBanco / RevogadaEmBanco | Servidor PostgreSQL e política aprovada | DateTimeOffset nullable | Sem persistência/revogação comprovadas não há autorização; null não prova estado remoto |
| VinculadaEmBanco | Transação privilegiada de vínculo P5 | Recibo de vínculo | Timestamp fornecido pelo chamador não prova COMMIT ou atomicidade |

Os cinco campos SAP foram informados como confirmados por ARES; não se repetiu
o GET nem se consultou metadata SAP. Product=1000111/ProductType=ROH é o exemplo
reportado pela HERA, não valor fixo do aplicativo. Fixtures usam produto sintético.
Dados não selecionados são descartados; corpo JSON completo não é armazenado.
Tipos JSON inesperados ou propriedades controladas duplicadas rejeitam a leitura.

## Correspondência e vínculo — sem falsa autoridade

Identidade tipada: PO/item canônico, lançamento, material, PK do item local e FK
local SAP. Tentativa: EscopoP5, tentativa, claim token, correlação P5, versão da
guarda e geração recovery (separada). O vínculo acrescenta referência/versão da
evidência, tipo vinculado e correlação da coleta. Não existe seleção pela última
data, troca implícita de tentativa ou preenchimento de FK pelo cache.

`VerificarConsistencia` rejeita produto/material ou identidades divergentes,
tipo desconhecido, metadados incompatíveis, ausência de recibo persistido,
revogação, ausência de vínculo, outra tentativa/token/correlação/versão/geração e
troca de classificação. Diferenças do snapshot não são assimiladas automaticamente.
Não consulta banco, não grava vínculo, não verifica assinatura e não consome claim.

Mesmo quando todas as verificações de forma/igualdade passam, o resultado é
**CoerenteSemAutoridade**, nunca PERMITIR/ENVIAR. Um record fabricado com valores
plausíveis pode ser consistente: por isso o validador não é a fronteira de
confiança. Nenhum resultado novo é conectado ao controller/writer ou usado para
substituir `ClassificacaoDuravelP5R8`. A futura função privilegiada deve refazer as
validações sob lock/CAS, atestar procedência e comprovar COMMIT.

## ABI e bloqueios

`SQL_ABI_COMPATIBILITY=PENDING_GAIA_R8B_SQL`.
As assinaturas conceituais RegistrarEvidenciaClassificacaoSapV1 e
ResolverClassificacaoPersistidaP5V1 foram representadas como interfaces C#.
O vínculo de tentativa é uma exigência tipada, não função SQL existente.
Não há pacote SQL R8B entregue neste gate para comparação executável.

O contrato documental R8A exigia revisao_origem e valida_ate. ARES não fornece
ETag e a política temporal ainda não foi aprovada. Os novos DTOs não inventam
esses valores: o mapeamento para a ABI anterior está **bloqueado**, não corrigido
com timestamp, hash de payload, correlação ou TTL escolhido pelo aplicativo.
Detalhes e parâmetros constam do relatório de interface para Gaia/HERA.

Bloqueios remanescentes:

- Host/coletor backend confiável autorizado e identidade/grants não homologados.
- Persistência versionada, procedência, revogação e binding durável não entregues.
- Política de revisão sem ETag, validade temporal e reclassificação não aprovadas.
- ABI SQL R8B, fencing atômico e prova transacional/runtime pendentes.
- `FencingAtomicoDisponivel=false` e `MutacoesHomologadas=false` preservados.
- D3 real, ROH/multi-item/misto e segundo POST continuam bloqueados pelos guards
  existentes; nenhuma ativação via novas flags ou composição foi adicionada.
- HIBE, ManufacturingOrder, ZZ1 e SQL R8A/R6E.1 intocados; 04_R6B não incorporado.

## Testes e limites da evidência

Testes novos cobrem ROH/HIBE em contexto de coletor **simulado**, produto
divergente, tipo ausente, ETag inexistente, datas sem/com offset, JSON inválido/
duplicado, evidência não persistida/revogada, tentativa sem vínculo, troca de tipo,
fencing exato, identidades locais e metadados divergentes. O writer ROH é chamado
duas vezes com transporte interceptado e deve continuar sem dispatch.

Regressões selecionadas, justificadas pela fronteira de classificação/envio:
`EntradaMpP5R7Tests`, `EntradaMpP5R7BTests`, os métodos
`TimeoutOu500NaoPermiteSegundoEnvio`,
`ConcorrenciaSimuladaPermiteUmDispatchECommitsPrecedemHttp` de R7Backend e
`ExecutorNpgsqlRealNuncaAbreBancoEnquantoTravaEstaticaEstaFechada` de R8.
Não se repetiu a suíte completa nem os 54 testes R8. Os handlers legados de HIBE/
ManufacturingOrder simulam GET/POST apenas dentro do processo, sem rede real.

Os testes não demonstram autenticação SAP, isolamento de um host real, grants,
locks, atomicidade ou persistência PostgreSQL. Red de desenvolvimento (leitor e
validador ainda sem comportamento): 43 casos, 32 falhas e 11 aprovados; depois da
implementação, os mesmos 43 passaram. TRX final inclui os casos adicionais e
regressões selecionadas. A revisão focal independente identificou Trim dispensável
em Product/ProductType: dois testes reproduziram a perda de representação e a
normalização foi removida. Erro de compilação inicial no tipo de parâmetro do
teste foi corrigido antes desse red, sem contabilizá-lo como prova funcional.
Logs/TRX com stack trace de falha não são empacotados.

Verificação final: **67 aprovados, zero falhas/ignorados** (49 novos e 18
regressões focais). Build Release da solução com Rebuild: **zero erros e 10
avisos preexistentes em arquivos intocados**. TRX final:
`TestResults/R8B/r8b-verified.trx`; build: `TestResults/R8B/release-build.log`.

## Teste manual — exclusivamente offline

1. Na worktree R8B, conferir que `git diff HEAD^ HEAD --name-only` lista somente
   os seis arquivos novos acima; nenhum SQL, controller, writer ou Designer.
2. Executar `dotnet test FugaPET_Dev.Tests/FugaPET_Dev.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~EntradaMpP5R8BEvidenciaTests`.
3. Conferir os casos de ROH/HIBE: ambos podem ser consistentes no contrato
   simulado, mas o resultado é CoerenteSemAutoridade. Produto divergente, revogação,
   falta de vínculo e outra tentativa devem retornar diagnóstico bloqueante.
4. Conferir datas sem offset e ETag ausente: representação original preservada,
   offset/ETag null, nenhuma conversão de fuso ou fabricação de revisão/TTL.
5. No pacote QA, conferir o TRX das regressões e o caso de duas chamadas ROH:
   dispatch real continua zero. Não iniciar aplicativo conectado, não executar
   SQL/migration nem testar GET/POST real para validar este incremento.

Nenhuma credencial, configuração operacional, URL sensível ou payload SAP real
incluído. SAP_GET_REAL/SAP_POST/SAP_WRITE/DB_WRITE/DDL_EXECUTION/MIGRATION/DEPLOY/
GIT_PUSH=0. Próximo dono: HERA V6. Ativação operacional: **NO**.
