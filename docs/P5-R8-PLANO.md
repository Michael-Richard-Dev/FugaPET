# R8 — plano e registro de execução

Gate: FUGAPET-ENTRADA-MP-P5-R8-CSHARP-D2D3-B5-INTEGRATION.
Base: 3c542c0c54e0c1ca2689105182459ce9ae82ee74.
Desenho e execução offline aprovados por HERA V6 em 19/10/2026.

## Restrições

Nenhum acesso PostgreSQL/SAP, migration, deploy ou push. Um commit consolidado.
R7B intocado; writer/controller/cliente continuam bloqueando ROH. Nenhum flag
operacional habilitado. Não modificar scripts aprovados nem inventar projeção FK.

## Execução

- [x] Testes RED dos contratos R6E.1, coordenação transacional e separação de papéis.
- [x] Adaptador R8 com executor transacional compartilhado: reserva P5, carimbo e
      reserva legada na mesma conexão/transação; compensação pré-HTTP igualmente.
- [x] Marcação de envio, commit e selo de fronteira em transações sucessivas
      explícitas. Selo repetido/commit ambíguo nunca autoriza outro dispatch.
- [x] Resultado normal coordenado com legado; retorno tardio preservado apenas
      como evidência, sem inferir confirmação pelo estado de outro resultado.
- [x] Recovery com executor separado, geração e identidade explícita; BLOQUEADO
      enquanto faltar API privilegiada conjunta, sem simular suporte real.
- [x] Provas offline de concorrência, falhas, restart, timeout e barreiras reais;
      build Release, revisão, commit e ZIP com TRX/manifesto.

## Decisões vinculantes

Ruling: não alterar a consulta D2 R5B para inserir FK por suposição. A projeção
ausente e bootstrap não homologado são dependências explícitas e bloqueantes.
Ruling: os quatro pontos B5 (escopo HIBE, compensação por último registro,
confirmação por EXISTS e transição de lote ausente) não são corrigidos com SQL
alternativo. O adaptador só declara coordenação disponível com todos esses
contratos homologados; a execução Npgsql real mantém a trava estática R7B.
Ruling: testes usam executores transacionais simulados e HTTP falso. PASS offline
não comprova locks, grants, triggers ou atomicidade real no PostgreSQL.

## Registro final de implementação

Ruling: revisão detectou corrida entre snapshot de geração e mutador SQL,
helper privado sem grants e falta de rastreabilidade no UPDATE legado preparado.
Removidos TODOS os UPDATEs legados novos do produto. A interface privilegiada
obrigatória recebe documento/item/ator e mesma conexão/transação; não há uma
implementação concreta inventada. FencingAtomicoDisponivel=false no Npgsql é
trava adicional permanente deste incremento, independente de flags declarativas.

Ruling: adendo B5 bloqueante recebido e hash validado. VersaoGuarda é separada
de GeracaoRecovery; não há classificação persistida inventada nem status de lote
artificial. Contratos exatos e lacunas estão em P5-R8-INTERFACE-GAIA.md.

Ruling: nova reconsulta de resultado não confirma por estado P5 isolado. A fixture
R7Backend dos dois casos impactados foi adaptada para comprovar a identidade e o
resultado realmente persistido pelo fake; asserts originais preservados.

RED inicial: tipos R8 ausentes. RED revisão: três defeitos materiais reproduzidos.
RED fixture: dois casos exigiram adaptar o double ao novo contrato explícito.
GREEN final: 54/54, zero failures/skipped. Desses, 42 novos R8, dois casos de
fixture impactados e dez casos estritos de preservação das barreiras/HIBE/MO.
Build Release: PASS, zero erros e sete warnings nos mesmos arquivos da baseline.
Nenhum QA amplo R7B executado. Nenhuma dependência SQL alegada resolvida.

Revisão final: sem caminho produtivo ativo crítico/importante identificado.
Achado latente adicional HTTP positivo sem selo reproduzido RED e corrigido:
persistir incerteza e impedir que o coordenador conclua confirmação por esse retorno.
