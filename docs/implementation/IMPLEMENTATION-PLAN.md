# AI Game Studio Workstation — Implementation Plan

## Purpose

This document defines the controlled implementation sequence for the AI Game Studio Workstation.

The implementation is intentionally staged.

The system must not jump directly from an empty repository to autonomous Garry's Mod development.

Each stage establishes foundations required by later stages.

---

# Implementation Rules

1. Implement one task at a time.
2. Do not skip dependencies.
3. Do not implement future tasks early.
4. Do not silently modify the architecture.
5. Do not weaken security to simplify implementation.
6. Every task must have tests where applicable.
7. Every task must have explicit verification.
8. `UNKNOWN` is never equivalent to `PASS`.
9. Failed tasks must be diagnosed before recovery.
10. Recovery cannot increase authority.
11. Human authority remains above autonomous execution.
12. The repository must remain buildable at appropriate milestones.
13. Git must remain usable as the source-of-truth history.
14. No destructive Git operation may be introduced autonomously.
15. No arbitrary shell must be exposed to agents.
16. No unrestricted filesystem API may be introduced.
17. No model may directly authorize its own capabilities.
18. No project content may become trusted instruction automatically.

---

# Bootstrap

The repository begins empty.

Bootstrap consists of:

```text
BOOTSTRAP-001
```

Its purpose is to establish the durable repository specification.

Bootstrap must create only the documentation/instruction foundation required for implementation.

It must not implement the autonomous runtime.

The bootstrap package consists of:

```text
AGENTS.md
.github/copilot-instructions.md
docs/implementation/IMPLEMENTATION-PLAN.md
```

After bootstrap is verified, implementation begins with `FOUNDATION-001`.

---

# Implementation Sequence

## Foundation

```text
FOUNDATION-001
FOUNDATION-002
FOUNDATION-003
FOUNDATION-004
FOUNDATION-005
```

Establish:

* solution structure
* Core
* Application
* Infrastructure
* Host
* baseline dependency direction
* foundational contracts
* baseline tests

---

## Persistence

```text
PERSISTENCE-001
PERSISTENCE-002
PERSISTENCE-003
PERSISTENCE-004
PERSISTENCE-005
```

Establish durable state for:

* tasks
* attempts
* invocations
* transitions
* evidence
* checkpoints
* recovery decisions

Persistence must support crash recovery without relying on LLM memory.

---

## Policy

```text
POLICY-001
POLICY-002
POLICY-003
POLICY-004
POLICY-005
```

Establish:

* capability registry
* policy evaluation
* permission decisions
* scope enforcement
* protected resources
* authorization boundaries

The policy layer must remain independent from model reasoning.

---

## Workspace Tools

```text
TOOL-001
TOOL-002
TOOL-003
```

Establish bounded workspace operations.

Initial semantic capabilities:

```text
Workspace.Read
Workspace.List
Workspace.Write
```

The workspace boundary must be independently enforced by the tool layer.

---

## Process

```text
PROCESS-001
PROCESS-002
PROCESS-003
PROCESS-004
```

Establish controlled process execution.

The system must not expose arbitrary shell execution.

Execution must use structured command profiles.

---

## Git

```text
GIT-001
GIT-002
GIT-003
```

Establish:

```text
Git.ReadStatus
Git.ReadHead
Git.ReadDiff
```

Git must distinguish:

```text
CLEAN
DIRTY_EXPECTED
DIRTY_UNEXPECTED
UNKNOWN
```

Unexpected user changes must be preserved.

---

## Build and Test

```text
BUILD-001
BUILD-002
BUILD-003
```

Establish semantic build/test adapters:

```text
DotNet.Build
DotNet.Test
```

Build and test results become evidence.

They do not independently grant task acceptance.

---

## Recovery

```text
RECOVERY-001
RECOVERY-002
RECOVERY-003
RECOVERY-004
RECOVERY-005
```

Establish:

* failure classification
* checkpointing
* retry policy
* recovery decisions
* crash recovery
* bounded repair

Unknown outcomes must remain explicit.

---

## Verification

```text
VERIFY-001
VERIFY-002
VERIFY-003
VERIFY-004
VERIFY-005
```

Establish:

* verification result model
* build verification
* test verification
* scope verification
* Git verification
* acceptance evaluation

The verification layer must remain independent from agent self-report.

---

## Agent Runtime

```text
AGENT-001
AGENT-002
AGENT-003
AGENT-004
AGENT-005
AGENT-006
```

Establish:

* agent contract
* context assembly
* trust/provenance
* model abstraction
* structured model output
* runtime limits
* capability requests

Agents receive bounded capabilities.

---

## Orchestrator

```text
ORCH-001
ORCH-002
ORCH-003
ORCH-004
ORCH-005
ORCH-006
ORCH-007
ORCH-008
```

Establish:

* task discovery
* dependency resolution
* readiness
* deterministic scheduling
* attempt lifecycle
* execution loop
* verification loop
* recovery loop
* human escalation

The initial orchestrator should remain deterministic.

Do not use an LLM to decide basic task scheduling in the MVP.

---

# MVP-001

After the infrastructure is established:

```text
MVP-001-A
MVP-001-B
MVP-001-C
MVP-001-D
MVP-001-E
MVP-001-F
MVP-001-G
```

The MVP proves the autonomous control plane using a deliberately small task.

It must not require:

* Garry's Mod
* Blender
* Hammer
* sound tooling
* texture tooling
* cloud services
* multi-agent execution
* subjective artistic judgement

The MVP should demonstrate:

```text
Task
→ Ready
→ Claim
→ Attempt
→ Agent reasoning
→ Capability request
→ Policy
→ Tool
→ Evidence
→ Verification
→ Acceptance
```

including controlled failure and recovery.

---

# Capability Progression

The implementation is designed to progress through:

```text
Level 0
Deterministic tooling

Level 1
Bounded task execution

Level 2
Verified autonomous implementation

Level 3
Multi-step workflows

Level 4
Multi-agent development pipeline

Level 5
Subjective/artistic/design judgement
```

Level 5 is intentionally deferred.

---

# Post-MVP

Only after the MVP has demonstrated reliable control-plane behavior should the system expand toward game development.

Future domains include:

```text
GameProject
Addon
Gamemode
Map
Asset
Material
Model
SoundAsset
LuaModule
RuntimeScenario
```

Future execution boundaries include:

```text
Garry's Mod runtime
Hammer
Blender
Image/material tooling
Audio tooling
Artifact registry
CI
```

Specialist agents may eventually include:

```text
Orchestrator
Game Developer
Level Designer
Blender Modeller
Texture/Materials Specialist
Sound Designer/Engineer
```

Specialist agents must communicate through controlled contracts and artifacts.

They must not receive unrestricted authority over each other's environments.

---

# Parallelism

The initial implementation is sequential.

Parallel execution is not an MVP requirement.

Parallelism may only be introduced after:

* task state is reliable
* persistence is reliable
* verification is reliable
* recovery is reliable
* scope enforcement is reliable
* auditability is reliable

Parallelism must never be used to compensate for weak orchestration.

---

# Level 5

Subjective artistic and design judgement is explicitly the final layer.

It must not be used to bypass:

* security
* policy
* verification
* scope
* recovery
* evidence
* human authority

Level 5 will eventually require a separate evaluation model capable of handling concepts such as:

* artistic coherence
* atmosphere
* gameplay feel
* historical plausibility
* visual consistency
* sound identity
* player experience

Those concerns are deliberately outside the initial implementation.

---

# Task Execution Protocol

For every task:

```text
1. Read task
2. Check dependencies
3. Inspect repository
4. Check Git state
5. Implement only declared scope
6. Add/update tests
7. Build if required
8. Test if required
9. Verify scope
10. Review security boundaries
11. Report evidence
12. Stop
```

---

# Task Completion Rule

A task is complete only when its Definition of Done is satisfied.

A task is not complete merely because its source code exists.

The following are distinct:

```text
Implementation
Verification
Acceptance
```

They must not be conflated.

---

# Copilot Task Prompt Template

Use the following structure when assigning a task to Copilot:

```text
TASK ID:
TASK TITLE:

PURPOSE:

DEPENDENCIES:

PRECONDITIONS:

CONTEXT:

SCOPE:

OUT OF SCOPE:

ARCHITECTURE CONSTRAINTS:

IMPLEMENTATION REQUIREMENTS:

SECURITY REQUIREMENTS:

TEST REQUIREMENTS:

FAILURE CASES:

EVIDENCE REQUIREMENTS:

ACCEPTANCE CRITERIA:

DEFINITION OF DONE:

NEXT TASK:

IMPLEMENTATION RULE:

Implement only this task.

Inspect the repository first.

Do not implement future tasks.

Do not invent missing architecture.

Do not weaken security boundaries.

If a dependency, requirement, or architectural decision is ambiguous or contradictory, STOP and report the issue.

At completion, provide:
- implementation summary
- files changed
- tests
- build result
- security review
- architecture review
- acceptance criteria status
- known limitations
- next task

Then stop.
```

---

# BOOTSTRAP-001

## Task

Establish the repository's durable implementation constitution.

## Scope

Create:

```text
AGENTS.md
.github/copilot-instructions.md
docs/implementation/IMPLEMENTATION-PLAN.md
```

Create directories required by those files.

Do not create application source code.

Do not create the .NET solution.

Do not add NuGet packages.

Do not implement Core/Application/Infrastructure/Host.

Do not implement the agent runtime.

Do not implement the orchestrator.

Do not implement tools.

Do not implement persistence.

---

## Acceptance Criteria

### AC-001

`AGENTS.md` exists at repository root.

### AC-002

`.github/copilot-instructions.md` exists.

### AC-003

`docs/implementation/IMPLEMENTATION-PLAN.md` exists.

### AC-004

The documents describe the architectural dependency direction:

```text
Core ← Application ← Infrastructure ← Host
```

### AC-005

The documents explicitly establish that:

```text
LLMs are reasoning components, never authorities.
```

### AC-006

The documents establish capability → policy → tool boundaries.

### AC-007

The documents establish trust boundaries.

### AC-008

The documents establish Git safety requirements.

### AC-009

The documents establish verification semantics:

```text
PASS
FAIL
UNKNOWN
```

and:

```text
UNKNOWN != PASS
```

### AC-010

The implementation plan establishes the complete implementation sequence.

### AC-011

No application implementation is introduced.

### AC-012

No dependency is added.

### AC-013

Git status contains only expected bootstrap changes.

---

## Definition of Done

BOOTSTRAP-001 is complete when all acceptance criteria pass and the repository contains the durable instructions required to safely begin `FOUNDATION-001`.

After completion:

**STOP.**

Do not begin `FOUNDATION-001`.

---

# FOUNDATION-001

The first actual software implementation task after bootstrap is:

```text
FOUNDATION-001
```

Its detailed task contract must be provided separately.

Do not implement FOUNDATION-001 as part of BOOTSTRAP-001.
