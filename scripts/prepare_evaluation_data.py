#!/usr/bin/env python3
"""Download FLORES-101 Polish and English sentences for local evaluation.

The downloaded data is written under .evaluation, which Git ignores. FLORES has its own
CC BY-SA 4.0 license and is not part of Tolmacz.

Usage:
    python3 scripts/prepare_evaluation_data.py --output .evaluation/data/flores.jsonl
"""
from __future__ import annotations

import argparse
import json
import subprocess
import time
import urllib.parse
from pathlib import Path

DATASET = "gsarti/flores_101"
PAGE_SIZE = 100


def fetch_json(url: str) -> dict:
    """Download a JSON document, retrying temporary network failures."""
    for attempt in range(5):
        result = subprocess.run(["curl", "-sfL", "-m", "60", url], capture_output=True, text=True)
        if result.returncode == 0:
            return json.loads(result.stdout)
        time.sleep(2 * (attempt + 1))
    raise RuntimeError(f"Could not download {url}")


def read_sentences(language: str, split: str) -> dict[int, str]:
    """Return one FLORES split as a mapping from sentence id to text."""
    sentences: dict[int, str] = {}
    offset = 0
    while True:
        query = urllib.parse.urlencode(
            {"dataset": DATASET, "config": language, "split": split, "offset": offset, "length": PAGE_SIZE}
        )
        page = fetch_json(f"https://datasets-server.huggingface.co/rows?{query}")
        rows = page["rows"]
        sentences.update({row["row"]["id"]: row["row"]["sentence"] for row in rows})
        offset += len(rows)
        if not rows or offset >= page["num_rows_total"]:
            return sentences


def main() -> None:
    """Write both translation directions for the FLORES dev and devtest splits."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    args.output.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    with args.output.open("w", encoding="utf-8") as output:
        for split in ("dev", "devtest"):
            english = read_sentences("eng", split)
            polish = read_sentences("pol", split)
            for sentence_id in sorted(english.keys() & polish.keys()):
                pairs = (
                    ("PolishToEnglish", "pl-en", polish[sentence_id], english[sentence_id]),
                    ("EnglishToPolish", "en-pl", english[sentence_id], polish[sentence_id]),
                )
                for direction, label, source, reference in pairs:
                    row = {
                        "id": f"flores-{split}-{sentence_id}-{label}",
                        "category": f"flores_{split}",
                        "direction": direction,
                        "source": source,
                        "reference": reference,
                    }
                    output.write(json.dumps(row, ensure_ascii=False) + "\n")
                    count += 1
    print(f"Wrote {count} evaluation cases to {args.output}")


if __name__ == "__main__":
    main()
