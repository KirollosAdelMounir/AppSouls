#!/usr/bin/env python3
"""Build one self-extracting Linux installer from a complete published server directory."""
import argparse
import hashlib
import pathlib
import shutil
import tarfile
import tempfile

HEADER = '''#!/bin/sh
# App Souls by Tetrarchia. Contains application payload; dependencies are fetched from native/vendor repositories.
set -eu
for command in tail sha256sum mktemp tar gzip find; do
    if ! command -v "$command" >/dev/null 2>&1; then
        echo "Required bootstrap utility missing: $command (coreutils, tar, gzip)." >&2
        exit 1
    fi
done
setup_tmp=$(mktemp -d "/tmp/appsouls-setup.XXXXXXXX")
trap 'find "$setup_tmp" -depth -delete' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
umask 077
tail -c +OFFSETNUMBER "$0" > "$setup_tmp/payload.tar.gz"
setup_digest=$(sha256sum "$setup_tmp/payload.tar.gz")
if [ "${setup_digest%% *}" != 'PAYLOADHASH' ]; then
    echo 'The App Souls payload checksum does not match. Installation was not started.' >&2
    exit 1
fi
mkdir "$setup_tmp/app"
tar -xzf "$setup_tmp/payload.tar.gz" --no-same-owner --no-same-permissions -C "$setup_tmp/app"
if sh "$setup_tmp/app/install/install-server.sh" "$@"; then setup_status=0; else setup_status=$?; fi
exit "$setup_status"
# App Souls embedded payload follows.
'''


def build(source, destination):
    source=pathlib.Path(source).resolve(strict=True);destination=pathlib.Path(destination).absolute()
    if destination == source or source in destination.parents:
        raise ValueError('Keep installer output outside the published input directory.')
    for name in ('TicketingCRM.dll','TicketingCRM.runtimeconfig.json','TicketingCRM.deps.json','wwwroot','install/install-server.sh','install/server_setup.py','install/scanner_setup.py'):
        if not (source/name).exists():raise ValueError('Incomplete published server: '+name)
    paths=sorted(source.rglob('*'))
    for path in paths:
        if path.is_symlink() or not(path.is_dir() or path.is_file()):raise ValueError('Links and special files are not allowed in server packages.')
        if path.suffix.lower() in ('.vault','.key','.pem','.pfx','.p12') or path.name in ('server-preset.json','server-input.json','owner-input.json'):
            raise ValueError('Private configuration/key material must not be packaged.')
    destination.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='appsouls-package-') as temporary:
        archive=pathlib.Path(temporary)/'payload.tar.gz'
        with tarfile.open(archive,'w:gz') as bundle:
            for path in paths:
                if '__pycache__' in path.parts or path.suffix=='.pyc':continue
                info=bundle.gettarinfo(str(path),str(path.relative_to(source)))
                info.uid=info.gid=0;info.uname=info.gname='root';info.mode=0o755 if path.is_dir() or path.suffix=='.sh' else 0o644
                if path.is_file():
                    with path.open('rb') as data:bundle.addfile(info,data)
                else:bundle.addfile(info)
        with archive.open('rb') as payload:
            digest=hashlib.file_digest(payload,'sha256').hexdigest()
        header=HEADER.replace('PAYLOADHASH',digest).replace('OFFSETNUMBER','0000000000').encode()
        header=header.replace(b'0000000000',f'{len(header)+1:010}'.encode())
        with destination.open('wb') as output,archive.open('rb') as payload:output.write(header);shutil.copyfileobj(payload,output,1024*1024)
        destination.chmod(0o755)
    return destination


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--source',type=pathlib.Path,required=True);parser.add_argument('--output',type=pathlib.Path,required=True)
    args=parser.parse_args();print(build(args.source,args.output))
