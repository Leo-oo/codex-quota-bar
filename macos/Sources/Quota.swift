import Foundation
import Darwin

struct QuotaWindow: Codable {
    let remaining: Double
    let minutes: Int?
    let reset: Double?
    let fallback: String
    var label: String {
        guard let m = minutes, m > 0 else { return fallback }
        if m == 10080 { return "周" }
        if m % 1440 == 0 { return "\(m / 1440)天" }
        if m % 60 == 0 { return "\(m / 60)h" }
        return "\(m)m"
    }
    var percent: String { String(format: "%.1f", remaining).replacingOccurrences(of: ".0", with: "") + "%" }
}
struct QuotaSnapshot: Codable {
    let windows: [QuotaWindow]
    let fetched: Date
    static func parse(_ result: [String: Any]) -> QuotaSnapshot {
        let all = result["rateLimitsByLimitId"] as? [String: Any]
        var bucket = all?["codex"] as? [String: Any]
        if bucket == nil, let old = result["rateLimits"] as? [String: Any], old["limitId"] == nil || old["limitId"] as? String == "codex" { bucket = old }
        let windows = ["primary", "secondary"].enumerated().compactMap { i, key -> QuotaWindow? in
            guard let w = bucket?[key] as? [String: Any], let n = w["usedPercent"] as? NSNumber,
                  CFGetTypeID(n) != CFBooleanGetTypeID(), n.doubleValue.isFinite else { return nil }
            let m = w["windowDurationMins"] as? Int
            let r = w["resetsAt"] as? NSNumber
            let reset:Double? = r.flatMap{CFGetTypeID($0) != CFBooleanGetTypeID() && $0.doubleValue.isFinite && $0.doubleValue>=0 && $0.doubleValue<=253402300799 ? $0.doubleValue:nil}
            return QuotaWindow(remaining: min(100,max(0,100-n.doubleValue)), minutes: m, reset: reset, fallback: i == 0 ? "主" : "次")
        }.sorted { ($0.minutes ?? Int.max) < ($1.minutes ?? Int.max) }
        return QuotaSnapshot(windows: windows, fetched: Date())
    }
}
import CoreFoundation
struct QuotaError: Error, LocalizedError { let message: String;let discardSnapshot:Bool;init(message:String,discardSnapshot:Bool=false){self.message=message;self.discardSnapshot=discardSnapshot};var errorDescription: String? { message } }
enum RetryPolicy {static func delay(_ failures:Int)->Double {[15,30,60,120][max(0,min(3,failures-1))]}}
final class IdentityGate {
 private var previous:String?
 func changed(_ identity:String?)->Bool {defer{previous=identity};guard let identity else{return true};return previous != nil && previous != identity}
}
final class QuotaClient {
    let runtime: URL
    var onIdentityChanged:(()->Void)?
    private let identityGate=IdentityGate()
    private let lock = NSLock()
    private var active: Process?
    private var cancelled = false
    func cancel() {lock.lock();cancelled=true;let process=active;lock.unlock();if let process,process.isRunning {process.terminate()}}
    init(runtime: URL) { self.runtime = runtime }
    static func locate(environment: [String:String] = ProcessInfo.processInfo.environment,
                       home: String = FileManager.default.homeDirectoryForCurrentUser.path,
                       executable: (String)->Bool = { FileManager.default.isExecutableFile(atPath:$0) }) -> String? {
        let candidates = [environment["CODEX_USAGE_MINI_CODEX_PATH"],
            "/Applications/ChatGPT.app/Contents/Resources/codex-cli/bin/codex",
            "/Applications/Codex.app/Contents/Resources/codex",
            home + "/Applications/Codex.app/Contents/Resources/codex",
            home + "/Applications/ChatGPT.app/Contents/Resources/codex-cli/bin/codex",
            "/opt/homebrew/bin/codex", "/usr/local/bin/codex"].compactMap { $0 }
        let pathCandidates = (environment["PATH"] ?? "").split(separator: ":").filter { $0.hasPrefix("/") }.map { String($0)+"/codex" }
        return (candidates + pathCandidates).first { $0.hasPrefix("/") && executable($0) }
    }
    func read() throws -> QuotaSnapshot {
        guard let binary = Self.locate() else { throw QuotaError(message: "未找到 Codex CLI，请先安装并登录 Codex；详见安装说明") }
        try FileManager.default.createDirectory(at: runtime, withIntermediateDirectories: true)
        let p = Process(), input = Pipe(), output = Pipe()
        p.executableURL = URL(fileURLWithPath: binary)
        func quote(_ s: String) -> String { String(data: try! JSONSerialization.data(withJSONObject: s, options: .fragmentsAllowed), encoding: .utf8)! }
        p.arguments = ["-c", "sqlite_home=" + quote(runtime.path), "-c", "log_dir=" + quote(runtime.appendingPathComponent("logs").path), "app-server", "--stdio"]
        p.currentDirectoryURL = runtime
        p.standardInput = input; p.standardOutput = output; p.standardError = FileHandle.nullDevice
        lock.lock();if cancelled {lock.unlock();throw QuotaError(message:"额度服务已停止")};do {try p.run();active=p;lock.unlock()} catch {lock.unlock();throw error}
        defer { lock.lock();active=nil;lock.unlock(); try? input.fileHandleForWriting.close(); if p.isRunning { p.terminate() }; try? output.fileHandleForReading.close() }
        var buffer = Data()
        func send(_ object: [String: Any]) throws { var d = try JSONSerialization.data(withJSONObject: object); d.append(10); try input.fileHandleForWriting.write(contentsOf: d) }
        func request(_ id: Int, _ method: String, _ params: [String:Any]? = nil) throws -> [String:Any] {
            var object: [String:Any] = ["id":id,"method":method]; if let params { object["params"] = params }; try send(object)
            let deadline = Date().addingTimeInterval(15)
            while Date() < deadline {
                if let newline = buffer.firstIndex(of: 10) {
                    let line = buffer.prefix(upTo: newline); buffer.removeSubrange(...newline)
                    guard let message = (try? JSONSerialization.jsonObject(with: line)) as? [String:Any], message["id"] as? Int == id else { continue }
                    if let error=message["error"] as? [String:Any] {let message=(error["message"] as? String ?? "").lowercased();let auth=["401","auth","login"].contains{message.contains($0)};throw QuotaError(message:auth ? "登录状态不可用，请在Codex检查登录":"服务暂时无法返回额度",discardSnapshot:auth)}
                    return message["result"] as? [String:Any] ?? [:]
                }
                var fd = pollfd(fd: output.fileHandleForReading.fileDescriptor, events: Int16(POLLIN), revents: 0)
                if poll(&fd,1,100) > 0 {
                    var bytes = [UInt8](repeating:0,count:8192)
                    let count = Darwin.read(fd.fd,&bytes,bytes.count)
                    if count <= 0 { throw QuotaError(message: "Codex 额度服务已退出") }
                    buffer.append(contentsOf: bytes.prefix(count))
                    if buffer.count > 2_097_152 { throw QuotaError(message:"响应过大") }
                }
            }
            throw QuotaError(message:"额度读取超时，稍后自动重试")
        }
        _ = try request(1,"initialize",["clientInfo":["name":"codex_usage_mac","version":"0.11.6"],"capabilities":[:]])
        try send(["method":"initialized","params":[:]])
        let account = try request(2,"account/read",["refreshToken":false])
        guard let identity=account["account"] as? [String:Any] else {throw QuotaError(message:"请先在 Codex 登录账户",discardSnapshot:true)}
        if identityGate.changed(identity["id"] as? String ?? identity["email"] as? String) {onIdentityChanged?()}
        // No account identity, credentials or raw protocol responses are stored or logged.
        let result = QuotaSnapshot.parse(try request(3,"account/rateLimits/read"))
        guard !result.windows.isEmpty else { throw QuotaError(message:"服务未提供通用额度窗口",discardSnapshot:true) }
        return result
    }
}
