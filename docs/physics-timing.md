# Physics timing and repeatability

Notes from the October 2026 investigation. Leave the current physics behavior as
it is unless we decide the remaining variation warrants a change. Responsive
controls and smooth movement are important; avoid adding visual or input delay
without checking how it feels in the game.

## What was observed

- Repeated straight passes through one booster, while continuously holding the
  interaction input, showed about **0.07** difference in the displayed final
  speed. The difference remained when targeting 50 FPS. Quick Launch was off.
- A puller also felt slow or jittery after the first redirector implementation.
  That implementation incorrectly mixed fixed-update handling with the project's
  per-frame physics. The original direct force behavior was restored, then the
  force queue was changed to run before each per-frame physics simulation.
- The exact cause and contribution of each possible factor in the player's
  booster test have not been measured. Do not assume that the frame-rate target,
  the displayed value, or one specific code change explains the full 0.07.

## Current behavior and settings

- `ProjectSettings/Physics2DSettings.asset` uses **Update** simulation mode:
  2D physics runs after `Update` each rendered frame. Frame durations can vary.
  The `0.02` fixed timestep in `TimeManager.asset` does **not** make this 2D
  simulation run at 50 Hz. See [Unity's 2D Update mode](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SimulationMode2D.Update.html).
- In `Player.cs`, pullers, pushers, and boosters queue force vectors during
  trigger callbacks. Their sum is applied before the next 2D simulation. A
  redirector turns the existing momentum and the combined interaction force;
  equal opposing forces cancel before their direction is changed. Booster
  effects use `OnTriggerStay2D`, so entering and leaving an area are resolved
  on simulation boundaries rather than at an exact fraction of a frame.
- Retry clears both the interaction-force queue and `Rigidbody2D.totalForce`
  as well as velocity. This was added because clearing velocity alone does not
  clear force already waiting for a simulation step. The speed display now
  updates in `LateUpdate`, after the frame's physics simulation.
- Retry moves the player's `Transform`; 2D `Auto Sync Transforms` is off. A
  controlled check showed that an immediate Rigidbody position read can still
  show the old position until transforms are synchronized. Physics normally
  synchronizes before simulation. No connection to the reported 0.07 has been
  established; check this if adding immediate physics queries during retry.
- `GameManager` sets `Application.targetFrameRate` every `Update`. A target is
  not a guarantee of identical frame durations. On desktop and Web, enabled
  VSync can override the target; Android and iOS handle VSync differently. The
  saved Main scene had a `FramerateLimit` override of 1000 during this
  investigation, but runtime or unsaved Inspector values may differ. Check the
  actual active settings and frame times rather than assuming the target is met.
  See [Unity's VSync documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/QualitySettings-vSyncCount.html).

## What was tested

- In an isolated Windows Unity 6000.0.62f1 play-mode test, a player traveled
  straight through a booster with interaction held. Script-controlled 2D
  simulation steps of 16.667 ms produced a final speed of **22.500** in each of
  two runs. Alternating 10 ms and 23.333 ms steps, with the same average,
  produced **22.833** in each of two runs. These are synthetic frame patterns,
  not a recording of the player's level or proof of its 0.07 difference. This
  test preceded the current combined-force queue and should be repeated if we
  revisit the issue.
- A controlled retry-state test showed that a force of 50 remained queued after
  setting velocity to zero and produced about 0.833 speed in a 1/60-second step
  with mass 1. Retry now explicitly clears the force; this only applies when a
  force was actually queued at retry. The player's reported booster test did
  not use Quick Launch.
- The first redirector implementation used `FixedUpdate` and `fixedDeltaTime`
  despite Update-mode physics. An isolated check demonstrated a mismatch when
  the next simulation step was shorter. The redirector was moved into `Update`
  and uses the frame's `deltaTime`. A later play-mode test in actual Update
  mode checked overlapping booster/redirector forces at roughly 30 and 120 FPS,
  as well as retry and Quick Launch. Subsequent tests checked force cancellation,
  single and perpendicular boosters, normal forces without a redirector, and
  retry. These were focused checks, not proof that all real levels are identical.
- An earlier old-versus-new puller comparison forced scripted fixed-size physics
  steps; it did **not** reproduce the project's Update simulation mode. Avoid
  using that comparison alone to assess per-frame behavior or visual jitter.

## If we revisit this

1. Record the start position, velocity, aim, queued force, active input,
   simulation mode, effective frame-rate/VSync settings, actual frame durations,
   trigger entry/stay/exit, and final velocity after queued force is applied.
   Buffer samples and output once per run so per-frame logging does not distort
   timing. Repeat the same setup, including the user's 50 FPS test.
2. Compare actual Update-mode runs with an isolated fixed-step experiment using
   identical initial world state and inputs tied to simulation steps. Test the
   current force queue, retry, overlapping objects, and the same real level.
   A fixed timestep can improve repeatability but does not by itself guarantee
   identical inputs, reset state, or bit-for-bit results across platforms.
3. If trying fixed physics, measure responsiveness and CPU/battery cost on
   lower-end Android as well as Windows and WebGL. At 120 Hz the physics step is
   about 8.3 ms; a 60 FPS display can run roughly two steps per frame, while a
   30 FPS display can run roughly four. Input and presentation still run at the
   frame rate. Heavy load or long stalls can hit the project's 0.333-second
   Maximum Allowed Timestep and make game time fall behind real time.
4. The player Rigidbody currently has interpolation set to None. Interpolation
   can smooth rendering between fixed steps but visually lags by roughly a step;
   extrapolation predicts ahead but can overshoot and correct at collisions.
   Neither should be adopted without checking input feel and camera following.
   See [Unity's Rigidbody2D interpolation documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D-interpolation.html).
5. If per-frame physics is retained, investigate booster entry/exit handling
   and whether partial-step accounting is worthwhile. Such a change can alter
   gameplay and needs deliberate design and tests; a 50 FPS cap alone is not a
   fixed simulation timestep.
