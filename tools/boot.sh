#!/bin/bash
# Rebuild (unless -n), relaunch, reach the main menu (accepts the privacy warning). Sends real keys, so it takes
# the window's focus. With DevFastBoot = true in the mod config the intro and title are skipped by the mod;
# without it the old key presses are sent.
cd "$(dirname "$0")/.."
P="/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess/RiftAccess_speech.log"
C="/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/config/gomods.riftaccess.cfg"
if [ "$1" = "-n" ]; then pwsh -NoProfile -File tools/restart.ps1 -NoBuild -WaitSec 3 | grep -E "error|Exception"; else pwsh -NoProfile -File tools/restart.ps1 -WaitSec 3 | grep -E "error|Exception"; fi
(pwsh -NoProfile -File tools/hold_focus.ps1 -Seconds 40 >/dev/null 2>&1 &)
if grep -q "DevFastBoot = true" "$C"; then
  for i in $(seq 1 60); do grep -q -E "Advertencia|\| Jugar|Warning|\| Play" "$P" 2>/dev/null && break; sleep 0.5; done
  sleep 1
  grep -q -E "Advertencia|Warning" "$P" && tools/keys.sh ENTER 2500 > /dev/null
else
  tools/keys.sh ENTER,ENTER 1000 > /dev/null
  for i in $(seq 1 60); do grep -q "Pulsa cualquier" "$P" && break; sleep 1; done
  sleep 1; tools/keys.sh ENTER 6000 > /dev/null
  for i in $(seq 1 20); do grep -q "Advertencia" "$P" && break; sleep 1; done
  tools/keys.sh ENTER 4000 > /dev/null
fi
tools/speech.sh 3
