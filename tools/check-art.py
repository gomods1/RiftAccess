"""Offline coverage check: every game picture source the mod handles resolves to an English string."""
import os, re
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
en = {l.split(' = ', 1)[0] for l in open('RiftAccess/Lang/en.txt', encoding='utf-8') if ' = ' in l}
bad = []
# Tutorial videos
tv = open('RiftAccess/Menus/TutorialVideos.cs', encoding='utf-8').read()
vmap = dict((k, v) for k, v in re.findall(r'\{ "([a-z0-9_]+)", new\[\] \{ "([a-z0-9_]+)"', tv))
own = dict(re.findall(r'\{ "([a-z0-9_]+)", new\[\] \{ "own", "([a-z0-9_]+)"', tv))
clips = sorted({re.sub(r'_(720|480)_noaudio$', '', os.path.splitext(f)[0].lower().replace('tutorial_', '', 1)) for f in os.listdir('game/assets/video/tut')})
for c in clips:
    if c in own: ok = 'vid.' + own[c] in en
    elif c in vmap: ok = 'vid.' + vmap[c] in en
    else: ok = 'vid.' + c in en
    if not ok: bad.append('video ' + c)
# Bestiary names used by the video templates exist in the game's table
loc = open('game/loc/loc_dump.tsv', encoding='utf-8').read()
for name in set(re.findall(r'"([A-Z][A-Za-z]+)"', tv)):
    if 'NavItemDisplayName_Bestiary_' + name not in loc: bad.append('bestiary ' + name)
# Song portraits
ids = ['Cadence', 'Dove', 'NecrodancerBurger', 'NecrodancerCloak', 'NecrodancerCrypt', 'Heph', 'Merlin', 'Suzu', 'Nocturna', 'Shopkeeper', 'CadenceCrypt', 'Matron', 'Harmonie', 'Queen', 'Reaper', 'Coda', 'Beastmaster', 'Apricot', 'Banana', 'Cadence_Supporter', 'Banana02', 'Cherry', 'CustomCounterpart', 'CustomHero', 'CustomCounterpartNative', 'CustomHeroNative', 'CustomCounterpartYuzu']
sa = open('RiftAccess/Menus/StageArt.cs', encoding='utf-8').read()
pc = dict(re.findall(r'\{ "([a-z0-9_]+)", "(char\.[a-z_]+)" \}', sa))
for i in ids:
    k = pc.get(i.lower())
    if k is None and not i.startswith('Custom'): bad.append('portrait ' + i)
    elif k is not None and k not in en: bad.append('portrait key ' + k)
for k in re.findall(r'Loc\.T\("([a-z_.]+)"\)', sa):
    if k not in en: bad.append('stageart key ' + k)
# Boss minions
minions = ['AttackSoul', 'Bicorn', 'Bishop', 'BlackFireFakeoutL', 'BlackSkeleton', 'DodgeSoul', 'DuckSoul', 'Horse', 'Knight', 'Pawn', 'PurpleFire', 'PurpleFireL', 'RedBishop', 'RedFireDownL', 'RedKnight', 'RedPawn', 'RedRook', 'Rook', 'SkeletonHorde', 'Unicorn', 'WhiteShieldedSkeleton', 'WhiteSkeleton', 'YellowSkeleton']
for m in minions:
    t = m[:-1] if m.endswith(('L', 'R')) else m
    if 'mn.' + t.lower() not in en: bad.append('minion ' + m)
# Portraits map and aliases point at real keys
po = open('RiftAccess/Menus/Portraits.cs', encoding='utf-8').read()
for k in set(re.findall(r'"((?:char|mood|pose|art)\.[a-z0-9_]+)"', po)):
    if k not in en: bad.append('portraits key ' + k)
print('videos %d, portraits %d, minions %d, portrait/alias keys %d' % (len(clips), len(ids), len(minions), len(set(re.findall(r'"((?:char|mood|pose|art)\.[a-z0-9_]+)"', po)))))
print('MISSING:' if bad else 'all resolve', *bad, sep='\n  ')
