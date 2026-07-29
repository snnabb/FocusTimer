import Foundation

extension Int64 {
    var byteString: String {
        ByteCountFormatter.string(fromByteCount: self, countStyle: .binary)
    }

    var rateString: String { "\(byteString)/s" }
}

extension Double {
    var percentString: String {
        guard isFinite else { return "—" }
        return String(format: "%.1f%%", self)
    }
}

extension String {
    var komariDate: Date? {
        ISO8601DateFormatter.komari.date(from: self)
    }
}

extension ISO8601DateFormatter {
    static let komari: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()
}
