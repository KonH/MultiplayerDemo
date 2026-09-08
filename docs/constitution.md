# Project Constitution

Non-negotiable architectural principles. The `plan` skill checks this before finalising any plan.

## Rendering

- **Universal Render Pipeline only.** No Built-in RP shaders, materials, or camera-stack patterns.

## Game Logic

- **Unity DOTS (Entities) for gameplay simulation.** Game state and simulation logic live in ECS systems/components (`com.unity.entities`), not in MonoBehaviours. MonoBehaviours are for authoring (baking) and presentation/input glue only.

## Dependency Injection

- No `FindObjectOfType` or static mutable singletons for cross-system dependencies; prefer explicit references resolved at authoring/baking time or through a composition root.

## Planning Discipline

- **Plan before implement.** No code or asset changes without an approved plan file under `docs/specs/`; this keeps scope and git history reviewable.

## Specification Discipline

- **Spec before plan for feature work.** Feature additions start with a `specify` pass that captures intent and acceptance criteria; purely technical tasks (migrations, refactors, infra) may skip the spec and go straight to `plan`.

## File Organisation

- **`docs/specs/<YY_MM_DD_HH>_<name>/` for all plans, spec-backed or not.** A technical-only plan (migration, refactor, infra) still gets a `docs/specs/<YY_MM_DD_HH>_<name>/plan.md`, simply with no `spec.md` alongside it.

## C# Code Style

- **Tabs for indentation, `_` prefix for private members, braces always, no redundant access modifiers.**
