$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    function Run-Dotnet([string[]] $Arguments) {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) { throw "Falló: dotnet $($Arguments -join ' ')" }
    }
    Run-Dotnet @('build', 'Jarvis.Core/Jarvis.Core.csproj', '-c', 'Release', '--nologo')
    Run-Dotnet @('run', '--project', 'Jarvis.MaintenanceTests/Jarvis.MaintenanceTests.csproj', '-c', 'Release', '--', '--live', '--audio', 'Jarvis.MaintenanceTests/fixtures')
    Run-Dotnet @('run', '--project', 'Jarvis.VoiceInputTests/Jarvis.VoiceInputTests.csproj', '-c', 'Release', '--', '--fixtures', 'Jarvis.VoiceInputTests/fixtures')
    Run-Dotnet @('run', '--project', 'Jarvis.CancellationTests/Jarvis.CancellationTests.csproj', '-c', 'Release', '--', '--live')
    Run-Dotnet @('run', '--project', 'Jarvis.Tests/Jarvis.Tests.csproj', '-c', 'Maintenance', '-p:UseAppHost=false')
    Run-Dotnet @('run', '--project', 'Jarvis.ReplyTests/Jarvis.ReplyTests.csproj', '-c', 'Maintenance', '-p:UseAppHost=false')
    Run-Dotnet @('Jarvis.Core/bin/Release/net10.0-windows/Jarvis.Core.dll', '--diagnostics')
    Write-Host 'Todas las comprobaciones pasaron. Revisa también los límites de reconocimiento y la validación con micrófono descritos en README.md.'
}
finally { Pop-Location }
