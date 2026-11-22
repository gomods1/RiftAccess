"""Checks every RiftAccess/Lang/*.txt against en.txt: same keys, same {n} placeholders, no BOM, no bad lines."""
import re, glob, os, sys
os.chdir(os.path.join(os.path.dirname(__file__), '..', 'RiftAccess', 'Lang'))
def load(p):
    d = {}
    for l in open(p, encoding='utf-8').read().split('\n'):
        l = l.rstrip('\r')
        if not l or l.lstrip().startswith('#'): continue
        if ' = ' not in l: print(p, 'BAD LINE', l[:60]); continue
        k, v = l.split(' = ', 1); d[k.strip()] = v
    return d
en = load('en.txt'); ok = True
for f in sorted(glob.glob('*.txt')):
    d = load(f)
    miss = [k for k in en if k not in d]; extra = [k for k in d if k not in en]
    ph = [k for k in en if k in d and sorted(re.findall(r'\{\d\}', en[k])) != sorted(re.findall(r'\{\d\}', d[k]))]
    bom = open(f, 'rb').read(3) == b'\xef\xbb\xbf'
    bad = miss or extra or ph or bom; ok &= not bad
    print(f"{f:12} {len(d)} keys" + (f" MISSING {miss[:5]} EXTRA {extra[:5]} PLACEHOLDERS {ph[:5]} BOM {bom}" if bad else " ok"))
sys.exit(0 if ok else 1)
