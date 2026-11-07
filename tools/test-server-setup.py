#!/usr/bin/env python3
import contextlib
import hashlib
import gzip
import importlib.util
import io
import json
import pathlib
import tarfile
import tempfile
import types
import unittest
from unittest.mock import patch

import server_setup as setup


def package(root, channel='10.0', rid=None):
    root.mkdir(parents=True)
    (root / 'wwwroot').mkdir()
    (root / 'TicketingCRM.dll').write_bytes(b'published-test-fixture')
    (root / 'TicketingCRM.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'frameworks': [
        {'name':'Microsoft.NETCore.App','version':channel+'.0'}, {'name':'Microsoft.AspNetCore.App','version':channel+'.1'}]}}))
    (root / 'TicketingCRM.deps.json').write_text(json.dumps({'runtimeTarget':{'name':'.NETCoreApp,Version=v'+channel+('/'+rid if rid else '')}}))
    return root


def metadata():
    return {'channel-version':'10.0','support-phase':'active','latest-runtime':'10.0.14','releases':[
        {'aspnetcore-runtime':{'version':'10.0.14','files':[
            {'name':'aspnetcore-runtime-linux-x64.tar.gz','rid':'linux-x64','url':'https://builds.dotnet.microsoft.com/runtime.tar.gz','hash':'a'*128}]}}]}


class ServerInstallerTests(unittest.TestCase):
    def test_detects_publish_channel_minimum_and_rejects_wrong_rid_or_pinned_runtime(self):
        with tempfile.TemporaryDirectory() as tmp:
            source=package(pathlib.Path(tmp)/'app')
            self.assertEqual(('10.0',(10,0,1)),setup.runtime_requirement(source,'x64'))
            deps=source/'TicketingCRM.deps.json';deps.write_text(json.dumps({'runtimeTarget':{'name':'net10.0/win-x64'}}))
            with self.assertRaisesRegex(ValueError,'identifier'):setup.runtime_requirement(source,'x64')
            deps.write_text(json.dumps({'runtimeTarget':{'name':'net10.0'}}))
            config=source/'TicketingCRM.runtimeconfig.json';value=json.loads(config.read_text());value['runtimeOptions']['applyPatches']=False;config.write_text(json.dumps(value))
            with self.assertRaisesRegex(ValueError,'Pinned'):setup.runtime_requirement(source,'x64')

    def test_missing_self_contained_mixed_framework_and_linked_inputs_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);source=package(root/'app');config=source/'TicketingCRM.runtimeconfig.json'
            config.write_text('{"runtimeOptions":{"includedFrameworks":[]}}')
            with self.assertRaisesRegex(ValueError,'framework-dependent'):setup.runtime_requirement(source,'x64')
            package(root/'other');config.write_bytes((root/'other'/config.name).read_bytes())
            value=json.loads(config.read_text());value['runtimeOptions']['frameworks'][0]['version']='9.0.0';config.write_text(json.dumps(value))
            with self.assertRaisesRegex(ValueError,'same .NET channel'):setup.runtime_requirement(source,'x64')
            (source/'linked').symlink_to('/etc/passwd')
            with self.assertRaisesRegex(ValueError,'without links'):setup.runtime_requirement(source,'x64')

    def test_selects_supported_matching_latest_patch_and_rejects_untrusted_urls_or_old_channel(self):
        data=metadata();latest,item=setup.select_runtime(data,'10.0',(10,0,1),'x64');self.assertEqual('10.0.14',latest)
        with self.assertRaisesRegex(ValueError,'newer'):setup.select_runtime(data,'10.0',(10,0,15),'x64')
        data['support-phase']='eol'
        with self.assertRaisesRegex(ValueError,'unsupported'):setup.select_runtime(data,'10.0',(10,0,1),'x64')
        for url in ['http://builds.dotnet.microsoft.com/a','https://builds.dotnet.microsoft.com.evil.test/a','https://evil.test/a','https://user@builds.dotnet.microsoft.com/a']:
            with self.subTest(url=url),self.assertRaises(ValueError):setup.vendor_url(url)

    def test_runtime_download_verifies_digest_before_code_is_used(self):
        with tempfile.TemporaryDirectory() as tmp,patch.object(setup,'vendor_open',return_value=io.BytesIO(b'runtime')):
            dest=pathlib.Path(tmp)/'runtime';setup.download({'url':'unused','hash':hashlib.sha512(b'runtime').hexdigest()},dest);self.assertEqual(b'runtime',dest.read_bytes())
        with tempfile.TemporaryDirectory() as tmp,patch.object(setup,'vendor_open',return_value=io.BytesIO(b'tampered')):
            with self.assertRaisesRegex(ValueError,'verification failed'):setup.download({'url':'unused','hash':'a'*128},pathlib.Path(tmp)/'bad')

    def test_runtime_archive_rejects_traversal_links_and_accepts_regular_files(self):
        for name,kind,success in [('dotnet',tarfile.REGTYPE,True),('../outside',tarfile.REGTYPE,False),('link',tarfile.SYMTYPE,False)]:
            with self.subTest(name=name),tempfile.TemporaryDirectory() as tmp:
                root=pathlib.Path(tmp);archive=root/'a.tar.gz'
                with tarfile.open(archive,'w:gz') as out:
                    member=tarfile.TarInfo(name);member.type=kind;member.linkname='/etc/passwd';member.size=1 if kind==tarfile.REGTYPE else 0;out.addfile(member,io.BytesIO(b'x') if member.size else None)
                if success:
                    setup.extract_runtime(archive,root/'out');self.assertEqual(b'x',(root/'out'/'dotnet').read_bytes())
                else:
                    with self.assertRaisesRegex(ValueError,'Unsafe'):setup.extract_runtime(archive,root/'out')
                    self.assertFalse((root/'out').exists())

    def test_host_and_port_inputs_cannot_inject_proxy_or_service_directives(self):
        self.assertEqual('crm.example.test',setup.hostname('CRM.example.test.'))
        for host in ['https://example.test','evil; include /tmp/a','a\nlisten 80','*.example.test','-bad.test','127.0.0.1','bad..test']:
            with self.subTest(host=host),self.assertRaises(ValueError):setup.hostname(host)
        for value in ('0','65536','-1'):
            with self.assertRaises(ValueError):setup.port(value)
        self.assertEqual(65535,setup.port('65535'))
        self.assertEqual('arm64',setup.architecture('aarch64'))
        with self.assertRaises(ValueError):setup.architecture('i686')

    def test_oversized_archive_is_rejected_before_any_extraction(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);archive=root/'oversized.tar.gz';member=tarfile.TarInfo('huge');member.size=1024*1024*1024+1
            with gzip.open(archive,'wb') as output:output.write(member.tobuf()+b'\0'*1024)
            with self.assertRaisesRegex(ValueError,'extraction limits'):setup.extract_runtime(archive,root/'out')
            self.assertFalse((root/'out').exists())

    def test_proxy_and_service_preserve_https_websockets_streaming_and_privilege_boundaries(self):
        proxy=setup.nginx_config('localhost',443,5198)
        self.assertIn('listen 127.0.0.1:443 ssl;',proxy)
        self.assertIn('proxy_set_header X-Forwarded-For $remote_addr;',proxy)
        self.assertNotIn('$proxy_add_x_forwarded_for',proxy)
        for directive in ('proxy_set_header X-Forwarded-Proto https;','proxy_set_header Upgrade $http_upgrade;','proxy_request_buffering off;','client_max_body_size 514m;'):
            self.assertIn(directive,proxy)
        self.assertIn('listen 0.0.0.0:443 ssl;',setup.nginx_config('crm.example.test',443,5198))
        unit=setup.service_config(5198)
        for directive in ('User=appsouls','ProtectSystem=strict','ReadWritePaths=/var/lib/appsouls','APPSOULS_BEHIND_NGINX=1','APPSOULS_LISTEN_URLS=http://127.0.0.1:5198','UMask=0077'):
            self.assertIn(directive,unit)
        self.assertNotIn('Development',unit)

    def test_owned_configuration_is_atomic_and_unmanaged_or_linked_paths_are_preserved(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);path=root/'site';path.write_text('unrelated')
            with self.assertRaisesRegex(ValueError,'unmanaged'):setup.write_owned(path,setup.MARKER+'\nnew')
            self.assertEqual('unrelated',path.read_text());path.unlink();setup.write_owned(path,setup.MARKER+'\na');setup.write_owned(path,setup.MARKER+'\nb');self.assertTrue(path.read_text().endswith('b'))
            (root/'link').symlink_to(path)
            with self.assertRaisesRegex(ValueError,'linked'):setup.write_owned(root/'link',setup.MARKER)

    def test_read_only_plan_includes_runtime_webserver_scanner_and_makes_no_system_changes(self):
        with tempfile.TemporaryDirectory() as tmp:
            args=types.SimpleNamespace(source=package(pathlib.Path(tmp)/'app'),plan=True,hostname='localhost',https_port=443,backend_port=5198)
            with patch.object(setup,'run') as run,patch.object(setup.scanner_setup,'install') as scanner,contextlib.redirect_stdout(io.StringIO()) as out:
                setup.deploy(args,'fedora','x64')
            value=json.loads(out.getvalue());self.assertEqual('10.0',value['channel']);self.assertEqual('/opt/AppSouls',value['destination']);self.assertIn('nginx',value['packages'][0]);self.assertEqual('clamd@scan',value['scanner']['daemon_service']);run.assert_not_called();scanner.assert_not_called()

    def test_deployment_refuses_unmanaged_destination_and_upgrade_without_explicit_switch(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);source=package(root/'publish');destination=root/'opt';destination.mkdir()
            args=types.SimpleNamespace(source=source,plan=False,hostname='localhost',https_port=443,backend_port=5198,upgrade=False)
            with patch.multiple(setup,ROOT=destination,STATE=root/'state',ETC=root/'etc',SITE=root/'site',UNIT=root/'unit'),patch.object(setup.os,'geteuid',return_value=0),patch.object(setup.shutil,'which',return_value='/bin/tool'),patch.object(setup,'vendor_open') as network:
                with self.assertRaisesRegex(ValueError,'without an App Souls'):setup.deploy(args,'fedora','x64')
                (destination/'deployment.json').write_text('{"hostname":"localhost","https_port":443,"backend_port":5198}')
                with self.assertRaisesRegex(ValueError,'--upgrade'):setup.deploy(args,'fedora','x64')
                args.upgrade=True;args.https_port=8443
                with self.assertRaisesRegex(ValueError,'must match'):setup.deploy(args,'fedora','x64')
                network.assert_not_called()

    def test_full_mocked_install_and_failed_upgrade_preserve_state_and_stop_new_service(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);source=package(root/'publish');destination=root/'opt';state=root/'state'
            args=types.SimpleNamespace(source=source,plan=False,hostname='localhost',https_port=443,backend_port=5198,upgrade=False,certificate=None,certificate_key=None)
            def extract(archive, output):
                (output/'dotnet').write_text('verified-runtime')
            def account(existing):
                state.mkdir(exist_ok=True);(state/'identity').write_text('retained identity and key material') if not (state/'identity').exists() else None
            def command(args, **kwargs):
                return types.SimpleNamespace(stdout='Microsoft.NETCore.App 10.0.14 [runtime]\nMicrosoft.AspNetCore.App 10.0.14 [runtime]\n',returncode=0)
            with contextlib.ExitStack() as stack:
                stack.enter_context(patch.multiple(setup,ROOT=destination,STATE=state,ETC=root/'etc',SITE=root/'site',UNIT=root/'unit'))
                for target,kwargs in [('os.geteuid',{'return_value':0}),('shutil.which',{'return_value':'/bin/tool'}),('socket.socket',{}),('tls_setup',{}),('download',{}),('extract_runtime',{'side_effect':extract}),('ensure_account',{'side_effect':account}),('verify_backend',{})]:
                    stack.enter_context(patch('server_setup.'+target,**kwargs))
                run=stack.enter_context(patch.object(setup,'run',side_effect=command));scanner=stack.enter_context(patch.object(setup.scanner_setup,'install'))
                stack.enter_context(patch.object(setup,'vendor_open',side_effect=lambda _:io.BytesIO(json.dumps(metadata()).encode())))
                connection=stack.enter_context(patch.object(setup.http.client,'HTTPSConnection'));connection.return_value.getresponse.return_value.status=200;connection.return_value.getresponse.return_value.read.return_value=b'App Souls setup'
                with contextlib.redirect_stdout(io.StringIO()):setup.deploy(args,'fedora','x64')
                self.assertEqual('ready',json.loads((destination/'deployment.json').read_text())['status'])
                first_release=(destination/'current').resolve();self.assertTrue((first_release/'TicketingCRM.dll').exists());self.assertEqual('verified-runtime',(destination/'runtime'/'dotnet').read_text())
                self.assertEqual(0o644,(first_release/'TicketingCRM.dll').stat().st_mode&0o777)
                scanner.assert_called_once();self.assertIn(('systemctl','reload-or-restart','nginx'),[tuple(c.args[0]) for c in run.call_args_list])
                args.upgrade=True
                with patch.object(setup,'verify_backend',side_effect=RuntimeError('startup verification failed')):
                    with self.assertRaisesRegex(RuntimeError,'startup verification failed'):setup.deploy(args,'fedora','x64')
                self.assertEqual('retained identity and key material',(state/'identity').read_text())
                self.assertTrue(first_release.exists());self.assertNotEqual(first_release,(destination/'current').resolve())
                self.assertEqual('prepared',json.loads((destination/'deployment.json').read_text())['status'])
                self.assertEqual(('systemctl','stop','appsouls'),tuple(run.call_args_list[-1].args[0]))

    def test_tls_is_private_and_mismatched_replacement_preserves_existing_certificate_and_key(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp)
            with patch.object(setup,'ETC',root/'first'):
                setup.tls_setup('localhost',None,None)
                cert=root/'first/tls/server.crt';key=root/'first/tls/server.key';original=(cert.read_bytes(),key.read_bytes())
                self.assertEqual(0o600,key.stat().st_mode&0o777);self.assertEqual(0o700,key.parent.stat().st_mode&0o777)
            with patch.object(setup,'ETC',root/'second'):setup.tls_setup('localhost',None,None)
            with patch.object(setup,'ETC',root/'first'):
                with self.assertRaisesRegex(ValueError,'do not match'):setup.tls_setup('localhost',root/'second/tls/server.crt',key)
                self.assertEqual(original,(cert.read_bytes(),key.read_bytes()))

    def test_selinux_labels_managed_tls_and_adds_only_unclaimed_custom_http_ports(self):
        def command(args, **kwargs):
            values={('getenforce',):'Enforcing',('semanage','fcontext','-l','-C'):'/etc/appsouls/tls(/.*)? all files cert_t',('semanage','port','-l'):'http_port_t tcp 80, 443, 8008-8009\nother_port_t tcp 5555'}
            return types.SimpleNamespace(stdout=values.get(tuple(args),''))
        with patch.object(setup.shutil,'which',return_value='/bin/tool'),patch.object(setup,'run',side_effect=command) as run:
            setup.configure_selinux(443)
            commands=[tuple(c.args[0]) for c in run.call_args_list]
            self.assertIn(('semanage','fcontext','-m','-t','cert_t','/etc/appsouls/tls(/.*)?'),commands)
            self.assertFalse(any(c[:3]==('semanage','port','-a') for c in commands))
            run.reset_mock();setup.configure_selinux(8443)
            self.assertIn(('semanage','port','-a','-t','http_port_t','-p','tcp','8443'),[tuple(c.args[0]) for c in run.call_args_list])
            self.assertFalse(any(tuple(c.args[0])[:3]==('semanage','port','-m') for c in run.call_args_list))


if __name__=='__main__':unittest.main()
