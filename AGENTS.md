# AGENTS

## Scope
- This repository is currently in bootstrap state.
- Apply these instructions to all work until project-specific instructions are added.

## Current State
- The repository currently includes `AgentFrameworkSolution.code-workspace` and `.github/copilot-instructions.md`.
- No source code, package manifests, build scripts, or tests are present yet.
- Architecture and coding conventions are defined in `.github/copilot-instructions.md`.

## How To Work Here
- Prefer minimal, incremental changes that establish clear project structure.
- Do not assume language, framework, or build system until the repository adds those files.
- Before running build or test commands, first detect project tooling from committed manifests.
- If tooling is missing, ask for confirmation before introducing new stacks or scaffolding.
- Follow `.github/copilot-instructions.md` for architecture and coding style when generating code.

## Documentation Practice
- When project docs are added, link to them instead of duplicating their content.
- Keep this file concise and update it when stable conventions appear.

## Update Triggers
- Update this file when any of the following are added:
  - build/test commands
  - architecture boundaries
  - coding style or linting rules
  - deployment or environment setup steps