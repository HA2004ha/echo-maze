# Microphone test matrix

Each row is a device/environment combination. Fill in **date** and **result** after every test; an empty row means untested. The goal: the game is never judged with an uncalibrated microphone.

| # | Combination | What to check | Date | Result |
|---|---|---|---|---|
| 1 | Unity editor on Linux (`6000.6.3f1`) | Length of `Microphone.devices`; if zero, the badge "mic not detected — using simulated input" must appear on the calibration page and `EchoDirector.sourceMode` must be switched to `Simulated` to produce pings (the shipped scene default is `ManualOnly`: Space only, no source at all) | | |
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
