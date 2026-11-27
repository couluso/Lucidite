import random, sys
sys.path.insert(0, ".")
from sim import *
PR = dict(hp=50, str=80, blk=50, sharp=55, new=70, en=150)
F = dict(mode="blood", eBase=52, eHpR=9, eScale=0.09, eEnBase=1, eEnR=1, eMaxEn=2, eMaxCards=3,
            startBlood=60, win=80, loss=50, eStartBlood=60, eWin=80, eLoss=50, eBloodScale=1.0, ePrices=PR,
            pHp=8, pBlk=6, pSharp=0.25, capEn=1, keep=True, limit=7)
random.seed(6)
report("FINAL", F, 6000)

c = dict(F); c["keep"] = False; report("  (sans garder la main)", c, 3000)
