# Microphone test matrix

Each row is a device/environment combination. Fill in **date** and **result** after every test; an empty row means untested. The goal: the game is never judged with an uncalibrated microphone.

**Schedule:** rows 1–2, 6–9 and 13 are week 2 tests (`MicrophoneEchoSource` + the gate land then, and the first Windows player build exists to run them on). Rows 3–5 and 10–12 need the calibration page, the Options persistence and the pause/quit handling, which are week 3.

| # | Combination | What to check | Date | Result |
|---|---|---|---|---|
| 1 | Unity editor on Linux (`6000.6.3f1`) | Length of `Microphone.devices`; if zero, the game falls back to `Simulated` with a Console warning (from week 3 it also shows the badge "mic not detected — using simulated input" on the calibration page) and `EchoDirector.sourceMode` can be switched to `Simulated` to produce pings (the shipped scene default is `ManualOnly`: Space only, no source at all) | | |
| 2 | Linux build | Same as 1 on the player (behaviour may differ from the editor) | | |
| 3 | Windows build ▸ laptop default microphone, quiet room | Ping from normal speech; `RawRms` moves on the calibration HUD; no false trigger over 10 seconds of silence | | |
| 4 | Windows build ▸ headset/external mic | Same as 3 + device selection from the dropdown and settings surviving a restart | | |
| 5 | Windows build ▸ noisy room / loud fan | False-trigger rate over 60 seconds of relative silence; after calibration it should reach zero or near zero | | |
| 6 | Windows build ▸ whisper | Lower threshold: a whisper produces a short-range ping (not a full one) | | |
| 7 | Windows build ▸ shout | Reach caps at `maxRadius` and no further; `minPingInterval` prevents spam | | |
| 8 | Speaker feedback (the laptop's own speakers, no headphones) | Do the echo effect/music trigger a false ping again? If so: reduce SFX Volume, increase the interval between pings, and the headphone guidance in Options | | |
| 9 | Latency | How long from speaking until the outgoing arc appears? Target: under ~150ms. Record `Microphone.GetPosition` and the buffer size | | |
| 10 | Pause / Resume | After pause (`AudioListener.pause` + closing the microphone) and resume, `IsRecording` is true again and `GetPosition` is correct | | |
| 11 | Quitting the game | **The microphone light must go off** (`Microphone.End` in `OnDisable` and `OnApplicationQuit`) | | |
| 12 | Incomplete `GetDeviceCaps` (sample rate zero) | Fallback to 44100 without an exception | | |
| 13 | Space (manual echo) with no microphone at all | Must work completely: longer cooldown, shorter reach — this path always has to stay playable | | |
| 14 | One sustained vowel, one sentence, one shout | Exactly **one ping per vocal event**: the gate needs a rising edge and hysteresis. A single sentence that produces five overlapping pings and a blinded field is a failure, not a loudness tuning problem | | |
| 15 | A stray spike (a cough, a chair, a keyboard) | The arc appears, and the enemy's suspicion meter moves a little — it must not jump straight to Chase. This is the design's tolerance for a noisy room; measure it, do not assume it | | |
| 16 | Landmark loops playing out of speakers, no headphones | Do the dripping pipe / generator / exit hum re-enter the microphone and ping for the player? Calibration must be measured **with the loops playing**, which is why row 8 is not enough | | |
| 17 | Cooldown against loudness | A shout costs `≈0.6 + 1.4·L` seconds of cooldown; continuous shouting must never be free | | |
