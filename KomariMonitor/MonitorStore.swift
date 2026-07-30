import Foundation
import SwiftUI

@MainActor
final class MonitorStore: ObservableObject {
    @Published var panel: PanelConfiguration?
    @Published var nodes: [String: KomariNode] = [:]
    @Published var statuses: [String: NodeStatus] = [:]
    @Published var pingSummaries: [String: NodePingSummary] = [:]
    @Published var pingTasks: [PingTask] = []
    @Published var version: KomariVersion?
    @Published var publicInfo: PublicInfo?
    @Published var lastUpdated: Date?
    @Published var isRefreshing = false
    @Published var isShowingSetup = false
    @Published var errorMessage: String?
    @Published var usingCachedData = false
    @Published var isConnected = false

    private var refreshTask: Task<Void, Never>?

    var sortedNodes: [KomariNode] {
        nodes.values.sorted {
            let left = $0.weight ?? Int.max
            let right = $1.weight ?? Int.max
            return left == right ? $0.name.localizedStandardCompare($1.name) == .orderedAscending : left < right
        }
    }

    var onlineCount: Int { statuses.values.filter { $0.healthState == .online }.count }
    var staleCount: Int { statuses.values.filter { $0.healthState == .stale }.count }
    var offlineCount: Int { sortedNodes.filter { statuses[$0.uuid]?.healthState == .offline }.count }
    var unknownCount: Int { sortedNodes.filter { statuses[$0.uuid] == nil || statuses[$0.uuid]?.healthState == .unknown }.count }
    var totalDownload: Int64 { statuses.values.filter { $0.healthState == .online }.reduce(0) { $0 + max($1.netIn ?? 0, 0) } }
    var totalUpload: Int64 { statuses.values.filter { $0.healthState == .online }.reduce(0) { $0 + max($1.netOut ?? 0, 0) } }
    var averageCPU: Double { average(\.cpu) }
    var averageLoad: Double { average(\.load) }

    private func average(_ keyPath: KeyPath<NodeStatus, Double?>) -> Double {
        let values = statuses.values.filter { $0.healthState == .online }.compactMap { $0[keyPath: keyPath] }
        return values.isEmpty ? 0 : values.reduce(0, +) / Double(values.count)
    }

    func bootstrap() async {
        panel = LocalStore.loadPanel()
        if let snapshot = LocalStore.loadSnapshot() {
            nodes = snapshot.nodes
            statuses = snapshot.statuses
            lastUpdated = snapshot.savedAt
            usingCachedData = true
        }
        guard panel != nil else {
            isShowingSetup = true
            return
        }
        await refresh()
        startAutoRefresh()
    }

    func configure(panel newPanel: PanelConfiguration, apiKey: String) async throws {
        let client = try KomariAPIClient(panel: newPanel, apiKey: apiKey)
        try await client.ping()
        async let fetchedVersion: KomariVersion = client.call("common:getVersion")
        async let fetchedNodes: [String: KomariNode] = client.call("common:getNodes")
        async let fetchedStatuses: [String: NodeStatus] = client.call("common:getNodesLatestStatus")
        async let fetchedInfo: PublicInfo = client.call("common:getPublicInfo")
        async let fetchedTasks: [PingTask] = client.call("public:getPublicPingTasks")
        let result = try await (fetchedVersion, fetchedNodes, fetchedStatuses, fetchedInfo, fetchedTasks)
        guard !result.1.isEmpty else {
            throw KomariAPIError.rpc(-1, "API Key 未返回任何节点，请确认它有效且能够读取私有节点")
        }

        try LocalStore.savePanel(newPanel)
        do {
            try KeychainStore.saveAPIKey(apiKey)
        } catch {
            LocalStore.deletePanel()
            throw error
        }
        panel = newPanel
        version = result.0
        nodes = result.1
        statuses = result.2
        publicInfo = result.3
        pingTasks = result.4
        let updatedAt = Date()
        lastUpdated = updatedAt
        usingCachedData = false
        isConnected = true
        LocalStore.saveSnapshot(MonitorSnapshot(nodes: nodes, statuses: statuses, savedAt: updatedAt))
        isShowingSetup = false
        await refreshPingSummaries()
        startAutoRefresh()
    }

    func refresh() async {
        guard !isRefreshing, let panel else { return }
        isRefreshing = true
        defer { isRefreshing = false }
        do {
            guard let apiKey = try KeychainStore.loadAPIKey(), !apiKey.isEmpty else {
                isShowingSetup = true
                throw KomariAPIError.invalidAPIKey
            }
            let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
            async let fetchedVersion: KomariVersion = client.call("common:getVersion")
            async let fetchedInfo: PublicInfo = client.call("common:getPublicInfo")
            async let fetchedNodes: [String: KomariNode] = client.call("common:getNodes")
            async let fetchedStatuses: [String: NodeStatus] = client.call("common:getNodesLatestStatus")
            async let fetchedTasks: [PingTask] = client.call("public:getPublicPingTasks")
            let nextVersion = try await fetchedVersion
            let nextInfo = try await fetchedInfo
            let nextNodes = try await fetchedNodes
            let nextStatuses = try await fetchedStatuses
            let nextTasks = try await fetchedTasks
            version = nextVersion
            publicInfo = nextInfo
            nodes = nextNodes
            withAnimation(.easeInOut(duration: 0.3)) { statuses = nextStatuses }
            pingTasks = nextTasks
            let updatedAt = Date()
            lastUpdated = updatedAt
            usingCachedData = false
            isConnected = true
            errorMessage = nil
            LocalStore.saveSnapshot(MonitorSnapshot(nodes: nodes, statuses: statuses, savedAt: updatedAt))
            await refreshPingSummaries()
        } catch {
            usingCachedData = !nodes.isEmpty
            isConnected = false
            errorMessage = error.localizedDescription
        }
    }

    func refreshNode(_ uuid: String) async throws -> NodeStatus? {
        guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
        let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
        let result: [String: NodeStatus] = try await client.call("common:getNodesLatestStatus", params: ["uuid": .string(uuid)])
        guard let status = result[uuid] else { return nil }
        withAnimation(.easeInOut(duration: 0.3)) { statuses[uuid] = status }
        return status
    }

    func recentHistory(for uuid: String) async throws -> [NodeStatus] {
        guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
        let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
        let result: RecentStatusResponse = try await client.call("common:getNodeRecentStatus", params: ["uuid": .string(uuid)])
        return result.records
    }

    func history(for uuid: String, hours: Int = 6) async throws -> [NodeStatus] {
        guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
        let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
        let result: RecordsResponse = try await client.call("common:getRecords", params: [
            "type": .string("load"),
            "uuid": .string(uuid),
            "hours": .int(hours),
            "load_type": .string("all"),
            "maxCount": .int(800)
        ])
        return result.records(for: uuid)
    }

    func compatibleHistory(for node: KomariNode, hours: Int) async throws -> [HistoricalSample] {
        var failures: [String] = []

        if hours == 0 {
            do {
                let records = try await recentHistory(for: node.uuid)
                let samples = records.compactMap { HistoricalSample(status: $0, node: node) }
                if !samples.isEmpty { return samples.sorted { $0.time < $1.time } }
                failures.append("近期 RPC 返回 0 条记录")
            } catch {
                failures.append("近期 RPC：\(error.localizedDescription)")
            }

            do {
                guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
                let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
                let response: LegacyRecentResponse = try await client.call("public:getClientRecentRecords", params: ["uuid": .string(node.uuid)])
                let samples = response.records.compactMap { $0.sample(node: node) }
                if !samples.isEmpty { return samples.sorted { $0.time < $1.time } }
                failures.append("兼容近期 RPC 返回 0 条记录")
            } catch {
                failures.append("兼容近期 RPC：\(error.localizedDescription)")
            }
        }

        do {
            let records = try await history(for: node.uuid, hours: max(hours, 1))
            let samples = records.compactMap { HistoricalSample(status: $0, node: node) }
            if !samples.isEmpty { return samples.sorted { $0.time < $1.time } }
            failures.append("历史 RPC 返回 0 条记录")
        } catch {
            failures.append("历史 RPC：\(error.localizedDescription)")
        }

        do {
            guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
            let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
            let response: MetricQueryResponse = try await client.call("public:queryMetrics", params: [
                "metric_keys": .strings(["cpu.usage", "load.average", "memory.used", "disk.used", "net.in.rate", "net.out.rate", "connections.tcp", "process.count"]),
                "entity_id": .string(node.uuid),
                "hours": .int(max(hours, 1)),
                "max_points": .int(400)
            ])
            let samples = response.samples(for: node)
            if !samples.isEmpty { return samples }
            failures.append("指标历史 RPC 返回 0 条记录")
        } catch {
            failures.append("指标历史 RPC：\(error.localizedDescription)")
        }

        do {
            guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
            let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
            let response: LegacyRecentResponse = try await client.call("public:getRecordsByUUID", params: [
                "uuid": .string(node.uuid),
                "load_type": .string("all"),
                "hours": .string(String(max(hours, 1)))
            ])
            let samples = response.records.compactMap { $0.sample(node: node) }
            if !samples.isEmpty { return samples.sorted { $0.time < $1.time } }
            failures.append("兼容历史 RPC 返回 0 条记录")
        } catch {
            failures.append("兼容历史 RPC：\(error.localizedDescription)")
        }

        throw KomariAPIError.rpc(-1, failures.joined(separator: "；"))
    }

    func pingDetails(for uuid: String, hours: Int = 6) async throws -> PingRecordsResponse {
        guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
        let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
        return try await client.call("public:getPingRecords", params: [
            "uuid": .string(uuid),
            "hours": .string(String(hours))
        ])
    }

    func refreshPingSummaries() async {
        guard let panel, let apiKey = try? KeychainStore.loadAPIKey() else { return }
        guard let client = try? KomariAPIClient(panel: panel, apiKey: apiKey) else { return }
        let nodeIDs = Array(nodes.keys)
        var summaries: [String: NodePingSummary] = [:]
        await withTaskGroup(of: (String, NodePingSummary?).self) { group in
            for uuid in nodeIDs {
                group.addTask {
                    do {
                        let response: PingRecordsResponse = try await client.call("public:getPingRecords", params: [
                            "uuid": .string(uuid), "hours": .string("1")
                        ])
                        let validRecords = response.records.filter { $0.value >= 0 }
                        let latestByTask = Dictionary(grouping: validRecords, by: { $0.taskID ?? -1 }).compactMap { _, records in
                            records.max(by: { ($0.time.komariDate ?? .distantPast) < ($1.time.komariDate ?? .distantPast) })
                        }
                        guard !latestByTask.isEmpty else { return (uuid, nil) }
                        let average = latestByTask.map(\.value).reduce(0, +) / Double(latestByTask.count)
                        let latestTime = latestByTask.compactMap { $0.time.komariDate }.max()
                        let loss = response.basicInfo?.first(where: { $0.client == uuid })?.loss
                        return (uuid, NodePingSummary(latency: average, loss: loss, taskName: nil, updatedAt: latestTime))
                    } catch {
                        return (uuid, nil)
                    }
                }
            }
            for await (uuid, summary) in group {
                if let summary { summaries[uuid] = summary }
            }
        }
        pingSummaries = summaries
    }

    func deleteConfiguration() {
        stopAutoRefresh()
        KeychainStore.deleteAPIKey()
        LocalStore.deletePanel()
        LocalStore.deleteSnapshot()
        panel = nil
        nodes = [:]
        statuses = [:]
        pingSummaries = [:]
        pingTasks = []
        version = nil
        publicInfo = nil
        lastUpdated = nil
        errorMessage = nil
        isConnected = false
        isShowingSetup = true
    }

    func startAutoRefresh() {
        refreshTask?.cancel()
        refreshTask = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(5))
                guard !Task.isCancelled else { return }
                await self?.refresh()
            }
        }
    }

    func stopAutoRefresh() {
        refreshTask?.cancel()
        refreshTask = nil
    }
}
