"""Simulation grossière de Lucidité pour l'équilibrage (joueuse IA simple)."""
import random, sys, math

# --- cartes joueuse : name, cost, dmg, hits, blk, draw, weak, vuln, str, exhaust, upDmg, upBlk
def card(n, c, d=0, h=1, b=0, dr=0, w=0, v=0, s=0, ex=False, ud=0, ub=0, crisis=None):
    return dict(n=n, c=c, d=d, h=h, b=b, dr=dr, w=w, v=v, s=s, ex=ex, ud=ud, ub=ub, crisis=crisis, up=False)

STARTER = [card("SeLever", 1, d=6, ud=3)] * 5 + [card("Respirer", 1, b=5, ub=3)] * 4 + [card("Crier", 2, d=8, v=2, ud=2)]
SHOP = [card("Marcher", 1, d=8, dr=1, ud=2), card("Poings", 1, d=3, h=3, ud=1), card("Nommer", 1, d=6, w=2, ud=2),
        card("Therapie", 2, b=12, dr=1, ub=4), card("Tenir", 1, b=7, ub=2), card("Ancrage", 1, b=6, w=1, ub=2),
        card("Couverture", 0, b=4, ub=3), card("Rage", 2, d=14, ud=4), card("Traitement", 2, b=18, ex=True, ub=6),
        card("Cri", 0, d=9, ex=True, ud=3), card("Souvenir", 1, s=2, ex=True)]
CRISES = {"doute": card("Doute", 99, crisis="doute"), "nuit": card("Nuit", 99, crisis="nuit"),
          "rechute": card("Rechute", 99, crisis="rechute"), "honte": card("Honte", 99, crisis="honte")}

# --- actions de la Dépression : name, dmg, hits, blk, str, weak, vuln, crisis(n, kind, toDraw), cost, minRound, weight, shopOnly
def mv(n, d=0, h=1, b=0, s=0, w=0, v=0, cr=0, ck=None, cost=1, mr=1, wt=1.0, shop=False):
    return dict(n=n, d=d, h=h, b=b, s=s, w=w, v=v, cr=cr, ck=ck, cost=cost, mr=mr, wt=wt, shop=shop)

def moves(cfg):
    M = cfg.get("moves")
    if M: return M
    return [mv("Idees", d=9, wt=1.4), mv("Spirale", d=4, h=2, wt=1.1), mv("Poids", d=5, b=7),
            mv("Intrusives", d=4, cr=2, ck="doute", wt=0.9), mv("Insomnie", w=2, cr=1, ck="nuit", wt=0.8),
            mv("Murmure", d=3, v=2, wt=0.8), mv("Vide", b=12, wt=0.7), mv("Culpa", s=2, wt=0.7),
            mv("Rechute", d=7, cr=1, ck="rechute", cost=2, mr=2, wt=0.8), mv("Honte", d=6, cr=1, ck="honte", mr=2, wt=0.7),
            mv("Gouffre", d=17, cost=2, mr=2, wt=0.6), mv("Cicatrices", d=6, h=3, cost=2, mr=3, wt=0.7),
            mv("Desespoir", s=3, b=10, cost=2, mr=3, wt=0.6),
            mv("Angoisse", d=4, h=3, cost=2, mr=2, wt=0.9, shop=True), mv("Effondrement", d=22, cost=3, mr=3, wt=0.7, shop=True),
            mv("Emprise", w=2, v=2, cr=2, ck="doute", cost=1, mr=2, wt=0.8, shop=True), mv("Isolement", b=14, s=1, cost=1, mr=2, wt=0.8, shop=True)]

def atk(base, a, t):
    d = base + a["str"]
    if a["weak"] > 0: d *= 0.75
    if t["vuln"] > 0: d *= 1.5
    return max(0, math.floor(d))

def val(c, k):
    return c[k] + (c["u" + k] if c["up"] and ("u" + k) in c else 0)

class Enemy:
    def __init__(s, cfg):
        s.cfg = cfg; s.moves = moves(cfg)
        s.bonusHp = s.startStr = s.bonusEn = s.startBlk = 0
        s.bonus = {}; s.unlocked = set(); s.blood = cfg.get("eStartBlood", 0); s.bought = []

    def avail(s, m, r): return (m["n"] in s.unlocked) if m["shop"] else m["mr"] <= r

    def plan(s, r, energy, hist):
        cfg = s.cfg; plan = []; rem = max(1, energy); attacked = False
        for n in range(cfg["eMaxCards"]):
            pool = []
            for m in s.moves:
                if not s.avail(m, r) or m["cost"] > rem: continue
                h = len(hist)
                if (h >= 2 and hist[-1] is m and hist[-2] is m) or (h >= 1 and hist[-1] is m and m["d"] == 0): continue
                pool.append(m)
            if not pool: break
            def W(m):
                w = m["wt"]
                if n > 0 and attacked and m["d"] > 0: w *= 0.6
                if n > 0 and not attacked and m["d"] > 0: w *= 1.5
                return w
            pick = random.choices(pool, [W(m) for m in pool])[0]
            hist.append(pick)
            sc = (1 + cfg["eScale"] * (r - 1)) * (1 + s.bonus.get(pick["n"], 0))
            plan.append(dict(m=pick, d=round(pick["d"] * sc), h=pick["h"], b=round(pick["b"] * sc)))
            attacked |= pick["d"] > 0; rem -= pick["cost"]
            if rem <= 0: break
        return plan

    # ancienne boutique : n renforcements gratuits
    def perks(s, n, r):
        for _ in range(n):
            opts = ["hp", "str", "blk"] + (["en"] if s.bonusEn < 2 and r >= 2 else [])
            sh = [m for m in s.moves if s.avail(m, r + 1) and m["d"] > 0]
            if sh: opts.append("sharp")
            lk = [m for m in s.moves if m["shop"] and m["n"] not in s.unlocked and m["mr"] <= r + 1]
            if lk: opts += ["new", "new"]
            s.apply(random.choice(opts), r, sh, lk)

    def apply(s, o, r, sh=None, lk=None):
        c = s.cfg
        if o == "hp": s.bonusHp += c.get("pHp", 10)
        elif o == "str": s.startStr += 1
        elif o == "blk": s.startBlk += c.get("pBlk", 8)
        elif o == "en": s.bonusEn += 1
        elif o == "sharp":
            m = random.choice(sh); s.bonus[m["n"]] = s.bonus.get(m["n"], 0) + c.get("pSharp", 0.3)
        elif o == "new": s.unlocked.add(random.choice(lk)["n"])
        s.bought.append(o)

    # nouvelle boutique : gouttes de sang
    def shop(s, r):
        c = s.cfg; P = c["ePrices"]
        while True:
            opts = []
            if s.bonusHp < c.get("capHp", 99): opts.append(("hp", P["hp"]))
            if s.startStr < c.get("capStr", 99): opts.append(("str", P["str"]))
            if s.startBlk < c.get("capBlk", 99): opts.append(("blk", P["blk"]))
            if s.bonusEn < c.get("capEn", 1) and r >= 2: opts.append(("en", P["en"]))
            sh = [m for m in s.moves if s.avail(m, r + 1) and m["d"] > 0 and s.bonus.get(m["n"], 0) < 0.5]
            if sh: opts.append(("sharp", P["sharp"]))
            lk = [m for m in s.moves if m["shop"] and m["n"] not in s.unlocked and m["mr"] <= r + 1]
            if lk: opts += [("new", P["new"])] * 2
            ok = [o for o in opts if o[1] <= s.blood]
            if not ok: break
            # épargne parfois pour l'énergie
            if any(o[0] == "en" for o in opts) and not any(o[0] == "en" for o in ok) and random.random() < 0.5: break
            o, p = random.choice(ok); s.blood -= p; s.apply(o, r, sh, lk)

def run(cfg, verbose=False):
    deck = [dict(c) for c in STARTER]
    E = Enemy(cfg)
    pC = eC = 3; blood = cfg["startBlood"]; r = 0; rounds = []
    while pC > 0 and eC > 0:
        r += 1
        # --- manche
        pmax = 50; P = dict(hp=pmax, blk=0, str=0, weak=0, vuln=0)
        ehp = cfg["eBase"] + (r - 1) * cfg["eHpR"] + E.bonusHp
        X = dict(hp=ehp, blk=E.startBlk, str=E.startStr, weak=0, vuln=0)
        een = min(cfg["eMaxEn"], cfg["eEnBase"] + math.floor((r - 1) * cfg["eEnR"])) + E.bonusEn
        draw = [dict(c) for c in deck]; random.shuffle(draw); disc = []; hist = []
        turn = 0; spilled = 0; taken = 0; hand = []; nextDraw = 0
        while P["hp"] > 0 and X["hp"] > 0 and turn < 40:
            turn += 1
            if turn > 1: P["blk"] = 0
            energy = 3
            plan = E.plan(r, een, hist)
            if not cfg.get("keep"): hand = []
            # Pioche normale jusqu'à la limite de main ; pioche bonus (cartes « pioche ») au-delà (plafond absolu 12)
            for k in range(5 + nextDraw):
                bonus = k >= 5
                if len(hand) >= (cfg.get("absMax", 12) if bonus else cfg.get("limit", 10)):
                    if bonus: break
                    continue
                if not draw: draw, disc = disc, []; random.shuffle(draw)
                if not draw: break
                c = draw.pop(); hand.append(c)
                if c["crisis"] == "honte": energy = max(0, energy - 1)
            nextDraw = 0
            # incoming
            inc = sum(atk(i["d"], X, P) * i["h"] for i in plan if i["d"] > 0)
            # IA : blocs jusqu'à couvrir l'entrée (sauf si on peut tuer), puis attaques
            playable = [c for c in hand if c["crisis"] is None]
            chosen = []; en = energy
            def dmgOf(c): return atk(val(c, "d"), P, X) * c["h"] if c["d"] else 0
            potential = 0
            for c in sorted(playable, key=lambda c: -dmgOf(c) / max(1, c["c"])):
                pass
            attacks = sorted([c for c in playable if c["d"] > 0 or c["s"] > 0], key=lambda c: -(dmgOf(c) + 1) / max(0.5, c["c"]))
            blocks = sorted([c for c in playable if c["b"] > 0 and c["d"] == 0], key=lambda c: -val(c, "b") / max(0.5, c["c"]))
            # tuer ?
            tot = 0; e2 = en; killset = []
            for c in attacks:
                if c["c"] <= e2: killset.append(c); e2 -= c["c"]; tot += dmgOf(c)
            if tot >= X["hp"] + X["blk"]:
                chosen = killset
            else:
                need = inc - P["blk"]
                for c in blocks:
                    if need <= 0: break
                    if c["c"] <= en: chosen.append(c); en -= c["c"]; need -= val(c, "b")
                for c in attacks:
                    if c["c"] <= en: chosen.append(c); en -= c["c"]
                for c in blocks:
                    if c not in chosen and c["c"] <= en: chosen.append(c); en -= c["c"]
            nextDraw = sum(c["dr"] for c in chosen)
            for c in chosen:
                if X["hp"] <= 0: break
                if c["d"] > 0:
                    for _ in range(c["h"]):
                        d = atk(val(c, "d"), P, X); b = min(X["blk"], d); X["blk"] -= b; l = min(X["hp"], d - b); X["hp"] -= l; spilled += l
                if c["b"] > 0: P["blk"] += val(c, "b")
                X["vuln"] += c["v"]; X["weak"] += c["w"]; P["str"] += c["s"]
            kept = []
            for c in hand:
                if c in chosen and c["ex"]: continue
                if c["crisis"] == "doute" and c not in chosen: continue  # éphémère
                if c["crisis"] == "rechute": P["hp"] -= 3; taken += 3
                # Règle « main conservée » : les cartes non jouées restent, sauf les Crises injouables
                if cfg.get("keep") and c not in chosen and c["crisis"] in (None, "nuit"): kept.append(c); continue
                disc.append(c)
            hand = kept
            P["weak"] = max(0, P["weak"] - 1); P["vuln"] = max(0, P["vuln"] - 1)
            if X["hp"] <= 0 or P["hp"] <= 0: break
            # tour de la Dépression
            X["blk"] = 0
            for i in plan:
                m = i["m"]; X["blk"] += i["b"]; X["str"] += m["s"]
                for _ in range(i["h"]):
                    if i["d"] <= 0: break
                    d = atk(i["d"], X, P); b = min(P["blk"], d); P["blk"] -= b; l = min(P["hp"], d - b); P["hp"] -= l; taken += l
                P["weak"] += m["w"]; P["vuln"] += m["v"]
                for _ in range(m["cr"]):
                    cc = dict(CRISES[m["ck"]])
                    if m["n"] == "Rechute": draw.insert(random.randint(0, len(draw)), cc)
                    else: disc.append(cc)
                if P["hp"] <= 0: break
            X["weak"] = max(0, X["weak"] - 1); X["vuln"] = max(0, X["vuln"] - 1)
        won = X["hp"] <= 0 and P["hp"] > 0
        if won: eC -= 1
        else: pC -= 1
        rounds.append((r, won, turn, P["hp"], een))
        b = cfg["win"] if won else cfg["loss"]
        if won: b += P["hp"] // 2
        b += math.floor(spilled * 0.6); blood += b
        if pC == 0 or eC == 0: break
        # boutique de la Dépression
        if cfg["mode"] == "perks":
            E.perks(cfg["perks"] + (0 if won else 1), r)
        else:
            eb = cfg["eWin"] if not won else cfg["eLoss"]
            eb += math.floor(taken * cfg.get("eBloodPerDmg", 0.6)); E.blood += round(eb * cfg.get("eBloodScale", 1))
            E.shop(r)
        # boutique joueuse : amélioration + cartes
        ups = [c for c in deck if not c["up"] and c["crisis"] is None and (c["d"] or c["b"])]
        if blood >= 50 and ups:
            c = random.choice([c for c in ups if c["n"] in ("SeLever", "Crier", "Respirer")] or ups); c["up"] = True; blood -= 50
        offers = random.sample(SHOP, 3)
        for o in offers:
            if blood >= 55: deck.append(dict(o)); blood -= 55
        if blood >= 60 and len(deck) > 8 and random.random() < 0.5:
            for c in deck:
                if c["n"] == "Respirer" and not c["up"]: deck.remove(c); blood -= 60; break
    if verbose: print(rounds, E.bought)
    return pC > 0, r, rounds

OLD = dict(mode="perks", eBase=42, eHpR=12, eScale=0.2, eEnBase=1, eEnR=1.0, eMaxEn=5, eMaxCards=3, perks=1,
           startBlood=60, win=80, loss=50)

def report(name, cfg, n=4000):
    wins = 0; rr = []; per = {}
    for _ in range(n):
        w, r, rounds = run(cfg)
        wins += w; rr.append(r)
        for (ri, rw, t, hp, en) in rounds:
            a = per.setdefault(ri, [0, 0, 0]); a[0] += 1; a[1] += rw; a[2] += t
    print(f"{name}: victoire {100*wins/n:.0f} %  manches moy {sum(rr)/n:.1f}")
    print("   " + "  ".join(f"M{k}: {100*v[1]/v[0]:.0f}% ({v[2]/v[0]:.1f} t)" for k, v in sorted(per.items())))

if __name__ == "__main__":
    random.seed(1)
    report("ACTUEL", OLD)
