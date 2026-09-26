# Working in this repo (read first)

More than one Claude Code session works on this repo at the same time. Sharing one folder has
already broken `main` three times: a commit made in the shared folder swept up another session's
unfinished files. These rules prevent that.

## Git rules

- **Work in your own git worktree, on your own branch.** Never edit or commit in a folder another
  session is using, and never switch its branch.
  ```
  git fetch origin
  git worktree add ..\lvss-<topic> -b feature/<topic> origin/main
  ```
  Remove it when the work is merged: `git worktree remove ..\lvss-<topic>`.
- **`main` is protected.** Changes land only through a pull request with the `backend`,
  `frontend` and `bicep` CI checks green. Admins included. Don't try to push to `main`.
- **Stage files by name**, not `git add -A` / `git add .`, and read `git diff --cached` before
  committing. Anything you didn't write this session doesn't go in your commit.
- **Never use bare `git stash` / `git stash pop`.** The stash is shared across worktrees and
  sessions.
- Before opening a PR, run what CI runs:
  - `dotnet build backend/SoccerSchool.Api/SoccerSchool.Api.csproj -c Release`
  - `dotnet test backend/SoccerSchool.Api.Tests/SoccerSchool.Api.Tests.csproj -c Release`
  - `cd frontend && npx tsc -b && npm run build`
  - for mobile changes, `cd mobile && npx tsc --noEmit`

## Database migrations

- Migrations run automatically when the API starts (`MigrateWithRetryAsync` in `Program.cs`). A
  migration that fails crashes every new release on boot.
- SQL Server rejects a second cascade path into a table (error 1785, "may cause cycles or multiple
  cascade paths"). `SetNull` counts as a cascade. For optional FKs to `AspNetUsers` on tables that
  already cascade from `ParentAccounts`, use `DeleteBehavior.ClientSetNull`.
- Before pushing a migration, apply it to a local database (`dotnet ef database update`) and run
  `dotnet ef migrations has-pending-model-changes`.

## Deploys

- Merging to `main` deploys to production (Azure Container Apps).
- The Deploy workflow does **not** check that the new revision started. A green Deploy only means
  the image was pushed. Confirm the latest revision is running and healthy in the Azure portal (or
  `az containerapp revision list`) after anything that touches startup or migrations.

## Security conventions

- **Authorization is permission-based.** Protect endpoints with
  `[RequirePermission(Permissions.X)]` (see `Auth/Permissions.cs`), not role names. Roles are
  bundles of permissions an admin can edit; Admin always has all of them. Permissions don't carry
  scope: endpoints must still limit coaches to their own teams (`IPermissionService` /
  `ICoachScopeService`) and parents to their own family. Never rename a permission key; add a
  new one and give it defaults in the catalogue.

- Never grant access because an email *matches* (coach cards, additional-parent contacts, chat
  seeding) unless the login's `EmailConfirmed` is true. Sign-up does not verify email.
