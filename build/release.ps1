# Выпуск новой версии игры (этап 6): тесты → сборка со встроенным .NET → установщик Velopack → GitHub Releases.
# Установленные у друзей игры увидят новую версию при следующем запуске и обновятся сами.
#
#   .\build\release.ps1           — выпустить версию из Directory.Build.props
#   .\build\release.ps1 -DryRun   — только собрать установщик в artifacts\releases, ничего не загружать
#
# Перед выпуском: поднять <Version> в Directory.Build.props, закоммитить и запушить.
# Нужны: GitHub CLI (gh, выполнен вход) и утилита vpk той же версии, что пакет Velopack:
#   dotnet tool install -g vpk --version 1.2.158
# Monopoly.Admin в выпуск не входит — он только для ПК автора.

param([switch]$DryRun)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$repoUrl = "https://github.com/morphymoons-ctrl/MonopolyGame"
$publishDir = Join-Path $root "artifacts\publish"
$releasesDir = Join-Path $root "artifacts\releases"
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

function Run([string]$what, [scriptblock]$command) {
    Write-Host "==> $what" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "Не вдалося: $what (код $LASTEXITCODE)" }
}

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props") -Encoding UTF8
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw "Не знайдено <Version> у Directory.Build.props." }
Write-Host "Версія $version" -ForegroundColor Green

if (-not $DryRun) {
    # Выпуск — только из закоммиченного и запушенного кода: тег ставится на этот коммит.
    if (git -C $root status --porcelain) { throw "Є незакомічені зміни. Спершу коміт." }
    git -C $root fetch -q origin
    $head = git -C $root rev-parse HEAD
    $remote = git -C $root rev-parse origin/main
    if ($head -ne $remote) { throw "Коміт не запушено на GitHub (HEAD $head, origin/main $remote). Спершу git push." }
    # «release not found» приходит в поток ошибок — в PowerShell 5.1 со Stop это исключение, поэтому проверяем мягко.
    $ErrorActionPreference = "Continue"
    gh release view "v$version" --repo $repoUrl 2>&1 | Out-Null
    $exists = $LASTEXITCODE -eq 0
    $ErrorActionPreference = "Stop"
    if ($exists) { throw "Випуск v$version уже є. Підніміть <Version> у Directory.Build.props." }
    $token = gh auth token
    if (-not $token) { throw "Немає входу в GitHub CLI: gh auth login." }
}

Run "Тести" { dotnet test (Join-Path $root "MonopolyGame.sln") -c Release -v q }

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
Run "Збірка гри з вбудованим .NET" {
    dotnet publish (Join-Path $root "src\Monopoly.App\Monopoly.App.csproj") -c Release -r win-x64 --self-contained -o $publishDir -v q
}

# Каждый раз с чистой папки: vpk не пакует версию, которая там уже лежит.
if (Test-Path $releasesDir) { Remove-Item $releasesDir -Recurse -Force }
New-Item -ItemType Directory -Force $releasesDir | Out-Null
if (-not $DryRun) {
    # Прошлый выпуск нужен, чтобы собрать дельту: друзья скачают только изменения, а не всю игру.
    Write-Host "==> Попередній випуск для дельти" -ForegroundColor Cyan
    $ErrorActionPreference = "Continue"
    vpk download github --repoUrl $repoUrl --token $token -o $releasesDir 2>&1 | Out-Host
    $downloaded = $LASTEXITCODE -eq 0
    $ErrorActionPreference = "Stop"
    if (-not $downloaded) { Write-Host "Попереднього випуску немає — буде лише повний пакет." }
}

Run "Пакування (Velopack)" {
    # Иконка игры — у установщика Velopack и в «Програмах і компонентах» (если она уже есть).
    $icon = Join-Path $root "src\Monopoly.App\Assets\Icon\monopoly.ico"
    $iconArgs = if (Test-Path $icon) { @("--icon", $icon) } else { @() }
    vpk pack --packId MonopolyGame --packVersion $version --packDir $publishDir --mainExe Monopoly.exe --runtime win-x64 `
        --packTitle "Монополія" --packAuthors "Denchik" -o $releasesDir @iconArgs
}

# Установщик для друзей: окно выбора папки (Monopoly.Setup), внутри — установщик Velopack.
$setupDir = Join-Path $root "artifacts\setup"
if (Test-Path $setupDir) { Remove-Item $setupDir -Recurse -Force }
Run "Установник із вибором папки" {
    dotnet build (Join-Path $root "src\Monopoly.Setup\Monopoly.Setup.csproj") -c Release -v q -o $setupDir `
        "-p:SetupPayload=$(Join-Path $releasesDir 'MonopolyGame-win-Setup.exe')"
}
$installer = Join-Path $setupDir "Monopoly-Install.exe"

if ($DryRun) {
    Write-Host "Готово (без завантаження). Установник для друзів: $installer" -ForegroundColor Green
    Get-ChildItem $releasesDir, $installer | Format-Table Name, @{ n = "МБ"; e = { [math]::Round($_.Length / 1MB, 1) } }
    return
}

Run "Завантаження на GitHub Releases" {
    vpk upload github --repoUrl $repoUrl --token $token -o $releasesDir --publish `
        --releaseName "Монополія $version" --tag "v$version" --targetCommitish $head
}
# Друзьям — один установщик с выбором папки; установщик Velopack без выбора убираем, чтобы не путать.
# Для обновлений он не нужен: игра берёт их из releases.win.json и пакетов .nupkg.
Run "Установник у випуск" { gh release upload "v$version" $installer --repo $repoUrl --clobber }
Run "Прибрати зайвий установник" { gh release delete-asset "v$version" "MonopolyGame-win-Setup.exe" --repo $repoUrl --yes }
Write-Host "Випущено v$version. Установник для друзів: $repoUrl/releases/latest" -ForegroundColor Green
