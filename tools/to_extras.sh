#!/bin/bash
# Relaunch (no build) and open Extras (with "tutorials": Extras > Tutorials). Holds focus while navigating.
cd "$(dirname "$0")/.."
tools/boot.sh -n > /dev/null
(pwsh -NoProfile -File tools/hold_focus.ps1 -Seconds 40 >/dev/null 2>&1 &)
tools/menu_to.sh "Extras" && tools/keys.sh ENTER 3000 > /dev/null
[ "$1" = "tutorials" ] && { tools/menu_to.sh "Tutoriales" && tools/keys.sh ENTER 3000 > /dev/null; }
tools/speech.sh 3
