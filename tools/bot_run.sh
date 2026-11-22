#!/bin/bash
# Lets the cue bot play the current stage: keeps the window in front, resumes when paused, advances dialogue
# (one Enter per new narration line). Usage: tools/bot_run.sh SECONDS
cd "$(dirname "$0")/.."
P=/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess/RiftAccess_speech.log
(pwsh -NoProfile -File tools/hold_focus.ps1 -Seconds $1 >/dev/null 2>&1 &)
end=$((SECONDS + $1)); seen=0
while [ $SECONDS -lt $end ]; do
  last=$(tail -1 $P); n=$(wc -l < $P)
  if echo "$last" | grep -q -E "Cue check|Canción completada|Canción fallida|Fin del juego|SELECCIÓN DE PISTA|REINTENTAR"; then break; fi
  if [ "$n" != "$seen" ]; then
    seen=$n
    if echo "$last" | grep -q -E "VOLVER AL JUEGO|EN PAUSA"; then tools/keys.sh ENTER 200 >/dev/null 2>&1
    elif echo "$last" | grep -q -E "\| .{25,}"; then sleep 2; tools/keys.sh ENTER 200 >/dev/null 2>&1; fi
  fi
  sleep 2
done
