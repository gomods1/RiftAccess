#!/bin/bash
# Send keys to the game window. Usage: tools/keys.sh DOWN,DOWN,ENTER [delayMs] [extra flags e.g. -Shift]
cd "$(dirname "$0")"
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& '.\SendKeys.ps1' -Keys $1 -DelayMs ${2:-700} $3" 2>&1 | tail -2
