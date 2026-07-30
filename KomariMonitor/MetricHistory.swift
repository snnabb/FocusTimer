import Foundation

struct MetricQueryResponse: Decodable, Sendable {
    let series: [MetricSeries]
}

struct MetricSeries: Decodable, Sendable {
    let metricKey: String
    let entityID: String
    let points: [MetricPoint]

    enum CodingKeys: String, CodingKey {
        case points
        case metricKey = "metric_key"
        case entityID = "entity_id"
    }
}

struct MetricPoint: Decodable, Sendable {
    let time: String
    let value: Double?
}

extension MetricQueryResponse {
    func samples(for node: KomariNode) -> [HistoricalSample] {
        var values: [Date: [String: Double]] = [:]
        for item in series where item.entityID == node.uuid {
            for point in item.points {
                guard let date = point.time.komariDate, let value = point.value else { continue }
                values[date, default: [:]][item.metricKey] = value
            }
        }
        return values.keys.sorted().map { date in
            let item = values[date] ?? [:]
            return HistoricalSample(
                time: date,
                cpu: item["cpu.usage"],
                load: item["load.average"],
                memoryUsed: item["memory.used"].map(Int64.init),
                memoryTotal: node.memTotal,
                diskUsed: item["disk.used"].map(Int64.init),
                diskTotal: node.diskTotal,
                networkIn: item["net.in.rate"].map(Int64.init),
                networkOut: item["net.out.rate"].map(Int64.init),
                connections: item["connections.tcp"].map(Int.init),
                process: item["process.count"].map(Int.init)
            )
        }
    }
}
