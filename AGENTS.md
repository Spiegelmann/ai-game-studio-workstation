# AI Game Studio Workstation — Repository Constitution

## Purpose

This repository contains the implementation of an AI-assisted and progressively autonomous development workstation for Garry's Mod game projects.

The system is designed to support controlled autonomous software development while preserving:

* human authority
* security
* auditability
* deterministic policy enforcement
* bounded execution
* recoverability
* verification
* evidence
* maintainability
* architectural integrity

The system must remain safe and useful even if an LLM is unavailable, incorrect, manipulated, or maliciously influenced.

---

## Core Principle

LLMs and agents are reasoning components.

They are **never authorities**.

The following boundaries are fundamental:

```text
Agent
  ↓ requests
Capability
  ↓ checked by
Policy
  ↓ authorizes
Tool
  ↓ executes
Evidence
  ↓ evaluated by
Verification
  ↓ determines
Task outcome
```

An LLM may propose an action.

It may not authorize its own action.

---

## Architectural Dependency Direction

The core dependency direction is:

```text
Core
  ↑
Application
  ↑
Infrastructure
  ↑
Host
```

In implementation terms:

```text
Core ← Application ← Infrastructure ← Host
```

Rules:

* Core must not reference Infrastructure.
* Core must not reference Host.
* Application may depend on Core.
* Infrastructure may depend on Application and Core.
* Host composes the application.
* Domain logic belongs in Core.
* Use-case orchestration belongs in Application.
* External implementations belong in Infrastructure.
* Composition and startup belong in Host.

Do not introduce dependencies that violate this direction.

---

## Domain Authority

The canonical domain concepts are:

* Task
* TaskAttempt
* ToolInvocation
* StateTransition
* EvidenceRecord
* Checkpoint
* RecoveryDecision
* HumanDecision

These concepts represent the control plane of autonomous execution.

---

## Task State

Tasks use the following states:

```text
PROPOSED
READY
CLAIMED
IN_PROGRESS
VERIFICATION
ACCEPTED
BLOCKED
FAILED
CANCELLED
RECOVERING
HUMAN_REVIEW_REQUIRED
```

Rules:

* Dependencies must be satisfied before a task becomes READY.
* An agent cannot directly assign ACCEPTED.
* Acceptance requires successful verification.
* Terminal states cannot silently mutate.
* Invalid state transitions must be rejected.
* CANCELLED tasks must not spontaneously resume.
* UNKNOWN execution outcomes must remain UNKNOWN until resolved.

---

## Trust Model

The system recognizes these trust classes:

```text
SYSTEM_TRUSTED
POLICY_TRUSTED
TASK_TRUSTED
PROJECT_DATA
TOOL_OUTPUT
EXTERNAL_DATA
UNTRUSTED
```

Project content, Lua files, comments, documentation, logs, generated files, Git metadata, dependencies, model output, tool output, external references, and similar content must never automatically become instructions or authority.

Data remains data.

Trust and provenance must survive context assembly.

---

## Capability Model

Agents never receive unrestricted machine access.

Agents request capabilities.

Initial capabilities include:

```text
Workspace.Read
Workspace.List
Workspace.Write
Git.ReadStatus
Git.ReadHead
Git.ReadDiff
DotNet.Build
DotNet.Test
```

A capability request follows:

```text
Agent
→ request validation
→ task scope
→ capability registry
→ policy
→ ALLOW / DENY / REQUIRE_APPROVAL / DEFER
→ tool gateway
→ tool
→ result
→ evidence
```

The model's reasoning must never grant itself permission.

No dynamic capabilities may be invented by an agent.

---

## Workspace Security

The workspace is not a generic unrestricted filesystem API.

Filesystem access must be:

* canonicalized
* repository-bounded
* task-scope-bounded
* resource-type-aware
* policy-checked

Relevant resource classes include:

```text
SOURCE
TEST
CONFIG
GENERATED
BUILD
TEMP
EVIDENCE
EXTERNAL
UNKNOWN
```

Do not implement arbitrary:

* delete
* move
* copy
* recursive filesystem manipulation
* unrestricted filesystem traversal

unless explicitly authorized by a later architectural task.

Writes should use safe/atomic mechanisms where appropriate.

---

## Process Security

Do not expose an arbitrary shell to agents.

Process execution must eventually use structured semantic command profiles.

A process request contains controlled information such as:

* executable
* arguments
* working directory
* environment
* timeout
* cancellation
* resource limits

Never allow model-generated shell syntax to become unrestricted host execution.

---

## Git Safety

Git is part of the source-of-truth boundary.

The MVP supports read operations such as:

```text
Git.ReadStatus
Git.ReadHead
Git.ReadDiff
```

The system must never silently destroy user work.

Do not introduce:

```text
git reset --hard
git clean -fd
force push
history rewriting
destructive checkout
```

as autonomous capabilities.

Unexpected dirty state must be preserved and escalated.

---

## Verification

Verification is independent from agent self-report.

Verification results are:

```text
PASS
FAIL
UNKNOWN
```

Critical invariant:

```text
UNKNOWN != PASS
```

An agent saying:

> "The implementation works."

is not evidence of correctness.

Acceptance requires evidence from appropriate verification mechanisms.

---

## Recovery

Failures must be classified.

Relevant failure types include:

```text
VALIDATION_FAILURE
POLICY_FAILURE
SCOPE_FAILURE
ENVIRONMENT_FAILURE
TOOL_FAILURE
PROCESS_FAILURE
TIMEOUT
CANCELLATION
BUILD_FAILURE
TEST_FAILURE
DEPENDENCY_FAILURE
STATE_FAILURE
GIT_FAILURE
EXTERNAL_FAILURE
SECURITY_FAILURE
UNKNOWN_FAILURE
```

Recovery decisions include:

```text
RETRY
RESUME
ROLLBACK
RESTART
ESCALATE
ABANDON
```

Rules:

* Retry budgets must be bounded.
* Repair is not equivalent to retry.
* Repair must create a new controlled execution.
* Recovery cannot increase authority.
* Unknown non-idempotent outcomes must not be blindly retried.
* Crashes must not corrupt authoritative state.
* Recovery state must survive process/LLM failure.

---

## Auditability

Every meaningful autonomous action must have an auditable identity.

Important identities include:

* TaskId
* AttemptId
* InvocationId
* AgentId
* EvidenceId
* CheckpointId
* StateTransitionId

Do not use ambiguous logs as the authoritative state model.

---

## Context Assembly

Agents must not automatically receive:

* the entire repository
* the entire Git history
* the entire conversation
* unrestricted tool output
* unrelated project files

Context must be deliberately assembled according to relevance and trust.

Expected context categories include:

```text
System invariants
Policy
Agent contract
Task contract
Architecture
Relevant project data
Evidence
Tool results
Previous attempts
External references
```

---

## Model Boundary

The model interface must remain provider-neutral.

The architecture should support an abstraction equivalent to:

```text
IAgentModel
ModelRequest
ModelResponse
```

Model output is untrusted data.

Structured output is required wherever the model communicates decisions or actions to the system.

Free-form prose must never become authority.

---

## Orchestrator Boundary

The Orchestrator determines:

> What happens next?

The Agent determines:

> How should this task be approached?

Policy determines:

> Is this action permitted?

The Tool determines:

> How is this action safely executed?

Verification determines:

> Did it actually succeed?

Recovery determines:

> What should happen after failure?

Do not collapse these responsibilities into one service.

---

## MVP Scope

The first MVP is intentionally small.

It must not initially implement:

* Garry's Mod integration
* Blender automation
* Hammer automation
* texture generation
* material generation
* sound generation
* multi-agent parallel execution
* cloud infrastructure
* distributed execution
* subjective artistic judgement

The initial MVP focuses on the autonomous development control plane.

---

## Implementation Philosophy

Prefer:

* simple designs
* explicit contracts
* deterministic behavior
* strong boundaries
* small interfaces
* testable components
* immutable evidence/checkpoints
* clear failure states
* boring infrastructure
* explicit validation

Avoid:

* speculative abstractions
* premature plugin systems
* unnecessary frameworks
* unnecessary dependencies
* hidden magic
* global mutable state
* autonomous self-modifying architecture
* architecture implemented only because an LLM suggested it

---

## Agent Implementation Rule

An implementation agent must:

1. Inspect the existing repository.
2. Read the applicable architecture and task specification.
3. Implement only the assigned task.
4. Respect all dependencies.
5. Add appropriate tests.
6. Run required verification.
7. Report evidence.
8. Stop when the task is complete.

If requirements are ambiguous or contradictory:

**STOP.**

Do not invent architecture to resolve an architectural ambiguity.

---

## Scope Rule

A task is allowed to modify only its declared scope.

Unexpected changes must be treated as a problem.

Do not "clean up" unrelated files.

Do not refactor unrelated code.

Do not add convenience abstractions outside the task.

Do not install dependencies unless the task explicitly permits them.

---

## Security Rule

Security restrictions are architectural requirements, not suggestions.

Never weaken:

* capability boundaries
* policy boundaries
* workspace boundaries
* process restrictions
* Git protections
* trust boundaries
* verification requirements
* auditability

in order to make an implementation easier.

---

## Human Authority

Human authority remains above autonomous execution.

The system must be able to stop and request human review when:

* authorization is ambiguous
* security boundaries are threatened
* unexpected user changes are detected
* non-idempotent execution has an unknown outcome
* recovery budgets are exhausted
* architecture conflicts occur
* required capabilities do not exist
* verification cannot establish correctness

---

## Future Expansion

The architecture will eventually support:

* GameProject
* Addon
* Gamemode
* Map
* Asset
* Material
* Model
* SoundAsset
* LuaModule
* RuntimeScenario
* Garry's Mod runtime adapters
* specialist agents
* controlled artifact contracts
* multi-agent workflows
* runtime verification
* economic/scaling controls
* subjective/artistic evaluation

These are future capabilities.

Do not implement them prematurely.

---

## Final Invariants

The following invariants must remain true:

1. LLMs are reasoning components, never authorities.
2. Agents request capabilities; they never execute directly.
3. Policy authorizes capabilities independently of the model.
4. Tools independently validate invocations.
5. Filesystem access is bounded.
6. Process execution is structured.
7. Git cannot silently destroy user work.
8. Tool output is untrusted.
9. Project content is untrusted.
10. Trust and provenance survive context assembly.
11. Meaningful autonomous actions are auditable.
12. Every attempt is distinct from its task.
13. Unknown outcomes are explicit.
14. Retries are bounded.
15. Repair is a new controlled execution.
16. Recovery cannot increase authority.
17. Verification is independent.
18. UNKNOWN never becomes PASS automatically.
19. Acceptance requires evidence.
20. Human authority remains above autonomous execution.
21. Specialist agents communicate through controlled artifacts/contracts.
22. Parallelism comes only after sequential reliability.
23. Garry's Mod is an external runtime boundary.
24. Subjective Level 5 judgement comes last.
25. The system remains safe and useful if the LLM disappears.
