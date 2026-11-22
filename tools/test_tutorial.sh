#!/bin/bash
# From the Extras > Tutorials list: find the tutorial NAME, let the cue bot play it for SECONDS, quit it, print the cue check.
cd "$(dirname "$0")/.."
NAME="$1"; SECS=${2:-70}
P=/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess/RiftAccess_speech.log
(pwsh -NoProfile -File tools/hold_focus.ps1 -Seconds $((SECS + 90)) >/dev/null 2>&1 &)
for i in $(seq 1 60); do tail -2 $P | grep -q -- "| $NAME\$" && break; tools/keys.sh DOWN 250 >/dev/null; done
tools/keys.sh ENTER 8000 >/dev/null
tools/bot_run.sh $SECS
tools/keys.sh ESC 1500 >/dev/null
for i in $(seq 1 5); do tail -1 $P | grep -q "SALIR DE LA FASE" && break; tools/keys.sh DOWN 600 >/dev/null; done
tools/keys.sh ENTER 5000 >/dev/null
echo "== $NAME: $(grep -a 'Cue check' $P | tail -1 | cut -d'|' -f2-)"
