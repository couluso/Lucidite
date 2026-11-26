using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Lucidite.EditorTools.AssetFactory;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Crée (ou met à jour en place) les cartes, les actions de la Dépression, les objets et la configuration.
    /// Système à énergie : coût, dégâts, bouclier, statuts.
    /// </summary>
    public static class DataFactory
    {
        static int s_arcana;
        static readonly HashSet<string> s_paths = new HashSet<string>();
        static readonly string[] Roman =
        { "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX", "XXI" };

        /// <summary>Petit constructeur fluide pour lisibilité des définitions.</summary>
        class C
        {
            public readonly CardData d;
            public C(string id, string name, CardType type, CardRarity rarity, CardTheme theme, int cost, string flavor)
            {
                d = LoadOrCreate<CardData>($"{CardDir}/{id}.asset");
                s_paths.Add($"{CardDir}/{id}.asset");
                d.cardName = name; d.type = type; d.rarity = rarity; d.theme = theme; d.cost = cost; d.flavorText = flavor;
                d.arcana = Roman[s_arcana++ % Roman.Length];
                d.unplayable = false;
                d.damage = 0; d.hits = 1; d.block = 0; d.draw = 0; d.energyGain = 0; d.heal = 0;
                d.applyWeak = 0; d.applyVulnerable = 0; d.strengthGain = 0; d.bloodGain = 0;
                d.special = CardSpecial.None; d.specialValue = 0;
                d.exhaust = false; d.ethereal = false; d.loseLucidityIfHeld = 0; d.energyLossOnDraw = 0;
                d.upCost = 0; d.upDamage = 0; d.upBlock = 0; d.upDraw = 0; d.upMagic = 0; d.upRemovesExhaust = false;
                d.priceOverride = 0; d.evolvesInto = null; d.transformsInto = null;
                d.inShop = rarity == CardRarity.Commune || rarity == CardRarity.PeuCommune || rarity == CardRarity.Rare;
                EditorUtility.SetDirty(d);
            }
            public C Dmg(int v, int up = 0, int hits = 1) { d.damage = v; d.upDamage = up; d.hits = hits; return this; }
            public C Blk(int v, int up = 0) { d.block = v; d.upBlock = up; return this; }
            public C Draw(int v, int up = 0) { d.draw = v; d.upDraw = up; return this; }
            public C Energy(int v) { d.energyGain = v; return this; }
            public C Heal(int v) { d.heal = v; return this; }
            public C Weak(int v) { d.applyWeak = v; return this; }
            public C Vuln(int v) { d.applyVulnerable = v; return this; }
            public C Str(int v) { d.strengthGain = v; return this; }
            public C Blood(int v) { d.bloodGain = v; return this; }
            public C Magic(int up) { d.upMagic = up; return this; }
            public C UpCost(int v) { d.upCost = v; return this; }
            public C Special(CardSpecial s, int v = 0) { d.special = s; d.specialValue = v; return this; }
            public C Exhaust(bool removedOnUpgrade = false) { d.exhaust = true; d.upRemovesExhaust = removedOnUpgrade; return this; }
            public C Ethereal() { d.ethereal = true; return this; }
            public C Unplayable() { d.unplayable = true; return this; }
            public C Held(int v) { d.loseLucidityIfHeld = v; return this; }
            public C DrawPain(int v) { d.energyLossOnDraw = v; return this; }
            public C NotInShop() { d.inShop = false; return this; }
        }

        static EnemyMoveData Move(string id, string name, string arcana, CardTheme theme, string flavor, int minRound = 1, float weight = 1f)
        {
            var m = LoadOrCreate<EnemyMoveData>($"{DataDir}/Moves/{id}.asset");
            m.moveName = name; m.arcana = arcana; m.theme = theme; m.flavorText = flavor;
            m.damage = 0; m.hits = 1; m.block = 0; m.strengthGain = 0; m.applyWeak = 0; m.applyVulnerable = 0;
            m.crisisCount = 0; m.crisisToDrawPile = false; m.specificCrisis = null;
            m.minRound = minRound; m.weight = weight;
            m.cost = 1; m.shopOnly = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static ItemData Item(string id, string name, ItemKind kind, ItemEffect effect, int value, int price, string hexTint, string flavor)
        {
            var i = LoadOrCreate<ItemData>($"{ItemDir}/{id}.asset");
            s_paths.Add($"{ItemDir}/{id}.asset");
            i.itemName = name; i.kind = kind; i.effect = effect; i.value = value; i.price = price;
            i.tint = Palette.Hex(hexTint); i.flavorText = flavor;
            EditorUtility.SetDirty(i);
            return i;
        }

        public static GameConfig BuildConfig()
        {
            var cfg = LoadOrCreate<GameConfig>($"{DataDir}/GameConfig.asset");
            // Économie (réécrite à chaque setup : l'ancienne était trop pauvre)
            cfg.startingBlood = 60;
            cfg.rewardRoundWin = 80;
            cfg.rewardRoundLoss = 50;
            cfg.lucidityPerBonusBlood = 2;
            cfg.bloodPerDamage = 0.6f;
            // Dépression : énergie croissante (1, 2, 3, 4, 5) et renforcements entre les manches
            cfg.enemyEnergyBase = 1;
            cfg.enemyEnergyPerRound = 1f;
            cfg.enemyMaxEnergy = 5;
            cfg.enemyMaxCardsPerTurn = 3;
            cfg.enemyPerksPerRound = 1;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssetIfDirty(cfg);
            return cfg;
        }

        public static CardDatabase BuildDatabase()
        {
            s_arcana = 0;
            s_paths.Clear();
            EnsureFolder($"{DataDir}/Moves");
            const CardType A = CardType.Attaque, D = CardType.Defense, T = CardType.Tourment, K = CardType.Crise;
            const CardRarity S = CardRarity.Depart, Co = CardRarity.Commune, U = CardRarity.PeuCommune, R = CardRarity.Rare, Cr = CardRarity.Crise;

            // ---------------------------------------------------------- Départ
            var seLever = new C("P_SeLever", "Se lever", A, S, CardTheme.Resilience, 1, "Le plus dur, c'est de poser un pied par terre.").Dmg(6, 3).d;
            var respirer = new C("P_Respirer", "Respirer", D, S, CardTheme.Acceptation, 1, "Inspire. Expire. Encore une fois.").Blk(5, 3).d;
            var crier = new C("P_Crier", "Crier", A, S, CardTheme.Resilience, 2, "Tout ce qui ne sortait pas.").Dmg(8, 2).Vuln(2).Magic(1).d;

            // ---------------------------------------------------------- Boutique : attaques
            var marcher = new C("S_MarcherDehors", "Marcher dehors", A, Co, CardTheme.Espoir, 1, "Le froid sur les joues, ça réveille.").Dmg(8, 2).Draw(1).d;
            var poings = new C("S_PoingsSerres", "Poings serrés", A, Co, CardTheme.Culpabilite, 1, "Serrer jusqu'à ne plus trembler.").Dmg(3, 1, 3).d;
            var nommer = new C("S_NommerDouleur", "Nommer la douleur", A, Co, CardTheme.Acceptation, 1, "Ce qui a un nom perd un peu de son pouvoir.").Dmg(6, 2).Weak(2).Magic(1).d;
            var colere = new C("S_ColereSourde", "Colère sourde", A, U, CardTheme.Culpabilite, 1, "Elle gronde sous la peau, faute de mieux.").Special(CardSpecial.DegatsSelonBouclier).UpCost(-1).d;
            var rage = new C("S_RageDeVivre", "Rage de vivre", A, U, CardTheme.Resilience, 2, "Pas comme ça. Pas maintenant.").Dmg(14, 4).Special(CardSpecial.BonusSiLuciditeBasse, 8).d;
            var cri = new C("S_CriLiberateur", "Cri libérateur", A, U, CardTheme.Resilience, 0, "Et après, le silence est plus léger.").Dmg(9, 3).Exhaust().d;
            var aube = new C("S_Aube", "L'aube", A, R, CardTheme.Espoir, 2, "La nuit finit toujours par finir.").Dmg(24, 8).Exhaust().d;

            // ---------------------------------------------------------- Boutique : défenses
            var therapie = new C("S_Therapie", "Thérapie", D, Co, CardTheme.Soutien, 2, "Une heure par semaine pour démêler les nœuds.").Blk(12, 4).Draw(1).d;
            var tenir = new C("S_TenirBon", "Tenir bon", D, Co, CardTheme.Resilience, 1, "Encore une minute. Puis une autre.").Blk(7, 2).Special(CardSpecial.BonusSiLuciditeBasse, 5).d;
            var ancrage = new C("S_Ancrage", "Ancrage", D, Co, CardTheme.Acceptation, 1, "Cinq choses que tu vois. Quatre que tu touches...").Blk(6, 2).Weak(1).Magic(1).d;
            var couverture = new C("S_Couverture", "Sous la couverture", D, Co, CardTheme.VideSolitude, 0, "Juste cinq minutes de plus.").Blk(4, 3).d;
            var traitement = new C("S_Traitement", "Traitement", D, U, CardTheme.Soutien, 2, "Une béquille n'est pas une faiblesse.").Blk(18, 6).Exhaust().d;
            var reconstruire = new C("S_Reconstruire", "Reconstruire", D, U, CardTheme.Espoir, 1, "Brique par brique.").Blk(7, 3).Blood(5).d;
            var accepter = new C("S_Accepter", "Accepter", D, R, CardTheme.Acceptation, 1, "Ce qui est là peut être accueilli.").Special(CardSpecial.DoublerBouclier).UpCost(-1).d;

            // ---------------------------------------------------------- Boutique : tourments
            var ecrire = new C("S_Ecrire", "Écrire", T, Co, CardTheme.Acceptation, 1, "Mettre des mots, c'est déjà les tenir à distance.").Draw(2, 1).d;
            var appeler = new C("S_AppelerAmie", "Appeler une amie", T, Co, CardTheme.Soutien, 1, "« Tu peux venir ? » — « J'arrive. »").Heal(6).Draw(1).Exhaust().Magic(3).d;
            var lucidite = new C("S_Lucidite", "Lucidité", T, U, CardTheme.Acceptation, 0, "Voir les choses telles qu'elles sont.").Vuln(2).Weak(1).Exhaust(true).d;
            var souvenir = new C("S_SouvenirEte", "Souvenir d'été", T, U, CardTheme.Espoir, 1, "Le goût du sel, le rire de quelqu'un.").Str(2).Exhaust().Magic(1).d;
            var cafe = new C("S_SecondSouffle", "Second souffle", T, U, CardTheme.Resilience, 1, "Encore un peu d'élan.").Energy(2).Exhaust(true).d;
            var pardonner = new C("S_SePardonner", "Se pardonner", T, R, CardTheme.Culpabilite, 0, "Tu as fait de ton mieux avec ce que tu avais.").Special(CardSpecial.PurgerCrisesEnMain).Draw(0, 1).d;

            // ---------------------------------------------------------- Crises et leurs transformations
            var doute = new C("C_Doute", "Doute", K, Cr, CardTheme.Anxiete, 0, "Et si tu n'y arrivais jamais ?").Unplayable().Ethereal().d;
            var rechute = new C("C_Rechute", "Rechute", K, Cr, CardTheme.Depression, 0, "Tu croyais que c'était fini.").Unplayable().Held(3).d;
            var nuitBlanche = new C("C_NuitBlanche", "Nuit blanche", K, Cr, CardTheme.Insomnie, 1, "Les yeux qui brûlent, la tête qui tourne.").Exhaust().d;
            var honte = new C("C_Honte", "Honte", K, Cr, CardTheme.Culpabilite, 0, "Tu baisses les yeux, encore.").Unplayable().DrawPain(1).d;
            foreach (var c in new[] { doute, rechute, nuitBlanche, honte }) c.inShop = false;

            var questionner = new C("T_Questionner", "Questionner ses pensées", T, U, CardTheme.Acceptation, 0, "Une pensée n'est pas un fait.").Draw(2, 1).Exhaust().NotInShop().d;
            var lecon = new C("T_LeconRechute", "Leçon de la rechute", D, U, CardTheme.Resilience, 1, "Tomber n'efface pas le chemin parcouru.").Blk(8, 3).Str(1).NotInShop().d;
            var sommeil = new C("T_SommeilRetrouve", "Sommeil retrouvé", T, U, CardTheme.Espoir, 1, "Une nuit entière. Enfin.").Heal(8).Energy(1).Exhaust().Magic(3).NotInShop().d;
            var pardon = new C("T_Pardon", "Pardon", A, U, CardTheme.Acceptation, 1, "Se parler comme on parlerait à une amie.").Dmg(9, 3).Heal(3).NotInShop().d;
            doute.transformsInto = questionner; rechute.transformsInto = lecon; nuitBlanche.transformsInto = sommeil; honte.transformsInto = pardon;

            // ---------------------------------------------------------- Actions de la Dépression
            var mIdees = Move("M_IdeesNoires", "Idées noires", "XIII", CardTheme.Depression, "Elles reviennent toujours à la même heure.", 1, 1.4f); mIdees.damage = 9;
            var mSpirale = Move("M_Spirale", "Spirale", "X", CardTheme.Anxiete, "Chaque pensée en entraîne une pire.", 1, 1.1f); mSpirale.damage = 4; mSpirale.hits = 2;
            var mPoids = Move("M_PoidsDuMatin", "Poids du matin", "IV", CardTheme.Depression, "La couette pèse une tonne.", 1, 1f); mPoids.damage = 5; mPoids.block = 7;
            var mIntrusives = Move("M_PenseesIntrusives", "Pensées intrusives", "XVIII", CardTheme.PenseesIntrusives, "« Et si... ? » « Et si... ? »", 1, 0.9f); mIntrusives.damage = 4; mIntrusives.crisisCount = 2; mIntrusives.specificCrisis = doute;
            var mInsomnie = Move("M_Insomnie", "Insomnie", "XVII", CardTheme.Insomnie, "3h47. Le plafond n'a pas changé.", 1, 0.8f); mInsomnie.applyWeak = 2; mInsomnie.crisisCount = 1; mInsomnie.specificCrisis = nuitBlanche;
            var mMurmure = Move("M_Murmure", "Murmure", "XV", CardTheme.Anxiete, "Une voix basse qui sait exactement où appuyer.", 1, 0.8f); mMurmure.damage = 3; mMurmure.applyVulnerable = 2;
            var mVide = Move("M_LeVide", "Le vide", "0", CardTheme.VideSolitude, "Rien ne fait mal. Rien ne fait rien.", 1, 0.7f); mVide.block = 12;
            var mCulpa = Move("M_Culpabilite", "Culpabilité", "VII", CardTheme.Culpabilite, "« Tout est de ta faute. »", 1, 0.7f); mCulpa.strengthGain = 2;
            var mRechute = Move("M_Rechute", "Rechute", "XVI", CardTheme.Depression, "Tu croyais que c'était fini.", 2, 0.8f); mRechute.damage = 7; mRechute.crisisCount = 1; mRechute.crisisToDrawPile = true; mRechute.specificCrisis = rechute;
            var mHonte = Move("M_Honte", "Regard des autres", "XII", CardTheme.Culpabilite, "Tu baisses les yeux, encore.", 2, 0.7f); mHonte.damage = 6; mHonte.crisisCount = 1; mHonte.specificCrisis = honte;
            var mGouffre = Move("M_Gouffre", "Le gouffre", "XX", CardTheme.Desespoir, "Au bord, ça tire vers le bas.", 2, 0.6f); mGouffre.damage = 17;
            var mCicatrices = Move("M_Cicatrices", "Cicatrices", "IX", CardTheme.Traumatismes, "Ce qui ne se voit plus fait encore mal.", 3, 0.7f); mCicatrices.damage = 6; mCicatrices.hits = 3;
            var mDesespoir = Move("M_Desespoir", "Désespoir", "XXI", CardTheme.Desespoir, "« À quoi bon ? »", 3, 0.6f); mDesespoir.strengthGain = 3; mDesespoir.block = 10;
            // Ses cartes les plus lourdes coûtent 2 énergies
            mRechute.cost = 2; mGouffre.cost = 2; mCicatrices.cost = 2; mDesespoir.cost = 2;

            // Cartes qu'elle n'obtient qu'en se renforçant entre les manches ("sa boutique")
            var mAngoisse = Move("M_CriseAngoisse", "Crise d'angoisse", "XIV", CardTheme.Anxiete, "Le cœur cogne. L'air ne passe plus.", 2, 0.9f);
            mAngoisse.damage = 4; mAngoisse.hits = 3; mAngoisse.cost = 2; mAngoisse.shopOnly = true;
            var mEffondrement = Move("M_Effondrement", "Effondrement", "XIX", CardTheme.Desespoir, "Tout lâche en même temps.", 3, 0.7f);
            mEffondrement.damage = 22; mEffondrement.cost = 3; mEffondrement.shopOnly = true;
            var mEmprise = Move("M_EmpriseTotale", "Emprise totale", "XI", CardTheme.PenseesIntrusives, "Il n'y a plus qu'elle dans ta tête.", 2, 0.8f);
            mEmprise.applyWeak = 2; mEmprise.applyVulnerable = 2; mEmprise.crisisCount = 2; mEmprise.cost = 1; mEmprise.shopOnly = true;
            var mIsolement = Move("M_Isolement", "Isolement", "II", CardTheme.VideSolitude, "Ton téléphone ne sonne plus. Tu ne l'allumes plus.", 2, 0.8f);
            mIsolement.block = 14; mIsolement.strengthGain = 1; mIsolement.cost = 1; mIsolement.shopOnly = true;

            // ---------------------------------------------------------- Objets
            var items = new List<ItemData>
            {
                Item("R_PhotoFamille", "Photo de famille", ItemKind.Relique, ItemEffect.BouclierDebutManche, 8, 140, "C9BFAE", "Des sourires d'avant. Ils existent encore."),
                Item("R_BraceletHopital", "Bracelet d'hôpital", ItemKind.Relique, ItemEffect.LuciditeMax, 10, 130, "8E8A86", "Tu es déjà passée par là. Tu en es sortie."),
                Item("R_CarnetIntime", "Carnet intime", ItemKind.Relique, ItemEffect.PiocheDebutManche, 2, 140, "5B3A1E", "Chaque page lue à voix basse te rend plus lucide."),
                Item("R_Pendentif", "Pendentif de grand-mère", ItemKind.Relique, ItemEffect.ForceDebutManche, 1, 150, "D9A441", "« Tu es plus forte que tu ne le crois. »"),
                Item("R_Peluche", "Chat en peluche", ItemKind.Relique, ItemEffect.EnergiePremierTour, 1, 150, "4A4644", "Un œil en moins, mais toujours là."),
                Item("R_Tirelire", "Tirelire fêlée", ItemKind.Relique, ItemEffect.GouttesBonus, 15, 110, "6E0B0E", "Garder un peu pour plus tard."),
                Item("R_TicketBus", "Ticket de bus usé", ItemKind.Relique, ItemEffect.RemiseBoutique, 20, 120, "3A3E4A", "Pour partir, n'importe où, un jour."),
                Item("I_Tisane", "Tisane", ItemKind.Consommable, ItemEffect.GagnerBouclier, 12, 50, "5A4130", "Chaude entre les mains."),
                Item("I_CafeFroid", "Café froid", ItemKind.Consommable, ItemEffect.GagnerEnergie, 2, 55, "3B2A1E", "Amer, mais ça réveille."),
                Item("I_LettreAmie", "Lettre d'une amie", ItemKind.Consommable, ItemEffect.Soigner, 15, 55, "C9BFAE", "« Je pense fort à toi. »"),
                Item("I_Somnifere", "Somnifère", ItemKind.Consommable, ItemEffect.Piocher, 3, 45, "8E8A86", "Oublier, recommencer."),
                Item("I_LunettesFelees", "Lunettes fêlées", ItemKind.Consommable, ItemEffect.AppliquerVulnerable, 3, 45, "4C5A66", "Tout devient net, d'un coup."),
                Item("I_MiroirFele", "Miroir fêlé", ItemKind.Consommable, ItemEffect.AppliquerFaiblesse, 3, 45, "A3161A", "Ce qu'elle montre n'est pas ce qu'elle est."),
                Item("I_Musique", "Écouteurs", ItemKind.Consommable, ItemEffect.GagnerForce, 2, 60, "1E2233", "Le volume au maximum."),
                Item("I_RendezVousPsy", "Rendez-vous chez la psy", ItemKind.Consommable, ItemEffect.PurgerDeck, 0, 80, "D9A441", "On range, ensemble, ce qui encombre. (Utilisable en boutique.)")
            };

            var db = LoadOrCreate<CardDatabase>($"{DataDir}/CardDatabase.asset");
            db.playerStarterDeck = new List<CardData> { seLever, seLever, seLever, seLever, seLever, respirer, respirer, respirer, respirer, crier };
            db.shopPool = new List<CardData> { marcher, poings, nommer, colere, rage, cri, aube, therapie, tenir, ancrage, couverture, traitement, reconstruire, accepter, ecrire, appeler, lucidite, souvenir, cafe, pardonner };
            db.enemyMoves = new List<EnemyMoveData> { mIdees, mSpirale, mPoids, mIntrusives, mInsomnie, mMurmure, mVide, mCulpa, mRechute, mHonte, mGouffre, mCicatrices, mDesespoir, mAngoisse, mEffondrement, mEmprise, mIsolement };
            db.crisisCards = new List<CardData> { doute, rechute, nuitBlanche, honte };
            db.items = items;
            EditorUtility.SetDirty(db);

            // Écrit immédiatement chaque asset sur le disque (sinon une étape ultérieure du setup peut recharger
            // la version vide du fichier et faire perdre les valeurs : cartes "Nouvelle carte").
            foreach (var p in s_paths)
            {
                var a = AssetDatabase.LoadAssetAtPath<Object>(p);
                if (a) { EditorUtility.SetDirty(a); AssetDatabase.SaveAssetIfDirty(a); }
            }
            foreach (var m in db.enemyMoves) if (m) AssetDatabase.SaveAssetIfDirty(m);
            AssetDatabase.SaveAssetIfDirty(db);

            // Retire les anciennes cartes / objets qui ne font plus partie du jeu
            foreach (var dir in new[] { CardDir, ItemDir })
                foreach (var guid in AssetDatabase.FindAssets("", new[] { dir }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".asset") && !s_paths.Contains(p)) AssetDatabase.DeleteAsset(p);
                }

            // Vérification
            var check = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDir}/P_SeLever.asset");
            if (check && check.damage == 0) Debug.LogError("[Lucidité] Les cartes n'ont pas été enregistrées correctement (dégâts à 0). Relance le setup.");
            foreach (var m in db.enemyMoves) EditorUtility.SetDirty(m);
            return db;
        }
    }
}
