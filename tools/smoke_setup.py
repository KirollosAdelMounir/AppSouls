"""Verify first-time setup against a fresh, disposable localhost installation.
Usage: python3 tools/smoke_setup.py /absolute/private/setup-input.json http://localhost:5177
Writes an administrator/tenant and applies migrations. Never use against an existing deployment.
Required input: OrganizationName, DisplayName, Email, Password, PublicBaseUrl.
Optional: DatabaseProvider, DatabaseConnectionString, PendingAdministratorPath, or the guided
DatabaseServer, DatabasePort, DatabaseCatalog, DatabaseUserName, DatabasePassword, DatabaseUseTls fields.
Input stays outside the repository; no credentials are printed.
"""
import html
import http.cookiejar
import json
from pathlib import Path
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

base = sys.argv[2].rstrip('/')
if urllib.parse.urlparse(base).hostname not in ('localhost', '127.0.0.1', '::1'):
    raise SystemExit('This setup check is restricted to a disposable localhost installation.')
input_path = Path(sys.argv[1])
if input_path.stat().st_mode & 0o077:
    raise SystemExit('Setup verification input must be private (0600).')
values = json.loads(input_path.read_text())
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def get(path):
    with opener.open(base + path) as response:
        return response.url, response.read().decode()
def token(body):
    match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
    if not match:
        raise SystemExit('A setup verification token was unavailable.')
    return html.unescape(match.group(1))
def post(path, fields):
    with opener.open(urllib.request.Request(base + path, data=urllib.parse.urlencode(fields).encode())) as response:
        return response.url, response.read().decode()

try:
    _, body = get('/setup')
    fields = {name: values[name] for name in ('OrganizationName', 'DisplayName', 'Email', 'Password')}
    fields['ConfirmPassword'] = values['Password']
    fields['__RequestVerificationToken'] = token(body)
    _, body = post('/setup/administrator/prepare', fields)
    if 'Connect &amp; activate' not in body and 'Connect & activate' not in body:
        raise SystemExit('Administrator preparation did not reach the migration step.')
    pending = Path(values['PendingAdministratorPath']) if values.get('PendingAdministratorPath') else None
    if pending and not pending.exists():
        raise SystemExit('The administrator was not persisted before migration.')
    fields = {'__RequestVerificationToken': token(body), 'DatabaseProvider': values.get('DatabaseProvider', 'PostgreSql'),
              'DatabaseConnectionString': values.get('DatabaseConnectionString', ''), 'PublicBaseUrl': values['PublicBaseUrl'],
              'ConfirmMigration': 'true', 'EmailPort': '587', 'EmailHost': '', 'SenderAddress': '', 'EmailUserName': '', 'EmailPassword': ''}
    for name in ('DatabaseServer', 'DatabasePort', 'DatabaseCatalog', 'DatabaseUserName', 'DatabasePassword', 'DatabaseUseTls'):
        if name in values:
            fields[name] = str(values[name]).lower() if isinstance(values[name], bool) else values[name]
    _, body = post('/setup/migrate', fields)
    if 'Your system is ready' not in body:
        raise SystemExit('Database migration/activation did not complete. The prepared administrator is retained.')
    for _ in range(100):
        try:
            url, body = get('/account/login')
            if '/account/login' in url and 'Welcome to App Souls' in body:
                break
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(.25)
    else:
        raise SystemExit('The configured host did not become ready after activation.')
    if pending and pending.exists():
        raise SystemExit('Prepared administrator cleanup did not complete.')
    url, _ = post('/account/sign-in', {'__RequestVerificationToken': token(body), 'Email': values['Email'], 'Password': values['Password']})
    if '/account/' in url:
        raise SystemExit('The prepared administrator could not sign in after activation.')
    with opener.open(base + '/api/server-configuration') as response:
        json.loads(response.read())
        if response.status != 200:
            raise SystemExit('The seeded administrator cannot access platform settings.')
    print('Prepared administrator -> migrations -> seed -> activation -> administrator sign-in passed. No credentials printed.')
except urllib.error.HTTPError as error:
    raise SystemExit(f'Setup verification received HTTP {error.code}. No credentials printed.')
