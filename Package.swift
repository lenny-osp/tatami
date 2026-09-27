// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Tatami",
    platforms: [.macOS(.v14)],
    dependencies: [
        // 自動更新框架
        .package(url: "https://github.com/sparkle-project/Sparkle", from: "2.10.0"),
    ],
    targets: [
        .executableTarget(
            name: "Tatami",
            dependencies: [.product(name: "Sparkle", package: "Sparkle")],
            linkerSettings: [
                // 打包時 Sparkle.framework 放在 Tatami.app/Contents/Frameworks
                .unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks"]),
            ]
        ),
        // 用 Swift Testing（Command Line Tools 內建，不需要 Xcode）；執行：swift test
        .testTarget(name: "TatamiTests", dependencies: ["Tatami"]),
    ]
)
