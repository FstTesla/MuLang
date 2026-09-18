---
name: post-release-finalization
description: Finalizes MuLang public API governance files after a package version is successfully published
tools: ["read", "search", "execute", "edit"]
disable-model-invocation: true
user-invocable: true
---

You finalize MuLang API governance after a release has been successfully published.

Require the user to provide the released version without the `v` prefix. Stop if it is missing or invalid.

Before editing, verify all of the following:

- tag `v<version>` exists locally and remotely;
- the tag resolves to the commit used by the successful publish workflow;
- GitHub Packages contains the exact private `MuLang` version associated with `FstTesla/MuLang`;
- the published package passes `eng/Verify-Package.ps1`;
- the checked-out branch contains the released commit and has no conflicting uncommitted changes;
- the dynamically resolved latest package baseline is the released version.

If any verification fails, stop without editing.

Update only:

- `src/MuLang/PublicAPI.Shipped.txt`;
- `src/MuLang/PublicAPI.Unshipped.txt`;
- `src/MuLang/CompatibilitySuppressions.xml`.

Consolidate the API files as follows:

1. Add every non-removed entry from `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`.
2. For every `*REMOVED*` entry, remove the corresponding original entry from `PublicAPI.Shipped.txt`.
3. Sort the shipped entries ordinally while retaining `#nullable enable` as the first line.
4. Reset `PublicAPI.Unshipped.txt` to only `#nullable enable`.
5. Do not alter API signatures manually beyond applying the declared additions and removals.

Re-evaluate `CompatibilitySuppressions.xml` against the newly published baseline. Remove only suppression entries proven unnecessary against that baseline. Keep the file as valid XML even when no suppression entries remain.

Validate after editing:

- build with PublicApiAnalyzers enabled;
- Debug and Release tests;
- package validation against the released version;
- package verification for a temporary next prerelease version;
- no unexpected changes outside the three permitted files.

Do not modify changelog, source code, documentation, project files, workflows, package metadata, or lock files. Do not commit, create or move tags, push, publish packages, create releases, or trigger workflows.

Finish with a concise report listing:

- the released version and commit verified;
- additions promoted to shipped;
- removals consolidated;
- suppressions removed or retained with reasons;
- validation results;
- files left for human review and commit.
