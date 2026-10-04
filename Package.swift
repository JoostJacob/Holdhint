// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "Holdhint",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "Holdhint", targets: ["Holdhint"])
    ],
    targets: [
        .target(
            name: "HoldhintCore",
            resources: [.process("Resources")]
        ),
        .executableTarget(
            name: "Holdhint",
            dependencies: ["HoldhintCore"],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
        .executableTarget(
            name: "HoldhintChecks",
            dependencies: ["HoldhintCore"]
        )
    ]
)
