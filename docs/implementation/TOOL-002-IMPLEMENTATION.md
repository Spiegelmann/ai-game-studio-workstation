# TOOL-002 Implementation — Workspace Path Resolution & Containment

## Resolution boundary

`WorkspaceResourceId` remains the TOOL-001 semantic identifier. Its `Value` preserves the supplied spelling for compatibility; `NormalizedValue` converts either accepted separator (`/` or `\`) to `/`. Parent/current-directory segments, empty segments, rooted and drive-qualified identifiers, control characters, Windows-invalid filename characters, trailing dots/spaces, and Windows device names are rejected when the identifier is constructed. These checks are deterministic and independent of the host operating system. They are lexical validation only.

The Application contract in `Application.Workspace` takes the existing `WorkspaceDescriptor` and `WorkspaceOperationRequest`, together with a configured physical root. The physical root must come from trusted workspace configuration, not the operation request or an agent. The contract carries the logical workspace/root identities and preserves the original resource and operation identifiers.

`IWorkspacePathResolver` is a read-only resolution contract. It does not classify a resource, evaluate policy, authorize a capability, or execute a workspace operation. Its immutable result separates:

* the logical workspace/root and requested resource;
* the normalized absolute candidate path;
* the verified existing target, when there is one;
* the last verified existing directory ancestor for a prospective target;
* target kind (existing or prospective file/directory);
* containment and resolution status; and
* an explicit failure reason for all non-success outcomes.

Only `ResolvedWithinPermittedRoot` may report `WithinPermittedWorkspace`. Outside-permitted-root results report `OutsidePermittedWorkspace`; all rejected, missing, indeterminate, and failed results report `Unknown`. A prospective path is not represented as an existing verified target.

## Filesystem checks

`PhysicalWorkspacePathResolver` supports local Windows and Linux filesystems. It requires an existing, fully qualified local workspace root and rejects Windows UNC/device roots as unsupported. Configured root components and resource components are checked individually; a symbolic link or Windows reparse point anywhere in that chain is rejected rather than followed, even if it currently points back inside the workspace. On Linux, `/proc/self/mountinfo` is read and mount points crossed below the configured physical root are rejected; an unavailable or malformed mount table yields an indeterminate result. Other operating systems are currently reported as unsupported/indeterminate rather than receiving a success-shaped result.

Containment uses `Path.GetRelativePath` and rooted/parent-segment checks rather than string-prefix matching. The request must also fall beneath one of the descriptor's permitted resource roots by exact normalized segment comparison. This deliberately rejects case-only spelling variants in the semantic permitted-root comparison; no assumptions are made that every Windows/macOS filesystem is case-insensitive. Filesystem components are verified using the active platform's filesystem behavior. Protected paths and resource classifications remain separate policy inputs and are not interpreted as authorization here.

`Read` requires an existing file. `List` requires an existing directory. `Write` permits an existing non-directory target or a missing final file whose parent already exists and has been verified. `CreateDirectory` permits an existing directory or a prospective directory with any number of missing trailing components after verifying the existing ancestor chain. Missing read/list targets, missing write ancestors, wrong target kinds, non-directory ancestors, inaccessible paths, and unsupported roots never produce successful containment. Resolution itself performs no filesystem mutation.

## Platform assumptions, limitations, and races

The resolver supports local filesystems exposed through `System.IO` and rejects reparse points it observes. Filesystem attributes and canonical path calculations are path-based; they do not pin directory/file handles or use platform-specific handle-relative traversal. Consequently, a concurrent process can replace a checked component after inspection or before a later operation (TOCTOU). A successful result describes the checked filesystem state only; it is not a race-free capability to open, list, write, or create the path.

Network shares, Windows device/extended paths, link-heavy roots, permission-sensitive configurations, and filesystems with semantics the runtime cannot establish are rejected or reported as indeterminate. Permission and I/O exceptions preserve an explicit failure category without exposing exception details. Internal symlinks are rejected conservatively; the resolver does not attempt to prove their target stays inside the workspace.

The resolver cannot enforce containment against mutation between resolution and use. Linux mount information can also change after it is read. A future operation gateway must independently validate policy and use an execution mechanism that binds validation to the actual filesystem operation (or revalidates and treats any intervening change as unsafe). It must not treat this result as permission or as proof of subsequent operation success.

## Verification coverage

Core tests cover lexical traversal/rooted/ambiguous-name rejection and separator normalization. Application tests enforce request identity and result consistency, including keeping prospective targets distinct from verified existing targets. Infrastructure filesystem tests exercise all four TOOL-001 operations, sibling-prefix boundaries, case-exact permitted roots, existing/prospective/missing targets, wrong target kinds, invalid roots, and symlink rejection when the host permits link creation. A Linux permission-denial test reports when the host identity can bypass the restrictive mode; other platforms report the permission fixture as unsupported. Symlink coverage similarly reports an explicit unsupported result if the test host denies link creation.
