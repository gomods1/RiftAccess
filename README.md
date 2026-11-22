# Rift Access

A blind-accessibility mod for **Rift of the NecroDancer** (Steam, Windows). It speaks the game's menus and dialogue through your screen reader and adds sounds that tell you when and where to hit, so the game can be played without sight.

Speech uses NVDA, JAWS or any other screen reader through Prism, and falls back to Windows speech when no screen reader is running. Every mod string follows the game's language: English, Spanish, French, German, Italian, Brazilian Portuguese, Russian, Japanese, Korean, and Simplified and Traditional Chinese.

## Install

Downloads: [latest release](https://github.com/gomods1/RiftAccess/releases/latest).

**With the installer (easiest):** download `RiftOfTheNecroDancerAccessibilityInstaller.exe` from the release and run it. It finds the game through Steam (or lets you browse to it), downloads the latest version of the mod, installs it, and can update or uninstall it later. It is screen reader accessible.

**By hand:**
1. Download `RiftAccess-vX.Y.Z.zip` from the release and extract everything into the game folder, next to `RiftOfTheNecroDancer.exe`. The zip contains BepInEx 5 (x64), the mod loader, and the `BepInEx\plugins\RiftAccess` folder (`RiftAccess.dll`, `prism.dll` and the `Sounds` folder).
2. Start the game. You will hear "Rift Access loaded" in the game's language.

To uninstall by hand, delete `BepInEx\plugins\RiftAccess`. To remove BepInEx as well, delete `BepInEx`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version`.

## How to play by ear

Monsters come down three lanes: left, middle and right. **Every monster you must hit makes a sound in its lane before it arrives.** The sound is panned to the lane and pitched by lane: low on the left, middle in the centre, high on the right. It plays one beat before the hit by default. Press the lane's key on the next beat: you hear the pattern, then answer it.

| Key | Action |
|---|---|
| Left arrow or A | Hit the left lane |
| Up arrow or W | Hit the middle lane |
| Right arrow or D | Hit the right lane |
| Down arrow or S | Hit all three lanes at once (when all three sound together) |
| Space or Shift | Activate vibe power (after the vibe ready chime) |
| Escape | Pause |

Kinds of sounds:
- **Tap**: a normal hit. Monsters that take several hits get one tap per hit, in the lane where each hit must land. That includes bats and skulls that jump lanes, and zombies that hop.
- **Long tone**: a hold note (wyrm). Press on the beat, keep holding, and let go at the **release pop**.
- **Sparkle**: food or a heart. Hit it to heal.
- **Rising chime**: vibe power is ready. Press Space.
- **Two-note warning**: your health is low.
- **Whoosh** (boss battles): an attack to dodge. Press its direction. A normal tap is a chance to counterattack. Up is a higher pitch, down a lower one.

The game's tutorials are adapted. Coach Zad's lines that describe the screen ("when a monster reaches the Action Row...") are replaced by explanations of how to play by ear. You can turn this off in the settings.

**Tips**
- The game's own **Practice Mode** (on the screen before a song) can slow a song down and loop part of it. It is the best way to learn a song.
- Use the settings' **Test lane cues** to hear the sounds and the lead with your current settings.
- Do the game's **audio calibration** (Settings, Calibration). Its first part is tapping Enter to a beat, which works by ear. Skip the video part.

## Mod keys

| Key | Action |
|---|---|
| F1 | Help: the keys for where you are (up and down to read, Escape to close) |
| F2 | Repeat the focused menu item |
| H | Status: health, combo, score, vibe power and how far into the song you are |
| F10 | Rift Access settings |

Menus are the game's own: arrows move, Enter selects, Escape goes back. The mod reads what is focused and what appears on screen, including the track list (name, artist, BPM, locked or new, unlock price), popups, dialogue, pause menu, game over and results.

## Settings (F10)

Up and down move through the settings, left and right change a value, and Enter toggles a checkbox or runs a button. Tab jumps to the next section. Control plus up or down reads what the setting does, and Escape closes the menu. Changes are saved immediately.

- **Lane cues**: on or off; **cue lead** (half a beat up to 4 beats before the hit); **count-in cues** (extra cues one beat apart before the main one); **click on the hit beat**; **cue volume**; **stereo width**; **different pitch per lane** (keep it on with one ear or mono sound); **cue timing offset** in milliseconds; **cue every hit of tough monsters**; **hold note release cue**; **sparkle sound for food and hearts**; **music volume during songs** (lowers the music while you play).
- **Announcements**: speech during songs; health changes; low health sound and threshold; vibe ready sound and speech; combo announcements every 10, 25, 50 or 100 hits; combo lost; new monster types; read results; blind-friendly tutorial; instant dialogue (each Enter moves to the next line).
- **Speech**: navigation interrupts speech; say position in menus; braille; debug logging.
- **Tools**: test lane cues; reset all settings (press Enter twice).

## Boss battles and minigames

**Boss battles** show a shrinking indicator for each move. The mod plays a sound for each one, in its direction, at your cue lead:
- a **whoosh** is an attack to dodge;
- a **tap** is a chance to counterattack;
- when the boss is stunned, centre taps mark the moments to attack (up, left or right all work).

Up sounds higher and down lower. On Hard boss battles the game shows no indicators, so there are no sounds either, exactly as for sighted players.

**Minigames** (yoga, burgers, photos, shows and the rest): every move gets a sound in its direction at your cue lead. Hold moves get a release pop. This works whether or not the game's own Assist mode is on.

## Known limitations

- Collisions between monsters can very rarely change a lane at the last moment. The cue then moves to the new lane, slightly late.
- The game pauses when its window loses focus. That is the game's own behaviour.
- The tracks' intensity meter is read as separate pieces ("x, 4").

## Credits

Speech through the Prism screen reader library (see `LICENSES` and `NOTICE` in the plugin folder). Mod loader: BepInEx. Sounds made with genny.
