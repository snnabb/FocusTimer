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

struct RecordsResponse: Codable, Sendable {
    let count: Int?
    let records: [NodeStatus]
    let from: String?
    let to: String?
}

struct MonitorSnapshot: Codable, Sendable {
    let nodes: [String: KomariNode]
    let statuses: [String: NodeStatus]
    let savedAt: Date
}
