#!/usr/bin/env python3
"""Install a published App Souls server on native systemd Linux. Run --plan first for a read-only preview."""
import argparse
import dataclasses
import fcntl
import hashlib
import http.client
import ipaddress
import json
import os
import pathlib
import platform
import pwd
import re
import shutil
import socket
import ssl
import stat
import subprocess
import sys
import tarfile
import tempfile
import time
import urllib.parse
import urllib.request
import uuid

# Read-only plans must not create cache files in an extracted publish package.
sys.dont_write_bytecode = True
import scanner_setup

ROOT = pathlib.Path('/opt/AppSouls')
STATE = pathlib.Path('/var/lib/appsouls')
ETC = pathlib.Path('/etc/appsouls')
UNIT = pathlib.Path('/etc/systemd/system/appsouls.service')
SITE = pathlib.Path('/etc/nginx/conf.d/appsouls.conf')
USER = 'appsouls'
MARKER = '# App Souls managed hosting'
VENDOR_HOSTS = {'builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com', 'ci.dot.net'}


def run(args, **kwargs):
    return subprocess.run([str(x) for x in args], check=True, **kwargs)


def version(text):
    if not re.fullmatch(r'\d+\.\d+\.\d+', text):
        raise ValueError('Only stable .NET runtime versions are supported.')
    return tuple(map(int, text.split('.')))


def runtime_requirement(source, arch):
    source = pathlib.Path(source).resolve(strict=True)
    for name in ('TicketingCRM.dll', 'TicketingCRM.runtimeconfig.json', 'TicketingCRM.deps.json', 'wwwroot'):
        if not (source / name).exists():
            raise ValueError('Use the complete dotnet publish output, not source/build output: missing ' + name)
    for path in source.rglob('*'):
        if path.is_symlink() or not (path.is_file() or path.is_dir()):
            raise ValueError('Published input must contain regular files/directories without links.')
    options = json.loads((source / 'TicketingCRM.runtimeconfig.json').read_text())['runtimeOptions']
    frameworks = options.get('frameworks', [options['framework']] if 'framework' in options else [])
    required = {f['name']: version(f['version']) for f in frameworks}
    if set(required) != {'Microsoft.NETCore.App', 'Microsoft.AspNetCore.App'}:
        raise ValueError('Use a framework-dependent ASP.NET Core publish with both shared frameworks.')
    if len({v[:2] for v in required.values()}) != 1:
        raise ValueError('Both required frameworks must use the same .NET channel.')
    if options.get('rollForward') == 'Disable' or options.get('applyPatches') is False:
        raise ValueError('Pinned runtimes are unsupported; publish with runtime patch roll-forward enabled.')
    target = json.loads((source / 'TicketingCRM.deps.json').read_text())['runtimeTarget']['name']
    if '/' in target and target.rsplit('/', 1)[1] != 'linux-' + arch:
        raise ValueError('The published runtime identifier does not match this Linux architecture.')
    minimum = max(required.values())
    return '.'.join(map(str, minimum[:2])), minimum


def architecture(machine=None):
    machine = machine or platform.machine()
    if machine.lower() in ('x86_64', 'amd64'):
        return 'x64'
    if machine.lower() in ('aarch64', 'arm64'):
        return 'arm64'
    raise ValueError('Automatic Linux installation supports x64 and arm64.')


def hostname(value):
    value = value.lower().rstrip('.')
    if len(value) > 253 or not re.fullmatch(r'[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?', value):
        raise ValueError('Enter a DNS hostname (or localhost), without URL, path, port or configuration characters.')
    if any(not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?', label) for label in value.split('.')):
        raise ValueError('Invalid DNS hostname.')
    try:
        ipaddress.ip_address(value)
    except ValueError:
        return value
    raise ValueError('Use a DNS hostname; raw IP addresses are not supported by this installer.')


def port(text):
    number = int(text)
    if number < 1 or number > 65535:
        raise ValueError('Port must be between 1 and 65535.')
    return number


def vendor_url(url):
    parsed = urllib.parse.urlsplit(url)
    if parsed.scheme != 'https' or parsed.hostname not in VENDOR_HOSTS or parsed.username or parsed.password or parsed.port not in (None, 443):
        raise ValueError('Runtime downloads must use approved HTTPS Microsoft hosts.')
    return url


class VendorRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        vendor_url(newurl)
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def vendor_open(url):
    return urllib.request.build_opener(VendorRedirects()).open(vendor_url(url), timeout=60)


def select_runtime(metadata, channel, minimum, arch):
    if metadata['channel-version'] != channel or metadata.get('support-phase') not in ('active', 'maintenance'):
        raise ValueError('The required .NET channel is unsupported or mismatched.')
    latest = metadata['latest-runtime']
    if version(latest) < minimum:
        raise ValueError('The published minimum runtime is newer than the available stable release.')
    release = next(r for r in metadata['releases'] if r.get('aspnetcore-runtime', {}).get('version') == latest)
    expected = 'aspnetcore-runtime-linux-' + arch + '.tar.gz'
    item = next(f for f in release['aspnetcore-runtime']['files'] if f['name'] == expected and f['rid'] == 'linux-' + arch)
    vendor_url(item['url'])
    if not re.fullmatch(r'[0-9a-fA-F]{128}', item['hash']):
        raise ValueError('Missing runtime SHA-512 digest.')
    return latest, item


def download(item, destination):
    digest = hashlib.sha512()
    total = 0
    with vendor_open(item['url']) as response, open(destination, 'xb') as output:
        while chunk := response.read(1024 * 1024):
            total += len(chunk)
            if total > 512 * 1024 * 1024:
                raise ValueError('Runtime download exceeds its bounded size.')
            digest.update(chunk)
            output.write(chunk)
    if digest.hexdigest().lower() != item['hash'].lower():
        raise ValueError('Runtime SHA-512 verification failed; downloaded code was not executed.')


def extract_runtime(archive, destination):
    # Reject link/device/traversal entries before extracting any member; extraction remains bounded.
    with tarfile.open(archive, 'r:gz') as bundle:
        members = []
        total = 0
        for member in bundle:
            members.append(member)
            total += member.size
            if member.size < 0 or len(members) > 20000 or total > 1024 * 1024 * 1024:
                raise ValueError('Runtime archive exceeds extraction limits.')
            path = pathlib.PurePosixPath(member.name)
            if path.is_absolute() or '..' in path.parts or not (member.isfile() or member.isdir()):
                raise ValueError('Unsafe runtime archive entry.')
        for member in members:
            member.mode &= 0o777
        if hasattr(tarfile, 'data_filter'):
            bundle.extractall(destination, members=members, filter='data')
        else:
            # Older supported Python patch levels: all entries were validated as regular and contained above.
            bundle.extractall(destination, members=members)


def private_path(path):
    path = pathlib.Path(path)
    for current in (path, *path.parents):
        if current.is_symlink():
            raise ValueError('Refusing a linked managed deployment path: ' + str(current))


def write_owned(path, text, mode=0o644):
    private_path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and not path.read_text().startswith(MARKER):
        raise ValueError('Refusing to overwrite an unmanaged hosting configuration: ' + str(path))
    descriptor, temporary = tempfile.mkstemp(dir=path.parent, prefix='.appsouls-')
    try:
        with os.fdopen(descriptor, 'w') as output:
            output.write(text)
            output.flush()
            os.fsync(output.fileno())
        os.chmod(temporary, mode)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def nginx_config(host, https_port, backend_port):
    bind = '127.0.0.1' if host == 'localhost' else '0.0.0.0'
    return f'''{MARKER}
server {{
    listen {bind}:{https_port} ssl;
    server_name {host};
    ssl_certificate /etc/appsouls/tls/server.crt;
    ssl_certificate_key /etc/appsouls/tls/server.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    client_max_body_size 514m;
    location / {{
        proxy_pass http://127.0.0.1:{backend_port};
        proxy_http_version 1.1;
        proxy_set_header Host $http_host;
        proxy_set_header X-Forwarded-Host "";
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header X-Forwarded-Proto https;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_buffering off;
        proxy_request_buffering off;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }}
}}
'''


def service_config(backend_port):
    return f'''{MARKER}
[Unit]
Description=App Souls by Tetrarchia
After=network-online.target
Wants=network-online.target
[Service]
Type=simple
User=appsouls
Group=appsouls
WorkingDirectory=/opt/AppSouls/current
ExecStart=/opt/AppSouls/runtime/dotnet /opt/AppSouls/current/TicketingCRM.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/opt/AppSouls/runtime
Environment=HOME=/var/lib/appsouls
Environment=APPSOULS_VAULT_PATH=/var/lib/appsouls/vault/server.vault
Environment=APPSOULS_KEY_PATH=/var/lib/appsouls/keys/server.key
Environment=APPSOULS_SETUP_PRESET_PATH=/var/lib/appsouls/setup/server-preset.json
Environment=APPSOULS_BEHIND_NGINX=1
Environment=APPSOULS_LISTEN_URLS=http://127.0.0.1:{backend_port}
Restart=on-failure
RestartSec=5
TimeoutStopSec=60
KillSignal=SIGINT
UMask=0077
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/appsouls
[Install]
WantedBy=multi-user.target
'''


def linux_plan(distro):
    if distro in ('debian', 'ubuntu'):
        return [('apt-get', 'update'), ('apt-get', 'install', '-y', 'nginx', 'openssl', 'ca-certificates', 'libicu-dev', 'libssl-dev', 'libkrb5-3', 'zlib1g', 'libgcc-s1', 'libstdc++6')]
    if distro == 'fedora':
        return [('dnf', 'install', '-y', 'nginx', 'openssl', 'ca-certificates', 'libicu', 'openssl-libs', 'krb5-libs', 'zlib', 'libgcc', 'libstdc++', 'policycoreutils-python-utils')]
    raise ValueError('Supported native Linux installers: Fedora, Debian and Ubuntu with systemd.')


def ensure_account(existing):
    try:
        account = pwd.getpwnam(USER)
        if not existing or account.pw_dir != str(STATE) or account.pw_uid == 0 or account.pw_shell not in ('/sbin/nologin', '/usr/sbin/nologin'):
            raise ValueError('Refusing to adopt an unrelated appsouls account.')
    except KeyError:
        shell = shutil.which('nologin') or '/usr/sbin/nologin'
        run(('useradd', '--system', '--user-group', '--home-dir', STATE, '--shell', shell, USER))
        account = pwd.getpwnam(USER)
    private_path(STATE)
    STATE.mkdir(mode=0o700, parents=True, exist_ok=True)
    os.chown(STATE, account.pw_uid, account.pw_gid)
    os.chmod(STATE, 0o700)


def tls_setup(host, cert, key):
    tls = ETC / 'tls'
    private_path(tls)
    tls.mkdir(mode=0o700, parents=True, exist_ok=True)
    os.chmod(tls, 0o700)
    private_path(tls / 'server.crt')
    private_path(tls / 'server.key')
    if cert:
        run(('openssl', 'x509', '-in', cert, '-noout', '-checkhost', host))
        run(('openssl', 'x509', '-in', cert, '-noout', '-checkend', '86400'))
        cert_pub = run(('openssl', 'x509', '-in', cert, '-noout', '-pubkey'), capture_output=True).stdout
        key_pub = run(('openssl', 'pkey', '-in', key, '-passin', 'pass:', '-pubout'), capture_output=True).stdout
        if cert_pub != key_pub:
            raise ValueError('TLS certificate and private key do not match.')
        shutil.copyfile(cert, tls / 'server.crt')
        shutil.copyfile(key, tls / 'server.key')
    elif not (tls / 'server.crt').exists():
        run(('openssl', 'req', '-x509', '-newkey', 'rsa:3072', '-sha256', '-nodes', '-days', '90', '-subj', '/CN=' + host,
             '-addext', 'subjectAltName=DNS:' + host, '-keyout', tls / 'server.key', '-out', tls / 'server.crt'), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        run(('openssl', 'x509', '-in', tls / 'server.crt', '-noout', '-checkhost', host))
        run(('openssl', 'x509', '-in', tls / 'server.crt', '-noout', '-checkend', '86400'))
    os.chmod(tls / 'server.key', 0o600)
    os.chmod(tls / 'server.crt', 0o644)


def verify_backend(backend_port):
    deadline = time.monotonic() + 120
    while time.monotonic() < deadline:
        try:
            connection = http.client.HTTPConnection('127.0.0.1', backend_port, timeout=3)
            try:
                connection.request('GET', '/setup', headers={'Host': 'localhost', 'X-Forwarded-For': '127.0.0.1', 'X-Forwarded-Proto': 'https'})
                response = connection.getresponse()
                if response.status == 200 and b'App Souls' in response.read(262144):
                    return
            finally:
                connection.close()
        except (OSError, http.client.HTTPException):
            pass
        time.sleep(1)
    raise RuntimeError('App Souls did not become ready. Inspect journalctl -u appsouls. No serving success was reported.')


def configure_selinux(https_port):
    if not shutil.which('getenforce') or run(('getenforce',), capture_output=True, text=True).stdout.strip() == 'Disabled':
        return
    run(('setsebool', '-P', 'httpd_can_network_connect', 'on'))
    mapping = '/etc/appsouls/tls(/.*)?'
    local = run(('semanage', 'fcontext', '-l', '-C'), capture_output=True, text=True).stdout
    run(('semanage', 'fcontext', '-m' if mapping in local else '-a', '-t', 'cert_t', mapping))
    run(('restorecon', '-RF', ETC / 'tls'))
    listed = run(('semanage', 'port', '-l'), capture_output=True, text=True).stdout
    for line in listed.splitlines():
        fields = line.split(None, 2)
        if len(fields) == 3 and fields[:2] == ['http_port_t', 'tcp']:
            for item in fields[2].split(','):
                bounds = item.strip().split('-')
                if int(bounds[0]) <= https_port <= int(bounds[-1]):
                    return
    # Never relabel a port belonging to an unrelated service; -a fails on conflicting labels.
    run(('semanage', 'port', '-a', '-t', 'http_port_t', '-p', 'tcp', str(https_port)))


def deploy(args, distro, arch):
    channel, minimum = runtime_requirement(args.source, arch)
    packages = linux_plan(distro)
    if args.plan:
        print(json.dumps({'channel': channel, 'minimum': '.'.join(map(str, minimum)), 'architecture': arch,
                          'destination': str(ROOT), 'state': str(STATE), 'hostname': args.hostname,
                          'https_port': args.https_port, 'backend_port': args.backend_port, 'packages': packages,
                          'scanner': dataclasses.asdict(scanner_setup.plan_for('Linux', distro))}, indent=2))
        return
    if os.geteuid() != 0 or not shutil.which('systemctl'):
        raise ValueError('Run as root on a systemd host. --plan makes no changes.')
    for path in (ROOT, STATE, ETC, SITE, UNIT):
        private_path(path)
    if ROOT.resolve() == args.source.resolve() or ROOT.resolve() in args.source.resolve().parents:
        raise ValueError('Run upgrades from a separate extracted publish package.')
    existing = (ROOT / 'deployment.json').exists()
    if ROOT.exists() and not existing:
        raise ValueError('The destination exists without an App Souls deployment record; no files were overwritten.')
    settings = {'hostname': args.hostname, 'https_port': args.https_port, 'backend_port': args.backend_port}
    if existing:
        previous = json.loads((ROOT / 'deployment.json').read_text())
        if not args.upgrade:
            raise ValueError('An installation already exists. Use --upgrade for an intentional stop/copy/start update.')
        if any(previous.get(k) != v for k, v in settings.items()):
            raise ValueError('Existing site hostname/ports must match; hosting changes require a separate maintenance operation.')
    else:
        if STATE.exists() or ETC.exists() or SITE.exists() or UNIT.exists():
            raise ValueError('Unmanaged state/hosting files already exist. Preserve and reconcile them before installing.')
    for path in (SITE, UNIT):
        if path.exists() and not path.read_text().startswith(MARKER):
            raise ValueError('Refusing to replace unmanaged hosting files.')
    for binding in (('127.0.0.1', args.backend_port), ('127.0.0.1' if args.hostname == 'localhost' else '0.0.0.0', args.https_port)):
        if not existing:
            with socket.socket() as check:
                check.bind(binding)
    # Resolve and verify runtime payload before stopping an existing service.
    with vendor_open(f'https://builds.dotnet.microsoft.com/dotnet/release-metadata/{channel}/releases.json') as response:
        metadata = json.load(response)
    latest, item = select_runtime(metadata, channel, minimum, arch)
    for command in packages:
        run(command)
    scanner_setup.install(scanner_setup.plan_for('Linux', distro))
    ROOT.mkdir(mode=0o755, parents=True, exist_ok=True)
    if not existing:
        record = ROOT / 'deployment.json'
        record.write_text(json.dumps({**settings, 'status': 'provisioning'}, indent=2))
        os.chmod(record, 0o600)
    with tempfile.TemporaryDirectory(prefix='appsouls-runtime-') as temporary:
        archive = pathlib.Path(temporary) / 'runtime.tar.gz'
        download(item, archive)
        unpacked = pathlib.Path(temporary) / 'runtime'
        unpacked.mkdir()
        extract_runtime(archive, unpacked)
        output = run((unpacked / 'dotnet', '--list-runtimes'), capture_output=True, text=True).stdout
        if any(f'{name} {latest} ' not in output for name in ('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App')):
            raise ValueError('Downloaded runtime does not contain both expected shared frameworks.')
        ensure_account(existing)
        tls_setup(args.hostname, args.certificate, args.certificate_key)
        release = ROOT / 'releases' / uuid.uuid4().hex
        shutil.copytree(args.source, release)
        for path in (release, *release.rglob('*')):
            os.chmod(path, 0o755 if path.is_dir() else 0o644)
        runtime = ROOT / 'runtimes' / (latest + '-' + uuid.uuid4().hex)
        shutil.copytree(unpacked, runtime)
        for path in (runtime, *runtime.rglob('*')):
            os.chmod(path, 0o755 if path.is_dir() or path.stat().st_mode & 0o111 else 0o644)
    # Save ownership before first startup so a failed activation can be intentionally retried.
    record = ROOT / 'deployment.json'
    record.write_text(json.dumps({**settings, 'runtime': latest, 'release': str(release), 'status': 'prepared'}, indent=2))
    os.chmod(record, 0o600)
    if existing:
        run(('systemctl', 'stop', 'appsouls'))
    for target, link in ((runtime, ROOT / 'runtime'), (release, ROOT / 'current')):
        temporary = ROOT / ('.link-' + uuid.uuid4().hex)
        temporary.symlink_to(target, target_is_directory=True)
        os.replace(temporary, link)
    write_owned(UNIT, service_config(args.backend_port))
    write_owned(SITE, nginx_config(args.hostname, args.https_port, args.backend_port))
    run(('nginx', '-t'))
    configure_selinux(args.https_port)
    try:
        run(('systemctl', 'daemon-reload'))
        run(('systemctl', 'enable', 'appsouls', 'nginx'))
        run(('systemctl', 'start', 'appsouls'))
        verify_backend(args.backend_port)
        run(('systemctl', 'reload-or-restart', 'nginx'))
        # Local TLS handshake verifies the proxy without trusting a generated bootstrap certificate.
        context = ssl._create_unverified_context()
        connection = http.client.HTTPSConnection('127.0.0.1', args.https_port, timeout=10, context=context)
        try:
            connection.request('GET', '/setup', headers={'Host': args.hostname})
            response = connection.getresponse()
            if response.status != 200 or b'App Souls' not in response.read(262144):
                raise RuntimeError('The HTTPS site readiness check failed.')
        finally:
            connection.close()
    except Exception:
        run(('systemctl', 'stop', 'appsouls'))
        raise
    if args.hostname != 'localhost':
        if shutil.which('firewall-cmd') and subprocess.run(['firewall-cmd', '--state'], capture_output=True).returncode == 0:
            run(('firewall-cmd', '--permanent', '--add-port=' + str(args.https_port) + '/tcp'))
            run(('firewall-cmd', '--reload'))
        elif shutil.which('ufw') and 'Status: active' in run(('ufw', 'status'), capture_output=True, text=True).stdout:
            run(('ufw', 'allow', str(args.https_port) + '/tcp'))
    record.write_text(json.dumps({**settings, 'runtime': latest, 'release': str(release), 'status': 'ready'}, indent=2))
    print(f'App Souls installed: https://{args.hostname}:{args.https_port}/setup')
    if not args.certificate:
        print('Without a supplied certificate, fresh installations use self-signed 90-day bootstrap TLS; existing certificates are retained. Supply/renew a trusted certificate before public use.')
    print('Complete database, administrator and email settings in server configuration. Existing vault, keys and artifacts were retained.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=pathlib.Path, default=pathlib.Path(__file__).resolve().parent.parent)
    parser.add_argument('--hostname', type=hostname, default='localhost')
    parser.add_argument('--https-port', type=port, default=443)
    parser.add_argument('--backend-port', type=port, default=5198)
    parser.add_argument('--certificate', type=pathlib.Path)
    parser.add_argument('--certificate-key', type=pathlib.Path)
    parser.add_argument('--upgrade', action='store_true')
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    if bool(args.certificate) != bool(args.certificate_key) or args.https_port == args.backend_port:
        parser.error('Supply both certificate files, and distinct HTTPS/backend ports.')
    if platform.system() != 'Linux':
        parser.error('This host installer targets Linux; use install-server.ps1 on Windows.')
    distro = platform.freedesktop_os_release().get('ID', '')
    if args.plan:
        deploy(args, distro, architecture())
    else:
        if os.geteuid() != 0:
            parser.error('Run the installer as root; --plan is read-only.')
        descriptor = os.open('/run/lock/appsouls-install.lock', os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
        with os.fdopen(descriptor, 'r+') as lock:
            info = os.fstat(lock.fileno())
            if info.st_uid != 0 or not stat.S_ISREG(info.st_mode):
                raise ValueError('The installer lock must be a root-owned regular file.')
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            deploy(args, distro, architecture())


if __name__ == '__main__':
    try:
        main()
    except (ValueError, RuntimeError, OSError, KeyError, StopIteration, subprocess.CalledProcessError) as error:
        # Installer arguments never include application credentials. Do not print file contents.
        print('Server installation failed: ' + str(error), file=__import__('sys').stderr)
        raise SystemExit(1)
