# TOOL-001 — Workspace Contract & Semantic Filesystem Boundary

## Objective

Establish the foundational workspace contract for AI Game Studio Workstation.

Define the domain model and Application-facing abstractions that describe which workspace a tool operation may access, which resources are within scope, and which resource classifications must be handled conservatively.

This task establishes contracts only. It must not implement unrestricted filesystem access, tool execution, operating-system integration, or the complete security enforcement pipeline.

Build on the existing Core policy domain, including POLICY-001 through POLICY-006.

## Architectural Context

The workstation uses a capability-based tool architecture:

ToolRequest → validation and registration → workspace/resource/scope checks → policy evaluation → budget and execution limits → controlled execution → evidence and ToolResult.

The workspace contract is one boundary within this architecture. It does not independently authorize operations or replace policy evaluation.

Agents and language models must never receive unrestricted filesystem access. Filesystem operations must eventually be mediated by a controlled Tool Gateway.

Project files, generated assets, documentation, logs, Lua scripts, dependency contents, tool output, and other workspace data are untrusted inputs. Their contents must not grant additional permissions or alter the workstation's security boundaries.

## 1. Inspect the Existing Architecture

Before implementation, inspect:

* The solution structure and project references.
* Core domain conventions and value objects.
* Application contracts and dependency direction.
* Existing policy types and evaluation semantics.
* Task identity, scope, and correlation abstractions.
* Existing tests, architectural tests, and repository conventions.
* The checked-in TOOL-001 specification and the implementation sequence.

Reuse existing concepts when their semantics match the requirements. Do not create duplicate identities, scope models, or result abstractions unnecessarily.

Identify the appropriate projects and namespaces before adding files.

Do not modify unrelated task lifecycle, persistence, policy, or infrastructure code.

## 2. Define Workspace Identity

Introduce an immutable workspace identity and descriptor, consistent with the existing architecture.

The workspace contract must represent the information required to determine the intended workspace boundary:

* A stable workspace identifier.
* The designated repository/workspace root.
* Explicitly permitted roots or resource boundaries, where applicable.
* Protected paths or protected resource classifications.
* An environment profile or equivalent execution-context identity.
* The applicable task scope or a reference to the existing task-scope abstraction.

Distinguish workspace identity from a physical filesystem path.

The domain model must not assume that a supplied path is trustworthy merely because it appears inside a workspace descriptor.

Use existing value-object validation conventions. Reject malformed or internally inconsistent definitions without relying on operating-system access from Core.

Do not invent machine-specific absolute paths, user directories, drive letters, or repository locations.

## 3. Define Semantic Workspace Operations

Define the minimum workspace operations supported by the MVP.

Required operations:

* `Read`: request access to an existing resource for reading.
* `List`: request a listing of resources within a designated workspace boundary.
* `Write`: request creation or modification of a resource.
* `CreateDirectory`: request creation of a directory within an authorized workspace boundary.

Represent operations explicitly using domain-appropriate types. Avoid accepting arbitrary operation names as strings when a closed type can express the contract.

The contract must distinguish the requested operation from whether that operation is authorized or has actually executed.

The initial operation model must not include generic filesystem deletion, arbitrary move/copy operations, process execution, unrestricted shell commands, or arbitrary host filesystem access.

Do not implement filesystem access in this task.

## 4. Define Resource Classification

Introduce an explicit resource-classification model suitable for later policy and security enforcement.

It must support, at minimum:

* Known workspace resources.
* Protected resources.
* Resources outside the permitted workspace boundary.
* Unknown or insufficiently classified resources.

The exact names and structure should follow existing domain conventions.

Unknown classifications must be represented explicitly. They must not silently become permitted resources.

Resource classification is descriptive input to subsequent validation and policy enforcement; it is not authorization by itself.

Do not attempt to classify arbitrary file contents by executing, parsing, or trusting their instructions.

## 5. Define Workspace Operation Requests

Define an immutable request model that expresses the intent of a workspace operation.

The model should carry the information required by subsequent pipeline stages, including:

* Workspace identity.
* Operation type.
* Requested resource or relative resource identifier.
* Relevant task or operation correlation identity, reusing existing abstractions where appropriate.
* Any explicitly required operation metadata.

Keep the request semantic rather than exposing raw operating-system handles or unrestricted filesystem APIs.

Do not include credentials, access tokens, secrets, or ambient authority in the request.

Do not treat a request supplied by an agent as trusted merely because it is structurally valid.

Validation of a request's structure must remain distinct from authorization to perform the requested operation.

## 6. Define Results and Failure Semantics

If required by the existing architecture, define narrow result types that distinguish contract validation failures from authorization decisions and execution outcomes.

Do not introduce an overly broad result framework if the repository already provides suitable abstractions.

The contract must make it possible for subsequent stages to distinguish at least:

* Invalid or incomplete workspace identity.
* Invalid operation requests.
* Unknown resource classification.
* Requests that require further security evaluation.
* Valid requests that are ready for subsequent pipeline evaluation.

Do not claim that an operation succeeded merely because its request passed validation.

Do not implement policy decisions independently of `PolicyEvaluator`.

Do not create new authorization semantics that conflict with POLICY-001 through POLICY-006.

## 7. Security Invariants

The workspace contract must preserve the following invariants:

1. An agent cannot expand its own workspace permissions.
2. A request cannot redefine trusted workspace roots or protected-path rules.
3. Relative resource identifiers do not constitute proof that the eventual filesystem target lies inside the workspace.
4. Unknown resource classification is not equivalent to permission.
5. Structural validation is not authorization.
6. Authorization is not execution.
7. Workspace identity does not imply that a resource is safe to consume.
8. Untrusted workspace contents cannot modify the policy evaluator, registry, gateway, or security configuration through their textual instructions.
9. A workspace operation cannot silently escalate from a semantic operation to arbitrary filesystem or process access.
10. The Core/domain contract remains independent of filesystem providers and operating-system APIs.

Path normalization, canonical path resolution, symbolic-link/reparse-point handling, and authoritative containment enforcement must be addressed by the appropriate subsequent implementation task. Do not falsely claim that TOOL-001 solves filesystem traversal or link-based escape vulnerabilities.

## 8. Architecture and Dependency Rules

Follow the repository's established architectural boundaries and dependency direction.

* Core owns domain concepts and invariants.
* Application owns orchestration-facing contracts when appropriate to the existing structure.
* Infrastructure owns eventual filesystem-specific implementations.
* Host owns composition and runtime configuration.

Do not introduce infrastructure dependencies into Core.

Do not introduce new NuGet packages.

Do not introduce filesystem execution, process spawning, shell access, network access, persistence, telemetry, or external tool integrations.

Do not implement TOOL-002 or subsequent tasks as part of this change.

## 9. Testing Requirements

Add focused unit tests following the repository's existing conventions.

Cover:

* Workspace identity validation.
* Immutable workspace descriptors and collections.
* Explicit representation of protected and unknown resources.
* Valid and invalid semantic operation requests.
* Rejection of malformed or incomplete request data.
* Prevention of implicit operation expansion.
* Separation of request validation from policy decisions and execution outcomes.
* Reuse of existing scope and correlation abstractions.
* Compatibility with the policy domain introduced in POLICY-001 through POLICY-006.
* Architectural dependency constraints where corresponding tests already exist.

Tests must verify observable behavior rather than private implementation details.

Do not create tests that require a real filesystem or operating-system integration unless the repository already has a justified test abstraction for contract-level testing.

## 10. Documentation

Add `TOOL-001-IMPLEMENTATION.md`.

Document:

* The workspace contract and its responsibilities.
* Workspace identity versus physical filesystem location.
* The supported semantic operations and excluded operations.
* Resource classifications and conservative handling of unknown resources.
* Validation versus authorization versus execution.
* Architectural ownership and dependency direction.
* Security invariants and limitations.
* Deferred canonical path resolution and filesystem containment enforcement.
* The intended integration points for TOOL-002 and the subsequent tool pipeline.

Preserve existing task specifications and unrelated untracked files unchanged.

## Acceptance Criteria

* [ ] Existing architecture and task specifications were inspected before implementation.
* [ ] An immutable workspace identity/descriptor exists.
* [ ] Workspace identity is distinct from physical filesystem trust.
* [ ] Read, List, Write, and CreateDirectory are represented explicitly.
* [ ] Unsupported destructive or execution-oriented operations are not implicitly enabled.
* [ ] Resource classification explicitly supports protected, out-of-boundary, and unknown states.
* [ ] Operation requests are immutable and structurally validated.
* [ ] Validation does not imply authorization or execution.
* [ ] Existing policy and task abstractions are reused where appropriate.
* [ ] Core remains independent of filesystem providers and operating-system APIs.
* [ ] No new dependencies are introduced.
* [ ] Focused tests cover the workspace contract and its security invariants.
* [ ] `TOOL-001-IMPLEMENTATION.md` is added.
* [ ] No unrelated architecture or behavior is changed.
* [ ] Full solution tests pass.
* [ ] Full solution build passes with zero warnings and errors.
* [ ] `git diff --check` passes.
* [ ] Changed C# files have no reported diagnostics.
* [ ] The final report clearly identifies limitations and deferred security enforcement.

## Required Verification

Run:

1. `dotnet test AiGameStudioWorkstation.sln --verbosity minimal`
2. `dotnet build AiGameStudioWorkstation.sln`
3. `git diff --check`
4. C# diagnostics for all changed C# files.

Inspect the final diff and confirm that pre-existing task specifications and unrelated untracked files remain untouched.

Report the actual test counts, build warnings/errors, changed files, architecture checks, security limitations, and any deviations from the task scope.

## Execution Instructions

Work autonomously within the repository.

Start with an audit of the existing architecture and the checked-in TOOL-001 specification. Resolve naming and ownership questions using existing repository conventions rather than inventing parallel abstractions.

Implement the smallest cohesive set of contracts and tests that satisfies this specification.

If an acceptance criterion conflicts with an existing established contract, explain the conflict and preserve the existing architecture rather than silently redesigning it.

Do not begin TOOL-002 or any other subsequent task.
