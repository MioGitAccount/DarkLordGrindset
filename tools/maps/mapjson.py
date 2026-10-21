# Writes map JSON exactly like the in-game map editor does (2-space indent, inline number arrays, CRLF).
import json
def dump(v, ind=0):
    pad = '  ' * ind
    if isinstance(v, dict):
        if not v: return '{}'
        items = [f'{pad}  {json.dumps(k)}: {dump(x, ind + 1)}' for k, x in v.items()]
        return '{\n' + ',\n'.join(items) + '\n' + pad + '}'
    if isinstance(v, list):
        if all(not isinstance(x, dict) for x in v):
            return json.dumps(v, separators=(',', ':'), ensure_ascii=False)
        return '[\n' + ',\n'.join(f'{pad}  {dump(x, ind + 1)}' for x in v) + '\n' + pad + ']'
    return json.dumps(v, ensure_ascii=False)
def write(m, path):
    open(path, 'wb').write(dump(m).replace('\n', '\r\n').encode('utf-8'))
