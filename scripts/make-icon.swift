// 產生 App 圖示：模擬真實榻榻米，以四疊半的風車排列（和選單列圖示相同）。
//
// 用法：swift scripts/make-icon.swift <輸出的 1024×1024 PNG 路徑>
// 通常不用直接執行，改用 ./scripts/make-icon.sh 產生 Resources/AppIcon.icns。
import AppKit

let size: CGFloat = 1024

// 和 TatamiIcon 相同的排列（3×3 單位，原點左上）
struct Mat {
    var rect: CGRect
    /// 長邊是否為水平方向（決定布邊和紋路的方向）
    var isHorizontal: Bool { rect.width > rect.height }
    var isHalf: Bool { rect.width == rect.height }
}
let mats = [
    Mat(rect: CGRect(x: 0, y: 0, width: 2, height: 1)),
    Mat(rect: CGRect(x: 2, y: 0, width: 1, height: 2)),
    Mat(rect: CGRect(x: 1, y: 2, width: 2, height: 1)),
    Mat(rect: CGRect(x: 0, y: 1, width: 1, height: 2)),
    Mat(rect: CGRect(x: 1, y: 1, width: 1, height: 1)),
]

func color(_ hex: UInt32, _ alpha: CGFloat = 1) -> CGColor {
    CGColor(
        srgbRed: CGFloat((hex >> 16) & 0xFF) / 255,
        green: CGFloat((hex >> 8) & 0xFF) / 255,
        blue: CGFloat(hex & 0xFF) / 255,
        alpha: alpha
    )
}

/// 固定亂數，每次產生的圖示都一樣
var seed: UInt64 = 0x7A7A_4D11
func random() -> CGFloat {
    seed = seed &* 6364136223846793005 &+ 1442695040888963407
    return CGFloat(seed >> 33) / CGFloat(1 << 31)
}

let space = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(
    data: nil, width: Int(size), height: Int(size), bitsPerComponent: 8, bytesPerRow: 0,
    space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
)!
// 改成左上原點，方便和排列資料對應
ctx.translateBy(x: 0, y: size)
ctx.scaleBy(x: 1, y: -1)

// MARK: - 底座：macOS 圖示標準的圓角方形（824×824，置中），深色木地板

let base = CGRect(x: 100, y: 100, width: 824, height: 824)
let basePath = CGPath(roundedRect: base, cornerWidth: 185, cornerHeight: 185, transform: nil)

ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: 12), blur: 28, color: color(0x000000, 0.35))
ctx.addPath(basePath)
ctx.setFillColor(color(0x3B342D))
ctx.fillPath()
ctx.restoreGState()

ctx.saveGState()
ctx.addPath(basePath)
ctx.clip()
ctx.drawLinearGradient(
    CGGradient(colorsSpace: space, colors: [color(0x4A4139), color(0x2C2621)] as CFArray, locations: [0, 1])!,
    start: CGPoint(x: 0, y: base.minY), end: CGPoint(x: 0, y: base.maxY), options: []
)
// 木地板的直向木紋
for i in 0..<14 {
    let x = base.minX + CGFloat(i) * base.width / 14
    ctx.setStrokeColor(color(0x000000, 0.18))
    ctx.setLineWidth(2)
    ctx.move(to: CGPoint(x: x, y: base.minY))
    ctx.addLine(to: CGPoint(x: x, y: base.maxY))
    ctx.strokePath()
}
ctx.restoreGState()

// MARK: - 榻榻米

let area = base.insetBy(dx: 92, dy: 92)
let unit = area.width / 3
let gap: CGFloat = 5          // 疊與疊之間的細縫
let heriWidth: CGFloat = 15   // 黑色布邊（縁）的寬度

// 整組榻榻米的陰影，讓它看起來鋪在地板上
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: 10), blur: 22, color: color(0x000000, 0.55))
ctx.setFillColor(color(0x1A1612))
ctx.fill(area)
ctx.restoreGState()

for mat in mats {
    let rect = CGRect(
        x: area.minX + mat.rect.minX * unit,
        y: area.minY + mat.rect.minY * unit,
        width: mat.rect.width * unit,
        height: mat.rect.height * unit
    ).insetBy(dx: gap / 2, dy: gap / 2)

    ctx.saveGState()
    ctx.clip(to: rect)

    // 藺草的底色，帶一點由上往下的明暗
    ctx.drawLinearGradient(
        CGGradient(colorsSpace: space, colors: [color(0xDCD3A0), color(0xC2B783)] as CFArray, locations: [0, 1])!,
        start: CGPoint(x: rect.minX, y: rect.minY), end: CGPoint(x: rect.maxX, y: rect.maxY), options: []
    )

    // 藺草紋路：細線與疊的短邊平行，深淺交錯
    let step: CGFloat = 6
    let length = mat.isHorizontal ? rect.width : rect.height
    var offset: CGFloat = 0
    var index = 0
    while offset < length {
        let shade = index % 2 == 0 ? color(0x8C8150, 0.16 + random() * 0.10) : color(0xF6EFC8, 0.12 + random() * 0.10)
        ctx.setStrokeColor(shade)
        ctx.setLineWidth(index % 2 == 0 ? 1.8 : 1.3)
        if mat.isHorizontal {
            ctx.move(to: CGPoint(x: rect.minX + offset, y: rect.minY))
            ctx.addLine(to: CGPoint(x: rect.minX + offset, y: rect.maxY))
        } else {
            ctx.move(to: CGPoint(x: rect.minX, y: rect.minY + offset))
            ctx.addLine(to: CGPoint(x: rect.maxX, y: rect.minY + offset))
        }
        ctx.strokePath()
        offset += step / 2
        index += 1
    }

    // 經線：沿著長邊方向的淡淡細線
    let warpCount = 4
    for i in 1..<warpCount {
        let t = CGFloat(i) / CGFloat(warpCount)
        ctx.setStrokeColor(color(0x7A6F42, 0.07))
        ctx.setLineWidth(1.2)
        if mat.isHorizontal {
            let y = rect.minY + rect.height * t
            ctx.move(to: CGPoint(x: rect.minX, y: y)); ctx.addLine(to: CGPoint(x: rect.maxX, y: y))
        } else {
            let x = rect.minX + rect.width * t
            ctx.move(to: CGPoint(x: x, y: rect.minY)); ctx.addLine(to: CGPoint(x: x, y: rect.maxY))
        }
        ctx.strokePath()
    }

    // 黑色布邊：整疊縫在兩條長邊上；半疊縫在上下兩邊
    let heriRects: [CGRect]
    if mat.isHorizontal || mat.isHalf {
        heriRects = [
            CGRect(x: rect.minX, y: rect.minY, width: rect.width, height: heriWidth),
            CGRect(x: rect.minX, y: rect.maxY - heriWidth, width: rect.width, height: heriWidth),
        ]
    } else {
        heriRects = [
            CGRect(x: rect.minX, y: rect.minY, width: heriWidth, height: rect.height),
            CGRect(x: rect.maxX - heriWidth, y: rect.minY, width: heriWidth, height: rect.height),
        ]
    }
    for heri in heriRects {
        ctx.setFillColor(color(0x15130F))
        ctx.fill(heri)
        // 布邊中間一條細細的反光
        ctx.setFillColor(color(0xFFFFFF, 0.08))
        if heri.width > heri.height {
            ctx.fill(CGRect(x: heri.minX, y: heri.midY - 1.5, width: heri.width, height: 3))
        } else {
            ctx.fill(CGRect(x: heri.midX - 1.5, y: heri.minY, width: 3, height: heri.height))
        }
    }

    // 疊面微微隆起：上方和左方亮一點，下方和右方暗一點
    ctx.drawLinearGradient(
        CGGradient(colorsSpace: space, colors: [color(0xFFFFFF, 0.10), color(0xFFFFFF, 0), color(0x000000, 0.10)] as CFArray, locations: [0, 0.5, 1])!,
        start: CGPoint(x: rect.minX, y: rect.minY), end: CGPoint(x: rect.minX, y: rect.maxY), options: []
    )

    // 每一疊的邊緣稍微壓暗，看起來有厚度
    ctx.setStrokeColor(color(0x000000, 0.25))
    ctx.setLineWidth(3)
    ctx.stroke(rect.insetBy(dx: 1.5, dy: 1.5))
    ctx.restoreGState()
}

// MARK: - 整體光線：左上亮、右下暗

ctx.saveGState()
ctx.addPath(basePath)
ctx.clip()
ctx.drawRadialGradient(
    CGGradient(colorsSpace: space, colors: [color(0xFFFFFF, 0.14), color(0xFFFFFF, 0)] as CFArray, locations: [0, 1])!,
    startCenter: CGPoint(x: base.minX + 180, y: base.minY + 160), startRadius: 0,
    endCenter: CGPoint(x: base.minX + 180, y: base.minY + 160), endRadius: 760, options: []
)
ctx.restoreGState()

// MARK: - 輸出

let output = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "icon_1024.png"
let rep = NSBitmapImageRep(cgImage: ctx.makeImage()!)
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: output))
print("Wrote \(output)")
