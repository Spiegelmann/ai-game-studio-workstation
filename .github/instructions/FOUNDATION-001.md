# FOUNDATION-001 — Establish .NET Solution and Architectural Skeleton

You are implementing `FOUNDATION-001` for the AI Game Studio Workstation.

## Read First

Before modifying anything, read:

* `AGENTS.md`
* `.github/copilot-instructions.md`
* `docs/implementation/IMPLEMENTATION-PLAN.md`

The repository has completed `BOOTSTRAP-001`.

This is the first actual software implementation task.

---

# Purpose

Establish the minimal .NET solution and architectural project structure required for the AI Game Studio Workstation.

The goal is to create the structural foundation for:

```text
Core
Application
Infrastructure
Host
```

with the correct dependency direction:

```text
Core ← Application ← Infrastructure ← Host
```

At this stage, the projects should be intentionally minimal.

Do not implement the autonomous runtime yet.

---

# Dependencies

Required:

```text
BOOTSTRAP-001
```

No other implementation task is required.

---

# Preconditions

Before implementation:

1. Inspect the repository.
2. Read the repository constitution.
3. Read the implementation plan.
4. Check Git status.
5. Confirm the repository is in the expected clean/bootstrap state.
6. Confirm no unexpected source files exist.
7. Confirm the .NET SDK available in the environment is suitable for the implementation.

If the repository contains unexpected changes:

STOP and report them.

Do not delete or reset them.

---

# Scope

Create the minimal .NET solution structure for the workstation.

The implementation should establish four architectural projects:

```text
Core
Application
Infrastructure
Host
```

Also establish appropriate test structure for the implemented foundation.

The exact naming should be consistent and professional with the project identity.

A reasonable structure is:

```text
src/
    Core/
    Application/
    Infrastructure/
    Host/

tests/
    Core.Tests/
    Application.Tests/
    Infrastructure.Tests/
    Host.Tests/
```

and a solution file at repository root.

The final structure may use a slightly different naming convention if required by the existing repository, but do not introduce unnecessary complexity.

---

# Architectural Responsibilities

## Core

Core contains domain-level concepts and contracts that have no dependency on external infrastructure.

Core must not reference:

* Infrastructure
* Host
* external execution mechanisms
* filesystem implementations
* process implementations
* Git implementations
* LLM providers
* database implementations

At this stage, Core should contain only the minimum foundation necessary to prove the project structure.

Do not prematurely implement all domain objects.

---

## Application

Application contains application/use-case orchestration abstractions.

Application may reference:

```text
Core
```

Application must not reference:

```text
Infrastructure
Host
```

At this stage, do not implement the Orchestrator.

Do not implement task scheduling.

Do not implement autonomous execution.

---

## Infrastructure

Infrastructure contains implementations of external boundaries.

Infrastructure may reference:

```text
Application
Core
```

It must not be referenced by Core.

Do not implement actual filesystem, process, Git, LLM, or persistence adapters yet unless absolutely required merely to establish project compilation.

Those implementations belong to later tasks.

---

## Host

Host is the composition/startup boundary.

Host may reference:

```text
Infrastructure
Application
Core
```

Host is responsible eventually for composing the runtime.

At this stage, only establish the minimal executable host necessary to demonstrate that the solution structure works.

Do not implement the Orchestrator.

Do not implement autonomous behavior.

Do not implement an agent runtime.

---

# Dependency Direction

The final project dependency graph for this task must be:

```text
Host
 ↓
Infrastructure
 ↓
Application
 ↓
Core
```

Equivalent architectural notation:

```text
Core ← Application ← Infrastructure ← Host
```

There must be no reverse dependency.

Examples of prohibited dependencies:

```text
Core → Application
Core → Infrastructure
Core → Host

Application → Infrastructure
Application → Host

Infrastructure → Host
```

Do not create circular references.

---

# Technology

Use the repository's intended modern .NET stack.

Do not add third-party NuGet packages unless explicitly required.

Prefer the .NET BCL and standard SDK capabilities for this foundation.

Do not introduce:

* Entity Framework
* database providers
* logging frameworks
* mediator frameworks
* CQRS frameworks
* AI SDKs
* Git libraries
* process libraries
* filesystem abstraction packages
* dependency injection frameworks beyond what the standard .NET stack already provides

unless a later task explicitly requires them.

The purpose of this task is structure, not feature implementation.

---

# Solution

Create a root solution representing the AI Game Studio Workstation.

The solution must include:

```text
Core
Application
Infrastructure
Host
```

and the applicable test projects.

The solution must build successfully.

---

# Test Projects

Create appropriate test projects corresponding to the architectural projects.

At minimum, establish the test structure without pretending to have domain behavior that does not yet exist.

Tests should verify the foundation where meaningful.

Do not create meaningless test cases solely to inflate coverage.

---

# Architecture Tests

Where practical, establish tests that protect the dependency direction.

The important architectural invariant is:

```text
Core
  has no dependency on Application, Infrastructure, or Host

Application
  depends only on Core

Infrastructure
  depends on Application and Core

Host
  may compose the other layers
```

If architectural dependency verification requires a third-party package, do not automatically add one.

Prefer a simple approach appropriate to this early stage.

If a robust automated architecture test cannot reasonably be implemented without introducing unnecessary dependencies, document that limitation rather than overengineering it.

---

# Namespace and Naming

Use consistent namespaces based on the chosen project naming convention.

Avoid:

* abbreviations without reason
* placeholder names such as `TestProject`
* generic `Utils`
* generic `Helpers`
* generic `Common`
* generic `Shared`

Do not create a shared project merely to avoid duplication.

---

# Repository Structure

The resulting repository should be conceptually similar to:

```text
/
├── AGENTS.md
├── README.md
├── .github/
│   └── copilot-instructions.md
├── docs/
│   └── implementation/
│       └── IMPLEMENTATION-PLAN.md
├── src/
│   ├── Core/
│   ├── Application/
│   ├── Infrastructure/
│   └── Host/
├── tests/
│   ├── Core.Tests/
│   ├── Application.Tests/
│   ├── Infrastructure.Tests/
│   └── Host.Tests/
└── <solution>.sln
```

Do not add folders for future systems merely because they are mentioned in the architecture documentation.

For example, do not create:

```text
agents/
orchestrator/
gmod/
blender/
hammer/
audio/
assets/
```

at this stage.

---

# Out of Scope

Do NOT implement:

* Task
* TaskAttempt
* ToolInvocation
* StateTransition
* EvidenceRecord
* Checkpoint
* RecoveryDecision
* HumanDecision

unless a minimal placeholder is genuinely required by the solution structure.

Prefer not to implement these domain objects yet.

They will be introduced in later foundation tasks.

Do NOT implement:

* persistence
* databases
* filesystem tools
* process execution
* Git adapters
* build adapters
* verification
* agent runtime
* LLM integration
* policy engine
* capability registry
* orchestrator
* recovery engine
* Garry's Mod integration
* Blender integration
* Hammer integration
* sound tooling
* texture tooling
* multi-agent execution
* Level 5 artistic judgement

---

# Security Requirements

This task must not create any unrestricted execution boundary.

In particular:

* no shell execution
* no arbitrary process execution
* no unrestricted filesystem access
* no network automation
* no secret handling
* no Git mutation tooling
* no LLM integration
* no dynamic plugin loading
* no reflection-based command execution

The host must not become an unrestricted automation shell.

---

# Build Verification

Run the solution build.

The build must succeed.

Record:

* SDK/runtime used
* configuration
* target frameworks
* exit status
* relevant diagnostics

Do not hide warnings or errors.

If the build fails because of an environmental problem, classify it rather than pretending the task passed.

---

# Test Verification

Run the applicable test suite.

Record:

* tests executed
* passed
* failed
* skipped
* exit status

A test command returning a non-success result must not be reported as PASS.

---

# Git Verification

Before implementation:

```text
Record baseline Git state.
```

After implementation:

```text
Inspect Git status.
Inspect Git diff.
```

Verify that only FOUNDATION-001 scope was changed.

Do not reset or clean the repository.

Do not modify unrelated files.

---

# Acceptance Criteria

## AC-001 — Solution exists

A valid .NET solution exists at repository root.

Status must be:

```text
PASS
```

only if the solution is valid and can be loaded/built.

---

## AC-002 — Core exists

The Core project exists and builds.

---

## AC-003 — Application exists

The Application project exists and builds.

---

## AC-004 — Infrastructure exists

The Infrastructure project exists and builds.

---

## AC-005 — Host exists

The Host project exists and builds.

---

## AC-006 — Dependency direction

The dependency graph conforms to:

```text
Core ← Application ← Infrastructure ← Host
```

No prohibited reverse dependencies exist.

---

## AC-007 — Test structure

Appropriate test projects exist and build.

---

## AC-008 — Build

The complete solution builds successfully.

---

## AC-009 — Tests

The applicable test suite completes successfully.

---

## AC-010 — No unnecessary dependencies

No unnecessary third-party dependencies were introduced.

---

## AC-011 — No autonomous execution

No arbitrary filesystem, process, shell, LLM, Git mutation, or autonomous execution capability was introduced.

---

## AC-012 — Scope

Only FOUNDATION-001 scope was changed.

No unrelated refactoring was performed.

---

## AC-013 — Documentation consistency

The implementation does not contradict:

* `AGENTS.md`
* `.github/copilot-instructions.md`
* `docs/implementation/IMPLEMENTATION-PLAN.md`

---

# Evidence

Provide evidence for:

* repository structure
* solution creation
* project references
* dependency direction
* build
* tests
* Git status
* Git diff
* dependency changes

Do not rely solely on your own description.

---

# Definition of Done

FOUNDATION-001 is complete only when:

1. The solution exists.
2. Core exists.
3. Application exists.
4. Infrastructure exists.
5. Host exists.
6. Correct dependency direction exists.
7. Test structure exists.
8. Solution builds.
9. Tests pass.
10. No unauthorized dependencies were added.
11. No autonomous execution boundary was introduced.
12. Git scope is clean and understood.
13. All acceptance criteria have evidence.

If any acceptance criterion is:

```text
FAIL
```

or:

```text
UNKNOWN
```

do not claim the task is complete.

---

# Completion Report

At completion, report:

## Task

```text
FOUNDATION-001
```

## Files

List:

* created files
* modified files
* deleted files, if any

## Architecture

Explain the resulting project dependency graph.

## Build

Report:

* command
* target framework
* result
* exit status
* relevant warnings/errors

## Tests

Report:

* command
* tests executed
* passed
* failed
* skipped
* result

## Dependencies

Report any added dependencies.

If none were added, explicitly state that.

## Security

Confirm:

* no arbitrary shell
* no arbitrary process execution
* no unrestricted filesystem
* no LLM integration
* no Git mutation capability
* no secret handling

## Git

Report:

* baseline state
* final status
* files changed
* scope verification

## Acceptance Criteria

Report every criterion individually:

```text
AC-001 PASS
AC-002 PASS
...
```

Use `FAIL` or `UNKNOWN` where appropriate.

Do not convert uncertainty into PASS.

## Limitations

List anything intentionally deferred to later tasks.

## Next Task

The next task is:

```text
FOUNDATION-002
```

Do not implement it.

STOP after the completion report.
