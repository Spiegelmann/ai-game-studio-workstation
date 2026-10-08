# POLICY-003 — Capability Provenance & Registration

## Objective

Extend the Policy subsystem with a minimal, explicit model for **capability provenance and registration**.

POLICY-001 established the policy decision boundary.

POLICY-002 established:

* capabilities
* action-to-capability mapping
* explicit deny rules
* conservative decision combination
* deterministic evaluation
* immutable policy context

POLICY-003 addresses the next architectural question:

> **Where do policy capabilities come from, and can the policy system distinguish a trusted capability declaration from an arbitrary capability string?**

The objective is **not** to implement authorization.

The objective is to establish a domain-level representation of capability provenance so that later application/orchestration layers can provide policy with capabilities whose origin and trust characteristics are explicit.

This task must remain small and provider-independent.

---

# 1. Read Before Changing Anything

Inspect the actual repository before making changes.

Read:

* `AGENTS.md`
* relevant repository instructions
* all FOUNDATION implementation records
* all PERSISTENCE implementation records
* `PERSISTENCE-005.md`
* `POLICY-001`
* `POLICY-001-IMPLEMENTATION.md`
* `POLICY-002`
* `POLICY-002-IMPLEMENTATION.md`
* all current Core policy types
* all policy tests
* current Application contracts
* current architecture boundaries
* current solution/project structure

Treat the repository as authoritative.

Do not assume the previous task specifications exactly match the implementation.

---

# 2. Core Problem

POLICY-002 currently allows a `PolicyContext` to contain capabilities.

This establishes:

```text
Action
    +
Capability Context
    ↓
Policy Decision
```

However, it does not establish:

```text
Who/what supplied the capability?
Why should the capability be trusted?
What scope does the capability have?
Is the capability explicitly registered?
```

POLICY-003 should introduce only the minimum model necessary to represent this distinction.

Do not implement a complete identity or authorization framework.

---

# 3. Capability Provenance

Introduce a domain concept representing the **source/provenance** of a capability.

The model should be able to distinguish conceptually between sources such as:

```text
System
Application
Agent
Tool
Configuration
ExplicitGrant
Unknown
```

The exact categories should follow the existing architecture and should remain small.

Do not create a large taxonomy.

The important invariant is:

> A capability should be traceable to a declared source category rather than being treated as an unexplained string.

---

# 4. Capability Registration

Introduce a minimal concept representing a registered capability.

A registration should conceptually contain:

```text
Capability
+
Provenance
```

It may additionally require a small amount of metadata if justified by the existing model.

Do not add speculative fields such as:

* user IDs
* role IDs
* organization IDs
* JWT claims
* OAuth scopes
* IP addresses
* machine identities
* credentials
* secrets
* network addresses
* process IDs

Those belong to later identity/security/authorization concerns.

---

# 5. Registration Semantics

A capability should not automatically become trusted merely because a caller constructs a `PolicyCapability`.

Establish a distinction between:

```text
Capability value
```

and:

```text
Registered capability
```

Conceptually:

```text
PolicyCapability
    ↓
CapabilityRegistration
    ↓
PolicyContext
    ↓
PolicyEvaluator
```

The exact implementation may differ if the existing architecture supports a cleaner representation.

The important property is that provenance is explicit.

---

# 6. Unknown Provenance

Unknown or untrusted provenance must not silently become permission.

For example:

```text
Capability = process.execute
Provenance = Unknown
```

must not automatically produce:

```text
ALLOW
```

The default behavior should remain conservative.

Depending on the resulting model, unknown provenance should produce either:

* `ESCALATE`, or
* a deterministic `DENY`

The preferred default is **ESCALATE** when the capability exists but its trust/provenance is insufficiently established.

Use `DENY` only where the policy semantics explicitly represent a prohibited source.

Document the distinction.

---

# 7. Trusted vs Untrusted Sources

Introduce only the minimum distinction necessary to support later policy decisions.

A provenance source should be capable of being classified conceptually as:

```text
Trusted
Untrusted
Unknown
```

However, do not build a general trust framework.

Do not introduce:

* trust scores
* numerical reputation
* trust decay
* cryptographic trust chains
* certificates
* PKI
* identity federation

Those belong to later security architecture.

The policy domain only needs enough information to avoid treating every capability declaration as equally trustworthy.

---

# 8. Scope

Review whether a capability registration should have a scope.

The current architecture may eventually need to distinguish between:

```text
workspace.read
```

and:

```text
workspace.read
for a specific bounded scope
```

However, **do not implement resource-level scope unless the existing domain already requires it**.

POLICY-003 should not become an authorization or resource-permission system.

If scope is not currently required, explicitly document:

> Capability scope is intentionally deferred.

This is preferable to inventing an abstraction without a concrete consumer.

---

# 9. Capability Registration Rules

Introduce a small deterministic registration mechanism if required.

It should answer:

> Is this capability registration valid and acceptable as policy input?

At minimum consider:

### Valid capability + trusted provenance

```text
→ eligible for policy evaluation
```

### Valid capability + unknown provenance

```text
→ insufficient trust information
```

### Invalid capability

```text
→ invalid input
```

### Duplicate identical registration

Should be deterministic and safe.

### Duplicate capability with conflicting provenance

Must not silently overwrite the existing registration.

Choose a deterministic conflict behavior consistent with existing domain conventions.

Prefer rejection over silent mutation.

---

# 10. Immutability

All registration and provenance objects must be immutable.

Do not expose mutable collections.

If a registry/context contains registrations:

* copy input collections
* prevent external mutation
* provide deterministic enumeration
* do not allow accidental replacement through references

Preserve the immutability guarantees established by POLICY-002.

---

# 11. Determinism

Registration behavior and policy consumption must remain deterministic.

For identical registrations:

```text
same capability
+
same provenance
```

the resulting state must be equivalent.

Conflicting registrations must produce deterministic results.

Do not depend on:

* insertion order unless explicitly part of the contract
* current time
* randomness
* environment variables
* database state
* filesystem state
* external services

---

# 12. Integration With PolicyContext

Extend `PolicyContext` only as necessary.

The policy evaluator should be able to consume registered capabilities without requiring infrastructure knowledge.

The desired conceptual flow is:

```text
CapabilityRegistration
        ↓
PolicyContext
        ↓
PolicyEvaluator
        ↓
PolicyDecision
```

Do not make `PolicyEvaluator` responsible for discovering or registering capabilities.

The evaluator evaluates.

Registration establishes valid policy input.

Keep those responsibilities separate.

---

# 13. Provenance Must Not Become Authorization

This is a critical boundary.

Do not interpret:

```text
Provenance = Agent
```

as:

```text
Agent is authorized to possess this capability.
```

Likewise:

```text
Provenance = Tool
```

does not mean:

```text
Tool is trusted to execute safely.
```

Provenance only describes the declared origin of the capability information.

Authorization remains a separate concern.

Security enforcement remains separate.

Execution remains separate.

---

# 14. Provenance Must Not Become Identity

Do not introduce identity concepts.

The following are explicitly out of scope:

* users
* agents as authenticated principals
* service identities
* roles
* accounts
* credentials
* authentication
* authorization tokens
* OAuth
* JWT
* API keys

A provenance value such as `Agent` is a **category**, not an authenticated identity.

Document this distinction.

---

# 15. Provenance Must Not Become Security Enforcement

Do not make the policy domain verify:

* signatures
* certificates
* process ownership
* filesystem permissions
* network identity
* sandbox state
* executable integrity
* tool binaries

The domain may consume a trusted/untrusted classification supplied by a later layer.

It must not implement the mechanisms that establish that trust.

---

# 16. Capability Revocation

Consider whether capability revocation belongs in POLICY-003.

The default answer should be:

> No.

Do not implement:

* revocation lists
* expiration timers
* leases
* capability TTLs
* background revocation
* persistence-backed capability state

unless the current repository contains a concrete requirement that makes them necessary.

Document revocation as deferred if appropriate.

This task establishes capability provenance, not lifecycle management.

---

# 17. Policy Evaluation Semantics

Update policy evaluation only where required to consume provenance.

The evaluator must preserve the POLICY-002 semantics:

```text
DENY > ESCALATE > ALLOW
```

Examples:

```text
Known action
+
required capability
+
trusted provenance
→ ALLOW
```

```text
Known action
+
required capability
+
unknown provenance
→ ESCALATE
```

```text
Known action
+
explicit deny rule
→ DENY
```

```text
Unknown action
→ ESCALATE
```

Do not weaken any existing fail-closed behavior.

---

# 18. Explicit Deny Still Wins

Provenance must never override explicit policy denial.

For example:

```text
Capability present
Provenance = Trusted
Explicit deny rule
```

must still produce:

```text
DENY
```

Likewise:

```text
Capability present
Provenance = Trusted
Capability allowed
```

must not automatically override a stronger policy rule introduced later.

Preserve the existing conflict semantics.

---

# 19. Tests

Expand the Core policy tests.

At minimum test:

## Provenance

* valid provenance
* invalid provenance
* trusted provenance
* unknown provenance
* untrusted provenance if represented

## Registration

* valid capability registration
* duplicate identical registration
* conflicting duplicate registration
* invalid registration
* immutable registration

## Policy integration

* trusted capability + known action → ALLOW
* unknown provenance + known action → ESCALATE
* untrusted capability + known action → ESCALATE or DENY according to the documented semantics
* explicit deny + trusted capability → DENY
* explicit deny + unknown provenance → DENY
* unknown action → ESCALATE

## Determinism

Identical registrations and contexts produce identical decisions.

## Immutability

External collection mutation cannot alter registered policy state.

## Conflict

Conflicting capability registrations cannot silently overwrite one another.

---

# 20. Existing Test Preservation

Do not remove existing POLICY-001 or POLICY-002 tests merely because the model changes.

Update tests where semantics legitimately evolve.

Existing guarantees must remain intact:

* fail-closed evaluation
* deterministic decisions
* immutable policy state
* explicit deny
* conservative conflict combination
* unsupported actions do not become ALLOW

The final test suite must demonstrate both the old and new invariants.

---

# 21. Architecture Constraints

Strictly preserve:

* Core provider independence
* Application independence from Infrastructure
* SQLite isolation
* policy/execution separation
* policy/orchestration separation
* policy/authorization separation
* policy/security separation
* policy/persistence separation

Do not add dependencies.

Do not add new projects.

Do not introduce infrastructure services.

Do not introduce persistence.

Do not introduce network calls.

Do not introduce MCP.

Do not introduce LLM SDKs.

Do not introduce authentication.

Do not introduce authorization.

Do not introduce execution.

Do not introduce orchestration.

Do not introduce background services.

---

# 22. Documentation

Create:

```text
POLICY-003-IMPLEMENTATION.md
```

Document:

* objective
* capability provenance model
* registration model
* trusted/unknown/untrusted semantics
* duplicate/conflict behavior
* relationship to `PolicyContext`
* relationship to `PolicyEvaluator`
* missing capability vs unknown provenance
* authorization boundary
* identity boundary
* security boundary
* execution boundary
* orchestration boundary
* persistence boundary
* intentionally deferred scope
* tests
* verification

Explicitly state:

> Capability provenance describes the declared origin of policy input. It does not authenticate, authorize, or secure that origin.

---

# 23. Verification

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

# 24. Scope Guard

Do not proceed into:

* POLICY-004
* authorization
* identity
* role/permission systems
* capability revocation
* capability persistence
* security enforcement
* sandboxing
* agent execution
* tool execution
* MCP
* orchestration
* autonomous loops
* approval workflows

If these concerns become apparent, document them as deferred architecture rather than implementing them.

---

# 25. Completion Report

Return a structured completion report.

## Task

* Task ID
* Task title

## Capability Model

* capability representation
* provenance representation
* registration representation
* trusted/unknown/untrusted semantics

## Evaluation

Explain:

* how provenance affects policy evaluation
* missing capability behavior
* explicit denial behavior
* conflict behavior
* fail-closed behavior

## Architecture

Explain the boundaries between:

* capability provenance
* policy
* authorization
* identity
* security
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

Do not automatically begin POLICY-004.

Stop after producing the completion report.
