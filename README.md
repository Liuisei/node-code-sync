🇯🇵 Japanese: [README_JP](Packages/com.nodecodesync.unity/Doc/README_JP.md)

# NodeCodeSync (NCS)

**Bidirectional Code ↔ Node sync — with `.cs` as the source of truth.**

> If visual coding and source code could be truly bidirectionally synchronized, most of these problems would disappear.

---

## Code-Driven Visual Coding

Visual representation of code carries enormous advantages.
Unreal Engine’s Blueprint system and Behavior Trees are strong examples of this success.

- Collaboration with non-programmers
- Purpose-specific visualization (e.g., Behavior Trees for AI)
- Intuitive understanding and code comprehension support

In practice, creating Mermaid diagrams like those used in this doc to strengthen shared understanding before implementation is already common—NCS takes that further.

---

## The Real-World Pain

Tools like **Unreal Engine Blueprint** and **Unity Visual Script**—well-known in the game industry—fail to reach their full potential due to the following limitations:

1. Visual code and source code cannot be bidirectionally converted
2. Poor diff support for visual assets
3. Translation cost and the "hybrid role" problem
4. Learning cost
5. Complex branching turns into "spaghetti" and density issues
6. Weak refactoring and searchability
7. Performance and runtime become a black box
8. High integration cost with external libraries and APIs

In short, visual coding is essentially a **language with a very weak ecosystem**.

As you may have guessed: if problem **1** (bidirectional conversion between visual and source code) is solved, problems **2–8** are all resolved as a consequence.

---

## Where the Project Stands Now

This project has achieved **bidirectional conversion of all C# syntax** with AST (Abstract Syntax Tree) nodes.
Minimum-density-unit visual coding is now possible. (No SyntaxFactory—entirely XML-driven.)

Going forward, we plan to implement filters and specialized GUIs for use cases such as numeric tuning and AI behavior editing (Behavior Trees).

---

### *Note: Unreal Engine Blueprint is out of scope*

## To Avoid Misconceptions

Unreal Engine is used here as a reference point for successful visual coding—but resolving the UE Blueprint problem is considered out of scope for this project.

C++ and Unreal Engine have an extremely powerful ecosystem, but due to differences in language design and tooling philosophy, applying a Roslyn-style bidirectional editing model is currently impractical.

We hope that explaining NCS's approach might inspire surprising new solutions for other ecosystems.

---

## What NCS Enables in Production (Programmers × Hybrid × Planners)

Visual scripting (Blueprint / Behavior Tree–style systems) is powerful, but in real production the boundary between roles often blurs, creating friction in the workflow.

### For Programmers
- Focus on design, abstraction, optimization, and testing
- Generate nodes that expose only tunable values to planners—leveraging their strength in maintainability
- Read planner changes instantly in C#
- Code that was hard to read in C# becomes naturally clearer when viewed as nodes

### For Planners
- Stay focused on what matters: **making the game fun**
- No more anxiety about unclear impact ranges when making changes
- Say goodbye to spaghetti graphs
- Edits made in nodes become C# assets

### For Hybrid Roles
- Understand design intent from both code and node perspectives
- Accelerate the prototype → implementation → optimization loop
- Maximize efficiency as a bridge between tuning and structural design

## As a Result

The common problems with visual coding are resolved:

- Git diffs are problematic → ✅ .cs files are plain text and fully diff-friendly
- PR review is difficult → ✅ reviewable in any code review tool
- CI and automated testing don't fit → ✅ standard C# test pipelines work
- Asset corruption risk → ✅ no proprietary binary assets

---

## The Future NCS Creates

A future where planners and programmers can make games with the least friction possible.

---

# Core Architecture

- **Model**: C# source code (Single Source of Truth)
- **Projection**: Unity node graph UI
- **Transformer**: Roslyn-backed bidirectional conversion layer

Code is the truth.
Nodes are projections.

---

## Similar to Projectional Editing—but Different

Traditional projectional editors operate directly on the AST.
Text is a byproduct.

NCS takes a different stance:

- Roslyn is at the core
- C# source files remain **first-class citizens**
- Nodes and code stay bidirectionally synchronized

---

## Parser-Backed Bidirectional Code ↔ Node Sync

| | Projectional Editing | NCS |
|---|----------------------|-----|
| Source of truth | AST | C# source code |
| Parser | Not needed | Roslyn |
| Editing direction | AST → UI | Code ↔ Node |
| Persistence | Custom format | `.cs` (Git-friendly) |

> `.cs` files remain first-class citizens.

---

## Architecture Overview

```text
┌─────────────┐   NodeToCodeConverter     ┌──────────────┐
│  C# Source  │ ◀──────────────────────── │  Node Graph  │
│  (.cs file) │                           │  (GraphView) │
│             │ ─────────────────────────▶│              │
└─────────────┘   ParseText() → NodeMeta  └──────────────┘
       │
       └───────────▶  Roslyn AST  (debug only)
```

---

## Schema-Driven Design

Built from Roslyn's full syntax node definition XML.

- **NodeMeta** — schema template per syntax node kind
- **FieldUnit** — recursive union type representing all possible syntax shapes
- **FieldMetadata** — schema definition and runtime instance data

---

## Far-Future Vision

By abstracting the XML schema, the node layer, and the parser layer, the framework could eventually support a wide range of languages beyond C#.

Thank you for reading.

---

## Tech Stack

- Unity 6000.3+
- Unity UI Toolkit
- Unity GraphView
- Microsoft Roslyn Syntax API
- UPM Package

---

## Status

🚧 Early Development
MIT License