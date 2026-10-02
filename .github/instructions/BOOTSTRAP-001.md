# BOOTSTRAP-001 — Establish Repository Constitution

You are implementing BOOTSTRAP-001 for the AI Game Studio Workstation.

Read these files first:

* `AGENTS.md`
* `.github/copilot-instructions.md`
* `docs/implementation/IMPLEMENTATION-PLAN.md`

Your task is only to verify and establish the repository bootstrap described by BOOTSTRAP-001.

## Scope

The repository must contain:

```text
AGENTS.md
.github/copilot-instructions.md
docs/implementation/IMPLEMENTATION-PLAN.md
```

Ensure the required directories exist.

Do not implement application source code.

Do not create the .NET solution.

Do not add NuGet packages.

Do not implement Core.

Do not implement Application.

Do not implement Infrastructure.

Do not implement Host.

Do not implement persistence.

Do not implement policy.

Do not implement tools.

Do not implement process execution.

Do not implement Git adapters.

Do not implement verification.

Do not implement agents.

Do not implement the orchestrator.

Do not implement Garry's Mod integration.

Do not implement any future task.

## Required verification

Verify:

1. All three bootstrap documents exist.
2. The architecture dependency direction is documented.
3. The capability/policy/tool boundary is documented.
4. The trust model is documented.
5. Git safety requirements are documented.
6. Verification semantics are documented.
7. `UNKNOWN != PASS` is documented.
8. The complete implementation sequence is documented.
9. No application source code was introduced.
10. No dependencies were added.
11. Git status contains only expected bootstrap changes.

If any requirement is ambiguous or contradictory:

STOP and report the problem.

## Completion report

Report:

* Task ID
* Files created or modified
* Verification performed
* Git status
* Security review
* Architecture review
* Acceptance criteria
* Known limitations
* Confirmation that BOOTSTRAP-001 is complete

Then STOP.

Do not begin FOUNDATION-001 automatically.
