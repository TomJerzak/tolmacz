---
name: translation-quality
description: Safely change and measure Tolmacz translation behaviour. Use before and after any change to src/Tolmacz that can change translations (segmenting, cleanup, beam search, short phrases), and when asked to compare quality, run the evaluation or COMET.
---

# Change translation behaviour safely

Any change in `src/Tolmacz` can change translations in places the change was not meant for.
Follow every step and report the numbers.

## 1. Before the change
- Make a backup zip in the folder that contains the repository, without `bin`, `obj`, `dist`,
  `models` and `.evaluation`, named `Tolmacz-backup-before-<change>-<date>.zip`.
- Find the latest full evaluation run in `.evaluation/runs/`. If the code changed since it
  was made, run a new one first and use it as the "before" run.

## 2. Make the change
- Keep it as narrow as possible and explain in a code comment which model behaviour it fixes,
  with a real example.
- Add unit tests for the new rule, including cases it must leave unchanged.

## 3. Check it
1. Run all three test projects (see the `build-and-test` skill).
2. For short-phrase changes, score the short sets before and after. Report every answer that
   was correct before and is wrong after.
3. Run the full evaluation and compare:
   ```bash
   dotnet run --project tools/Tolmacz.Evaluation -c Release -- run --data .evaluation/data/flores.jsonl --output .evaluation/runs/<new>
   dotnet run --project tools/Tolmacz.Evaluation -c Release -- compare --before .evaluation/runs/<before> --after .evaluation/runs/<new> --output .evaluation/compare-<new>
   ```
   The full run takes about 20 minutes, so run it in the background. Do not rebuild while it
   runs, because the build replaces the files it is using.
4. Read every entry in `changed.jsonl`. Automatic checks cannot judge meaning.
5. When FLORES sentences changed, score COMET:
   ```bash
   .venv-comet/bin/python scripts/score_comet.py --run .evaluation/runs/<new>
   ```

## Do not tune rules to a check set
A rule designed by looking at a set's results scores too well on that set. So:
- Use a working set to design a rule.
- Write a separate check set, with expected answers, before seeing any result for it.
- Never change a rule because of the check set's errors. If that happens, the set no longer
  counts, and a new check set is needed.
- Report the check set result as the real measure, and say which sets were used for design.

## Report
Give: tests passed, changed translations by category, new and fixed problems, short-set
scores before and after, COMET before and after, and any wrong results that remain.
Do not change translation code to hide differences between macOS and Windows; int8 results
differ between Apple and x64 processors at near-ties.
