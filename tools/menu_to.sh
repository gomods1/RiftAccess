#!/bin/bash
# Press DOWN (or UP with -u) until the spoken focus line starts with NAME. Usage: tools/menu_to.sh [-u] "NAME"
cd "$(dirname "$0")/.."
K=DOWN; [ "$1" = "-u" ] && { K=UP; shift; }
P=/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess/RiftAccess_speech.log
for i in $(seq 1 40); do tail -2 $P | grep -q -F -- "| $1" && exit 0; tools/keys.sh $K 350 >/dev/null; done; exit 1
