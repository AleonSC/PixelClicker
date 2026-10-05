# Photo Sorter (Windows 11)

Open a photo, flip through its folder with ← / →, press a bound key to move the photo to that key's folder.

- **Open Photo…** (or drag a photo/folder onto the window) loads every image in that photo's folder.
- **← / →** previous / next photo. **Ctrl+Z** undoes the last move.
- Right panel: **+ Add keybind**, click the key box then press a key, click the folder button to pick the destination. Bindings are saved in `%AppData%\PhotoSorter\bindings.json`.
- Name clashes in the destination get a ` (1)` suffix. Supports jpg, png, bmp, gif, tif.

## Build
`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out`
produces a standalone `out/PhotoSorter.exe` (no .NET install needed). The GitHub Action `Build PhotoSorter` does this and uploads the exe as an artifact.
