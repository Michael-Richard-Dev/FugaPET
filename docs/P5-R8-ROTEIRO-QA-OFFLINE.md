# R8 — roteiro HERMES estritamente offline

1. Conferir manifesto SHA256 do ZIP, diff e commit R8, cujo parent é R7B.
2. Não iniciar WinForms nem configurar conexão real. Não aplicar nenhum SQL.
3. Restaurar dependências somente do cache local, se necessário, e build Release.
4. Executar `EntradaMpP5R8Tests` e os dez focais de preservação listados no TRX.
5. Conferir no TRX: sem skipped/falhas, nomes completos e resultados reais.
6. Inspecionar eventos do fake: um BEGIN/COMMIT na reserva P5+legado; rollback
   integral se a etapa legada falha; COMMIT_ENVIO e novo COMMIT_SELO antes do
   delegate de transporte. Selo repetido e COMMIT ambíguo não produzem dispatch.
7. Conferir fixtures de PO4500000071/item00010/material1000111: quantidade
   documental 1000.000 KG, peso líquido independente em ZZ1; nenhum HTTP real.
8. Validar timeout/concorrência/restart/ERRO_SAP impedido pelo D2/retorno tardio,
   divergência de documento, cobertura parcial ou multi-item e falta de prova.
9. Confirmar barreiras reais do writer/cliente com HttpMessageHandler falso e
   HIBE/ManufacturingOrder preservados, sem ZZ1 indevido.
10. Conferir que TransacaoEntradaMpP5R8 nunca abre banco: capacidade fencing
    false e trava estática false. Nenhuma implementação concreta da nova
    interface privilegiada existe. Recovery R8 não executa SQL.
11. Conferir ausência de novos segredos/ENV/config operacional e nenhum push.

Não é teste transacional PostgreSQL. O roteiro manual é inspeção de evidência
e execução de testes offline, não instrução para pesagem, login ou POST SAP.
P5_OPERATIONAL_ACTIVATION=BLOCKED.
