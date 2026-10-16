# J.A.R.V.I.S. Pro

Asistente de escritorio para Windows, en desarrollo. Esta versión incluye conversaciones persistentes, entrada de voz con Whisper, activación por «Jarvis», respuestas en español e inglés y síntesis local Piper.

## Estado compartible

- **V0.7.2 — mantenimiento y regresiones:** completado. Compilación Release sin advertencias; 72 comprobaciones de mantenimiento, 48 de entrada de voz, 35 de memoria, 28 de cancelación y pruebas de idioma/respuestas aprobadas en el equipo de desarrollo.
- **Pendiente:** validar de nuevo con el micrófono y entorno de cada usuario. El reconocimiento depende de voz, ruido y micrófono.
- **Límite conocido:** una frase sintética muy corta sobre la fecha puede transcribirse de forma aproximada. La app pide confirmación para esas variantes; no garantiza transcripción perfecta.

Consulta [ROADMAP.md](ROADMAP.md) para ver el recorrido y el siguiente paso.

## Requisitos

- Windows x64 y .NET SDK 10.
- [Ollama](https://ollama.com/) ejecutándose localmente con el modelo `qwen3:8b` disponible.
- Python 3.10 o posterior y el paquete `piper-tts` para las voces locales.
- Dos modelos de voz Piper compatibles (español e inglés), colocados en `voices/`. Los modelos no se distribuyen en este repositorio.
- Micrófono para el modo de voz. Whisper Small se descarga la primera vez y ocupa espacio local.

## Ejecutar

1. Clona el repositorio y abre una terminal en su carpeta.
2. Prepara Ollama y el modelo `qwen3:8b`.
3. Crea el entorno local de Piper (`.venv-piper`) e instala `piper-tts`.
4. Coloca tus modelos de voz en `voices/` con los nombres configurados en `Jarvis.Core/piper-settings.json`.
5. Ejecuta `Start-Jarvis.cmd`.

Los datos y modelos son locales. Las conversaciones se almacenan en `data/`, que se excluye del control de versiones. Revisa las licencias de Ollama, Piper, Whisper y cada modelo antes de distribuirlos.

## Controles

- `1`: respuesta/voz en español; `2`: respuesta/voz en inglés; `A`: selección automática.
- `Esc`: cancelar la respuesta o detener la voz cuando la terminal de J.A.R.V.I.S. tiene el foco; `Ctrl+C` es alternativa.
- La activación continua escucha «Jarvis»; mientras el asistente habla, la escucha se pausa. Interrumpirlo hablando aún está pendiente.

## Compilar y comprobar

```powershell
dotnet build Jarvis.Core/Jarvis.Core.csproj -c Release
```

Los proyectos de prueba separados contienen comprobaciones unitarias. Las pruebas de transcripción con Whisper requieren audio sintético de prueba, que no se distribuye aquí. La validación de voz en tiempo real también requiere Ollama, Piper, modelos locales y dispositivos configurados. `Validate-Jarvis.ps1` ejecuta las comprobaciones reproducibles que no requieren esos recursos.

## Privacidad del repositorio

Este repositorio es privado: el enlace solo funciona para ti y las personas a quienes concedas acceso desde GitHub.

No se incluyen conversaciones, recuerdos, copias de seguridad, registros, voces descargadas, modelos de lenguaje/Whisper, entornos virtuales ni credenciales. El audio en `*/fixtures/` es material sintético de prueba.



