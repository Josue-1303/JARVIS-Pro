$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    function Run-Dotnet([string[]] $Arguments) {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) { throw "Falló: dotnet $($Arguments -join ' ')" }
    }
    Run-Dotnet @('build', 'Jarvis.Core/Jarvis.Core.csproj', '-c', 'Release', '--nologo')
    Run-Dotnet @('run', '--project', 'Jarvis.Tests/Jarvis.Tests.csproj', '-c', 'Maintenance', '-p:UseAppHost=false')
    Run-Dotnet @('run', '--project', 'Jarvis.ReplyTests/Jarvis.ReplyTests.csproj', '-c', 'Maintenance', '-p:UseAppHost=false')
    Run-Dotnet @('run', '--project', 'Jarvis.VoiceInputTests/Jarvis.VoiceInputTests.csproj', '-c', 'Release')
    Run-Dotnet @('run', '--project', 'Jarvis.CancellationTests/Jarvis.CancellationTests.csproj', '-c', 'Release')
    Run-Dotnet @('run', '--project', 'Jarvis.MaintenanceTests/Jarvis.MaintenanceTests.csproj', '-c', 'Release')
    Write-Host 'Comprobaciones reproducibles finalizadas. Las pruebas de audio real requieren recursos locales y fixtures sintéticos.'
}
finally { Pop-Location }
