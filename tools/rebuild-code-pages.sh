#!/usr/bin/env bash
# Rebuilds the two code pages from the current Assets/Scripts:
#   tools/code-map/CodeMap.html        (interactive map of every script)  -> published at https://claude.ai/artifact/9KCRYHzzmGrNWNFTuqqz7w
#   tools/code-health/CodeHealth.html  (health / optimization report)     -> published at https://claude.ai/artifact/SvWtYz7Y1c3Ja3BsNTNTHr
# Claude republishes each rebuilt file to its link with the Artifact tool (same URL, so the link stays the same).
# Pass --no-history to leave the Code Health trend chart alone.
set -e
cd "$(dirname "$0")/.."
python3 tools/code-map/build.py
python3 tools/code-health/build.py "$@"
echo "Rebuilt. Republish: tools/code-map/CodeMap.html -> https://claude.ai/artifact/9KCRYHzzmGrNWNFTuqqz7w"
echo "                    tools/code-health/CodeHealth.html -> https://claude.ai/artifact/SvWtYz7Y1c3Ja3BsNTNTHr"
