# Runtime spike — 2026-11-04

What Husnisse's design assumes, tested against the real runtime rather than argued from the docs.

**Environment:** rig tree `V:\ValheimTest` (port 2556, world `MTTest`), `build_parity: MATCH
E5E761C58099`, BepInEx 5.4.23.6, no game client attached. Probe: `probe/NisseProbe.dll`
(`artifacts/NisseProbe.dll`), three boots, probe removed from the tree afterwards.

## 1. Where chest contents live — PASS

Contents are in the chest's ZDO, not in a side channel, and they are replicated.

- `Container.Save()` packs the whole `Inventory` into a `ZPackage` and writes it to the ZDO key
  `items` as a byte array; `Container.Load()` re-reads it whenever `DataRevision` changes, and
  `CheckForChanges()` polls that once a second **on clients too**.
- `ZDO.Serialize()` writes every stored byte array with no per-peer filtering, and
  `ZDOMan.SendZDOs` sends each ZDO to any peer whose `ShouldSend` covers its sector. There is no
  "owner-only field" concept for container data.
- Runtime: a freshly spawned `piece_chest_wood` had `zdos items = 0 bytes`; adding 50 wood made it
  `21 bytes`, readable through `ZNetView.GetZDO().GetByteArray("items")` without opening anything.

**Consequence:** QI's `Count` / `CanAfford` can be answered from the claim client-side with a plain
scan of instantiated `Container` objects. **No custom server→client snapshot layer is needed at
0.3.** The earlier concern that craft-from-claim needed a networking design was wrong; this is the
cheap path, and the contract's "data-source swap" sentence stands.

Residual: confirm on a real client join that a chest in range but out of line of sight is counted.
That is one join, not a design question.

## 2. Are player-built pieces pathable? — YES as geometry, no runtime proof yet

- `Pathfinding.m_layers = 0x01908C01` → `Default(0)`, **`piece(10)`**, `terrain(11)`,
  `static_solid(15)`, `Default_small(20)`, `blocker(23)`, `pathblocker(24)`; water `0x10`.
- `BuildTile` calls `NavMeshBuilder.CollectSources(bounds, m_layers, PhysicsColliders, ...)`, so
  wood floors and walls on layer 10 are baked into the navmesh exactly like terrain.
- Humanoid agent build: height 1.8 m, radius 0.4 m, climb 0.3 m, slope 85°. A 2 m door is
  comfortably wide; any step taller than 0.3 m is not walkable without stairs or a ramp.

**The constraint that matters more than the answer:** tiles are 32 m and built **lazily**.
`GetPath` pokes the from/to tiles plus their neighbours and then immediately snaps to the navmesh —
so the *first* request in a fresh tile returns `false` while `Buildtiles` waits out
`m_updateInterval` (measured: 10 s). Any nisse AI must treat "no path" as retry-later, and must not
spam path requests, because every request re-pokes the tile and postpones its rebuild.

## 3. Indoor pathing — RESOLVED (client probe)

The dedicated server could not answer this. With a player connected it still reported the player's
zone unloaded after 180 s, and raycasts found no terrain; `ZoneSystem.Update` returns before zone
creation on this world. The probe was therefore run **on the client** (`mode=client`), where zones
always load.

Run: player at `(-37.13, 40.05, -10.85)`, standing on a `WaterVolume` collider at y = 40. The probe
built a 3×3 wood-floor room with a 2 m doorway gap plus a four-tile walkway, then pathed from the
walkway's far end (z −10.5) to the room centre.

```
navsample[house_inside] found=True dist=0.05
solid[house_inside]     collider=collider layer=piece y=40.15
path[poll0..2]  have=True  partial=True/6  partial_end_h=0.00  full=True/6  full_end_h=0.00
path[poll3..9]  have=False partial=True/2  partial_end_h=5.42  full=False/0
```

- `found=True dist=0.05` on layer `piece`: **built floors are navmesh**, confirmed on the built
  geometry rather than from the mask alone.
- Polls 0–2 report a clean path, but that is the tile from *before* the walls were baked; polls 3–9
  are the rebuilt tile, and they stop 5.42 m short of centre — i.e. at the doorway.

**Conclusion: floors are walkable, a 2 m doorway is not.** A 0.4 m agent radius plus wall posts
leaves no corridor the navmesh builder will bridge. Husnisse must not design walking around crossing
player doorways: walking is room-scale and outdoor, and a threshold fades. Re-test with a 4 m
opening if walking through doorways ever becomes a requirement.

## What this means for the plan

| Assumption | Status |
|---|---|
| Client can count claim storage without opening chests | **proven** |
| Player-built floors become navmesh | **proven** (mask, plus a live path endpoint on layer `piece`) |
| A creature can walk inside a player base | **partial** — floors yes, 2 m doorway no |
| Pathing is free | **false** — 10 s lazy tile build, first query in a new tile loses the race |
| 0.3 needs a new sync layer | **false** — ZDO already carries it |

## Probe lessons (for the next spike)

- The dedicated rig never loads terrain zones, even with a client connected. Client-side is the only
  reliable place for navmesh tests.
- An unspawned character sits at the `(1000000, 1000000)` sentinel; reject implausible positions or
  the probe builds its test room a million metres away.
- A navmesh result taken during the first 10 s after geometry changes is stale — the tile rebuild has
  not happened yet. Poll for at least 30 s and trust the later polls.

## Libraries

Checked against the actual assemblies rather than reputation:

- **Jotunn 2.30.2** ships `CreatureManager` + `CustomCreature` (ctor takes a prefab clone and a
  `CreatureConfig`, supports `Spawns`), plus `CustomPiece`, `CustomRPC`, `PrefabManager`,
  `LocalizationManager`. That is the whole "no manual patches" ask for the nisse and the bowl, and
  it is already in the rig's plugin set.
- **Newtonsoft.Json** is already inside `valheim_Data/Managed`; referencing it costs no extra DLL.
- **ConditionalConfigSync** is the right answer for host-owned config: the design already says
  world-state config is server-authoritative, and this is that mechanism rather than a custom RPC.
- **YamlDotNet** is the only genuinely new DLL to ship, and only if config/data files are YAML.

None of the four solve the two hard problems (pathing, and the creature feeling good); they remove
boilerplate around them, which is the correct reason to use them.

## Reproduce

```bash
cd probe && ./build.sh                       # or: dotnet build NisseProbe.csproj -c Release
cd ../testrig && ./rig.sh down && ./rig.sh deploy ../nisse/probe/artifacts/NisseProbe.dll NisseProbe
./rig.sh up && sleep 55 && ./rig.sh tail 900 | grep 'probe:'
./rig.sh down                                # then remove BepInEx/plugins/NisseProbe
```
