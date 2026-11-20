#!/usr/bin/env python3
"""Add COMET quality scores to an evaluation run.

This optional developer step needs a separate Python environment:
    python3 -m venv .venv-comet
    .venv-comet/bin/pip install unbabel-comet "setuptools<81"
    .venv-comet/bin/python scripts/score_comet.py --run .evaluation/runs/baseline
"""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path

import torch
from comet import load_from_checkpoint
from huggingface_hub import snapshot_download

MODEL_ID = "Unbabel/wmt22-comet-da"


def load_model():
    """Load the COMET checkpoint without allowing arbitrary pickled objects."""
    original_load = torch.load

    def safe_load(*args, **kwargs):
        kwargs["weights_only"] = True
        return original_load(*args, **kwargs)

    torch.load = safe_load
    return load_from_checkpoint(snapshot_download(MODEL_ID) + "/checkpoints/model.ckpt")


def main() -> None:
    """Write per-sentence COMET scores and category averages."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, required=True)
    parser.add_argument("--batch-size", type=int, default=8)
    args = parser.parse_args()

    with (args.run / "results.jsonl").open(encoding="utf-8") as results:
        rows = [row for row in map(json.loads, results) if row.get("reference")]
    samples = [{"src": row["source"], "mt": row["translation"], "ref": row["reference"]} for row in rows]
    scores = load_model().predict(samples, batch_size=args.batch_size, gpus=0, progress_bar=False, num_workers=2).scores

    groups: dict[str, list[float]] = defaultdict(list)
    with (args.run / "comet.jsonl").open("w", encoding="utf-8") as output:
        for row, score in zip(rows, scores):
            value = round(float(score) * 100, 3)
            groups[f"{row['category']} {row['direction']}"].append(value)
            output.write(json.dumps({"id": row["id"], "comet": value}) + "\n")

    summary = {name: round(sum(values) / len(values), 3) for name, values in sorted(groups.items())}
    (args.run / "comet-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
