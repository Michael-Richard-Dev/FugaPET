# Checklist de Publica??o HML - FugaPET

Antes de entregar a m?quina do usu?rio, verificar:

- [ ] `configuracao.banco.json` existe na pasta publicada
- [ ] `configuracao.terminal.json` existe na pasta publicada
- [ ] `configuracao.sap.exemplo.json` existe na pasta publicada
- [ ] `configuracao.sap.json` existe na pasta publicada quando o ambiente for operar SAP real
- [ ] `production_order_base_url` configurada
- [ ] `material_document_base_url` configurada
- [ ] `product_base_url` configurada
- [ ] `sap_client = 110`
- [ ] host SAP HML correto: `vhfufds4ci.sap.fugacouros.com.br`
- [ ] credenciais configuradas sem versionar senha real no Git
- [ ] `Test-NetConnection vhfufds4ci.sap.fugacouros.com.br -Port 44300` OK
- [ ] `API_PRODUCTION_ORDER_2_SRV/$metadata` retorna HTTP 200

Observa??es:

- N?o commitar `configuracao.sap.json` com senha real.
- O publish inclui `configuracao.sap.exemplo.json`.
- Se existir `configuracao.sap.json` local e seguro na pasta do projeto no momento do publish, ele ? copiado para a pasta publicada por MSBuild, mas continua ignorado pelo Git.
