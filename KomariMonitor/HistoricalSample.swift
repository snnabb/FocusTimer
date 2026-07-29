import Foundation

struct HistoricalSample: Identifiable, Hashable, Sendable {
    let time: Date
    let cpu: Double?
    let load: Double?
    let memoryUsed: Int64?
    let memoryTotal: Int64?
    let diskUsed: Int64?
    let diskTotal: Int64?
    let networkIn: Int64?
    let networkOut: Int64?
    let connections: Int?
    let process: Int?

    var id: Date { time }

    init?(status: NodeStatus, node: KomariNode) {
        guard let date = status.time.komariDate else { return nil }
        time = date
        cpu = status.cpu
        load = status.load
        memoryUsed = status.ram
        memoryTotal = status.ramTotal ?? node.memTotal
        diskUsed = status.disk
        diskTotal = status.diskTotal ?? node.diskTotal
        networkIn = status.netIn
        networkOut = status.netOut
        connections = status.connections
        process = status.process
    }
}

struct LegacyRecentResponse: Decodable, Sendable {
    let records: [LegacyRecord]

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let records = try? container.decode([LegacyRecord].self) {
            self.records = records
            return
        }
        let wrapped = try container.decode(Wrapped.self)
        self.records = wrapped.records ?? wrapped.data ?? []
    }

    private struct Wrapped: Decodable {
        let records: [LegacyRecord]?
        let data: [LegacyRecord]?
    }
}

struct LegacyRecord: Decodable, Sendable {
    let time: String?
    let updatedAt: String?
    let cpu: LegacyUsage?
    let ram: LegacyUsedTotal?
    let disk: LegacyUsedTotal?
    let load: LegacyLoad?
    let network: LegacyNetwork?
    let connections: LegacyConnections?
    let process: Int?

    enum CodingKeys: String, CodingKey {
        case time, cpu, ram, disk, load, network, connections, process
        case updatedAt = "updated_at"
    }

    func sample(node: KomariNode) -> HistoricalSample? {
        let timestamp = time ?? updatedAt
        guard let timestamp, let date = timestamp.komariDate else { return nil }
        return HistoricalSample(
            time: date,
            cpu: cpu?.usage,
            load: load?.load1,
            memoryUsed: ram?.used,
            memoryTotal: ram?.total ?? node.memTotal,
            diskUsed: disk?.used,
            diskTotal: disk?.total ?? node.diskTotal,
            networkIn: network?.down,
            networkOut: network?.up,
            connections: connections?.tcp,
            process: process
        )
    }
}

private extension HistoricalSample {
    init(time: Date, cpu: Double?, load: Double?, memoryUsed: Int64?, memoryTotal: Int64?, diskUsed: Int64?, diskTotal: Int64?, networkIn: Int64?, networkOut: Int64?, connections: Int?, process: Int?) {
        self.time = time
        self.cpu = cpu
        self.load = load
        self.memoryUsed = memoryUsed
        self.memoryTotal = memoryTotal
        self.diskUsed = diskUsed
        self.diskTotal = diskTotal
        self.networkIn = networkIn
        self.networkOut = networkOut
        self.connections = connections
        self.process = process
    }
}

struct LegacyUsage: Decodable, Sendable { let usage: Double? }
struct LegacyUsedTotal: Decodable, Sendable { let used: Int64?; let total: Int64? }
struct LegacyLoad: Decodable, Sendable {
    let load1: Double?
    let load5: Double?
    let load15: Double?
}
struct LegacyNetwork: Decodable, Sendable {
    let up: Int64?
    let down: Int64?
    let totalUp: Int64?
    let totalDown: Int64?
}
struct LegacyConnections: Decodable, Sendable { let tcp: Int?; let udp: Int? }
