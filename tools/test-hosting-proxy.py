#!/usr/bin/env python3
"""Live trust-boundary check against a built host; no database, root privileges or host services required."""
import http.client
import os
import pathlib
import socket
import subprocess
import tempfile
import time
import unittest


class HostingProxyTests(unittest.TestCase):
    def test_only_explicitly_enabled_exact_loopback_proxy_can_supply_symmetric_forwarded_headers(self):
        assembly=pathlib.Path(__file__).resolve().parent.parent/'TicketingCRM/bin/Debug/net10.0/TicketingCRM.dll'
        self.assertTrue(assembly.is_file(),'Build the solution first.')
        for enabled in (False,True):
            with self.subTest(proxy_enabled=enabled),tempfile.TemporaryDirectory() as tmp:
                with socket.socket() as listener:
                    listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
                env=os.environ.copy();env.update(APPSOULS_VAULT_PATH=tmp+'/vault/server.vault',APPSOULS_KEY_PATH=tmp+'/keys/server.key',APPSOULS_SETUP_PRESET_PATH=tmp+'/setup/preset.json',APPSOULS_LISTEN_URLS='http://127.0.0.1:'+str(port),APPSOULS_BEHIND_NGINX='1' if enabled else '0',ASPNETCORE_ENVIRONMENT='Production')
                env.pop('ASPNETCORE_FORWARDEDHEADERS_ENABLED',None)
                with open(tmp+'/host.log','w') as log:
                    process=subprocess.Popen(['dotnet',str(assembly)],env=env,stdout=log,stderr=subprocess.STDOUT,cwd=assembly.parents[3])
                    try:
                        def request(source,headers):
                            connection=http.client.HTTPConnection('127.0.0.1',port,timeout=3,source_address=(source,0))
                            try:
                                connection.request('GET','/setup',headers={'Host':'crm.example.test',**headers});response=connection.getresponse();body=response.read();self.assertEqual(200,response.status);self.assertIn(b'App Souls',body);return response.getheader('Strict-Transport-Security')
                            finally:connection.close()
                        deadline=time.monotonic()+30
                        while True:
                            try:request('127.0.0.1',{});break
                            except OSError:
                                if time.monotonic()>deadline or process.poll() is not None:raise
                                time.sleep(.1)
                        headers={'X-Forwarded-For':'203.0.113.7','X-Forwarded-Proto':'https'}
                        hsts=request('127.0.0.1',headers)
                        self.assertEqual(enabled,hsts is not None)
                        self.assertIsNone(request('127.0.0.2',headers),'An untrusted 127/8 address supplied a scheme.')
                        self.assertIsNone(request('127.0.0.1',{'X-Forwarded-Proto':'https'}),'Asymmetric forwarded headers were trusted.')
                        self.assertIsNone(request('127.0.0.1',{}))
                    finally:
                        process.terminate()
                        try:process.wait(timeout=15)
                        except subprocess.TimeoutExpired:process.kill();process.wait()
                self.assertFalse(pathlib.Path(tmp+'/vault/server.vault').exists(),'Read-only setup probes created an installation vault.')

    def test_iis_allocated_port_and_pairing_token_take_precedence_over_standalone_listener(self):
        assembly=pathlib.Path(__file__).resolve().parent.parent/'TicketingCRM/bin/Debug/net10.0/TicketingCRM.dll'
        with tempfile.TemporaryDirectory() as tmp:
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
            env=os.environ.copy();env.update(APPSOULS_VAULT_PATH=tmp+'/vault/server.vault',APPSOULS_KEY_PATH=tmp+'/keys/server.key',APPSOULS_SETUP_PRESET_PATH=tmp+'/setup/preset.json',APPSOULS_LISTEN_URLS='http://127.0.0.1:1',APPSOULS_BEHIND_NGINX='0',ASPNETCORE_ENVIRONMENT='Production',ASPNETCORE_PORT=str(port),ASPNETCORE_APPL_PATH='/',ASPNETCORE_TOKEN='isolated-iis-bridge-test',ASPNETCORE_IIS_HTTPAUTH='anonymous')
            with open(tmp+'/host.log','w') as log:
                process=subprocess.Popen(['dotnet',str(assembly)],env=env,stdout=log,stderr=subprocess.STDOUT,cwd=assembly.parents[3])
                try:
                    def request(token=None):
                        c=http.client.HTTPConnection('127.0.0.1',port,timeout=3)
                        try:
                            headers={'Host':'crm.example.test','X-Forwarded-Proto':'https','X-Forwarded-For':'203.0.113.8'}
                            if token:headers['MS-ASPNETCORE-TOKEN']=token
                            c.request('GET','/setup',headers=headers);r=c.getresponse();body=r.read();return r.status,r.getheader('Strict-Transport-Security'),body
                        finally:c.close()
                    deadline=time.monotonic()+30
                    while True:
                        try:status,_,_=request();break
                        except OSError:
                            if time.monotonic()>deadline or process.poll() is not None:raise
                            time.sleep(.1)
                    self.assertEqual(400,status,'The ANCM pairing token was not enforced.')
                    self.assertEqual(400,request('wrong-token')[0])
                    status,hsts,body=request('isolated-iis-bridge-test');self.assertEqual(200,status);self.assertIsNotNone(hsts);self.assertIn(b'App Souls',body)
                finally:
                    process.terminate()
                    try:process.wait(timeout=15)
                    except subprocess.TimeoutExpired:process.kill();process.wait()


if __name__=='__main__':unittest.main()
