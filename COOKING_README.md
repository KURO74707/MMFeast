# Food truck cooking prototype

The next Food Cannon / Customer purification system is included in both prototype scenes. See [CANNON_README.md](CANNON_README.md). Right mouse still cooks at the station; holding/releasing it near the loaded cannon aims/fires instead.

Open `Assets/Prototype/CookingCollection.unity`, enter Play Mode, and click the Game view. The dark rectangular floor represents the usable Food Truck interior. The truck is present at game start, centered at (0,0) in the camera view. The teal CookingStation is at (0,-1.2); the player starts outside at (0,-4). All references are assigned in this scene.

## Files changed for food truck access

Created:

- `Assets/Scripts/Prototype/FoodTruckInteriorTrigger.cs` and `.meta`: detects this prototype's player entering/exiting a 2D interior volume.
- `Assets/Editor/FoodTruckPrototypeBuilder.cs` and `.meta`: adds the floor, interior trigger and station reference to the existing cooking scene without adding another station.
- `Assets/Prototype/Prefabs/FoodTruck.prefab` and `.meta`: visible floor plus the configured interior trigger.

Modified:

- `Assets/Scripts/Prototype/CookingStation.cs`: requires both interior membership and interaction distance for placement, cooking and retrieval; adds clear rejection logs.
- `Assets/Scripts/Prototype/PlayerCarry.cs`: checks station access before any station transfer while retaining ground pickup outside the truck.
- `Assets/Editor/CookingPrototypeBuilder.cs`: includes truck setup when generating a fresh cooking prototype.
- `Assets/Prototype/CookingCollection.unity`: adds one FoodTruck instance and assigns the existing station's Truck Interior reference.
- `COOKING_README.md` and `COOKING_VALIDATION.txt`: current setup instructions and test results.

The existing IngredientPickup Raw/Cooked state, PlayerController right-mouse input, Meat/Enemy/Station prefabs, combat/spawning scripts, CombatCollection scene, input asset, packages and project settings are reused.

## Interior trigger setup

The project uses 2D physics. Use **BoxCollider2D**, not a 3D BoxCollider.

The generated FoodTruck root is at (0,0), with unit scale. Its FoodTruckInteriorTrigger child has BoxCollider2D with **Is Trigger enabled**, Size (6,4), Offset (0,0), and FoodTruckInteriorTrigger. This covers x=-3..3 and y=-2..2. InteriorFloor matches that area visually. EnemyBarrier is a solid BoxCollider2D slightly larger than the interior. Its layer overrides allow collision only with PrototypeEnemy, so enemies cannot enter while the player can cross freely. There are no truck HP or driving systems.

In another scene:

1. Drag `Assets/Prototype/Prefabs/FoodTruck.prefab` into the scene and position it around the usable truck interior.
2. Resize the trigger's BoxCollider2D to cover the walkable interior, excluding outside ground. Resize the floor visual to match. Keep the trigger object at unit scale where practical.
3. The player needs its existing PlayerController, Collider2D and Rigidbody2D. No new player component, tag or layer is required. With custom layers, enable collisions between player and trigger in the Physics 2D layer matrix.
4. FoodTruckInteriorTrigger identifies a player by `GetComponentInParent<PlayerController>()`. Enemy, Meat and other objects are ignored.
5. Trigger entry sets membership; exit removes it. Multiple player colliders do not cause premature exit. Disabled, destroyed or non-overlapping player colliders cannot retain access. Disabling the trigger clears membership.

This is a single-player prototype, without multiplayer networking or a generic zone framework.

## Cooking Station setup and references

The existing scene already has a single configured CookingStation; no manual assignments are needed there.

For another scene, drag `Assets/Prototype/Prefabs/CookingStation.prefab` into the truck interior. Keep its root scale (1,1,1). Assign **Truck Interior** on CookingStation to the scene instance's FoodTruckInteriorTrigger. The prefab cannot store a reference to an arbitrary scene truck, so this reference must be assigned when placing it in another scene.

For manual creation:

1. Create CookingStation with a visible counter and optional solid BoxCollider2D.
2. Add CookingStation.
3. Create a CookingPoint child where Meat should sit; assign it to **Cooking Point**.
4. Assign **Truck Interior** to the interior trigger component.
5. Assign the shared **Settings** resource. Set **Cooking Interaction Range** through **Global Data**, default 1.5 world units from station root to player root. Ensure the counter's solid collider does not prevent approaching this close.
6. Leave **Current Meat** empty initially.

The generated station's CookingPoint is already assigned. Its Truck Interior reference points to the generated FoodTruck instance. PlayerCarry continues using its existing CarryPoint and pickup range.

The menu **MMFeast > Add Food Truck Access To Cooking Prototype** adds the missing truck and station reference to a saved cooking scene. Re-running it reuses the existing trigger and station. **MMFeast > Create Cooking Prototype** creates a fresh scene with truck access if the cooking scene is absent. These tools require any scene being edited to be saved first.

## Access rule and interaction

CookingStation checks the referenced trigger for this player's membership, then checks distance. Its TryPlace, TryCook and TryTake methods all enforce both conditions. A missing/disabled trigger grants no access.

- WASD / arrows: move; yellow marker shows attack direction.
- Left mouse: existing melee attack.
- E outside truck: ground pickup still works. Station transfers are refused, even if the player is close enough to the counter.
- E inside and near station, holding Meat: place it if the slot is empty. Rejected placement retains the held item.
- E inside and near station, empty-handed: retrieve the station item. Empty station permits normal nearby ground pickup.
- Right mouse inside and near station: cook Raw Meat immediately.
- Right mouse outside, too far, empty station or already Cooked Meat: no item/state changes.

On placement, the same Meat detaches from CarryPoint and snaps to CookingPoint. PlayerCarry's hand becomes empty; the station stores that Meat. Collider2D and Rigidbody2D simulation stay disabled. Retrieval clears the station slot and reparents the same Meat to CarryPoint. Only one item can be carried; the station has one slot. Raw Meat can also be retrieved before cooking. Cooked Meat can be placed again without resetting state.

## State, input and safe color

The existing IngredientPickup represents Meat and stores a serialized MeatState enum, Raw or Cooked. Its IsCooked property exposes the state. Cook() changes Raw to Cooked once; pickup, placement and leaving the truck do not reset it.

Inspector settings on Meat remain Raw Color (pink), Cooked Color (brown), and Item Renderer (its SpriteRenderer). The existing color update writes SpriteRenderer.color for that item only. It never edits sharedMaterial or a material asset, so cooking one Meat does not recolor the others.

PlayerController already owns a new Input System button action bound to `<Mouse>/rightButton`, enabled/disabled with the controller and disposed on destruction. PlayerCarry routes the press to CookingStation.TryCook. Move/Attack/Interact still use the existing input asset; E remains an immediate press. No input bindings were changed for this update.

## Full test

1. Open CookingCollection, enter Play Mode, focus Game view and open Console.
2. Enemies spawn and approach. Face one with WASD and left-click three times, at least 0.5 seconds apart. It drops exactly one pink Raw Meat.
3. Walk near Meat and press E while outside the truck; Meat attaches to CarryPoint.
4. Walk onto the dark truck floor, for example near (-2,0). Console logs `Player entered Food Truck`.
5. Approach the counter from its left, around (-1.3,-1.2), and press E. Raw Meat snaps to CookingPoint; your hand becomes empty.
6. Confirm it remains pink until you right-click.
7. Right-click while inside and nearby. Meat becomes brown immediately. Console logs the cooking trigger and success.
8. Right-click again; nothing changes, and Console reports that Meat is already cooked.
9. Press E with empty hands. Console logs `Cooked Meat picked up`; Meat returns to CarryPoint and stays brown.
10. Leave the dark floor. Console logs `Player exited Food Truck`; you still carry Cooked Meat.
11. Check the outside-near-counter case around (0,-2.5). The player is close to the station but fully outside the interior collider. E must not place held Meat.
12. To test outside cooking/retrieval, place Raw Meat inside first, then leave to (0,-2.5). Right-click must not cook it; E must not retrieve it.
13. Check inside-but-too-far at (2,1). Cooking and station transfers must be refused.
14. Place one Meat, pick up another from the ground, and approach the station from inside. E must report occupied and preserve both items.
15. With Meat in hand outside the truck, try E near another ground Meat. The one-item restriction must still hold.

Restart Play Mode to reset the test. No recipes, extra ingredients, timing, menus, progress bars, burning, quality, SAN, Holy Water, purification, upgrades, cannon, truck HP or networking are included.

## Validation

Unity 6000.6.4f1 compiled the delivered scripts in an isolated project using the same Input System package and copies of existing assets. Thirty-eight Play Mode integration checks passed, covering automatic spawn, combat/drop, ground pickup, trigger entry/exit, nearby-outside rejection, inside-distance rejection, placement/cooking/retrieval, state/color isolation, one-item carry, occupied-slot rejection and collider/trigger disable/reactivation. See COOKING_VALIDATION.txt. Delivered gameplay source and GUIDs match the tested copies. The headless test does not visually verify this project's URP Game view; follow the manual test to confirm the floor and colors.


## Centered truck and enemy exclusion update

The truck now exists in the saved scene from game start and is centered in the camera viewport. The player begins at (0,-4), outside its 6 by 4 interior. The counter is inside at (0,-1.2).

FoodTruck.prefab now contains EnemyBarrier, a static non-trigger BoxCollider2D of size (6.1,4.1). Its Include Layers selects PrototypeEnemy, Exclude Layers excludes every other layer, and Layer Override Priority is 100. The player remains on Default and is not blocked. The separate interior trigger still detects player entry/exit and controls cooking access.

Enemy.prefab and its children use the new PrototypeEnemy layer; its Rigidbody2D uses Continuous collision detection. Enemies continue pursuing the player normally but cannot cross the solid truck boundary. No AI, attack, carry, cooking state or global collision-matrix changes were needed. When creating a different enemy prefab, assign PrototypeEnemy to its physics collider objects and use a dynamic Rigidbody2D with continuous detection.

Files modified for this update: Assets/Editor/FoodTruckPrototypeBuilder.cs, Assets/Prototype/CookingCollection.unity, Assets/Prototype/Prefabs/FoodTruck.prefab, Assets/Prototype/Prefabs/Enemy.prefab, ProjectSettings/TagManager.asset (adds PrototypeEnemy to a free user layer), and this README. TRUCK_BARRIER_VALIDATION.txt was created with 22 passing Play Mode checks. Existing cooking validation results remain available in COOKING_VALIDATION.txt.

Test by opening CookingCollection and pressing Play: the truck is centered immediately. Enter through the left/right edge with WASD, let enemies pursue you inside, and confirm they stop at its boundary. Leave the truck to resume outdoor combat. Test cooking from (-1.3,-1.2), then confirm outside access is still rejected at (0,-2.5).

Unity 6000.6.4f1 compiled the updated builder and ran actual 2D physics tests for player traversal, all four enemy approach sides, four diagonal approaches, continued pursuit and high-speed collision. All 22 checks passed; the headless run did not visually inspect URP rendering.

## Main scene repair

CombatCollection.unity now also contains the centered FoodTruck and CookingStation with the interior reference assigned, and is first in Build Settings. This fixes the mismatch where the currently used combat scene had neither object. If CombatCollection was already open while files were updated, stop Play Mode and reopen it from the Project panel to load the saved changes. The menu MMFeast > Upgrade Combat Scene With Truck And Cooking can also rebuild missing truck/station setup in a saved combat scene.
