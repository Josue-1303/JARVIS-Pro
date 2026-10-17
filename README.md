# J.A.R.V.I.S. Pro — guía actual

La aplicación de consola está en **V0.8 Preview 1**; la nueva interfaz de escritorio está en **V0.9 en desarrollo**. Esta guía documenta ambas por separado.

## Inicio

Desde PowerShell en la carpeta `JARVIS-Pro`:

```powershell
dotnet run --project .\Jarvis.Core --configuration Release
```

También puedes abrir `Start-Jarvis.cmd`. Para aplicar cambios recientes, cierra la instancia abierta con Esc desde el menú y vuelve a iniciarla. No hace falta borrar conversaciones ni recuerdos para actualizar.

## Interfaz de escritorio (V0.9 en desarrollo)

La aplicación WPF incluye chat, historial, Memories, Rules, Personality, selector Español/English/Automático, captura manual con micrófono, Piper y controles para detener operaciones. La validación visual y de ubicación todavía requiere una sesión interactiva. Para iniciar la interfaz desde la carpeta raíz:

~~~powershell
dotnet run --project .\Jarvis.Desktop\Jarvis.Desktop.csproj --configuration Release
~~~

La consola sigue disponible como alternativa. Cierra cualquier otra instancia de J.A.R.V.I.S. antes de abrir la interfaz; ambas comparten los datos locales.

### Acceso a Internet en la interfaz

El interruptor **Acceso a Internet** está en la barra lateral. Apagado, J.A.R.V.I.S. no consulta la web; si su decisión local indica que una pregunta necesita datos actuales, explica que el acceso está desactivado y no inventa una respuesta. Encendido, la interfaz permite las consultas necesarias y las funciones web habilitadas. La búsqueda del chat requiere una clave de Ollama Web Search, que se configura con el botón **Configure** junto al interruptor. Si el acceso está encendido pero falta la clave, J.A.R.V.I.S. lo informa y no finge haber buscado.

Qwen3 decide si cada pregunta necesita búsqueda: por ejemplo, noticias recientes o información cambiante pueden requerirla; cálculos, conocimientos estables, recuerdos y hora local no. Cuando busca, solo se envía la pregunta actual al servicio de búsqueda; los resultados se tratan como contenido externo no confiable y las fuentes aparecen debajo de la respuesta. La clave se cifra con Windows DPAPI en el equipo y no se guarda en el repositorio.

Para usarlo, active **Acceso a Internet**, configure la clave una vez y pregunte algo que dependa de información actual. Para probar el modo sin red, apague el interruptor y vuelva a formular la misma pregunta: J.A.R.V.I.S. debe explicar que no puede verificarla con el acceso apagado.
## Preparar el entorno de desarrollo (V0.8 Preview)

Estas instrucciones preparan el entorno de desarrollo de J.A.R.V.I.S. desde el código fuente en Windows x64. Todavía no son el instalador para usuarios: el prototipo de escritorio está en desarrollo y después se integrará con los requisitos y recursos en un instalador guiado. Para preparar el entorno desde cero, reserva espacio para Qwen3 8B (aprox. 5.2 GB), Whisper Small (488 MB) y las dos voces Piper (aprox. 126 MB), además del SDK y Python.

### 1. Instalar los requisitos del equipo

- Instala el [SDK de .NET 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0). También puedes usar `winget install --id Microsoft.DotNet.SDK.10 --exact --source winget`. El proyecto requiere el SDK, no solo el runtime; `global.json` solicita 10.0.402 y permite avanzar a otro feature band de .NET 10.
- Instala [Ollama para Windows](https://ollama.com/download/windows), abre una terminal nueva y descarga el modelo: `ollama pull qwen3:8b`. Confirma que aparece en `ollama list`.
- Instala Python 3.13.16 x64 desde [Python.org](https://www.python.org/downloads/release/python-31316/). Abre una terminal nueva y verifica `py -3.13 --version`.
- Obtén el código actualizado del repositorio privado y abre PowerShell en su carpeta raíz. No copies la carpeta `data/` de otra instalación.

### 2. Preparar Piper, sus voces y Whisper

Ejecuta este bloque desde la raíz del proyecto. Crea un entorno virtual propio y descarga los archivos a las rutas que usa la configuración:

```powershell
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force .\voices, .\models | Out-Null
py -3.13 -m venv .\.venv-piper
.\.venv-piper\Scripts\python.exe -m pip install --upgrade pip
.\.venv-piper\Scripts\python.exe -m pip install 'piper-tts==1.8.0'

$downloads = @(
    @{ Url = 'https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/es/es_MX/claude/high/es_MX-claude-high.onnx'; Path = '.\voices\es_MX-claude-high.onnx'; Sha256 = '3ef40a71ea63852cd8ab7e6fa7d2ecdcfa67a0b47c9c48e3f10e02ee02083ea0' },
    @{ Url = 'https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/es/es_MX/claude/high/es_MX-claude-high.onnx.json'; Path = '.\voices\es_MX-claude-high.onnx.json' },
    @{ Url = 'https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_GB/alan/medium/en_GB-alan-medium.onnx'; Path = '.\voices\en_GB-alan-medium.onnx'; Sha256 = '0a309668932205e762801f1efc2736cd4b0120329622adf62be09e56339d3330' },
    @{ Url = 'https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_GB/alan/medium/en_GB-alan-medium.onnx.json'; Path = '.\voices\en_GB-alan-medium.onnx.json' },
    @{ Url = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/90a64d80ea254cf67575b41a5971f972c79f7b45/ggml-small.bin'; Path = '.\models\ggml-small.bin'; Sha256 = '1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b' }
)
foreach ($item in $downloads) {
    Invoke-WebRequest -Uri $item.Url -OutFile $item.Path
    if ($item.Sha256) {
        $actual = (Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $item.Sha256) { throw "La verificación SHA-256 falló para $($item.Path)." }
    }
}
.\.venv-piper\Scripts\python.exe -c "import json, pathlib; [json.loads(p.read_text(encoding='utf-8')) for p in pathlib.Path('voices').glob('*.onnx.json')]"
```

Los enlaces de Piper apuntan a la versión `v1.0.0` de las voces; el enlace de Whisper fija una revisión concreta. El bloque verifica el SHA-256 de los tres modelos binarios y valida que ambos archivos de metadatos de voz sean JSON válido. Si una descarga se interrumpe, vuelve a ejecutar el bloque desde el principio.

### 3. Compilar y probar la instalación

Desde la misma raíz del proyecto:

```powershell
dotnet --version
dotnet build .\Jarvis.Core\Jarvis.Core.csproj -c Release
dotnet run --project .\Jarvis.Core --configuration Release -- --diagnostics
dotnet run --project .\Jarvis.Core --configuration Release -- --test-voices
dotnet run --project .\Jarvis.Core -c Release -- --data-dir .\validation\clean-install-data
```

El diagnóstico confirma dispositivos de entrada/salida, índice de micrófono configurado, archivos principales locales y conexión de Ollama con `qwen3:8b`. `--test-voices` comprueba síntesis y reproducción real en español e inglés. La última orden inicia la aplicación con memoria de prueba aislada: no lee ni modifica el historial de `data/`. Sal de esa sesión desde el menú cuando termines. Para la validación completa de regresión, usa `.\Validate-Jarvis.ps1`; requiere servicios y recursos de prueba y puede reproducir audio.

En el equipo actual ya se validaron con el micrófono la activación, transcripción, seguimiento, sensibilidad, silencio sin activaciones falsas e interrupción hablada. No hace falta repetir esas pruebas aquí. Solo valida el micrófono de nuevo si el equipo, dispositivo, permiso o configuración cambia.

`data/` contiene conversaciones y recuerdos locales. Consérvala al actualizar una instalación existente y exclúyela de cualquier copia que vayas a compartir. No subas `.venv-piper`, modelos, voces, credenciales ni datos personales al repositorio.

### Solución de problemas

- **No se encuentra el SDK:** instala el SDK de .NET 10 x64, abre una terminal nueva y ejecuta `dotnet --version` desde la carpeta del proyecto.
- **Ollama no responde o falta el modelo:** inicia Ollama; ejecuta `ollama list` y, si hace falta, `ollama pull qwen3:8b`. El diagnóstico espera Ollama en `http://localhost:11434`.
- **Falla una verificación SHA-256:** elimina únicamente el archivo incompleto o incorrecto y vuelve a ejecutar el bloque de descargas.
- **Falta una voz:** comprueba que existen tanto el `.onnx` como el `.onnx.json` en `voices/`, y que coinciden con las rutas de `Jarvis.Core/piper-settings.json`.
- **El diagnóstico no detecta audio:** comprueba los dispositivos y permisos de Windows. `MicrophoneDevice` en `voice-input-settings.json` es un índice que puede cambiar de un equipo a otro.
- **El diagnóstico pasa pero no se oye Piper:** ejecuta `--test-voices`, confirma la salida predeterminada de Windows y revisa que el entorno `.venv-piper` se haya creado e instalado correctamente.
- **La compilación indica que un archivo está en uso:** cierra todas las instancias de J.A.R.V.I.S. y repite la compilación.

## Controles

| Tecla | Acción |
|---|---|
| Jarvis | Activa la escucha; también puedes decir la pregunta en la misma frase. |
| V | Pausar o reactivar la escucha por voz. |
| T | Escribir una pregunta. |
| Espacio | Empezar una grabación manual; Espacio de nuevo la termina. El silencio también la termina. |
| 1 / 2 / A | Responder y hablar en español / inglés / idioma automático. |
| Voz | Mientras Piper habla, di «Jarvis, detente» (también entiende «Jarvis, stop»). |
| N / C | Empezar una conversación / continuar otra guardada. |
| H | Ver el historial de la conversación actual. |
| M | Consultar, guardar o borrar recuerdos. |
| X | Borrar la conversación actual, previa confirmación. |
| Esc | En el menú, cerrar JARVIS; durante una respuesta, cancelarla o detener la voz. |
| Ctrl+C | Alternativa para cancelar durante una respuesta; en espera de activación pausa la escucha. |

Haz clic en la terminal antes de usar Esc. La escucha espera un segundo de silencio para cerrar la frase. Tras una respuesta, puedes hacer una pregunta de seguimiento durante 12 segundos sin repetir «Jarvis». Si solo dices «Jarvis», espera el aviso antes de formular la pregunta. Al no recibir una pregunta o seguimiento, vuelve a la espera sin inventar texto.

## Memoria y conversaciones

Los historiales y recuerdos se guardan localmente en `data/`. Enter continúa la conversación más reciente; C permite elegir otra y N inicia una nueva. En M, pulsa G para guardar un recuerdo y escribe `SI` para confirmarlo; B borra uno y L borra todos con confirmación. Los recuerdos se guardan solo cuando los confirmas. Borrarlos no elimina menciones en conversaciones anteriores; X borra el historial de la conversación actual.

## Voces y ajustes de reconocimiento

Piper selecciona español o inglés según el idioma de la respuesta; los controles 1 y 2 también fijan el idioma de la respuesta. A vuelve a la selección automática. La captura y transcripción se hacen localmente con Whisper y el audio no se guarda como archivo.

Los ajustes están en `Jarvis.Core/voice-input-settings.json`. El umbral de voz predeterminado es `0.0054` (10% más sensible que el valor anterior `0.006`), la pausa final es `1000` ms, la frase mínima es `200` ms y la duración máxima es `20` segundos. `FollowUpWaitSeconds` configura los 12 segundos de seguimiento. Reinicia JARVIS después de cambiar ajustes. No cambies el umbral por una sola transcripción fallida: el micrófono, la distancia, el ruido y los nombres propios afectan el reconocimiento.

## Verificación

```powershell
dotnet build .\Jarvis.Core\Jarvis.Core.csproj -c Release
dotnet run --project .\Jarvis.VoiceInputTests\Jarvis.VoiceInputTests.csproj -c Release
dotnet run --project .\Jarvis.Tests\Jarvis.Tests.csproj -c Release
dotnet build .\Jarvis.Desktop\Jarvis.Desktop.csproj -c Release
.\Validate-Jarvis.ps1
```

La suite completa puede requerir Ollama y las voces Piper instaladas, y reproduce audio en algunas pruebas. Las pruebas simuladas y los WAV sintéticos no sustituyen la validación con tu micrófono.

Para comprobar con el Qwen local que distingue una pregunta actual de un cálculo estable cuando Internet está desactivado, ejecuta `dotnet run --project .\Jarvis.Tests\Jarvis.Tests.csproj -c Release -- --live-web-routing`. Este chequeo usa Ollama local y no envía una búsqueda web.

## Estado de V0.8

V0.8 mejora la medición de actividad de voz con ventanas de 10 ms para distinguir voz y silencio dentro de cada bloque de captura. Las pruebas con el micrófono confirmaron activación, seguimiento en español e inglés, voces correspondientes, retorno a la espera tras 12 segundos y cancelación durante una respuesta. La interrupción hablada acepta órdenes explícitas con wake word («Jarvis, detente» / «Jarvis, stop») mientras se prepara o reproduce Piper; las pruebas deterministas y de cancelación de Piper pasan. La prueba real del micrófono detuvo la voz al decir «Jarvis, detente» y devolvió J.A.R.V.I.S. al seguimiento; Esc y Ctrl+C también detienen Piper. La precisión de nombres propios puede variar.








