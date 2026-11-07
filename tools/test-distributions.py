import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('builder',pathlib.Path(__file__).with_name('build-linux-installer.py'));builder=importlib.util.module_from_spec(spec);spec.loader.exec_module(builder)


class DistributionTests(unittest.TestCase):
    def fixture(self,root):
        source=root/'publish';(source/'wwwroot').mkdir(parents=True);(source/'install').mkdir()
        for name in ['TicketingCRM.dll','TicketingCRM.runtimeconfig.json','TicketingCRM.deps.json']:(source/name).write_text('fixture')
        for name in ['server_setup.py','scanner_setup.py']:(source/'install'/name).write_text('# fixture')
        (source/'install/install-server.sh').write_text('#!/bin/sh\nset -eu\ntest -f "$(dirname "$0")/../TicketingCRM.dll"\nprintf "%s\\n" "$@"\n')
        return source

    def test_single_file_preserves_payload_arguments_and_cleans_extraction(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);source=self.fixture(root);output=builder.build(source,root/'AppSoulsSetup.sh')
            result=subprocess.run(['sh',str(output),'--plan','two words'],capture_output=True,text=True)
            self.assertEqual(0,result.returncode,result.stderr);self.assertEqual('--plan\ntwo words\n',result.stdout)
            self.assertEqual(0o755,output.stat().st_mode&0o777)

    def test_tampered_payload_never_starts_installation(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);output=builder.build(self.fixture(root),root/'setup.sh');data=bytearray(output.read_bytes());data[-1]^=1;output.write_bytes(data)
            result=subprocess.run(['sh',str(output),'--plan'],capture_output=True,text=True)
            self.assertNotEqual(0,result.returncode);self.assertIn('checksum does not match',result.stderr);self.assertEqual('',result.stdout)

    def test_builder_rejects_links_private_material_and_output_recursion(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);source=self.fixture(root)
            with self.assertRaisesRegex(ValueError,'outside'):builder.build(source,source/'setup.sh')
            (source/'secret.key').write_text('test-only')
            with self.assertRaisesRegex(ValueError,'Private'):builder.build(source,root/'setup.sh')
            (source/'secret.key').unlink();(source/'linked').symlink_to('/etc/passwd')
            with self.assertRaisesRegex(ValueError,'Links'):builder.build(source,root/'setup.sh')

    def test_wix_chain_keeps_dependency_installation_outside_msi_transaction(self):
        import xml.etree.ElementTree as E
        root=pathlib.Path(__file__).resolve().parent.parent
        namespace={'w':'http://wixtoolset.org/schemas/v4/wxs'}
        bundle=E.parse(root/'packaging/windows/Bundle/Bundle.wxs');chain=bundle.find('.//w:Chain',namespace)
        self.assertEqual(['ExePackage','MsiPackage'],[node.tag.split('}')[-1] for node in chain]);self.assertEqual('dependencies',chain[0].attrib['InstallArguments'])
        package=E.parse(root/'packaging/windows/Msi/Package.wxs');actions={a.attrib['Id']:a for a in package.findall('.//w:CustomAction',namespace)}
        self.assertTrue(actions['ConfigureHosting'].attrib['ExeCommand'].startswith('configure '))
        self.assertFalse(any('dependencies' in a.attrib['ExeCommand'] for a in actions.values()))
        self.assertTrue(all(a.attrib['Return']=='check' and a.attrib['Impersonate']=='no' for a in actions.values()))
        sequence=package.find('.//w:InstallExecuteSequence',namespace)
        remove=next(n for n in sequence if n.attrib['Action']=='RemoveHosting');self.assertIn('NOT UPGRADINGPRODUCTCODE',remove.attrib['Condition'])


if __name__=='__main__':unittest.main()
