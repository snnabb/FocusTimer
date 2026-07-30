import SwiftUI
import Charts

struct InteractiveMetricChart: View {
    let title: String
    let samples: [HistoricalSample]
    let primaryName: String
    let primaryUnit: String
    let primaryColor: Color
    let primaryValue: (HistoricalSample) -> Double?
    let secondaryName: String?
    let secondaryUnit: String
    let secondaryColor: Color
    let secondaryValue: ((HistoricalSample) -> Double?)?
    @Binding var selectedTime: Date?

    private var selectedSample: HistoricalSample? {
        guard let selectedTime, !samples.isEmpty else { return nil }
        var low = 0
        var high = samples.count
        while low < high {
            let mid = (low + high) / 2
            if samples[mid].time < selectedTime { low = mid + 1 } else { high = mid }
        }
        if low == 0 { return samples[0] }
        if low == samples.count { return samples[samples.count - 1] }
        let previous = samples[low - 1]
        let next = samples[low]
        return selectedTime.timeIntervalSince(previous.time) <= next.time.timeIntervalSince(selectedTime) ? previous : next
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).font(.headline)
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
            .chartOverlay { proxy in
                GeometryReader { geometry in
                    if let sample = selectedSample, let plotFrame = proxy.plotFrame {
                        let plot = geometry[plotFrame]
                        let x = proxy.position(forX: sample.time) ?? plot.midX
                        tooltip(sample)
                            .position(x: min(max(x + plot.minX, plot.minX + 82), plot.maxX - 82), y: plot.minY + 32)
                            .allowsHitTesting(false)
                    }
                }
            }
            .frame(height: 205)
        }
        .padding(14)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))
    }

    private func tooltip(_ sample: HistoricalSample) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(sample.time.formatted(date: .omitted, time: .standard))
                .font(.caption2.monospacedDigit()).foregroundStyle(.secondary)
            if let value = primaryValue(sample) { valueLabel(primaryName, value: value, unit: primaryUnit, color: primaryColor) }
            if let secondaryName, let secondaryValue, let value = secondaryValue(sample) {
                valueLabel(secondaryName, value: value, unit: secondaryUnit, color: secondaryColor)
            }
        }
        .padding(.horizontal, 9).padding(.vertical, 6)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 9))
        .shadow(radius: 3)
    }

    private func valueLabel(_ name: String, value: Double, unit: String, color: Color) -> some View {
        HStack(spacing: 4) {
            Circle().fill(color).frame(width: 6, height: 6)
            Text("\(name) \(format(value))\(unit)").font(.caption2.monospacedDigit().weight(.semibold))
        }
    }

    private func format(_ value: Double) -> String { abs(value) >= 100 ? String(format: "%.0f", value) : String(format: "%.2f", value) }
}
