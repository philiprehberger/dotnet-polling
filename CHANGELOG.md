# Changelog

## 0.2.5 (2026-03-31)

- Standardize README to 3-badge format with emoji Support section
- Update CI actions to v5 for Node.js 24 compatibility
- Add GitHub issue templates, dependabot config, and PR template

## 0.2.4 (2026-03-26)

- Add Sponsor badge to README
- Fix License section format
- Add trailing period to description

## 0.2.3 (2026-03-24)

- Sync .csproj description with README

## 0.2.2 (2026-03-22)

- Add dates to changelog entries

## 0.2.1 (2026-03-17)

- Rename Install section to Installation in README per package guide

## 0.2.0 (2026-03-16)

- Add `WithMaxAttempts` to limit polling attempts independent of timeout
- Add `PollContext` providing attempt number and elapsed time to predicates
- Add context-aware `Until` predicate overload
- Add `OnlyRetryOn<TException>` for exception type filtering

## 0.1.3 (2026-03-16)

- Add Development section to README
- Add GenerateDocumentationFile, RepositoryType, PackageReadmeFile to .csproj

## 0.1.1 (2026-03-16)

- Fix: add NuGet publishing secret

## 0.1.0 (2026-03-15)

- Initial release
- Static `Poll.Until<T>()` entry point with predicate-based polling
- Side-effect overload `Poll.Until()` for fire-and-forget operations
- Fluent builder with `Every()`, `WithTimeout()`, `WithBackoff()`, `WithCancellation()`
- Backoff strategies: Constant, Linear, Exponential, ExponentialWithJitter
- `PollResult<T>` record with `Succeeded`, `Value`, `Attempts`, `Elapsed`, `IsTimedOut`
- `OnAttempt()` callback for observability
