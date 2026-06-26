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
    [string]$DestinoRaiz,
    [switch]$NaoGerarZip
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

$DiretoriosBloqueados = @(
    '.git',
    '.vs',
    'bin',
    'obj',
    '.claude',
    'pacotes_limpos'
)

function Testar-NomeConfiguracaoReal {
    param([Parameter(Mandatory)] [string]$NomeArquivo)

    return $NomeArquivo -match '^configuracao\..+\.json$' -and
        $NomeArquivo -notmatch '\.exemplo\.json$'
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

    if (Testar-NomeConfiguracaoReal -NomeArquivo $Arquivo.Name) {
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

    New-Item -ItemType Directory -Path $Destino -Force | Out-Null

    foreach ($Item in Get-ChildItem -LiteralPath $Origem -Force) {
        if ($Item.PSIsContainer) {
            if (-not (Testar-DiretorioBloqueado -Diretorio $Item)) {
                Copiar-ConteudoLimpo -Origem $Item.FullName -Destino (Join-Path $Destino $Item.Name)
            }

            continue
        }

        if (-not (Testar-ArquivoBloqueado -Arquivo $Item)) {
            Copy-Item -LiteralPath $Item.FullName -Destination (Join-Path $Destino $Item.Name) -Force
        }
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
                $NomeNormalizado -in @('password', 'senha', 'username', 'usuario') -and
                $Valor -is [string] -and
                -not [string]::IsNullOrWhiteSpace($Valor)) {
                throw "Pacote bloqueado: credencial preenchida em $Origem."
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

    if ($NomeArquivo -ieq 'configuracao.sap.json' -or
        (Testar-NomeConfiguracaoReal -NomeArquivo $NomeArquivo)) {
        throw "Pacote bloqueado: configuracao real encontrada em $Origem."
    }

    if ([System.IO.Path]::GetExtension($NomeArquivo) -in @('.log', '.user')) {
        throw "Pacote bloqueado: arquivo local encontrado em $Origem."
    }
}

function Validar-PastaPacote {
    param([Parameter(Mandatory)] [string]$Pasta)

    foreach ($Diretorio in Get-ChildItem -LiteralPath $Pasta -Directory -Recurse -Force) {
        if ($DiretoriosBloqueados -contains $Diretorio.Name) {
            throw "Pacote bloqueado: diretorio local encontrado em $($Diretorio.FullName)."
        }
    }

    foreach ($Arquivo in Get-ChildItem -LiteralPath $Pasta -File -Recurse -Force) {
        Validar-NomeArquivoPacote -NomeArquivo $Arquivo.Name -Origem $Arquivo.FullName

        if ($Arquivo.Name -match '^configuracao\..+\.exemplo\.json$') {
            Validar-ArquivoConfiguracao `
                -Conteudo (Get-Content -LiteralPath $Arquivo.FullName -Raw) `
                -Origem $Arquivo.FullName `
                -ValidarCredenciais ($Arquivo.Name -ieq 'configuracao.sap.exemplo.json')
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

            if ($Entrada.Name -match '^configuracao\..+\.exemplo\.json$') {
                $Leitor = [System.IO.StreamReader]::new($Entrada.Open())
                try {
                    Validar-ArquivoConfiguracao `
                        -Conteudo $Leitor.ReadToEnd() `
                        -Origem $Entrada.FullName `
                        -ValidarCredenciais ($Entrada.Name -ieq 'configuracao.sap.exemplo.json')
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
        foreach ($Arquivo in Get-ChildItem -LiteralPath $PastaOrigem -File -Recurse -Force) {
            $CaminhoRelativo = $Arquivo.FullName.Substring($BaseRelativa.Length + 1)
            $NomeEntrada = $CaminhoRelativo -replace '\\', '/'
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $Zip,
                $Arquivo.FullName,
                $NomeEntrada,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $Zip.Dispose()
    }
}

New-Item -ItemType Directory -Path $DestinoRaiz -Force | Out-Null

if (Test-Path -LiteralPath $DestinoPacote) {
    throw "A pasta de destino ja existe: $DestinoPacote"
}

try {
    Copiar-ConteudoLimpo -Origem $ProjetoRaiz -Destino $DestinoPacote
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

    if ($ZipDestinoCompleto.StartsWith(
        $DestinoRaizCompleto + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $ZipDestinoCompleto)) {
        Remove-Item -LiteralPath $ZipDestinoCompleto -Force
    }

    if ($DestinoPacoteCompleto.StartsWith(
        $DestinoRaizCompleto + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $DestinoPacoteCompleto)) {
        Remove-Item -LiteralPath $DestinoPacoteCompleto -Recurse -Force
    }

    throw
}

Write-Host 'Pacote limpo gerado e validado com sucesso.' -ForegroundColor Green
Write-Host "Pasta: $DestinoPacote"

if (-not $NaoGerarZip) {
    Write-Host "ZIP:   $ZipDestino"
}
