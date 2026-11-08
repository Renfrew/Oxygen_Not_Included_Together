---
applyTo: "ONI_United/STRINGS.cs,ONI_United/ModAssets/translations/*.po"
---

# Incremental localization updates

When `STRINGS.cs` changes, update `de.po`, `pl.po`, `zh.po`, `ko.po`, and `ru.po`
directly. Add or revise only the affected entries; do not regenerate catalogs or
POT files, reorder unrelated entries, or use translation APIs.

Keep each `msgctxt` aligned with its full `ONI_United.STRINGS...` key and each
`msgid` byte-for-byte equivalent to the current English source. Preserve format
placeholders (`{0}`), supported markup tags, newline counts, and zero-width-space
sentinels in `msgstr`. Use UTF-8 without a BOM and valid PO multiline syntax.

After editing, run `python3 scripts/validate_localizations.py`, then report its
result, `git diff --check`, the focused diff, and any untranslated or
native-speaker-review uncertainty. Leave changes unstaged.
