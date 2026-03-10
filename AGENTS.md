# AGENTS.md - GameplayAbilitySystem-in-Unity

This is a Unity Gameplay Ability System (GAS) implementation. This file provides guidelines for AI agents working in this repository.

## Project Overview

- **Unity Version**: 2022.3.62f
- **Language**: C# 9.0 (.NET Framework 4.7.1)
- **Namespace**: `GAS` (main), `EGF` (GameplayTag)
- **Packages**: Standard Unity packages (TextMeshPro, Timeline, UGUI, VisualScripting)

## Build & Development Commands

### Building the Project

This is a Unity project - compilation happens through Unity Editor or Rider:

```bash
# Via Unity Editor
# Open project in Unity and use Build or Ctrl+B to compile

# Via JetBrains Rider
# Open the .sln file - Rider will sync with Unity
```

### Running Tests

No formal test framework is configured. Tests are in Example files:
- `Assets/GAS/GameplayAbilitySystem/Example/GameplayAbilityTestAndExample.cs`
- `Assets/GAS/GameplayAbilitySystem/Example/GameplayEffectTestAndExample.cs`

To run tests:
1. Open the project in Unity
2. Create a test scene with `AbilitySystemComponent`
3. Run in Play Mode

### Code Compilation Check

```bash
# Unity Editor: Window > Analysis > Code Coverage (if installed)
# Or use JetBrains Rider's built-in code analysis
```

## Code Style Guidelines

### General Conventions

- **Indentation**: 4 spaces (Unity default)
- **Line endings**: Windows (CRLF)
- **Encoding**: UTF-8 with BOM
- **Max line length**: 120 characters (soft limit)

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes/Structs | PascalCase | `AbilitySystemComponent`, `GameplayAbility` |
| Methods | PascalCase | `AddActorTags()`, `UpdateLoopActiveEffectSpecs()` |
| Properties | PascalCase | `ActorTags`, `Instanced`, `IsValid` |
| Private fields | camelCase | `initialized`, `actorTags`, `actorTagsCount` |
| Private backing fields | camelCase | `initialized`, `actorTags` |
| Constants | PascalCase | `FEpsilon`, `CreatePath` |
| Enums | PascalCase | `InstanceStrategy`, `ActiveCountPolicy` |
| Events | PascalCase (with ON prefix for internal) | `ONAnyTagAdd`, `ONActivate` |
| Interfaces | PascalCase with I prefix | (not currently used) |
| Namespaces | PascalCase | `GAS`, `EGF` |
| GameplayTags | PascalCase | `Character.Health`, `Ability.Fire` |

### File Organization

- **Partial classes**: Split into separate files using `.Ability.cs`, `.Effect.cs`, `.Attribute.cs` suffixes
- **Editor code**: Placed in `Editor/` subfolder
- **Editor utilities**: Placed in `Editor/Utils/` subfolder
- **ScriptableObjects**: Use `CreatePath` constant for menu creation
- **One class per file**: Unless using partial classes

### Using Statements (Imports)

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EGF;           // GameplayTag system
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
```

- System using statements first, then Unity, then project-specific
- Use explicit namespaces instead of `using static`
- Use `#if UNITY_EDITOR` guards for editor-only imports

### Type Usage

- **Primitives**: Use `float` for most numeric values (Unity convention), `int` for counts/indices
- **Collections**: `List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`
- **Null handling**: Use null-conditional operator `?.` and null-coalescing `??`
- **Event types**: `Action<T>` and `Action<T1, T2>` for events (not delegates)
- **Strings**: Use `nameof()` for string literals that reference code symbols

### Property & Field Patterns

```csharp
[Header("Display Name")]
[Tooltip("Description for inspector")]
[SerializeField] private GameplayTagContainer actorTags = new GameplayTagContainer();
public GameplayTagContainer ActorTags => actorTags;
```

- Use `[SerializeField]` for private serialized fields
- Use `[Header("")]` for inspector grouping
- Use `[Tooltip("")]` for field descriptions
- Use expression-bodied properties when simple: `public bool IsValid => ability;`

### Conditional Compilation

```csharp
#if UNITY_EDITOR
// Editor-only code
#endif

[Conditional("UNITY_EDITOR")]
public void EditorOnlyMethod() { }
```

- Use `#if UNITY_EDITOR` for editor-only code blocks
- Use `[Conditional("UNITY_EDITOR")]` for editor-only methods

### Error Handling & Logging

```csharp
Debug.Log("Message");           // General info
Debug.LogWarning("Warning");    // Warnings
Debug.LogError("Error");        // Errors
Debug.Assert(condition, "msg"); // Assertions (if needed)
```

- Use `Debug = UnityEngine.Debug` alias when importing both UnityEngine and other logging
- Check for null before operations when failure could occur
- Return early on invalid conditions rather than deeply nesting

### Code Patterns

**Null check pattern:**
```csharp
if (ability && ability.instanced)
{
    return ability.GetRemainingCooldownTime();
}
return 0;
```

**Event registration pattern:**
```csharp
public void RegisterOnActivateAbility(Action<GameplayAbilitySpecHandle> onActivateAbility)
{
    if(!instanced || onActivateAbility == null) return;
    ONActivate += onActivateAbility;
}
```

**Visitor pattern (for tag traversal):**
```csharp
void Visitor(GTagRuntimeTrieNode node)
{
    if(!node.active) return;
    // process node
}
tagContainer.Traverse(Visitor);
```

**Dictionary count pattern:**
```csharp
if (actorTagsCount.ContainsKey(addingTagHash))
    actorTagsCount[addingTagHash] += 1;
else
{
    actorTagsCount.Add(addingTagHash, 1);
}
```

### Comments

- Use Chinese comments (following existing codebase style)
- Use `///` for XML documentation on public APIs if needed
- Use `// TODO:` for future work items
- Comment complex logic or non-obvious behavior

### Unity-Specific Guidelines

- Use `[DisallowMultipleComponent]` on components that should only exist once
- Use `[RequireComponent(typeof(T))]` for automatic component dependency
- Use `ScriptableObject` for data assets and ability definitions
- Implement `OnValidate()` for editor-time validation
- Use `CreateAssetMenu` for ScriptableObject creation menus
- Use `[ContextMenu("Name")]` for debug/test menu items
- Initialize in `Awake()`, cleanup in `OnDestroy()`
- Use `Update()` for per-frame logic, but prefer event-driven where possible

### Performance Considerations

- Use `struct` for small data types (like `GameplayAbilitySpec`)
- Cache collections when used repeatedly in loops
- Use `Dictionary` for O(1) lookups
- Consider memory when creating objects in `Update()`

## Directory Structure

```
Assets/
├── GAS/
│   ├── EGF_GameplayTag/
│   │   ├── Scripts/          # Core tag system
│   │   └── Editor/            # Tag editor tools
│   └── GameplayAbilitySystem/
│       ├── GameplayAbility/   # Ability system core
│       ├── GameplayAttribute/ # Attribute system
│       ├── GameplayEffect/    # Effect system
│       ├── GameplayCue/       # Visual/audio cues
│       ├── Example/           # Test & example code
│       └── Editor/            # Editor inspectors/drawers
```

## Key Classes

| Class | Purpose |
|-------|---------|
| `AbilitySystemComponent` | Core ASC, manages abilities, effects, tags |
| `GameplayAbility` | Base class for abilities (ScriptableObject) |
| `GameplayEffect` | Defines stat modifiers, duration, etc. |
| `GameplayTagContainer` | Tag storage and querying |
| `GameplayAttributeSet` | Holds character attributes |
| `GameplayHandle` | Handle system for referencing objects |

## Common Tasks

**Adding a new ability type:**
1. Create new class inheriting from `GameplayAbility`
2. Override `ActivateAbility()`, `EndAbility()`, etc.
3. Add to appropriate namespace
4. Create ScriptableObject in editor

**Adding a new attribute:**
1. Create new `GameplayAttributeSet` subclass
2. Add `[SerializeField]` fields for attributes
3. Register with `AbilitySystemComponent`

**Adding editor functionality:**
1. Create class in `Editor/` folder
2. Use `#if UNITY_EDITOR` guard
3. Use `EditorWindow` for custom windows
4. Use `[CustomEditor(typeof(T))]` for custom inspectors
