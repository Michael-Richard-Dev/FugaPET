# FUGAPET / P5 / 056 — envio do pacote para a VM do PostgreSQL.
#
# Copia os quatro scripts do incremento para a VM e confere o SHA-256 depois da copia,
# para que o que sera executado la seja comprovadamente o mesmo que esta aqui.
#
# Nao transporta credencial: a autenticacao e a do seu proprio SSH.
#
# Uso:
#   .\enviar_para_vm.ps1 -VmUsuario michael -VmHost 192.168.3.226
#   .\enviar_para_vm.ps1 -VmUsuario michael -VmHost 192.168.3.226 -Destino /tmp/fugapet056

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$VmUsuario,
    [Parameter(Mandatory = $true)][string]$VmHost,
    [string]$Destino = '/tmp/fugapet_056',
    [int]$Porta = 22
)

$ErrorActionPreference = 'Stop'

$origem = $PSScriptRoot
$arquivos = @(
    '056_PREFLIGHT.sql',
    '056_PROPOSTA.sql',
    '056_VALIDACAO.sql',
    '056_ROLLBACK.sql',
    'aplicar_na_vm.sh'
)

Write-Host "Origem : $origem"
Write-Host "Destino: ${VmUsuario}@${VmHost}:${Destino}"
Write-Host ''

# 1. Conferir que tudo existe antes de abrir conexao.
$faltando = @()
foreach ($a in $arquivos) {
    if (-not (Test-Path (Join-Path $origem $a))) { $faltando += $a }
}
if ($faltando.Count -gt 0) {
    throw "Arquivos ausentes na origem: $($faltando -join ', ')"
}

# 2. Impressao digital local, para comparar depois da copia.
Write-Host 'SHA-256 na origem:'
$local = @{}
foreach ($a in $arquivos) {
    $h = (Get-FileHash (Join-Path $origem $a) -Algorithm SHA256).Hash
    $local[$a] = $h
    '  {0,-22} {1}' -f $a, $h.Substring(0, 32) | Write-Host
}
Write-Host ''

# 3. Criar o destino e copiar.
Write-Host 'Criando o diretorio de destino...'
& ssh -p $Porta "${VmUsuario}@${VmHost}" "mkdir -p '$Destino'"
if ($LASTEXITCODE -ne 0) { throw "Falha ao criar $Destino na VM (ssh retornou $LASTEXITCODE)." }

Write-Host 'Copiando os arquivos...'
foreach ($a in $arquivos) {
    & scp -P $Porta (Join-Path $origem $a) "${VmUsuario}@${VmHost}:${Destino}/$a"
    if ($LASTEXITCODE -ne 0) { throw "Falha ao copiar $a (scp retornou $LASTEXITCODE)." }
}

# 4. Conferir a integridade do outro lado. Copia silenciosamente truncada e um risco real.
Write-Host ''
Write-Host 'SHA-256 na VM:'
$remoto = & ssh -p $Porta "${VmUsuario}@${VmHost}" "cd '$Destino' && sha256sum $($arquivos -join ' ')"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao calcular o SHA-256 na VM.' }

$divergentes = @()
foreach ($linha in $remoto) {
    if ($linha -match '^([0-9a-f]{64})\s+(.+)$') {
        $hash = $Matches[1].ToUpperInvariant()
        $nome = $Matches[2].Trim()
        $igual = $local.ContainsKey($nome) -and $local[$nome] -eq $hash
        '  {0,-22} {1} {2}' -f $nome, $hash.Substring(0, 32), $(if ($igual) { 'OK' } else { 'DIVERGENTE' }) | Write-Host
        if (-not $igual) { $divergentes += $nome }
    }
}

Write-Host ''
if ($divergentes.Count -gt 0) {
    throw "Copia divergente em: $($divergentes -join ', '). NAO execute nada na VM; refaca o envio."
}

& ssh -p $Porta "${VmUsuario}@${VmHost}" "chmod +x '$Destino/aplicar_na_vm.sh'" | Out-Null

Write-Host 'Transferencia conferida. Na VM, execute:'
Write-Host ''
Write-Host "  cd $Destino"
Write-Host '  ./aplicar_na_vm.sh                # so o preflight, nao altera nada'
Write-Host '  ./aplicar_na_vm.sh --aplicar      # cria a tabela e valida'
Write-Host ''
