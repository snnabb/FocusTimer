import SwiftUI

struct DashboardView: View {
    @EnvironmentObject private var store: MonitorStore

    private let columns = [GridItem(.flexible()), GridItem(.flexible())]

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 20) {
                if store.usingCachedData {
                    Label("当前显示缓存数据", systemImage: "clock.arrow.circlepath")
                        .font(.caption.weight(.medium))
                        .foregroundStyle(.orange)
                }

                LazyVGrid(columns: columns, spacing: 12) {
                    SummaryCard(title: "在线节点", value: "\(store.onlineCount) / \(store.nodes.count)", icon: "checkmark.circle.fill", tint: .green)
                    SummaryCard(title: "平均 CPU", value: store.averageCPU.percentString, icon: "cpu", tint: .cyan)
                    SummaryCard(title: "总下载", value: store.totalDownload.rateString, icon: "arrow.down.circle.fill", tint: .blue)
                    SummaryCard(title: "总上传", value: store.totalUpload.rateString, icon: "arrow.up.circle.fill", tint: .purple)
                }

                if store.offlineCount > 0 {
                    VStack(alignment: .leading, spacing: 10) {
                        Label("离线节点", systemImage: "exclamationmark.triangle.fill")
                            .font(.headline).foregroundStyle(.red)
                        ForEach(store.sortedNodes.filter { store.statuses[$0.uuid]?.online != true }) { node in
                            NavigationLink(value: node) {
                                HStack {
                                    Text(node.region ?? "🌐")
                                    Text(node.name).foregroundStyle(.primary)
                                    Spacer()
                                    Text(store.statuses[node.uuid]?.time.komariDate?.formatted(date: .omitted, time: .shortened) ?? "无上报")
                                        .font(.caption).foregroundStyle(.secondary)
                                }
                                .padding(.vertical, 4)
                            }
                        }
                    }
                    .padding(16)
                    .background(.red.opacity(0.08), in: RoundedRectangle(cornerRadius: 20))
                }

                Text("节点状态").font(.title2.bold())
                ForEach(store.sortedNodes.prefix(5)) { node in
                    NavigationLink(value: node) {
                        NodeCard(node: node, status: store.statuses[node.uuid])
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding()
        }
        .navigationTitle(store.panel?.name ?? "Komari")
        .navigationDestination(for: KomariNode.self) { NodeDetailView(node: $0) }
        .refreshable { await store.refresh() }
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                if store.isRefreshing { ProgressView() }
                else { Button { Task { await store.refresh() } } label: { Image(systemName: "arrow.clockwise") } }
            }
        }
        .safeAreaInset(edge: .bottom) {
            if let date = store.lastUpdated {
                Text("更新于 \(date.formatted(date: .omitted, time: .standard))")
                    .font(.caption2).foregroundStyle(.secondary).padding(.vertical, 5)
            }
        }
    }
}
