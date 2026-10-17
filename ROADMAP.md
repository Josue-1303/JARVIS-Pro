# J.A.R.V.I.S. Pro — Development Roadmap

Updated: 17 October 2026

**Current milestone: V0.9 — desktop interface (in progress).**

Checked items are implemented. Future milestones below are proposals, not work already approved or started.

## Foundation — complete

- [x] C# / .NET 10 Windows application with Jarvis.Core.
- [x] Local Ollama integration with Qwen3 8B.
- [x] Typed questions and conversation responses.
- [x] Microphone recording through NAudio and transcription through Whisper.net.
- [x] Whisper configured for automatic language detection.

The user confirmed the complete microphone/transcription/response/playback workflow in Spanish and English on 16 October 2026.

## V0.4 — bilingual speech complete

- [x] Integrate local Piper speech generation and NAudio playback.
- [x] Spanish voice: es_MX-claude-high.
- [x] British English voice: en_GB-alan-medium.
- [x] Confirm both voices generate and play audio successfully.
- [x] User confirms both speaking speeds are correct.
- [x] Select the voice automatically from the response language.
- [x] Keep manual controls: 1 = Spanish, 2 = English, A = automatic.
- [x] Use Spanish when the response language is ambiguous.
- [x] Verify Spanish → English → Spanish with live Qwen3 and Piper.

Validation: zero build errors or warnings; 25 automatic-selection checks passed.

## V0.5 — persistent memory complete

- [x] Store conversations and approved memories locally in JARVIS-Pro/data.
- [x] Continue the latest or another saved conversation at startup.
- [x] Start a new conversation without importing the previous history.
- [x] View the full saved conversation history.
- [x] Add preferences explicitly with user confirmation.
- [x] View and delete individual memories or all memories.
- [x] Delete a conversation with confirmation.
- [x] Limit model context to 12 previous turns and 16,000 history characters.
- [x] Preserve the complete conversation on disk.
- [x] Protect writes against simultaneous instances and report damaged files.
- [x] Verify memory across two separate processes using live Ollama.
- [x] Save code backups and a V0.5 usage guide.

Validation: application builds with zero errors or warnings; 35 checks passed. Live restart recovered a fictional preference and conversation code. Tests used isolated data, not personal memories.

Controls: N = new conversation; C = choose a saved conversation; H = history; M = memories; X = delete current conversation.

Deleting a memory does not erase its mentions in existing conversation histories.

## V0.6 — smoother voice interaction (implemented scope validated)

- [x] Stop speech playback with Esc; Ctrl+C also cancels during a response.
- [x] Cancel a pending Ollama HTTP request without saving an incomplete turn.
- [x] Cancel Piper generation, stop its process and remove temporary audio.
- [x] Return to the menu and accept another question after cancellation.
- [x] Keep a completed response in history when only its speech is interrupted.
- [x] Show thinking, speech-generation and speaking states with cancellation hints.
- [x] Verify cancellation with real Ollama and both Piper voices, then verify recovery.
- [x] Validate physical keyboard controls in an interactive user session.
- [x] Verify the entire microphone → Whisper → Qwen → Piper conversation flow in Spanish and English.

Validation: main application builds with zero errors or warnings; 24 cancellation checks (including real Ollama and Piper) and all 35 V0.5 memory checks passed. On 16 October 2026, the user reported all five guided acceptance tests passed:

- [x] Esc cancels while thinking, returns to the menu and leaves no incomplete turn in history.
- [x] Esc interrupts Spanish speech while preserving the completed response in history.
- [x] Ctrl+C cancels without closing the active session; the next question works normally.
- [x] Spanish microphone input is transcribed, answered and spoken in Spanish.
- [x] English microphone input selects the British voice; interruption and the next microphone question work.

These interactive results are user-confirmed. No microphone recording or transcription cancellation was added in this delivery.

Controls: during a response, Esc or Ctrl+C cancels the active phase; Esc at the idle menu exits. See README.md for the current controls.

### Remaining reliability work

- [ ] Exercise unavailable microphone, audio output and Ollama scenarios and improve recovery where needed. These failure scenarios were not covered by the five successful user tests.

## V0.7 — voice control (implemented scope awaiting user validation)

- [x] End a spoken phrase automatically after one second of silence.
- [x] Reject very brief noise and bound each phrase to 20 seconds.
- [x] Activate on «Jarvis» at the start of a phrase, optionally after a greeting.
- [x] Accept «Jarvis + question» or «Jarvis», then a question after the ready message.
- [x] Keep Space for manual recording/early stop and T for typing.
- [x] Add V to pause/resume wake listening; show its current state.
- [x] Release the microphone before menus, Ollama requests and speech playback.
- [x] Cancel Whisper transcription when handing control back to the keyboard.
- [x] Keep incoming audio in memory rather than saving microphone recordings.
- [x] Reuse the local Whisper model in the shared models folder.
- [x] Validate silence logic, capture cleanup, wake routing and keyboard handoff.
- [x] Validate actual Whisper recognition using Spanish/English synthetic audio, isolated wake word and negative phrases.
- [x] Validate activation, silence timing and keyboard fallback with the user's microphone and pronunciation in V0.8.
- [ ] Tune microphone sensitivity if the real environment requires it.

Validation: Release build has zero errors or warnings; 44 voice-input checks, 35 memory checks and 12 deterministic cancellation checks passed. The audio fixtures used real Whisper inference with synthetic Piper audio, not a live user microphone. The previous Debug application was open and locked its executable, so verification used Release. Close the old instance before starting the new version.

Implementation limits: activation uses local Whisper transcription after a pause, not a dedicated instantaneous keyword detector. Exact initial-word aliases «Carvis» and «Carbise» cover spellings observed in the Spanish fixtures. Listening is paused while JARVIS speaks. Spoken cancellation and interrupting JARVIS by speaking are future work, not delivered here.

Controls: V = pause/resume wake listening; Space = manual start/stop (silence also ends recording); T = type. See README.md for current usage and settings.

## V0.7.1 — continuous conversation and recognition tuning

- [x] Listen for a follow-up for 12 seconds after completed speech, without requiring Jarvis again.
- [x] Allow a phrase started within that window to finish normally.
- [x] Return to wake mode after silence; preserve V pause and keyboard controls.
- [x] Lower capture threshold from 0.008 to 0.006 and preserve 500 ms before speech detection.
- [x] Install multilingual Whisper Small; keep Base available as a speed fallback.
- [x] Pass 16 continuous-conversation checks, 35 memory checks and 12 cancellation checks.
- [x] Pass 48 voice-input checks, including real Whisper Small inference with bilingual Piper fixtures, isolated wake word, negative phrases and «Jarvis, salúdame en español».
- [x] Build the updated application with zero errors and warnings in VoicePreview (the previous app may still be open).
- [x] Validate continuous conversation and recognition with the user's microphone in V0.8; response latency was exercised during the reported question-answer cycles.

Follow-up fix: reproduced an unrelated greeting in response to an English time question with the saved conversation. Updated the model instructions to answer the latest request and supply a fresh local computer clock on every call. Live Ollama tests recovered the affected history and passed English time → arithmetic → Spanish time with isolated test data. Personal conversations were preserved. Jarvis.ReplyTests provides a repeatable live regression check.

Weekday follow-up: explicit weekday context alone still failed against incorrect prior replies. Common current-local date/time requests now return deterministic computer-clock answers in Spanish/English, including a contextual request to check again. Other dates, locations and compound requests remain model-handled. Tests passed for Friday 16 October 2026, midnight rollover, wrong saved weekday history, bilingual answers and saved clock replies; build: zero warnings/errors, memory: 35 checks. Clock accuracy follows the Windows clock/time zone.

Esc report: 28 cancellation checks passed, including an injected Escape through the actual operation wrapper stopping real Piper playback in Spanish and English within three seconds. The user was unsure of terminal focus. Physical Escape delivery from VS Code remains pending verification; no cancellation-code fix is claimed. Focus the running terminal before pressing Esc; Ctrl+C is the existing alternative during a response.

User confirmed basic V0.7 activation works, but reported needing to repeat Jarvis and a misrecognition of «salúdame en español». V0.7.1 addresses these reports. Synthetic tests cannot establish accuracy with the user's actual microphone. See README.md for current startup instructions and microphone checks.

## V0.8 — voice reliability and validation (voice scope validated; packaged installation deferred)

- [x] Measure speech activity in 10 ms PCM frames instead of treating an entire 100 ms capture callback as voice or silence.
- [x] Add regression checks proving brief bursts do not count as sustained speech and speech mixed with silence is preserved.
- [x] Build Release with zero warnings/errors and pass the full validation script: 77 maintenance, 19 continuous-input, 54 voice-input, 28 cancellation and 35 memory checks, plus live reply regressions and local diagnostics.
- [x] User tested «Jarvis» activation, the first English question and several follow-ups without repeating the wake word; Whisper transcribed the English prompts and Piper used English speech.
- [x] User tested Spanish questions and follow-ups; Whisper transcribed them and automatic voice selection used Piper Spanish.
- [x] User interrupted Piper during a spoken response and the application returned to the menu.
- [x] User left the post-wake question capture empty; JARVIS returned to wake-word listening without inventing a question.
- [x] User confirmed a full 12 seconds of follow-up silence returns to wake-word mode; suppress the redundant blank-input message on timeout.
- [x] Add a bounded confirmation path for uncertain short current-date/time phrases; never silently rewrite the recognized words.
- [x] Validate the new 10% higher microphone sensitivity (`SpeechThreshold` 0.0054) with a real microphone sample: «Jarvis» activated, the Spanish question was transcribed correctly, and no false activation occurred during 30 seconds of room silence. Broader noise and pronunciation testing remains recommended. Whisper confidence alone is not a safe trigger for clarification.
- [x] Validate spoken interruption with the real microphone while preserving Esc and Ctrl+C: «Jarvis, detente» stopped Piper mid-response and returned to the 12-second follow-up window. Real Piper tests also verified Esc, and a console test verified Ctrl+C.
- [x] Document clean Windows setup, required local assets, diagnostics, voice playback checks and common recovery steps in README.md.

The automatic speech detector and pre-roll remain local and in-memory. The 10 ms analysis granularity corrects how callbacks containing mixed speech/silence are measured; it does not establish recognition quality in the user's room. The diagnostic found one Windows microphone device and verified the local configuration. V0.8 preview 1 is not a completed milestone.

User microphone report, 16 October 2026: «Jarvis» activation and English/Spanish question-answer cycles worked, including follow-ups without repeating the wake word and matching Piper voices. Leaving the post-wake question capture empty returned to wake listening without inventing a question. The user also confirmed that 12 seconds of follow-up silence returns to wake mode. A spoken response was canceled and control returned to the menu. A short proper noun required restating; this is recorded as an ASR limitation, not a reason to change the global microphone threshold. A redundant English blank-input message on the silence path was removed; isolated voice tests pass (19 continuous-input and 54 voice-input checks). On 16 October, the synthetic date fixture transcribed as «¿Qué diga es hoy?» at 73.8% and correctly triggered a confirmation; the corrected Spanish phrase scored 71.9%, so a global confidence threshold would be misleading. The English fixture received the correct answer and voice. The microphone test later exposed «Dime la hora, por favor» reaching Qwen, which returned the malformed phrase «La hora actual es las 22:10». This utterance now routes to the local clock; Release tests cover this phrase and singular «Es la» versus plural «Son las». The full Release validation passed after the fix: zero build warnings/errors, 77 maintenance checks, 19 continuous-input checks, 54 voice-input checks, 28 cancellation checks, 35 memory checks, live Ollama regressions, and local diagnostics. The follow-up real-microphone retest passed: «dime la hora, por favor» was transcribed correctly, returned «Son las 22:18» at the 22:18 local time, Piper used Spanish, and 12 seconds of silence returned the app to wake-word mode. A live microphone retest on 16 October 2026 with `SpeechThreshold` 0.0054 detected «Jarvis», transcribed «dime la hora, por favor», returned the correct local time and used Piper Spanish; 30 seconds of room silence produced no false activation. Broader noise and pronunciation testing remains recommended. The spoken-interruption implementation now monitors microphone input during Piper synthesis/playback and only stops on an explicit wake-prefixed stop command. Deterministic stop/false-positive/listener-release checks and live Piper interruption tests pass. On 16 October 2026, the user said «Jarvis, detente» during Spanish Piper playback; the command was recognized, speech stopped, the completed answer remained saved, and J.A.R.V.I.S. entered its 12-second follow-up window. The follow-up timed out without inventing input and returned to wake listening. Real Piper Escape cancellation and a console Ctrl+C cancellation also returned control promptly.

## V0.9 — desktop interface (in progress)

- [x] Create the Windows WPF desktop application and compile Release with zero warnings/errors; interactive layout validation remains pending.
- [x] Distribute a local date/time readout, opt-in device location and compact weather forecast around the central core instead of grouping them into a text panel.
- [x] Request location only when the user activates its control; keep coordinates in memory and disclose that Open-Meteo receives them for weather lookup.
- [x] Show current conditions, today's high/low and precipitation chance; refresh the forecast every 30 minutes.
- [x] Add a persistent Spanish/English interface selector; localize controls, system statuses and weather while leaving conversation text unchanged.
- [x] Replace default white scrollbars with dark, draggable purple/blue scrollbar styling and a gold active state.
- [x] Build the Windows desktop chat interface on the existing .NET 10 voice and memory services.
- [x] Build the Windows UI with .NET 10 WPF/XAML, custom styles and minimal third-party dependencies.
- [x] Make animated J.A.R.V.I.S. cores the home-screen focal point, combining the chat, memory, rules and personality visuals while retaining their distinct colors.
- [x] Use black with purple/blue as the main palette, red for stop/errors, and gold for selective highlights.
- [ ] Keep the local interface free: no paid design software, commercial UI controls or required subscription for the core experience.
- [x] Provide text chat, microphone controls, listening/speaking status and an immediate stop button.
- [x] Preserve automatic Spanish/English voice selection and manual language controls.
- [x] Expose conversation history, persistent memories, editable user rules and personality controls.
- [x] Keep user data in a user data folder, separate from application binaries.
- [x] Add a persistent Internet Access switch and show its enabled/disabled state in the interface.
- [x] Let Qwen3 classify whether the current question needs web search; keep local knowledge, saved memories, rules, personality and the computer clock on the local path.
- [x] When a web-dependent question arrives with Internet Access off, explain that access is unavailable and do not answer from a guess.
- [x] When Internet Access is on but no search key is configured, explain what setup is missing without claiming a search occurred.
- [x] Search only the current question, treat results as untrusted evidence, cite returned sources, and display source links in chat.
- [x] Store the Ollama Web Search API key encrypted with Windows DPAPI; keep it and local user data out of version control.
- [x] Keep the news panel limited to the main chat page.
- [x] Pass the desktop Release build and all 83 isolated regression checks; live Qwen routing also passed for one current fact with Internet Access off and one stable arithmetic question.
- [ ] Add a simple Connections settings area showing available providers and account status; live access follows in V1.1.
- [ ] Validate the location permission flow, rendered layout and complete interface language switch in the user's interactive desktop session.
- [ ] Verify Internet Access on/off behavior and a real cited search with the user's configured key in the desktop session.
- [ ] Build and validate the desktop app before starting installer work.

## V1.0 — installable desktop assistant (proposed)

- [ ] Package the desktop interface and .NET runtime in a Windows installer.
- [ ] Add setup checks and guided provisioning for Ollama/Qwen, Piper, bilingual voices and Whisper.
- [ ] Download and verify large models during setup instead of embedding several gigabytes in the installer.
- [ ] Create Start Menu/Desktop launch options and preserve user data during upgrades.
- [ ] Verify installation, first launch, both languages, microphone, cancellation, upgrade and uninstall on a clean Windows profile.

## V1.1 — connected assistant (proposed)

- [ ] Offer a clear “Connect account” action for supported email and calendar providers; let the user choose the provider and account in the provider's official sign-in page.
- [ ] Use the installed-app OAuth authorization-code flow with PKCE; never collect or store the provider password in J.A.R.V.I.S.
- [ ] Explain requested permissions before consent, start with read-only scopes and show which account and access are connected.
- [ ] Store tokens securely in Windows and provide clear disconnect/revoke controls.
- [ ] Connect calendar and email in read-only mode first.
- [ ] Prepare drafts and proposed calendar changes.
- [ ] Require explicit confirmation before sending messages or changing external data.
## Known development note

A failed Debug test process was retained by Windows and denied termination. The final V0.5 test suite passed using a separate Release output. The main application build was unaffected. Current verification commands are in README.md and Validate-Jarvis.ps1.


## V0.7.2 — reliability maintenance

- [x] Recover bilingual answers for current date/time using the local system clock, including contextual rechecks and midnight rollover.
- [x] Correct detectable reply-language labels and align the requested reply language with the selected Piper voice.
- [x] Keep prior conversation data intact while filtering stale or invalid assistant context.
- [x] Confirm wake-phrase alias handling and reject embedded near-matches.
- [x] Verify Escape cancellation through the application path and real Spanish/English Piper playback.
- [x] Pass Release build (0 warnings/errors), 72 maintenance checks, 48 voice checks, 35 memory checks, 28 cancellation checks, reply-language/date regressions and local diagnostics.
- [x] Validate speech recognition with the user microphone and environment in V0.8; synthetic fixtures alone cannot guarantee personal recognition accuracy.

Known limit: a very short synthetic Spanish date question may be transcribed approximately. The app requests confirmation for bounded ambiguous variants; it does not claim perfect recognition. Whisper noise and pronunciation behavior remain part of V0.8 validation.




