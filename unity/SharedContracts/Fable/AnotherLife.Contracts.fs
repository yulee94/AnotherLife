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
    type BodyPreset =
        {
            id: string
            displayName: string
            scale: float array
        }

    [<CLIMutable>]
    type StyleOption =
        {
            id: string
            displayName: string
        }

    [<CLIMutable>]
    type ColorOption =
        {
            id: string
            displayName: string
            rgb: float array
        }

    [<CLIMutable>]
    type ChampionForgePreset =
        {
            id: string
            displayName: string
            summary: string
            bodyPresetId: string
            hairStyleId: string
            armorStyleId: string
            faceMarkId: string
            weaponStyleId: string
            offhandStyleId: string
            primaryColor: float array
            hairColor: float array
            skinColor: float array
            eyeColor: float array
            accentColor: float array
            capeEnabled: bool
            helmetEnabled: bool
        }

    [<CLIMutable>]
    type CharacterCustomizationCatalog =
        {
            version: string
            game: string
            characterSlots: string array
            bodyPresets: BodyPreset array
            hairStyles: StyleOption array
            armorStyles: StyleOption array
            primaryColors: ColorOption array
            hairColors: ColorOption array
            skinColors: ColorOption array
            eyeColors: ColorOption array
            accentColors: ColorOption array
            faceMarks: StyleOption array
            weaponStyles: StyleOption array
            offhandStyles: StyleOption array
            forgePresets: ChampionForgePreset array
            realms: RealmCustomization array
            qualityTargets: QualityTargets
        }

    [<CLIMutable>]
    type SkillLoadout =
        {
            slot: int
            id: string
            displayName: string
            role: string
            vfxKey: string
            cooldownSeconds: float
            manaCost: float
            castTimeSeconds: float
            rangeMeters: float
            power: float
            botDamageMultiplier: float
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
    type WindProfile =
        {
            yawDegrees: float
            main: float
            turbulence: float
            pulseAmplitude: float
            pulseFrequency: float
        }

    [<CLIMutable>]
    type LightingProfile =
        {
            applyFog: bool
            fogColor: float array
            fogDensity: float
            ambientColor: float array
            directionalLightColor: float array
            directionalLightIntensity: float
        }

    [<CLIMutable>]
    type LightningProfile =
        {
            enabled: bool
            color: float array
            flashIntensity: float
            duration: float
            minDelay: float
            maxDelay: float
        }

    [<CLIMutable>]
    type WeatherProfile =
        {
            key: string
            realm: string
            displayName: string
            particles: string array
            color: float array
            particleStartColor: float array
            particleEndColor: float array
            maxParticles: int
            radius: float
            fallSpeed: float
            particleSize: float
            particleLifetime: float
            emissionRateMultiplier: float
            horizontalDrift: float
            noiseStrength: float
            noiseFrequency: float
            wind: WindProfile
            lighting: LightingProfile
            lightning: LightningProfile
        }

    [<CLIMutable>]
    type SkillWeatherCatalog =
        {
            version: string
            skillLoadouts: SkillLoadout array
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
            faceMarkId: string
            weaponStyleId: string
            offhandStyleId: string
            primaryR: float
            primaryG: float
            primaryB: float
            hairR: float
            hairG: float
            hairB: float
            skinR: float
            skinG: float
            skinB: float
            eyeR: float
            eyeG: float
            eyeB: float
            accentR: float
            accentG: float
            accentB: float
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
