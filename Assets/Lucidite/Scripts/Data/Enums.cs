namespace Lucidite
{
    /// <summary>Familles de cartes (GDD) adaptées au deckbuilder à énergie.</summary>
    public enum CardType
    {
        Attaque,   // inflige des dégâts
        Defense,   // donne du bouclier
        Tourment,  // effets spéciaux (pioche, énergie, affaiblissements, force...)
        Crise      // cartes négatives glissées dans ton deck par la Dépression
    }

    public enum CardRarity { Depart, Commune, PeuCommune, Rare, Crise }

    /// <summary>
    /// Classes des cartes (comme les couleurs d'un jeu classique) : quatre parties du corps. Symbole dans le coin
    /// haut-gauche de la carte et motif de fond. Chaque classe a son niveau (pilules) et sa Résonance
    /// (3 cartes de la même classe jouées dans un tour). Les Crises n'en ont pas.
    /// </summary>
    public enum CardSuit { Aucune, Coeur, Souffle, Regard, Main }

    /// <summary>Thématiques de l'univers (section 8 du GDD).</summary>
    public enum CardTheme
    {
        Depression,
        Anxiete,
        Desespoir,
        VideSolitude,
        Culpabilite,
        Dissociation,
        PenseesIntrusives,
        Insomnie,
        Traumatismes,
        Soutien,
        Acceptation,
        Resilience,
        Espoir
    }

    /// <summary>Effets particuliers qui ne se résument pas à des nombres.</summary>
    public enum CardSpecial
    {
        None,
        DegatsSelonBouclier,   // inflige des dégâts égaux à ton bouclier
        BonusSiLuciditeBasse,  // +specialValue dégâts/bouclier si Lucidité <= 50 %
        PurgerCrisesEnMain,    // épuise les Crises de ta main et pioche autant
        DoublerBouclier,       // double ton bouclier actuel
        SaignementAdverseTour, // elle saigne deux fois plus ce tour
        SaignementReduitTour,  // tu saignes deux fois moins ce tour
        DoubleSiEcarlate,      // dégâts / bouclier doublés si la carte est Écarlate
        SoinSelonSang,         // récupère 1 Lucidité par tranche de 10 % de sang sur la carte (+ specialValue)
        PerdreLucidite,        // perds specialValue Lucidité
        BouclierParClasseEnMain, // +specialValue Bouclier par carte de la même classe gardée en main
        CoupsParClasse,        // +1 coup par carte de la même classe déjà jouée ce tour
        Joker                  // compte comme une carte de chaque classe pour la Résonance
    }

    public enum ItemKind
    {
        Relique,      // statuette posée sur la table : effet permanent
        Consommable   // pilule : utilisable pendant ton tour (ou en boutique pour certaines)
    }

    public enum ItemEffect
    {
        // Reliques
        LuciditeMax,          // +X Lucidité max
        BouclierDebutManche,  // commence chaque manche avec X bouclier
        PiocheDebutManche,    // pioche X cartes de plus au 1er tour
        ForceDebutManche,     // commence chaque manche avec X Force
        EnergiePremierTour,   // +X énergie au 1er tour de chaque manche
        GouttesBonus,         // +X gouttes à chaque fin de manche
        RemiseBoutique,       // les prix baissent de X %
        SaignementAdverse,    // l'adversaire saigne deux fois plus (projections, gouttes gagnées, sang sur les cartes)
        ResonanceFacile,      // la Résonance de la classe « value » se déclenche avec 2 cartes
        RelanceGratuite,      // X relance(s) gratuite(s) par boutique
        SangCartes,           // les cartes se couvrent de sang X % plus vite
        DegatsSiLuciditeBasse, // +X dégâts par coup quand ta Lucidité est à 50 % ou moins
        EnergieMax,           // +X énergie à chaque tour
        DegatsClasse,         // les cartes de la classe « value » infligent +3 dégâts par coup
        VoileAdverse,         // chaque manche, une relique adverse tirée au hasard est voilée (sans effet)
        Reliquaire,           // double l'effet d'une autre de tes reliques (choisie à l'achat)
        PremierCoupReduit,    // le premier coup reçu à chaque tour est réduit de X
        SoinDebutTour,        // récupère X Lucidité au début de chacun de tes tours
        Epines,               // chaque fois que tu perds de la Lucidité, ton adversaire en perd X
        // Consommables
        GagnerBouclier,
        GagnerEnergie,
        Soigner,
        Piocher,
        AppliquerVulnerable,
        AppliquerFaiblesse,
        GagnerForce,
        PurgerDeck,           // retire toutes les Crises du deck (utilisable en boutique)
        SaignementReduitTour, // tu saignes deux fois moins ce tour
        SaignementAdverseTour, // l'adversaire saigne deux fois plus ce tour
        NiveauClasse,         // +1 niveau à la classe « value » (pour toute la partie, utilisable en boutique)
        EnergieAdverseTour,   // l'adversaire perd X énergie ce tour (elle renonce à ses dernières cartes)
        ConvertirClasse,      // jusqu'à 2 cartes sélectionnées passent dans la classe « value »
        CopierCarte,          // deux cartes sélectionnées : celle de droite devient une copie de celle de gauche
        Voler,                // vole une relique de l'adversaire (ou une pilule s'il n'en a pas)
        Purifier,             // retire tes Faiblesse et Vulnérable
        Placebo               // l'effet d'une autre pilule, tirée au hasard
    }

    /// <summary>Petites icônes de bonus affichées en bas à gauche des cartes.</summary>
    public enum CardStat
    {
        Force, Faiblesse, Vulnerable, Pioche, Energie, Ponction, Soin, Sang, Bouclier, Degats,
        Epuiser, Ephemere, Crise, Retenue, LuciditeBasse, Purge
    }

    public enum Side { Player, Opponent }

    public enum IntentKind { Attaque, AttaqueDefense, Defense, Renfort, Malediction, AttaqueMalediction }
}
