using System.Collections.Generic;
using System.Linq;
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
                d.icon = CardFaceFactory.SaveIcon(id); // grande icône unique, dessinée par carte
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
            m.icon = CardFaceFactory.SaveIcon(id);
            m.damage = 0; m.hits = 1; m.block = 0; m.strengthGain = 0; m.applyWeak = 0; m.applyVulnerable = 0;
            m.crisisCount = 0; m.crisisToDrawPile = false; m.specificCrisis = null;
            m.minRound = minRound; m.weight = weight;
            m.cost = 1; m.shopOnly = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static ItemData Item(string id, string name, ItemKind kind, ItemEffect effect, int value, int price, string hexTint, string flavor, bool her = false, bool rare = false)
        {
            var i = LoadOrCreate<ItemData>($"{ItemDir}/{id}.asset");
            s_paths.Add($"{ItemDir}/{id}.asset");
            i.itemName = name; i.kind = kind; i.effect = effect; i.value = value; i.price = price;
            i.tint = Palette.Hex(hexTint); i.flavorText = flavor;
            i.enemyCanBuy = her;
            i.rare = rare;
            i.model = kind == ItemKind.Relique ? RelicFactory.Build(id) : null; // chaque relique a sa statuette
            EditorUtility.SetDirty(i);
            return i;
        }

        public static GameConfig BuildConfig()
        {
            var cfg = LoadOrCreate<GameConfig>($"{DataDir}/GameConfig.asset");
            // Économie (réécrite à chaque setup : l'ancienne était trop pauvre)
            // Main : les cartes non jouées restent ; la pioche normale s'arrête à 7, les pioches bonus vont au-delà (12 max)
            cfg.drawPerTurn = 5;
            cfg.maxHandSize = 7;
            cfg.absoluteMaxHandSize = 12;
            cfg.startingBlood = 60;
            // Gouttes : une base (faible en cas de défaite) + un bonus selon le sang que tu as fait couler chez la Dépression
            cfg.rewardRoundWin = 60;
            cfg.rewardRoundLoss = 25;
            cfg.lucidityPerBonusBlood = 0;
            cfg.bloodPerDamage = 1f;
            // Boutique : services répétables (+25 à chaque usage, remis à zéro à la boutique suivante), relance 10 puis +5
            cfg.serviceCostIncrease = 25;
            cfg.rerollCost = 10; cfg.rerollCostIncrease = 5;
            cfg.pillOffers = 3; cfg.relicOffers = 2;
            cfg.relicSlots = 5; cfg.itemSlots = 3;
            // Classes : chaque niveau ajoute +2 aux dégâts / bouclier / soin des cartes de la classe
            cfg.suitLevelBonus = 2;
            // Dépression : énergie 1 puis 2 (3 si elle l'achète), progression douce entre les manches
            // (équilibrage simulé, main conservée entre les tours : ~20 % de victoires pour une joueuse IA naïve ; à 40 %, une vraie joueuse gagnait 3-0)
            cfg.enemyBaseHp = 52;
            cfg.enemyHpPerRound = 9;
            cfg.enemyDamageScalePerRound = 0.09f;
            cfg.enemyEnergyBase = 1;
            cfg.enemyEnergyPerRound = 1f;
            cfg.enemyMaxEnergy = 2;
            cfg.enemyMaxCardsPerTurn = 3;
            // Sa boutique : mêmes gains de gouttes que la joueuse, achats secrets
            cfg.enemyStartingBlood = 60;
            cfg.enemyBloodScale = 1f;
            cfg.enemyPriceHp = 50; cfg.enemyPerkHp = 8;
            cfg.enemyPriceStrength = 80;
            cfg.enemyPriceBlock = 50; cfg.enemyPerkBlock = 6;
            cfg.enemyPriceSharpen = 55; cfg.enemyPerkSharpen = 0.25f;
            cfg.enemyPriceNewMove = 70;
            cfg.enemyPriceEnergy = 150; cfg.enemyMaxBonusEnergy = 1;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssetIfDirty(cfg);
            return cfg;
        }

        /// <summary>Faces des cartes : icône et bonus cuits dans la texture (après que tous les effets sont définis).</summary>
        static void BuildFaces(CardDatabase db)
        {
            var stats = new List<CardStat>();
            var up = new List<CardStat>();
            foreach (var p in s_paths)
            {
                var d = AssetDatabase.LoadAssetAtPath<CardData>(p);
                if (!d) continue;
                var c = new CardInstance(d);
                CardFace.Stats(c, stats);
                d.face = CardFaceFactory.SaveFace(d.name, d.name, Palette.ForType(d.type), false, stats);
                d.faceUpgraded = null;
                if (d.evolvesInto == null && c.CanUpgrade)
                {
                    c.upgraded = true;
                    CardFace.Stats(c, up);
                    if (!stats.SequenceEqual(up)) d.faceUpgraded = CardFaceFactory.SaveFace(d.name + "_plus", d.name, Palette.ForType(d.type), false, up);
                }
                EditorUtility.SetDirty(d);
            }
            foreach (var m in db.enemyMoves)
            {
                var it = new EnemyIntent { move = m, damagePerHit = m.damage, hits = m.hits, block = m.block, strength = m.strengthGain,
                    weak = m.applyWeak, vulnerable = m.applyVulnerable, crisis = m.crisisCount };
                CardFace.Stats(it, stats);
                m.face = CardFaceFactory.SaveFace(m.name, m.name, Palette.TypeEnemy, true, stats);
                EditorUtility.SetDirty(m);
            }
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

            // ---------------------------------------------------------- Classes et saignement
            var battements = new C("S_Battements", "Battements", A, Co, CardTheme.Anxiete, 1, "Boum. Boum. Plus vite à chaque carte de sa classe.").Dmg(4, 1).Special(CardSpecial.CoupsParClasse).d;
            var souffleCourt = new C("S_SouffleCourt", "Souffle court", D, Co, CardTheme.Anxiete, 0, "Petites goulées, mais de l'air quand même.").Blk(4, 2).Draw(1).d;
            var regardNoir = new C("S_RegardNoir", "Regard noir", A, Co, CardTheme.Culpabilite, 1, "Pour une fois, c'est elle qui baisse les yeux.").Dmg(5, 2).Vuln(1).Magic(1).d;
            var gorge = new C("S_GorgeNouee", "Gorge nouée", T, Co, CardTheme.Anxiete, 1, "Les mots restent coincés. Les siens aussi.").Weak(2).Magic(1).Draw(1).d;
            var mainTendue = new C("S_MainTendue", "Main tendue", D, U, CardTheme.Soutien, 1, "Quelqu'un est là, même sans rien dire.").Blk(5, 2).Special(CardSpecial.BouclierParClasseEnMain, 3).Magic(1).d;
            var apnee = new C("S_Apnee", "Apnée", D, U, CardTheme.VideSolitude, 2, "Sous l'eau, plus rien ne crie.").Blk(11, 3).Draw(2).d;
            var hemorragie = new C("S_Hemorragie", "Hémorragie", A, U, CardTheme.Traumatismes, 2, "Ça ne s'arrête plus de couler. Chez elle, pour une fois.").Dmg(12, 4).Special(CardSpecial.SaignementAdverseTour).d;
            var garrot = new C("S_Garrot", "Garrot", D, Co, CardTheme.Resilience, 1, "Serrer fort, le temps que ça passe.").Blk(6, 3).Special(CardSpecial.SaignementReduitTour).d;
            var transfusion = new C("S_Transfusion", "Transfusion", T, U, CardTheme.Soutien, 1, "Le sang des autres, pour continuer.").Special(CardSpecial.SoinSelonSang, 2).Magic(2).Exhaust(true).d;
            var palpitations = new C("S_Palpitations", "Palpitations", T, Co, CardTheme.Anxiete, 0, "Ça cogne trop fort, mais ça pousse en avant.").Special(CardSpecial.PerdreLucidite, 3).Energy(1).Draw(1, 1).d;
            var saignerBlanc = new C("S_SaignerABlanc", "Saigner à blanc", A, R, CardTheme.Desespoir, 1, "Quand tout est rouge, frapper deux fois plus fort.").Dmg(9, 3).Special(CardSpecial.DoubleSiEcarlate).d;
            var echo = new C("S_Echo", "Écho", T, R, CardTheme.Espoir, 1, "Elle résonne avec tout ce que tu es.").Special(CardSpecial.Joker).Draw(1, 1).d;

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

            // ---------------------------------------------------------- Reliques (statuettes posées sur la table) et pilules
            // her = la Dépression peut l'acheter aussi (posée de son côté de la table)
            var items = new List<ItemData>
            {
                Item("R_Pleureuse", "La Pleureuse", ItemKind.Relique, ItemEffect.SaignementAdverse, 2, 125, "A9A397", "Elle pleure pour toi, à genoux, des larmes rouges. Ton adversaire saigne deux fois plus.", true),
                Item("R_ClocheCoeur", "Cloche au cœur", ItemKind.Relique, ItemEffect.ResonanceFacile, (int)CardSuit.Coeur, 90, "8E2A26", "Sous le verre, il bat encore. Tout ce qu'on entend, c'est lui."),
                Item("R_ClochePoumons", "Cloche aux poumons", ItemKind.Relique, ItemEffect.ResonanceFacile, (int)CardSuit.Souffle, 90, "A8646A", "L'air sous la cloche est toujours le même. On le respire quand même."),
                Item("R_ClocheOeil", "Cloche à l'œil", ItemKind.Relique, ItemEffect.ResonanceFacile, (int)CardSuit.Regard, 90, "D9D3C6", "Il te regarde à travers le verre. Il ne cligne jamais."),
                Item("R_ClocheMain", "Cloche à la main", ItemKind.Relique, ItemEffect.ResonanceFacile, (int)CardSuit.Main, 90, "A9A397", "Une main tendue, mise sous verre. Personne ne la prend."),
                Item("R_ExVotoCoeur", "Ex-voto au cœur", ItemKind.Relique, ItemEffect.DegatsClasse, (int)CardSuit.Coeur, 95, "8E2A26", "Un cœur de cire cloué au mur d'une chapelle. Merci pour rien."),
                Item("R_ExVotoPoumons", "Ex-voto aux poumons", ItemKind.Relique, ItemEffect.DegatsClasse, (int)CardSuit.Souffle, 95, "8E8C86", "Pour un souffle qui revient. Il revient en cri."),
                Item("R_ExVotoOeil", "Ex-voto à l'œil", ItemKind.Relique, ItemEffect.DegatsClasse, (int)CardSuit.Regard, 95, "8E8C86", "Pour voir clair à nouveau, et viser juste."),
                Item("R_ExVotoMain", "Ex-voto à la main", ItemKind.Relique, ItemEffect.DegatsClasse, (int)CardSuit.Main, 95, "8E8C86", "Pour des mains qui ne tremblent plus quand elles frappent."),
                Item("R_VisageRecolle", "Visage recollé", ItemKind.Relique, ItemEffect.LuciditeMax, 10, 105, "D9D3C6", "Brisé, puis réparé à l'or. Les fêlures se voient ; c'est le but.", true),
                Item("R_Couverture", "Sous la couverture", ItemKind.Relique, ItemEffect.BouclierDebutManche, 8, 105, "4E5566", "Ne plus sortir de là. Au moins, là-dessous, rien n'atteint.", true),
                Item("R_Atlas", "Atlas fatigué", ItemKind.Relique, ItemEffect.ForceDebutManche, 1, 110, "5E5032", "Il porte tout, tout le temps. Ça finit par faire des muscles.", true),
                Item("R_Lacrymatoire", "Lacrymatoire", ItemKind.Relique, ItemEffect.GouttesBonus, 15, 80, "8A1A1A", "Une fiole pour garder ses larmes. La tienne déborde.", true),
                Item("R_Penseur", "Le Penseur voûté", ItemKind.Relique, ItemEffect.PiocheDebutManche, 2, 105, "5E5032", "La tête entre les mains, il ressasse. Parfois, il en sort une idée."),
                Item("R_Danseuse", "Danseuse brisée", ItemKind.Relique, ItemEffect.EnergiePremierTour, 1, 110, "D9D3C6", "Un bras en moins, elle tourne encore quand on remonte la boîte."),
                Item("R_Sablier", "Sablier presque vide", ItemKind.Relique, ItemEffect.RelanceGratuite, 1, 75, "8A3A22", "Le temps se retourne, une fois. Une relance gratuite par boutique."),
                Item("R_Corbeau", "Le Corbeau", ItemKind.Relique, ItemEffect.RemiseBoutique, 20, 90, "1A1717", "Perché sur les livres que tu ne lis plus. « Jamais plus », mais moins cher."),
                Item("R_CoeurPerce", "Cœur percé", ItemKind.Relique, ItemEffect.SangCartes, 50, 90, "8E2A26", "Transpercé, il goutte sans arrêt. Tes cartes absorbent plus de sang."),
                Item("R_Vanite", "Vanité", ItemKind.Relique, ItemEffect.DegatsSiLuciditeBasse, 3, 110, "C2B79C", "Memento mori. Plus tu vacilles, plus tu frappes fort.", true),
                Item("R_Metronome", "Métronome", ItemKind.Relique, ItemEffect.EnergieMax, 1, 150, "3A2618", "Il impose le tempo. Même lent, c'est un tempo.", true),
                Item("R_VoileNoir", "Voile de deuil", ItemKind.Relique, ItemEffect.VoileAdverse, 0, 110, "231E26", "Couvrir ce qu'on ne veut plus voir. Chez l'autre, ça marche aussi.", true),
                Item("R_Reliquaire", "Reliquaire", ItemKind.Relique, ItemEffect.Reliquaire, 0, 120, "A8873E", "On y garde ce qui compte. Ce qu'on y met compte deux fois plus.", true),
                Item("R_BrasCroises", "Bras croisés", ItemKind.Relique, ItemEffect.PremierCoupReduit, 4, 95, "A9A397", "Elle se serre fort dans ses propres bras. Le premier coup glisse.", true),
                Item("R_Veilleuse", "Veilleuse", ItemKind.Relique, ItemEffect.SoinDebutTour, 2, 100, "D9CFA0", "Une petite lune allumée toute la nuit. Pas grand-chose, mais elle reste.", true),
                Item("R_RoseFanee", "Rose fanée", ItemKind.Relique, ItemEffect.Epines, 2, 100, "5C1A20", "Les pétales sont tombés. Les épines, elles, sont restées.", true),

                Item("I_Hemostatique", "Hémostatique", ItemKind.Consommable, ItemEffect.SaignementReduitTour, 0, 40, "C9BFAE", "Tu saignes deux fois moins pendant ce tour.", true),
                Item("I_Anticoagulant", "Anticoagulant", ItemKind.Consommable, ItemEffect.SaignementAdverseTour, 0, 45, "6E0B0E", "Ton adversaire saigne deux fois plus pendant ce tour.", true),
                Item("I_Cardiotonique", "Cardiotonique", ItemKind.Consommable, ItemEffect.NiveauClasse, (int)CardSuit.Coeur, 60, "8E2A26", "Le cœur bat plus fort. +1 niveau à tes cartes Cœur. (Utilisable en boutique.)"),
                Item("I_Bronchodilatateur", "Bronchodilatateur", ItemKind.Consommable, ItemEffect.NiveauClasse, (int)CardSuit.Souffle, 60, "6A7A9A", "L'air passe enfin. +1 niveau à tes cartes Souffle. (Utilisable en boutique.)"),
                Item("I_Mydriatique", "Mydriatique", ItemKind.Consommable, ItemEffect.NiveauClasse, (int)CardSuit.Regard, 60, "1E2233", "Les pupilles s'ouvrent. +1 niveau à tes cartes Regard. (Utilisable en boutique.)"),
                Item("I_Myorelaxant", "Myorelaxant", ItemKind.Consommable, ItemEffect.NiveauClasse, (int)CardSuit.Main, 60, "C2B79C", "Les doigts se desserrent. +1 niveau à tes cartes Main. (Utilisable en boutique.)"),
                Item("I_Excitant", "Excitant", ItemKind.Consommable, ItemEffect.GagnerEnergie, 2, 55, "D9A441", "Le cœur s'emballe, les mains aussi.", true),
                Item("I_Tranquillisant", "Tranquillisant", ItemKind.Consommable, ItemEffect.EnergieAdverseTour, 1, 55, "8FA7B0", "Glissé dans son verre. Elle ralentit, pour ce tour.", true),
                Item("I_Digitaline", "Digitaline", ItemKind.Consommable, ItemEffect.ConvertirClasse, (int)CardSuit.Coeur, 50, "7A2A5A", "Extrait de digitale : tout se met à battre au même rythme."),
                Item("I_Ether", "Éther", ItemKind.Consommable, ItemEffect.ConvertirClasse, (int)CardSuit.Souffle, 50, "C8D4D8", "Une bouffée, et tout devient souffle."),
                Item("I_Belladone", "Belladone", ItemKind.Consommable, ItemEffect.ConvertirClasse, (int)CardSuit.Regard, 50, "3A2A44", "Les pupilles s'élargissent ; tout devient regard."),
                Item("I_Curare", "Curare", ItemKind.Consommable, ItemEffect.ConvertirClasse, (int)CardSuit.Main, 50, "4A3A20", "Paralyse tout, sauf les mains."),
                Item("I_DejaVu", "Déjà-vu", ItemKind.Consommable, ItemEffect.CopierCarte, 0, 60, "B8A0C8", "Tu as déjà vécu ce moment. Exactement celui-là.", true),
                Item("I_Calmant", "Calmant", ItemKind.Consommable, ItemEffect.Purifier, 0, 35, "A7B8A0", "Respire. Ça passe.", true),
                Item("I_Placebo", "Placebo", ItemKind.Consommable, ItemEffect.Placebo, 0, 30, "E8E4DA", "Du sucre. Mais si tu y crois...", true),
                Item("I_Antalgique", "Antalgique", ItemKind.Consommable, ItemEffect.Soigner, 15, 50, "D9D3C6", "La douleur recule d'un pas.", true),
                Item("I_Betabloquant", "Bêtabloquant", ItemKind.Consommable, ItemEffect.GagnerBouclier, 12, 45, "4C5A66", "Le calme, chimiquement.", true),
                Item("I_Stimulant", "Stimulant", ItemKind.Consommable, ItemEffect.GagnerForce, 2, 55, "A3161A", "Tout paraît possible, pour un temps.", true),
                Item("I_Somnifere", "Somnifère", ItemKind.Consommable, ItemEffect.Piocher, 3, 45, "8E8A86", "Oublier, recommencer."),
                Item("I_Neuroleptique", "Neuroleptique", ItemKind.Consommable, ItemEffect.AppliquerFaiblesse, 3, 45, "5A4130", "Les voix baissent d'un ton. Les siennes aussi.", true),
                Item("I_Desinhibant", "Désinhibant", ItemKind.Consommable, ItemEffect.AppliquerVulnerable, 3, 45, "4B3550", "Dire enfin ce qu'on pense, sans détour.", true),
                Item("I_Antidepresseur", "Antidépresseur", ItemKind.Consommable, ItemEffect.PurgerDeck, 0, 80, "D9A441", "Lentement, ce qui encombre se range. (Utilisable en boutique.)"),
                Item("I_Kleptomanie", "Kleptomanie", ItemKind.Consommable, ItemEffect.Voler, 0, 120, "C9A227", "Ce n'est pas du vol si on en a vraiment besoin. (Rare.)", true, true)
            };

            var db = LoadOrCreate<CardDatabase>($"{DataDir}/CardDatabase.asset");
            // Départ : un « Se lever » et un « Respirer » de chaque classe, plus deux « Crier » d'une même classe tirée au hasard
            db.playerStarterDeck = new List<CardData> { seLever, seLever, seLever, seLever, respirer, respirer, respirer, respirer, crier, crier };
            db.starterPerSuit = new List<CardData> { seLever, respirer };
            db.starterRandomSuit = new List<CardData> { crier, crier };
            db.shopPool = new List<CardData> { marcher, poings, nommer, colere, rage, cri, aube, therapie, tenir, ancrage, couverture, traitement, reconstruire, accepter, ecrire, appeler, lucidite, souvenir, cafe, pardonner,
                battements, souffleCourt, regardNoir, gorge, mainTendue, apnee, hemorragie, garrot, transfusion, palpitations, saignerBlanc, echo };
            db.enemyMoves = new List<EnemyMoveData> { mIdees, mSpirale, mPoids, mIntrusives, mInsomnie, mMurmure, mVide, mCulpa, mRechute, mHonte, mGouffre, mCicatrices, mDesespoir, mAngoisse, mEffondrement, mEmprise, mIsolement };
            db.crisisCards = new List<CardData> { doute, rechute, nuitBlanche, honte };
            db.items = items;
            BuildFaces(db);
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

            // Anciennes statuettes qui ne correspondent plus à aucune relique
            var relicPrefabs = new HashSet<string>();
            foreach (var i in items) if (i && i.model) relicPrefabs.Add(AssetDatabase.GetAssetPath(i.model));
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { AssetFactory.PrefabDir + "/Relics" }))
            {
                var rp = AssetDatabase.GUIDToAssetPath(guid);
                if (!relicPrefabs.Contains(rp)) AssetDatabase.DeleteAsset(rp);
            }

            // Vérification
            var check = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDir}/P_SeLever.asset");
            if (check && check.damage == 0) Debug.LogError("[Lucidité] Les cartes n'ont pas été enregistrées correctement (dégâts à 0). Relance le setup.");
            foreach (var m in db.enemyMoves) EditorUtility.SetDirty(m);
            return db;
        }
    }
}
