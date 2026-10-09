#!/usr/bin/env python3
"""Read-only repository pattern audit. Reports locations and counts, never matches."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import xml.etree.ElementTree as ET

PATTERNS = {
    "library_file_identifier": r"\blibfile_[0-9a-f]{32}\b",
    "personal_absolute_path": r"(?i)(?:[a-z]:[\\/]+(?:Users|Documents and Settings)[\\/]+[^\\/\s\"']+|/(?:Users|home)/[^/\s\"']+)",
    "private_key_header": r"-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----",
    "provider_token": r"(?:sk-(?:proj-)?[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[baprs]-[A-Za-z0-9-]{20,}|AKIA[0-9A-Z]{16})",
    "jwt": r"\beyJ[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\b",
    "secret_literal_assignment": r"(?i)[\"']?(?:password|passwd|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret)[\"']?\s*[:=]\s*[\"'][^\"'\r\n]{4,}[\"']",
    "email_literal": r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
    "credential_url": r"https?://[^/\s:@]+:[^/\s@]+@",
    "credential_url_parameter": r"(?i)[?&](?:token|key|password|secret|access_token|api_key)=[^&\s\"']+",
}
REGEX = {key: re.compile(value) for key, value in PATTERNS.items()}
TEXT_SUFFIXES = {".cs", ".ps1", ".txt", ".md", ".json", ".xml", ".config",
                 ".yml", ".yaml", ".toml", ".ini", ".py", ".js", ".sln", ".csproj", ".resx", ".svg", ".diff"}
TEXT_NAMES = {".gitignore", ".gitattributes", "LICENSE", "NOTICE", "Makefile"}
SKIP_DIRS = {".git", "runtime", "fixture-runtime", "logs", "node_modules", "bin", "obj", "build"}
STATE_SUFFIXES = {".db", ".sqlite", ".sqlite3", ".exe", ".pdb", ".pfx", ".p12", ".pem", ".zip"}
STATE_NAMES = {"auth.json", ".env"}
SELF_REPORTS = {"audit/scan_privacy.py", "audit/privacy-audit.md", "audit/privacy-audit.json",
                "audit/privacy-scan-initial.json", "audit/privacy-scan-final.json"}
REVIEWED_SUPPRESSION = {
    "category": "email_literal", "file": "windows/src/ResetNewsChecks.cs", "line": 59,
    "lineSha256": "bfb7af2d3f607437bb9d005bc69e89869d28202c96a33581b3c04d8114522000",
    "expectedCount": 1,
    "reason": "Reviewed explicit negative SafeLink URL-userinfo assertion; exact source context only.",
}
REVIEWED_ICONSET_SUPPRESSIONS = [
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 34,
    "count": 1,
    "lineSha256": "73b63a4a1f37c3bf6c34d61c03278db74b21dd14c62f44cd222d7af8bc34c8c1",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 36,
    "count": 1,
    "lineSha256": "5e00ab2e6cd086f3fe5db650c8d4d638781efafee364af4fc4ae2f5ff6e62fb5",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 38,
    "count": 1,
    "lineSha256": "2da4ef8775d59814b1d716026af35da991fec8802b4d09c5e3fee6550b8d1e2e",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 40,
    "count": 1,
    "lineSha256": "2a79c8f078edc3e458e068b238d95aebab15ff8485e8879d35d5c228ec6b16fd",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 42,
    "count": 1,
    "lineSha256": "fed6270a4d12df4f1e60f6f207468154be5d062f584b2a7c4ca01de88927dd50",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 102,
    "count": 1,
    "lineSha256": "d6f702d17d8236292b9d0b2cba4734d4792bf164bf769936400237ad2a1e3080",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 114,
    "count": 1,
    "lineSha256": "6fc7285ee4f9360912ed7328c2b5061c1706b031ce2958232fd349c7342d4dd4",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 126,
    "count": 1,
    "lineSha256": "093a29b8fd4268ded2d515965a596b73f18a6a30058bded3b5f78daa06fa77c9",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 138,
    "count": 1,
    "lineSha256": "848ce83f32266462d92837b38435ee9364f3777d9d1b260eb0ddca55681e0540",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 150,
    "count": 1,
    "lineSha256": "00b939d4bf6e000f35485c53a78fca49e282c806d1c700a3489fd06c20f86282",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 162,
    "count": 1,
    "lineSha256": "e33d8f473c773ab4f3280789ac87c4c901340aa5934a15e661e090f2892104f7",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 174,
    "count": 1,
    "lineSha256": "16903e0f9d3600c215af8eee83ee316145bb4e7e22a39d7affa3fdceb96f0b5e",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 186,
    "count": 1,
    "lineSha256": "cbda545a661dc61ff6865058f0e82f1740b7c25d9a8150d16d25d3aa3df7a485",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 198,
    "count": 1,
    "lineSha256": "ecf58879b7b82d90a6b90ae6f89e8c425f094def0e96e292885e9d3accecc126",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  },
  {
    "category": "email_literal",
    "file": "shared/icons/asset-manifest.json",
    "line": 210,
    "count": 1,
    "lineSha256": "93407f5785a300379c5f39b28f521334679f3aa931265d76bab07b8585035cba",
    "reason": "Reviewed standard @2x iconset PNG filename in exact metadata line; generated asset exists in verified manifest."
  }
]


def png_metadata(blob):
    if not blob.startswith(b"\x89PNG\r\n\x1a\n"):
        return []
    result, offset = [], 8
    while offset + 12 <= len(blob):
        length = struct.unpack_from(">I", blob, offset)[0]
        kind = blob[offset + 4:offset + 8]
        if offset + length + 12 > len(blob):
            raise ValueError("truncated PNG chunk")
        if kind in {b"tEXt", b"zTXt", b"iTXt", b"eXIf"}:
            result.append(kind.decode("ascii"))
        offset += length + 12
    return result


def ico_record(relative, data):
    if len(data) < 6 or struct.unpack_from("<HH", data)[0:2] != (0, 1):
        raise ValueError("invalid ICO directory")
    count = struct.unpack_from("<H", data, 4)[0]
    metadata = []
    for index in range(count):
        entry = 6 + 16 * index
        if entry + 16 > len(data):
            raise ValueError("truncated ICO directory")
        size, offset = struct.unpack_from("<II", data, entry + 8)
        if offset + size > len(data):
            raise ValueError("truncated ICO frame")
        for kind in png_metadata(data[offset:offset + size]):
            metadata.append({"frame": index, "chunk": kind})
    decoded = data.decode("utf-8", "ignore") + "\n" + data.decode("utf-16-le", "ignore")
    selected = ("personal_absolute_path", "private_key_header", "provider_token", "credential_url")
    return {"file": relative, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
            "frameCount": count, "pngMetadataChunks": metadata,
            "selectedBinaryPatternCounts": {key: len(REGEX[key].findall(decoded)) for key in selected}}



def selected_binary_patterns(data):
    decoded = data.decode("utf-8", "ignore") + "\n" + data.decode("utf-16-le", "ignore")
    selected = ("personal_absolute_path", "private_key_header", "provider_token", "credential_url")
    return {key: len(REGEX[key].findall(decoded)) for key in selected}


def png_record(relative, data):
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("invalid PNG signature")
    return {"file": relative, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
            "metadataChunks": png_metadata(data),
            "selectedBinaryPatternCounts": selected_binary_patterns(data)}


def svg_record(relative, data):
    text = data.decode("utf-8-sig")
    document_declarations = len(re.findall(r"<!DOCTYPE|<!ENTITY", text, re.IGNORECASE))
    if document_declarations:
        raise ValueError("SVG document declarations are not permitted in geometry assets")
    root = ET.fromstring(text)
    if root.tag.rsplit("}", 1)[-1] != "svg":
        raise ValueError("not an SVG root")
    forbidden_media = []
    href_attributes = []
    external_style_references = 0
    tags = {}
    for element in root.iter():
        tag = element.tag.rsplit("}", 1)[-1]
        tags[tag] = tags.get(tag, 0) + 1
        if tag in {"image", "foreignObject", "script", "audio", "video", "iframe"}:
            forbidden_media.append(tag)
        for name, value in element.attrib.items():
            local = name.rsplit("}", 1)[-1]
            if local in {"href", "src"}:
                href_attributes.append(local)
            if re.search(r"url\s*\(|@import", value, re.IGNORECASE):
                external_style_references += 1
        if tag == "style" and re.search(r"url\s*\(|@import", element.text or "", re.IGNORECASE):
            external_style_references += 1
    return {"file": relative, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
            "elementCounts": tags, "forbiddenMediaElements": forbidden_media,
            "hrefOrSrcAttributes": href_attributes, "styleReferenceCount": external_style_references,
            "documentDeclarationCount": document_declarations,
            "geometryOnlyWithoutAssetReferences": not (
                forbidden_media or href_attributes or external_style_references or document_declarations)}


def scan(root, scope):
    report = {
        "scope": scope, "textFileCount": 0, "textPatternCounts": {key: 0 for key in PATTERNS},
        "actionablePatternCounts": {key: 0 for key in PATTERNS}, "reviewedSuppressions": [],
        "findings": [], "excludedStateEntries": [], "uninspectedBinaryFiles": [],
        "icoInspection": [], "pngInspection": [], "svgInspection": [], "readErrors": [], "secretValuesPrinted": False,
        "limitations": ["Pattern matching does not prove absence of every secret.",
                        "Binary pixels are not reviewed by this scanner.",
                        "Third-party redistribution rights require separate review.",
                        "Real state and credential files are listed but never read.",
                        "This scanner and its generated reports are excluded to prevent self-matches."]
    }
    roots = [root / "windows" / "src", root / "licenses"] if scope == "src-licenses" else [root]
    for start in roots:
        if not start.is_dir():
            report["readErrors"].append({"file": start.relative_to(root).as_posix(), "error": "missing scan directory"})
            continue
        for current, dirs, files in os.walk(start):
            current_path = Path(current)
            for name in list(dirs):
                relative = (current_path / name).relative_to(root).as_posix()
                if name.lower() in SKIP_DIRS:
                    report["excludedStateEntries"].append({"file": relative, "kind": "directory", "contentRead": False})
                    dirs.remove(name)
            for name in sorted(files):
                path = current_path / name
                relative = path.relative_to(root).as_posix()
                if relative in SELF_REPORTS:
                    continue
                if path.suffix.lower() in STATE_SUFFIXES or name.lower() in STATE_NAMES:
                    report["excludedStateEntries"].append({"file": relative, "kind": "file", "contentRead": False})
                    continue
                try:
                    if path.suffix.lower() == ".ico":
                        report["icoInspection"].append(ico_record(relative, path.read_bytes()))
                        continue
                    if path.suffix.lower() == ".png":
                        report["pngInspection"].append(png_record(relative, path.read_bytes()))
                        continue
                    if path.suffix.lower() == ".svg":
                        report["svgInspection"].append(svg_record(relative, path.read_bytes()))
                    if path.suffix.lower() not in TEXT_SUFFIXES and name not in TEXT_NAMES:
                        report["uninspectedBinaryFiles"].append({"file": relative, "bytes": path.stat().st_size})
                        continue
                    text = path.read_text(encoding="utf-8-sig")
                    report["textFileCount"] += 1
                    for number, line in enumerate(text.splitlines(), 1):
                        for category, regex in REGEX.items():
                            count = len(regex.findall(line))
                            if count:
                                report["textPatternCounts"][category] += count
                                finding = {"category": category, "file": relative, "line": number, "count": count}
                                reviewed = REVIEWED_SUPPRESSION
                                exact_reviewed_context = (
                                    category == reviewed["category"] and relative == reviewed["file"] and
                                    number == reviewed["line"] and count == reviewed["expectedCount"] and
                                    hashlib.sha256(line.encode("utf-8")).hexdigest() == reviewed["lineSha256"] and
                                    line.count("!ResetNewsParser.SafeLink(") == 3)
                                iconset_reviewed = next((item for item in REVIEWED_ICONSET_SUPPRESSIONS
                                    if item["category"] == category and item["file"] == relative and
                                    item["line"] == number and item["count"] == count and
                                    item["lineSha256"] == hashlib.sha256(line.encode("utf-8")).hexdigest()), None)
                                if iconset_reviewed is not None:
                                    report["reviewedSuppressions"].append(dict(finding,
                                        lineSha256=iconset_reviewed["lineSha256"], reason=iconset_reviewed["reason"]))
                                elif exact_reviewed_context:
                                    report["reviewedSuppressions"].append(dict(finding,
                                        lineSha256=reviewed["lineSha256"], reason=reviewed["reason"]))
                                else:
                                    report["actionablePatternCounts"][category] += count
                                    report["findings"].append(finding)
                except PermissionError:
                    report["readErrors"].append({"file": relative, "error": "permission denied; audit stopped"})
                    return report
                except (OSError, UnicodeError, ValueError, struct.error, ET.ParseError) as error:
                    report["readErrors"].append({"file": relative, "errorType": type(error).__name__})
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("repository", type=Path, help="Repository root; its absolute path is not reported.")
    parser.add_argument("--scope", choices=("src-licenses", "all"), default="all")
    args = parser.parse_args()
    report = scan(args.repository.resolve(), args.scope)
    print(json.dumps(report, ensure_ascii=True, indent=2))
    return 2 if report["readErrors"] else 0


if __name__ == "__main__":
    raise SystemExit(main())


