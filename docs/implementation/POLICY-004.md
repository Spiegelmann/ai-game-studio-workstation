# POLICY-004 — Capability Scope & Boundary Constraints

## Objective

Extend the Policy subsystem with a minimal, explicit model for **capability scope and boundary constraints**.

The Policy phase currently establishes:

* POLICY-001 — policy decisions and fail-closed semantics
* POLICY-002 — capabilities, action mapping, explicit denial, deterministic rule evaluation
* POLICY-003 — capability provenance and registration

POLICY-003 intentionally deferred resource scope.

POLICY-004 addresses that gap.

The objective is to allow policy to distinguish between:

> "This capability exists."

and:

> "This capability exists, but only within an explicitly bounded scope."

The result must remain:

* deterministic
* immutable
* provider-independent
* execution-independent
* authorization-independent
* persistence-independent
* infrastructure-independent
* small enough to reason about directly

This task must **not** implement filesystem permissions, sandboxing, OS security, authorization, resource discovery, or execution.

---

# 1. Read Before Changing Anything

Inspect the actual repository before modifying it.

Read:

* `AGENTS.md`
* all applicable repository instructions
* FOUNDATION implementation records
* PERSISTENCE implementation records
* `PERSISTENCE-005.md`
* `POLICY-001`
* `POLICY-001-IMPLEMENTATION.md`
* `POLICY-002`
* `POLICY-002-IMPLEMENTATION.md`
* `POLICY-003`
* `POLICY-003-IMPLEMENTATION.md`
* all current Core policy types
* all policy tests
* current Application contracts
* current architecture boundaries

Treat the repository as authoritative.

Do not assume the previous task specifications exactly describe the implementation.

---

# 2. Core Problem

The current policy model can establish:

```text
Action
+
Capability
+
Provenance
+
Trust classification
→
Policy decision
```

However, a capability currently does not necessarily establish **where its authority applies**.

For example:

```text
workspace.write
```

could conceptually mean:

```text
write anywhere
```

or:

```text
write only within the designated workspace
```

or:

```text
write only within a particular project
```

Policy must eventually be able to distinguish bounded authority from unrestricted authority.

POLICY-004 should introduce the smallest domain abstraction necessary to represent this distinction.

---

# 3. Scope Model

Introduce a domain concept representing capability scope.

A scope should describe the **boundary within which a capability registration is valid**.

Do not immediately model every possible resource type.

Prefer a minimal abstraction that can later support bounded policy without coupling Core to:

* Windows paths
* Linux paths
* Docker paths
* cloud resource IDs
* database IDs
* HTTP URLs
* Git repositories
* IDE workspaces
* MCP resources

The policy domain should represent scope conceptually rather than implementing resource access.

---

# 4. Global vs Bounded Scope

The model should explicitly distinguish between:

### Bounded scope

The capability applies only within a defined policy boundary.

Conceptually:

```text
workspace.write
scope = designated workspace
```

### Unbounded/global scope

The capability is not restricted by a resource boundary.

This is potentially more powerful and therefore must **not** be silently inferred.

If the current architecture cannot establish that an unbounded capability is safe, policy should remain conservative.

Do not automatically treat:

```text
scope = missing
```

as:

```text
global
```

This is an important fail-closed invariant.

---

# 5. Scope Identity

If a scope requires an identifier, establish a validated value object.

The identifier must be:

* immutable
* deterministic
* non-empty
* normalized according to explicit rules
* independent of infrastructure

Do not use raw strings throughout the evaluator if a dedicated value object is justified.

Do not invent provider-specific formats.

For example, do not assume that all scopes are filesystem paths.

The domain should be capable of expressing:

```text
workspace-A
project-B
resource-C
```

without knowing what those resources physically are.

---

# 6. Scope Matching

Establish deterministic semantics for determining whether a proposed action falls within a capability's scope.

The policy model should conceptually support:

```text
Capability scope
        +
Requested action scope
        ↓
Is the action inside the permitted boundary?
```

The result should be one of:

```text
MATCH
NO MATCH
UNKNOWN
```

or an equivalent domain representation.

Do not reduce all three cases to a boolean.

This distinction matters because:

* `NO MATCH` can represent a known boundary violation.
* `UNKNOWN` represents insufficient information.
* `MATCH` represents a valid bounded relationship.

---

# 7. Conservative Evaluation Semantics

Preserve the existing policy semantics:

```text
DENY > ESCALATE > ALLOW
```

Apply scope conservatively.

### Matching scope

If:

* action is known
* capability exists
* provenance is trusted
* capability scope matches requested scope
* no explicit deny applies

then:

```text
ALLOW
```

### Known scope mismatch

If:

* capability exists
* provenance is trusted
* requested action is outside the registered capability scope

then this is a **policy boundary violation**.

The default should be:

```text
DENY
```

because the system knows that the requested action lies outside the granted boundary.

Use a deterministic reason such as:

```text
capability.scope.denied
```

or follow the repository's established reason naming conventions.

### Unknown scope

If the evaluator cannot establish whether the action is within scope:

```text
ESCALATE
```

Do not guess.

---

# 8. Scope Is Not Authorization

This distinction is mandatory.

A scope does **not** mean:

> This principal is authorized to access this resource.

It means:

> If this capability is otherwise trusted and applicable, this is the boundary within which the policy permits it to operate.

Do not introduce:

* users
* roles
* principals
* accounts
* authorization policies
* identity providers
* JWT
* OAuth
* access-control lists

Those remain separate concerns.

---

# 9. Scope Is Not Security Enforcement

The policy layer must not verify that a scope is technically enforced.

For example, if a scope represents:

```text
workspace-A
```

policy may determine:

```text
requested resource = workspace-B
→ DENY
```

But policy must not itself:

* inspect the filesystem
* verify directory permissions
* open files
* resolve symbolic links
* query Docker mounts
* inspect OS ACLs
* launch processes
* access networks

Those belong to execution/security infrastructure.

---

# 10. Scope Is Not Resource Discovery

Do not make the policy layer discover resources.

Policy should consume already-established scope information.

Do not introduce:

* filesystem scanners
* project discovery
* Git discovery
* Docker discovery
* MCP discovery
* network discovery
* workspace scanning

A later Application/Infrastructure layer may construct the appropriate policy context.

---

# 11. Scope Relationship Model

Consider whether the model needs to support relationships such as:

```text
workspace
  └── project
       └── resource
```

Do **not** implement a resource hierarchy unless it is genuinely required by the current repository.

If hierarchy is deferred, document that policy currently supports only explicit scope identity matching.

Do not invent containment semantics such as:

```text
workspace-A contains project-B
```

unless the domain has an explicit reason and model for establishing that relationship.

---

# 12. Scope Absence

Explicitly define the semantics of missing scope.

Distinguish:

### Capability has no scope information

from:

### Capability is intentionally global

from:

### Requested action has no scope information

These must not accidentally collapse into one state.

A safe default is:

```text
Unknown scope relationship
→ ESCALATE
```

unless the action itself is intrinsically scope-independent.

Do not introduce arbitrary assumptions.

---

# 13. Scope-Independent Actions

Review the current action model.

Some actions may not require a resource scope.

For example, depending on the existing model:

```text
read
write
execute
```

may require scope.

Other future actions might not.

Do not invent additional actions merely to demonstrate this.

If the current action catalogue cannot yet distinguish scope-dependent and scope-independent actions, document that limitation rather than introducing speculative categories.

---

# 14. Registration Integration

Extend the capability registration model from POLICY-003 only as necessary.

Conceptually:

```text
CapabilityRegistration
    ├── Capability
    ├── Provenance
    └── Scope
```

The registration remains immutable.

Duplicate registrations must remain deterministic.

For example:

```text
same capability
+
same provenance
+
same scope
```

should remain an identical registration.

Where registrations conflict:

```text
same capability
+
different scope
```

must not silently overwrite one another.

Follow the existing POLICY-003 conflict semantics.

---

# 15. Multiple Registrations

Consider whether the same capability can legitimately have multiple scoped registrations.

For example:

```text
workspace.read
scope = workspace-A

workspace.read
scope = workspace-B
```

The policy model should support this if it is consistent with the existing registration design.

Do not collapse these registrations into a single capability record if doing so would lose meaningful scope information.

Policy evaluation should determine whether **any valid trusted registration** permits the requested scope.

However:

> An explicit deny must still override an otherwise matching registration.

---

# 16. Provenance Interaction

Preserve POLICY-003 semantics.

A scope match must not compensate for insufficient provenance.

For example:

```text
Capability: workspace.write
Scope: workspace-A
Provenance: Unknown
Requested: workspace-A
```

must not become:

```text
ALLOW
```

It should remain:

```text
ESCALATE
```

Likewise:

```text
Capability: workspace.write
Scope: workspace-A
Provenance: Trusted
Requested: workspace-B
```

should result in:

```text
DENY
```

The relevant constraints must all be satisfied before `ALLOW` is possible.

---

# 17. Explicit Deny Interaction

Preserve the existing rule precedence:

```text
DENY > ESCALATE > ALLOW
```

Examples:

```text
trusted + matching scope + explicit deny
→ DENY
```

```text
trusted + mismatching scope + allow rule
→ DENY
```

```text
unknown provenance + matching scope
→ ESCALATE
```

```text
trusted provenance + unknown scope relationship
→ ESCALATE
```

Do not allow a matching scope to override a stronger denial.

---

# 18. Determinism

Scope evaluation must be deterministic.

For identical:

```text
Action
Capability
Provenance
Scope
Rules
Context
```

the evaluator must produce the same decision and reason.

Do not depend on:

* filesystem state
* current working directory
* environment variables
* machine state
* current time
* randomness
* network state
* database state

Scope is a policy-domain concept, not a resource-discovery mechanism.

---

# 19. Immutability

Preserve the existing immutability guarantees.

Scope objects, registrations, and policy contexts must not expose mutable internal state.

If multiple scoped registrations are supported:

* copy input collections
* prevent caller mutation
* provide deterministic enumeration
* preserve registration identity
* prevent accidental replacement

---

# 20. Validation

Validate scope values at the domain boundary.

Reject:

* null values
* empty values
* invalid identifiers
* ambiguous normalization
* malformed values according to the chosen scope representation

Do not overvalidate arbitrary resource identifiers without a concrete domain requirement.

The goal is deterministic policy input, not resource validation.

---

# 21. Tests

Expand the Core policy test suite.

At minimum cover:

## Scope values

* valid scope
* invalid scope
* empty scope
* normalization
* immutable scope

## Scope matching

* matching capability scope → ALLOW when all other conditions are satisfied
* known mismatch → DENY
* unknown relationship → ESCALATE

## Registration

* scoped registration
* identical scoped registration deduplication
* conflicting scoped registrations
* multiple valid scopes for the same capability
* registration immutability

## Provenance + scope

* trusted + matching scope → ALLOW
* unknown provenance + matching scope → ESCALATE
* untrusted provenance + matching scope → ESCALATE or DENY according to documented semantics
* trusted + mismatching scope → DENY
* unknown provenance + mismatching scope → DENY if the scope violation is independently known

Be explicit about precedence.

## Explicit deny

* explicit deny overrides matching scope
* explicit deny overrides trusted provenance
* explicit deny remains stronger than escalation

## Missing information

* missing capability → existing escalation behavior
* missing requested scope where required → ESCALATE
* missing registration scope → conservative behavior
* unknown scope relationship → ESCALATE

## Determinism

Identical policy inputs produce identical results.

## Immutability

Caller mutation of supplied collections cannot alter registered scopes or policy context.

---

# 22. Existing Test Preservation

Do not remove tests from POLICY-001, POLICY-002, or POLICY-003 merely because the model evolves.

Update tests only where the policy semantics legitimately become more precise.

Existing guarantees must remain:

* fail-closed behavior
* deterministic evaluation
* immutable policy state
* explicit deny precedence
* provenance requirements
* unsupported actions do not become ALLOW
* missing capabilities do not silently become DENY unless explicitly prohibited

---

# 23. Architecture Constraints

Strictly preserve:

* Core provider independence
* Application independence from Infrastructure
* policy/execution separation
* policy/authorization separation
* policy/security separation
* policy/orchestration separation
* policy/persistence separation

Do not add:

* filesystem APIs
* Docker APIs
* HTTP APIs
* MCP
* LLM SDKs
* SQLite
* database access
* network access
* OS process APIs
* authentication
* authorization
* sandboxing
* resource discovery
* execution services
* background services

No new project should be required.

No external dependencies should be introduced.

---

# 24. Documentation

Create:

```text
POLICY-004-IMPLEMENTATION.md
```

Document:

* objective
* scope model
* scope identity
* bounded vs unbounded scope
* scope matching semantics
* mismatch semantics
* unknown-scope semantics
* multiple scoped registrations
* provenance interaction
* explicit-deny interaction
* authorization boundary
* security boundary
* execution boundary
* resource-discovery boundary
* persistence boundary
* deferred hierarchy/resource semantics
* tests
* verification

Explicitly document:

> Policy scope describes the logical boundary of a capability. It does not establish authorization or enforce access to the underlying resource.

---

# 25. Verification

Before completion run:

```text
dotnet test AiGameStudioWorkstation.sln --verbosity minimal
dotnet build AiGameStudioWorkstation.sln
git diff --check
```

Inspect diagnostics for all changed C# files.

Expected:

* zero test failures
* zero build errors
* zero new warnings unless explicitly justified
* zero diff-check errors

Report exact test counts.

---

# 26. Scope Guard

Do not proceed into:

* POLICY-005
* authorization
* identity
* role/permission systems
* capability revocation
* resource discovery
* filesystem sandboxing
* OS security
* tool execution
* agent execution
* MCP
* orchestration
* persistence/audit integration
* autonomous loops
* approval workflows

If these become apparent, document them as deferred architecture.

Do not implement them.

---

# 27. Completion Report

Return a structured completion report.

## Task

* Task ID
* Task title

## Scope Model

* scope representation
* bounded/unbounded semantics
* scope matching
* mismatch behavior
* unknown behavior
* multiple registration behavior

## Evaluation

Explain:

* how scope affects ALLOW
* how scope mismatch produces DENY
* how unknown scope produces ESCALATE
* interaction with provenance
* interaction with explicit denial
* conflict precedence

## Architecture

Explain the boundaries between:

* policy scope
* authorization
* security enforcement
* resource discovery
* execution
* orchestration
* persistence

## Implementation

List every changed file and why it changed.

## Tests

Report:

* tests added
* tests modified
* important scenarios
* total test count

## Deferred Concerns

Explicitly identify functionality intentionally left for later.

## Verification

Report exact results for:

* `dotnet test`
* `dotnet build`
* `git diff --check`
* diagnostics

## Final Assessment

Classify as:

* **READY**
* **READY WITH DOCUMENTED DEFERMENTS**
* **NOT READY**

Do not automatically begin POLICY-005.

Stop after producing the completion report.
