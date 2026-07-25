param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$NotesFile
)

$ErrorActionPreference = "Stop"
$expectedRoot = "D:\Codex\2026-07-23\new-chat"
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))

if ($repositoryRoot -ne $expectedRoot) {
    throw "当前工作目录不是指定的 D 盘项目：$repositoryRoot"
}

$tag = "v$Version"
$projectRoot = Join-Path $repositoryRoot "outputs\Windows工具箱"
$solutionPath = Join-Path $projectRoot "WindowsToolbox.sln"
$applicationProject = Join-Path $projectRoot "src\WindowsToolbox.App\WindowsToolbox.App.csproj"
$releaseRoot = Join-Path $repositoryRoot "artifacts\release\$tag"
$publishDirectory = Join-Path $releaseRoot "WindowsToolbox-win-x64"
$zipPath = Join-Path $releaseRoot "WindowsToolbox-$tag-win-x64.zip"
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$resolvedNotesFile = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $NotesFile))
$portableDotnet = Join-Path $repositoryRoot "work\dotnet-sdk\dotnet.exe"
$dotnetCommand = if (Test-Path -LiteralPath $portableDotnet -PathType Leaf) {
    $portableDotnet
}
else {
    "dotnet"
}

function Assert-LastExitCode([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation 失败，退出代码：$LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $resolvedNotesFile -PathType Leaf)) {
    throw "Release Notes 文件不存在：$resolvedNotesFile"
}

Push-Location $repositoryRoot
try {
    $status = @(git status --porcelain)
    Assert-LastExitCode "读取 Git 状态"
    if ($status.Count -ne 0) {
        throw "Git 工作区不干净。发布脚本不会自动暂存或提交文件。"
    }

    $branch = git branch --show-current
    Assert-LastExitCode "读取当前分支"
    if ($branch -ne "main") {
        throw "当前分支不是 main：$branch"
    }

    git fetch origin main --tags
    Assert-LastExitCode "同步 origin"

    $localCommit = git rev-parse main
    Assert-LastExitCode "读取本地 main"
    $remoteCommit = git rev-parse origin/main
    Assert-LastExitCode "读取 origin/main"
    if ($localCommit -ne $remoteCommit) {
        throw "本地 main 与 origin/main 不同步，停止发布。"
    }

    if (@(git tag --list $tag).Count -gt 0) {
        throw "本地标签已经存在：$tag"
    }
    if (@(git ls-remote --tags origin "refs/tags/$tag" "refs/tags/$tag^{}").Count -gt 0) {
        throw "远程标签已经存在：$tag"
    }

    $releaseList = gh release list --repo "QiFenjun/windows-toolbox" --limit 100 --json tagName
    Assert-LastExitCode "读取 GitHub Releases"
    if (($releaseList | ConvertFrom-Json).tagName -contains $tag) {
        throw "GitHub Release 已经存在：$tag"
    }

    if (Test-Path -LiteralPath $publishDirectory) {
        throw "发布目录已经存在，拒绝覆盖：$publishDirectory"
    }
    if (Test-Path -LiteralPath $zipPath) {
        throw "发布 ZIP 已经存在，拒绝覆盖：$zipPath"
    }
    if (Test-Path -LiteralPath $checksumPath) {
        throw "校验文件已经存在，拒绝覆盖：$checksumPath"
    }

    $env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet"
    $env:NUGET_PACKAGES = Join-Path $repositoryRoot ".packages"
    New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
    New-Item -ItemType Directory -Path $env:NUGET_PACKAGES -Force | Out-Null

    & $dotnetCommand restore $solutionPath --verbosity minimal
    Assert-LastExitCode "dotnet restore"
    & $dotnetCommand build $solutionPath --configuration Release --no-restore --verbosity minimal
    Assert-LastExitCode "dotnet build"
    & $dotnetCommand test $solutionPath --configuration Release --no-build --logger "console;verbosity=minimal"
    Assert-LastExitCode "dotnet test"

    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
    & $dotnetCommand publish $applicationProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $publishDirectory `
        --verbosity minimal
    Assert-LastExitCode "dotnet publish"

    @"
Windows工具箱 $tag

运行方式：
双击 Windows工具箱.exe 运行。

系统要求：
Windows 10/11 64 位。

说明：
本软件无需安装，也无需另外安装 .NET。
首次运行时 Windows SmartScreen 可能显示“未知发布者”提示，
这是因为软件暂未购买代码签名证书。

项目主页：
https://github.com/QiFenjun/windows-toolbox

隐私：
软件离线运行，不收集或上传用户数据及已安装软件列表。
"@ | Set-Content -LiteralPath (Join-Path $publishDirectory "README.txt") -Encoding utf8

    $forbiddenPublishFiles = @(
        Get-ChildItem -LiteralPath $publishDirectory -Recurse -Force |
            Where-Object {
                $_.FullName -match '\\(\.git|\.vs|bin|obj)(\\|$)' -or
                $_.Extension -in ".pdb", ".user", ".suo", ".env"
            }
    )
    if ($forbiddenPublishFiles.Count -ne 0) {
        throw "发布目录包含禁止文件。"
    }

    Compress-Archive `
        -Path (Join-Path $publishDirectory "*") `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal

    $hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "$($hash.Hash)  $([IO.Path]::GetFileName($zipPath))" |
        Set-Content -LiteralPath $checksumPath -Encoding utf8

    if (@(git status --porcelain).Count -ne 0) {
        throw "生成 artifacts 后 Git 工作区出现跟踪修改，停止发布。"
    }

    git tag -a $tag -m "Windows工具箱 $tag"
    Assert-LastExitCode "创建 Git 标签"
    git push origin $tag
    Assert-LastExitCode "推送 Git 标签"

    gh release create $tag `
        $zipPath `
        $checksumPath `
        --repo "QiFenjun/windows-toolbox" `
        --verify-tag `
        --title "Windows工具箱 $tag" `
        --notes-file $resolvedNotesFile
    Assert-LastExitCode "创建 GitHub Release"

    $releaseJson = gh release view $tag `
        --repo "QiFenjun/windows-toolbox" `
        --json name,tagName,isDraft,isPrerelease,url,assets
    Assert-LastExitCode "验证 GitHub Release"
    $release = $releaseJson | ConvertFrom-Json
    $assetNames = @($release.assets.name)
    if ($release.isDraft -or $release.isPrerelease -or
        $release.tagName -ne $tag -or
        $assetNames -notcontains [IO.Path]::GetFileName($zipPath) -or
        $assetNames -notcontains [IO.Path]::GetFileName($checksumPath)) {
        throw "GitHub Release 验证失败。"
    }

    Write-Host "Release URL: $($release.url)"
    Write-Host "ZIP: $zipPath"
    Write-Host "SHA256: $($hash.Hash)"
}
finally {
    Pop-Location
}
