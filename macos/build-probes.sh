#!/bin/zsh
set -eu
cd "$(dirname "$0")"
mkdir -p evidence
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Quota.swift Tests/probe.swift -o evidence/probe
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Quota.swift Tests/QuotaTests.swift -o evidence/quota-tests
./evidence/quota-tests
