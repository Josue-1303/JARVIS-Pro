# Hoja de ruta de J.A.R.V.I.S. Pro

## Completado

- [x] **V0.1–V0.4:** asistente conversacional local y síntesis de voz inicial.
- [x] **V0.5:** conversaciones y recuerdos persistentes en `data/`, con opciones para crear, continuar, consultar y borrar.
- [x] **V0.6:** cancelación de respuestas y reproducción de voz con `Esc` y `Ctrl+C` como alternativa.
- [x] **V0.7:** activación por palabra «Jarvis», detección de silencio y continuidad de conversación.
- [x] **V0.7.1:** respuestas bilingües con voz alineada al idioma, controles manuales `1`/`2` y modo automático.
- [x] **V0.7.2:** mantenimiento integral, recuperación de respuestas de fecha/hora, corrección conservadora de etiquetas de idioma, manejo seguro del historial y pruebas de regresión.

## Evidencia V0.7.2

Compilación Release: **0 advertencias, 0 errores**. En el equipo de desarrollo pasaron 72 comprobaciones de mantenimiento, 48 de voz, 35 de memoria, 28 de cancelación, pruebas de respuesta y diagnóstico de dispositivos/modelos locales. La suite incluye datos aislados y fixtures de audio sintético.

El texto y la voz se alinean en inglés/español, el modo manual conserva el idioma solicitado, las preguntas habituales de hora/fecha usan el reloj local y Esc detiene una reproducción real de Piper. Las pruebas no garantizan precisión idéntica con cada voz, sala o micrófono.

## Siguiente paso: V0.8 — validación y escucha fiable

- [ ] Repetir las pruebas de micrófono en un entorno cotidiano y registrar ejemplos anónimos de errores de transcripción.
- [ ] Ajustar umbral de voz, silencio y duración mínima según resultados medidos.
- [ ] Mejorar las preguntas de confirmación para transcripciones dudosas, evitando cambiar silenciosamente lo dicho.
- [ ] Diseñar y probar una interrupción por voz mientras J.A.R.V.I.S. habla; mantener `Esc` y `Ctrl+C` como controles alternativos.
- [ ] Añadir una guía de instalación/diagnóstico que no requiera conocimientos de desarrollo.

## Límites actuales

Whisper puede confundir frases cortas y parecidas; una prueba sintética breve produjo una transcripción aproximada. La app pide confirmación para algunas variantes acotadas de consultas de fecha. Hace falta validar con micrófono real antes de considerar terminada V0.8. No se promete reconocimiento perfecto.

Las consultas de otras ciudades/fechas y el acceso a internet o calendarios no forman parte del reloj local. La escucha no es global: las teclas llegan a J.A.R.V.I.S. cuando su terminal conserva el foco.
