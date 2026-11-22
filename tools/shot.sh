#!/bin/bash
# Screenshot the game window to shots/NAME.png at half size.
cd "$(dirname "$0")/.."
pwsh -NoProfile -File tools/shot.ps1 -Out shots/full.png > /dev/null && python -c "import sys; from PIL import Image; Image.open('shots/full.png').resize((960,540)).save(sys.argv[1])" "shots/${1:-shot}.png" && echo "shots/${1:-shot}.png"
