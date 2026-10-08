# POLICY-001 — Policy Foundation & Decision Boundaries

## Objective

Establish a small, deterministic, fail-closed policy decision model in Core without adding execution, persistence, authorization, orchestration, or concrete policy rules.

## Policy boundary

`IPolicyEvaluator` accepts a `PolicyAction` and `PolicyContext` and returns a `PolicyDecision`. The evaluator makes no external calls, performs no I/O, and does not execute the proposed action. Its current implementation escalates every valid request because this task defines no rules that could justify an automatic allow or deny.

## Concepts introduced

* `PolicyAction` represents a validated, extensible action category, not execution details.
* `PolicyContext` carries only whether the available context is complete, incomplete, or unknown.
* `PolicyReason` contains a structured code and explanation.
* `PolicyDecision` represents `Allow`, `Deny`, or `Escalate` and requires a reason for each outcome.
* `IPolicyEvaluator` defines the synchronous evaluation boundary; `PolicyEvaluator` provides the current fail-closed behavior.

## Decision and fail-closed semantics

* `Allow` means policy permits the action; it does not guarantee execution success or security.
* `Deny` means policy prohibits the action.
* `Escalate` means the action cannot currently be decided automatically; it is not approval, retry, or execution failure.
* Incomplete and unknown contexts escalate with distinct reason codes.
* A complete context also escalates when there is no applicable policy rule. No implicit allow is produced.
* Invalid value-object inputs and undefined enum values are rejected at construction/evaluation boundaries.

## Conflict semantics

Multi-rule evaluation and conflict resolution are intentionally deferred. If introduced later, combining decisions must be conservative: `Deny` takes precedence over `Escalate`, which takes precedence over `Allow`. POLICY-001 does not introduce a rule engine or numeric priority system.

## Architectural decisions

* All policy concepts are in `Core.Domain.Policy`; Core remains independent of persistence providers and execution infrastructure.
* Policy is distinct from authorization, which determines whether a principal has authority.
* Policy is one security control and does not establish that an allowed action is secure.
* Policy does not create/update tasks, invoke agents or tools, schedule or recover work, or make orchestration decisions.
* Decisions are not persisted. Later application/orchestration work may determine which decisions need durable audit evidence without coupling policy to `ITaskRepository`.
* Evidence persistence remains unchanged; current Evidence contracts do not link decisions to operations.

## Tests

Core tests cover all decision outcomes and required reasons, invalid action/context/decision/reason inputs, fail-closed behavior for incomplete/unknown context and absent rules, and deterministic evaluation for identical inputs.

## Verification

* `dotnet test AiGameStudioWorkstation.sln --verbosity minimal` — **PASS**, 201 passed, 0 failed, 0 skipped.
* `dotnet build AiGameStudioWorkstation.sln` — **PASS**, 0 warnings, 0 errors.
* `git diff --check` — **PASS**.
* Diagnostics for changed C# files — **PASS**, no errors reported.

## Intentionally deferred

Concrete rules, rule composition, conflict evaluation, capability registration, authorization, task-scope enforcement, protected-resource rules, decision persistence/audit linkage, tool gateways, execution, human approval workflows, orchestration, and game-specific policy are outside POLICY-001.
