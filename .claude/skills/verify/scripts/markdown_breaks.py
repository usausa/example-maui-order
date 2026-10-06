# 文書の改行: 1 行 1 文にし、「。」の後を 2 スペース + 改行にする
# (段落・箇条書き・引用だけ。表・見出し・コードブロック・HTML は対象外)
import re

# 「。」の直後に続いてよい閉じ括弧
CLOSERS = '」』）)]'
SENTENCE_END = re.compile('。[' + re.escape(CLOSERS) + ']*$')
LIST_MARKER = re.compile(r'^([-*+] (?:\[[ x]\] )?|\d+\. )')


def split_sentences(text):
    # コードスパンとリンク・括弧の中では区切らない
    pieces = []
    in_code = False
    start = 0
    i = 0
    while i < len(text):
        ch = text[i]
        if ch == '`':
            in_code = not in_code
        elif ch == '。' and not in_code:
            j = i + 1
            while j < len(text) and text[j] in CLOSERS:
                j += 1
            before = text[start:j]
            if text[j:].strip() and before.count('[') == before.count(']') and before.count('(') == before.count(')'):
                pieces.append(before.rstrip())
                start = j
                while start < len(text) and text[start] == ' ':
                    start += 1
                i = start
                continue
        i += 1
    pieces.append(text[start:])
    return pieces


def continues_paragraph(next_line):
    stripped = next_line.lstrip(' ')
    if not stripped:
        return False
    if stripped.startswith(('|', '#', '```', '~~~', '>')):
        return False
    return LIST_MARKER.match(stripped) is None


def format_lines(lines):
    out = []
    in_code = False
    for index, line in enumerate(lines):
        stripped = line.lstrip(' ')
        if stripped.startswith(('```', '~~~')):
            in_code = not in_code
            out.append(line)
            continue
        if in_code or not stripped or stripped.startswith(('|', '#', '<')):
            out.append(line)
            continue
        indent = line[:len(line) - len(stripped)]
        prefix = ''
        content = stripped
        quote = re.match(r'^(> ?)', content)
        if quote:
            prefix = quote.group(1)
            content = content[quote.end():]
        marker = LIST_MARKER.match(content)
        if marker:
            content = content[marker.end():]
            first_prefix = indent + prefix + marker.group(1)
            next_prefix = indent + prefix + (' ' * len(marker.group(1)))
        else:
            first_prefix = indent + prefix
            next_prefix = indent + prefix
        pieces = split_sentences(content.rstrip())
        continues = continues_paragraph(lines[index + 1] if index + 1 < len(lines) else '')
        for k, piece in enumerate(pieces):
            text = piece.rstrip()
            # 文の途中の区切りと、次の行へ続く文の終わり (閉じ括弧付きも) は 2 スペースで改行する
            if (k < len(pieces) - 1) or (continues and SENTENCE_END.search(text)):
                text += '  '
            out.append((first_prefix if k == 0 else next_prefix) + text)
    return out


def format_markdown(text):
    crlf = '\r\n' in text
    lines = text.replace('\r\n', '\n').split('\n')
    result = '\n'.join(format_lines(lines))
    return result.replace('\n', '\r\n') if crlf else result
