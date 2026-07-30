import Foundation

struct PanelConfiguration: Codable, Equatable, Sendable {
    var name: String
    var baseURL: String

    var normalizedURL: URL? {
        var value = baseURL.trimmingCharacters(in: .whitespacesAndNewlines)
        while value.hasSuffix("/") { value.removeLast() }
        guard var components = URLComponents(string: value),
              components.scheme?.lowercased() == "https",
              components.host != nil else { return nil }
        components.query = nil
        components.fragment = nil
        return components.url
    }
}

struct KomariVersion: Codable, Sendable {
    let version: String
    let hash: String
}

struct PublicInfo: Codable, Sendable {
    let sitename: String?
    let description: String?
    let privateSite: Bool?
    let recordEnabled: Bool?
    let recordPreserveTime: Int?
    let pingRecordPreserveTime: Int?

    enum CodingKeys: String, CodingKey {
        case sitename, description
        case privateSite = "private_site"
        case recordEnabled = "record_enabled"
        case recordPreserveTime = "record_preserve_time"
        case pingRecordPreserveTime = "ping_record_preserve_time"
    }
}

struct KomariNode: Codable, Identifiable, Hashable, Sendable {
    let uuid: String
    let name: String
    let cpuName: String?
    let virtualization: String?
    let arch: String?
    let cpuCores: Int?
    let cpuPhysicalCores: Int?
    let os: String?
    let kernelVersion: String?
    let gpuName: String?
    let ipv4: String?
    let ipv6: String?
    let region: String?
    let remark: String?
    let publicRemark: String?
    let memTotal: Int64?
    let swapTotal: Int64?
    let diskTotal: Int64?
    let version: String?
    let weight: Int?
    let price: Double?
    let billingCycle: Int?
    let autoRenewal: Bool?
    let currency: String?
    let expiredAt: String?
    let group: String?
    let tags: String?
    let hidden: Bool?
    let trafficLimit: Int64?
    let trafficLimitType: String?

    var id: String { uuid }

    enum CodingKeys: String, CodingKey {
        case uuid, name, virtualization, arch, os, region, remark, weight, price, currency, group, tags, hidden
        case cpuName = "cpu_name"
        case cpuCores = "cpu_cores"
        case cpuPhysicalCores = "cpu_physical_cores"
        case kernelVersion = "kernel_version"
        case gpuName = "gpu_name"
        case ipv4, ipv6
        case publicRemark = "public_remark"
        case memTotal = "mem_total"
        case swapTotal = "swap_total"
        case diskTotal = "disk_total"
        case version
        case billingCycle = "billing_cycle"
        case autoRenewal = "auto_renewal"
        case expiredAt = "expired_at"
        case trafficLimit = "traffic_limit"
        case trafficLimitType = "traffic_limit_type"
    }
}

struct NodeStatus: Codable, Identifiable, Hashable, Sendable {
    let client: String
    let time: String
    let cpu: Double?
    let gpu: Double?
    let ram: Int64?
    let ramTotal: Int64?
    let swap: Int64?
    let swapTotal: Int64?
    let load: Double?
    let load5: Double?
    let load15: Double?
    let temp: Double?
    let disk: Int64?
    let diskTotal: Int64?
    let netIn: Int64?
    let netOut: Int64?
    let netTotalUp: Int64?
    let netTotalDown: Int64?
    let process: Int?
    let connections: Int?
    let connectionsUDP: Int?
    let online: Bool?

    var id: String { "\(client)-\(time)" }

    enum CodingKeys: String, CodingKey {
        case client, time, cpu, gpu, ram, swap, load, load5, load15, temp, disk, process, connections, online
        case ramTotal = "ram_total"
        case swapTotal = "swap_total"
        case diskTotal = "disk_total"
        case netIn = "net_in"
        case netOut = "net_out"
        case netTotalUp = "net_total_up"
        case netTotalDown = "net_total_down"
        case connectionsUDP = "connections_udp"
    }
}

struct RecentStatusResponse: Codable, Sendable {
    let count: Int?
    let records: [NodeStatus]
}

struct RecordsResponse: Decodable, Sendable {
    let count: Int?
    let records: [NodeStatus]
    let groupedRecords: [String: [NodeStatus]]
    let from: String?
    let to: String?

    enum CodingKeys: String, CodingKey { case count, records, from, to }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        count = try container.decodeIfPresent(Int.self, forKey: .count)
        from = try container.decodeIfPresent(String.self, forKey: .from)
        to = try container.decodeIfPresent(String.self, forKey: .to)
        if let list = try? container.decode([NodeStatus].self, forKey: .records) {
            records = list
            groupedRecords = [:]
        } else {
            let groups = try container.decode([String: [NodeStatus]].self, forKey: .records)
            records = groups.values.flatMap { $0 }
            groupedRecords = groups
        }
    }

    func records(for uuid: String) -> [NodeStatus] {
        groupedRecords[uuid] ?? records.filter { $0.client == uuid || $0.client.isEmpty }
    }
}

struct PingTask: Codable, Identifiable, Hashable, Sendable {
    let id: Int
    let name: String
    let clients: [String]?
    let defaultOn: Bool?
    let type: String?
    let interval: Int?

    enum CodingKeys: String, CodingKey {
        case id, name, clients, type, interval
        case defaultOn = "default_on"
    }
}

struct PingRecord: Codable, Identifiable, Hashable, Sendable {
    let taskID: Int?
    let time: String
    let value: Double
    let client: String?

    var id: String { "\(client ?? "")-\(taskID ?? -1)-\(time)" }

    enum CodingKeys: String, CodingKey {
        case time, value, client
        case taskID = "task_id"
    }
}

struct PingClientStats: Codable, Sendable {
    let client: String
    let loss: Double?
    let min: Double?
    let max: Double?
}

struct PingTaskStats: Codable, Identifiable, Sendable {
    let id: Int
    let name: String
    let type: String?
    let interval: Int?
    let defaultOn: Bool?
    let clients: [String]?
    let loss: Double?
    let min: Double?
    let max: Double?
    let avg: Double?
    let total: Int?

    enum CodingKeys: String, CodingKey {
        case id, name, type, interval, clients, loss, min, max, avg, total
        case defaultOn = "default_on"
    }
}

struct PingRecordsResponse: Codable, Sendable {
    let count: Int?
    let basicInfo: [PingClientStats]?
    let records: [PingRecord]
    let tasks: [PingTaskStats]?
    let from: String?
    let to: String?

    enum CodingKeys: String, CodingKey {
        case count, records, tasks, from, to
        case basicInfo = "basic_info"
    }
}

struct MonitorSnapshot: Codable, Sendable {
    let nodes: [String: KomariNode]
    let statuses: [String: NodeStatus]
    let savedAt: Date
}
