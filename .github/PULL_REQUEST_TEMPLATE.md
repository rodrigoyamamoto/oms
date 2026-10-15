## Summary

<!-- Provide a concise summary of the changes and the motivation behind them. -->

## Related Task / ADR

<!-- Reference the task identifier (e.g. TASK 001) and any relevant ADR in docs/adr/. -->
- Task:
- ADR (if applicable):

## Architectural & Scope Impact

- [ ] Changes adhere to [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) and [`docs/ENGINEERING.md`](../docs/ENGINEERING.md).
- [ ] No prohibited dependencies introduced (`.NET Aspire`, `MassTransit`, or direct `Polly` API usage).
- [ ] All NuGet package versions (if any) are managed centrally in `Directory.Packages.props`.
- [ ] No unrequested features or speculative abstractions added.

## Verification Evidence

<!-- Record the exact commands executed and their actual outputs. Distinguish clearly between passed checks and checks that were not applicable or not executed. -->

| Check | Command | Outcome (Passed / Failed / Not Applicable) | Notes |
| :--- | :--- | :--- | :--- |
| Build (Debug) | `dotnet build Oms.slnx` | | |
| Build (Release) | `dotnet build Oms.slnx -c Release` | | |
| Formatting | `dotnet format Oms.slnx --verify-no-changes` | | |
| Unit Tests | `dotnet test Oms.slnx` | | |
| Integration Tests | `dotnet test Oms.slnx` | | |

## Independent Review & Engineering Quality Gate

<!-- Summarise the pre-handoff evaluation against docs/QUALITY-GATES.md (Areas A–I), meaningful regression risks tested, independent reviewer verdict (APPROVED / APPROVED WITH NOTES / NOT APPROVED), findings resolved, and any remaining limitations. -->

- [ ] Pre-handoff Engineering Quality Gate ([`docs/QUALITY-GATES.md`](../docs/QUALITY-GATES.md)) completed.
- [ ] Independent review completed (via `/review-change` or `reviewer` subagent) and confirmed findings resolved.
- [ ] Human engineer review and approval required before merge.
