import SwiftUI
import Charts

struct NodeDetailView: View {
    @EnvironmentObject private var store: MonitorStore
    let node: KomariNode
    @State private var history: [NodeStatus] = []
    @State private var historyHours = 6
    @State private var loadingHistory = false
    @State private var historyError: String?

    private var status: NodeStatus? { store.statuses[node.uuid] }

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 18) {
                header
                liveMetrics
                historySection
                hostSection
            }
            .padding()
        }
        .navigationTitle(node.name)
        .navigationBarTitleDisplayMode(.inline)
        .task(id: historyHours) { await loadHistory() }
        .refreshable {
            await store.refresh()
            await loadHistory()
        }
    }

    private var header: some View {
        HStack(spacing: 14) {
            Text(node.region ?? "🌐").font(.system(size: 42))
            VStack(alignment: .leading) {
                Text(node.name).font(.title2.bold())
                Label(status?.online == true ? "在线" : "离线", systemImage: "circle.fill")
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(status?.online == true ? .green : .red)
                if let time = status?.time.komariDate {
                    Text("上报于 \(time.formatted(date: .abbreviated, time: .standard))")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }
            Spacer()
        }
        .padding(18)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 24))
    }

    private var liveMetrics: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("实时状态").font(.title2.bold())
            metricGrid
            if let ram = status?.ram, let total = status?.ramTotal ?? node.memTotal, total > 0 {
                UsageBar(title: "内存", value: Double(ram) / Double(total) * 100, detail: "\(ram.byteString) / \(total.byteString)", tint: .indigo)
            }
            if let disk = status?.disk, let total = status?.diskTotal ?? node.diskTotal, total > 0 {
                UsageBar(title: "磁盘", value: Double(disk) / Double(total) * 100, detail: "\(disk.byteString) / \(total.byteString)", tint: .purple)
            }
        }
    }

    private var metricGrid: some View {
        let items: [(String, String, String, Color)] = [
            ("CPU", status?.cpu?.percentString ?? "—", "cpu", .cyan),
            ("负载", status.map { String(format: "%.2f", $0.load ?? 0) } ?? "—", "waveform.path.ecg", .orange),
            ("下载", (status?.netIn ?? 0).rateString, "arrow.down", .blue),
            ("上传", (status?.netOut ?? 0).rateString, "arrow.up", .purple),
            ("温度", status?.temp.map { String(format: "%.1f℃", $0) } ?? "—", "thermometer.medium", .red),
            ("TCP", status?.connections.map(String.init) ?? "—", "point.3.connected.trianglepath.dotted", .green)
        ]
        return LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
            ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                SummaryCard(title: item.0, value: item.1, icon: item.2, tint: item.3)
            }
        }
    }

    private var historySection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("历史趋势").font(.title2.bold())
                Spacer()
                Picker("时间", selection: $historyHours) {
                    Text("1小时").tag(1)
                    Text("6小时").tag(6)
                    Text("24小时").tag(24)
                }
                .pickerStyle(.menu)
            }
            if loadingHistory {
                ProgressView().frame(maxWidth: .infinity, minHeight: 180)
            } else if let historyError {
                ContentUnavailableView("无法读取历史数据", systemImage: "chart.xyaxis.line", description: Text(historyError))
                    .frame(minHeight: 180)
            } else if history.isEmpty {
                ContentUnavailableView("暂无历史数据", systemImage: "chart.xyaxis.line")
                    .frame(minHeight: 180)
            } else {
                Chart(history) { record in
                    if let date = record.time.komariDate, let cpu = record.cpu {
                        LineMark(x: .value("时间", date), y: .value("CPU", cpu))
                            .foregroundStyle(.cyan)
                            .interpolationMethod(.catmullRom)
                    }
                }
                .chartYScale(domain: 0...100)
                .chartYAxisLabel("CPU %")
                .frame(height: 220)
                .padding(12)
                .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))
            }
        }
    }

    private var hostSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("主机信息").font(.title2.bold())
            VStack(spacing: 0) {
                infoRow("系统", node.os)
                infoRow("CPU", node.cpuName)
                infoRow("核心", node.cpuCores.map { "\($0)" })
                infoRow("架构", node.arch)
                infoRow("虚拟化", node.virtualization)
                infoRow("内核", node.kernelVersion)
                infoRow("GPU", node.gpuName)
                infoRow("Agent", node.version)
                infoRow("IPv4", node.ipv4)
                infoRow("IPv6", node.ipv6)
                infoRow("分组", node.group)
                infoRow("标签", node.tags)
                infoRow("备注", node.remark ?? node.publicRemark)
            }
            .padding(.horizontal, 16)
            .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))
        }
    }

    @ViewBuilder
    private func infoRow(_ title: String, _ value: String?) -> some View {
        if let value, !value.isEmpty, value.lowercased() != "none" {
            LabeledContent(title, value: value)
                .font(.subheadline)
                .padding(.vertical, 11)
                .overlay(alignment: .bottom) { Divider() }
        }
    }

    private func loadHistory() async {
        loadingHistory = true
        historyError = nil
        defer { loadingHistory = false }
        do {
            history = try await store.history(for: node.uuid, hours: historyHours)
                .sorted { ($0.time.komariDate ?? .distantPast) < ($1.time.komariDate ?? .distantPast) }
        } catch {
            historyError = error.localizedDescription
        }
    }
}
