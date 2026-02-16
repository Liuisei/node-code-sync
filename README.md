🇯🇵 Japanese: Packages/com.nodecodesync.unity/Documentation~/README_JP.md
# NodeCodeSync (NCS)

**Bidirectional Code ↔ Node sync — with `.cs` as the source of truth.**  
Nodes are projections. Your IDE stays in charge.

## Code-Driven Visual Projection

> Nodes are not the source of truth.  
> Code is the truth.

---

## What happens in production (Programmers × Designers)

Visual scripting (Blueprint/Behavior Tree–style systems) is powerful, but in real production the boundary between roles often becomes blurred, creating friction in the workflow.

### Pain on the programmer side
- Spending time replaying behaviors and tweaking numbers
- Losing focus on architecture, abstraction, optimization, and testing
- Not being able to fully leverage IDE-level analysis and debugging

### Pain on the designer side
- Being forced toward implementation because required features are missing
- Changes being hard to review and communicate
- Unclear impact/diff making iteration feel risky

As a result:
- Git diffs are hard
- PR review becomes difficult
- CI and automated testing don’t fit well
- Asset corruption becomes a risk

These structural issues slow down iteration.

---

## The future NCS enables

By treating code as the **Single Source of Truth**, NCS enables a workflow where specialization naturally works.

### Programmers can focus on code
They can concentrate on architecture, abstraction, optimization, and testing—  
while using their IDE’s analysis, debugging, and refactoring capabilities as-is.

### Designers can focus on fun
They can iterate on behavior tuning, structural experimentation, and balancing.  
Nodes are UI; the final artifact is always code.

NCS aims to eliminate “translation cost” in production.

---

## Core Architecture

- **Model**: C# source code (Single Source of Truth)
- **Projection**: Unity node graph UI
- **Transformer**: Roslyn-backed bidirectional conversion layer

Nodes are projections.  
Code is the truth.

---

## Not Projectional Editing

Traditional projectional editors operate directly on AST.  
Text is a byproduct.

NCS takes a different stance:

- Roslyn is at the core
- C# source files remain first-class citizens
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

┌─────────────┐      ParseText()       ┌──────────────┐
│  C# Source  │ ◀────────────────────  │  Node Graph  │
│  (.cs file) │                        │  (GraphView) │
│             │ ─────────────────────▶ │              │
└─────────────┘     AST → NodeMeta     └──────────────┘
       │
       └───────────▶  Roslyn AST (entity)
---

## Schema-Driven Design

Roslyn’s full syntax node definitions are stored as an XML schema.

- **NodeMeta**
- **FieldUnit**
- **FieldMetadata**

By swapping schemas, the same framework can be adapted to other languages.

---

## Design Principles

- Keep the model clean
- Parent/child relations are derived from edges (the single source of structure)
- No intermediate trees
- Code is always the source of truth
- Nodes are projections of code

---

## Tech Stack

- Unity 2022.3+ / Unity 6
- Unity UI Toolkit
- Unity GraphView
- Microsoft Roslyn Syntax API
- UPM package distribution

---

## Status

🚧 Early Development  
MIT License