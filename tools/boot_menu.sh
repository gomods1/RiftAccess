#!/bin/bash
# Relaunch (no build) to the main menu, then open each menu item named in the arguments in turn.
cd "$(dirname "$0")/.."
P=/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess/RiftAccess_speech.log
(pwsh -NoProfile -File tools/hold_focus.ps1 -Seconds 200 >/dev/null 2>&1 &)
pwsh -NoProfile -File tools/restart.ps1 -NoBuild -WaitSec 8 >/dev/null
tools/keys.sh ENTER 500 >/dev/null
for i in $(seq 1 60); do grep -q "Pulsa cualquier" $P && break; sleep 1; done
sleep 1; tools/keys.sh ENTER 7000 >/dev/null
grep -q "Advertencia" $P && tools/keys.sh ENTER 4000 >/dev/null
grep -q "Ver paquete" $P && { tools/keys.sh DOWN 600 >/dev/null; tools/keys.sh ENTER 2500 >/dev/null; }
for item in "$@"; do tools/menu_to.sh "$item" || { echo "not found: $item"; exit 1; }; tools/keys.sh ENTER 6000 >/dev/null; done
tools/speech.sh 3
