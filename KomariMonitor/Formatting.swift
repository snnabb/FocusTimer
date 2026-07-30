import Foundation

extension Int64 {
    var byteString: String {
        ByteCountFormatter.string(fromByteCount: self, countStyle: .binary)
    }

    var zeroSafeByteString: String {
        self == 0 ? "0 KB" : byteString
    }

    var rateString: String { self == 0 ? "0 KB/s" : "\(byteString)/s" }
}

extension Double {
    var percentString: String {
        guard isFinite else { return "—" }
        return String(format: "%.1f%%", self)
    }
}

extension String {
    var komariDate: Date? {
        let fractional = ISO8601DateFormatter()
        fractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = fractional.date(from: self) { return date }

        let standard = ISO8601DateFormatter()
        standard.formatOptions = [.withInternetDateTime]
        return standard.date(from: self)
    }
}
