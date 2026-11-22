#!/bin/bash
# Print the last N lines of the BepInEx log (default 30), optionally filtered by a regex.
L="/s/steam/steamapps/common/RiftOfTheNecroDancerOSTVolume1/BepInEx/LogOutput.log"
if [ -n "$2" ]; then grep -E "$2" "$L" | tail -n ${1:-30}; else tail -n ${1:-30} "$L"; fi
