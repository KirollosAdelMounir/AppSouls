#!/bin/sh
# Run this before deploying App Souls. It never reads or writes the application vault.
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if ! command -v python3 >/dev/null 2>&1; then
    if [ "$#" -gt 0 ] && [ "$1" != "--configure-existing" ]; then echo "Python 3 is required for read-only checks/plans. No changes made." >&2; exit 1; fi
    if [ "$(id -u)" -ne 0 ]; then
        echo "Python 3 is required. Run this installer as root to install server dependencies." >&2
        exit 1
    fi
    case "$(uname -s)" in
        FreeBSD) pkg install -y python3 ;;
        Linux)
            if command -v dnf >/dev/null 2>&1; then dnf install -y python3
            elif command -v apt-get >/dev/null 2>&1; then apt-get update && apt-get install -y python3
            else echo "Unsupported native package manager." >&2; exit 1; fi ;;
        *) echo "Use the Windows PowerShell dependency installer or a managed local scanner." >&2; exit 1 ;;
    esac
fi
exec python3 "$script_dir/scanner_setup.py" "$@"
