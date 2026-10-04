"""Keyboard-driven photo sorter for Windows (also runs on macOS/Linux).

Left/Right  - previous / next photo
Mapped keys - move current photo to the folder configured in config.json
Z / Backspace - undo last move
O           - choose a different source folder
Esc         - quit
"""
import json
import shutil
import sys
from pathlib import Path

from PyQt6.QtCore import Qt
from PyQt6.QtGui import QImageReader, QPixmap
from PyQt6.QtWidgets import QApplication, QFileDialog, QLabel, QMessageBox, QWidget, QVBoxLayout

CONFIG_PATH = Path(__file__).with_name("config.json")
IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff"}


def load_config():
    if not CONFIG_PATH.exists():
        default = {"source_folder": "", "keys": {"1": "sorted/keep", "2": "sorted/maybe", "3": "sorted/reject"}}
        CONFIG_PATH.write_text(json.dumps(default, indent=2), encoding="utf-8")
    cfg = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    keys = {str(k).lower(): Path(v).expanduser() for k, v in cfg.get("keys", {}).items()}
    return cfg.get("source_folder", ""), keys


def unique_destination(dest_dir: Path, name: str) -> Path:
    """Return a path in dest_dir that does not exist, appending ' (n)' if needed."""
    target = dest_dir / name
    stem, suffix, n = target.stem, target.suffix, 1
    while target.exists():
        target = dest_dir / f"{stem} ({n}){suffix}"
        n += 1
    return target


class Sorter(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Photo Sorter")
        self.resize(1100, 800)
        self.setStyleSheet("background:#111; color:#ddd;")
        self.label = QLabel(alignment=Qt.AlignmentFlag.AlignCenter)
        self.label.setMinimumSize(1, 1)
        self.status = QLabel(alignment=Qt.AlignmentFlag.AlignCenter)
        self.status.setStyleSheet("padding:4px; font-size:13px;")
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.addWidget(self.label, 1)
        layout.addWidget(self.status)

        self.source_text, self.keymap = load_config()
        self.files, self.index, self.pixmap = [], 0, None
        self.undo_stack = []  # (original_path, moved_path)
        self.finished = False

        start = Path(self.source_text).expanduser() if self.source_text else None
        if not (start and start.is_dir()):
            start = self.choose_folder()
        if start is None:
            sys.exit(0)
        self.load_folder(start)

    # ---- folder / file handling -------------------------------------------
    def choose_folder(self):
        d = QFileDialog.getExistingDirectory(self, "Select source folder of photos")
        return Path(d) if d else None

    def load_folder(self, folder: Path):
        self.folder = folder
        self.files = sorted((p for p in folder.iterdir() if p.suffix.lower() in IMAGE_EXTS and p.is_file()),
                            key=lambda p: p.name.lower())
        self.index = 0
        self.undo_stack.clear()
        self.show_current()

    def show_current(self):
        # Skip files that can't be decoded or have vanished.
        while self.files:
            self.index = max(0, min(self.index, len(self.files) - 1))
            path = self.files[self.index]
            # Decode fully into memory so no file handle stays open (avoids Windows locks).
            reader = QImageReader(str(path))
            reader.setAutoTransform(True)  # honour EXIF rotation
            img = reader.read()
            del reader
            if not img.isNull():
                self.pixmap = QPixmap.fromImage(img)
                self.finished = False
                self.fit_pixmap()
                self.status.setText(f"{self.index + 1}/{len(self.files)}  {path.name}    "
                                    + "  ".join(f"[{k}] {v}" for k, v in self.keymap.items()))
                return
            self.files.pop(self.index)
        self.pixmap, self.finished = None, True
        self.label.setPixmap(QPixmap())
        self.label.setText("<h1>Finished!</h1><p>No more images in this folder.<br>"
                           "Z = undo, O = open another folder, Esc = quit</p>")
        self.status.setText("")

    def fit_pixmap(self):
        if self.pixmap:
            self.label.setPixmap(self.pixmap.scaled(self.label.size(), Qt.AspectRatioMode.KeepAspectRatio,
                                                    Qt.TransformationMode.SmoothTransformation))

    def resizeEvent(self, e):
        super().resizeEvent(e)
        self.fit_pixmap()

    # ---- actions ----------------------------------------------------------
    def move_current(self, dest_dir: Path):
        if not self.files:
            return
        src = self.files[self.index]
        try:
            dest_dir.mkdir(parents=True, exist_ok=True)
            dest = unique_destination(dest_dir, src.name)
            self.pixmap = None  # drop our reference before moving
            shutil.move(str(src), str(dest))
        except OSError as err:
            QMessageBox.warning(self, "Move failed", f"{src.name}\n\n{err}")
            self.show_current()
            return
        self.undo_stack.append((src, dest))
        self.files.pop(self.index)  # next image slides into this index
        self.show_current()

    def undo(self):
        if not self.undo_stack:
            return
        src, dest = self.undo_stack.pop()
        try:
            back = unique_destination(src.parent, src.name)
            shutil.move(str(dest), str(back))
        except OSError as err:
            QMessageBox.warning(self, "Undo failed", str(err))
            return
        self.files.append(back)
        self.files.sort(key=lambda p: p.name.lower())
        self.index = self.files.index(back)
        self.show_current()

    def keyPressEvent(self, e):
        key = e.key()
        if key == Qt.Key.Key_Right:
            if self.files and self.index < len(self.files) - 1:
                self.index += 1
                self.show_current()
        elif key == Qt.Key.Key_Left:
            if self.files and self.index > 0:
                self.index -= 1
                self.show_current()
        elif key in (Qt.Key.Key_Z, Qt.Key.Key_Backspace):
            self.undo()
        elif key == Qt.Key.Key_O:
            d = self.choose_folder()
            if d:
                self.load_folder(d)
        elif key == Qt.Key.Key_Escape:
            self.close()
        else:
            dest = self.keymap.get(e.text().lower()) if e.text() else None
            if dest is not None:
                self.move_current(dest if dest.is_absolute() else self.folder / dest)


def main():
    app = QApplication(sys.argv)
    w = Sorter()
    w.show()
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
