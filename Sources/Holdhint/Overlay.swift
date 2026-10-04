import AppKit

struct HUDRow {
    var key: String
    var title: String
    var note: String?
}

struct HUDContent {
    var symbols: [String]
    var subtitle: String
    var rows: [HUDRow]
}

final class HUDPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    init() {
        super.init(
            contentRect: NSRect(x: 0, y: 0, width: 480, height: 200),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        isFloatingPanel = true
        level = .popUpMenu
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        hidesOnDeactivate = false
        isOpaque = false
        backgroundColor = .clear
        hasShadow = true
        ignoresMouseEvents = true
        isReleasedWhenClosed = false
        animationBehavior = .none
        worksWhenModal = true
    }
}

final class OverlayController {
    let panel = HUDPanel()
    private let view = HUDView()
    private var onScreen = false
    private var token = 0

    init() {
        panel.contentView = view
    }

    func present(_ content: HUDContent) {
        token += 1
        guard let screen = Self.screenUnderMouse() else { return }
        let visible = screen.visibleFrame
        let maxSize = NSSize(width: max(320, visible.width - 48), height: max(160, visible.height - 36))
        let size = view.update(content, maxSize: maxSize)
        var origin = NSPoint(x: visible.midX - size.width / 2, y: visible.midY - size.height / 2)
        origin.x = min(max(origin.x, visible.minX + 12), visible.maxX - size.width - 12)
        origin.y = min(max(origin.y, visible.minY + 12), visible.maxY - size.height - 12)
        panel.setContentSize(size)
        panel.setFrameOrigin(origin)
        view.needsDisplay = true

        if onScreen {
            panel.alphaValue = 1
            panel.orderFrontRegardless()
            return
        }
        onScreen = true
        panel.alphaValue = 0
        panel.orderFrontRegardless()
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.12
            self.panel.animator().alphaValue = 1
        }
    }

    var isShowing: Bool { onScreen }

    func dismiss() {
        guard onScreen else { return }
        // Order out immediately. The fade-out completion did not always run,
        // and a panel that ignores clicks then had no way to leave the screen.
        onScreen = false
        token += 1
        panel.orderOut(nil)
        panel.alphaValue = 1
    }

    func writePNG(to url: URL) throws {
        guard let data = view.pngData() else {
            throw SnapshotError.failed
        }
        try data.write(to: url, options: .atomic)
    }

    static func screenUnderMouse() -> NSScreen? {
        let mouse = NSEvent.mouseLocation
        return NSScreen.screens.first { $0.frame.contains(mouse) }
            ?? NSScreen.main
            ?? NSScreen.screens.first
    }
}

enum SnapshotError: Error {
    case failed
}

private final class HUDView: NSView {
    override var isFlipped: Bool { true }
    override var isOpaque: Bool { false }

    private var layout = HUDLayout.empty

    func update(_ content: HUDContent, maxSize: NSSize) -> NSSize {
        layout = HUDLayout.make(content: content, maxSize: maxSize)
        frame = NSRect(origin: .zero, size: layout.size)
        needsDisplay = true
        return layout.size
    }

    override func draw(_ dirtyRect: NSRect) {
        layout.draw(in: bounds)
    }

    func pngData() -> Data? {
        if let rep = bitmapImageRepForCachingDisplay(in: bounds) {
            cacheDisplay(in: bounds, to: rep)
            if let png = rep.representation(using: .png, properties: [:]), png.count > 500 {
                return png
            }
        }
        let image = NSImage(size: bounds.size, flipped: true) { [weak self] rect in
            self?.draw(rect)
            return true
        }
        guard let tiff = image.tiffRepresentation,
              let rep = NSBitmapImageRep(data: tiff) else { return nil }
        return rep.representation(using: .png, properties: [:])
    }
}

private struct HUDLayout {
    var size: NSSize
    var symbols: [String]
    var symbolFrames: [NSRect]
    var brandRect: NSRect
    var subtitle: String
    var subtitleRect: NSRect
    var divider: NSRect?
    var rows: [PlacedRow]

    struct PlacedRow {
        var pill: NSRect
        var key: String
        var titleRect: NSRect
        var title: String
        var noteRect: NSRect?
        var note: String?
    }

    static let empty = HUDLayout(
        size: NSSize(width: 420, height: 120),
        symbols: [],
        symbolFrames: [],
        brandRect: .zero,
        subtitle: "",
        subtitleRect: .zero,
        divider: nil,
        rows: []
    )

    static func make(content: HUDContent, maxSize: NSSize) -> HUDLayout {
        var omitted = 0
        let source = content.rows
        while true {
            let shown: [HUDRow]
            if omitted == 0 {
                shown = source
            } else {
                let keep = max(0, source.count - omitted)
                shown = Array(source.prefix(keep)) + [HUDRow(key: "…", title: "\(omitted) more", note: nil)]
            }
            let layout = compute(symbols: content.symbols, subtitle: content.subtitle, rows: shown, maxSize: maxSize)
            if layout.size.height <= maxSize.height + 0.5 || shown.count <= 2 || omitted >= source.count {
                return layout
            }
            omitted += 1
        }
    }

    private static func compute(symbols: [String], subtitle: String, rows: [HUDRow], maxSize: NSSize) -> HUDLayout {
        let metrics = Metrics()
        let padX: CGFloat = 28
        let padTop: CGFloat = 22
        let padBottom: CGFloat = 20
        let colGap: CGFloat = 36
        let keyGap: CGFloat = 16
        let minWidth: CGFloat = 420

        let symbolFont = NSFont.systemFont(ofSize: metrics.symbol, weight: .medium)
        let brandFont = NSFont.systemFont(ofSize: metrics.brand, weight: .semibold)
        let keyFont = NSFont.monospacedSystemFont(ofSize: metrics.key, weight: .semibold)
        let titleFont = NSFont.systemFont(ofSize: metrics.title, weight: .medium)
        let noteFont = NSFont.systemFont(ofSize: metrics.note, weight: .regular)

        let symbolAttrs: [NSAttributedString.Key: Any] = [.font: symbolFont, .foregroundColor: Palette.text]
        let brandAttrs: [NSAttributedString.Key: Any] = [.font: brandFont, .foregroundColor: Palette.brand]
        let keyAttrs: [NSAttributedString.Key: Any] = [.font: keyFont, .foregroundColor: Palette.text]
        let titleAttrs = wrappingAttributes(font: titleFont, color: Palette.text)
        let noteAttrs = wrappingAttributes(font: noteFont, color: Palette.secondary)

        let brand = "Holdhint" as NSString
        let brandSize = brand.size(withAttributes: brandAttrs)
        var symbolsWidth: CGFloat = 0
        var symbolSizes: [NSSize] = []
        for symbol in symbols {
            let size = (symbol as NSString).size(withAttributes: symbolAttrs)
            symbolSizes.append(size)
            symbolsWidth += ceil(size.width)
        }
        if symbols.count > 1 {
            symbolsWidth += CGFloat(symbols.count - 1) * 16
        }
        let symbolHeight = ceil(symbolSizes.map(\.height).max() ?? 36)
        let headerMin = padX + symbolsWidth + 24 + ceil(brandSize.width) + padX

        var keyWidth: CGFloat = 52
        for row in rows {
            let width = ceil((row.key as NSString).size(withAttributes: keyAttrs).width) + 22
            keyWidth = max(keyWidth, min(160, width))
        }

        let naturalTitle = widestLine(rows: rows, titleFont: titleFont, noteFont: noteFont, cap: 480)
        let naturalColumn = keyWidth + keyGap + naturalTitle

        func layout(columns: Int) -> HUDLayout {
            let gaps = CGFloat(max(0, columns - 1)) * colGap
            let natural = naturalColumn * CGFloat(columns) + gaps + padX * 2
            let width = min(maxSize.width, max(minWidth, max(headerMin, natural)))
            let columnWidth = (width - padX * 2 - gaps) / CGFloat(columns)
            let titleWidth = max(80, columnWidth - keyWidth - keyGap)

            var placed: [[PlacedRow]] = Array(repeating: [], count: columns)
            var heights = Array(repeating: CGFloat(0), count: columns)
            let perColumn = Int(ceil(Double(rows.count) / Double(columns)))
            for (index, row) in rows.enumerated() {
                let column = min(columns - 1, index / max(perColumn, 1))
                let titleHeight = measure(row.title, width: titleWidth, attributes: titleAttrs).height
                var noteHeight: CGFloat = 0
                if let note = row.note, !note.isEmpty {
                    noteHeight = measure(note, width: titleWidth, attributes: noteAttrs).height

                }
                let block = titleHeight + (noteHeight > 0 ? 3 + noteHeight : 0)
                let rowHeight = max(44, block + 14)
                let pillHeight: CGFloat = 36
                let y = heights[column]
                let pill = NSRect(x: 0, y: y + (rowHeight - pillHeight) / 2, width: keyWidth, height: pillHeight)
                let titleRect = NSRect(x: 0, y: y + (rowHeight - block) / 2, width: titleWidth, height: titleHeight)
                var noteRect: NSRect?
                if noteHeight > 0 {
                    noteRect = NSRect(
                        x: 0,
                        y: titleRect.maxY + 3,
                        width: titleWidth,
                        height: noteHeight
                    )
                }
                placed[column].append(PlacedRow(
                    pill: pill,
                    key: row.key,
                    titleRect: titleRect,
                    title: row.title,
                    noteRect: noteRect,
                    note: row.note
                ))
                heights[column] = y + rowHeight + 4
            }

            let subtitleHeight: CGFloat = 22
            let headerBottom = padTop + symbolHeight + 8 + subtitleHeight + 28
            let bodyHeight = heights.max() ?? 0
            let height = headerBottom + bodyHeight + padBottom
            var symbolFrames: [NSRect] = []
            var x = padX
            for size in symbolSizes {
                let frame = NSRect(
                    x: x,
                    y: padTop + (symbolHeight - size.height) / 2,
                    width: ceil(size.width),
                    height: ceil(size.height)
                )
                symbolFrames.append(frame)
                x = frame.maxX + 16
            }
            let brandRect = NSRect(
                x: width - padX - ceil(brandSize.width),
                y: padTop + (symbolHeight - brandSize.height) / 2,
                width: ceil(brandSize.width),
                height: ceil(brandSize.height)
            )
            let subtitleRect = NSRect(x: padX, y: padTop + symbolHeight + 8, width: width - padX * 2, height: 22)
            let divider = rows.isEmpty ? nil : NSRect(x: padX, y: subtitleRect.maxY + 14, width: width - padX * 2, height: 1)

            var absolute: [PlacedRow] = []
            for column in 0..<columns {
                let originX = padX + CGFloat(column) * (columnWidth + colGap)
                let originY = headerBottom
                for row in placed[column] {
                    absolute.append(PlacedRow(
                        pill: row.pill.offsetBy(dx: originX, dy: originY),
                        key: row.key,
                        titleRect: row.titleRect.offsetBy(dx: originX + keyWidth + keyGap, dy: originY),
                        title: row.title,
                        noteRect: row.noteRect?.offsetBy(dx: originX + keyWidth + keyGap, dy: originY),
                        note: row.note
                    ))
                }
            }

            return HUDLayout(
                size: NSSize(width: ceil(width), height: ceil(max(120, height))),
                symbols: symbols,
                symbolFrames: symbolFrames,
                brandRect: brandRect,
                subtitle: subtitle,
                subtitleRect: subtitleRect,
                divider: divider,
                rows: absolute
            )
        }

        for columns in 1...3 {
            let candidate = layout(columns: columns)
            if candidate.size.height <= maxSize.height + 0.5 {
                return candidate
            }
        }
        return layout(columns: 3)
    }

    func draw(in bounds: NSRect) {
        let box = bounds.insetBy(dx: 1, dy: 1)
        let path = NSBezierPath(roundedRect: box, xRadius: 20, yRadius: 20)
        Palette.background.setFill()
        path.fill()
        Palette.stroke.setStroke()
        path.lineWidth = 1
        path.stroke()

        let symbolFont = NSFont.systemFont(ofSize: 34, weight: .medium)
        let symbolAttrs: [NSAttributedString.Key: Any] = [.font: symbolFont, .foregroundColor: Palette.text]
        for (symbol, frame) in zip(symbols, symbolFrames) {
            (symbol as NSString).draw(in: frame, withAttributes: symbolAttrs)
        }

        let brandAttrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 13, weight: .semibold),
            .foregroundColor: Palette.brand
        ]
        ("Holdhint" as NSString).draw(in: brandRect, withAttributes: brandAttrs)

        let subtitleAttrs = Self.wrappingAttributes(
            font: NSFont.systemFont(ofSize: 15, weight: .regular),
            color: Palette.secondary
        )
        (subtitle as NSString).draw(in: subtitleRect, withAttributes: subtitleAttrs)

        if let divider {
            Palette.divider.setFill()
            NSRect(x: divider.minX, y: divider.minY, width: divider.width, height: 1).fill()
        }

        let keyFont = NSFont.monospacedSystemFont(ofSize: 22, weight: .semibold)
        let titleFont = NSFont.systemFont(ofSize: 22, weight: .medium)
        let noteFont = NSFont.systemFont(ofSize: 16, weight: .regular)
        let keyStyle = NSMutableParagraphStyle()
        keyStyle.alignment = .center
        let keyAttrs: [NSAttributedString.Key: Any] = [
            .font: keyFont,
            .foregroundColor: Palette.text,
            .paragraphStyle: keyStyle
        ]
        let titleAttrs = Self.wrappingAttributes(font: titleFont, color: Palette.text)
        let noteAttrs = Self.wrappingAttributes(font: noteFont, color: Palette.secondary)

        for row in rows {
            let pill = NSBezierPath(roundedRect: row.pill, xRadius: 10, yRadius: 10)
            Palette.pill.setFill()
            pill.fill()
            let keySize = (row.key as NSString).size(withAttributes: keyAttrs)
            let keyRect = NSRect(
                x: row.pill.minX,
                y: row.pill.midY - keySize.height / 2,
                width: row.pill.width,
                height: keySize.height
            )
            (row.key as NSString).draw(in: keyRect, withAttributes: keyAttrs)
            NSAttributedString(string: row.title, attributes: titleAttrs).draw(
                with: row.titleRect,
                options: [.usesLineFragmentOrigin, .usesFontLeading],
                context: nil
            )
            if let note = row.note, let noteRect = row.noteRect {
                NSAttributedString(string: note, attributes: noteAttrs).draw(
                    with: noteRect,
                    options: [.usesLineFragmentOrigin, .usesFontLeading],
                    context: nil
                )
            }
        }
    }

    private static func wrappingAttributes(font: NSFont, color: NSColor) -> [NSAttributedString.Key: Any] {
        let style = NSMutableParagraphStyle()
        style.lineBreakMode = .byWordWrapping
        style.alignment = .left
        return [.font: font, .foregroundColor: color, .paragraphStyle: style]
    }

    private static func measure(_ text: String, width: CGFloat, attributes: [NSAttributedString.Key: Any]) -> NSSize {
        // Measure a few points narrower than the draw rect. boundingRect sometimes
        // reports one line for a string that draw(in:) then wraps, which clips the rest.
        let limit = max(40, width - 8)
        let rect = (text as NSString).boundingRect(
            with: NSSize(width: limit, height: 2000),
            options: [.usesLineFragmentOrigin, .usesFontLeading],
            attributes: attributes
        )
        var height = ceil(rect.height)
        let singleLine = ceil((text as NSString).size(withAttributes: attributes).width)
        if singleLine > limit {
            let font = (attributes[.font] as? NSFont) ?? NSFont.systemFont(ofSize: 16)
            let line = ceil(font.ascender - font.descender + max(1, font.leading))
            if height < line * 1.6 {
                height = line * 2
            }
        }
        return NSSize(width: ceil(min(rect.width, width)), height: height + 1)
    }

    private static func widestLine(rows: [HUDRow], titleFont: NSFont, noteFont: NSFont, cap: CGFloat) -> CGFloat {
        let titleAttrs: [NSAttributedString.Key: Any] = [.font: titleFont]
        let noteAttrs: [NSAttributedString.Key: Any] = [.font: noteFont]
        var width: CGFloat = 220
        for row in rows {
            width = max(width, min(cap, ceil((row.title as NSString).size(withAttributes: titleAttrs).width)))
            if let note = row.note {
                width = max(width, min(cap, ceil((note as NSString).size(withAttributes: noteAttrs).width)))
            }
        }
        return width
    }
}

private struct Metrics {
    var symbol: CGFloat = 34
    var key: CGFloat = 22
    var title: CGFloat = 22
    var note: CGFloat = 16
    var brand: CGFloat = 13
}

private enum Palette {
    static let background = NSColor(srgbRed: 0.07, green: 0.08, blue: 0.10, alpha: 0.90)
    static let stroke = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.14)
    static let text = NSColor(srgbRed: 0.98, green: 0.98, blue: 0.97, alpha: 1)
    static let secondary = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.68)
    static let pill = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.14)
    static let brand = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.42)
    static let divider = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.14)
}

