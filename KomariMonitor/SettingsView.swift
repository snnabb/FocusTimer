import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var store: MonitorStore
    @State private var confirmDelete = false

    var body: some View {
        Form {
            Section("面板") {
                LabeledContent("名称", value: store.panel?.name ?? "未配置")
                LabeledContent("地址", value: store.panel?.normalizedURL?.host ?? "—")
                LabeledContent("Komari", value: store.version?.version ?? "—")
                LabeledContent("站点模式", value: store.publicInfo?.privateSite == true ? "私有" : "未知")
                Button("重新配置") { store.isShowingSetup = true }
            }

            Section("隐私与数据") {
                Label("API Key 存储在系统钥匙串", systemImage: "key.fill")
                Label("不使用中转服务器", systemImage: "arrow.left.arrow.right")
                Label("只调用只读 RPC2 接口", systemImage: "eye.fill")
                Button("清除缓存") { LocalStore.deleteSnapshot() }
                Button("删除面板与凭据", role: .destructive) { confirmDelete = true }
            }

            Section("诊断") {
                LabeledContent("节点", value: "\(store.nodes.count)")
                LabeledContent("在线", value: "\(store.onlineCount)")
                LabeledContent("缓存状态", value: store.usingCachedData ? "缓存" : "实时")
            }
        }
        .navigationTitle("设置")
        .confirmationDialog("删除面板配置？", isPresented: $confirmDelete, titleVisibility: .visible) {
            Button("删除", role: .destructive) { store.deleteConfiguration() }
        } message: {
            Text("面板地址、API Key 和缓存数据都会从此设备删除。")
        }
    }
}
