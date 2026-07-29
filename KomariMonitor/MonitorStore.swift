import Foundation
import SwiftUI

@MainActor
final class MonitorStore: ObservableObject {
    @Published var panel: PanelConfiguration?
    @Published var nodes: [String: KomariNode] = [:]
    @Published var statuses: [String: NodeStatus] = [:]
    @Published var version: KomariVersion?
    @Published var publicInfo: PublicInfo?
    @Published var lastUpdated: Date?
    @Published var isRefreshing = false
    @Published var isShowingSetup = false
    @Published var errorMessage: String?
    @Published var usingCachedData = false

    private var refreshTask: Task<Void, Never>?

    var sortedNodes: [KomariNode] {
        nodes.values.sorted {
            let left = $0.weight ?? 0
            let right = $1.weight ?? 0
            return left == right ? $0.name.localizedStandardCompare($1.name) == .orderedAscending : left > right
        }
    }

    var onlineCount: Int { statuses.values.filter { $0.online == true }.count }
    var offlineCount: Int { max(nodes.count - onlineCount, 0) }
    var totalDownload: Int64 { statuses.values.reduce(0) { $0 + max($1.netIn ?? 0, 0) } }
    var totalUpload: Int64 { statuses.values.reduce(0) { $0 + max($1.netOut ?? 0, 0) } }
    var averageCPU: Double {
        let values = statuses.values.compactMap(\.cpu)
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
        let fetchedVersion: KomariVersion = try await client.call("common:getVersion")
        let fetchedNodes: [String: KomariNode] = try await client.call("common:getNodes")
        let fetchedStatuses: [String: NodeStatus] = try await client.call("common:getNodesLatestStatus")
        let info: PublicInfo = try await client.call("common:getPublicInfo")
        guard !fetchedNodes.isEmpty else {
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
        version = fetchedVersion
        publicInfo = info
        nodes = fetchedNodes
        statuses = fetchedStatuses
        let updatedAt = Date()
        lastUpdated = updatedAt
        usingCachedData = false
        LocalStore.saveSnapshot(MonitorSnapshot(nodes: nodes, statuses: statuses, savedAt: updatedAt))
        isShowingSetup = false
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
            version = try await fetchedVersion
            publicInfo = try await fetchedInfo
            nodes = try await fetchedNodes
            statuses = try await fetchedStatuses
            let updatedAt = Date()
            lastUpdated = updatedAt
            usingCachedData = false
            errorMessage = nil
            LocalStore.saveSnapshot(MonitorSnapshot(nodes: nodes, statuses: statuses, savedAt: updatedAt))
        } catch {
            usingCachedData = !nodes.isEmpty
            errorMessage = error.localizedDescription
        }
    }

    func history(for uuid: String, hours: Int = 6) async throws -> [NodeStatus] {
        guard let panel, let apiKey = try KeychainStore.loadAPIKey() else { throw KomariAPIError.invalidAPIKey }
        let client = try KomariAPIClient(panel: panel, apiKey: apiKey)
        let result: RecordsResponse = try await client.call("common:getRecords", params: [
            "type": .string("load"),
            "uuid": .string(uuid),
            "hours": .int(hours),
            "load_type": .string("all"),
            "maxCount": .int(600)
        ])
        return result.records
    }

    func deleteConfiguration() {
        stopAutoRefresh()
        KeychainStore.deleteAPIKey()
        LocalStore.deletePanel()
        LocalStore.deleteSnapshot()
        panel = nil
        nodes = [:]
        statuses = [:]
        version = nil
        publicInfo = nil
        lastUpdated = nil
        errorMessage = nil
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
