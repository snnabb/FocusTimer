import SwiftUI
import Charts

struct LatencyDetailView: View {
    let nodeName: String
    let tasks: [PingTask]
    let response: PingRecordsResponse
    @Environment(\.dismiss) private var dismiss
    @State private var selectedTaskID: Int
    @State private var selectedTime: Date?

    init(nodeName: String, tasks: [PingTask], response: PingRecordsResponse) {
        self.nodeName = nodeName
        self.tasks = tasks
        self.response = response
        let first = response.records.compactMap(\.taskID).sorted().first ?? -1
        _selectedTaskID = State(initialValue: first)
    }

    private var records: [PingRecord] {
        response.records.filter { ($0.taskID ?? -1) == selectedTaskID }.sorted { $0.time < $1.time }
    }

    private var selectedRecord: PingRecord? {
        guard let selectedTime else { return records.last }
        return records.min { abs(($0.time.komariDate ?? .distantPast).timeIntervalSince(selectedTime)) < abs(($1.time.komariDate ?? .distantPast).timeIntervalSince(selectedTime)) }
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Picker("检测目标", selection: $selectedTaskID) {
                        ForEach(taskIDs, id: \.self) { id in
                            Text(taskName(id)).tag(id)
                        }
                    }
                    .pickerStyle(.menu)

                    if let record = selectedRecord {
                        HStack(spacing: 12) {
                            SummaryCard(title: "延迟", value: "\(Int(record.value.rounded())) ms", icon: "network", tint: latencyColor(record.value))
                            SummaryCard(title: "记录时间", value: record.time.komariDate?.formatted(date: .omitted, time: .shortened) ?? "—", icon: "clock", tint: .blue)
                        }
                    }

                    Chart(records) { record in
                        if let date = record.time.komariDate {
                            LineMark(x: .value("时间", date), y: .value("延迟", record.value))
                                .foregroundStyle(.green)
                            if selectedRecord?.id == record.id {
                                PointMark(x: .value("时间", date), y: .value("延迟", record.value))
                                    .foregroundStyle(.green).symbolSize(55)
                            }
                        }
                        if let selectedTime {
                            RuleMark(x: .value("选择", selectedTime))
                                .foregroundStyle(.secondary.opacity(0.6))
                        }
                    }
                    .chartXSelection(value: $selectedTime)
                    .frame(height: 300)
                    .padding(14)
                    .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))

                    let values = records.map(\.value)
                    if !values.isEmpty {
                        LabeledContent("平均", value: "\(Int((values.reduce(0, +) / Double(values.count)).rounded())) ms")
                        LabeledContent("最低", value: "\(Int((values.min() ?? 0).rounded())) ms")
                        LabeledContent("最高", value: "\(Int((values.max() ?? 0).rounded())) ms")
                    }
                }
                .padding()
            }
            .navigationTitle("\(nodeName) 延迟")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("完成") { dismiss() } } }
        }
    }

    private var taskIDs: [Int] { Array(Set(response.records.map { $0.taskID ?? -1 })).sorted() }
    private func taskName(_ id: Int) -> String { tasks.first(where: { $0.id == id })?.name ?? "任务 \(id)" }
    private func latencyColor(_ value: Double) -> Color { value >= 200 ? .red : value >= 80 ? .orange : .green }
}
