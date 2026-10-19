# Security Policy

## Project Status

AI Game Studio Workstation is an evolving software project. Security controls, integrations, and execution capabilities may be incomplete or under development.

Do not assume that every planned security control has been implemented, independently reviewed, or validated in a production environment.

## Supported Versions

| Version | Security Updates |
| --- | --- |
| Development / unreleased | Best effort; no guaranteed response time |
| Released versions | Support status will be documented for each release |

Only versions explicitly identified as supported in the project's release documentation should be considered eligible for security fixes.

## Reporting a Vulnerability

**Please do not disclose exploitable security vulnerabilities in public GitHub issues, discussions, or pull requests.**

Use GitHub's private vulnerability reporting feature when enabled for this repository.

If private reporting is unavailable, contact the repository maintainer privately through an established contact channel. Do not publish exploit details while requesting a contact address.

Include, where possible:

- Affected version, component, or commit.
- A description of the vulnerability and its security impact.
- Reproduction steps or a minimal proof of concept.
- Relevant logs or configuration details, with secrets and personal data removed.
- Any suggested mitigation.

Please avoid accessing, modifying, or disclosing data belonging to others while investigating a suspected vulnerability.

## Scope

Security reports are particularly relevant to:

- Authentication, authorization, and policy enforcement.
- AI-agent permissions and trust boundaries.
- Workspace access and filesystem containment.
- Tool invocation and process execution.
- Secret handling and configuration.
- Persistence, task ownership, and execution claims.
- Dependency vulnerabilities and software supply-chain risks.
- CI/CD workflows, release integrity, and repository permissions.

## Handling and Disclosure

The maintainer will assess reports in good faith and determine severity, affected versions, mitigation, and appropriate disclosure timing.

Please allow reasonable time for investigation and remediation before public disclosure. Acknowledgement, remediation, release, and disclosure timelines are not guaranteed.

Reporter attribution will be handled in consultation with the reporter, where practical.

## Security Limitations

This project is provided on an evolving, best-effort basis. A documented security requirement does not establish that the corresponding control is implemented or effective.

Do not use the workstation to execute untrusted code or grant agents access to sensitive systems unless the required protections have been implemented, tested, and explicitly approved.

Security documentation does not replace the applicable software license or create a separate warranty.
