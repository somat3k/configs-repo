# Rule: Session Complete

## Purpose
Define a clear, verifiable definition of "done" for every development session. Prevents partial work from being merged.

## Completion Criteria

A session is **complete** when ALL of the following are verified:

### 1. Module runs standalone
```bash
make run MODULE=<name>
# Process starts, no immediate exit
```

### 2. Health check passes
```bash
curl -sf http://localhost:<port>/health | jq .status
# Output: "ok"
```

### 3. Module registers on service mesh
```bash
curl -sf http://localhost:9000/mesh/members | jq '.[].name'
# Output includes "<name>"
```

### 4. Tests pass
```bash
make test MODULE=<name>
# All tests pass, 0 failures
```

### 5. Session notes committed
```
sessions/<session-id>/SESSION.md  ← exists and has "Results / Notes" filled in
```

## Enforcement

Before a PR can be merged:
- The dynamic-merge workflow checks for session notes.
- The PR checklist requires all items above to be checked.
- Copilot should refuse to call a session "done" until all criteria pass.

## Copilot Prompt

Use this prompt to verify session completion:

```
Run the session completion checklist for module <NAME>:
1. make run MODULE=<NAME>  (check it starts)
2. curl /health            (check 200 ok)
3. make test MODULE=<NAME> (check all pass)
4. Confirm sessions/<ID>/SESSION.md exists with results filled in
Report PASS or FAIL for each item.
```
