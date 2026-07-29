import SwiftUI

struct NodesView: View {
    @EnvironmentObject private var store: MonitorStore
    @State private var search = ""
    @State private var filter: NodeFilter = .all
    @State private var sort: NodeSort = .komari

    enum NodeFilter: String, CaseIterable, Identifiable {
        case all = "全部"
        case online = "在线"
        case abnormal = "异常"
        var id: String { rawValue }
    }

    enum NodeSort: String, CaseIterable, Identifiable {
        case komari = "Komari 顺序"
        case name = "名称"
        case cpu = "CPU"
        case memory = "内存"
        case traffic = "流量"
        case latency = "延迟"
        var id: String { rawValue }
    }

    private var filtered: [KomariNode] {
        let matches = store.sortedNodes.filter { node in
            let matchesSearch = search.isEmpty || node.name.localizedCaseInsensitiveContains(search) || (node.group?.localizedCaseInsensitiveContains(search) == true)
            let state = store.statuses[node.uuid]?.healthState ?? .unknown
            let matchesFilter = filter == .all || (filter == .online && state == .online) || (filter == .abnormal && state != .online)
            return matchesSearch && matchesFilter
        }
        return sortNodes(matches)
    }

    var body: some View {
        ScrollView {
            LazyVStack(spacing: 14) {
                HStack {
                    Picker("状态", selection: $filter) {
                        ForEach(NodeFilter.allCases) { Text($0.rawValue).tag($0) }
                    }
                    .pickerStyle(.segmented)
                    Menu {
                        Picker("排序", selection: $sort) {
                            ForEach(NodeSort.allCases) { Text($0.rawValue).tag($0) }
                        }
                    } label: {
                        Image(systemName: "arrow.up.arrow.down.circle")
                            .font(.title2)
                    }
                }
                ForEach(filtered) { node in
                    NavigationLink(value: node) {
                        NodeCard(node: node, status: store.statuses[node.uuid], ping: store.pingSummaries[node.uuid])
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding()
        }
        .navigationTitle("节点")
        .searchable(text: $search, prompt: "搜索名称或分组")
        .navigationDestination(for: KomariNode.self) { NodeDetailView(node: $0) }
        .refreshable { await store.refresh() }
        .overlay {
            if filtered.isEmpty { ContentUnavailableView("没有匹配的节点", systemImage: "server.rack") }
        }
    }

    private func sortNodes(_ nodes: [KomariNode]) -> [KomariNode] {
        switch sort {
        case .komari: nodes
        case .name: nodes.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
        case .cpu: nodes.sorted { (store.statuses[$0.uuid]?.cpu ?? -1) > (store.statuses[$1.uuid]?.cpu ?? -1) }
        case .memory: nodes.sorted { memoryPercent($0) > memoryPercent($1) }
        case .traffic: nodes.sorted { ($0.trafficUsed(status: store.statuses[$0.uuid]) ?? -1) > ($1.trafficUsed(status: store.statuses[$1.uuid]) ?? -1) }
        case .latency: nodes.sorted { (store.pingSummaries[$0.uuid]?.latency ?? .greatestFiniteMagnitude) < (store.pingSummaries[$1.uuid]?.latency ?? .greatestFiniteMagnitude) }
        }
    }

    private func memoryPercent(_ node: KomariNode) -> Double {
        guard let used = store.statuses[node.uuid]?.ram, let total = node.memTotal, total > 0 else { return -1 }
        return Double(used) / Double(total)
    }
}
