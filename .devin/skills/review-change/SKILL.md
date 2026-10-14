---
name: review-change
description: Perform an independent read-only review of repository changes for correctness, architecture compliance, maintainability, security, and test adequacy
agent: reviewer
triggers:
  - user
  - model
---

Perform a rigorous, independent, read-only review of the changes in the OMS repository.

## Review Mandate

- **Read-Only**: Do not modify files, apply fixes, or approve your own changes.
- Inspect all modified and newly created files against `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/ENGINEERING.md`, `docs/TESTING.md`, and `docs/AI-WORKFLOW.md`.

## Evaluation Checklist

Evaluate the changes across all seven required dimensions:

1. **Correctness**
   - Are there logic errors, invalid MSBuild/XML/YAML/JSON syntax, broken Markdown links, or inaccurate statements?
   - Does the documentation accurately describe the current state of the repository without claiming planned features are already implemented?

2. **Architecture Compliance**
   - Do the changes respect the principles in `docs/ARCHITECTURE.md`?
   - Are prohibited technologies (`.NET Aspire`, `MassTransit`, direct `Polly` API usage) strictly excluded?
   - Are open architectural decisions (such as physical service boundaries and deployment topology) preserved as open rather than prematurely fixed without an ADR?

3. **Maintainability**
   - Are files concise, well-structured, and written in professional British English?
   - Does `AGENTS.md` remain short and reference authoritative documents rather than duplicating their contents?

4. **Security**
   - Are any secrets, credentials, tokens, or fabricated server URLs hardcoded?
   - Are `.gitignore` patterns and dependency management settings safe and appropriate?

5. **Test Adequacy**
   - Are testing expectations and verification procedures realistic for the current project state?
   - If application code exists, are appropriate unit and integration tests present?

6. **Unnecessary Complexity**
   - Have any speculative abstractions, unrequested NuGet packages, extra projects, or unnecessary agent profiles been introduced?

7. **Conflicting Instructions or Configuration**
   - Are `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `AGENTS.md`, `docs/*.md`, and `.devin/` configurations mutually consistent?
   - Do skill and subagent frontmatter fields strictly conform to documented Devin CLI properties?

## Output Format

Report findings grouped by severity:

- **Blocking / High**: Must be fixed before task completion.
- **Medium / Low**: Concrete improvements within task scope.
- **Verdict**: State clearly whether the changes meet OMS standards or require corrections, citing specific file paths and line numbers for every finding.
