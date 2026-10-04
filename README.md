# Photo Sorter

Keyboard-only photo viewer/sorter (Python + PyQt6).

## Install & run (Windows)
1. Install Python 3.9+ from python.org (tick "Add Python to PATH").
2. In a terminal in this folder: `pip install -r requirements.txt`
3. Edit `config.json` (see below), then run: `python photo_sorter.py`

## Config (`config.json`)
- `source_folder`: folder to open at start ("" = show a folder picker).
- `keys`: key -> destination folder. Relative paths are relative to the source folder; use forward slashes or `\\\\` for absolute Windows paths. Folders are created automatically.

## Keys
Left/Right = prev/next · mapped keys = move & show next · Z/Backspace = undo last move · O = open another folder · Esc = quit
