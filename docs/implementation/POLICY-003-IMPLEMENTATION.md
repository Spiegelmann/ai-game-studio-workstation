# POLICY-003 Implementation — Capability Provenance & Registration

## Objective

Add a small Core policy model that distinguishes a capability value from an explicit capability registration and records the registration's declared source and trust classification. This is provenance modeling, not authorization.

## Capability and provenance model

`PolicyCapability` remains the normalized capability value. `PolicyCapabilityProvenance` records a `PolicyCapabilitySource` category (`System`, `Application`, `Agent`, `Tool`, `Configuration`, `ExplicitGrant`, or `Unknown`) and a `PolicyCapabilityTrust` classification (`Trusted`, `Untrusted`, or `Unknown`). Undefined enum values are rejected. An unknown source must have unknown trust.

**Capability provenance describes the declared origin of policy input. It does not authenticate, authorize, or secure that origin.** Source category and trust classification are supplied data; neither is proof of identity, authority, or safe execution.

## Registration and context behavior

`PolicyCapabilityRegistration` is an immutable pair of a `PolicyCapability` and its provenance. `PolicyContext.FromRegistrations` copies registrations and rules, exposes read-only deterministic collections, removes identical duplicate registrations, and rejects the same capability registered with conflicting provenance instead of overwriting either registration. Capability scope is intentionally deferred.

The existing `PolicyContext` constructors that accept only `PolicyCapability` values remain source-compatible. They wrap those values with unknown provenance, so a bare capability can no longer implicitly establish an allow. `PolicyContext.Capabilities` remains available as a read-only view; evaluation uses `Registrations`.

## Evaluation semantics

For a complete context and supported action:

* no matching registration escalates with `capability.missing`;
* unknown provenance escalates with `capability.provenance.unknown`;
* untrusted provenance escalates with `capability.provenance.untrusted`;
* trusted provenance is eligible for the existing `capability.granted` allow.

Unknown and untrusted sources are distinct from an explicit policy prohibition. They lack sufficient trust information, so they escalate rather than silently permit or automatically deny. Existing explicit deny rules continue to take precedence over baseline decisions, including decisions with unknown provenance. Rule combination remains `Deny > Escalate > Allow`; unsupported actions remain escalations.

## Architectural boundaries

* **Policy:** consumes immutable registrations and makes deterministic decisions; it does not discover or register inputs dynamically.
* **Authorization and identity:** not implemented. A source category such as `Agent` is not an authenticated principal and grants no authority to possess a capability.
* **Security:** provenance does not verify signatures, permissions, process ownership, sandboxing, or tool integrity. A later layer must establish any trust classification it supplies.
* **Execution:** policy decisions do not execute actions or secure execution.
* **Orchestration:** no task scheduling, agent coordination, approvals, or recovery was added.
* **Persistence:** the registration model is in-memory immutable domain input. No persistence or audit linkage was added; Core remains provider-independent.

## Intentionally deferred

Resource-level capability scope, authorization, authenticated identity, revocation/expiration, capability persistence, security enforcement, execution/tool gateways, approvals, orchestration, and policy auditing remain deferred. Provenance trust classifications are declarative input, not a general trust framework.

## Tests

Core policy tests cover valid and invalid provenance, trusted/unknown/untrusted registrations, legacy bare-capability escalation, duplicate and conflicting registrations, immutable copied context state, all existing action mappings, missing capabilities, explicit deny precedence, unsupported actions, conservative rule combination, and deterministic evaluation.

## Verification

* `dotnet test AiGameStudioWorkstation.sln --verbosity minimal` — **PASS**, 230 passed, 0 failed, 0 skipped.
* `dotnet build AiGameStudioWorkstation.sln` — **PASS**, 0 warnings, 0 errors.
* `git diff --check` — **PASS**.
* Diagnostics for all changed C# files — **PASS**, no errors reported.

## Assessment

**READY WITH DOCUMENTED DEFERMENTS.** Provenance and registration are explicit immutable policy inputs, and policy evaluation escalates whenever a required capability is absent or not classified as trusted. Existing explicit-deny and conservative rule-combination behavior remains intact.
