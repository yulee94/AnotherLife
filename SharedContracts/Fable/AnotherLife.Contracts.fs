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

    module CatalogPaths =
        [<Literal>]
        let CharacterCustomization = "Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json"

        [<Literal>]
        let SkillWeather = "Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json"

