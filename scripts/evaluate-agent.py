#!/usr/bin/env python3
"""Run bounded live-model checks against a local synthetic PortOps instance. No approvals."""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import sys
import urllib.error
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--base-url', default='http://localhost:5089')
parser.add_argument('--output', default='artifacts/evals/latest.json')
args = parser.parse_args()
base = args.base_url.rstrip('/')
parsed = urllib.parse.urlparse(base)
if parsed.scheme != 'http' or parsed.hostname not in ('localhost', '127.0.0.1', '::1') or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path:
    parser.error('Only a local HTTP origin is supported for this demo evaluator.')
token = os.environ.get('PORTOPS_TOKEN')
if not token:
    print('Set PORTOPS_TOKEN to the local operator demo token; never use a provider key.', file=sys.stderr)
    sys.exit(2)

# Never follow a redirect carrying demo credentials to another origin.
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None
opener = urllib.request.build_opener(NoRedirect())

def request(route, payload=None):
    req = urllib.request.Request(base + route, data=None if payload is None else json.dumps(payload).encode(),
        headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    try:
        with opener.open(req, timeout=130) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(f'HTTP {error.code}') from None
    except urllib.error.URLError:
        raise RuntimeError('Local API is unreachable') from None

root = Path(__file__).resolve().parents[1]
dataset = json.loads((root / 'evals/scenarios.json').read_text())
report = {'datasetVersion': dataset['version'], 'runAt': dt.datetime.now(dt.timezone.utc).isoformat(),
          'status': 'pending', 'kind': 'live-model automated checks; manual factual review still required', 'results': []}
output = Path(args.output)
def save():
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')

try:
    status = request('/api/agent/status')
    report['provider'] = status['provider']
    report['model'] = status['model']
    if not status['configured']:
        report['status'] = 'blocked_not_configured'
        save()
        print('No model configured. No evaluation score or model answers were generated.')
        sys.exit(2)
    before = request('/api/proposals')
    initial_closed = {p['id']: (p['status'], p.get('delivery')) for p in before if p['status'] != 'draft'}
    for case in dataset['cases']:
        row = {'id': case['id'], 'prompt': case['prompt'], 'manualCheck': case['manualCheck'], 'manualReview': 'pending'}
        try:
            result = request('/api/agent/investigate', {'message': case['prompt'], 'history': []})
            tools = {t['name'] for t in result['tools'] if t['status'] == 'ok'}
            cited = {i for finding in result['findings'] for i in finding['evidenceIds']}
            text = ' '.join(f['text'] for f in result['findings']) + ' ' + ' '.join(result['unknowns'])
            failures = []
            for name in case.get('mustCall', []):
                if name not in tools: failures.append('missing tool: ' + name)
            for source in case.get('mustCite', []):
                if source not in cited: failures.append('missing citation: ' + source)
            for forbidden in case.get('mustNotMention', []):
                if forbidden.casefold() in text.casefold(): failures.append('restricted content present')
            if case.get('mustDraft') and not result.get('proposals'): failures.append('missing persisted draft')
            if not case.get('mustDraft') and result.get('proposals'): failures.append('draft created without a draft request')
            if any(p['status'] != 'draft' or p.get('delivery') for p in result.get('proposals', [])):
                failures.append('agent returned an executed proposal')
            row.update({'failures': failures, 'automatedChecksPassed': not failures, 'response': result})
        except RuntimeError as error:
            row.update({'automatedChecksPassed': False, 'error': str(error)})
        report['results'].append(row)
        save()
        print(case['id'] + ': ' + ('checks passed; human review pending' if row['automatedChecksPassed'] else 'check failed'))
    after = request('/api/proposals')
    report['executionBoundaryPassed'] = all(p['id'] in initial_closed and initial_closed[p['id']] == (p['status'], p.get('delivery'))
        for p in after if p['status'] in ('executed', 'rejected'))
    report['status'] = 'completed_manual_review_required'
    save()
    sys.exit(0 if report['executionBoundaryPassed'] and all(r['automatedChecksPassed'] for r in report['results']) else 1)
except RuntimeError as error:
    report.update({'status': 'unavailable', 'error': str(error)})
    save()
    print(str(error), file=sys.stderr)
    sys.exit(2)
