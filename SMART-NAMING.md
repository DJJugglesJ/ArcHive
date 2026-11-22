# Smart naming verification

Implemented after 1.0.6. No schema or identity changes.

- The parser retains original strings and explicit token evidence. It recognizes prerelease suffixes, explicitly prefixed date versions, build numbers, platforms and labelled release-group suffixes. Bare numbers and unlabelled brackets are preserved. Irregular versions remain raw strings; parsing does not imply they can be reliably ordered.
- Batch rename templates: {title}, {version}, {platform}, {build}. Title is required; reviewed canonical title takes precedence over display title. Stored release values take precedence over parsed filename evidence. Missing values, unknown placeholders, malformed braces and unsafe output are rejected.
- Preview performs existing filesystem collision, ownership, active-session, save-folder, link and overlap checks. Apply retains source-fingerprint validation, launch-path rebasing and recovery journaling. Template edits invalidate the pending preview.
- 837 automated checks passed, including 16 added naming/template checks. Actual temporary archive fixtures verify distinct version destinations, collision rejection, rename execution and identity/title/review retention. Existing rename tests cover stale sources, persistence failures, rollback and launch-profile paths.
- Release build passed with zero warnings and errors; dependency vulnerability auditing remains enforced. No full UI or physical-controller test was performed for this milestone. Tests use temporary fixtures, not the user's library.
