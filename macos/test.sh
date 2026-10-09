#!/bin/zsh
set -eu
cd "$(dirname "$0")"
mkdir -p evidence
./build-probes.sh > evidence/parser-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Quota.swift Sources/Geometry.swift Sources/News.swift Sources/NewsHistory.swift Tests/ModelTests.swift -o evidence/model-tests
./evidence/model-tests "$PWD/evidence" > evidence/model-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Quota.swift Sources/HostWindowPolicy.swift Sources/FloatingAttachment.swift Sources/Views.swift Tests/RenderFixtures.swift -o evidence/render-fixtures
./evidence/render-fixtures "$PWD/evidence/renders" > evidence/render-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Quota.swift Sources/Geometry.swift Sources/News.swift Sources/NewsHistory.swift Sources/NewsLayout.swift Tests/ExtendedTests.swift -o evidence/extended-tests
./evidence/extended-tests "$PWD" > evidence/extended-tests.txt
printf '%s\n' 'Parser, geometry/news, and offscreen rendering checks passed. Real host integration is not covered.'

swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Tests/HostResolverTests.swift -o evidence/resolver-tests
./evidence/resolver-tests > evidence/resolver-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/HostTraversal.swift Tests/HostTraversalTests.swift -o evidence/traversal-tests
./evidence/traversal-tests > evidence/traversal-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Sources/HostWindowPolicy.swift Sources/HostSession.swift Tests/HostSessionTests.swift -o evidence/session-tests
./evidence/session-tests > evidence/session-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/HostWindowPolicy.swift Tests/HostWindowPolicyTests.swift -o evidence/window-policy-tests
./evidence/window-policy-tests > evidence/window-policy-tests.txt

swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Tests/HostStructureTests.swift -o evidence/structure-tests
./evidence/structure-tests > evidence/structure-tests.txt

swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Sources/HostTraversal.swift Sources/HostCollectionPolicy.swift Tests/HostCollectionTests.swift -o evidence/collection-tests
./evidence/collection-tests > evidence/collection-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Sources/HostWindowPolicy.swift Sources/HostSession.swift Tests/WindowFollowTests.swift -o evidence/window-follow-tests
./evidence/window-follow-tests "$PWD/evidence" > evidence/window-follow-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Sources/HostWindowPolicy.swift Sources/HostSession.swift Tests/OverlayRecoveryTests.swift -o evidence/recovery-tests
./evidence/recovery-tests > evidence/recovery-tests.txt
swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Geometry.swift Sources/HostModel.swift Sources/HostStructure.swift Sources/HostWindowPolicy.swift Sources/HostSession.swift Tests/FocusContinuityTests.swift -o evidence/focus-tests
./evidence/focus-tests > evidence/focus-tests.txt

swiftc -module-cache-path /tmp/codex-usage-swift-cache Sources/Quota.swift Sources/News.swift Sources/NewsHistory.swift Tests/NewsHistoryTests.swift -o evidence/history-tests
./evidence/history-tests > evidence/history-tests.txt
