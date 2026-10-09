# PROCESS-001 Implementation — Structured Process Request Contract

## Contract

Core defines `ExecutionDefinitionId`, `ProcessExecutionRequest`, and the related
working-directory, environment, and resource-limit values. A request selects a
stable execution-definition identifier, not an executable path or command
string. The identifier does not itself prove that a definition is registered,
trusted, supported, or authorized.

Arguments are copied into an immutable ordered collection. Each string remains
one argument; values are never joined or interpreted as shell syntax. Empty
argument values remain valid, while null entries and null characters are
rejected. This contract does not make an arbitrary executable safe.

The working directory combines an existing `WorkspaceId` with a relative
`WorkspaceResourceId`. It contains no physical root and cannot expand
workspace-permitted roots. `WorkspaceResourceId` provides lexical path
validation only. Application/Infrastructure must retrieve trusted registration,
resolve containment through `IWorkspacePathResolver`, and independently
evaluate policy before any future execution.

## Environment

The request contains an explicit immutable environment collection; an empty
collection is available as `ProcessEnvironment.Empty`. Names use a constrained
ASCII identifier form and duplicates are rejected case-insensitively. Empty
values and null characters are rejected. Entries are sorted deterministically
for equality and inspection; their input order has no meaning.

The repository contains no process-specific environment allowlist, protected
override list, collection maximum, or value-length maximum. This contract does
not invent those policy rules; a trusted execution definition and later
execution boundary must enforce them. Explicit values do not isolate secrets
from a process.

## Bounds and cancellation

Timeout is required and must be positive. No maximum duration is specified in
the available contract, so this layer adds no machine-specific maximum.
`ProcessResourceLimits` supports optional positive `MemoryLimitBytes` and
`CpuTimeLimit` values. These declarations are descriptive only; no limits are
enforced here.

The existing domain has no cancellation contract. Cancellation is therefore
not embedded in the immutable process request or durable process configuration.
The Application `ProcessExecutionContext` carries a `CancellationToken`
separately from the request as invocation-level cancellation intent. A future
execution provider may observe it; requesting cancellation does not prove that
an operating-system process terminated. This task adds no worker, scheduler,
or termination behavior.

## Validation and boundaries

Malformed values and incomplete required metadata are rejected at construction
with argument exceptions identifying the invalid parameter. A constructed
request is structurally valid only. It is not an authorization decision,
execution result, or proof of containment. In particular, whether an execution
definition is supported can only be determined by a future trusted definition
registry; no registry exists or is implemented in PROCESS-001. These failures
remain distinct from policy denials, execution failures, timeouts, and
cancellation outcomes.

Core owns provider-independent process values and references only Core workspace
identifiers. Application may consume these contracts in later orchestration.
Infrastructure and Host remain responsible for later execution providers,
trusted definitions, and composition. Core uses no operating-system process
APIs; no process is spawned and no shell, filesystem, network, or other host
capability is granted.

## Audit notes and limitations

The implementation sequence, repository constitution, policy contracts, and
TOOL-001 through TOOL-003 workspace contracts were inspected. No separate
architecture-document tree, established process-pipeline document, or MCP
Pipeline Matrix is present in the checked-in repository. The existing
agent-to-validation-to-scope/policy-to-tool boundary and workspace resolver
contracts were used without introducing a replacement pipeline.

The prompt requests cancellation semantics "required by the existing
architecture", but no such existing cancellation contract was found. The narrow
Application context establishes the invocation-level token contract without
adding cancellation infrastructure or execution behavior. Environment
restrictions beyond structural validation and exact resource-limit enforcement
remain future trusted execution-definition and provider responsibilities.
