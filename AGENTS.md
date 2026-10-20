# AGENTS.md

## Project Overview

This is a Unity prototype for a co-op action / cooking roguelike currently titled **MMFeast**.

The game is set during a supernatural mental-infection apocalypse.

Players operate around a mobile food truck and fight fully mutated enemies. Defeated enemies drop body-part ingredients. Players collect those ingredients, process and cook them into dishes, then use those dishes as ammunition to purify infected humans who can still be saved.

The long-term core loop is:

Combat
→ Collect Ingredients
→ Cook
→ Load Food
→ Fire at Infected Customers
→ Purify Customers
→ Gain Resources
→ Upgrade

The project is currently in an EARLY PROTOTYPE stage.

The current priority is NOT to build the complete game.

The priority is to implement and test one system at a time with the smallest possible playable implementation.

---

# Current Prototype Goal

The current prototype focuses first on the basic combat / collection loop:

Enemy Spawn
→ Player Attacks Enemy
→ Enemy Takes Damage
→ Enemy Dies
→ Enemy Drops Ingredient
→ Player Picks Up Ingredient

Unless explicitly requested, do NOT implement later systems yet.

Later systems include:

- Cooking
- Recipes
- Ingredient processing
- Seasoning
- Holy Water
- Customer purification
- SAN system
- Food truck HP
- Food truck upgrades
- Player upgrades
- Roguelike upgrade selection
- Character abilities
- Multiple weapon classes
- Elemental damage
- Automation
- Shops
- Meta progression

Do not add these systems proactively.

---

# Development Philosophy

## Prototype First

Always prefer the simplest implementation that allows the mechanic to be tested.

Do not over-engineer systems for hypothetical future requirements.

For example:

BAD:
- Building a generic RPG stat framework before implementing enemy HP
- Building a full inventory system when the player only needs to carry one object
- Creating a generic ability framework for one melee attack
- Creating complex ScriptableObject architectures before they are necessary

GOOD:
- EnemyHealth with a simple TakeDamage()
- PlayerCombat with one melee attack
- PlayerCarry with one currentHeldItem
- A simple EnemySpawner

Systems can be generalized later after the gameplay proves that they are useful.

---

# Before Editing Anything

Before implementing a requested feature:

1. Inspect the relevant existing scripts.
2. Inspect the current scene hierarchy if accessible.
3. Inspect existing prefabs.
4. Check which Unity Input System the project uses.
5. Search for existing systems that already perform similar functionality.
6. Understand dependencies before modifying scripts.

Reuse or extend existing systems whenever practical.

Do NOT create duplicate systems.

For example, if the project already contains:

- PlayerController
- InteractionSystem
- Health component
- Pickup system
- Input Actions
- Audio manager

then modify or integrate with them instead of creating parallel replacements.

---

# Scope Discipline

Only implement what the user explicitly requests.

Do not add unrelated features because they "might be useful later."

If the user asks for enemy damage, do not automatically add:

- critical hits
- armor
- elemental resistances
- stamina
- status effects
- damage numbers
- loot rarity

unless specifically requested.

If additional work appears necessary for the requested feature to function, explain why before significantly expanding scope.

---

# Core Gameplay Terminology

Use the following terminology consistently.

## Player

The controllable character.

For the current prototype:

- WASD movement
- Basic melee attack
- Can carry one object at a time

## Enemy

A fully mutated hostile creature.

Enemies can:

- Spawn
- Move toward the player
- Take damage
- Die
- Drop ingredients

## Ingredient

An item dropped by enemies.

For the earliest prototype, use only:

`Meat`

Later versions may include:

- Eye
- Brain
- Limb
- Intestine
- Tongue
- Bone
- Vegetables
- Seasonings

Do not implement these additional ingredient types unless requested.

## Infected Customer

A human who is infected but can still be purified.

Do not refer to successful purification as "killing the customer."

Long-term terminology should be:

- Purification
- Purification Progress
- Purified

instead of:

- Customer HP
- Customer Death

unless combat against that entity is explicitly intended.

## Food Truck

The central mobile base that will eventually contain systems such as:

- Cooking stations
- Holy Water
- Garden
- Cannon
- Customer service area

Most of these systems are not part of the current prototype.

---

# Coding Guidelines

## General

Write clear, readable C# intended for a student game-development team.

Prefer straightforward code over clever abstractions.

Use descriptive names.

Avoid unnecessary inheritance hierarchies.

Avoid premature generic frameworks.

Avoid unnecessary singletons.

Avoid static global state unless there is a clear reason.

---

# Unity Inspector

Gameplay values that designers may want to tune should usually be exposed in the Inspector.

Prefer:

```csharp
[SerializeField] private float attackDamage = 10f;
[SerializeField] private float attackRange = 1.5f;
[SerializeField] private float attackCooldown = 0.5f;