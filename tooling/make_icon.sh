#!/usr/bin/env bash
# Regenerate the 256x256 release icon from the high-resolution source.
set -euo pipefail
cd "$(dirname "$0")/.."

ffmpeg -hide_banner -loglevel error -y \
  -i resources/icon-source.jpg \
  -vf "scale=256:256:flags=area" \
  -frames:v 1 -pix_fmt rgba resources/icon.png

file resources/icon.png
