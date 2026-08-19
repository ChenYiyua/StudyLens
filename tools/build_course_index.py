"""Build a page-cited local search index from PDF, Markdown, and text materials.

The generated JSON deliberately stores relative filenames rather than the source
folder. This keeps the index portable and avoids leaking a user's local path.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from pypdf import PdfReader


COURSE_ID = "tum-inhn0017-eam"
COURSE_NAME = "Enterprise Architecture Management and Reference Models"
WORD_PATTERN = re.compile(r"\S+")
WHITESPACE_PATTERN = re.compile(r"\s+")
SUPPORTED_SUFFIXES = {".pdf", ".md", ".txt"}


@dataclass(frozen=True)
class Chunk:
    id: str
    document_id: str
    title: str
    relative_path: str
    material_type: str
    page: int
    text: str


def chunk_to_json(chunk: Chunk) -> dict[str, object]:
    return {
        "id": chunk.id,
        "documentId": chunk.document_id,
        "title": chunk.title,
        "relativePath": chunk.relative_path,
        "materialType": chunk.material_type,
        "page": chunk.page,
        "text": chunk.text,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        required=True,
        type=Path,
        help="Folder containing PDF, Markdown, or text course materials",
    )
    parser.add_argument("--output", required=True, type=Path, help="JSON index to create")
    parser.add_argument("--course-id", default=COURSE_ID, help="Stable URL-safe course identifier")
    parser.add_argument("--course-name", default=COURSE_NAME, help="Human-readable course name")
    parser.add_argument("--chunk-words", type=int, default=220)
    parser.add_argument("--overlap-words", type=int, default=40)
    return parser.parse_args()


def classify_material(relative_path: Path) -> str:
    name = relative_path.name.lower()
    path = relative_path.as_posix().lower()

    if "revision_notes" in name:
        return "revision-note"
    if "mockexam" in name or "final" in name or "skill check" in name:
        return "exam"
    if "solution" in name:
        return "solution"
    if "/exercise/" in f"/{path}" or "/exercises/" in f"/{path}":
        return "exercise"
    if name.startswith("h") and re.match(r"h\d+", name):
        return "exercise"
    if "/lecture/" in f"/{path}" or name.startswith("l"):
        return "lecture"
    if "case" in name or "archihotel" in name or "studyproject" in name or "c2f1g2" in path:
        return "case-study"
    return "supplement"


def stable_id(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:16]


def normalize_text(value: str) -> str:
    return WHITESPACE_PATTERN.sub(" ", value).strip()


def split_page(text: str, chunk_words: int, overlap_words: int) -> list[str]:
    words = WORD_PATTERN.findall(text)
    if not words:
        return []

    chunks: list[str] = []
    step = chunk_words - overlap_words
    for start in range(0, len(words), step):
        current = words[start : start + chunk_words]
        if not current:
            break
        chunks.append(" ".join(current))
        if start + chunk_words >= len(words):
            break
    return chunks


def extract_pages(material_path: Path) -> list[str]:
    """Return page-like text units while keeping PDF page numbers auditable."""
    if material_path.suffix.lower() == ".pdf":
        return [page.extract_text() or "" for page in PdfReader(str(material_path)).pages]

    # Form-feed is a portable explicit page boundary for plain-text fixtures.
    return material_path.read_text(encoding="utf-8").split("\f")


def build_index(
    source: Path,
    chunk_words: int,
    overlap_words: int,
    course_id: str = COURSE_ID,
    course_name: str = COURSE_NAME,
) -> dict[str, object]:
    if not source.is_dir():
        raise ValueError(f"Course folder does not exist: {source}")
    if overlap_words < 0 or chunk_words <= overlap_words:
        raise ValueError("chunk-words must be greater than overlap-words")

    material_paths = sorted(
        (path for path in source.rglob("*") if path.suffix.lower() in SUPPORTED_SUFFIXES),
        key=lambda path: path.as_posix().lower(),
    )
    if not material_paths:
        raise ValueError(f"No supported course materials found under: {source}")

    chunks: list[Chunk] = []
    documents: list[dict[str, object]] = []
    total_pages = 0
    empty_pages = 0

    for material_path in material_paths:
        relative_path = material_path.relative_to(source)
        portable_path = relative_path.as_posix()
        document_id = stable_id(portable_path.lower())
        material_type = classify_material(relative_path)
        pages = extract_pages(material_path)
        document_chunks = 0

        for page_number, raw_text in enumerate(pages, start=1):
            total_pages += 1
            text = normalize_text(raw_text)
            if not text:
                empty_pages += 1
                continue

            for chunk_number, chunk_text in enumerate(
                split_page(text, chunk_words, overlap_words), start=1
            ):
                chunks.append(
                    Chunk(
                        id=stable_id(f"{portable_path}:{page_number}:{chunk_number}"),
                        document_id=document_id,
                        title=material_path.stem,
                        relative_path=portable_path,
                        material_type=material_type,
                        page=page_number,
                        text=chunk_text,
                    )
                )
                document_chunks += 1

        documents.append(
            {
                "id": document_id,
                "title": material_path.stem,
                "relativePath": portable_path,
                "materialType": material_type,
                "pageCount": len(pages),
                "chunkCount": document_chunks,
            }
        )

    return {
        "schemaVersion": 1,
        "course": {"id": course_id, "name": course_name},
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "statistics": {
            "documentCount": len(documents),
            "pageCount": total_pages,
            "emptyPageCount": empty_pages,
            "chunkCount": len(chunks),
        },
        "documents": documents,
        "chunks": [chunk_to_json(chunk) for chunk in chunks],
    }


def main() -> None:
    args = parse_args()
    index = build_index(
        args.source.resolve(),
        args.chunk_words,
        args.overlap_words,
        args.course_id,
        args.course_name,
    )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(index, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )

    stats = index["statistics"]
    print(
        "Indexed "
        f"{stats['documentCount']} documents, {stats['pageCount']} pages and "
        f"{stats['chunkCount']} searchable chunks."
    )
    print(f"Skipped {stats['emptyPageCount']} pages without extractable text.")
    print(f"Output: {args.output.resolve()}")


if __name__ == "__main__":
    main()
