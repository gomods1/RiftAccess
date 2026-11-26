# Changelog

## 0.3.0

From player feedback: "I take damage for hitting certain lanes, but they don't have a different audio cue."

- Key lockout: the game locks the lane keys for a beat and a half after three presses with nothing to hit within a beat (or nine within five beats), shown only as a padlock on screen. Monsters arriving meanwhile hurt you. Now a latch sound plays and the mod says "keys locked" (explained the first time), and a light click says the keys work again. New setting "Announce key lockout".
- Down (all three lanes) misses in every lane when one lane is empty. The mod now says so (explained the first time, then short, at most every 4 seconds).
- Damage says what hurt you: "health 7, Skeleton, left".
- Monster names (new monster announcements, damage) are in the game's language, from the bestiary.
- F1 help and sound guide explain the lockout and play its sounds.
- Vibe chains: "vibe chain, 6 monsters" when one starts (on screen the monsters only glow), then "chain broken", "chain complete" or "chain complete, all perfect". New setting "Announce vibe chains".
- Active modifiers (pins) are spoken at the start of a song with their effect, e.g. Glass Guitar. Micro Rift challenges are named.
- Bat Bane, Slime Bane and the other Bane modifiers: the game hits a damaged monster's remaining hits for you, so only its first hit is cued.
- Results: the letter grade is spoken (only shown and played as a jingle before).
- Traps are announced when they appear: kind, lane and row, and what they do (bounce direction, portal exit, coal speeds monsters up, mystery hides the monster's kind). New setting "Announce traps".
- Boss battles: boss health at 75, 50 and 25 percent; H gives boss health and phase, your health, shield and vibe charges, and the battle time. Minigames: health changes and H.
- The start countdown, silent in most songs, now ticks (the last tick higher) when the game itself plays no countdown sound.
- H and the results say how early or late you hit on average, in milliseconds.
- Track lists and the story map say your best grade on the selected difficulty; story beats say "completed".
- Developers: hidden config option DevFastBoot skips the intro and title screen (tools/boot.sh waits for the menu instead of fixed delays).
- Story dialogue scenes are described: each line says who speaks and with what expression ("Dove, surprised: ..."), plus who appears or leaves, the place when the background changes (25 locations described), the screen darkening and the Rift's effects. In-song dialogue says the speaker and whether they shout or think. New setting "Describe scenes".
- The intro and outro cinematics have audio description, timed to the video (15 and 38 lines).
- Boss battles describe the boss's entrance, phase transitions and defeat (Harmonie and her Banshee form, the Deep Blues duo, the Matron, the Reaper, the NecroDancer), and Cadence's defeat.
- Review buffers on Alt+arrows (Ctrl is the game's tab and difficulty key): Alt+Up and Alt+Down read the focused item's details, such as the description of its picture; Alt+Left and Alt+Right switch to the list of recent announcements. Pictures described: album covers, monsters, food and traps, about 210 profile portraits, 163 profile icons, card frames and name colours, boss, story map, results and minigame art, the title logo and main menu. After the
  focused item come the screen's large pictures: characters first, then specific art, screen-wide backdrops last
  (main menu Cadence, logo and city; song list backdrop per difficulty; story chapter panels, stage-type banners and
  bookshelf backdrop; profile backdrop). A focused row reads up to three of its own pictures.
- In a tutorial box, Alt+Up describes its silent demo video (all 76: monsters, food, traps, vibe power, boss and
  minigame clips). Paused in a song, Alt+Up describes the two portraits beside the board and how they are reacting
  (a rival's reactions are the reverse), then the song's background. In a boss battle, the boss's minions on the field.
- The main menu's promotion carousel says what it is: "Promotion: DLC Out Now!, slide 2 of 8, Enter opens its Steam store page".
- The daily challenge countdown clock is no longer read every second.

## 0.2.0

From player feedback: hold notes and health items were hard to tell apart, and the first minigame's instructions were confusing.

- New cue sounds (genny-reloaded). Each lane has its own instrument as well as its own side and pitch: marimba on the left, kalimba in the middle, steel drum on the right.
- Hold notes: an accordion note instead of the tap, then a soft hum in the lane for as long as you hold the key, then the release pop. New setting "Hold tone". The first hold note of a session also gets a spoken reminder.
- Food and hearts: the lane's own single note with a sparkle above it, no longer two notes that overlap other lanes' pitches.
- Boss battles and minigames: up and down are their own sounds (the kalimba gliding up or down) instead of a slightly higher or lower pitch.
- F1 help ends with a sound guide: each line plays the sound it describes.
- The "new Rift profile item unlocked" popup is read in full: what was unlocked and where to equip it, not only its Accept and Equip buttons. F2 repeats it.
- Minigame tutorials reworded in every language (yoga, breathing, burger, photo, show): one sound per move tells you the key (left, right, rising for up, falling for down) and the moment to press. Hold notes are explained in the basic tutorial, the wyrm lesson, the burger condiments and the help.

## 0.1.0

First release.

- Lane cues: every monster you must hit sounds in its lane (panned and pitched by lane) one beat before it arrives, including multi-hit monsters, bats and skulls that change lanes, and hopping zombies. Hold notes get a long tone and a release pop; food and hearts a sparkle.
- Vibe ready chime, low health warning, combo, health and results announcements. H reads the status during a song.
- Boss battles and minigames: a sound for each move in its direction.
- Menus, track list, popups, dialogue, pause menu, game over and results read through your screen reader (Prism), in every language the game has.
- Tutorials adapted: lines that describe the screen are replaced by explanations of how to play by ear.
- F1 help, F2 repeat, F10 settings (cue lead, volume, stereo width, music lowering and more).

Install with `RiftOfTheNecroDancerAccessibilityInstaller.exe`, or extract the zip into the game folder (next to `RiftOfTheNecroDancer.exe`).
