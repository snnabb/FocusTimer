import Foundation
import SwiftUI

enum NodeHealthState: Equatable {
    case online
    case stale
    case offline
    case unknown

    var label: String {
        switch self {
        case .online: "在线"
        case .stale: "数据延迟"
        case .offline: "离线"
        case .unknown: "无数据"
        }
    }

    var color: Color {
        switch self {
        case .online: .green
        case .stale: .orange
        case .offline: .red
        case .unknown: .gray
        }
    }
}

extension NodeStatus {
    var healthState: NodeHealthState {
        guard online != nil || !time.isEmpty else { return .unknown }
        if online == false { return .offline }
        guard let date = time.komariDate else { return online == true ? .online : .unknown }
        if Date().timeIntervalSince(date) > 30 { return .stale }
        return online == true ? .online : .offline
    }
}

extension KomariNode {
    func trafficUsed(status: NodeStatus?) -> Int64? {
        guard let status else { return nil }
        let up = max(status.netTotalUp ?? 0, 0)
        let down = max(status.netTotalDown ?? 0, 0)
        switch trafficLimitType?.lowercased() ?? "sum" {
        case "max": return max(up, down)
        case "min": return min(up, down)
        case "up": return up
        case "down": return down
        default: return up + down
        }
    }

    func trafficPercent(status: NodeStatus?) -> Double? {
        guard let limit = trafficLimit, limit > 0, let used = trafficUsed(status: status) else { return nil }
        return Double(used) / Double(limit) * 100
    }

    var trafficRuleLabel: String {
        switch trafficLimitType?.lowercased() ?? "sum" {
        case "max": "上传/下载取较大值"
        case "min": "上传/下载取较小值"
        case "up": "仅上传"
        case "down": "仅下载"
        default: "上传 + 下载"
        }
    }
}

struct NodePingSummary: Equatable, Sendable {
    let latency: Double
    let loss: Double?
    let taskName: String?
    let updatedAt: Date?
}
