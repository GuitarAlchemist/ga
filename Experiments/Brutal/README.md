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