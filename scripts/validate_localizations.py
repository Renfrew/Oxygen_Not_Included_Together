#!/usr/bin/env python3
"""Validate ONI United gettext catalogs against STRINGS.cs.

This intentionally uses only the Python standard library. It does not generate or
rewrite catalogs; validation is deterministic and read-only.
"""

from __future__ import annotations

import argparse
import ast
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
import re
import sys


DEFAULT_LANGUAGES = ("de", "pl", "zh", "ko", "ru")
KEY_PREFIX = "ONI_United.STRINGS."
CLASS_RE = re.compile(r"^public class ([A-Za-z_][A-Za-z0-9_]*)$")
LOCSTRING_RE = re.compile(
    r'^public static LocString ([A-Za-z_][A-Za-z0-9_]*)\s*=\s*("(?:\\.|[^"\\])*");$'
)
PLACEHOLDER_RE = re.compile(r"\{\d+\}")
MARKUP_RE = re.compile(r"</?(?:b|color|link)(?:=[^>]+)?>")
FIELD_RE = re.compile(r"^(msgctxt|msgid|msgstr)\s+(\".*\")$")


class ValidationError(Exception):
    pass


@dataclass(frozen=True)
class PoEntry:
    context: str | None
    msgid: str
    msgstr: str
    line: int


def quoted_value(raw: str, path: Path, line: int) -> str:
    try:
        value = ast.literal_eval(raw)
    except (SyntaxError, ValueError) as exc:
        raise ValidationError(f"{path}:{line}: invalid quoted PO string: {exc}") from exc
    if not isinstance(value, str):
        raise ValidationError(f"{path}:{line}: expected a quoted string")
    return value


def csharp_value(raw: str, path: Path, line: int) -> str:
    try:
        value = ast.literal_eval(raw)
    except (SyntaxError, ValueError) as exc:
        raise ValidationError(f"{path}:{line}: unsupported C# string literal: {exc}") from exc
    if not isinstance(value, str):
        raise ValidationError(f"{path}:{line}: expected a C# string literal")
    return value


def source_strings(path: Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8-sig")
    classes: list[str] = []
    strings: dict[str, str] = {}

    for line_number, original in enumerate(text.splitlines(), 1):
        line = original.strip()
        if not line or line.startswith("//"):
            continue

        class_match = CLASS_RE.fullmatch(line)
        if class_match:
            classes.append(class_match.group(1))
            continue

        if line == "}":
            if classes:
                classes.pop()
            continue

        string_match = LOCSTRING_RE.fullmatch(line)
        if not string_match:
            continue
        if not classes:
            raise ValidationError(f"{path}:{line_number}: LocString is outside a class")

        name, raw_value = string_match.groups()
        key = KEY_PREFIX + ".".join((*classes, name))
        if key in strings:
            raise ValidationError(f"{path}:{line_number}: duplicate LocString key {key}")
        strings[key] = csharp_value(raw_value, path, line_number)

    if not strings:
        raise ValidationError(f"{path}: no LocString declarations found")
    return strings


def po_entries(path: Path) -> list[PoEntry]:
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        raise ValidationError(f"{path}: UTF-8 BOM is not allowed")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise ValidationError(f"{path}: not valid UTF-8: {exc}") from exc

    entries: list[PoEntry] = []
    fields: dict[str, str] = {}
    current_field: str | None = None
    entry_line = 1
    obsolete = False

    def finish() -> None:
        nonlocal fields, current_field, entry_line, obsolete
        if fields and not obsolete:
            if "msgid" not in fields or "msgstr" not in fields:
                raise ValidationError(f"{path}:{entry_line}: entry lacks msgid or msgstr")
            entries.append(
                PoEntry(fields.get("msgctxt"), fields["msgid"], fields["msgstr"], entry_line)
            )
        fields = {}
        current_field = None
        obsolete = False

    for line_number, line in enumerate(text.splitlines(), 1):
        if not line.strip():
            finish()
            entry_line = line_number + 1
            continue
        if line.startswith("#~"):
            obsolete = True
            continue
        if line.startswith("#"):
            continue
        if "msgid_plural" in line or re.match(r"^msgstr\[\d+\]", line):
            raise ValidationError(f"{path}:{line_number}: plural entries are not supported")

        field_match = FIELD_RE.fullmatch(line)
        if field_match:
            current_field, raw_value = field_match.groups()
            if current_field in fields:
                raise ValidationError(
                    f"{path}:{line_number}: duplicate {current_field} in one entry"
                )
            fields[current_field] = quoted_value(raw_value, path, line_number)
            continue
        if line.startswith('"'):
            if current_field is None:
                raise ValidationError(f"{path}:{line_number}: orphaned continuation string")
            fields[current_field] += quoted_value(line, path, line_number)
            continue
        raise ValidationError(f"{path}:{line_number}: unrecognized PO syntax")

    finish()
    return entries


def compare_catalog(path: Path, expected: dict[str, str]) -> list[str]:
    problems: list[str] = []
    entries = [entry for entry in po_entries(path) if entry.context is not None]
    by_context: dict[str, PoEntry] = {}

    for entry in entries:
        assert entry.context is not None
        if entry.context in by_context:
            problems.append(f"duplicate key: {entry.context}")
        else:
            by_context[entry.context] = entry

    for key in sorted(set(expected) - set(by_context)):
        problems.append(f"missing key: {key}")
    for key in sorted(set(by_context) - set(expected)):
        problems.append(f"unknown key: {key}")

    for key in sorted(set(expected) & set(by_context)):
        entry = by_context[key]
        source = expected[key]
        translation = entry.msgstr
        if entry.msgid != source:
            problems.append(f"source text mismatch: {key}")
            continue
        if source and not translation:
            problems.append(f"empty translation: {key}")
        if Counter(PLACEHOLDER_RE.findall(source)) != Counter(
            PLACEHOLDER_RE.findall(translation)
        ):
            problems.append(f"placeholder mismatch: {key}")
        if MARKUP_RE.findall(source) != MARKUP_RE.findall(translation):
            problems.append(f"markup mismatch: {key}")
        if source.count("\n") != translation.count("\n"):
            problems.append(f"newline mismatch: {key}")
        if source.count("\u200b") != translation.count("\u200b"):
            problems.append(f"zero-width-space mismatch: {key}")

    return problems


def parse_args() -> argparse.Namespace:
    script = Path(__file__).resolve()
    default_root = script.parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=default_root, help="repository root")
    parser.add_argument(
        "--languages",
        nargs="+",
        default=list(DEFAULT_LANGUAGES),
        metavar="LANG",
        help="catalog basenames to validate (default: %(default)s)",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    strings_path = root / "ONI_United" / "STRINGS.cs"
    translations = root / "ONI_United" / "ModAssets" / "translations"

    try:
        expected = source_strings(strings_path)
        failed = False
        for language in args.languages:
            path = translations / f"{language}.po"
            if not path.is_file():
                print(f"FAIL {language}: catalog not found: {path}")
                failed = True
                continue
            problems = compare_catalog(path, expected)
            if problems:
                failed = True
                print(f"FAIL {language}: {len(problems)} problem(s)")
                for problem in problems:
                    print(f"  - {problem}")
            else:
                print(f"PASS {language}: {len(expected)} keys")
        return 1 if failed else 0
    except (OSError, ValidationError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
