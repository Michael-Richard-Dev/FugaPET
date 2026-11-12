<#
.SYNOPSIS
    Gera e valida um pacote limpo do projeto FugaPET_Dev.

.DESCRIPTION
    Copia o projeto excluindo historico Git, arquivos locais de IDE, saidas de
    build, logs e configuracoes reais. Depois da copia e da compactacao, valida
    o conteudo para bloquear regressao de credenciais ou configuracoes inseguras.
#>

[CmdletBinding()]
param(
    # Diretorio-raiz de saida. Se vazio, usa <projeto>\pacotes_limpos (dentro do projeto, como a DEV).
    [string]$DestinoRaiz,
    [switch]$NaoGerarZip,
    [string]$ValidarCaminhoPacote
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $PSCommandPath
$ProjetoRaiz = Split-Path -Parent $ScriptDir

if ([string]::IsNullOrWhiteSpace($DestinoRaiz)) {
    $DestinoRaiz = Join-Path $ProjetoRaiz 'pacotes_limpos'
}

$DataPacote = Get-Date -Format 'yyyyMMdd_HHmmss'
$NomePacote = "FugaPET_Dev_limpo_$DataPacote"
$DestinoPacote = Join-Path $DestinoRaiz $NomePacote
$ZipDestino = "$DestinoPacote.zip"

$script:TotalCopiados = 0
$script:TotalIgnorados = 0
$script:MaiorCaminhoDestino = 0

# Prefixa um caminho com o formato estendido (\\?\) para operar ACIMA do MAX_PATH (260) do Windows.
# Necessario porque o pacote fica DENTRO do projeto (pacotes_limpos) e alguns caminhos passam de 260.
# Assim mantemos o arquivamento no projeto, sem "subst", sem mover o projeto e sem renomear artefatos.
function ConvertTo-CaminhoLongo {
    param([Parameter(Mandatory)] [string]$Caminho)

    $full = [System.IO.Path]::GetFullPath($Caminho)
    if ($full.StartsWith('\\?\')) { return $full }
    if ($full.StartsWith('\\'))   { return '\\?\UNC\' + $full.Substring(2) }
    return '\\?\' + $full
}

# Remove o prefixo \\?\ para exibicao/calculo de caminho relativo (nomes de entrada do ZIP etc.).
function Remove-PrefixoCaminhoLongo {
    param([Parameter(Mandatory)] [string]$Caminho)

    if ($Caminho.StartsWith('\\?\UNC\')) { return '\\' + $Caminho.Substring(8) }
    if ($Caminho.StartsWith('\\?\'))     { return $Caminho.Substring(4) }
    return $Caminho
}

$DiretoriosBloqueados = @(
    '.git',
    '.vs',
    'bin',
    'obj',
    '.claude',
    'pacotes_limpos',
    'TestResults'
)

function Testar-NomeConfiguracaoReal {
    param([Parameter(Mandatory)] [string]$NomeArquivo)

    return $NomeArquivo -match '^configuracao\..+\.json$' -and
        $NomeArquivo -notmatch '\.exemplo\.json$'
}

function Testar-NomeArquivoSensivel {
    param([Parameter(Mandatory)] [string]$NomeArquivo)

    $NomeNormalizado = $NomeArquivo.ToLowerInvariant()
    if (Testar-NomeConfiguracaoReal -NomeArquivo $NomeArquivo) {
        return $true
    }

    if ($NomeNormalizado -match '^configuracao\..+\.json\.(bak_.*|backup.*|old.*)$') {
        return $true
    }

    if ($NomeNormalizado -eq '.env' -or
        $NomeNormalizado -like '.env*' -or
        $NomeNormalizado -like '*.env') {
        return $true
    }

    return [System.IO.Path]::GetExtension($NomeNormalizado) -in @('.pfx', '.p12', '.key', '.pem')
}

function Testar-DiretorioBloqueado {
    param([Parameter(Mandatory)] [System.IO.DirectoryInfo]$Diretorio)

    if ($DiretoriosBloqueados -contains $Diretorio.Name) {
        return $true
    }

    $DestinoRaizCompleto = [System.IO.Path]::GetFullPath($DestinoRaiz)
    $DiretorioCompleto = [System.IO.Path]::GetFullPath($Diretorio.FullName)
    return $DiretorioCompleto.StartsWith(
        $DestinoRaizCompleto,
        [System.StringComparison]::OrdinalIgnoreCase)
}

# Mede, SEM copiar, o maior caminho relativo (em caracteres) entre os diretorios NAO bloqueados.
# Serve ao preflight de comprimento: projeta se o destino excederia o MAX_PATH do Windows.
function Medir-MaiorCaminhoRelativo {
    param(
        [Parameter(Mandatory)] [string]$Origem,
        [string]$Prefixo = ''
    )

    $maior = 0
    foreach ($Item in Get-ChildItem -LiteralPath $Origem -Force) {
        $relativo = if ([string]::IsNullOrEmpty($Prefixo)) { $Item.Name } else { "$Prefixo\$($Item.Name)" }

        if ($Item.PSIsContainer) {
            if (Testar-DiretorioBloqueado -Diretorio $Item) {
                continue
            }

            $sub = Medir-MaiorCaminhoRelativo -Origem $Item.FullName -Prefixo $relativo
            if ($sub -gt $maior) {
                $maior = $sub
            }

            continue
        }

        if ($relativo.Length -gt $maior) {
            $maior = $relativo.Length
        }
    }

    return $maior
}

# Enumera (achatado) os arquivos que SERAO copiados, com o mesmo filtro do copiador.
# Usado apenas para a verificacao pos-copia (contagem + SHA-256), sem alterar o fluxo de copia.
function Obter-ArquivosCopiaveis {
    param(
        [Parameter(Mandatory)] [string]$Origem,
        [string]$Prefixo = ''
    )

    $lista = New-Object System.Collections.Generic.List[object]
    foreach ($Item in Get-ChildItem -LiteralPath $Origem -Force) {
        $relativo = if ([string]::IsNullOrEmpty($Prefixo)) { $Item.Name } else { "$Prefixo\$($Item.Name)" }

        if ($Item.PSIsContainer) {
            if (-not (Testar-DiretorioBloqueado -Diretorio $Item)) {
                foreach ($sub in Obter-ArquivosCopiaveis -Origem $Item.FullName -Prefixo $relativo) {
                    $lista.Add($sub)
                }
            }

            continue
        }

        if (-not (Testar-ArquivoBloqueado -Arquivo $Item)) {
            $lista.Add([pscustomobject]@{ Relativo = $relativo; Origem = $Item.FullName })
        }
    }

    return $lista
}

function Testar-ScriptHabilitaEscritaSap {
    param([string]$Conteudo)

    if ([string]::IsNullOrWhiteSpace($Conteudo)) {
        return $false
    }

    # Cobre: FUGAPET_SAP_WRITE_ENABLED=true / set ... / $env: ... / [Environment]::SetEnvironmentVariable(...),
    # com ou sem aspas (" ou '), espacos e variacoes de caixa (-match e case-insensitive).
    $padroes = @(
        'FUGAPET_SAP_WRITE_ENABLED\s*=\s*["'']?\s*true',
        'set\s+FUGAPET_SAP_WRITE_ENABLED\s*=\s*["'']?\s*true',
        '\$env:FUGAPET_SAP_WRITE_ENABLED\s*=\s*["'']?\s*true',
        'SetEnvironmentVariable\s*\(\s*["'']FUGAPET_SAP_WRITE_ENABLED["'']\s*,\s*["'']?\s*true'
    )

    foreach ($padrao in $padroes) {
        if ($Conteudo -match $padrao) {
            return $true
        }
    }

    return $false
}

function Testar-ArquivoBloqueado {
    param([Parameter(Mandatory)] [System.IO.FileInfo]$Arquivo)

    if (Testar-NomeArquivoSensivel -NomeArquivo $Arquivo.Name) {
        return $true
    }

    if ($Arquivo.Extension -in @('.log', '.user')) {
        return $true
    }

    # Seguranca: nunca empacotar atalho/script (.cmd/.bat/.ps1) que habilite a escrita SAP.
    if ($Arquivo.Extension -in @('.cmd', '.bat', '.ps1')) {
        try {
            $ConteudoScript = Get-Content -LiteralPath $Arquivo.FullName -Raw -ErrorAction Stop
            if (Testar-ScriptHabilitaEscritaSap -Conteudo $ConteudoScript) {
                return $true
            }
        }
        catch { }
    }

    $ProjetoRaizCompleto = [System.IO.Path]::GetFullPath($ProjetoRaiz).TrimEnd('\', '/')
    $ArquivoCompleto = [System.IO.Path]::GetFullPath($Arquivo.FullName)
    $CaminhoRelativo = $ArquivoCompleto

    if ($ArquivoCompleto.StartsWith(
        $ProjetoRaizCompleto + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        $CaminhoRelativo = $ArquivoCompleto.Substring($ProjetoRaizCompleto.Length + 1)
    }

    return $CaminhoRelativo -like 'Propriedades\PublishProfiles\*.pubxml' -or
        $CaminhoRelativo -like 'Propriedades\PublishProfiles\*.pubxml.user'
}

function Copiar-ConteudoLimpo {
    param(
        [Parameter(Mandatory)] [string]$Origem,
        [Parameter(Mandatory)] [string]$Destino
    )

    [System.IO.Directory]::CreateDirectory((ConvertTo-CaminhoLongo $Destino)) | Out-Null

    foreach ($Item in Get-ChildItem -LiteralPath $Origem -Force) {
        if ($Item.PSIsContainer) {
            if (-not (Testar-DiretorioBloqueado -Diretorio $Item)) {
                Copiar-ConteudoLimpo -Origem $Item.FullName -Destino (Join-Path $Destino $Item.Name)
            }

            continue
        }

        if (Testar-ArquivoBloqueado -Arquivo $Item) {
            $script:TotalIgnorados++
            continue
        }

        $DestinoArquivo = Join-Path $Destino $Item.Name

        # Garante o diretorio-pai antes de cada copia (formato \\?\ suporta caminhos > 260).
        $DiretorioPai = Split-Path -Parent $DestinoArquivo
        if (-not [string]::IsNullOrEmpty($DiretorioPai)) {
            [System.IO.Directory]::CreateDirectory((ConvertTo-CaminhoLongo $DiretorioPai)) | Out-Null
        }

        [System.IO.File]::Copy(
            (ConvertTo-CaminhoLongo $Item.FullName),
            (ConvertTo-CaminhoLongo $DestinoArquivo),
            $true)
        $script:TotalCopiados++

        if ($DestinoArquivo.Length -gt $script:MaiorCaminhoDestino) {
            $script:MaiorCaminhoDestino = $DestinoArquivo.Length
        }
    }
}

# Verificacao pos-copia: confirma que cada arquivo de origem existe no pacote com SHA-256 identico
# e que o pacote nao tem arquivos a mais/a menos. Nao altera nada; apenas valida integridade.
function Verificar-IntegridadeCopia {
    param(
        [Parameter(Mandatory)] [string]$Origem,
        [Parameter(Mandatory)] [string]$Pacote
    )

    $arquivos = Obter-ArquivosCopiaveis -Origem $Origem
    $divergencias = New-Object System.Collections.Generic.List[string]

    foreach ($arquivo in $arquivos) {
        $destinoLongo = ConvertTo-CaminhoLongo (Join-Path $Pacote $arquivo.Relativo)
        if (-not [System.IO.File]::Exists($destinoLongo)) {
            $divergencias.Add("AUSENTE no pacote: $($arquivo.Relativo)")
            continue
        }

        $hashOrigem = (Get-FileHash -LiteralPath (ConvertTo-CaminhoLongo $arquivo.Origem) -Algorithm SHA256).Hash
        $hashDestino = (Get-FileHash -LiteralPath $destinoLongo -Algorithm SHA256).Hash
        if ($hashOrigem -ne $hashDestino) {
            $divergencias.Add("SHA-256 divergente: $($arquivo.Relativo)")
        }
    }

    $totalPacote = @([System.IO.Directory]::EnumerateFiles(
        (ConvertTo-CaminhoLongo $Pacote), '*', [System.IO.SearchOption]::AllDirectories)).Count

    return [pscustomobject]@{
        TotalOrigem  = $arquivos.Count
        TotalPacote  = $totalPacote
        Divergencias = $divergencias
    }
}

function Validar-ObjetoConfiguracao {
    param(
        [Parameter(Mandatory)] [AllowNull()] $Objeto,
        [Parameter(Mandatory)] [string]$Origem,
        [Parameter(Mandatory)] [bool]$ValidarCredenciais
    )

    if ($null -eq $Objeto) {
        return
    }

    if ($Objeto -is [System.Management.Automation.PSCustomObject]) {
        foreach ($Propriedade in $Objeto.PSObject.Properties) {
            $NomeNormalizado = ($Propriedade.Name -replace '[_\-\s]', '').ToLowerInvariant()
            $Valor = $Propriedade.Value

            if ($ValidarCredenciais -and
                $NomeNormalizado -in @('password', 'senha', 'username', 'usuario', 'clientsecret', 'authorization') -and
                $Valor -is [string] -and
                -not [string]::IsNullOrWhiteSpace($Valor) -and
                $Valor -notmatch '^DEFINIR_[A-Z0-9_]+$') {
                throw "Pacote bloqueado: credencial real preenchida em $Origem. Use placeholder DEFINIR_*."
            }

            if ($NomeNormalizado -eq 'ignorarvalidacaocertificado' -and $Valor -eq $true) {
                throw "Pacote bloqueado: ignorar_validacao_certificado=true em $Origem."
            }

            Validar-ObjetoConfiguracao `
                -Objeto $Valor `
                -Origem $Origem `
                -ValidarCredenciais $ValidarCredenciais
        }

        return
    }

    if ($Objeto -is [System.Collections.IEnumerable] -and $Objeto -isnot [string]) {
        foreach ($Item in $Objeto) {
            Validar-ObjetoConfiguracao `
                -Objeto $Item `
                -Origem $Origem `
                -ValidarCredenciais $ValidarCredenciais
        }
    }
}

function Validar-ArquivoConfiguracao {
    param(
        [Parameter(Mandatory)] [string]$Conteudo,
        [Parameter(Mandatory)] [string]$Origem,
        [Parameter(Mandatory)] [bool]$ValidarCredenciais
    )

    try {
        $Configuracao = $Conteudo | ConvertFrom-Json
    }
    catch {
        throw "Pacote bloqueado: JSON de configuracao invalido em $Origem."
    }

    Validar-ObjetoConfiguracao `
        -Objeto $Configuracao `
        -Origem $Origem `
        -ValidarCredenciais $ValidarCredenciais
}

function Validar-NomeArquivoPacote {
    param(
        [Parameter(Mandatory)] [string]$NomeArquivo,
        [Parameter(Mandatory)] [string]$Origem
    )

    if (Testar-NomeArquivoSensivel -NomeArquivo $NomeArquivo) {
        throw "Pacote bloqueado: configuracao real encontrada em $Origem."
    }

    if ([System.IO.Path]::GetExtension($NomeArquivo) -in @('.log', '.user')) {
        throw "Pacote bloqueado: arquivo local encontrado em $Origem."
    }
}

function Validar-PastaPacote {
    param([Parameter(Mandatory)] [string]$Pasta)

    $PastaLonga = ConvertTo-CaminhoLongo $Pasta

    foreach ($Diretorio in [System.IO.Directory]::EnumerateDirectories(
            $PastaLonga, '*', [System.IO.SearchOption]::AllDirectories)) {
        $NomeDir = [System.IO.Path]::GetFileName($Diretorio.TrimEnd('\'))
        if ($DiretoriosBloqueados -contains $NomeDir) {
            throw "Pacote bloqueado: diretorio local encontrado em $(Remove-PrefixoCaminhoLongo $Diretorio)."
        }
    }

    foreach ($Arquivo in [System.IO.Directory]::EnumerateFiles(
            $PastaLonga, '*', [System.IO.SearchOption]::AllDirectories)) {
        $NomeArquivo = [System.IO.Path]::GetFileName($Arquivo)
        $OrigemExibicao = Remove-PrefixoCaminhoLongo $Arquivo
        Validar-NomeArquivoPacote -NomeArquivo $NomeArquivo -Origem $OrigemExibicao

        if ([System.IO.Path]::GetExtension($NomeArquivo) -ieq '.json') {
            Validar-ArquivoConfiguracao `
                -Conteudo ([System.IO.File]::ReadAllText($Arquivo)) `
                -Origem $OrigemExibicao `
                -ValidarCredenciais ($NomeArquivo -notmatch '\.exemplo\.json$')
        }
    }
}

function Validar-ZipPacote {
    param([Parameter(Mandatory)] [string]$CaminhoZip)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $Zip = [System.IO.Compression.ZipFile]::OpenRead($CaminhoZip)

    try {
        foreach ($Entrada in $Zip.Entries) {
            if ($Entrada.FullName.Contains('\')) {
                throw "Pacote bloqueado: entrada do ZIP usa barra invertida como separador: $($Entrada.FullName)."
            }

            $Segmentos = $Entrada.FullName -split '[/\\]'
            if ($Segmentos | Where-Object { $DiretoriosBloqueados -contains $_ }) {
                throw "Pacote bloqueado: diretorio local encontrado em $($Entrada.FullName)."
            }

            if ([string]::IsNullOrWhiteSpace($Entrada.Name)) {
                continue
            }

            Validar-NomeArquivoPacote -NomeArquivo $Entrada.Name -Origem $Entrada.FullName

            if ([System.IO.Path]::GetExtension($Entrada.Name) -ieq '.json') {
                $Leitor = [System.IO.StreamReader]::new($Entrada.Open())
                try {
                    Validar-ArquivoConfiguracao `
                        -Conteudo $Leitor.ReadToEnd() `
                        -Origem $Entrada.FullName `
                        -ValidarCredenciais ($Entrada.Name -notmatch '\.exemplo\.json$')
                }
                finally {
                    $Leitor.Dispose()
                }
            }
        }
    }
    finally {
        $Zip.Dispose()
    }
}

function Compactar-PastaComBarrasNormais {
    param(
        [Parameter(Mandatory)] [string]$PastaOrigem,
        [Parameter(Mandatory)] [string]$CaminhoZip
    )

    # Compress-Archive (Windows PowerShell) grava entradas com "\", o que gera aviso no unzip do
    # Linux. Aqui geramos o ZIP via System.IO.Compression escrevendo nomes com "/" (portavel).
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $PastaOrigemCompleta = [System.IO.Path]::GetFullPath($PastaOrigem)
    # Mantem a pasta-raiz do pacote dentro do ZIP (mesma estrutura do Compress-Archive).
    $BaseRelativa = [System.IO.Path]::GetDirectoryName($PastaOrigemCompleta)

    $Zip = [System.IO.Compression.ZipFile]::Open(
        $CaminhoZip,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($Arquivo in [System.IO.Directory]::EnumerateFiles(
                (ConvertTo-CaminhoLongo $PastaOrigem), '*', [System.IO.SearchOption]::AllDirectories)) {
            $ArquivoNormal = Remove-PrefixoCaminhoLongo $Arquivo
            $CaminhoRelativo = $ArquivoNormal.Substring($BaseRelativa.Length + 1)
            $NomeEntrada = $CaminhoRelativo -replace '\\', '/'
            # Le a origem via \\?\ (suporta > 260); a entrada do ZIP usa nome relativo com "/".
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $Zip,
                $Arquivo,
                $NomeEntrada,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $Zip.Dispose()
    }
}

# ------------------------------------------------------------------------------------------------
# O pacote fica DENTRO do projeto (pacotes_limpos). Como alguns caminhos passam de 260 caracteres,
# todas as operacoes do lado do destino usam o formato estendido \\?\ (ver ConvertTo-CaminhoLongo),
# eliminando a necessidade de "subst" e mantendo o arquivamento no projeto.
# ------------------------------------------------------------------------------------------------
$MaiorRelativo = Medir-MaiorCaminhoRelativo -Origem $ProjetoRaiz
$ComprimentoProjetado = $DestinoPacote.Length + 1 + $MaiorRelativo

Write-Host '--- Gerar Pacote Limpo (FugaPET_Dev) ---'
Write-Host "Diretorio-fonte       : $ProjetoRaiz"
Write-Host "Diretorio de saida    : $DestinoRaiz"
Write-Host "Maior caminho relativo: $MaiorRelativo caracteres (projetado no destino: $ComprimentoProjetado)"
if ($ComprimentoProjetado -gt 260) {
    Write-Host "Alguns caminhos passam de 260 caracteres; usando formato estendido \\?\ para copiar/validar/compactar." -ForegroundColor Yellow
}

if (-not [string]::IsNullOrWhiteSpace($ValidarCaminhoPacote)) {
    if (-not (Test-Path -LiteralPath $ValidarCaminhoPacote -PathType Container)) {
        throw "Pacote bloqueado: pasta para validacao nao encontrada."
    }

    Validar-PastaPacote -Pasta $ValidarCaminhoPacote
    Write-Host 'Pacote limpo validado com sucesso; nenhum ZIP foi gerado.' -ForegroundColor Green
    return
}
[System.IO.Directory]::CreateDirectory((ConvertTo-CaminhoLongo $DestinoRaiz)) | Out-Null

if ([System.IO.Directory]::Exists((ConvertTo-CaminhoLongo $DestinoPacote))) {
    throw "A pasta de destino ja existe: $DestinoPacote"
}

try {
    Copiar-ConteudoLimpo -Origem $ProjetoRaiz -Destino $DestinoPacote

    # Verificacao de integridade: contagem + SHA-256 origem x pacote (garante que nada foi alterado).
    $Verificacao = Verificar-IntegridadeCopia -Origem $ProjetoRaiz -Pacote $DestinoPacote
    if ($Verificacao.Divergencias.Count -gt 0 -or $Verificacao.TotalOrigem -ne $Verificacao.TotalPacote) {
        $detalhe = ($Verificacao.Divergencias | Select-Object -First 10) -join "`n  "
        throw @"
Verificacao de integridade FALHOU (pacote nao confere com a origem).
  Arquivos na origem: $($Verificacao.TotalOrigem)
  Arquivos no pacote: $($Verificacao.TotalPacote)
  Divergencias:
  $detalhe
"@
    }

    Validar-PastaPacote -Pasta $DestinoPacote

    if (-not $NaoGerarZip) {
        if (Test-Path -LiteralPath $ZipDestino) {
            throw "O arquivo ZIP ja existe: $ZipDestino"
        }

        Compactar-PastaComBarrasNormais -PastaOrigem $DestinoPacote -CaminhoZip $ZipDestino
        Validar-ZipPacote -CaminhoZip $ZipDestino
    }
}
catch {
    $DestinoRaizCompleto = [System.IO.Path]::GetFullPath($DestinoRaiz).TrimEnd('\', '/')
    $DestinoPacoteCompleto = [System.IO.Path]::GetFullPath($DestinoPacote)
    $ZipDestinoCompleto = [System.IO.Path]::GetFullPath($ZipDestino)

    # Remove SOMENTE os artefatos desta execucao (guarda StartsWith); nunca fontes nem pacotes anteriores.
    if ($ZipDestinoCompleto.StartsWith(
        $DestinoRaizCompleto + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.File]::Exists((ConvertTo-CaminhoLongo $ZipDestinoCompleto))) {
        [System.IO.File]::Delete((ConvertTo-CaminhoLongo $ZipDestinoCompleto))
    }

    if ($DestinoPacoteCompleto.StartsWith(
        $DestinoRaizCompleto + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Directory]::Exists((ConvertTo-CaminhoLongo $DestinoPacoteCompleto))) {
        [System.IO.Directory]::Delete((ConvertTo-CaminhoLongo $DestinoPacoteCompleto), $true)
    }

    throw
}

Write-Host 'Pacote limpo gerado e validado com sucesso.' -ForegroundColor Green
Write-Host "Arquivos copiados : $script:TotalCopiados"
Write-Host "Arquivos ignorados: $script:TotalIgnorados"
Write-Host "Maior caminho no destino: $script:MaiorCaminhoDestino caracteres"
Write-Host "Pasta: $DestinoPacote"

if (-not $NaoGerarZip) {
    Write-Host "ZIP:   $ZipDestino"
}
