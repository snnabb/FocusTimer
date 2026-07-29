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
        .alert("连接异常", isPresented: Binding(
            get: { store.errorMessage != nil },
            set: { if !$0 { store.errorMessage = nil } }
        )) {
            Button("好") { store.errorMessage = nil }
        } message: {
            Text(store.errorMessage ?? "未知错误")
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
