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
                .lineLimit(1)
                .minimumScaleFactor(0.7)
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

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(title).font(.caption).foregroundStyle(.secondary)
                Spacer()
                Text(detail ?? value.percentString).font(.caption.monospacedDigit().weight(.semibold))
            }
            ProgressView(value: min(max(value, 0), 100), total: 100)
                .tint(value >= 90 ? .red : value >= 75 ? .orange : tint)
        }
    }
}

struct NodeCard: View {
    let node: KomariNode
    let status: NodeStatus?

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
                Label(status?.online == true ? "在线" : "离线", systemImage: "circle.fill")
                    .labelStyle(.titleAndIcon)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(status?.online == true ? .green : .red)
            }
            UsageBar(title: "CPU", value: status?.cpu ?? 0, detail: status?.cpu?.percentString)
            UsageBar(title: "内存", value: memoryPercent, detail: memoryPercent.percentString, tint: .indigo)
            UsageBar(title: "磁盘", value: diskPercent, detail: diskPercent.percentString, tint: .purple)
            HStack {
                Label((status?.netIn ?? 0).rateString, systemImage: "arrow.down")
                Spacer()
                Label((status?.netOut ?? 0).rateString, systemImage: "arrow.up")
            }
            .font(.caption.monospacedDigit())
            .foregroundStyle(.secondary)
        }
        .padding(16)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
    }
}
