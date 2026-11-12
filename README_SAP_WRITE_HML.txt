============================================================
HABILITAR ESCRITA SAP EM HOMOLOGACAO (HML) - INSTRUCAO CONTROLADA
Projeto: FugaPET_Dev
============================================================

AVISO DE SEGURANCA
  Este README SUBSTITUI os atalhos executaveis removidos por seguranca:
    - Abrir_FugaPET_Dev_SAP_WRITE_TRUE.cmd
    - Abrir_VisualStudio_HML_SAP_WRITE_TRUE.cmd
  Atalhos .cmd/.ps1 que setam FUGAPET_SAP_WRITE_ENABLED=true NAO sao mais
  versionados nem empacotados (bloqueados no Scripts/GerarPacoteLimpo.ps1),
  para evitar escrita SAP acidental.

QUANDO USAR
  Apenas em JANELA AUTORIZADA pelo Richard, com massa de teste autorizada, para
  validar a criacao de Documento de Material (movimento 101) no SAP HML.

COMO HABILITAR (por sessao, manual)
  A variavel vale SOMENTE para o processo/sessao onde for definida. NAO defina
  no nivel de Usuario/Maquina (evita deixar a escrita ligada permanentemente).

  PowerShell (sessao atual):
    $env:FUGAPET_SAP_WRITE_ENABLED = "true"
    # depois, na MESMA janela, abrir a aplicacao ou o Visual Studio:
    & ".\bin\Debug\net10.0-windows\FugaPET_Dev.exe"

  CMD (sessao atual):
    set FUGAPET_SAP_WRITE_ENABLED=true
    start "" ".\bin\Debug\net10.0-windows\FugaPET_Dev.exe"

  Tambem sao necessarias (somente no ambiente):
    FUGAPET_SAP_BASE_URL, FUGAPET_SAP_MATERIAL_DOCUMENT_BASE_URL,
    FUGAPET_SAP_USERNAME, FUGAPET_SAP_PASSWORD, FUGAPET_SAP_CLIENT,
    FUGAPET_SAP_ALLOWED_HOSTS.

COMO DESABILITAR (apos o teste)
  Feche a janela/processo (a variavel de sessao some) OU:
    PowerShell: Remove-Item Env:FUGAPET_SAP_WRITE_ENABLED
    CMD:        set FUGAPET_SAP_WRITE_ENABLED=
  O padrao do sistema permanece com a escrita DESLIGADA.

LEMBRETE
  Sem FUGAPET_SAP_WRITE_ENABLED=true, o envio fica bloqueado (somente leitura/
  finalizacao local). As demais travas (ambiente HML, permissao ENVIAR_SAP,
  integracao ativa, Material Document configurado) continuam valendo.
============================================================
