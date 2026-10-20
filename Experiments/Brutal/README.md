# BRUTAL evaluation

A small, isolated prototype to judge whether [BRUTAL](https://github.com/ahwootech/BRUTAL), Ahwoo's C# framework with
native bindings (GLFW, Vulkan, ImGui, Jolt…), is useful for the Observatory and for composable software-factory
experiments. It is an evaluation, not a plan to replace Godot or the NVIDIA RTX fork. Coordination:
[GuitarAlchemist/gaia#257](https://github.com/GuitarAlchemist/gaia/issues/257).

## Pinned

- **Upstream:** `ahwootech/BRUTAL` `main` @ `a25cdf80b2a15530e7da6cff744def9d841ad3c3`.
- **Packages:** NuGet only, because upstream has no project files or build scripts yet. Exact versions are in the
  project files; transitive versions are in each project's `packages.lock.json`, written by the first restore.
  - `Ahwoo.Brutal.Core` 2026.10.1
  - `Ahwoo.Brutal.Glfw` 2026.10.4
  - `Ahwoo.Brutal.Vulkan` 2026.10.2, `Ahwoo.Brutal.Vulkan.Abstractions` 2026.10.2 and `Ahwoo.Brutal.ShaderC` 2026.10.2
    (M2 only)
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
| M2: a small Observatory-relevant visual slice | PASS, 11/11 checks with vsync, 2026-10-19; uncapped, it starves the RTX view | [evidence/m2-20261019-235332.md](evidence/m2-20261019-235332.md) (vsync), [evidence/m2-20261019-235218.md](evidence/m2-20261019-235218.md) (uncapped) |
| M3: measurements | INCONCLUSIVE, 2026-10-20: up to a 1,000 fps cap the view holds; uncapped, the collapse did not come back, and the view varies on its own | [evidence/m3-20261020-000131.md](evidence/m3-20261020-000131.md) (sweep), [evidence/m3-20261020-000727.md](evidence/m3-20261020-000727.md) (controls) |
| M4: report and continue/park recommendation | DONE, 2026-10-20: park for the Observatory; re-enter only for a small C# tool on GA's code | [M4](#m4-report-and-recommendation) |

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

## M2

`HallSlice` draws a slice of the Observatory's hall with Vulkan, through BRUTAL's bindings: the floor, the ring of 16
piers with their lintels, and the central pedestal with the statue, as 35 flat-shaded boxes (1,260 vertices) with a
depth buffer, at 1280×720. Its two GLSL shaders are compiled at start-up with `Ahwoo.Brutal.ShaderC`. The camera
orbits the axis by itself; the arrows turn and tilt it, W and S move it in and out. Upstream has no rendering sample, so
the instance, swapchain, render pass, pipeline and frame loop (two frames in flight) are written here, after the Vulkan
renderer in upstream's `Brutal.ImGui.Backends`. The window has a fixed size: a swapchain that goes out of date ends the
run instead of being rebuilt.

```powershell
pwsh -File Experiments/Brutal/run-m2.ps1 -Vsync -FpsLog <log of a Godot view printing FPSLOG lines>
```

The script gates on memory like M1, builds once and runs once. It waits for the window, sends Left, Up and W, captures
the window, samples the GPU each second and sends Esc after 12 s. To M1's checks it adds:

- the first frame;
- a picture that is not blank;
- the three camera keys;
- the frame times;
- given the log of a Godot view that prints `FPSLOG <local time> <fps>` each second, that view's frame rate while
  HallSlice ran.

For the runs below, the RTX view printed those lines through a temporary piece in its `live/add/` folder, removed
afterwards.

### M2 result (2026-10-19)

| | Uncapped (immediate present) | Vsync (FIFO) |
|---|---|---|
| Evidence | [m2-20261019-235218](evidence/m2-20261019-235218.md) | [m2-20261019-235332](evidence/m2-20261019-235332.md) |
| Checks | 9/11 (see below) | 11/11 |
| HallSlice frame rate | 10,059 fps | 60 fps |
| Frame time p50 / p95 / p99 / max | 0.073 / 0.187 / 0.334 / 5.0 ms | 16.64 / 16.81 / 17.01 / 46.8 ms |
| Shader compilation, first frame | 208 ms, 648 ms | 141 ms, 849 ms |
| CPU (24 logical CPUs) | 3.4 % | 0.2 % |
| Peak working set | 143 MB | 208 MB |
| Video memory, whole card | +93 MiB | +105 MiB |
| **RTX view beside it** | **fell from 60 to 2–3 fps for the whole run**, back to 60 within a second of Esc | **stayed at 60 fps** |

- **Uncapped rendering starves the RTX view.** Presenting as fast as it could, HallSlice took the Godot RTX view from
  60 fps to 2–3 fps for the 13 s it ran. With vsync, the same scene left the view at 60 fps. HallSlice had the focus
  in both runs, so the drop comes from sharing the GPU, not from the view losing focus. Any BRUTAL tool that runs
  beside the RTX view must cap its frame rate.
- **GPU load:** the whole card stayed near 30 % busy in both runs, and its power fell from 98 W to 80 W in the uncapped
  run. The card did less work, not more, which points at scheduling between the two processes rather than throughput.
  That reading is an inference, *to verify* in M3.
- **More unsigned native binaries:** `shaderc_shared.dll` and `SPIRV-Tools-shared.dll` (ShaderC 2026.10.2) and
  `VulkanEx.dll` (Vulkan 2026.10.2) are unsigned, like `glfw3.dll`. Their hashes are in the evidence.
- **The binding is thin.** The Vulkan structs, enums and calls come through nearly one to one, with C# names
  (`VkFormat.B8G8R8A8SRGB`), `ByteSize` for sizes and spans for arrays. Nothing helps with the swapchain or the
  pipeline: `Program.cs` is about 650 lines, half of it Vulkan set-up and tear-down.
- **Uncapped run, camera keys:** this check failed because of the harness, not BRUTAL. `SendKeys` sends `{LEFT}`
  without the extended-key flag, and GLFW reads it as the keypad's 4. The script now sends the arrows with their
  extended scan codes, and HallSlice logs the keys it has no action for. The vsync run passed with that fix. The
  uncapped run also predates that logging, so its binary differs from the committed one by those lines.
- **Not done from the proposal:** no recording, as the screenshots and logs carry the evidence; and keys instead of a
  mouse drag and wheel for the camera.

## M3

`HallSlice` now takes `--cap N`: it paces frames to N a second with 1 ms sleeps and a short spin. It also records frame
times from one present to the next; M2 measured from the start of a frame's work to its present. `run-m3.ps1` gates on
memory and builds once. It then runs HallSlice for 15 s per setting, 6 s apart: a vsync soak first, then each cap. For
each run it reads the RTX view's frame rate from its `FPSLOG` lines, both during the run and in the pause before. A cap
of -1 runs a control: the same window, with no HallSlice.

```powershell
pwsh -Command "& ./Experiments/Brutal/run-m3.ps1 -FpsLog <log> -Caps @(60,120,240,480,1000,0)"   # -File would pass -Caps as one string
```

### M3 result (2026-10-20): inconclusive

- **Question:** at which frame cap does HallSlice leave the RTX view at 60 fps?
- **Hypothesis, written before measuring:** the view holds with HallSlice capped at 240 fps, and collapses somewhere
  between 1,000 fps and uncapped.

Sweep, [m3-20261020-000131](evidence/m3-20261020-000131.md):

| HallSlice | Its fps | Frame ms p50 / p99 / max | View fps min / median | Whole card: GPU %, W |
|---|---|---|---|---|
| vsync, 60 s | 60 | 16.67 / 17.56 / 66.5 | 56 / 60 | 29, 100 |
| cap 60 | 60 | 16.66 / 17.23 / 19.1 | 60 / 60 | 28, 102 |
| cap 120 | 120 | 8.33 / 8.93 / 9.7 | 59 / 60 | 29, 102 |
| cap 240 | 240 | 4.17 / 4.71 / 5.2 | 60 / 60 | 30, 103 |
| cap 480 | 469 | 2.08 / 5.55 / 30.6 | 59 / 60 | 29, 104 |
| cap 1000 | 980 | 1.00 / 2.94 / 9.4 | 60 / 60 | 30, 107 |
| uncapped | 10,803 | 0.064 / 0.63 / 11.5 | 51 / 59 | 48, 115 |

Controls alternated with uncapped runs, [m3-20261020-000727](evidence/m3-20261020-000727.md):

| Start | Run | View fps min / median | Whole card: GPU %, W |
|---|---|---|---|
| 00:07:52 | control | 57 / 60 | 25, 96 |
| 00:08:13 | uncapped, 12,480 fps | 60 / 60 | 51, 117 |
| 00:08:36 | control | 60 / 60 | 28, 91 |
| 00:08:57 | uncapped, 12,052 fps | 60 / 60 | 50, 117 |
| 00:09:19 | control | 49 / 57 | 27, 89 |
| 00:09:40 | uncapped, 10,308 fps | 38 / 51 | 50, 110 |

- **Capped:** at every cap up to 1,000 fps, the view kept a median of 60 fps.
  - Its minimum of 59 at caps 120 and 480 is its counter alternating between 59 and 61. It does that with nothing
    beside it too.
  - The sweep's evidence marks those rows "drops" because that run's threshold was exactly 60. The script now allows
    2 fps (`-Tolerance`).
- **Uncapped:** across four runs, the view's minimum was 60, 60, 51 and 38. M2's fall to 2–3 fps did not come back.
- **The view varies on its own:** two of the three controls dipped too, to 57 and to 49, with nothing running beside it.
  Its scene moves (flyovers, timed events), so its load changes from one 15 s window to the next.
- **Verdict:** inconclusive.
  - The capped half agrees with the hypothesis.
  - The collapse is neither confirmed nor ruled out. M2's single observation stands, and the controls are too noisy to
    show a smaller effect.
- **Recommendation:** a BRUTAL tool next to the RTX view should still be capped. Uncapped, HallSlice took the card from
  about 27 % to 50 % busy and from about 90 W to 115 W, and nothing it draws needs more than a capped rate.
- **To settle it:** a quiet view (fixed camera, timed events off) and more alternated pairs. That changes the live
  view's state, so it is the operator's call.
- **Afterwards:** the RTX view was still running at the end of the runs. Its log ends at 00:21:53, and the machine was
  restarted afterwards (booted at 2026-10-20 00:23).

## M4: report and recommendation

**Recommendation: park BRUTAL for the Observatory.** Re-enter only for one narrow case, under conditions. The decision
is the epic owner's.

### What M0–M3 showed

| Area | Finding |
|---|---|
| Fit | Godot already gives the Observatory its scene, path tracing with DLSS, and the web build. BRUTAL is a set of bindings: everything above the API (scene, materials, lighting, export) would be ours to write. |
| Effort | Upstream has no project files, build scripts or rendering sample. A first lit scene with a depth buffer took about 650 lines, half of them Vulkan set-up and tear-down (M2). |
| Performance | Fast and small. The window opened in 167 ms (M1). The first frame came in 0.53–0.85 s, with shaders compiled in 141–208 ms. Peak working set was 141–208 MB, and CPU 0.2 % under vsync (M2, M3). |
| Coexistence | Beside the RTX view it must be capped. One uncapped run took the view from 60 to 2–3 fps (M2). Four more did not repeat that, but they doubled the card's load (M3). |
| Supply chain | Four native binaries are unsigned: `glfw3.dll`, `shaderc_shared.dll`, `SPIRV-Tools-shared.dll` and `VulkanEx.dll`. A .NET 10 project pulls `Microsoft.Extensions.ObjectPool` 11.0.0-preview transitively. At M0, each package had a single published version. |
| Licence | MIT-based, with attribution. Any distributed build must credit Ahwoo, show the unaltered BRUTAL logo before its primary function, and ship `NOTICE.txt`. |
| Web | Not evaluated. The packages used here target desktop windows (GLFW) and Vulkan, and none of them gives the Observatory a web build. |

### Why park

- **It adds work:** it replaces nothing Godot does for the Observatory, and it would add a renderer to write and
  maintain.
- **Supply chain:** the unsigned native binaries and the preview dependency would need work before anything built on it
  ships.
- **Licence:** the logo-before-start requirement applies to every distributed build, small tools included.

### When to re-enter

One case could suit it: a small native C# tool built around GA's own .NET code, where a full engine is more than the
tool needs. A fretboard or voicing visualiser, or a debug panel with upstream's ImGui bindings, are examples. Before
starting:

- build the native binaries from upstream's source, or verify them against published hashes;
- accept the licence's credit, logo and `NOTICE.txt` for anything distributed;
- pin exact versions and keep the lock files, as here;
- apply a frame cap and the memory gate whenever it runs next to the RTX view.

### Left open

- **The collapse:** whether uncapped rendering really collapses the RTX view rests on one observation (M2), which M3
  did not reproduce. Settling it needs a quiet view and more alternated pairs.
- **Upstream:** the unsigned binaries could be reported there. This evaluation does not touch other repositories.
- **This PR:** it is a draft. Merge it as a record, or close it if the prototype should not live in GA.

## Showcase: Colossus (2026-10-20)

Asked after M4: « Je veux voir ce que brutal a dans le ventre ». `Colossus/` uses more of BRUTAL at once: Vulkan, Jolt,
Dear ImGui and ImPlot. It is an original scene with no outside assets. A lone figure stands at the end of a walkway in a
city of towers. Above it, 6,000 machines circle, each one a Jolt rigid body. They gather into a colossal face, built
from Gaussian bumps, which looks at the figure and speaks, then disperses again.

Run it with `dotnet run -c Release` in `Colossus/`. It takes `--swarm N`, `--seconds T` and `--immediate` (no vsync).

**Keys:**
- Space: assemble or disperse;
- C: change the camera;
- drag: turn the camera;
- wheel: zoom;
- Esc: quit.

**Rendering.** The scene is about 1,500 lines in one file. Every object is an instance of one cube mesh, 8,753 of them in
a single draw call.
- The scene pass renders in HDR, with 4x MSAA and reversed infinite depth.
- Towers get procedural windows, which fade to their average where a cell gets smaller than two pixels.
- Machines get seams and a pulsing eye.
- A second pass adds:
  - the sky, with a low sun and bands of smog;
  - a bloom built by blitting the HDR image's mips;
  - ACES tone mapping, split toning, a vignette and grain.
- The panel then draws over it.

**Measured** on the RTX 5080, at 1600×900:

| Run | Frame time | Jolt step (6,000 bodies, 6 threads) | Other |
|---|---|---|---|
| vsync, 20 s | p50 16.69 ms, p95 18.36, p99 19.86 | p50 3.24 ms, p99 4.21 | — |
| uncapped | about 270 fps | — | GPU about 10 % busy |
| any run | — | — | peak working set 224 MB |

**Found in the packages** (Ahwoo.Brutal.Jolt 2026.10.2, win-x64):
- **Missing entry points:** 774 of the 3,246 entry points the binding declares are not exported by its `jolt.dll`. The
  DLL exports 3,674 names. This was measured by matching the assembly's P/Invoke imports against the DLL's export table.
  Three of them stopped this scene, and each has a way around it:
  - `BoxShape_Create_3`: use `BoxShapeSettings.Create` instead;
  - `SphereShape_Create_3`: use `SphereShapeSettings.Create` instead;
  - `BodyLockInterface_GetBodyStates`: read each body through `BodyInterface` instead.
- **Abort in the temp allocator:** `TempAllocatorImpl.Create(64 MB)` aborts the process with 0xC0000409 in the first
  `PhysicsSystem.Update` that has moving bodies. The same scene runs with `TempAllocatorMalloc`, or with
  `TempAllocatorImplWithMallocFallback` of the same size. It also runs with no moving bodies. So the allocator seems to
  hold far less than it was asked for. The thread pool and the static bodies were each ruled out.
- **Uneven frames under vsync:** with three swapchain images, the frames came back unevenly under vsync, at about 5 ms
  then 28 ms, although each was shown for one refresh. Two images and a smoothed time step made the motion even.

The licence's logo and `NOTICE.txt` requirements apply to distributed builds. This showcase is a local experiment and is
not distributed.