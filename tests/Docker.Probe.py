"""Private inputs arrive over stdin. Trust only the generated QA TLS certificate."""
import json
import ssl
import sys
import time
import urllib.request
import urllib.error

settings = json.load(sys.stdin)
context = ssl.create_default_context(cafile=settings['certificate'])

def request(path, token=None, body=None, secure=True):
    url = ('https://localhost:15501' if secure else 'http://127.0.0.1:15500') + path
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(url, data=json.dumps(body).encode() if body is not None else None, headers=headers)
    try:
        response = urllib.request.urlopen(req, context=context, timeout=5)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        data = response.read()
        return response.status, json.loads(data) if data else None

def check(condition, message):
    if not condition: raise AssertionError(message)
    print('PASS ' + message)

for attempt in range(60):
    try:
        if request('/health')[0] == 200: break
    except (OSError, urllib.error.URLError):
        pass
    time.sleep(0.25)
else:
    raise RuntimeError('Container HTTPS health endpoint did not become ready')
check(True, 'Container HTTPS health with certificate and hostname validation')
check(request('/health', secure=False)[0] == 400, 'Remote cleartext HTTP is rejected')
check(request('/admin/users')[0] == 401, 'Anonymous administration is rejected in container')
status, session = request('/login', body={'username':'docker-admin','password':settings['password']})
check(status == 200, 'Explicit bootstrap account authenticates over HTTPS')
token = session['token']
check(request('/machines', token)[1] == [], 'Container installation contains no demo machines')
check(request('/admin/users', token)[1]['totalCount'] == 1, 'Only the explicitly created administrator exists')
print('SUCCESS: 6 Docker HTTPS checks passed. Windows/AD/installer testing is separate.')
