# Planet Shooter

A small Unity prototype built to test [Unity's official MCP server](https://unity.com/blog/unity-ai-mcp-how-to-get-started)
with [Claude Code](https://claude.com/claude-code). The game is a straight-up *Geometry Wars*
mock-up played on the surface of a sphere. The game itself isn't the point. It exists only as
something with enough moving parts to see how far the AI-driven editor loop can go.

## Why it exists

The aim was to answer one question: can Claude Code drive a real Unity project through MCP, or
does it fall over the moment you leave "hello world"?

Copying a known design (*Geometry Wars*) removed the question of what to build and left only the
question that mattered: how much of the work the MCP loop could carry. That covers gameplay
scripting, procedural meshes, particle effects, HDR materials, post-processing, and scene
wiring, all authored and tuned live in the editor over MCP, driven from the CLI rather than by
clicking around the inspector.

## The game, briefly

A ship crawls across the surface of a planet; the camera follows so the ship stays centred. Red
cubes wander the surface like asteroids and take several hits to kill. Pyramid "chasers" spawn
in escalating waves, telegraph their arrival, then home in on the ship. Everything glows:
bullets, tracers, and explosions are HDR and bloom against a neon grid.

That's the whole game. It's derivative on purpose and demo-grade throughout. None of it is the
interesting part.

### Controls

| Action | Keyboard / Mouse | Gamepad |
| --- | --- | --- |
| Move | `WASD` | Left stick |
| Aim | Mouse | Right stick |
| Fire | Hold left mouse | Right trigger / bumper / right-stick deflect (auto-fire) |

## What the MCP loop handled

The parts expected to be painful mostly weren't. A few stood out.

**Procedural geometry held up well.** The diamond bullets, the pyramid chasers, and the lat/long
grid are built in code rather than modelled. That suits the loop: Claude writes a mesh
generator, it runs, the result shows in the scene, the values get adjusted. No round-trip
through an art tool.

**The neon look came together over MCP.** HDR emissive materials and the particle systems
(additive sparks, velocity-stretched tracers, soft radial spawn glows) were code-generated and
tuned against URP's global Bloom. Live tuning in the editor while talking to the harness is
where the MCP integration earned its keep: change a value, see it, iterate.

**The sphere math is the one genuinely non-trivial bit.** The planet sits static at the origin
and everything (ship, enemies, bullets) moves over the same sphere via great-circle math, so
stock world-space effects like `TrailRenderer` work for free. The camera rig parallel-transports
its up-vector to avoid roll and pole flips. This took the most back-and-forth and is the
clearest case of the loop handling real logic rather than boilerplate.

**Collision is deliberately cheap:** a static `Enemies` registry plus distance tests, no physics
queries. The kind of shortcut a prototype takes, and one the loop is happy to take when told to.

## What it didn't handle

The MCP loop is good at producing and adjusting things, less good at seeing them. Anything that
came down to "does this look right" still needed a human in front of the Game view. Bloom only
shows in the Game view (and in the Scene view with post-processing toggled on), so visual
judgement stayed manual: the harness can change the numbers but can't tell whether the result
feels good. That's the honest boundary of this kind of work right now. It authors and wires
confidently, but taste stays with the developer.

## Tech

- **Unity 6** (`6000.5.0f1`)
- **Universal Render Pipeline** 17.5 (global Bloom, HDR emissive materials)
- **Input System** 1.19 (device polling: gamepad takes priority, else keyboard + mouse)

The MCP integration uses Unity's own MCP server, which ships inside the AI Assistant package
(`com.unity.ai.assistant`) and requires Unity 6 or newer. Note that it is gated behind a paid
Unity subscription: Pro, Enterprise, and Industry plans include it, and Personal users can
access it through a trial that converts to a monthly subscription. Using the MCP server does not
consume Unity AI credits.

### Scripts (`Assets/Scripts/`)

| Script | Role |
| --- | --- |
| `ShipController` | Surface-crawler movement + aim/fire input |
| `ShipWeapon` | Auto-fires bullets; builds the bullet mesh/material/trail |
| `Bullet` | Great-circle projectile with arc-limited lifetime + hidden-hemisphere culling |
| `CameraRig` | Ship-following camera with parallel-transported up |
| `Enemy` | Wandering, tumbling red-cube enemy (multi-hit) |
| `Chaser` | One-hit pyramid that pursues the ship |
| `Enemies` | `IEnemy` interface + global live-enemy registry |
| `EnemySpawner` | Central tuning for cube respawns and chaser waves |
| `ExplosionFX` | Code-built death-burst / hit-spark particle systems |
| `HitFlash` | Per-renderer "I got hit" flash via `MaterialPropertyBlock` |
| `GridGlow` | Inspector knob for the planet grid's bloom intensity |
| `SpawnGlow` / `SpawnWarning` | Two spawn-telegraph effects |

Most gameplay and aesthetic values are exposed in the Inspector (move/turn speed, fire rate,
bullet speed/colour/damage, enemy health/speed, wave sizes and timing, grid glow, spawn
telegraph colours), so the feel can be tuned live, including over MCP.

## Running it

1. Open the project in **Unity 6 (6000.5.0f1)** or newer with URP.
2. Open `Assets/Scenes/Main.unity`.
3. Press **Play**.

## Notes

As a test, it answered the question. Claude Code over the Unity MCP server can carry a project
well past "hello world": real scripting, procedural art, particle work, and live tuning, not
just file edits. Where it still needs a human is judgement. The loop builds and adjusts fast,
but it can't yet look at the screen and tell whether the game feels good. For a throwaway
prototype that's a fair trade, and a useful read on where the AI-driven editor loop stands
right now.
