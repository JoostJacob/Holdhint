import AppKit
import CoreGraphics

if CommandLine.arguments.contains("--preflight-listen") {
    let allowed = CGPreflightListenEventAccess()
    fputs(allowed ? "yes\n" : "no\n", stdout)
    fflush(stdout)
    exit(allowed ? 0 : 2)
}

let application = NSApplication.shared
let delegate = AppDelegate()
application.delegate = delegate
application.setActivationPolicy(.accessory)
application.run()
