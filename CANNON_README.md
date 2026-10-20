# Food Cannon / Purification prototype

Open `Assets/Prototype/CombatCollection.unity`, the default startup scene. `CookingCollection.unity` has the same cannon/customer setup. If a scene was already open during the update, stop Play Mode and reopen it from the Project panel so Unity loads the saved hierarchy.

The truck remains centered. Player starts outside at (0,-4), CookingStation is at (0,-1.2), and FoodCannon is mounted inside the truck at (0,1.2). All scene references are assigned; **no manual setup is required in the supplied scenes**.

## Files

Created gameplay scripts in `Assets/Scripts/Prototype/`:

| File | Responsibility |
| --- | --- |
| Customer.cs | IsPurified, green feedback, disable colliders, remove after one second; no combat HP or Meat drop |
| FoodCannon.cs | One cooked-food shot, access/range checks, held aiming, target/range clamp, barrel rotation, trajectory and marker, release firing |
| FoodProjectile.cs | Rigidbody gravity, ground collision, one impact, 2D AoE, Zombie/Customer resolution, temporary splash ring |
| FoodBallistics.cs | Shared flight-time/velocity/position math and shared display projection for preview and projectile |

Also created `Assets/Editor/FoodCannonPrototypeBuilder.cs`, `Customer.prefab`, `FoodCannon.prefab`, `FoodProjectile.prefab`, `FoodTrajectory.mat` in `Assets/Prototype/Prefabs/`, their Unity `.meta` files, this guide and `CANNON_VALIDATION.txt`.

Modified gameplay scripts:

- EnemyHealth.cs: the existing normal enemy is a Zombie; melee and cannon kills call the same guarded Die()/Meat-drop path. KillByFoodProjectile() kills immediately.
- EnemySpawner.cs: the same spawner alternates Zombies and Customers; the existing maximumAliveEnemies cap counts both. Customer Every Nth Spawn defaults to 2, so the sequence is Zombie, Customer, Zombie, Customer.
- PlayerController.cs: exposes held/released state from its private clone of the shared Aim action, mouse screen position and IsCannonAiming. Configure all keys through Global Data; Aim also controls cooking.
- PlayerCarry.cs: the same E interaction selects the nearer in-range station/cannon, handles one-item loading, and routes right mouse to the cannon when appropriate.
- PlayerCombat.cs: blocks melee in cannon aim mode and executes after carry/cannon input routing.

Updated `CombatCollection.unity` and `CookingCollection.unity` with one cannon, customer spawn references and one FoodProjectileGround collider. Prototype/cooking guides now link here. Existing Meat state, cooking access rules, truck enemy barrier, movement, input asset and project packages/settings were retained.

## Zombie vs Customer

Separate existing/new components provide the smallest distinction:

- `Enemy.prefab` represents **Zombie**: red placeholder, EnemyHealth, EnemySimpleAI, dynamic Rigidbody2D and Collider2D. It takes melee damage, pursues the player, stays outside the truck and drops one Raw Meat on death.
- `Customer.prefab` represents **Customer**: yellow/orange placeholder, Customer, BoxCollider2D and dynamic zero-gravity Rigidbody2D. It has no EnemyHealth, so melee does not kill it. It shares EnemySimpleAI with Zombies to pursue the player and stops at the food-truck barrier. Purify() sets IsPurified once, changes that renderer to green, disables its colliders and removes it after Removal Delay, default one second. It never drops Meat.

To create another Customer prefab, make a SpriteRenderer with a distinctive color, Collider2D, dynamic zero-gravity Rigidbody2D, Customer and EnemySimpleAI. Freeze rotation, use continuous collision detection, and assign PrototypeEnemy to the collider objects. Assign the shared Settings asset to Customer and EnemySimpleAI. Assign Customer Renderer if desired; otherwise Customer finds a child SpriteRenderer. Set Purified Color on Customer and Customer Removal Delay through Global Data. Do not add EnemyHealth. Assign the prefab to **Customer Prefab** on the existing EnemySpawner. The spawner assigns the shared movement Target to the player; manually placed Customers need that Target reference assigned on EnemySimpleAI. Its spawn points and interval are reused; there is no second spawner/manager.

## Cannon hierarchy and Inspector

Drag the supplied FoodCannon prefab into another truck and assign its scene references. The generated hierarchy is:

```text
FoodTruck
  FoodTruckInteriorTrigger
  EnemyBarrier
  FoodCannon                         FoodCannon + trajectory LineRenderer
    CannonBase                      SpriteRenderer placeholder
    CannonBarrel                    rotating Transform
      BarrelVisual                  SpriteRenderer placeholder
      MuzzlePoint                   Transform, local (0.8,0,-0.2)
    TargetPoint                     Transform + explosion-radius LineRenderer
      LandingDot                    SpriteRenderer placeholder
```

- FoodCannon root owns the FoodCannon component and trajectory LineRenderer. No Rigidbody or separate input controller is needed on the cannon.
- CannonBase is only the visible mount. CannonBarrel is an unscaled pivot rotated around Z to face the selected target. Its visual child provides the barrel shape.
- MuzzlePoint must be a child of the barrel, at the barrel tip, slightly above the projectile ground plane. Keep cannon/pivot/projectile roots at unit scale. In this top-down implementation negative Z represents height, so its local Z is -0.2.
- Assign **Cannon Barrel**, **Muzzle Point**, **Projectile Prefab**, **Truck Interior**, **Aim Camera**, **Trajectory**, **Target Point** and **Landing Ring** on FoodCannon. These are already assigned in both supplied scenes.
- Assign the shared **Settings** resource on FoodCannon and FoodProjectile. Edit gameplay numbers through **Global Data**. Defaults: Interaction Range 1.5, Min Range 3, Max Range 12, Flight Time 1.2 seconds, Trajectory Resolution 40. Max Range is constrained to be at least Min Range. Is Loaded starts false.
- Trajectory and landing ring use FoodTrajectory.mat, per-renderer colors and world-space positions. TargetPoint and preview are hidden until aiming.
- FoodProjectile has a **3D Rigidbody**, **SphereCollider** radius 0.12, FoodProjectile and a SpriteRenderer visual child. Rigidbody has zero damping, frozen rotation, interpolation and continuous collision detection. Assign **Visual** and **Splash Material** on FoodProjectile. Defaults: Gravity 9.81, Explosion Radius 2, Splash Duration 0.6 seconds.
- The scene needs **FoodProjectileGround**, a static 3D BoxCollider size (100,100,1), centered at (0,0,0.5). Its upper surface is Z=0. It has no renderer and does not affect 2D player/enemy collisions.

Use **MMFeast > Add Food Cannon And Customers** to add missing setup to saved prototype scenes. It reuses existing assets/cannons and updates both prototype scenes. Save scene edits before running it.

## Load and input routing

E near the cannon requires the player to be inside the truck and within cannon interaction range. Only an IngredientPickup that is carried and IsCooked is accepted. Success sets IsLoaded, destroys that one Meat object and clears PlayerCarry's held reference. Raw Meat, an empty hand or an already loaded cannon leaves all items/ammo unchanged. One cooked Meat provides one shot; there is no ammo inventory.

Near the station, E and a right-button press still place/retrieve/cook normally. When both station and cannon are in range, the nearer object receives the interaction. Move toward the lower station to cook and toward the upper cannon to load/aim. A loaded preferred cannon consumes the right-button gesture so it cannot cook the station at the same time.

Hold right mouse near the loaded cannon to enter aim mode. The normal camera is retained. Move the mouse to choose the desired battlefield landing position. Melee and E transfers are blocked while aiming; WASD remains available. Moving outside the truck or out of interaction range cancels aiming without consuming the loaded shot. Release after such cancellation does not fire.

Release right mouse while a valid aim remains active to fire exactly one projectile, empty the cannon and hide previews. The cannon stores only IsLoaded/IsAiming plus the current target/launch calculation.

## Target and shared ballistic prediction

Aim Camera.ScreenToWorldPoint maps the mouse position to the X/Y battlefield plane. The offset from cannon root is clamped to Min Range..Max Range; no angle/power adjustment is required. The barrel turns toward the resulting offset.

The game uses X/Y for top-down ground movement. FoodProjectile uses a 3D Rigidbody with **custom gravity along positive Z**, representing falling toward the ground plane, rather than changing global Physics.gravity or converting the existing 2D game. FixedUpdate applies that same Gravity vector with AddForce(..., ForceMode.Acceleration).

FoodBallistics quantizes flight time to fixed physics steps and computes:

```text
velocity = (target - origin) / T - gravity * (T + fixedDeltaTime) / 2
position(t) = origin + velocity*t + gravity*t*(t + fixedDeltaTime)/2
```

The fixed-step correction matches Unity's acceleration-before-position integration. The target's physical Z is negative SphereCollider radius, where the sphere contacts the Z=0 ground. Both the preview samples and launched Rigidbody use the same origin, velocity and projectile gravity. No Bezier curve or alternate projectile path is used.

Firing also copies the marker position and quantized flight duration into that projectile. The landing surface is excluded from this shot's collision pairs during flight, so PhysX contact tolerances cannot trigger a premature or delayed ground impact. At the scheduled landing time, the projectile resolves at the copied marker and stops before applying AoE. Other 3D obstacles still cause normal earlier collision impacts. Later cursor movement or flight-time changes do not move an already fired shot's target; gravity and explosion-radius tuning remain live.

Because looking directly down the height axis would hide the arc, FoodBallistics.VisualPosition maps physical height to a small upward display offset. **Both** the projectile's sprite and trajectory samples use that same projection. At contact height the display offset is zero, so the trajectory ends at the ground marker. The landing ring has the same radius as the real AoE.

## Impact, AoE and purification

On scheduled landing at the locked marker, or earlier collision with a 3D obstacle, FoodProjectile performs one Physics2D.OverlapCircleAll at its Rigidbody's X/Y impact position with Explosion Radius. Separate HashSets deduplicate Zombie EnemyHealth and Customer components even when an entity has multiple colliders.

- Zombie: KillByFoodProjectile() calls the existing Die(), destroys the Zombie and spawns exactly one Raw Meat.
- Customer: Purify() changes state/feedback without HP, death terminology or drops. Repeated calls do nothing.
- Player, truck, carried Meat and other unrelated objects receive no effects.

A temporary orange ring shows the food splash. The impact guard prevents repeated collision/Impact calls from applying AoE twice. There are no damage values for cannon hits, purification meters, scores, bonuses or extra food types.

## Exact Play Mode test

1. Reopen CombatCollection and press Play; focus the Game view and open Console.
2. Red Zombies and yellow Customers appear from the existing spawn points. Every second spawn is a Customer; the shared cap remains five.
3. Face a red Zombie with WASD and left-click five times, at least 0.1 seconds apart with the default global settings. It dies and drops pink Raw Meat. Yellow Customers must ignore melee.
4. Press E near the drop to carry it. Enter the centered truck; Zombies must remain outside.
5. Approach the lower teal station, for example (-1.3,-1.2), and press E to place Raw Meat.
6. Right-click to cook it brown, then press E to retrieve it.
7. Approach the upper cannon, for example (-0.9,1.2), and press E. Meat disappears from your hand and Console reports Cooked Meat loaded into cannon.
8. Hold right mouse. A cyan ballistic arc, yellow target dot and yellow explosion-radius ring appear. The barrel turns toward the target.
9. Move the pointer among groups and judge which red/yellow entities fit inside the ring. Target range is clamped; no power/angle control is required.
10. While still holding right mouse, click left mouse. It must not perform melee. The cannon must not fire until right mouse is released.
11. Release right mouse. One orange food projectile follows the arc, lands near the marker and produces a temporary orange splash ring.
12. Zombies touched by the AoE die and drop Meat. Customers touched by it turn green and disappear after about one second, with Customer purified logs and no drops. Targets outside the circle remain unchanged.
13. Try another right-button gesture without reloading: no additional shot. Cook/load another Meat to repeat.
14. Negative tests: try loading Raw Meat; try loading a second Meat into a loaded cannon; try operating it from outside the truck. Each must preserve the carried item and existing shot.
15. Begin aiming, then walk out of the truck/range before releasing. Aim cancels, and the loaded shot remains for later use.

## Validation and prototype limits

Unity 6000.6.4f1 compiled the gameplay/editor scripts and passed **45 Play Mode integration checks** in an isolated project using the same Input System package and copied scene/prefab assets. The complete melee → drop → carry → cook → load → mouse aim → release → Rigidbody impact → mixed AoE loop passed. Tests covered range clamps, previews, cooking/cannon routing, raw/full loading rejection, aim cancellation, customer melee protection, duplicate colliders, one-shot ammo, one-drop Zombie behavior and Customer removal.

For the tested six-unit shot, maximum physical path error against the shared preview calculation was about 0.000004 world units; actual landing error was about 0.014 world units. See CANNON_VALIDATION.txt. Visual URP Game-view rendering still needs the manual test above because the automated run was headless.

Known limits: Customers pursue the player without attacking; one cannon/one loaded shot; fixed orthographic camera; fixed flight duration rather than arbitrary artillery power; no 3D obstacle collision except the added ground; small contact/interpolation differences at landing; the projected arc can extend beyond the camera near screen edges. Projectile cleanup is ten seconds if ground collision is missing. No recipes, Holy Water, purification progress, SAN, friendly-fire penalty, UI menus, upgrades, scoring or multiplayer networking are included.

## Moving Customer update

Customers now share EnemySimpleAI pursuit with Zombies, using the same default Move Speed (2) and Stopping Distance (0.75). EnemySimpleAI requires only Rigidbody2D and optionally checks Zombie death or Customer purification, so adding movement does not add combat HP. Customer.prefab uses dynamic zero-gravity physics on PrototypeEnemy and is blocked by the existing truck barrier. Purification immediately stops/suspends its body before green feedback and removal.

Files modified: EnemySimpleAI.cs, EnemySpawner.cs, Customer.cs, Assets/Editor/FoodCannonPrototypeBuilder.cs, Customer.prefab, and this guide. Existing scene prefab references continue to work; no manual scene assignment is required. CUSTOMER_MOVEMENT_VALIDATION.txt records the targeted Play Mode results.
