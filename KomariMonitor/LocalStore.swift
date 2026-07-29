import Foundation

enum LocalStore {
    private static let panelKey = "komari.panel"
    private static let snapshotKey = "komari.snapshot"

    static func savePanel(_ panel: PanelConfiguration) throws {
        UserDefaults.standard.set(try JSONEncoder().encode(panel), forKey: panelKey)
    }

    static func loadPanel() -> PanelConfiguration? {
        guard let data = UserDefaults.standard.data(forKey: panelKey) else { return nil }
        return try? JSONDecoder().decode(PanelConfiguration.self, from: data)
    }

    static func deletePanel() {
        UserDefaults.standard.removeObject(forKey: panelKey)
    }

    static func saveSnapshot(_ snapshot: MonitorSnapshot) {
        guard let data = try? JSONEncoder().encode(snapshot) else { return }
        UserDefaults.standard.set(data, forKey: snapshotKey)
    }

    static func loadSnapshot() -> MonitorSnapshot? {
        guard let data = UserDefaults.standard.data(forKey: snapshotKey) else { return nil }
        return try? JSONDecoder().decode(MonitorSnapshot.self, from: data)
    }

    static func deleteSnapshot() {
        UserDefaults.standard.removeObject(forKey: snapshotKey)
    }
}
