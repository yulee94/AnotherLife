namespace AnotherLife.Contracts

[<AutoOpen>]
module DesignContracts =

    [<CLIMutable>]
    type RealmCustomization =
        {
            id: string
            displayName: string
            materialKeys: string array
            customizationFocus: string array
        }

    [<CLIMutable>]
    type QualityTargets =
        {
            heroTriangles: string
            mediumTriangles: string
            lowTriangles: string
            farRepresentation: string
        }

    [<CLIMutable>]
    type CharacterCustomizationCatalog =
        {
            version: string
            game: string
            characterSlots: string array
            realms: RealmCustomization array
            qualityTargets: QualityTargets
        }

    [<CLIMutable>]
    type SkillEffect =
        {
            key: string
            realm: string
            ``use``: string
            colors: string array
        }

    [<CLIMutable>]
    type WeatherProfile =
        {
            key: string
            realm: string
            particles: string array
        }

    [<CLIMutable>]
    type SkillWeatherCatalog =
        {
            version: string
            skillEffects: SkillEffect array
            weatherProfiles: WeatherProfile array
        }

    [<CLIMutable>]
    type TroopInventoryData =
        {
            troopType: string
            count: int
            woundedCount: int
        }

    [<CLIMutable>]
    type ChampionCustomizationState =
        {
            bodyPresetId: string
            hairStyleId: string
            armorStyleId: string
            primaryR: float
            primaryG: float
            primaryB: float
            hairR: float
            hairG: float
            hairB: float
            capeEnabled: bool
            helmetEnabled: bool
        }

    [<CLIMutable>]
    type TerritorySnapshot =
        {
            id: string
            name: string
            ownerRealm: string
            bonusType: string
            bonusAmount: int64
            isFortress: bool
        }

    [<CLIMutable>]
    type WarmasterProgression =
        {
            equippedSetId: string option
            purchasedPieceIds: string array
            purchasedPieceCount: int
            requiredPieceCount: int
            isTrueWarmaster: bool
            level: int
            experience: int
        }

    [<CLIMutable>]
    type PrototypeProgressionSnapshot =
        {
            selectedRealm: string
            troops: TroopInventoryData array
            territories: TerritorySnapshot array
            warzoneCredits: int
            warmasterSetId: string option
            warmaster: WarmasterProgression
            championCustomization: ChampionCustomizationState
        }

    module CatalogPaths =
        [<Literal>]
        let CharacterCustomization = "Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json"

        [<Literal>]
        let SkillWeather = "Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json"
