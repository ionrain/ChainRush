# System Interaction Contract Template

Date: YYYY-MM-DD  
Status: Draft | Active | Deprecated

## 1) Passport

1. `System name`:
2. `Owner`:
3. `Layer/package`:
4. `Related blueprint`:

## 2) State Ownership

1. `Source-of-truth state`:
2. `Who can write`:
3. `Who can read`:
4. `Forbidden write paths`:

## 3) Inbound Surface

Commands/events/queries this system consumes:

1. `Type` -> `Contract` -> `From system` -> `Validation notes`

## 4) Outbound Surface

Commands/events/queries this system emits:

1. `Type` -> `Contract` -> `To system(s)` -> `Delivery guarantees`

## 5) Allowed Integration Channels

1. Events/command bus contracts.
2. Query/provider interfaces.
3. Explicit public API contracts of target system owner.

## 6) Forbidden Coupling

1. Direct runtime calls to foreign concrete classes.
2. Cross-system shared mutable state outside owner contract.
3. Untyped scene refs used as service locator dependencies.

## 7) Dependency Rules

1. Allowed assembly references:
2. Forbidden assembly references:
3. Bridge points (if any):

## 8) Failure & Recovery

1. Expected failure modes:
2. Fallback behavior:
3. Rollback-safe toggle/checkpoint:

## 9) Tests/Fitness Gates

1. Architecture test(s):
2. Behavior regression test(s):
3. Data-driven/file-sprawl gate(s):
