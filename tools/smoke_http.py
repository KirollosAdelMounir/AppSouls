"""Local HTTP verification. Reads an existing private login file; never prints secrets.
Usage: python3 tools/smoke_http.py /absolute/path/private-login.json [http(s)://host]
Run only against a designated development/test workspace. Creates no business records.
"""
import html
import http.cookiejar
import json
from pathlib import Path
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

credentials = json.loads(Path(sys.argv[1]).read_text())
base = sys.argv[2] if len(sys.argv) > 2 else "http://localhost:5176"
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
with opener.open(base + "/account/login") as response:
    page = response.read().decode()
match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
if not match:
    raise SystemExit("Login form verification token was unavailable.")
fields = {"Email": credentials["Email"], "Password": credentials["Password"], "ReturnUrl": "/", "__RequestVerificationToken": html.unescape(match.group(1))}
with opener.open(urllib.request.Request(base + "/account/sign-in", data=urllib.parse.urlencode(fields).encode())) as response:
    if "/account/login" in response.url:
        raise SystemExit("Sign-in failed; no business records changed.")
    continuation = response.read().decode()
    if "/account/continue" in response.url:
        choices = re.findall(r'<button[^>]*name="MembershipId"[^>]*value="([^"]+)"[^>]*>(.*?)</button>', continuation)
        workspace = credentials.get("WorkspaceName")
        selected = [value for value, label in choices if html.unescape(label).startswith(str(workspace) + " · ")] if workspace else []
        if len(selected) != 1:
            raise SystemExit("A unique WorkspaceName is required for multiple memberships. Enrolled MFA requires a separate test flow.")
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', continuation)
        if not token:
            raise SystemExit("Workspace verification token was unavailable.")
        try:
            opener.open(base + "/api/workspace")
        except urllib.error.HTTPError as error:
            if error.code != 401:
                raise SystemExit("Pending sign-in did not receive the expected API rejection.")
        else:
            raise SystemExit("Pending sign-in unexpectedly granted application access.")
        invalid = urllib.parse.urlencode({"MembershipId": "00000000-0000-0000-0000-000000000001", "__RequestVerificationToken": html.unescape(token.group(1))}).encode()
        with opener.open(urllib.request.Request(base + "/account/complete-sign-in", data=invalid)) as rejected:
            if "/account/continue" not in rejected.url:
                raise SystemExit("An invalid workspace selection was unexpectedly accepted.")
            continuation = rejected.read().decode()
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', continuation)
        if not token:
            raise SystemExit("Workspace verification token was unavailable after rejection.")
        data = urllib.parse.urlencode({"MembershipId": selected[0], "__RequestVerificationToken": html.unescape(token.group(1))}).encode()
        with opener.open(urllib.request.Request(base + "/account/complete-sign-in", data=data)) as completed:
            if "/account/" in completed.url:
                raise SystemExit("Workspace sign-in could not be completed.")
            completed.read()
for path in ["/api/workspace/", "/api/support/", "/api/licensing/"]:
    with opener.open(base + path) as response:
        json.loads(response.read())
        print(path, response.status)
# Antiforgery filters must reject the request before any business operation runs.
request = urllib.request.Request(base + "/api/workspace/customers", data=b"{}", headers={"Content-Type": "application/json"})
try:
    opener.open(request)
except urllib.error.HTTPError as error:
    if error.code != 400:
        raise SystemExit(f"Expected antiforgery rejection, received {error.code}.")
    problem = error.read().decode()
    if "validation errors" in problem.lower():
        raise SystemExit("Model validation ran instead of the expected antiforgery rejection.")
else:
    raise SystemExit("Mutation without antiforgery was unexpectedly accepted.")
print("Authenticated reads and antiforgery rejection passed; no secrets or business records were written.")
