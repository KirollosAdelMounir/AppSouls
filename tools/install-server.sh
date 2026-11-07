#!/bin/sh
# Entry point from a framework-dependent published server package.
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if ! command -v python3 >/dev/null 2>&1; then
    case " $* " in *' --plan '*|*' --help '*) echo 'Python 3 is required for a read-only plan. No changes made.' >&2; exit 1;; esac
    if [ "$(id -u)" -ne 0 ]; then echo 'Run the server installer with sudo or as root.' >&2; exit 1; fi
    if command -v dnf >/dev/null 2>&1; then dnf install -y python3
    elif command -v apt-get >/dev/null 2>&1; then apt-get update && apt-get install -y python3
    else echo 'Supported automatic hosts: Fedora, Debian and Ubuntu with systemd.' >&2; exit 1; fi
fi
exec python3 "$script_dir/server_setup.py" "$@"
