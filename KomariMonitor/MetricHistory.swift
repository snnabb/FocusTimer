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
        var samples: [HistoricalSample] = []
        samples.reserveCapacity(values.count)
        for second in values.keys.sorted() {
            guard let item = values[second] else { continue }
            let memoryUsed = item["memory.used"].map { Int64($0.value) }
            let diskUsed = item["disk.used"].map { Int64($0.value) }
            let networkIn = item["net.in.rate"].map { Int64($0.value) }
            let networkOut = item["net.out.rate"].map { Int64($0.value) }
            let connections = item["connections.tcp"].map { Int($0.value) }
            let process = item["process.count"].map { Int($0.value) }
            samples.append(HistoricalSample(
                time: Date(timeIntervalSince1970: TimeInterval(second)),
                cpu: item["cpu.usage"]?.value,
                load: item["load.average"]?.value,
                memoryUsed: memoryUsed,
                memoryTotal: node.memTotal,
                diskUsed: diskUsed,
                diskTotal: node.diskTotal,
                networkIn: networkIn,
                networkOut: networkOut,
                connections: connections,
                process: process
            ))
        }
        return samples
    }
}
