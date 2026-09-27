import CoreGraphics
import Testing
@testable import Tatami

struct NextScreenTests {
    let small = CGRect(x: 0, y: 0, width: 1000, height: 800)
    let large = CGRect(x: 1000, y: 0, width: 2000, height: 1600)

    @Test func leftHalfStaysTheLeftHalf() {
        let leftHalf = CGRect(x: 0, y: 0, width: 500, height: 800)
        let moved = WindowMover.relativeFrame(leftHalf, from: small, to: large)
        #expect(moved == CGRect(x: 1000, y: 0, width: 1000, height: 1600))
    }

    @Test func movingBackRestoresTheOriginalFrame() {
        let window = CGRect(x: 100, y: 200, width: 300, height: 400)
        let there = WindowMover.relativeFrame(window, from: small, to: large)
        let back = WindowMover.relativeFrame(there, from: large, to: small)
        #expect(back == window)
    }

    @Test func windowIsKeptInsideTheTargetScreen() {
        // 目標螢幕比較矮，而且換算後會超出上緣
        let short = CGRect(x: 1000, y: 0, width: 1000, height: 400)
        let tall = CGRect(x: 0, y: 700, width: 1000, height: 800)
        let moved = WindowMover.relativeFrame(tall, from: CGRect(x: 0, y: 0, width: 1000, height: 1500), to: short)
        #expect(short.contains(moved))
    }
}

struct SnapZoneTests {
    let screen = CGRect(x: 0, y: 0, width: 1440, height: 900)

    @Test func leftEdgeSnapsToLeftHalf() {
        #expect(SnapController.zone(at: CGPoint(x: 2, y: 400), in: screen) == .leftHalf)
    }

    @Test func rightEdgeSnapsToRightHalf() {
        #expect(SnapController.zone(at: CGPoint(x: 1438, y: 400), in: screen) == .rightHalf)
    }

    @Test func topEdgeMaximizes() {
        // Cocoa 座標：頂端是 maxY
        #expect(SnapController.zone(at: CGPoint(x: 700, y: 898), in: screen) == .maximize)
    }

    @Test func middleOfTheScreenDoesNothing() {
        #expect(SnapController.zone(at: CGPoint(x: 700, y: 400), in: screen) == nil)
    }

    @Test func topCornerPrefersTheSideEdge() {
        #expect(SnapController.zone(at: CGPoint(x: 1, y: 899), in: screen) == .leftHalf)
    }

    @Test func worksOnASecondaryScreen() {
        // 主螢幕右邊的第二個螢幕，座標不是從 0 開始
        let secondary = CGRect(x: 1440, y: -200, width: 1920, height: 1080)
        #expect(SnapController.zone(at: CGPoint(x: 1442, y: 300), in: secondary) == .leftHalf)
        #expect(SnapController.zone(at: CGPoint(x: 2000, y: 878), in: secondary) == .maximize)
    }
}
