# Windows fixture tests

These fixtures compile the frozen business sources through explicit test entry
points. They do not run the normal application, contact the network, start Codex
or an app server, or read real account and message-cache files.

Run from the repository root on Windows. The runners use the installed .NET
Framework 64-bit C# compiler found through `WINDIR` or `SystemRoot`.
The message regression needs Node.js; the card, icon and history fixtures need Python 3.
They use only standard-library modules and install nothing.

```powershell
node .\windows\tests\run_news_unread_regression118.js
python .\windows\tests\run_card_freeze110.py
python .\windows\tests\run_icon119.py
python .\windows\tests\run_history110.py
python .\windows\tests\run_history_migration110.py
python .\windows\tests\run_history_layout110.py
```

| Fixture | Required assertions | Scope |
| --- | ---: | --- |
| `NewsUnreadRegression118` | 79 | Synthetic message state, display fingerprints, read-version migration, frozen-card predicates, and unshown bitmap/layout checks. |
| `CardFreeze110Probe` | 46 | Original eight stages and all native paint/read oracles, with only its own host/card raised without activation on show/reopen to ensure fixture visibility. |
| `Icon119Probe` | 247 | Independent SVG, PNG and classic ICO decoding; full-frame runtime equality, exact ink and alpha, nine requested memory-HICON sizes per theme, composition and resource release. |
| `NewsHistoryRegression110` | 22 | Synthetic confirmed-event history, explicit Beijing time and date precision, ordering, rollover, read migration and unshown empty-card preview. |
| `HistoryMigration110Probe` | 8 | Synthetic policy3 same-day and cross-day history/activities/read preservation, ETag refetch and invalid/legacy clearing. |
| `HistoryLayout110Probe` | 12 | Unshown light/dark history layout at 125%, independent glyph/ROI equality and complete footer. |

The native fixture briefly displays its own windows and uses the production
dismissal hook and timer. It sends no global keyboard or mouse input. The message
fixture never shows its owner window. Neither fixture establishes that a real
user's issue is fixed.

The original `CardFreeze118Probe.cs` remains byte-identical. Its native run and
a private v0.11.9 source control both stalled at the same row-hit gate. Raising
only the fixture's own host and card with `HWND_TOPMOST` and `SWP_NOACTIVATE`,
without a per-tick raise, allowed all 46 original oracles to complete. Production
code, the 18-second bound, native-hit checks and foreground preservation were
unchanged. This verifies acknowledgement with a visible self-owned fixture; it
does not establish the application's normal-band visibility in a real session.
The distributed `CardFreeze110Probe.cs` exactly matches the passing fixture;
its portable runner was adapted afterwards and was not rerun. The summary
records executed and distributed runner hashes separately.

The history fixture also leaves its owner unshown. Its original 22 C# assertions
are unchanged; the runner requires every original PASS name in order, the final
completion line, exit zero and unchanged hashes. `run-history110.ps1` is a
portable wrapper for this same Python runner, with an optional fresh output path.

All runners check all 34 source hashes and both embedded tray icon hashes against
`audit/baseline.json` before compiling. They keep the original C# fixture
assertions unchanged, recheck inputs afterwards, and fail if the required count,
exit status, or native card stage sequence is incomplete.

The icon fixture reads `audit/tests-icon-inputs.json`, whose relative paths and
hashes come from the generated `shared/icons/asset-manifest.json`. It verifies
the black and white artwork and all ten ICO frames, including 18 and 22 pixels.
`ToolIcon.Draw` and `NativeTrayAssets.Original` must equal the independently
decoded PNG and DIB pixels across the entire frame; an extra gauge cannot pass
that comparison. It creates memory HICONs but does not display a tray icon or
establish Explorer taskbar appearance. The application ICO is decoded separately;
the normal build controls its embedding as the executable icon.

All generated executables, synthetic caches, PNGs, compiler logs, paths, window
geometry, and HWND diagnostics stay outside the repository:

```text
../private-validation/tests/news-unread/
../private-validation/tests/card-freeze110/
../private-validation/tests/icon119/
../private-validation/tests/history110/
../private-validation/tests/history-migration110/
```

Only an allowlisted summary of counts, exit codes, hashes, and scope flags is
written to `audit/tests-validation.json` and, for the icon suite, the separate
`audit/tests-icon119-validation.json` and `audit/tests-history110-validation.json`.
The final native card result is in `audit/tests-card110-validation.json`; migration
and layout use `audit/tests-history-migration110-validation.json` and
`audit/tests-history-layout110-validation.json`. Earlier failed native evidence
is preserved separately and is not counted as a pass.
Source changes require a reviewed,
updated baseline; the runners do not regenerate it or suppress hash mismatches.

Previous execution evidence is never overwritten. For another run, supply a
fresh suite folder under the same sibling `private-validation/tests` tree:

```powershell
node .\windows\tests\run_news_unread_regression118.js .\audit\baseline.json ..\private-validation\tests\news-unread-second
python .\windows\tests\run_card_freeze110.py .\audit\baseline.json ..\private-validation\tests\card-freeze110-second
python .\windows\tests\run_icon119.py .\audit\baseline.json .\audit\tests-icon-inputs.json ..\private-validation\tests\icon119-second
python .\windows\tests\run_history110.py .\audit\baseline.json ..\private-validation\tests\history110-second
python .\windows\tests\run_history_migration110.py .\audit\baseline.json ..\private-validation\tests\history-migration110-second
```

The same command may be retried after an explicitly recorded compilation
failure with no fixture run or cache. Its first compile evidence is preserved
under `compile-default-denied`. Directory aliases resolving inside the
repository are rejected.

The two message C# fixtures were copied byte for byte from the validated v0.11.8 fixtures.
Only their runners were adapted for this repository layout, private outputs,
and the `quota-tray-dark.ico` / `quota-tray-light.ico` resource names and aliases.
`Icon119Probe` is a separate new fixture; it does not replace either message test.

| Fixture source | Original and copied SHA-256 |
| --- | --- |
| `NewsUnreadRegression118.cs` | `cda82604705a0a38910c8c42bd1b3f78f4638e2e85de9d1904d07ec93895a7d9` |
| `CardFreeze118Probe.cs` | `9d894a24940decfdd5f2bf62216a9b93dc00433992dd8105cd62e29a5cf2dea5` |


