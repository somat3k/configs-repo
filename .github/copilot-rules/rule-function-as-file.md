# Rule: Function-as-File

## Purpose
Keep each public function in its own file to maximise discoverability, testability, and AI context clarity.

## Enforcement

### File layout
```
src/
  commands/      ← one file per command function
  queries/       ← one file per query function
  invokers/      ← one file per invoker
  modules/       ← one directory per module (has its own commands/queries/invokers)
```

### Rules
- One `pub fn` / `def` (public) per file.
- Private helpers for that function live in the same file.
- Types/structs used exclusively by that function live in the same file.
- Tests for that function live in the same file (`#[cfg(test)]` or `test_` prefix).
- File name equals the function name in snake_case.

### What to flag
- Files with more than one exported function.
- Functions defined inline inside modules without their own file.
- Unnamed utility files (`utils.rs`, `helpers.py`) with multiple exports.

## Example

❌ Bad:
```
src/storage/mod.rs  → pub fn put(...), pub fn get(...), pub fn delete(...)
```

✅ Good:
```
src/storage/put.rs    → pub fn put(...)
src/storage/get.rs    → pub fn get(...)
src/storage/delete.rs → pub fn delete(...)
```
