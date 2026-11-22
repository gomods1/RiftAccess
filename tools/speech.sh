#!/bin/bash
# Print the last N spoken lines (default 15).
P="/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/plugins/RiftAccess"
tail -n ${1:-15} "$P/RiftAccess_speech.log"
