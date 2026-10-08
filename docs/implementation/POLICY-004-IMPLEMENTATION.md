# POLICY-004 Implementation — Capability Scope & Boundary Constraints

## Objective

Add explicit, provider-independent capability scope to policy actions and registrations. Scope contributes a logical boundary to policy evaluation; it does not implement resource access, authorization, or enforcement.

## Scope model

`PolicyScopeId` is an immutable value object. It rejects empty/whitespace values and control characters, trims surrounding whitespace, and preserves case and interior characters. Scope identity comparisons are therefore ordinal and case-sensitive after trimming.

`PolicyScope` distinguishes:

* `Unknown` — scope information is absent or cannot be established; it is never treated as global.
* `Bounded` — a boundary identified by `PolicyScopeId`.
* `Global` — an explicitly declared, unbounded scope.

An explicitly global registration matches any known requested scope. A bounded registration matches only the same bounded identity; it does not match a global request. An unknown operand produces an unknown relationship.

Actions and registrations created through their existing constructors default to `Unknown` scope. Callers must explicitly provide `PolicyScope.Global` to declare global scope.

## Registration and matching

`PolicyCapabilityRegistration` now includes immutable scope. Contexts keep distinct scoped registrations, deduplicate exact duplicates, and sort by capability, provenance, and scope for deterministic enumeration. Multiple scopes for the same capability are retained when provenance is consistent. The POLICY-003 rule rejecting conflicting provenance for one capability is preserved.

Scope comparison returns `Match`, `NoMatch`, or `Unknown`, rather than collapsing boundary comparison to a boolean. No hierarchy, containment, or resource discovery is modeled.

## Evaluation semantics

For complete contexts and supported actions:

* At least one trusted registration matching the requested scope permits the capability, unless a more restrictive applicable rule applies.
* If every registration is a known scope mismatch, evaluation denies with `capability.scope.denied`. This is true even when provenance is unknown or untrusted because the mismatch is independently established by the declared boundaries.
* If scope matching is unknown, evaluation escalates with `capability.scope.unknown`, unless a matching registration instead provides the more specific unknown/untrusted provenance escalation.
* A matching registration with unknown or untrusted provenance escalates; a scope match cannot compensate for provenance.
* Missing capabilities retain `capability.missing`.

Across multiple registrations, any trusted match permits the scope. Known mismatches do not defeat another trusted match. An unknown relationship prevents a scope-denial conclusion because another registration may apply. Explicit deny rules continue to outrank every baseline decision; less restrictive rules cannot override a scope denial. The existing conservative ordering remains `Deny > Escalate > Allow`.

The current action catalogue does not distinguish scope-independent actions. All currently supported mapped actions (`read`, `write`, `execute`) therefore require a known scope relationship.

## Architectural boundaries

* **Authorization/identity:** Scope does not identify or authorize a principal.
* **Security enforcement:** Policy compares declared scope values; it does not inspect filesystem permissions, paths, OS security, containers, or other resources.
* **Resource discovery:** Scope values are supplied inputs; Core does not discover or resolve resources.
* **Execution:** Evaluation does not access or execute the scoped resource.
* **Orchestration:** No task scheduling, approval, agent, or recovery behavior was added.
* **Persistence:** Scope and registrations remain immutable in-memory Core policy inputs; no persistence or audit linkage was added.
* **Architecture:** Core remains provider-independent. No project or external dependency was added.

> Policy scope describes the logical boundary of a capability. It does not establish authorization or enforce access to the underlying resource.

## Tests

The Core policy test suite covers normalized and invalid scope identities, explicit unknown/global/bounded values, three-way scope comparison, matching and mismatching evaluation, unknown requested or registration scope, explicit global behavior, unknown/untrusted provenance interaction, multiple scopes for one capability, exact scoped-registration deduplication, immutable copied context state, explicit-deny precedence, conservative rule combination, and deterministic evaluation. Existing POLICY-001 through POLICY-003 tests remain in place.

## Verification

* `dotnet test AiGameStudioWorkstation.sln --verbosity minimal` — **PASS**, 245 passed, 0 failed, 0 skipped.
* `dotnet build AiGameStudioWorkstation.sln` — **PASS**, 0 warnings, 0 errors.
* Focused `PolicyEvaluatorTests` — **PASS**, 60 passed, 0 failed, 0 skipped.
* `git diff --check` — **PASS**.
* Diagnostics for all changed C# files — **PASS**, no errors reported.

## Deferred concerns

Scope hierarchy/containment, resource validation and discovery, authorization, authenticated identity, revocation, operating-system or tool enforcement, execution, approvals, orchestration, persistence, and audit integration remain deferred.

## Assessment

**READY WITH DOCUMENTED DEFERMENTS.** Scope is explicit, immutable, conservatively matched, and integrated with existing provenance and deny precedence without adding execution or authorization authority.
