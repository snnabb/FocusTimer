import SwiftUI

@main
struct KomariMonitorApp: App {
    @StateObject private var store = MonitorStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(store)
                .task { await store.bootstrap() }
        }
    }
}
