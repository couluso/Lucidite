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
        DoublerBouclier        // double ton bouclier actuel
    }

    public enum ItemKind
    {
        Relique,      // effet permanent passif
        Consommable   // utilisable pendant ton tour (ou en boutique pour certains)
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
        // Consommables
        GagnerBouclier,
        GagnerEnergie,
        Soigner,
        Piocher,
        AppliquerVulnerable,
        AppliquerFaiblesse,
        GagnerForce,
        PurgerDeck            // retire toutes les Crises du deck (utilisable en boutique)
    }

    public enum Side { Player, Opponent }

    public enum IntentKind { Attaque, AttaqueDefense, Defense, Renfort, Malediction, AttaqueMalediction }
}
