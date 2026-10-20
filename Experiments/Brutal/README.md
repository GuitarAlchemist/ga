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
| M1: one window, input, clean close | NOT RUN: blocked on host memory headroom | `run-m1.ps1` writes `evidence/m1-<stamp>.md` |
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
