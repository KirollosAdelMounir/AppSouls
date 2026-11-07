#!/usr/bin/env python3
"""App Souls native Unix scanner dependency installation (no web-process elevation)."""
import argparse
import dataclasses
import json
import os
import pathlib
import shutil
import socket
import subprocess
import sys
import tempfile
import time

PORT = 3310
STREAM_LIMIT = 512 * 1024 * 1024
MARKER = "# App Souls managed scanner settings"


@dataclasses.dataclass(frozen=True)
class Plan:
    platform: str
    packages: tuple
    daemon_config: str
    update_config: str
    database: str
    scanner_user: str
    updater_user: str
    daemon_service: str
    updater_service: str
    bsd: bool = False


def plan_for(system, distro=""):
    if system == "FreeBSD":
        return Plan("FreeBSD", (("pkg", "install", "-y", "clamav"),), "/usr/local/etc/clamd.conf", "/usr/local/etc/freshclam.conf", "/var/db/clamav", "clamav", "clamav", "clamav_clamd", "clamav_freshclam", True)
    if system == "Linux" and distro in ("debian", "ubuntu"):
        return Plan(distro, (("apt-get", "update"), ("apt-get", "install", "-y", "clamav", "clamav-daemon", "clamav-freshclam")), "/etc/clamav/clamd.conf", "/etc/clamav/freshclam.conf", "/var/lib/clamav", "clamav", "clamav", "clamav-daemon", "clamav-freshclam")
    if system == "Linux" and distro == "fedora":
        return Plan(distro, (("dnf", "install", "-y", "clamav", "clamd", "clamav-freshclam"),), "/etc/clamd.d/scan.conf", "/etc/freshclam.conf", "/var/lib/clamav", "clamscan", "clamupdate", "clamd@scan", "clamav-freshclam")
    raise RuntimeError("No bundled automatic scanner installer is available for this OS family. Use an administrator-managed local ClamAV service.")


def daemon_values(plan):
    return {"TCPAddr": "127.0.0.1", "TCPSocket": str(PORT), "DatabaseDirectory": plan.database,
            "User": plan.scanner_user, "StreamMaxLength": str(STREAM_LIMIT), "MaxFileSize": str(STREAM_LIMIT),
            "MaxScanSize": str(2 * STREAM_LIMIT), "MaxThreads": "2", "MaxQueue": "8",
            "AlertExceedsMax": "yes", "AlertEncrypted": "yes"}


def patch_text(original, values):
    """Replace only owned directives; retain other vendor/operator settings and comments."""
    owned = {name.casefold() for name in values}
    lines = []
    for line in original.splitlines():
        stripped = line.strip()
        if stripped == MARKER:
            continue
        if stripped and not stripped.startswith("#") and stripped.split()[0].casefold() in owned | {"example"}:
            continue
        lines.append(line)
    return "\n".join(lines).rstrip() + "\n" + MARKER + "\n" + "\n".join(f"{name} {value}" for name, value in values.items()) + "\n"


def patch_file(path, values):
    path = pathlib.Path(path)
    if path.is_symlink():
        raise RuntimeError(f"Refusing to replace a symlinked scanner configuration: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    source = path
    if not source.exists():
        source = next((p for p in (pathlib.Path(str(path) + ".sample"), pathlib.Path(str(path) + ".example")) if p.is_file() and not p.is_symlink()), path)
    original = source.read_text() if source.exists() else ""
    updated = patch_text(original, values)
    if path.exists() and updated == original:
        return
    previous = path.stat() if path.exists() else None
    if previous:
        backup = pathlib.Path(str(path) + ".appsouls-backup-" + str(time.time_ns()))
        with open(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "wb") as stream:
            stream.write(original.encode())
    descriptor, temporary = tempfile.mkstemp(prefix=".appsouls-", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w") as stream:
            stream.write(updated)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, previous.st_mode & 0o777 if previous else 0o644)
        if previous and os.geteuid() == 0:
            os.chown(temporary, previous.st_uid, previous.st_gid)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def ping(port=PORT):
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=3) as client:
            client.settimeout(3)
            client.sendall(b"zPING\0")
            reply = bytearray()
            while len(reply) < 128:
                item = client.recv(1)
                if not item or item == b"\0":
                    break
                reply.extend(item)
            return bytes(reply).strip(b"\r\n") == b"PONG"
    except OSError:
        return False


def run(command, check=True):
    # Arguments come from the reviewed platform plan, never shell interpolation or browser inputs.
    return subprocess.run(list(command), check=check)


def install(plan, configure_existing=False):
    if ping() and not configure_existing:
        print("ClamAV already responds on localhost:3310. Existing scanner configuration and updater are retained; no changes made.")
        return
    if os.geteuid() != 0:
        raise RuntimeError("Run the server dependency installer as root to install/configure ClamAV. The App Souls web process must remain unprivileged.")
    if not plan.bsd and not shutil.which("systemctl"):
        raise RuntimeError("The native Linux installer requires systemd. Supply a separately managed local scanner in other deployment environments.")
    for command in plan.packages:
        run(command)
    update = pathlib.Path(plan.update_config)
    daemon = pathlib.Path(plan.daemon_config)
    patch_file(daemon, daemon_values(plan))
    patch_file(update, {"DatabaseDirectory": plan.database, "DatabaseOwner": plan.updater_user, "DatabaseMirror": "database.clamav.net", "Checks": "12"})
    if shutil.which("restorecon"):
        run(("restorecon", "-F", str(daemon), str(update)))
    if plan.bsd:
        run(("sysrc", f"{plan.daemon_service}_enable=YES", f"{plan.updater_service}_enable=YES"))
        stop_update = ("service", plan.updater_service, "onestop")
        start_update = ("service", plan.updater_service, "start")
    else:
        run(("systemctl", "enable", plan.daemon_service, plan.updater_service))
        stop_update = ("systemctl", "stop", plan.updater_service)
        start_update = ("systemctl", "start", plan.updater_service)
    run(stop_update, check=False)
    try:
        print("Updating virus definitions before starting the scanner…", flush=True)
        run(("freshclam", "--config-file=" + str(update)))
    finally:
        # An update failure must not leave a previously enabled updater deliberately stopped.
        run(start_update, check=False)
    run(start_update)
    if plan.bsd:
        run(("service", plan.daemon_service, "restart"))
    else:
        run(("systemctl", "restart", plan.daemon_service))
    deadline = time.monotonic() + 120
    while time.monotonic() < deadline:
        if ping():
            print("ClamAV is responding locally. App Souls connects automatically; no integration entry is required. Each package still requires a clean scan.")
            return
        time.sleep(1)
    raise RuntimeError("Scanner installation did not become ready. Check the daemon and signature updater services; publishing remains blocked.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configure-existing", action="store_true", help="Apply the managed configuration to an existing package-managed scanner (requires root)")
    parser.add_argument("--check", action="store_true", help="Check the local daemon without making any changes")
    parser.add_argument("--plan", choices=("fedora", "debian", "ubuntu", "freebsd"), help="Print a platform plan without making changes")
    args = parser.parse_args()
    if args.check:
        ready = ping()
        print("ClamAV responding on localhost:3310" if ready else "ClamAV unavailable on localhost:3310")
        return 0 if ready else 1
    if args.plan:
        plan = plan_for("FreeBSD" if args.plan == "freebsd" else "Linux", args.plan)
        print(json.dumps(dataclasses.asdict(plan), indent=2))
        return 0
    system = os.uname().sysname
    distro = ""
    if system == "Linux":
        for line in pathlib.Path("/etc/os-release").read_text().splitlines():
            if line.startswith("ID="):
                distro = line[3:].strip('"')
    install(plan_for(system, distro), args.configure_existing)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print(f"Scanner dependency setup failed: {error}", file=sys.stderr)
        sys.exit(1)
