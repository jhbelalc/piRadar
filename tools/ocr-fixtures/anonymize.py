"""Anonimiza un OcrPage JSON antes de guardarlo como fixture.

uso: python3 anonymize.py <entrada.json> <salida.json>

Reemplaza correos personales, finales de tarjeta, CUFE/CUDE, números largos (socio, códigos de barras) y los
términos de private-terms.txt (junto a este script, ignorado por git): una línea por término, con el formato
    PALABRA              → se reemplaza por NN (nombres de clientes, cajeros, meseros)
    PALABRA=REEMPLAZO    → reemplazo fijo (p. ej. cédulas o el nombre de un titular persona natural)
    re:PATRÓN=REEMPLAZO  → expresión regular (p. ej. variantes de una cédula mal leídas por el OCR)
No se versiona esa lista: contiene justamente los datos que se quieren quitar.
"""
import json, os, re, sys, unicodedata

def fold(s):
    return ''.join(c for c in unicodedata.normalize('NFD', s) if unicodedata.category(c) != 'Mn').upper()

words, fixed, patterns = set(), {}, []
terms = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'private-terms.txt')
if os.path.exists(terms):
    for line in open(terms, encoding='utf-8'):
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        key, _, value = line.rpartition('=') if '=' in line else (line, '', '')
        if key.startswith('re:'):
            patterns.append((key[3:], value))
        elif value:
            fixed[key.strip()] = value.strip()
        else:
            words.add(fold(key))

def clean_word(w):
    f = fold(w).strip('.,:;')
    if f in words or any(f.startswith(n) and len(n) >= 6 for n in words):
        return 'NN'
    return w

def clean(text):
    t = text
    for pattern, value in patterns:
        t = re.sub(pattern, value, t)
    for key, value in fixed.items():
        t = re.sub(re.escape(key), value, t, flags=re.I)
    t = re.sub(r'[\w.+-]+@(?:yahoo|gmail|hotmail|outlook)\.\w+', 'cliente@example.com', t, flags=re.I)
    t = re.sub(r'(X{4,})\d{4}', r'\g<1>0000', t)                                   # finales de tarjeta
    t = re.sub(r'\b[0-9a-fA-F]{16,}\b', lambda m: '0' * len(m.group()), t)          # CUFE / CUDE
    def hexish(m):                                                                  # CUFE con letras mal leídas
        w = m.group()
        hexes = sum(c in '0123456789abcdefABCDEF' for c in w)
        return '0' * len(w) if hexes >= 0.7 * len(w) and any(c.isdigit() for c in w) and any(c.isalpha() for c in w) else w
    t = re.sub(r'[0-9A-Za-zеб]{10,}', hexish, t)
    # Números de 12+ dígitos (socio, códigos de barras); se conservan resoluciones DIAN y el 2222... de consumidor final.
    t = re.sub(r'[0-9A-Za-z]*\d{12,}[0-9A-Za-z]*',
               lambda m: m.group() if re.fullmatch(r'18764\d+|2{12}', m.group()) else '0' * len(m.group()), t)
    return ' '.join(':'.join(clean_word(p) for p in w.split(':')) for w in t.split(' '))

page = json.load(open(sys.argv[1], encoding='utf-8'))
for e in page['Elements']:
    e['Text'] = clean(e['Text'])
json.dump(page, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
