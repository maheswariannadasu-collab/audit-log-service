# AI Usage Log

## Task: Project Architecture

AI Tool: ChatGPT

Prompt:
Suggest a maintainable architecture for an audit log service.

AI Output:
Suggested API, BLL, DAL and database layers.

Engineer Review:
Reviewed the architecture and selected the layers.

Status: Accepted with modifications

Reason:
Kept the architecture simple for the assignment.

## Task: Scenario C Compliance Reporting

AI Tool: AI coding assistant

Prompt:
Implement the compliance-reporting scenario with account/date filtering,
authorized access, unit tests, and aligned project documentation.

AI Output:
Helped analyze the requirement and implement the report service and API,
configuration-backed API-key protection for local assessment use, focused unit
tests, and documentation updates.

Engineer Review:
The project owner should review the implementation and validate the report
against the configured SQL Server database before submission.

Status: Implemented; human review and SQL-backed validation pending

Reason:
No identity provider was configured for the local project. The API key is a
minimal assessment access gate; it does not provide individual regulator
identity and is not the recommended production identity solution.

## Task: Protect Scenario A and B API Endpoints

AI Tool: AI coding assistant

Prompt:
Extend the existing `X-Compliance-Api-Key` authentication to all Scenario A
and B endpoints, reject unauthorized requests, and preserve the current
authentication mechanism.

AI Output:
Applied the existing authorization policy to the Scenario A and B controllers,
added request-level tests for missing and invalid keys across their routes, and
updated the related scenario and project documentation. No database changes
were needed.

Engineer Review:
The .NET 10 API build succeeded with zero warnings and errors. The full test
suite passed: 34 tests, including authorization checks for all 11 Scenario A
and B route mappings.

Status: Implemented and validated in the development workspace

Reason:
Scenario A and B endpoints had not been protected, while Scenario C already
used the API-key scheme. The same shared key now gates all three scenarios; it
does not provide individual identity or separate read/write permissions.
