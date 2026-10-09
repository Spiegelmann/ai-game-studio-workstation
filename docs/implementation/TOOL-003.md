# TOOL-003 — Workspace Registry & Trusted Workspace Registration

## Objective

Implement the next workspace-management layer of AI Game Studio Workstation, building on the workspace contracts established by TOOL-001 and the path-resolution boundary established by TOOL-002.

The objective is to provide a controlled mechanism for registering workspace definitions, retrieving registered workspaces, and establishing the trusted configuration that later tool-pipeline stages will use.

A registered workspace must not automatically authorize an operation. Registration, resource containment, policy evaluation, and execution remain distinct responsibilities.

## 0. Mandatory Specification and Architecture Audit

Before making changes, inspect:

* The supplied `TOOL-003.md`.
* The checked-in implementation sequence and task dependencies.
* `TOOL-001-IMPLEMENTATION.md` and `TOOL-002-IMPLEMENTATION.md`.
* Existing workspace domain contracts and `WorkspaceResourceId`.
* The Application path-resolver contract and Infrastructure implementation.
* Existing task identity, scope, correlation, and policy abstractions.
* Existing Core, Application, Infrastructure, and Host project boundaries.
* Existing tests, architectural checks, and repository conventions.

**The checked-in TOOL-003 specification and implementation sequence are authoritative.**

If they define a different title, scope, or set of responsibilities from this prompt, follow the repository specification and report the discrepancy. Do not implement an invented registry design merely because this prompt suggests one.

Do not begin TOOL-004 or any subsequent task.

## 1. Establish Workspace Registration Contracts

Within the scope authorized by the checked-in specification, establish the contracts required to register and retrieve workspaces.

Where applicable, support:

* A stable workspace identifier.
* An immutable workspace descriptor.
* The configured workspace root and permitted boundaries.
* The applicable environment profile.
* The associated task scope.
* The trusted origin of the registration.
* An explicit registration lifecycle or status, if required by the existing design.

Reuse TOOL-001 value objects and contracts rather than introducing duplicate workspace identities, path identifiers, or scope types.

Registration metadata must distinguish the identity of a workspace from the authority to trust its configuration.

Do not assume that a descriptor is trustworthy merely because it is immutable or structurally valid.

## 2. Define Registry Responsibilities

Implement only the registry responsibilities specified by TOOL-003.

If the specification calls for a workspace registry, its contract should provide the necessary operations to:

* Register a workspace through an explicitly trusted registration boundary.
* Retrieve a registered workspace by stable identity.
* Determine whether a workspace is registered and available for subsequent evaluation.
* Handle duplicate registrations deterministically.
* Represent missing or invalid registrations explicitly.
* Apply any lifecycle or update restrictions required by the specification.

Use narrow interfaces and explicit result types consistent with the existing Application architecture.

Do not introduce unnecessary generic repository abstractions or a parallel domain model.

If the specification requires only contracts and not an implementation, do not introduce a concrete registry prematurely. Conversely, if a concrete implementation is required, place it in the appropriate layer and test its behavior.

## 3. Establish the Trusted Registration Boundary

A workspace registry must not allow an untrusted agent or operation request to establish new filesystem authority.

Ensure that the implementation follows the existing security model:

* Workspace registration is separate from an ordinary workspace operation request.
* An operation request cannot register a new workspace as a side effect.
* An agent cannot expand permitted roots or protected-path definitions through request metadata.
* Registration provenance is explicit wherever required by the existing trust model.
* Unknown or untrusted registration provenance is not silently treated as trusted.
* Duplicate identifiers cannot silently replace an existing trusted registration.
* Updates to trusted configuration follow an explicit contract rather than implicit mutation.

Do not invent a new trust hierarchy or alter POLICY-001 through POLICY-006.

Use the existing policy domain when a policy decision is required. Do not duplicate authorization logic inside the registry.

## 4. Integrate with TOOL-002 Without Bypassing Containment

The registry must provide the trusted configuration needed by subsequent workspace-resolution operations.

Preserve the distinction between:

1. Workspace registration.
2. Retrieval of trusted workspace configuration.
3. Lexical resource validation.
4. Physical path resolution and containment.
5. Resource classification.
6. Policy evaluation.
7. Execution.

Do not treat registry membership as proof that a requested physical path is contained within the workspace.

Do not allow the registry to bypass `PhysicalWorkspacePathResolver` or equivalent existing resolution contracts.

Preserve TOOL-002's conservative behavior for unsupported paths, resolution errors, and indeterminate containment.

Do not claim that path-based checks eliminate TOCTOU races.

## 5. Immutability, Identity, and Determinism

Follow the existing domain conventions.

Verify that:

* Registered workspace descriptors cannot be mutated through retained collection references.
* Workspace identifiers have stable equality semantics.
* Duplicate registration behavior is deterministic.
* Registration and retrieval results have explicit outcomes.
* Collection ordering, if exposed, follows a documented contract.
* Concurrent or competing registration attempts are handled according to the specified implementation model.
* Missing registrations never produce fabricated or default trusted workspaces.

Do not introduce persistence, concurrency infrastructure, or distributed coordination unless TOOL-003 explicitly requires it.

## 6. Configuration and Lifecycle

Follow the checked-in specification regarding workspace configuration and lifecycle.

If registration requires configuration from a trusted host boundary, ensure that workspace roots and permitted boundaries cannot be supplied or overridden by ordinary agent requests.

Do not hardcode developer-specific absolute paths, Windows drive letters, machine-specific directories, or repository locations.

Do not assume that a workspace root is physically valid merely because it is registered. Physical validation must use the existing resolver contract where required.

Do not implement a general configuration-management framework.

## 7. Testing Requirements

Add focused tests appropriate to the implementation required by TOOL-003.

At minimum, cover the applicable cases:

* Valid workspace registration.
* Duplicate workspace identifiers.
* Retrieval of registered and missing workspaces.
* Invalid or incomplete descriptors.
* Registration provenance and trust boundaries.
* Attempts to replace or expand trusted workspace configuration.
* Immutable descriptors and defensive collection handling.
* Deterministic registration outcomes.
* Compatibility with TOOL-001 contracts.
* Integration with the TOOL-002 resolution contract.
* Separation of registration from authorization and execution.
* Preservation of existing policy and task-domain behavior.

If the registry has a concrete implementation, test its actual behavior rather than testing only its interfaces.

If concurrency is in scope, test competing registrations and updates using the implementation's documented guarantees.

Tests must not depend on developer-specific paths or external services.

## 8. Architecture and Dependency Rules

Preserve the established dependency direction:

* Core owns workspace-domain concepts and invariants.
* Application owns workspace registry and orchestration-facing contracts where appropriate.
* Infrastructure owns concrete implementations when required.
* Host owns composition and trusted runtime configuration.

Do not introduce filesystem-provider dependencies into Core.

Do not introduce unnecessary NuGet packages.

Do not add unrelated persistence, telemetry, execution, scheduling, authorization, or external service integrations.

Do not modify existing task lifecycle or policy behavior unless explicitly required by the authoritative TOOL-003 specification.

## 9. Documentation

Add `TOOL-003-IMPLEMENTATION.md`, unless the checked-in specification explicitly defines a different documentation convention.

Document:

* The registry's responsibilities and boundaries.
* The registration and retrieval contracts.
* The distinction between workspace identity and registration trust.
* Duplicate registration and update semantics.
* The relationship with TOOL-001 and TOOL-002.
* How trusted configuration reaches subsequent pipeline stages.
* Any concurrency guarantees or lifecycle restrictions.
* Security limitations and deferred work.
* Any discrepancy between this prompt and the checked-in specification.

Preserve the original task specification and unrelated untracked files unchanged.

## Acceptance Criteria

* [ ] The checked-in TOOL-003 specification and implementation sequence were inspected first.
* [ ] The implemented scope matches the authoritative repository specification.
* [ ] Existing TOOL-001 and TOOL-002 contracts remain compatible.
* [ ] Workspace registration and retrieval follow explicit contracts.
* [ ] Trusted configuration cannot be silently replaced or expanded by an ordinary agent request.
* [ ] Duplicate registration behavior is explicit and deterministic.
* [ ] Missing or invalid registrations do not create implicit trusted workspaces.
* [ ] Registration remains separate from containment, authorization, and execution.
* [ ] Existing policy semantics remain unchanged.
* [ ] Architectural dependency direction is preserved.
* [ ] Tests cover the required registration, trust, identity, and integration behavior.
* [ ] `TOOL-003-IMPLEMENTATION.md` is added when required by repository conventions.
* [ ] No unrelated task or architecture changes are introduced.
* [ ] Full solution tests pass.
* [ ] Full solution build succeeds with zero warnings and errors.
* [ ] `git diff --check` passes.
* [ ] Changed C# files have no reported diagnostics.
* [ ] All limitations and deviations are reported accurately.

## Required Verification

Run:

1. `dotnet test AiGameStudioWorkstation.sln --verbosity minimal`
2. `dotnet build AiGameStudioWorkstation.sln`
3. `git diff --check`
4. C# diagnostics for every changed C# file.

Inspect the final diff. Confirm that the supplied task specification and unrelated untracked files remain unchanged.

Report exact test counts, build warnings and errors, files created or modified, architecture findings, security limitations, and any deviations from the authoritative specification.

## Execution Instructions

Work autonomously within the repository.

Begin with the mandatory specification and architecture audit. Implement the smallest cohesive solution that satisfies the actual TOOL-003 acceptance criteria.

Do not redesign existing abstractions or introduce speculative functionality for later phases.

If the task specification leaves a security-sensitive behavior undefined, preserve the existing conservative security posture and document the unresolved decision rather than silently granting additional authority.

Stop after TOOL-003. Do not begin TOOL-004 or any subsequent task.
