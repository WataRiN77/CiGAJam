# SceneBState JSON Data Flow

This document describes the current local JSON data flow between the face-customization scene and the shooting/observer scenes.

## Goal

The project currently uses a shared local JSON file as the temporary synchronization layer between two players/scenes.

- The main menu online flow is responsible for creating the authoritative NPC seeds and murderer seed.
- Scene A consumes those seeds to create the answer face and initial JSON.
- Scene B reads that data, spawns the playable NPC scene, then writes runtime state changes.
- Scene A can also read the same JSON to mirror Scene B: terrain, NPCs, tracking target, shots, marks, and end state.
- In the future, online synchronization can replace or update this JSON file, while keeping the same data shape.

Current JSON path:

```text
C:\CiGAJam\SceneBState.json
```

## Main Scripts

### `MainMenuController`

Online room and match-start owner.

When the Photon room has two players and the countdown finishes, the master client generates:

- `npcSeeds`
- `murdererSeed`
- `mapNumber`

Then it calls `AsymmetricSyncManager.BroadcastSeeds(...)`.

These values are the authoritative gameplay setup for the round.

### `AsymmetricSyncManager`

Online data bridge.

Responsibilities:

- Broadcast authoritative round seeds from the master client to all clients.
- Broadcast the authoritative `mapNumber` from the master client to all clients.
- Cache received seeds in `SyncedNpcSeeds` and `SyncedMurdererSeed`.
- Cache received map number in `SyncedMapNumber`.
- Write received seeds into `SeededNpcSpawnManager.PendingNpcSeeds` and `PendingMurdererSeed`.
- Send A player's full face JSON to B.
- Send B scene state JSON back to A.

### `FaceCustomizationGameManager`

Scene A answer/setup owner.

When `GenerateNewRound()` runs:

1. First tries to read authoritative seeds from `AsymmetricSyncManager`.
2. If unavailable, falls back to `SeededNpcSpawnManager.PendingNpcSeeds`.
3. If synced seeds exist, uses `murdererSeed` to generate the answer face.
4. Uses the non-murderer `npcSeeds` to generate distractor faces.
5. Uses the synced `mapNumber` if one exists.
6. Calls `SceneBStateJsonSaver.WriteInitialConfigFromFaceManager(...)`.
7. Writes the initial `SceneBState.json`.

In online mode, it should not invent a new murderer. It should consume the seed created by `MainMenuController`.

If no synced seeds exist, it falls back to the old local random generation path for single-player testing.

### `SceneBStateJsonSaver`

JSON read/write owner.

Default save path:

```text
C:\CiGAJam\SceneBState.json
```

Responsibilities:

- Create initial JSON data from seeds and map number.
- Cache NPC runtime states.
- Save runtime changes such as tracking, marking, shooting, shot count, and game result.
- Provide `TryGetInitialJsonForSpawn(...)` so Scene B can spawn from JSON.

### `SeededNpcSpawnManager`

NPC spawning owner.

Responsibilities:

- Spawn NPCs from `npcSeeds`.
- Mark one NPC as murderer by comparing each seed with `murdererSeed`.
- Generate deterministic initial positions from each seed.
- Ensure the same seed produces the same face, body, movement, and initial point when the setup is consistent.

Scene B should generally enable `Use SceneB Json Seeds On Start` so it waits for the JSON written by Scene A.

### `BinASceneJsonDriver`

Scene A mirror/observer driver.

Responsibilities:

- Read `SceneBState.json`.
- Open the matching terrain from `mapNumber`.
- Spawn mirrored NPCs from `npcSeeds` and `murdererSeed`.
- Apply each NPC state:
  - `isTracked`
  - `isShot`
  - `isMarked`
- Move the output camera.
- If an NPC is tracked, the output camera follows that NPC's `shotpoint` child.
- If no NPC is tracked, the output camera returns to its fixed original transform.
- If a tracked NPC is shot, the camera exits follow mode.
- On shot, plays local mirrored feedback: death, blood FX, shotpoint children, slow motion, camera shake, and RawImage shake.

### `BinAJsonNpcStateTestController`

Temporary local test controller before online sync exists.

It directly edits `C:\CiGAJam\SceneBState.json` during Play Mode.

Default keys:

- `Tab`: select next NPC seed from JSON
- `F`: set selected NPC as tracked
- `C`: clear tracking
- `M`: toggle selected NPC mark
- `X`: toggle selected NPC shot
- `R`: refill selected seed from JSON

It can fill `selectedSeed` from JSON using:

- first NPC
- random NPC
- murderer
- first non-murderer

## Runtime Flow

### New Round Initialization

1. `MainMenuController` finishes the online countdown.
2. The master client generates `npcSeeds`, `murdererSeed`, and `mapNumber`.
3. `AsymmetricSyncManager.BroadcastSeeds(...)` sends those values to all clients.
4. Each client caches the seeds in `SeededNpcSpawnManager.PendingNpcSeeds` and caches the map in `SyncedMapNumber`.
5. Scene A calls `FaceCustomizationGameManager.GenerateNewRound()`.
6. Scene A uses the synced `murdererSeed` as the answer face seed.
7. Scene A writes `SceneBState.json` using the synced `npcSeeds`, `murdererSeed`, and `mapNumber`.
8. Scene B uses the synced `PendingNpcSeeds` to spawn NPCs and the synced `mapNumber` to open the same terrain.
9. Both scenes can now identify each NPC by seed.

### Scene B Runtime Updates

Scene B changes the JSON when gameplay happens:

- Focus/tracking changes `isTracked`.
- Marking a suspicious NPC changes `isMarked`.
- Shooting an NPC changes `isShot`.
- Shooting increments `shotsFired`.
- Game end changes `gameEnded`.
- Success/failure changes `gameSucceeded`.

### Scene A Mirror Updates

Scene A repeatedly checks whether the JSON file changed.

When changed:

1. Reads the JSON.
2. Applies terrain.
3. Spawns NPCs if needed.
4. Matches scene NPCs by `seed`.
5. Applies runtime state.
6. Updates the output camera.

## JSON Structure

Example:

```json
{
  "mapNumber": 1,
  "npcSeeds": [785365690, 1303489403],
  "murdererSeed": 785365690,
  "shotsFired": 0,
  "gameEnded": false,
  "gameSucceeded": false,
  "npcs": [
    {
      "seed": 785365690,
      "positionSeed": 785365690,
      "initialPosition": { "x": -105.34, "y": -5.19, "z": -6.38 },
      "isMurderer": true,
      "isTracked": false,
      "isShot": false,
      "isMarked": false
    }
  ]
}
```

## Top-Level Fields

### `mapNumber`

The selected terrain/map number.

Meaning:

- `1` means terrain option 1.
- `2` means terrain option 2.
- etc.

Both Scene A and Scene B should use this number to enable the same terrain. In online mode this value is generated by `MainMenuController`, not by Scene A.

### `npcSeeds`

The full list of NPC seeds for this round.

Each seed controls deterministic generation for one NPC, including:

- face
- body/clothing
- movement seed
- spawn position seed

The list should include the murderer seed.

### `murdererSeed`

The seed of the murderer NPC.

The NPC whose `seed == murdererSeed` should:

- receive the `Murderer` tag in Scene B
- have `isMurderer = true` in the JSON NPC state

### `shotsFired`

How many shots have been fired in the current game.

Used by game result logic, especially the failure condition when all chances are used.

### `gameEnded`

Whether the round has ended.

- `false`: game still running
- `true`: game finished

### `gameSucceeded`

Whether the finished game is a success.

Only meaningful when `gameEnded == true`.

- `true`: murderer was killed in time
- `false`: failed, such as timeout or all shots used without killing murderer

## NPC Fields

Each item inside `npcs` describes one NPC.

### `seed`

Primary identity of the NPC.

This is the most important cross-scene key. Scene A and Scene B match the same character by this value.

### `positionSeed`

Seed used for the initial spawn point.

Currently this is the same as `seed`, because the project was changed to avoid secondary seed processing for movement/position logic.

### `initialPosition`

The deterministic spawn position generated from the seed.

Fields:

- `x`
- `y`
- `z`

Scene A can snap mirrored NPCs to these positions so both scenes begin from the same layout.

### `isMurderer`

Whether this NPC is the murderer.

This should be true only for the NPC whose `seed == murdererSeed`.

### `isTracked`

Whether this NPC is currently being followed/focused.

Effects:

- Scene A output camera follows this NPC's `shotpoint` child.
- Scene A output camera orthographic size changes to the tracked size.
- If no NPC is tracked, Scene A output camera returns to its fixed original position and size.
- If the tracked NPC is shot, Scene A exits follow mode automatically.

Only one NPC should normally have `isTracked = true`.

### `isShot`

Whether this NPC has been shot/killed.

Effects:

- NPC stops normal wandering and enters dead/free-fall behavior.
- `shotpoint` children are activated.
- `blood` FX is played.
- Scene A mirror triggers slow motion, output camera shake, and RawImage shake the first time it reads this transition.
- Shot NPCs are not used as follow targets.

### `isMarked`

Whether this NPC is marked as suspicious/pending.

Effects:

- The NPC's `arrows` child is shown or hidden.
- Scene A mirror applies the same arrow state.

## Expected Visual Effects

### Tracking

When an NPC is tracked:

- Scene A output camera follows `npc/shotpoint`.
- Output camera size moves toward `1`.
- Camera keeps following until JSON clears tracking or the NPC becomes shot.

### No Tracking

When no NPC has `isTracked = true`:

- Scene A output camera returns to its stored fixed position.
- Output camera size returns to its stored fixed size.

### Shooting / Death

When an NPC becomes shot:

- The character dies locally.
- The `blood` child effect plays.
- The `shotpoint` children become active.
- Time briefly slows.
- Output camera shakes.
- The RawImage showing the output camera feed shakes.
- If this NPC was tracked, Scene A exits follow mode.

## Current Local Testing

Use `BinAJsonNpcStateTestController` in Play Mode.

Recommended setup:

1. Make sure `C:\CiGAJam\SceneBState.json` exists.
2. Add `BinAJsonNpcStateTestController` to a test GameObject.
3. Set `Seed Fill Mode` to `RandomNpc`, `Murderer`, or another desired option.
4. Press `F` to follow the selected NPC.
5. Press `X` to mark the selected NPC as shot.
6. Watch Scene A mirror camera and RawImage update from JSON.

## Notes for Future Online Sync

The JSON file is currently acting as a local stand-in for network data.

Future networking should preserve the same logical fields:

- map number
- NPC seeds
- murderer seed
- per-NPC state
- shots fired
- game ended
- game succeeded

The network layer can either:

- write the same JSON file, or
- bypass the file and feed equivalent data objects into the same scene drivers.

The important rule is: all cross-scene identity and state should continue to use `seed` as the stable NPC key.
