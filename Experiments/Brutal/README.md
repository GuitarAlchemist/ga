# BRUTAL evaluation

A small, isolated prototype to judge whether [BRUTAL](https://github.com/ahwootech/BRUTAL), Ahwoo's C# framework with
native bindings (GLFW, Vulkan, ImGui, Jolt…), is useful for the Observatory and for composable software-factory
experiments. It is an evaluation, not a plan to replace Godot or the NVIDIA RTX fork. Coordination:
[GuitarAlchemist/gaia#257](https://github.com/GuitarAlchemist/gaia/issues/257).

## Pinned

- **Upstream:** `ahwootech/BRUTAL` `main` @ `a25cdf80b2a15530e7da6cff744def9d841ad3c3`.
- **Packages:** NuGet only, because upstream has no project files or build scripts yet. Exact versions are in the
  project file; transitive versions are in `HelloBrutal/packages.lock.json`, written by the first restore.
  - `Ahwoo.Brutal.Core` 2026.10.1
  - `Ahwoo.Brutal.Glfw` 2026.10.4
- **SDK:** .NET 10, through the repository's `global.json`. Run `dotnet` from inside the repository, because the
  current directory decides which SDK runs.

## Licence obligations

The [BRUTAL License v1.0](https://github.com/ahwootech/BRUTAL/blob/main/LICENSE.txt) is MIT-based with attribution
terms. Any build distributed to anyone must:

- credit Ahwoo in its credits screen (2.1);
- show the unaltered BRUTAL logo before its primary function starts (2.2);
- ship upstream's `NOTICE.txt` alongside (2.3).

Each library also carries third-party notices in its `LICENSES` folder. This prototype is not distributed. FMOD is
proprietary and separately licensed, so `Brutal.Fmod` is not used.

## Milestones

| Milestone | Status | Evidence |
|---|---|---|
| M0: discover and scope | PASS | [ACK and M0 report](https://github.com/GuitarAlchemist/gaia/issues/257#issuecomment-6282507425) |
| M1: one window, input, clean close | PASS, 7/7 checks, 2026-10-19 | [evidence/m1-20261019-233551.md](evidence/m1-20261019-233551.md) |
| M2: a small Observatory-relevant visual slice | NOT RUN | |
| M3: measurements | NOT RUN | |
| M4: report and continue/park recommendation | NOT RUN | |

## M1

`HelloBrutal` is upstream's first-window sample, with these changes:

- the window is 640×360;
- it logs key presses;
- it closes on Esc, or after 10 s;
- it prints its loop and key counts.

It uses no graphics API, so a blank window proves the lifecycle only.

```powershell
pwsh -File Experiments/Brutal/run-m1.ps1   # refuses to build when commit > 125 GB or free RAM < 8 GB
```

The script checks memory headroom, then builds once and runs once. During the run it waits for the window, sends `b`,
takes a screenshot and sends Esc. It records each check as PASS, FAIL or NOT RUN:

- the build;
- the restored packages, and their native binaries with hash and signer;
- the window opening;
- the key input;
- the clean exit;
- leftover processes;
- whether the Godot processes running beside it were left alone.

### M1 result (2026-10-19)

- **Run:** all seven checks passed. The window opened 167 ms after start, `b` and Esc arrived through `OnKey`, the app
  exited with code 0 and left no process. Peak working set was 36 MB.
- **Host:** the run budget was lowered, with the operator's agreement, to 6 GB of commit headroom and 4 GB free. The
  host stayed at 139–140 of 148.6 GB committed throughout.
- **Pause keys:** the log also shows Pause presses that nobody typed. They are a known quirk of this host (the focused
  window receives Pause about 5 times a second), not a BRUTAL event.
- **Loop rate:** about 58 iterations per second, set by `Thread.Sleep(1)` and Windows' 15.6 ms timer. It is not a frame
  rate: nothing is drawn.
- **Native binaries:** the Windows `glfw3.dll` in `Ahwoo.Brutal.Glfw` 2026.10.4 is unsigned. Its SHA-256 is in the
  evidence.
- **Preview dependency:** the restore pulls `Microsoft.Extensions.ObjectPool` 11.0.0-preview transitively.
- **Earlier runs:** an earlier run passed every check, but its screen copy caught the desktop around the window; it was
  discarded and the script now captures the window alone (`PrintWindow`). A run in between failed the key checks
  because the script started the app without a console (`-NoNewWindow`), which broke focus. That was a harness bug,
  reverted, not a BRUTAL result.
