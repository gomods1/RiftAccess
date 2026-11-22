#!/bin/bash
# Rebuild (unless -n), relaunch, skip intro and title, accept the privacy warning. Ends on the main menu
# (possibly with a promo popup open). Sends real keys, so it takes the window's focus.
cd "$(dirname "$0")/.."
if [ "$1" = "-n" ]; then pwsh -NoProfile -File tools/restart.ps1 -NoBuild -WaitSec 12 | grep -E "error|Exception"; else pwsh -NoProfile -File tools/restart.ps1 -WaitSec 12 | grep -E "error|Exception"; fi
P="/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess/RiftAccess_speech.log"
tools/keys.sh ENTER,ENTER 1000 > /dev/null
for i in $(seq 1 60); do grep -q "Pulsa cualquier" "$P" && break; sleep 1; done
sleep 1; tools/keys.sh ENTER 6000 > /dev/null
for i in $(seq 1 20); do grep -q "Advertencia" "$P" && break; sleep 1; done
tools/keys.sh ENTER 4000 > /dev/null
tools/speech.sh 3
