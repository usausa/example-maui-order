#!/usr/bin/env python3
# 作業の単位の検証: ビルド (警告 0)、InspectCode (指摘 0)、改行コード、文書の改行
#
#   python .claude/skills/verify/scripts/verify.py [terminal] [files] [--no-inspect] [--fix] [--all-docs] [--out DIR]
#
# 対象を省くと terminal、files のすべてを行う。1 つでも問題があれば終了コードは 1
import argparse
import json
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from markdown_breaks import format_markdown

ROOT = Path(__file__).resolve().parents[4]

TERMINAL_SOLUTION = 'TableOrder.Terminal.slnx'

# 改行コードを見ない (バイナリ) ファイル
BINARY_SUFFIXES = {'.png', '.jpg', '.jpeg', '.gif', '.ico', '.ttf', '.otf', '.woff', '.woff2', '.pfx', '.snk', '.keystore', '.jar', '.db', '.zip', '.pdf'}

results = []


def report(name, ok, detail):
    results.append((name, ok, detail))
    print(f'{"OK" if ok else "NG"}  {name}: {detail}', flush=True)


def run(args, log_path):
    # 出力はログファイルに残し、画面には結果だけを出す
    with open(log_path, 'w', encoding='utf-8', errors='replace') as log:
        return subprocess.run(args, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=False).returncode


def diagnostics(path, kind):
    # ファイルロガーの行から同じ指摘の重複 (プロジェクトごとの行) を除く
    if not path.exists():
        return []
    lines = path.read_text(encoding='utf-8', errors='replace').splitlines()
    found = {re.sub(r'\s+\[[^\]]*\]$', '', line) for line in lines if f': {kind} ' in line}
    return sorted(found)


#--------------------------------------------------------------------------------
# Build
#--------------------------------------------------------------------------------

def build(solution, configuration, out):
    name = f'build {Path(solution).stem} ({configuration})'
    stem = f'{Path(solution).stem}-{configuration}'
    warn_log = out / f'{stem}.warnings.log'
    error_log = out / f'{stem}.errors.log'
    # 増分ビルドでは変わっていないプロジェクトの警告が出ないので、毎回作り直す
    code = run([
        'dotnet', 'build', solution, '-c', configuration, '--no-incremental', '-nologo',
        f'-flp1:logfile={warn_log};warningsonly;encoding=utf-8',
        f'-flp2:logfile={error_log};errorsonly;encoding=utf-8',
    ], out / f'{stem}.log')
    warnings = diagnostics(warn_log, 'warning')
    errors = diagnostics(error_log, 'error')
    report(name, code == 0 and not warnings and not errors, f'警告 {len(warnings)}、エラー {len(errors)} (ログ {out / (stem + ".log")})')
    for line in (errors + warnings)[:20]:
        print('    ' + line[:240])
    return code == 0 and not errors


#--------------------------------------------------------------------------------
# InspectCode
#--------------------------------------------------------------------------------

def inspect(solution, out):
    stem = Path(solution).stem
    sarif = out / f'{stem}.sarif'
    # キャッシュが残っていると古い解析結果を返すことがあるので、毎回新しい場所を使う
    caches = out / f'inspect-cache-{stem}-{time.strftime("%Y%m%d%H%M%S")}'
    code = run(['jb', 'inspectcode', solution, '--swea', f'--caches-home={caches}', f'-o={sarif}'], out / f'inspect-{stem}.log')
    if code != 0 or not sarif.exists():
        report(f'inspect {stem}', False, f'InspectCode が失敗しました (ログ {out / ("inspect-" + stem + ".log")})')
        return
    data = json.loads(sarif.read_text(encoding='utf-8-sig'))
    items = [r for run_ in data.get('runs', []) for r in run_.get('results', [])]
    report(f'inspect {stem}', not items, f'指摘 {len(items)}')
    for r in items[:40]:
        location = r['locations'][0]['physicalLocation']
        print(f'    {r["ruleId"]} {location["artifactLocation"]["uri"]}:{location.get("region", {}).get("startLine")} {r["message"]["text"][:160]}')


#--------------------------------------------------------------------------------
# Files
#--------------------------------------------------------------------------------

def changed_files():
    # (パス, 新しいファイルか) の一覧
    output = subprocess.run(['git', 'status', '--porcelain', '--untracked-files=all', '-z'], cwd=ROOT, capture_output=True, check=True).stdout.decode('utf-8')
    entries = output.split('\0')
    files = []
    i = 0
    while i < len(entries):
        entry = entries[i]
        i += 1
        if not entry:
            continue
        status, path = entry[:2], entry[3:]
        if status[0] in 'RC':
            i += 1  # 名前の変更と複製は元の名前が続く
        if 'D' in status:
            continue
        full = ROOT / path
        if full.is_file():
            files.append((path, status in ('??', 'A ', 'AM')))
    return files


def check_line_endings(files, fix):
    # 新しいファイルは CRLF。既存のファイルは元の改行コードのままにする (混ざっていたら NG)
    bad = []
    for path, new in files:
        full = ROOT / path
        if full.suffix.lower() in BINARY_SUFFIXES:
            continue
        data = full.read_bytes()
        if b'\0' in data[:8000]:
            continue
        crlf = data.count(b'\r\n')
        lf = data.count(b'\n') - crlf
        if lf == 0 or (not new and crlf == 0):
            continue
        bad.append(f'{path} (CRLF {crlf}、LF {lf}{"、新規" if new else ""})')
        if fix:
            normalized = data.replace(b'\r\n', b'\n')
            full.write_bytes(normalized.replace(b'\n', b'\r\n') if new or crlf >= lf else normalized)
    report('改行コード', not bad or fix, f'直すファイル {len(bad)}' + (' (直した)' if bad and fix else ''))
    for line in bad[:40]:
        print('    ' + line)


def check_markdown(files, fix, all_docs):
    # 規則ファイル (AGENTS.md、.claude/) は 1 項目 1 行なので対象外
    targets = [p for p, _ in files if p.endswith('.md') and (p.startswith('docs/') or p == 'README.md')]
    if all_docs:
        targets = sorted({str(p.relative_to(ROOT)).replace('\\', '/') for p in (ROOT / 'docs').glob('*.md')} | {'README.md'})
    bad = []
    for path in targets:
        full = ROOT / path
        text = full.read_bytes().decode('utf-8')
        formatted = format_markdown(text)
        if formatted != text:
            before = text.replace('\r\n', '\n').split('\n')
            after = formatted.replace('\r\n', '\n').split('\n')
            bad.append(f'{path} ({len(before)} 行 → {len(after)} 行)')
            if fix:
                full.write_bytes(formatted.encode('utf-8'))
    report('文書の改行', not bad or fix, f'直すファイル {len(bad)}' + (' (直した)' if bad and fix else '') + f' / 対象 {len(targets)}')
    for line in bad[:40]:
        print('    ' + line)


#--------------------------------------------------------------------------------
# Main
#--------------------------------------------------------------------------------

def main():
    # パイプやファイルへの出力は UTF-8 にする (Windows の既定のコードページでは文字化けする)
    if not sys.stdout.isatty():
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    parser = argparse.ArgumentParser(description='ビルド・InspectCode・改行コード・文書の改行を確かめる')
    parser.add_argument('targets', nargs='*', choices=['terminal', 'files'], help='省くとすべて')
    parser.add_argument('--no-inspect', action='store_true', help='InspectCode を省く (途中の確認用。作業の単位の検証では省かない)')
    parser.add_argument('--fix', action='store_true', help='改行コードと文書の改行を直す')
    parser.add_argument('--all-docs', action='store_true', help='変更のない文書も含めて docs/*.md と README.md の改行を確かめる')
    parser.add_argument('--out', help='ログの置き場所 (既定は一時フォルダの tableorder-verify)')
    args = parser.parse_args()
    targets = args.targets or ['terminal', 'files']
    out = Path(args.out) if args.out else Path(tempfile.gettempdir()) / 'tableorder-verify'
    out.mkdir(parents=True, exist_ok=True)
    print(f'ログ: {out}', flush=True)

    if 'terminal' in targets:
        build(TERMINAL_SOLUTION, 'Release', out)
        build(TERMINAL_SOLUTION, 'Debug', out)
        if not args.no_inspect:
            inspect(TERMINAL_SOLUTION, out)
    if 'files' in targets:
        files = changed_files()
        check_line_endings(files, args.fix)
        check_markdown(files, args.fix, args.all_docs)

    failed = [name for name, ok, _ in results if not ok]
    print('---')
    print('すべて OK' if not failed else 'NG: ' + '、'.join(failed))
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
