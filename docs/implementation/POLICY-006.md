# POLICY-006 — Policy Evaluation Determinism & Replay Verification

## Objective

Establish deterministic, independently verifiable policy evaluation outcomes so that equivalent policy inputs produce equivalent decisions and evaluation traces.

This task must build on POLICY-001 through POLICY-005 and preserve the existing policy evaluator as the source of truth.

The goal is to strengthen confidence in policy decisions before introducing higher-level enforcement, orchestration, or execution behavior.

## Context

The policy domain currently includes:

* Explicit policy decisions and precedence.
* Capability registration and trust provenance.
* Capability scope constraints, including `Unknown`, `Bounded`, and `Global`.
* Scope comparison outcomes: `Match`, `NoMatch`, and `Unknown`.
* Structured, immutable evaluation traces.
* Observable rule ordering, winning decision sources, and final precedence.
* Existing deny, escalate, and allow behavior.

POLICY-005 introduced immutable evaluation traces and documented their boundaries. Decisions constructed directly outside `PolicyEvaluator` remain compatible and may not have an evaluation trace.

Preserve these properties.

## Scope

### 1. Audit Existing Determinism

Inspect the policy-domain implementation and tests from POLICY-001 through POLICY-005.

Identify potential sources of nondeterminism, including:

* Unstable iteration over dictionaries, sets, or other unordered collections.
* Registration ordering that depends on insertion order when insertion order is not part of the contract.
* Rule evaluation ordering that is implicit or undocumented.
* Tie-breaking between equivalent candidates.
* Trace collection ordering.
* Equality and comparison behavior for evaluation traces.
* Dependence on current time, environment state, random values, or external services.

Do not assume these problems exist. Verify the implementation and document actual findings.

### 2. Define Determinism Contracts

Document and implement the minimum deterministic behavior required by the existing policy model.

At minimum:

* Identical logical inputs must produce equivalent policy decisions.
* Identical logical inputs must produce structurally equivalent evaluation traces.
* Rule ordering must be explicit and deterministic.
* Candidate selection and tie-breaking must follow a documented, deterministic contract wherever the existing model permits equivalent candidates.
* Collection ordering in traces must be deterministic.
* Equivalent scope and provenance inputs must produce equivalent evaluation outcomes.

Do not silently change the meaning of existing policy rules to achieve determinism.

If existing behavior intentionally depends on registration order or another established ordering contract, preserve it and document that dependency.

### 3. Add Determinism Regression Tests

Add focused tests covering:

* Repeated evaluation of equivalent inputs.
* Equivalent registrations supplied in different collection orders, where collection order is not contractually meaningful.
* Stable ordering of rule evaluations.
* Stable ordering of registration and capability evaluation traces.
* Equivalent decisions and traces across repeated runs.
* Deterministic handling of tied or competing candidates.
* Preservation of `Unknown` scope behavior.
* Preservation of deny, escalate, and allow precedence.
* Decisions constructed outside `PolicyEvaluator`, including the existing optional-trace compatibility behavior.

Tests must assert observable contracts rather than private implementation details.

Where ordering is intentionally meaningful, test that contract instead of requiring order independence.

### 4. Verify Trace Integrity

Review `PolicyEvaluationTrace` and its supporting types.

Verify that:

* Trace data remains immutable after evaluation.
* Trace collections cannot be mutated through exposed interfaces or retained mutable input references.
* Structural equality behaves consistently for equivalent traces.
* Trace ordering reflects the documented evaluation order.
* The recorded winning source and final precedence agree with the actual evaluator result.

Fix any demonstrated defects within the task's scope.

Do not introduce cryptographic signatures, hashing protocols, persistent audit storage, or external serialization frameworks unless already required by the existing architecture.

### 5. Preserve Architecture Boundaries

Keep policy evaluation within the existing Core/domain boundaries.

Do not introduce:

* Authorization enforcement or execution.
* Orchestration or scheduling.
* Persistence or database dependencies.
* Logging, telemetry, or external observability providers.
* External services or network dependencies.
* New third-party packages.
* Changes to unrelated task lifecycle or persistence behavior.

Do not treat an explainability trace as an authorization token, enforcement mechanism, or proof of trustworthiness.

### 6. Documentation

Add `POLICY-006-IMPLEMENTATION.md`.

Document:

* The determinism contract.
* Which input orderings are meaningful and which are not.
* Rule ordering and candidate tie-breaking behavior.
* Trace ordering and structural equality guarantees.
* Any identified defects and their resolution.
* Remaining limitations and deferred work.
* Why deterministic evaluation does not, by itself, provide tamper-proof auditability or authorization enforcement.

Preserve existing documentation, including the supplied, pre-existing `POLICY-005.md`, unchanged.

## Acceptance Criteria

* [ ] Existing POLICY-001 through POLICY-005 behavior is preserved.
* [ ] Determinism contracts are explicit and documented.
* [ ] Repeated equivalent evaluations produce equivalent decisions and traces.
* [ ] Collection-order independence is tested only where collection order is not semantically meaningful.
* [ ] Rule evaluation order and tie-breaking are deterministic under the documented contract.
* [ ] Trace immutability and structural equality are verified.
* [ ] Winning decision source and final precedence remain consistent with evaluator output.
* [ ] Unknown scope behavior and existing deny/escalate/allow semantics remain unchanged.
* [ ] Directly constructed decisions remain backward-compatible.
* [ ] No new dependencies or prohibited architectural responsibilities are introduced.
* [ ] `POLICY-006-IMPLEMENTATION.md` is added.
* [ ] The full solution test suite passes.
* [ ] The full solution builds without warnings or errors.
* [ ] `git diff --check` passes.
* [ ] Changed C# files have no reported diagnostics.
* [ ] The final report distinguishes verified behavior from assumptions and deferred work.

## Required Verification

Run:

1. `dotnet test AiGameStudioWorkstation.sln --verbosity minimal`
2. `dotnet build AiGameStudioWorkstation.sln`
3. `git diff --check`
4. C# diagnostics for all changed C# files.

Inspect the final diff for unintended changes and confirm that pre-existing untracked files were not modified or removed.

Report the exact test counts, build warnings/errors, and any deviations from the planned scope.

## Execution Instructions

Work autonomously within the repository.

Begin by inspecting the existing policy implementation, tests, and POLICY-001 through POLICY-005 documentation. Establish the current contracts before making changes.

Implement only changes justified by the audit and acceptance criteria. Do not redesign the policy system or invent new policy semantics.

If the audit finds that a proposed change would alter an existing contract, preserve the current behavior and document the issue rather than silently broadening scope.

At completion, provide a concise implementation report with changed files, verified determinism guarantees, test results, architectural findings, limitations, and readiness assessment.

Do not begin POLICY-007 as part of this task.
