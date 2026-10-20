# Combat + Ingredient Collection Prototype

The main scene now includes the first Food Cannon / Customer purification loop. See [CANNON_README.md](CANNON_README.md) for controls, setup and validation.

CombatCollection now includes the centered Food Truck and CookingStation, and is the default startup scene. The dedicated cooking test is also available in `Assets/Prototype/CookingCollection.unity`. See [COOKING_README.md](COOKING_README.md) for station placement, right-click cooking and retrieval.

All gameplay numbers and key bindings are now edited through the **Global Data** object in either scene. Both scenes and their gameplay prefabs reference the same `Assets/Prototype/GlobalGameSettings.asset`. See [GLOBAL_DATA_README.md](GLOBAL_DATA_README.md) for live tuning and runtime player state.

## Open and play

Open `Assets/Prototype/CombatCollection.unity`, enter Play Mode, and click the Game view.
If the scene is absent, wait for Unity to finish importing scripts; the editor builder creates it automatically. You can also run **MMFeast > Create Combat Collection Prototype**. Existing prototype assets are not overwritten.

- WASD / arrow keys: move. The yellow marker shows the last movement direction and melee facing.
- Left mouse button: attack in a 120-degree cone in front of the player, within 3 world units by default.
- E: pick up the nearest Meat within 1.5 world units. Pressing E while holding Meat logs a rejected pickup.
- Restart Play Mode to clear the held item. Dropping items is outside this prototype's scope.

## Scripts created

All gameplay scripts are in `Assets/Scripts/Prototype/`:

| Script | Responsibility |
| --- | --- |
| PlayerController.cs | Rigidbody2D movement, facing, independent runtime stats and private clones of shared input configurations |
| PlayerCombat.cs | Attack damage, range, cooldown, front-facing checks, one hit per enemy per attack |
| EnemyHealth.cs | HP, brief white hit flash, guarded death, exactly one Meat drop |
| EnemySimpleAI.cs | Move toward the configured player, stop nearby, no attacks |
| EnemySpawner.cs | Spawn at configured points, track spawned living enemies and enforce the cap |
| IngredientPickup.cs | Mark an item carried, disable physics and attach it to the CarryPoint |
| PlayerCarry.cs | Find nearest Meat and carry only one item |

`Assets/Editor/CombatPrototypeBuilder.cs` creates the scene, square placeholder sprite, and two prefabs. No existing gameplay scripts, original scenes, input bindings, package configuration or project settings were modified.

The original project contained a 2D template scene with a camera and global light, an Input Actions asset, and a welcome script. There was no existing player movement, combat, enemy, spawner, carry system or gameplay prefab to extend.

## GameObjects and components

The generated scene and prefabs already have all required references assigned. **No manual Inspector assignments are required for the generated scene.**

| Object | Components / configuration |
| --- | --- |
| Player | Rigidbody2D (zero gravity, frozen rotation), BoxCollider2D, PlayerController, PlayerCombat, PlayerCarry; Visual, FacingIndicator and CarryPoint children |
| Enemy prefab | SpriteRenderer, Rigidbody2D (zero gravity, frozen rotation), BoxCollider2D, EnemyHealth, EnemySimpleAI |
| Meat prefab | SpriteRenderer, Rigidbody2D (zero gravity), trigger BoxCollider2D, IngredientPickup |
| EnemySpawner | EnemySpawner and four SpawnPoint children |
| Main Camera | Orthographic Camera, fixed view centered on the test area |

If you use these components in another scene, assign:

Assign the shared `GlobalGameSettings.asset` to the Settings reference on each gameplay component. Missing settings produce an error and disable the affected component. Keep scene references assigned as follows:

1. **PlayerController**: Facing Indicator = optional yellow marker transform.
2. **PlayerCombat**: Uses the shared combat settings.
3. **PlayerCarry**: Carry Point = a player child transform; leave Current Held Item empty initially.
4. **EnemyHealth**: Ingredient Prefab = `Meat.prefab`. Instance HP initializes from the shared Monster HP.
5. **EnemySimpleAI**: The spawner assigns Target; manually placed enemies need their Target assigned to Player.
6. **EnemySpawner**: Enemy Prefab = `Enemy.prefab`, Player = player transform, Spawn Points = the four configured transforms.

Select Global Data to edit Move, Attack, Interact, Aim and Dash with the Input System binding editor. Original movement bindings were copied into the shared asset. Other defaults are left mouse, E, right mouse and left Shift. Aim also controls cooking; hold to aim and release to fire. Dash exposes configuration and input only. Shared settings edits, including Play Mode edits, are saved to the asset.

## Full-loop test

1. Open the generated scene, press Play, focus the Game view and open Console.
2. Red enemies spawn immediately, then every three seconds, with at most five living enemies from this spawner.
3. Enemies approach the blue player and stop close by. They do not attack.
4. Use WASD to face an enemy; the yellow marker must point toward it.
5. Click the left mouse button with the enemy inside melee range. Console logs the hit and remaining HP, and the enemy briefly flashes white.
6. Click five times with at least 0.1 seconds between attacks. HP goes 50 → 40 → 30 → 20 → 10 → 0.
7. The enemy disappears and leaves one pink Meat at its death position. Console logs death and ingredient spawn.
8. Move within pickup range and press E. Meat attaches above the player and travels with them. Console logs the pickup.
9. Kill another enemy and press E near its Meat. The second Meat stays on the ground; Console logs that the player is already holding something.
10. Check that enemies behind the player or outside range take no damage, rapid clicks respect cooldown, and the spawner stays within its living-enemy cap.

No cooking, recipes, player health, customer purification, SAN, truck systems, upgrades, extra weapons or inventory slots are included.

## Validation performed

Unity 6000.6.4f1 compiled these scripts in an isolated project using the same Input System package. Seventeen automated Play Mode integration checks passed, including simulated WASD, left mouse and E inputs, spawn cap, pursuit, attack direction/range/cooldown, hit flash, death, one-drop protection and one-item carry. See `PROTOTYPE_VALIDATION.txt` for results. The delivered runtime source and script GUIDs match the tested copies. The headless test did not visually verify the Game view in this project's URP renderer; use the full-loop test above for that final visual check.

