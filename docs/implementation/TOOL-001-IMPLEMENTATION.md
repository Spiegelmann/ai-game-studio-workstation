# TOOL-001 Implementation — Workspace Contract & Semantic Filesystem Boundary

## Workspace contract

Core defines immutable workspace identifiers and a `WorkspaceDescriptor` containing a logical root identity, permitted relative resource roots, protected relative resources, protected resource types, an environment-profile identity, and the existing `TaskScope`.

`WorkspaceId` and `WorkspaceRootId` identify a configured workspace boundary. They are not operating-system paths, do not resolve to a physical location in this task, and do not establish that any supplied resource is safe or contained. `WorkspaceResourceId` is a relative semantic identifier; it rejects obviously rooted values and `.`/`..` segments but does not normalize or resolve a filesystem path.

Descriptor collections are copied and exposed read-only. The descriptor's `TaskScope` reuses the existing Core abstraction rather than defining another task-scope model.

## Operations and requests

The supported semantic operations are `Read`, `List`, `Write`, and `CreateDirectory`. The closed operation enum does not include delete, move, copy, process execution, shell execution, or arbitrary filesystem operations.

`WorkspaceOperationRequest` identifies a workspace, operation, relative resource, descriptive resource classification, and existing operation/correlation identifiers. It carries no descriptor, roots, protected-path rules, credentials, or ambient authority, so a request cannot redefine those workspace boundaries.

Constructors reject malformed identities, paths, enum values, missing references, and invalid descriptor collections. This is structural validation only. A successfully constructed request is not authorized and has not executed. Classification is descriptive input, including explicit `Unknown`; it is not trusted as proof of containment or permission. Subsequent stages must independently resolve the registered workspace, enforce task and workspace boundaries, evaluate policy, and execute through a controlled gateway.

## Resource classifications

`WorkspaceResourceType` supports Source, Test, Config, Generated, Build, Temp, Evidence, External, and Unknown. `WorkspaceResourceBoundary` separately represents WithinPermittedWorkspace, Protected, OutsidePermittedWorkspace, and Unknown. Keeping type and boundary separate allows protected/outside/unknown state to remain explicit without implying that a type alone authorizes an operation.

Unknown classification is never treated as permission. The contract does not infer classifications from file contents or trust project-provided instructions.

## Ownership and security boundaries

Workspace identity, operations, classifications, and request structure live in Core. The types depend only on existing Core task contracts; Core remains independent of Application, Infrastructure, Host, filesystem providers, and operating-system APIs. No application service is added because this task introduces no orchestration or operation execution.

This task does not implement path canonicalization, symlink/reparse-point handling, authoritative filesystem containment, policy evaluation, permission decisions, or execution. Lexical path checks only reject obviously non-relative identifiers and are not a traversal defense. Those controls must be implemented and verified by subsequent workspace/tool-pipeline work. A valid contract object is not an authorization token or execution result.

## Verification

Focused Core tests cover identifier and request validation, immutable descriptor collections, scope reuse, explicit resource classifications, the closed operation set, and unknown-classification preservation. No filesystem is accessed by these tests.
