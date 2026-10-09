# PROCESS-001 — Process Contract

## Objective

Establish the foundational, structured process-execution contract for AI Game Studio Workstation.

Define the domain concepts and Application-facing abstractions required to represent a bounded process execution request and its intended execution context.

This task establishes the contract only. It must not implement actual process spawning, sandboxing, output capture, cancellation infrastructure, or process execution providers unless explicitly required by the authoritative checked-in PROCESS-001 specification.

The purpose is to ensure that later build, test, lint, and other approved process operations can be expressed as structured requests rather than unrestricted shell commands.

## 0. Mandatory Specification Audit

Before implementation, inspect:

* The checked-in `PROCESS-001.md` specification.
* The implementation sequence and task dependencies.
* The established process pipeline design and MCP Pipeline Matrix.
* TOOL-001 workspace contracts.
* TOOL-002 path-resolution contracts and implementation.
* TOOL-003 workspace registry and registration contracts.
* POLICY-001 through POLICY-006.
* Existing task, operation, correlation, scope, and result abstractions.
* Existing Core, Application, Infrastructure, and Host boundaries.
* Existing architectural tests and repository conventions.

The checked-in PROCESS-001 specification is authoritative.

If the repository defines a different scope from this prompt, follow the checked-in specification and document the discrepancy. Reuse existing types wherever their semantics match the required contracts.

Do not begin PROCESS-002 or any subsequent task.

## 1. Define the Structured Process Contract

Establish an immutable process-execution request containing the fields required by the authoritative specification.

The established architecture calls for these concepts:

* **Executable:** an explicitly identified executable or registered execution definition.
* **Arguments:** a structured, ordered collection of individual arguments.
* **WorkingDirectory:** the designated working directory, associated with the relevant trusted workspace.
* **Environment:** an explicitly controlled set of permitted environment variables.
* **Timeout:** a bounded execution duration.
* **Cancellation:** a representation of the cancellation intent or contract required by the existing architecture.
* **ResourceLimits:** a structured representation of applicable execution limits.

Reuse existing domain value objects where appropriate. Introduce new immutable value objects only when necessary to express meaningful invariants.

The request must distinguish the identity of an executable from arbitrary user-supplied command text.

Do not model the process request as a single shell-command string.

## 2. Executable Identity and Registration Boundaries

Represent executable selection in a way that can support later registered execution definitions.

An agent must not be able to obtain arbitrary execution authority simply by supplying an executable path or command string.

Where the existing design uses registered execution definitions, the request should reference their stable identity rather than redefining their trusted configuration.

Do not implement the execution registry unless it is explicitly within PROCESS-001's scope.

Do not invent a new trust hierarchy or duplicate the policy evaluator's responsibilities.

Keep the following concepts distinct:

* Executable identity.
* Executable configuration and trust.
* Process request validation.
* Workspace containment.
* Policy authorization.
* Actual process execution.

A structurally valid request must not be treated as authorized or safe to execute.

## 3. Arguments and Command Injection Boundaries

Arguments must be represented as an ordered collection of individual values.

The contract must preserve argument boundaries and must not implicitly concatenate arguments into a shell command.

Validate structural requirements such as null entries, invalid values, and any limits specified by the authoritative contract.

Do not implement ad hoc shell escaping as a substitute for avoiding shell invocation.

Do not allow arbitrary shell interpreters, command chaining, pipelines, redirection, or shell expansion to become implicitly supported.

If a later execution provider uses a platform-specific process API, that provider must be responsible for safely translating the structured request into the platform's process-start mechanism.

PROCESS-001 must not claim that structured arguments alone make every executable safe.

## 4. Working Directory and Workspace Integration

Reuse the workspace contracts established by TOOL-001 through TOOL-003.

The process contract must express the intended working directory without independently redefining trusted workspace roots.

Where required by the authoritative specification, associate the working directory with a registered workspace and a workspace-relative resource identifier.

Do not treat a syntactically valid path as proof of physical containment.

Do not bypass the existing workspace path resolver.

Do not allow the agent to expand permitted workspace roots through process-request metadata.

Preserve the separation between workspace registration, physical containment verification, policy evaluation, and execution.

Do not implement filesystem resolution again within the process domain.

## 5. Controlled Environment Variables

Represent environment configuration explicitly.

The contract must support a controlled set of environment variables rather than inheriting arbitrary environment state implicitly.

Follow existing conventions for immutable collections and value-object validation.

Where the authoritative specification defines restrictions, enforce them, including any relevant constraints on:

* Variable names.
* Duplicate names.
* Invalid or empty values.
* Disallowed environment overrides.
* Maximum collection size or value length.

Do not invent an expansive environment policy if the repository already defines one.

Do not include credentials, API keys, access tokens, or other secrets in diagnostic output or process-result representations.

Do not claim that the environment contract provides secret isolation; that requires enforcement by later execution components.

## 6. Timeout, Cancellation, and Resource Limits

Define explicit contracts for bounded process execution.

### Timeout

Represent timeout values using an appropriate duration abstraction.

Reject invalid or non-positive values unless the existing specification explicitly defines a different meaning.

Support a documented maximum where required.

Avoid machine-specific assumptions about execution duration.

### Cancellation

Represent cancellation intent using the established architecture.

Do not introduce background workers, cancellation infrastructure, process termination, or scheduling as part of this task unless explicitly required by PROCESS-001.

A cancellation request is not proof that a running operating-system process has terminated.

### Resource limits

Define the resource-limit representation required by the specification.

Use explicit units and validated ranges for supported limits.

Do not claim that a limit is enforced merely because it is present in a request. Enforcement belongs to the later execution infrastructure.

Do not introduce operating-system-specific sandboxing or resource-control APIs into Core.

## 7. Validation and Failure Semantics

Provide explicit validation outcomes consistent with existing domain and Application conventions.

The contract must distinguish invalid requests from requests that are structurally valid and ready for subsequent evaluation.

Where required by the existing architecture, distinguish:

* Missing or invalid executable identity.
* Invalid argument collections.
* Invalid working-directory references.
* Invalid environment configuration.
* Invalid timeout or resource-limit values.
* Unsupported execution definitions.
* Incomplete required metadata.

Do not conflate validation failures with policy denials, execution failures, timeouts, or cancellation outcomes.

Do not fabricate successful execution results from validated requests.

Reuse existing result abstractions where appropriate rather than introducing a parallel generic result framework.

## 8. Security Invariants

Preserve these invariants:

1. An agent cannot turn arbitrary text into unrestricted process-execution authority.
2. Executable selection must respect the future registered-execution boundary.
3. Arguments remain separate values and are not implicitly interpreted by a shell.
4. A process request cannot redefine trusted workspace roots.
5. Working-directory validation does not replace physical containment verification.
6. Request validation does not imply authorization.
7. Authorization does not imply execution.
8. Declared timeouts, cancellation, and resource limits are not claims of enforcement.
9. Untrusted workspace contents cannot change execution policy through embedded instructions.
10. The process contract does not grant network, filesystem, shell, or other host capabilities by implication.

Do not bypass `PolicyEvaluator` or duplicate its allow, deny, or escalate semantics.

## 9. Architecture and Dependencies

Preserve the established dependency direction.

* Core owns provider-independent process-domain concepts and invariants.
* Application owns execution-facing contracts where appropriate.
* Infrastructure owns concrete process-provider integrations in later tasks.
* Host owns composition and trusted runtime configuration.

Do not introduce operating-system process APIs into Core.

Do not add new NuGet packages unless explicitly required by the authoritative specification.

Do not implement process spawning, shell execution, sandboxing, output capture, persistence, telemetry, or external service integration as speculative future functionality.

## 10. Testing Requirements

Add focused tests appropriate to the actual PROCESS-001 scope.

Cover:

* Valid and invalid process requests.
* Executable identity validation.
* Ordered argument preservation.
* Argument-boundary preservation.
* Rejection of malformed argument entries.
* Working-directory contract validation.
* Controlled environment configuration.
* Duplicate or invalid environment entries.
* Timeout validation.
* Resource-limit validation.
* Cancellation contract behavior, where applicable.
* Immutability and defensive collection handling.
* Stable equality semantics where required.
* Reuse of existing workspace and policy abstractions.
* Separation of request validation from authorization and execution.

Tests must verify observable contracts rather than private implementation details.

Do not execute real operating-system processes in contract-only unit tests.

If the checked-in specification explicitly requires an implementation beyond contracts, add the appropriate isolated tests and preserve the architecture boundaries.

## 11. Documentation

Add `PROCESS-001-IMPLEMENTATION.md`, following existing repository conventions.

Document:

* The structured process request.
* Executable identity and future registration requirements.
* Argument-boundary semantics.
* Working-directory and workspace integration.
* Environment configuration.
* Timeout, cancellation, and resource-limit semantics.
* Validation and failure outcomes.
* Architectural ownership and dependency direction.
* Security guarantees and limitations.
* Deferred execution-provider responsibilities.
* Any discrepancy between the task prompt and the authoritative specification.

Preserve the supplied PROCESS-001 specification and unrelated untracked files unchanged.

## Acceptance Criteria

* [ ] The authoritative PROCESS-001 specification and implementation sequence were inspected.
* [ ] The implementation matches the checked-in task scope.
* [ ] Process execution is represented structurally, not as an unrestricted shell-command string.
* [ ] Executable identity is distinct from trusted executable configuration.
* [ ] Arguments remain ordered, individual values.
* [ ] Working-directory semantics reuse the established workspace architecture.
* [ ] Environment configuration is explicit and immutable.
* [ ] Timeout, cancellation, and resource-limit contracts are explicit where required.
* [ ] Validation failures are distinct from authorization and execution outcomes.
* [ ] Existing policy and workspace contracts remain compatible.
* [ ] Core remains independent of operating-system process APIs.
* [ ] No unnecessary dependencies or speculative infrastructure are introduced.
* [ ] Focused tests cover the applicable contracts and security invariants.
* [ ] Implementation documentation is added.
* [ ] Full solution tests pass.
* [ ] Full solution build succeeds with zero warnings and errors.
* [ ] `git diff --check` passes.
* [ ] Changed C# files have no reported diagnostics.
* [ ] Final reporting identifies verified behavior, limitations, and deferred work.

## Required Verification

Run:

1. `dotnet test AiGameStudioWorkstation.sln --verbosity minimal`
2. `dotnet build AiGameStudioWorkstation.sln`
3. `git diff --check`
4. C# diagnostics for all changed C# files.

Inspect the final diff and confirm that the supplied task specification and unrelated untracked files remain unchanged.

Report exact test counts, build warnings and errors, changed files, architecture findings, security limitations, and deviations from the authoritative specification.

## Execution Instructions

Work autonomously within the repository.

Begin with the mandatory specification audit. Implement the smallest cohesive solution that satisfies the actual PROCESS-001 acceptance criteria.

Do not redesign the tool pipeline or introduce speculative functionality from later phases.

If the authoritative specification leaves a security-sensitive behavior undefined, preserve the existing conservative security posture and document the unresolved decision rather than silently granting additional authority.

Stop after PROCESS-001. Do not begin PROCESS-002 or any subsequent task.
