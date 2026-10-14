# AI-Assisted Development Workflow

## Purpose

This document defines the collaborative workflow between human engineers, the lead AI agent, and specialised subagents in the OMS repository.

## Standard Collaboration Workflow

All non-trivial engineering tasks follow this nine-step lifecycle:

1. **Inspection**: The lead agent inspects the repository state, relevant documentation, and task constraints to understand the problem thoroughly before proposing changes.
2. **Planning**: The lead agent prepares a concise implementation plan when the task involves multiple steps or architectural trade-offs.
3. **Research Delegation**: Independent research or discovery tasks may be delegated to read-only subagents (`subagent_explore`) where parallel investigation adds value.
4. **Implementation Ownership**: One implementation agent owns each overlapping area of code. Overlapping files, shared build configuration, or database migrations are never edited concurrently by multiple agents.
5. **Automated Verification**: Automated verification is executed against the resulting changes (via `/verify-change`).
6. **Independent Review**: An independent read-only reviewer (`reviewer` subagent profile or `/review-change`) evaluates the changes without modifying them.
7. **Resolution**: The lead agent addresses confirmed review findings without expanding task scope.
8. **Final Verification**: Final verification is executed to confirm all checks pass after corrections.
9. **Human Review & Approval**: A human engineer reviews and approves the result before changes are finalised or merged.

## Delegation & Parallelism Rules

- **Deliberate Delegation**: Agent delegation is a means of improving quality and focus, not a mandatory ritual for every small task. Simple, self-contained edits should be handled directly by the lead agent.
- **Genuine Independence**: Use parallel subagents only when work items are genuinely independent and touch disjoint sets of files.
- **No Concurrent Shared Edits**: Avoid concurrent modifications to shared configuration files (`Directory.Build.props`, `Directory.Packages.props`, `Oms.slnx`, `.editorconfig`), database migrations, or overlapping domain/application code.
- **Separation of Duties**: The independent `reviewer` subagent is strictly read-only. It must never edit files, apply fixes, or approve its own changes.

## Agent Responsibilities and Governance

Agents operating in this repository must adhere to the following governance rules:

- **No Silent Architectural Changes**: Agents must not silently alter architectural boundaries, consistency patterns, or technology choices. Architectural decisions require explicit human agreement and an ADR in `docs/adr/`.
- **No Unapproved Dependencies**: Agents must not install NuGet or npm packages without justification tied to an active requirement.
- **No Silent Instruction Edits**: Agents must not modify `AGENTS.md`, `.devin/` skills, or subagent profiles without explicit justification.
- **Evidence-Based Reporting**: Agents must report actual command outputs and distinguish clearly between checks that succeeded, checks that failed, and checks that could not be executed (for example, when no test projects exist yet).

## Repository-Local Devin Customisations

Devin capabilities are configured using supported project-local directories:

| Path | Type | Purpose |
| :--- | :--- | :--- |
| `.devin/skills/verify-change/SKILL.md` | Skill (`/verify-change`) | Identifies modified files, executes applicable builds/analysers/formatting/tests, and reports evidence-based outcomes. |
| `.devin/skills/review-change/SKILL.md` | Skill (`/review-change`) | Executes a structured review across correctness, architecture compliance, maintainability, security, test adequacy, complexity, and configuration consistency using the read-only `reviewer` profile. |
| `.devin/agents/reviewer.md` | Custom Subagent Profile (`reviewer`) | Read-only reviewer restricted to `read`, `grep`, and `glob` tools so it cannot modify files or execute destructive commands. |

## Third-Party Skills Policy

### Policy Rules

1. **No Uninspected Installation**: Third-party skills and agent plugins can contain arbitrary prompt instructions and tool permissions. Every external skill must be inspected for security, prompt injection risks, tool scope, and compatibility with repository standards before installation.
2. **Avoid Redundancy**: Do not install external skills that duplicate built-in capabilities or conflict with `AGENTS.md` and `docs/ENGINEERING.md`.
3. **Status in TASK 001**: No third-party skills are installed during the foundation bootstrap.

### Evaluation of Candidate Sources

#### 1. `https://github.com/dotnet/skills` (.NET Agent Skills)

- **Overview**: Curated portable skills (`SKILL.md` following the Agent Skills standard) maintained by the Microsoft .NET team across plugins such as `dotnet`, `dotnet-msbuild`, `dotnet-nuget`, `dotnet-data`, `dotnet-test`, `dotnet-aspnetcore`, and `dotnet-diag`.
- **Useful Candidates for OMS**:
  - `dotnet-msbuild`: Build failure diagnosis, analyser configuration, and MSBuild performance inspection.
  - `dotnet-test`: Running, filtering, and analysing xUnit test suites and code coverage once test projects are introduced.
  - `dotnet-data`: Entity Framework Core migration, query performance, and DbContext configuration guidance when PostgreSQL persistence is added.
  - `dotnet-aspnetcore`: Endpoint conventions, middleware ordering, and ASP.NET Core best practices.
- **Potential Benefits**: High technical accuracy aligned with official .NET 10 SDK and runtime guidance; portable `SKILL.md` format supported by Devin via `.agents/skills/` or `.devin/skills/`.
- **Compatibility Considerations**:
  - Some plugins in `dotnet/skills` reference `.NET Aspire`, `MSTest`, or GitHub Copilot-specific `.agent.md` files and static handoffs. If adopted selectively in future tasks, any skill referencing Aspire or MSTest must be excluded or adapted to respect OMS exclusions (no Aspire; xUnit + Testcontainers only).
  - Devin supports `SKILL.md` files natively, whereas Copilot `.agent.md` files are host-specific and should not be imported directly.

#### 2. `https://github.com/openai/skills` (OpenAI Codex Skills Catalogue)

- **Overview**: General-purpose skill catalogue originally created for OpenAI Codex (note: the upstream repository marks itself as deprecated in favour of the OpenAI Plugins repository, though its `.curated` and `.experimental` skills follow the portable `SKILL.md` layout).
- **Useful Candidates for OMS**:
  - Structured planning and PR comment resolution workflows (e.g. `create-plan`, `gh-address-comments`) as reference patterns.
- **Potential Benefits**: Lightweight workflow templates for general Git and pull request hygiene.
- **Compatibility Considerations**:
  - The repository is upstream-deprecated and targets Codex-specific installer conventions (`$skill-installer`) and `.codex/` paths.
  - Most general workflow needs are already covered by Devin's built-in tools and the repository-local `verify-change` and `review-change` skills, making direct installation from `openai/skills` unnecessary.

### Security Considerations for External Skills

- **Arbitrary Command Execution**: External skills may include helper scripts or prompt instructions that invoke shell commands (`exec`) or network calls.
- **Supply Chain & Drift**: Pinning or copying only vetted `SKILL.md` files into version control is safer than pulling unpinned upstream branches dynamically.
- **Permission Scoping**: Any external skill adopted in the future must have its `allowed-tools` and `permissions` audited so it cannot bypass project guardrails.
