import AppKit
import Foundation

func drawArrow(from start: NSPoint, to end: NSPoint) {
    let color = NSColor(white: 1, alpha: 0.92)
    let shaft = NSBezierPath()
    shaft.move(to: start)
    shaft.line(to: end)
    shaft.lineWidth = 3
    color.setStroke()
    shaft.stroke()

    let angle = atan2(end.y - start.y, end.x - start.x)
    let length: CGFloat = 16
    let spread: CGFloat = .pi / 6
    let head = NSBezierPath()
    head.move(to: NSPoint(
        x: end.x - length * cos(angle - spread),
        y: end.y - length * sin(angle - spread)
    ))
    head.line(to: end)
    head.line(to: NSPoint(
        x: end.x - length * cos(angle + spread),
        y: end.y - length * sin(angle + spread)
    ))
    head.lineWidth = 3
    head.lineCapStyle = .round
    head.lineJoinStyle = .round
    head.stroke()
}

// Draws the Holdhint disk-image background. Icon slots are left clear so
// Finder can place Holdhint.app and Applications on top. The warning sits
// in the lower band, which stays readable if those positions are not applied.
let output = CommandLine.arguments.dropFirst().first ?? ""
if output.isEmpty {
    fputs("usage: dmg-background.swift output.png\n", stderr)
    exit(1)
}

let size = NSSize(width: 680, height: 420)
let image = NSImage(size: size, flipped: true) { rect in
    NSColor(srgbRed: 0.09, green: 0.10, blue: 0.12, alpha: 1).setFill()
    rect.fill()

    let band = NSRect(x: 28, y: 248, width: 624, height: 148)
    let bandPath = NSBezierPath(roundedRect: band, xRadius: 16, yRadius: 16)
    NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.06).setFill()
    bandPath.fill()

    let title = "Install Holdhint" as NSString
    let titleFont = NSFont.systemFont(ofSize: 22, weight: .semibold)
    let titleAttrs: [NSAttributedString.Key: Any] = [
        .font: titleFont,
        .foregroundColor: NSColor.white
    ]
    let titleSize = title.size(withAttributes: titleAttrs)
    title.draw(
        at: NSPoint(x: (rect.width - titleSize.width) / 2, y: 28),
        withAttributes: titleAttrs
    )

    drawArrow(from: NSPoint(x: 250, y: 158), to: NSPoint(x: 412, y: 158))

    let lines: [(String, NSFont, NSColor)] = [
        ("First launch", NSFont.systemFont(ofSize: 15, weight: .semibold), NSColor.white),
        ("Click Done. Do not click Move to Trash.",
         NSFont.systemFont(ofSize: 18, weight: .semibold),
         NSColor(srgbRed: 1, green: 0.78, blue: 0.42, alpha: 1)),
        ("Then System Settings → Privacy & Security, scroll down, and click Open Anyway.",
         NSFont.systemFont(ofSize: 14, weight: .regular),
         NSColor(white: 1, alpha: 0.88)),
        ("Confirm with your password or Touch ID, then click Open Anyway again.",
         NSFont.systemFont(ofSize: 14, weight: .regular),
         NSColor(white: 1, alpha: 0.88))
    ]
    var y: CGFloat = 266
    for (text, font, color) in lines {
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color]
        (text as NSString).draw(at: NSPoint(x: 48, y: y), withAttributes: attrs)
        y += font.pointSize + 12
    }
    return true
}

guard let tiff = image.tiffRepresentation,
      let rep = NSBitmapImageRep(data: tiff),
      let png = rep.representation(using: .png, properties: [:]) else {
    fputs("Could not draw the disk image background\n", stderr)
    exit(1)
}
do {
    try png.write(to: URL(fileURLWithPath: output))
} catch {
    fputs("Could not write \(output): \(error)\n", stderr)
    exit(1)
}
