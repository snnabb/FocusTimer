import SwiftUI
import Charts

struct NodeDetailView: View {
    @EnvironmentObject private var store: MonitorStore
    let node: KomariNode
    @State private var history: [HistoricalSample] = []
    @State private var pingResponse: PingRecordsResponse?
    @State private var historyHours = 0
    @State private var loadingHistory = false
    @State private var historyError: String?
    @State private var realtimeTask: Task<Void, Never>?
    @State private var selectedHistoryTime: Date?
    @State private var showingLatencyDetails = false

    private var status: NodeStatus? { store.statuses[node.uuid] }
    private var pingTasks: [PingTask] { store.pingTasks.filter { $0.clients?.contains(node.uuid) == true || $0.defaultOn == true } }

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 20) {
                hostOverview
                liveResources
                trafficSection
                if !pingTasks.isEmpty || pingResponse?.records.isEmpty == false { latencySection }
                historySection
                moreInfo
            }
            .padding()
        }
        .navigationTitle(node.name)
        .navigationBarTitleDisplayMode(.inline)
        .task {
            await loadHistory()
            await loadPing()
            startRealtime()
        }
        .onDisappear { realtimeTask?.cancel() }
        .onChange(of: historyHours) { _, _ in Task { await loadHistory() } }
        .refreshable {
            _ = try? await store.refreshNode(node.uuid)
            await loadHistory()
            await loadPing()
        }
        .sheet(isPresented: $showingLatencyDetails) {
            if let pingResponse {
                LatencyDetailView(nodeName: node.name, tasks: pingTasks, response: pingResponse)
            }
        }
    }

    private var hostOverview: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 12) {
                Text(node.region ?? "🌐").font(.system(size: 42))
                VStack(alignment: .leading, spacing: 4) {
                    Text(node.name).font(.title2.bold())
                    StatusBadge(state: status?.healthState ?? .unknown)
                    Text([node.os, node.virtualization?.uppercased(), node.arch?.uppercased()].compactMap { $0 }.joined(separator: " · "))
                        .font(.caption).foregroundStyle(.secondary).lineLimit(2)
                }
                Spacer()
            }

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                if let cpu = node.cpuName { Label(cpu, systemImage: "cpu").lineLimit(2) }
                HStack {
                    if let cores = node.cpuCores { Label("\(cores) 核", systemImage: "square.stack.3d.up") }
                    if let memory = node.memTotal { Label(memory.byteString, systemImage: "memorychip") }
                    if let disk = node.diskTotal { Label(disk.byteString, systemImage: "internaldrive") }
                }
                .font(.subheadline)
                .foregroundStyle(.secondary)
                if let time = status?.time.komariDate {
                    Label("最后上报 \(time.formatted(date: .omitted, time: .standard))", systemImage: "clock")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }
        }
        .padding(18)
        .background(
            LinearGradient(colors: [.cyan.opacity(0.18), .blue.opacity(0.06)], startPoint: .topLeading, endPoint: .bottomTrailing),
            in: RoundedRectangle(cornerRadius: 26, style: .continuous)
        )
        .overlay(RoundedRectangle(cornerRadius: 26).stroke(.white.opacity(0.12)))
    }

    private var liveResources: some View {
        VStack(alignment: .leading, spacing: 14) {
            sectionHeader("当前资源", detail: "每 2 秒更新")
            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                SummaryCard(title: "CPU", value: status?.cpu?.percentString ?? "—", icon: "cpu", tint: .cyan)
                SummaryCard(title: "内存使用率", value: memoryPercentText, icon: "memorychip", tint: .indigo)
                SummaryCard(title: "负载 1 / 5 / 15", value: loadText, icon: "waveform.path.ecg", tint: .orange)
                SummaryCard(title: "实时网络", value: "↓ \((status?.netIn ?? 0).rateString)\n↑ \((status?.netOut ?? 0).rateString)", icon: "arrow.up.arrow.down", tint: .blue)
                SummaryCard(title: "TCP / UDP", value: "\(status?.connections ?? 0) / \(status?.connectionsUDP ?? 0)", icon: "point.3.connected.trianglepath.dotted", tint: .green)
                SummaryCard(title: "进程", value: status?.process.map(String.init) ?? "—", icon: "list.bullet.rectangle", tint: .indigo)
            }
            if let ram = status?.ram, let total = status?.ramTotal ?? node.memTotal, total > 0 {
                UsageBar(title: "内存", value: Double(ram) / Double(total) * 100, detail: "\(ram.byteString) / \(total.byteString)", tint: .indigo)
            }
            if let swap = status?.swap, let total = status?.swapTotal ?? node.swapTotal, total > 0 {
                UsageBar(title: "Swap", value: Double(swap) / Double(total) * 100, detail: "\(swap.zeroSafeByteString) / \(total.byteString)", tint: .orange)
            }
            if let disk = status?.disk, let total = status?.diskTotal ?? node.diskTotal, total > 0 {
                UsageBar(title: "磁盘", value: Double(disk) / Double(total) * 100, detail: "\(disk.byteString) / \(total.byteString)", tint: .purple)
            }
        }
    }

    private var trafficSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            sectionHeader("累计流量", detail: node.trafficRuleLabel)
            if let used = node.trafficUsed(status: status), let limit = node.trafficLimit, limit > 0,
               let percent = node.trafficPercent(status: status) {
                UsageBar(title: "用量", value: percent, detail: "\(used.byteString) / \(limit.byteString)", tint: .blue)
            }
            HStack(spacing: 12) {
                SummaryCard(title: "累计上传", value: (status?.netTotalUp ?? 0).byteString, icon: "arrow.up.circle", tint: .purple)
                SummaryCard(title: "累计下载", value: (status?.netTotalDown ?? 0).byteString, icon: "arrow.down.circle", tint: .blue)
            }
            if let expiry = node.expiredAt?.komariDate {
                Label("套餐到期 \(expiry.formatted(date: .abbreviated, time: .omitted))", systemImage: "calendar")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
    }

    private var latencySection: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("网络延迟").font(.title2.bold())
                Spacer()
                if pingResponse?.records.isEmpty == false {
                    Button("详细趋势") { showingLatencyDetails = true }
                        .font(.subheadline.weight(.semibold))
                }
            }
            if let response = pingResponse, !response.records.isEmpty {
                let grouped = Dictionary(grouping: response.records, by: { $0.taskID ?? -1 })
                VStack(spacing: 0) {
                    HStack {
                        Text("检测目标").frame(maxWidth: .infinity, alignment: .leading)
                        Text("当前").frame(width: 68, alignment: .trailing)
                        Text("平均").frame(width: 68, alignment: .trailing)
                    }
                    .font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                    .padding(.vertical, 10)
                    Divider()
                    ForEach(grouped.keys.sorted(), id: \.self) { taskID in
                        if let records = grouped[taskID], let latest = records.max(by: { $0.time < $1.time }) {
                            let values = records.map(\.value)
                            HStack {
                                Text(pingTasks.first(where: { $0.id == taskID })?.name ?? "任务 \(taskID)")
                                    .lineLimit(1).frame(maxWidth: .infinity, alignment: .leading)
                                Text("\(Int(latest.value.rounded())) ms")
                                    .foregroundStyle(latencyColor(latest.value)).frame(width: 68, alignment: .trailing)
                                Text("\(Int((values.reduce(0, +) / Double(values.count)).rounded())) ms")
                                    .frame(width: 68, alignment: .trailing)
                            }
                            .font(.subheadline.monospacedDigit())
                            .padding(.vertical, 11)
                            Divider()
                        }
                    }
                }
                .padding(.horizontal, 14)
                .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 18))
            } else {
                ContentUnavailableView("暂无延迟记录", systemImage: "network")
                    .frame(minHeight: 120)
            }
        }
    }

    private var historySection: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("历史趋势").font(.title2.bold())
                Spacer()
                Picker("时间", selection: $historyHours) {
                    Text("实时").tag(0)
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
                ContentUnavailableView(store.publicInfo?.recordEnabled == false ? "Komari 未开启历史记录" : "暂无历史记录", systemImage: "chart.xyaxis.line")
                    .frame(minHeight: 180)
            } else {
                InteractiveMetricChart(title: "CPU 与负载", samples: history, primaryName: "CPU", primaryUnit: "%", primaryColor: .cyan, primaryValue: { $0.cpu }, secondaryName: "Load", secondaryUnit: "", secondaryColor: .orange, secondaryValue: { $0.load }, selectedTime: $selectedHistoryTime)
                InteractiveMetricChart(title: "内存与磁盘", samples: history, primaryName: "内存", primaryUnit: "%", primaryColor: .indigo, primaryValue: { percent($0.memoryUsed, total: $0.memoryTotal) }, secondaryName: "磁盘", secondaryUnit: "%", secondaryColor: .purple, secondaryValue: { percent($0.diskUsed, total: $0.diskTotal) }, selectedTime: $selectedHistoryTime)
                InteractiveMetricChart(title: "网络速度", samples: history, primaryName: "下载", primaryUnit: " MB/s", primaryColor: .blue, primaryValue: { Double($0.networkIn ?? 0) / 1_048_576 }, secondaryName: "上传", secondaryUnit: " MB/s", secondaryColor: .purple, secondaryValue: { Double($0.networkOut ?? 0) / 1_048_576 }, selectedTime: $selectedHistoryTime)
                InteractiveMetricChart(title: "连接与进程", samples: history, primaryName: "TCP", primaryUnit: "", primaryColor: .green, primaryValue: { Double($0.connections ?? 0) }, secondaryName: "进程", secondaryUnit: "", secondaryColor: .indigo, secondaryValue: { Double($0.process ?? 0) }, selectedTime: $selectedHistoryTime)
            }
        }
    }

    private var moreInfo: some View {
        DisclosureGroup("更多主机信息") {
            VStack(spacing: 0) {
                infoRow("内核", node.kernelVersion)
                infoRow("Agent", node.version)
                infoRow("IPv4", node.ipv4)
                infoRow("IPv6", node.ipv6)
                infoRow("分组", node.group)
                infoRow("标签", node.tags)
                infoRow("备注", node.remark ?? node.publicRemark)
            }
            .padding(.top, 8)
        }
        .padding(16)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 20))
    }

    private var memoryPercentText: String {
        guard let used = status?.ram, let total = status?.ramTotal ?? node.memTotal, total > 0 else { return "—" }
        return (Double(used) / Double(total) * 100).percentString
    }

    private var loadText: String {
        guard let status else { return "—" }
        return String(format: "%.2f / %.2f / %.2f", status.load ?? 0, status.load5 ?? 0, status.load15 ?? 0)
    }

    private func sectionHeader(_ title: String, detail: String) -> some View {
        HStack {
            Text(title).font(.title2.bold())
            Spacer()
            Text(detail).font(.caption).foregroundStyle(.secondary)
        }
    }


    @ViewBuilder
    private func infoRow(_ title: String, _ value: String?) -> some View {
        if let value, !value.isEmpty, value.lowercased() != "none" {
            LabeledContent(title, value: value)
                .font(.subheadline)
                .padding(.vertical, 10)
                .overlay(alignment: .bottom) { Divider() }
        }
    }

    private func percent(_ used: Int64?, total: Int64?) -> Double? {
        guard let used, let total, total > 0 else { return nil }
        return Double(used) / Double(total) * 100
    }

    private func latencyColor(_ value: Double) -> Color {
        value >= 200 ? .red : value >= 80 ? .orange : .green
    }

    private func startRealtime() {
        realtimeTask?.cancel()
        realtimeTask = Task {
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(2))
                guard !Task.isCancelled else { return }
                if historyHours == 0, let latestStatus = try? await store.refreshNode(node.uuid), let latest = HistoricalSample(status: latestStatus, node: node) {
                    if history.last?.time != latest.time {
                        history.append(latest)
                        if history.count > 120 { history.removeFirst(history.count - 120) }
                    }
                }
            }
        }
    }

    private func loadHistory() async {
        loadingHistory = true
        historyError = nil
        defer { loadingHistory = false }
        do {
            history = try await store.compatibleHistory(for: node, hours: historyHours)
            selectedHistoryTime = nil
        } catch {
            historyError = error.localizedDescription
            history = []
        }
    }

    private func loadPing() async {
        pingResponse = try? await store.pingDetails(for: node.uuid, hours: 6)
    }
}
