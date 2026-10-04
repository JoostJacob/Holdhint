#!/bin/bash
# Run the logic checks. Does not need the Accessibility or Input Monitoring permission.
set -euo pipefail
cd "$(dirname "$0")/.."
swift run HoldhintChecks
