import SwiftUI

struct RootView: View {
    @EnvironmentObject private var store: MonitorStore
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        TabView {
            NavigationStack { DashboardView() }
                .tabItem { Label("总览", systemImage: "gauge.with.dots.needle.67percent") }
            NavigationStack { NodesView() }
                .tabItem { Label("节点", systemImage: "server.rack") }
            NavigationStack { SettingsView() }
                .tabItem { Label("设置", systemImage: "gearshape") }
        }
        .tint(.cyan)
        .sheet(isPresented: $store.isShowingSetup) {
            SetupView()
                .interactiveDismissDisabled(store.panel == nil)
        }
        .safeAreaInset(edge: .top, spacing: 0) {
            if let reconnectMessage = store.reconnectMessage {
                Label(reconnectMessage, systemImage: "arrow.triangle.2.circlepath")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.orange)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal)
                    .padding(.vertical, 8)
                    .background(.orange.opacity(0.12))
                    .accessibilityLabel(reconnectMessage)
            }
        }
        .alert("连接异常", isPresented: Binding(
            get: { store.actionableErrorMessage != nil },
            set: { if !$0 { store.actionableErrorMessage = nil } }
        )) {
            Button("好") { store.actionableErrorMessage = nil }
        } message: {
            Text(store.actionableErrorMessage ?? "未知错误")
        }
        .onChange(of: scenePhase) { _, phase in
            switch phase {
            case .active:
                store.startAutoRefresh()
                Task { await store.refresh() }
            default:
                store.stopAutoRefresh()
            }
        }
    }
}
