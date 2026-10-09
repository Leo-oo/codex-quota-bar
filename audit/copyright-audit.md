# Static copyright and icon-resource audit: Windows 0.11.10

The current source, unchanged icon media, and new 0.11.10.0 EXE pass this static audit. The EXE was read as bytes and was not loaded or run. Its SHA-256 is `f0f7090202bf196ce7a969bdc3b4c1e03d66181182798c9b591d1c8a6fa709f7`.

All 34 current C# hashes match [the frozen baseline](baseline.json). Against 0.11.9, 27 are byte-identical; seven contain the history fix and its checks/help text, version updates, and removal of a private design identifier. Valid policy-3 offline history and cross-day messages are preserved while refetch clears ETag. Quota, input, menu, and icon-loading/drawing sources are unchanged. [The JSON report](copyright-audit.json) records each source hash and the comparison.

All 52 shared icon files and three Windows ICOs retain the 0.11.9 hashes. The current [asset manifest](../shared/icons/asset-manifest.json) has 53 independently verified size/hash records. Both SVGs contain geometry only, with no image, href, src, or external asset. No public media match the recorded private-reference hash; this auditor did not open the private original.

The new x64 EXE contains exactly two CLR media resources, `QuotaBar.Tray.Dark.ico` and `QuotaBar.Tray.Light.ico`. Their bytes match the current tray ICOs. Its native RT_GROUP_ICON has ten frames (16, 18, 20, 22, 24, 32, 40, 48, 64, and 256), all byte-identical to the current program ICO frames. The two identified archived client ICOs and their sixteen frame payloads are absent from the new EXE and standalone public media. This establishes removal of those known bytes; it is not a blanket third-party-rights conclusion.

[The upstream MIT notice](../licenses/LICENSE.upstream.txt), Copyright (c) 2026 Zoey Liew, is intact. It covers the declared upstream adapted material and does not automatically license the additions or artwork. No project-wide MIT grant or public publication was performed; those remain user decisions.

No normal application, live Codex UI, account/runtime/authentication data, or Mac project was accessed. Shared iconset PNGs are media assets only; this audit does not claim Mac runtime compatibility. Normal builds and synthetic product checks are recorded by their assigned reviewers separately.

