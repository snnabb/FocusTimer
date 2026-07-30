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
        var values: [Int64: [String: (timestamp: Date, value: Double)]] = [:]
        for item in series where item.entityID == node.uuid {
            for point in item.points {
                guard let date = point.time.komariDate, let value = point.value else { continue }
                let second = Int64(date.timeIntervalSince1970.rounded(.down))
                if let existing = values[second]?[item.metricKey],
                   date < existing.timestamp || (date == existing.timestamp && value <= existing.value) {
                    continue
                }
                values[second, default: [:]][item.metricKey] = (date, value)
            }
        }
        return values.keys.sorted().map { second in
            let item = values[second]!
            return HistoricalSample(
                time: Date(timeIntervalSince1970: TimeInterval(second)),
                cpu: item["cpu.usage"]?.value,
                load: item["load.average"]?.value,
                memoryUsed: item["memory.used"]?.value.map(Int64.init),
                memoryTotal: node.memTotal,
                diskUsed: item["disk.used"]?.value.map(Int64.init),
                diskTotal: node.diskTotal,
                networkIn: item["net.in.rate"]?.value.map(Int64.init),
                networkOut: item["net.out.rate"]?.value.map(Int64.init),
                connections: item["connections.tcp"]?.value.map(Int.init),
                process: item["process.count"]?.value.map(Int.init)
            )
        }
    }
}
