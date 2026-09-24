import AppKit
import Carbon
import Vision

private enum HotKeyID: UInt32 {
    case screenshot = 1
    case extractText = 2
}

private enum OcrOutcome {
    case copied(String)
    case empty
    case cancelled
    case failed(Error)
}

final class ScreenshotApp: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private var screenshotHotKeyRef: EventHotKeyRef?
    private var ocrHotKeyRef: EventHotKeyRef?
    private var eventHandlerRef: EventHandlerRef?
    private var isCapturing = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        setupStatusItem()
        installHotKeyHandler()
        registerHotKey(id: .screenshot, modifiers: UInt32(optionKey))
        registerHotKey(id: .extractText, modifiers: UInt32(optionKey | shiftKey))
    }

    func applicationWillTerminate(_ notification: Notification) {
        if let screenshotHotKeyRef {
            UnregisterEventHotKey(screenshotHotKeyRef)
        }
        if let ocrHotKeyRef {
            UnregisterEventHotKey(ocrHotKeyRef)
        }
        if let eventHandlerRef {
            RemoveEventHandler(eventHandlerRef)
        }
    }

    private func setupStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.title = "截屏"
        item.button?.toolTip = "Option + A 截图"

        let menu = NSMenu()
        menu.addItem(NSMenuItem(title: "Option + A 截图", action: #selector(capture), keyEquivalent: ""))
        menu.addItem(NSMenuItem(title: "Option + Shift + A 提取文字", action: #selector(extractText), keyEquivalent: ""))
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "退出", action: #selector(quit), keyEquivalent: "q"))
        item.menu = menu
        statusItem = item
    }

    private func installHotKeyHandler() {
        var eventType = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let selfPointer = UnsafeMutableRawPointer(Unmanaged.passUnretained(self).toOpaque())

        let handler: EventHandlerUPP = { _, event, userData in
            guard let userData else {
                return noErr
            }

            var hotKeyID = EventHotKeyID()
            let status = GetEventParameter(
                event,
                EventParamName(kEventParamDirectObject),
                EventParamType(typeEventHotKeyID),
                nil,
                MemoryLayout<EventHotKeyID>.size,
                nil,
                &hotKeyID
            )
            guard status == noErr,
                  hotKeyID.signature == OSType(0x57535348),
                  let action = HotKeyID(rawValue: hotKeyID.id) else {
                return noErr
            }

            let app = Unmanaged<ScreenshotApp>.fromOpaque(userData).takeUnretainedValue()
            DispatchQueue.main.async {
                switch action {
                case .screenshot:
                    app.capture()
                case .extractText:
                    app.extractText()
                }
            }
            return noErr
        }

        InstallEventHandler(
            GetApplicationEventTarget(),
            handler,
            1,
            &eventType,
            selfPointer,
            &eventHandlerRef
        )
    }

    private func registerHotKey(id: HotKeyID, modifiers: UInt32) {
        var hotKeyRef: EventHotKeyRef?
        let hotKeyID = EventHotKeyID(signature: OSType(0x57535348), id: id.rawValue)
        let status = RegisterEventHotKey(
            UInt32(kVK_ANSI_A),
            modifiers,
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &hotKeyRef
        )

        guard status == noErr else {
            showStatus("快捷键注册失败")
            return
        }

        switch id {
        case .screenshot:
            screenshotHotKeyRef = hotKeyRef
        case .extractText:
            ocrHotKeyRef = hotKeyRef
        }
    }

    @objc private func capture() {
        runScreenshot(arguments: ["-i", "-c"])
    }

    @objc private func extractText() {
        guard !isCapturing else {
            return
        }
        isCapturing = true

        DispatchQueue.global(qos: .userInitiated).async {
            let outcome = self.captureAndRecognizeText()
            DispatchQueue.main.async {
                self.isCapturing = false
                switch outcome {
                case .copied(let text):
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(text, forType: .string)
                    self.showStatus("文字已提取并复制")
                case .empty:
                    self.showStatus("未识别到文字")
                case .cancelled:
                    break
                case .failed:
                    self.showStatus("文字提取失败")
                }
            }
        }
    }

    private func runScreenshot(arguments: [String]) {
        guard !isCapturing else {
            return
        }
        isCapturing = true

        DispatchQueue.global(qos: .userInitiated).async {
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
            process.arguments = arguments

            do {
                try process.run()
                process.waitUntilExit()
                DispatchQueue.main.async {
                    self.isCapturing = false
                    if process.terminationStatus != 0 {
                        self.showStatus("截图失败")
                    }
                }
            } catch {
                DispatchQueue.main.async {
                    self.isCapturing = false
                    self.showStatus("截图启动失败")
                }
            }
        }
    }

    private func captureAndRecognizeText() -> OcrOutcome {
        let imageURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("WechatStyleScreenshot-\(UUID().uuidString).png")
        defer { try? FileManager.default.removeItem(at: imageURL) }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
        process.arguments = ["-i", imageURL.path]

        do {
            try process.run()
            process.waitUntilExit()
            guard process.terminationStatus == 0,
                  FileManager.default.fileExists(atPath: imageURL.path),
                  let attributes = try? FileManager.default.attributesOfItem(atPath: imageURL.path),
                  let fileSize = attributes[.size] as? NSNumber,
                  fileSize.intValue > 0 else {
                return .cancelled
            }

            let text = try recognizeText(from: imageURL)
                .components(separatedBy: .newlines)
                .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
                .filter { !$0.isEmpty }
                .joined(separator: "\n")
            return text.isEmpty ? .empty : .copied(text)
        } catch {
            return .failed(error)
        }
    }

    private func recognizeText(from imageURL: URL) throws -> String {
        let request = VNRecognizeTextRequest()
        request.recognitionLevel = .accurate
        request.usesLanguageCorrection = true

        let availableLanguages = try request.supportedRecognitionLanguages()
        let preferredLanguages = ["zh-Hans", "en-US"].filter(availableLanguages.contains)
        request.recognitionLanguages = preferredLanguages.isEmpty ? availableLanguages : preferredLanguages

        let handler = VNImageRequestHandler(url: imageURL)
        try handler.perform([request])

        let observations = request.results ?? []
        return observations
            .sorted {
                if abs($0.boundingBox.midY - $1.boundingBox.midY) > 0.015 {
                    return $0.boundingBox.midY > $1.boundingBox.midY
                }
                return $0.boundingBox.minX < $1.boundingBox.minX
            }
            .compactMap { $0.topCandidates(1).first?.string }
            .joined(separator: "\n")
    }

    private func showStatus(_ message: String) {
        statusItem?.button?.title = message
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) { [weak self] in
            self?.statusItem?.button?.title = "截屏"
        }
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}

let app = NSApplication.shared
let delegate = ScreenshotApp()
app.delegate = delegate
app.run()
