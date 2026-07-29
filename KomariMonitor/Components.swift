import SwiftUI

struct SummaryCard: View {
    let title: String
    let value: String
    let icon: String
    let tint: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Image(systemName: icon)
                .font(.title3.weight(.semibold))
                .foregroundStyle(tint)
            Text(value)
                .font(.title2.monospacedDigit().weight(.bold))
                .contentTransition(.numericText())
                .lineLimit(1)
                .minimumScaleFactor(0.65)
            Text(title)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20, style: .continuous))
    }
}

struct UsageBar: View {
    let title: String
    let value: Double
    let detail: String?
    var tint: Color = .cyan
    var animated = true

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(title).font(.caption).foregroundStyle(.secondary)
                Spacer()
                Text(detail ?? value.percentString)
                    .font(.caption.monospacedDigit().weight(.semibold))
                    .contentTransition(.numericText())
            }
            ProgressView(value: min(max(value, 0), 100), total: 100)
                .tint(value >= 90 ? .red : value >= 75 ? .orange : tint)
                .animation(animated ? .easeInOut(duration: 0.3) : nil, value: value)
        }
    }
}

struct StatusBadge: View {
    let state: NodeHealthState

    var body: some View {
        Label(state.label, systemImage: "circle.fill")
            .font(.caption.weight(.semibold))
            .foregroundStyle(state.color)
    }
}

struct NodeCard: View {
    let node: KomariNode
    let status: NodeStatus?
    let ping: NodePingSummary?

    private var memoryPercent: Double {
        guard let used = status?.ram, let total = status?.ramTotal ?? node.memTotal, total > 0 else { return 0 }
        return Double(used) / Double(total) * 100
    }

    private var diskPercent: Double {
        guard let used = status?.disk, let total = status?.diskTotal ?? node.diskTotal, total > 0 else { return 0 }
        return Double(used) / Double(total) * 100
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 10) {
                Text(node.region.flatMap { $0.isEmpty ? nil : $0 } ?? "🌐").font(.title2)
                VStack(alignment: .leading, spacing: 2) {
                    Text(node.name).font(.headline)
                    Text([node.group, node.os].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: " · "))
                        .font(.caption).foregroundStyle(.secondary).lineLimit(1)
                }
                Spacer()
                VStack(alignment: .trailing, spacing: 3) {
                    StatusBadge(state: status?.healthState ?? .unknown)
                    if let ping {
                        Text("\(Int(ping.latency.rounded())) ms")
                            .font(.caption2.monospacedDigit())
                            .foregroundStyle(latencyColor(ping.latency))
                    }
                }
            }
            UsageBar(title: "CPU", value: status?.cpu ?? 0, detail: status?.cpu?.percentString, animated: false)
            UsageBar(title: "内存", value: memoryPercent, detail: memoryPercent.percentString, tint: .indigo, animated: false)
            UsageBar(title: "磁盘", value: diskPercent, detail: diskPercent.percentString, tint: .purple, animated: false)

            HStack {
                Label((status?.netIn ?? 0).rateString, systemImage: "arrow.down")
                Spacer()
                Label((status?.netOut ?? 0).rateString, systemImage: "arrow.up")
            }
            .font(.caption.monospacedDigit())
            .foregroundStyle(.secondary)

            trafficView
        }
        .padding(16)
        .background(Color(uiColor: .secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
    }

    @ViewBuilder
    private var trafficView: some View {
        if let used = node.trafficUsed(status: status), let limit = node.trafficLimit, limit > 0,
           let percent = node.trafficPercent(status: status) {
            UsageBar(title: "累计流量", value: percent, detail: "\(used.byteString) / \(limit.byteString)", tint: .blue, animated: false)
        } else if let status {
            HStack {
                Text("累计流量").foregroundStyle(.secondary)
                Spacer()
                Text("↑ \((status.netTotalUp ?? 0).byteString)  ↓ \((status.netTotalDown ?? 0).byteString)")
                    .monospacedDigit()
            }
            .font(.caption)
        }
    }

    private func latencyColor(_ latency: Double) -> Color {
        latency >= 200 ? .red : latency >= 80 ? .orange : .green
    }
}
