// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Tatami",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(name: "Tatami")
    ]
)
