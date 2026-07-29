import SwiftUI

struct DashboardView: View {
    @EnvironmentObject private var store: MonitorStore
    private let columns = [GridItem(.flexible()), GridItem(.flexible())]

    private var problemNodes: [KomariNode] {
        store.sortedNodes.filter {
            let state = store.statuses[$0.uuid]?.healthState ?? .unknown
            return state != .online
        }
    }

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 20) {
                connectionBanner

                LazyVGrid(columns: columns, spacing: 12) {
                    SummaryCard(title: "在线节点", value: "\(store.onlineCount) / \(store.nodes.count)", icon: "checkmark.circle.fill", tint: .green)
                    SummaryCard(title: "异常节点", value: "\(store.offlineCount + store.staleCount + store.unknownCount)", icon: "exclamationmark.triangle.fill", tint: .orange)
                    SummaryCard(title: "总下载", value: store.totalDownload.rateString, icon: "arrow.down.circle.fill", tint: .blue)
                    SummaryCard(title: "总上传", value: store.totalUpload.rateString, icon: "arrow.up.circle.fill", tint: .purple)
                    SummaryCard(title: "平均 CPU", value: store.averageCPU.percentString, icon: "cpu", tint: .cyan)
                    SummaryCard(title: "平均负载", value: String(format: "%.2f", store.averageLoad), icon: "waveform.path.ecg", tint: .orange)
                }

                if problemNodes.isEmpty {
                    Label("所有节点运行正常", systemImage: "checkmark.seal.fill")
                        .font(.headline)
                        .foregroundStyle(.green)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(18)
                        .background(.green.opacity(0.08), in: RoundedRectangle(cornerRadius: 20))
                } else {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("异常节点").font(.title2.bold())
                        ForEach(problemNodes) { node in
                            NavigationLink(value: node) {
                                HStack(spacing: 10) {
                                    Text(node.region ?? "🌐")
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(node.name).foregroundStyle(.primary)
                                        Text(store.statuses[node.uuid]?.time.komariDate?.formatted(date: .omitted, time: .standard) ?? "尚无上报")
                                            .font(.caption).foregroundStyle(.secondary)
                                    }
                                    Spacer()
                                    StatusBadge(state: store.statuses[node.uuid]?.healthState ?? .unknown)
                                }
                                .padding(14)
                                .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 16))
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
            }
            .padding()
        }
        .navigationTitle(store.panel?.name ?? "Komari")
        .navigationDestination(for: KomariNode.self) { NodeDetailView(node: $0) }
        .refreshable { await store.refresh() }
    }

    private var connectionBanner: some View {
        HStack(spacing: 8) {
            Circle()
                .fill(store.usingCachedData ? .orange : store.isConnected ? .green : .red)
                .frame(width: 8, height: 8)
            Text(store.usingCachedData ? "缓存数据" : store.isConnected ? "实时连接" : "正在重连")
                .font(.caption.weight(.semibold))
            Spacer()
            if let date = store.lastUpdated {
                Text(date.formatted(date: .omitted, time: .standard))
                    .font(.caption2.monospacedDigit()).foregroundStyle(.secondary)
            }
        }
    }
}
