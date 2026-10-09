# TOOL-003 Implementation — Workspace Registry & Trusted Workspace Registration

## Responsibilities and boundaries

`IWorkspaceRegistrar` in Application defines the registration capability, while read-only `IWorkspaceRegistry` defines lookup. `InMemoryWorkspaceRegistry` in Infrastructure implements both and stores registrations for the lifetime of the process. Host composition should retain the registrar and give ordinary operation consumers only the lookup interface. The registry is first-write-wins: duplicate workspace identifiers return `Duplicate` and never replace existing configuration. There is no update or removal operation.

Registration contains the existing immutable `WorkspaceDescriptor`, the configured physical root, and an explicit registration origin. Only `HostConfiguration` is accepted. `AgentRequest` and `Unknown` origins are rejected. A missing lookup returns `NotFound` with no registration; no default workspace is fabricated.

The registration origin is provenance metadata supplied by the caller, not an independently authenticated identity. The registry must be wired and called only from trusted Host configuration. No agent-facing registration route or host composition for workspace configuration exists yet, so provenance authentication remains a Host responsibility for a later integration task.

## Relationship with TOOL-001 and TOOL-002

The descriptor reuses TOOL-001 workspace identities, resource boundaries, environment profile, and task scope. The registry does not duplicate or mutate those contracts. It provides the configured physical root alongside the descriptor for callers that construct `WorkspacePathResolutionRequest` under TOOL-002.

Registration does not validate filesystem existence or establish physical containment. `PhysicalWorkspacePathResolver` remains responsible for physical path resolution and conservative containment outcomes. Registry membership does not classify a resource, evaluate policy, authorize an operation, or execute it. The operation request cannot change a stored descriptor or physical root because registration is a separate API.

Path resolution remains subject to the documented path-based TOCTOU limitations in `TOOL-002-IMPLEMENTATION.md`.

## Immutability, determinism, and concurrency

Registration values are immutable and retain the immutable descriptor, whose collections are defensively copied. Workspace identifier equality follows the existing `WorkspaceId` record equality semantics. Registry reads and writes are guarded by one lock; each registration is atomic, and exactly one concurrent host registration for an identifier can win. Which contender wins is the order in which it acquires the lock. Subsequent attempts deterministically return `Duplicate` and cannot replace the winner.

The in-memory registry does not provide persistence, distributed coordination, update, or removal. Restarting the process loses all entries.

## Security limitations and deferred work

The registry's origin enum prevents accidental acceptance of explicitly marked untrusted/unknown registrations; it is not a security token and cannot prove that a caller claiming `HostConfiguration` is actually trusted. Host wiring must preserve that boundary. Registration accepts a non-empty physical-root string without checking whether it is fully qualified, exists, or is free of links; TOOL-002 performs that validation at resolution time and fails conservatively.

This task adds no policy logic, authorization, filesystem mutation, process execution, persistence, or host configuration framework. TOOL-003 has no separate architecture documents checked in under `docs/architecture`; `AGENTS.md`, the implementation plan, and the TOOL-001/TOOL-002 contracts were used as the governing architecture materials.
