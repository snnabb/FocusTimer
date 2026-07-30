import SwiftUI
import Charts

private struct PreparedPingPoint: Identifiable, Sendable {
    let time: Date
    let value: Double
    let lost: Bool
    var id: Date { time }
}

struct LatencyDetailView: View {
    let nodeName: String
    let tasks: [PingTask]
    let response: PingRecordsResponse
    @Environment(\.dismiss) private var dismiss
    @State private var selectedTaskID: Int
    @State private var selectedTime: Date?
    private let grouped: [Int: [PreparedPingPoint]]

    init(nodeName: String, tasks: [PingTask], response: PingRecordsResponse) {
        self.nodeName = nodeName
        self.tasks = tasks
        self.response = response
        let prepared = response.records.compactMap { record -> (Int, PreparedPingPoint)? in
            guard let date = record.time.komariDate else { return nil }
            return (record.taskID ?? -1, PreparedPingPoint(time: date, value: max(record.value, 0), lost: record.value < 0))
        }
        grouped = Dictionary(grouping: prepared, by: \.0).mapValues { values in values.map(\.1).sorted { $0.time < $1.time } }
        let first = grouped.keys.sorted().first ?? -1
        _selectedTaskID = State(initialValue: first)
    }

    private var allPoints: [PreparedPingPoint] { grouped[selectedTaskID] ?? [] }
    private var plottedPoints: [PreparedPingPoint] { downsample(allPoints, maxCount: 280) }
    private var validPoints: [PreparedPingPoint] { allPoints.filter { !$0.lost } }

    private var selectedPoint: PreparedPingPoint? {
        guard !plottedPoints.isEmpty else { return nil }
        guard let selectedTime else { return plottedPoints.last }
        var low = 0
        var high = plottedPoints.count
        while low < high {
            let mid = (low + high) / 2
            if plottedPoints[mid].time < selectedTime { low = mid + 1 } else { high = mid }
        }
        if low == 0 { return plottedPoints[0] }
        if low == plottedPoints.count { return plottedPoints[plottedPoints.count - 1] }
        return selectedTime.timeIntervalSince(plottedPoints[low - 1].time) <= plottedPoints[low].time.timeIntervalSince(selectedTime) ? plottedPoints[low - 1] : plottedPoints[low]
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Picker("检测目标", selection: $selectedTaskID) {
                        ForEach(taskIDs, id: \.self) { id in Text(taskName(id)).tag(id) }
                    }
                    .pickerStyle(.menu)
                    .onChange(of: selectedTaskID) { _, _ in selectedTime = nil }

                    Chart {
                        ForEach(plottedPoints.filter { !$0.lost }) { point in
                            LineMark(x: .value("时间", point.time), y: .value("延迟", point.value))
                                .foregroundStyle(.green)
                        }
                        ForEach(plottedPoints.filter(\.lost)) { point in
                            PointMark(x: .value("时间", point.time), y: .value("延迟", 0))
                                .foregroundStyle(.red).symbol(.cross).symbolSize(45)
                        }
                        if let point = selectedPoint {
                            RuleMark(x: .value("选择", point.time)).foregroundStyle(.secondary.opacity(0.6))
                            PointMark(x: .value("时间", point.time), y: .value("延迟", point.value))
                                .foregroundStyle(point.lost ? .red : .green).symbolSize(55)
                        }
                    }
                    .chartXSelection(value: $selectedTime)
                    .chartOverlay { proxy in
                        GeometryReader { geometry in
                            if let point = selectedPoint, let plotFrame = proxy.plotFrame {
                                let plot = geometry[plotFrame]
                                let x = proxy.position(forX: point.time) ?? plot.midX
                                tooltip(point)
                                    .position(x: min(max(x + plot.minX, plot.minX + 72), plot.maxX - 72), y: plot.minY + 30)
                                    .allowsHitTesting(false)
                            }
                        }
                    }
                    .frame(height: 310)
                    .padding(14)
                    .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))

                    if !validPoints.isEmpty {
                        let values = validPoints.map(\.value)
                        LabeledContent("平均", value: "\(Int((values.reduce(0, +) / Double(values.count)).rounded())) ms")
                        LabeledContent("最低", value: "\(Int((values.min() ?? 0).rounded())) ms")
                        LabeledContent("最高", value: "\(Int((values.max() ?? 0).rounded())) ms")
                        LabeledContent("丢包率", value: String(format: "%.1f%%", lossRate))
                    }
                }
                .padding()
            }
            .navigationTitle("\(nodeName) 延迟")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("完成") { dismiss() } } }
        }
    }

    private func tooltip(_ point: PreparedPingPoint) -> some View {
        VStack(spacing: 2) {
            Text(point.time.formatted(date: .omitted, time: .standard)).font(.caption2.monospacedDigit())
            Text(point.lost ? "丢包" : "\(Int(point.value.rounded())) ms")
                .font(.caption.monospacedDigit().bold()).foregroundStyle(point.lost ? .red : .primary)
        }
        .padding(.horizontal, 9).padding(.vertical, 6)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 9)).shadow(radius: 3)
    }

    private var lossRate: Double { allPoints.isEmpty ? 0 : Double(allPoints.filter(\.lost).count) / Double(allPoints.count) * 100 }
    private var taskIDs: [Int] { grouped.keys.sorted() }
    private func taskName(_ id: Int) -> String { tasks.first(where: { $0.id == id })?.name ?? "任务 \(id)" }

    private func downsample(_ values: [PreparedPingPoint], maxCount: Int) -> [PreparedPingPoint] {
        guard values.count > maxCount else { return values }
        let step = Double(values.count - 1) / Double(maxCount - 1)
        return (0..<maxCount).map { values[Int((Double($0) * step).rounded())] }
    }
}
