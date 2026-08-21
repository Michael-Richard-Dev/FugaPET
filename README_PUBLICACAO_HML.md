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

## API de embalagem (Produto Acabado - norma / Integration Suite - INT012)

A consulta da norma de embalagem usa um endpoint PROPRIO no SAP Integration Suite, com credenciais
PROPRIAS (nunca as SAP standard) e allowlist propria.

- [ ] `packaging_base_url` configurada com a rota exata consultavel do Integration Suite (`.../ZAPI_PACKAGING_SRV/GetPackagingSet`)
- [ ] host do Integration Suite (ex.: `*.cfapps.br10.hana.ondemand.com`) presente em `packaging_hosts_permitidos`
- [ ] variavel de ambiente `FUGAPET_SAP_PACKAGING_USERNAME` definida (usuario proprio da embalagem)
- [ ] variavel de ambiente `FUGAPET_SAP_PACKAGING_PASSWORD` definida (senha propria da embalagem)
- [ ] `FUGAPET_SAP_PACKAGING_CLIENT` definido SOMENTE quando o endpoint exigir mandante
- [ ] GET seguro de validacao: `GetPackagingSet?$filter=Material eq '4000108'` retorna HTTP 200
- [ ] a resposta do GET de validacao contem `PkgInstructionItems` (ou `_PkgInstructionItems`) com um item `P` e um item `I`/`M`
- [ ] nunca imprimir usuario/senha/Authorization/cookie/token em log ou tela

## POST de Handling Unit (Produto Acabado - modo HML repetitivo controlado)

O POST /HandlingUnit e autorizado por uma flag PROPRIA e ISOLADA, independente da escrita SAP generica.

- [ ] `FUGAPET_SAP_WRITE_ENABLED` permanece `false` (nao habilita HU)
- [ ] `hu_write_habilitado` / `FUGAPET_SAP_HU_WRITE_ENABLED` = `false` por padrao; `true` SOMENTE no ambiente HML autorizado
- [ ] `handling_unit_base_url` (env `FUGAPET_SAP_HANDLING_UNIT_BASE_URL`) aponta para o servico API_HANDLINGUNIT
- [ ] host do servico de HU presente em `hosts_permitidos`
- [ ] a autorizacao HU permite EXCLUSIVAMENTE `POST /HandlingUnit` (nunca outro endpoint, PATCH ou DELETE)
- [ ] cada caixa gera correlation_id/claim_token/tentativa proprios e no maximo 1 POST
- [ ] gate excepcional single-post `HML2-044-...` NAO e reutilizado pelo modo repetitivo

Observa??es:

- N?o commitar `configuracao.sap.json` com senha real.
- O publish inclui `configuracao.sap.exemplo.json`.
- Se existir `configuracao.sap.json` local e seguro na pasta do projeto no momento do publish, ele ? copiado para a pasta publicada por MSBuild, mas continua ignorado pelo Git.
- As credenciais da API de embalagem NUNCA vao no JSON: apenas nas variaveis de ambiente `FUGAPET_SAP_PACKAGING_USERNAME` / `FUGAPET_SAP_PACKAGING_PASSWORD`.
