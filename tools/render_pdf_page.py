"""Render one PDF page to a PNG preview for the local StudyLens UI."""

from __future__ import annotations

import argparse
from pathlib import Path

import pypdfium2 as pdfium


def render_page(source: Path, output: Path, page_number: int, target_width: int = 1200) -> None:
    if page_number < 1:
        raise ValueError("page-number must be at least 1")

    document = pdfium.PdfDocument(str(source))
    try:
        if page_number > len(document):
            raise ValueError(f"PDF contains only {len(document)} pages")

        page = document[page_number - 1]
        try:
            width, _ = page.get_size()
            scale = max(1.0, min(3.0, target_width / max(width, 1.0)))
            bitmap = page.render(scale=scale)
            try:
                image = bitmap.to_pil()
                output.parent.mkdir(parents=True, exist_ok=True)
                image.save(output, format="PNG", optimize=True)
            finally:
                bitmap.close()
        finally:
            page.close()
    finally:
        document.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--page-number", required=True, type=int)
    parser.add_argument("--target-width", type=int, default=1200)
    args = parser.parse_args()
    render_page(args.source, args.output, args.page_number, args.target_width)


if __name__ == "__main__":
    main()
