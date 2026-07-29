import SwiftUI

struct NodesView: View {
    @EnvironmentObject private var store: MonitorStore
    @State private var search = ""
    @State private var filter: NodeFilter = .all

    enum NodeFilter: String, CaseIterable, Identifiable {
        case all = "全部"
        case online = "在线"
        case offline = "离线"
        var id: String { rawValue }
    }

    private var filtered: [KomariNode] {
        store.sortedNodes.filter { node in
            let matchesSearch = search.isEmpty || node.name.localizedCaseInsensitiveContains(search) || (node.group?.localizedCaseInsensitiveContains(search) == true)
            let isOnline = store.statuses[node.uuid]?.online == true
            let matchesFilter = filter == .all || (filter == .online && isOnline) || (filter == .offline && !isOnline)
            return matchesSearch && matchesFilter
        }
    }

    var body: some View {
        ScrollView {
            LazyVStack(spacing: 14) {
                Picker("状态", selection: $filter) {
                    ForEach(NodeFilter.allCases) { Text($0.rawValue).tag($0) }
                }
                .pickerStyle(.segmented)
                ForEach(filtered) { node in
                    NavigationLink(value: node) { NodeCard(node: node, status: store.statuses[node.uuid]) }
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
}
