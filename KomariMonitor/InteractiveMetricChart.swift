import SwiftUI
import Charts

struct InteractiveMetricChart: View {
    let title: String
    let samples: [HistoricalSample]
    let primaryName: String
    let primaryColor: Color
    let primaryValue: (HistoricalSample) -> Double?
    let secondaryName: String?
    let secondaryColor: Color
    let secondaryValue: ((HistoricalSample) -> Double?)?
    @Binding var selectedTime: Date?

    private var selectedSample: HistoricalSample? {
        guard let selectedTime else { return nil }
        return samples.min { abs($0.time.timeIntervalSince(selectedTime)) < abs($1.time.timeIntervalSince(selectedTime)) }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(title).font(.headline)
                Spacer()
                if let sample = selectedSample {
                    Text(sample.time.formatted(date: .omitted, time: .standard))
                        .font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                }
            }
            if let sample = selectedSample {
                HStack(spacing: 14) {
                    if let value = primaryValue(sample) {
                        valueLabel(primaryName, value: value, color: primaryColor)
                    }
                    if let secondaryName, let secondaryValue, let value = secondaryValue(sample) {
                        valueLabel(secondaryName, value: value, color: secondaryColor)
                    }
                }
            }
            Chart {
                ForEach(samples) { sample in
                    if let value = primaryValue(sample) {
                        LineMark(x: .value("时间", sample.time), y: .value(primaryName, value), series: .value("指标", primaryName))
                            .foregroundStyle(primaryColor)
                    }
                    if let secondaryName, let secondaryValue, let value = secondaryValue(sample) {
                        LineMark(x: .value("时间", sample.time), y: .value(secondaryName, value), series: .value("指标", secondaryName))
                            .foregroundStyle(secondaryColor)
                    }
                }
                if let sample = selectedSample {
                    RuleMark(x: .value("选择", sample.time))
                        .foregroundStyle(.secondary.opacity(0.6))
                        .lineStyle(StrokeStyle(lineWidth: 1, dash: [4, 3]))
                    if let value = primaryValue(sample) {
                        PointMark(x: .value("时间", sample.time), y: .value(primaryName, value))
                            .foregroundStyle(primaryColor).symbolSize(45)
                    }
                    if let secondaryName, let secondaryValue, let value = secondaryValue(sample) {
                        PointMark(x: .value("时间", sample.time), y: .value(secondaryName, value))
                            .foregroundStyle(secondaryColor).symbolSize(45)
                    }
                }
            }
            .chartXSelection(value: $selectedTime)
            .frame(height: 190)
        }
        .padding(14)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))
    }

    private func valueLabel(_ name: String, value: Double, color: Color) -> some View {
        HStack(spacing: 4) {
            Circle().fill(color).frame(width: 7, height: 7)
            Text("\(name) \(format(value))")
                .font(.caption.monospacedDigit().weight(.semibold))
        }
    }

    private func format(_ value: Double) -> String {
        abs(value) >= 100 ? String(format: "%.0f", value) : String(format: "%.2f", value)
    }
}
