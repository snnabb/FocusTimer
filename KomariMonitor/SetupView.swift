import SwiftUI

struct SetupView: View {
    @EnvironmentObject private var store: MonitorStore
    @Environment(\.dismiss) private var dismiss
    @State private var name = "我的 Komari"
    @State private var address = ""
    @State private var apiKey = ""
    @State private var isSaving = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("面板名称", text: $name)
                    TextField("https://status.example.com", text: $address)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                    SecureField("API Key", text: $apiKey)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                } header: {
                    Text("连接 Komari")
                } footer: {
                    Text("API Key 仅存入此设备的钥匙串。应用只调用只读 RPC2 方法。")
                }

                if let error {
                    Section { Text(error).foregroundStyle(.red) }
                }

                Section {
                    Button {
                        Task { await connect() }
                    } label: {
                        HStack {
                            Spacer()
                            if isSaving { ProgressView().padding(.trailing, 6) }
                            Text(isSaving ? "正在验证" : "验证并保存")
                            Spacer()
                        }
                    }
                    .disabled(isSaving || name.trimmingCharacters(in: .whitespaces).isEmpty || address.isEmpty || apiKey.isEmpty)
                }
            }
            .navigationTitle("添加面板")
            .toolbar {
                if store.panel != nil {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("取消") { dismiss() }
                    }
                }
            }
        }
    }

    private func connect() async {
        isSaving = true
        error = nil
        defer { isSaving = false }
        do {
            let panel = PanelConfiguration(name: name.trimmingCharacters(in: .whitespacesAndNewlines), baseURL: address)
            try await store.configure(panel: panel, apiKey: apiKey)
            apiKey = ""
            dismiss()
        } catch {
            self.error = error.localizedDescription
        }
    }
}
