import Foundation

struct RPCEnvelope<Result: Decodable & Sendable>: Decodable, Sendable {
    let jsonrpc: String?
    let id: Int?
    let result: Result?
    let error: RPCErrorPayload?
}

struct RPCErrorPayload: Decodable, Sendable {
    let code: Int
    let message: String
}

enum RPCValue: Encodable, Sendable {
    case string(String)
    case int(Int)
    case bool(Bool)
    case strings([String])

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .string(let value): try container.encode(value)
        case .int(let value): try container.encode(value)
        case .bool(let value): try container.encode(value)
        case .strings(let value): try container.encode(value)
        }
    }
}

struct RPCRequest: Encodable, Sendable {
    let jsonrpc = "2.0"
    let method: String
    let params: [String: RPCValue]
    let id: Int
}

enum KomariAPIError: LocalizedError {
    case invalidURL
    case invalidAPIKey
    case http(Int)
    case cloudflareChallenge
    case invalidResponse
    case rpc(Int, String)

    var errorDescription: String? {
        switch self {
        case .invalidURL: "请输入有效的 HTTPS 面板地址"
        case .invalidAPIKey: "请输入 API Key"
        case .http(let code): "服务器返回 HTTP \(code)"
        case .cloudflareChallenge: "Cloudflare 返回了验证页面，请为 /api/rpc2 关闭交互式质询"
        case .invalidResponse: "Komari 返回了无法识别的数据"
        case .rpc(_, let message): message
        }
    }
}

actor KomariAPIClient {
    private let panel: PanelConfiguration
    private let apiKey: String
    private let session: URLSession
    private var requestID = 0

    init(panel: PanelConfiguration, apiKey: String) throws {
        guard panel.normalizedURL != nil else { throw KomariAPIError.invalidURL }
        guard !apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw KomariAPIError.invalidAPIKey }
        self.panel = panel
        self.apiKey = apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 20
        configuration.timeoutIntervalForResource = 30
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        self.session = URLSession(configuration: configuration)
    }

    func ping() async throws {
        guard let url = panel.normalizedURL?.appendingPathComponent("ping") else { throw KomariAPIError.invalidURL }
        var request = URLRequest(url: url)
        request.timeoutInterval = 12
        let (data, response) = try await session.data(for: request)
        try validate(data: data, response: response)
        guard String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines) == "pong" else {
            throw KomariAPIError.invalidResponse
        }
    }

    func call<Result: Decodable & Sendable>(_ method: String, params: [String: RPCValue] = [:], as type: Result.Type = Result.self) async throws -> Result {
        guard let url = panel.normalizedURL?.appendingPathComponent("api/rpc2") else { throw KomariAPIError.invalidURL }
        requestID += 1
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.setValue("KomariMonitor/1.1 iOS", forHTTPHeaderField: "User-Agent")
        request.httpBody = try JSONEncoder().encode(RPCRequest(method: method, params: params, id: requestID))

        let (data, response) = try await session.data(for: request)
        try validate(data: data, response: response)
        let envelope = try JSONDecoder().decode(RPCEnvelope<Result>.self, from: data)
        if let error = envelope.error { throw KomariAPIError.rpc(error.code, error.message) }
        guard let result = envelope.result else { throw KomariAPIError.invalidResponse }
        return result
    }

    private func validate(data: Data, response: URLResponse) throws {
        guard let http = response as? HTTPURLResponse else { throw KomariAPIError.invalidResponse }
        guard (200..<300).contains(http.statusCode) else { throw KomariAPIError.http(http.statusCode) }
        let contentType = (http.value(forHTTPHeaderField: "Content-Type") ?? "").lowercased()
        let prefix = String(decoding: data.prefix(512), as: UTF8.self).lowercased()
        if contentType.contains("text/html") || prefix.contains("cf-chl-") || prefix.contains("just a moment") {
            throw KomariAPIError.cloudflareChallenge
        }
    }
}
