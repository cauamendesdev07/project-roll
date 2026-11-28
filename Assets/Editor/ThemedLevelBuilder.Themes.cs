using UnityEngine;

// Os 10 temas. Cada paleta tem 16 papéis com o mesmo significado em todos os temas:
// GrassTop (chão/topo), GrassLight, PineDark, PineMid, LeafLight, Bark, WoodRing, Rock, RockMoss (topo da rocha),
// Dirt (faixa de terra/areia de praia), RiverBed (fundo da água), MushCap (destaque 1), Cream, FlowerYellow, FlowerWhite, Fern.
public static partial class ThemedLevelBuilder
{
    enum WaterMode { None, Water, Ice, Lava, Clouds }
    enum ParticleMode { Pollen, Dust, Fireflies, Snow, Embers, Spores, Sparkles, Sand }

    class Theme
    {
        public string key, title;
        public string[] atlas;
        public Color skyZenith, skyHorizon, cloudCol = Color.white; public float cloudAmount = 0.6f; public bool stars;
        public Color sunCol; public float sunElev = 40f, sunYaw = 300f, sunIntensity = 1.25f;
        public Color ambSky, ambEq, ambGround;
        public Color fogCol; public float fogStart = 30f, fogEnd = 210f;
        public TerrainMode terrain = TerrainMode.Gorge; public float bankHeight = 1.2f;
        public WaterMode water = WaterMode.Water; public Color waterCol; public float waterAlpha = 0.8f; public float waterY = -12.4f;
        public bool waterfall, lightShafts, mist, fallingLeaves, glowMushrooms, floatingOnly, lightning;
        public Color mistCol = Color.white; public float mistAlpha = 0.18f;
        public bool woodStumps = true;
        public ParticleMode particles = ParticleMode.Pollen; public Color particleA, particleB; public float particleRate = 26f;
        public Color accent, glow;
        public float exposure = 0.25f, contrast = 10f, saturation = 12f, temperature = 7f, tint = -3f, bloom = 0.4f, vignette = 0.24f;
        public Color vignetteCol = new Color(0.086f, 0.141f, 0.11f);
        public string[] trees; public float[] treeWeights; public float treeDensity = 0.6f; public string gorgeTree = "pine";
        public (string kind, float p)[] decor;
        public string[] topDecor = { "tuft", "flower", "mushroom", "rock" };
        public string plateauTree = "pine", plateauTree2 = "broadleaf";
        public Color hazardCol; public bool hazardIsCloud;
        public R tuftBase = R.GrassTop, tuftTip = R.GrassLight, bushAlt = R.GrassTop, canopyAlt = R.GrassTop;
        public bool caveLights, neutralTonemap;
    }

    static Color C(string h) => Hex(h);

    static Theme ThemeFor(int level)
    {
        switch (level)
        {
            case 1: return new Theme
            {
                key = "Amazonia", title = "Amazônia",
                atlas = new[] { "#3E7A30", "#5E9A3A", "#1C442A", "#2C6434", "#5C9C36", "#5A4430", "#B08A5A", "#6E6A5A", "#4A6E38", "#6A4A2E", "#3A2C1E", "#D8402C", "#E8DCC0", "#F2C030", "#F2EEE0", "#2A5A2A" },
                skyZenith = C("#5C8CB4"), skyHorizon = C("#CBD8CC"), cloudCol = C("#F2F0E6"), cloudAmount = 0.75f,
                sunCol = C("#FFE6B8"), sunElev = 42, sunYaw = 300, sunIntensity = 1.2f,
                ambSky = C("#A2BFCC"), ambEq = C("#7E9C78"), ambGround = C("#3A3224"),
                fogCol = C("#B6CABC"), fogStart = 24, fogEnd = 170,
                waterCol = C("#4A3A24"), waterAlpha = 0.9f, lightShafts = true, mist = true, mistCol = C("#D8E4D8"), mistAlpha = 0.26f, fallingLeaves = true,
                particles = ParticleMode.Pollen, particleA = C("#FFF4D0"), particleB = C("#E6F2C0"), particleRate = 22f,
                accent = C("#F2B134"), glow = C("#F2B134"),
                saturation = 18f, contrast = 12f, temperature = 5f,
                vignetteCol = C("#0A1A10"),
                trees = new[] { "kapok", "acai", "broadleaf" }, treeWeights = new[] { 0.3f, 0.4f, 0.3f }, treeDensity = 0.85f, gorgeTree = "acai",
                decor = new[] { ("tuft", 0.28f), ("bush", 0.08f), ("heliconia", 0.05f), ("bromeliad", 0.03f), ("rock", 0.012f), ("mushroom", 0.01f) },
                topDecor = new[] { "tuft", "heliconia", "bromeliad" },
                plateauTree = "kapok", plateauTree2 = "acai",
            };
            case 2: return new Theme
            {
                key = "Caatinga", title = "Caatinga",
                atlas = new[] { "#C2A15E", "#DCC285", "#3B6A47", "#5B8A55", "#8E9A55", "#8A7A68", "#C8A27A", "#B8693E", "#C9955E", "#A2532E", "#B39573", "#E86B9A", "#EFE3C8", "#F2C14E", "#F4EEE0", "#7E7448" },
                skyZenith = C("#3C86D6"), skyHorizon = C("#EED6AE"), cloudCol = C("#FFF4E0"), cloudAmount = 0.35f,
                sunCol = C("#FFE0A8"), sunElev = 55, sunYaw = 300, sunIntensity = 1.45f,
                ambSky = C("#BFD3E6"), ambEq = C("#C9A877"), ambGround = C("#6B4A30"),
                fogCol = C("#EBD2A6"), fogStart = 40, fogEnd = 260,
                water = WaterMode.None, bankHeight = 1.6f,
                particles = ParticleMode.Dust, particleA = C("#F2DDB0"), particleB = C("#E8C48A"), particleRate = 18f,
                accent = C("#F2A23A"), glow = C("#F2A23A"),
                exposure = 0.1f, contrast = 14f, saturation = 24f, temperature = 6f, tint = 0f, vignetteCol = C("#2A1A10"), bushAlt = R.LeafLight,
                trees = new[] { "cactus", "drytree", "drybare" }, treeWeights = new[] { 0.45f, 0.3f, 0.25f }, treeDensity = 0.45f, gorgeTree = "drybare",
                decor = new[] { ("tuft", 0.22f), ("rock", 0.05f), ("smallcactus", 0.03f), ("bush", 0.02f) },
                topDecor = new[] { "tuft", "smallcactus", "rock" },
                plateauTree = "cactus", plateauTree2 = "drytree",
            };
            case 3: return new Theme
            {
                key = "Cerrado", title = "Cerrado",
                atlas = new[] { "#D9A626", "#EBC94E", "#3E5E32", "#557A3A", "#8DA246", "#5E4632", "#C29466", "#B5734A", "#A88F4A", "#A4502E", "#7A5A3A", "#D8574A", "#EBDDBE", "#F3B23E", "#F2EAD8", "#7D8A3A" },
                skyZenith = C("#5374B8"), skyHorizon = C("#F6B98A"), cloudCol = C("#FFD9B8"), cloudAmount = 0.7f,
                sunCol = C("#FFC27F"), sunElev = 27, sunYaw = 300, sunIntensity = 1.55f,
                ambSky = C("#A7B6D8"), ambEq = C("#C99A6A"), ambGround = C("#5A3A24"),
                fogCol = C("#F2C7A0"), fogStart = 55, fogEnd = 280,
                waterCol = C("#7A6A3A"), waterAlpha = 0.85f, lightShafts = true,
                particles = ParticleMode.Fireflies, particleA = C("#FFE7A0"), particleB = C("#FFC15E"), particleRate = 14f,
                accent = C("#FFB04A"), glow = C("#FFB04A"),
                exposure = 0.15f, contrast = 14f, saturation = 38f, temperature = 6f, tint = 2f, bloom = 0.5f, vignetteCol = C("#2A1408"),
                trees = new[] { "flattop", "termite", "palm", "drytree" }, treeWeights = new[] { 0.55f, 0.2f, 0.1f, 0.15f }, treeDensity = 0.42f, gorgeTree = "flattop",
                decor = new[] { ("tuft", 0.5f), ("flower", 0.02f), ("rock", 0.025f), ("termite", 0.01f) },
                topDecor = new[] { "tuft", "flower", "rock" },
                plateauTree = "flattop", plateauTree2 = "termite",
            };
            case 4: return new Theme
            {
                key = "Pantanal", title = "Pantanal",
                atlas = new[] { "#6E9E3E", "#98BD55", "#2F5A3A", "#4C7E3E", "#7DAA48", "#6B5440", "#C29A6A", "#7E806E", "#6E8A4A", "#6B5338", "#4E5A40", "#E07AA0", "#EFE6CE", "#F2C94C", "#F7F2E6", "#3E6E3A" },
                skyZenith = C("#5C9AD6"), skyHorizon = C("#DDE6DA"), cloudCol = C("#FFFFFF"), cloudAmount = 0.55f,
                sunCol = C("#FFE9C4"), sunElev = 25, sunYaw = 300, sunIntensity = 1.2f,
                ambSky = C("#B8CFE0"), ambEq = C("#9CB28E"), ambGround = C("#4A4232"),
                fogCol = C("#D6E2D8"), fogStart = 45, fogEnd = 250,
                terrain = TerrainMode.Wetland, waterCol = C("#335E4A"), waterAlpha = 0.86f,
                mist = true, mistCol = C("#EEF2E6"), mistAlpha = 0.12f, lightShafts = true,
                particles = ParticleMode.Pollen, particleA = C("#FFFBE6"), particleB = C("#E8F2C8"),
                accent = C("#F2B134"), glow = C("#F2B134"),
                saturation = 24f, contrast = 12f,
                trees = new[] { "palm", "broadleaf", "flattop" }, treeWeights = new[] { 0.45f, 0.35f, 0.2f }, treeDensity = 0.5f, gorgeTree = "palm",
                decor = new[] { ("tuft", 0.4f), ("reeds", 0.06f), ("flower", 0.03f), ("bush", 0.04f) },
                topDecor = new[] { "tuft", "flower", "reeds" },
                plateauTree = "palm", plateauTree2 = "broadleaf",
            };
            case 5: return new Theme
            {
                key = "Noronha", title = "Fernando de Noronha",
                atlas = new[] { "#7DAA4A", "#A6C760", "#2F5E38", "#4A7E40", "#86B24C", "#7A5E44", "#D2A878", "#4A4644", "#6E7A5A", "#E8C98A", "#5ED6CF", "#E8566A", "#F5EBD3", "#F7C94A", "#FFF6E8", "#4E7A3E" },
                skyZenith = C("#2A82E4"), skyHorizon = C("#C4E6F2"), cloudCol = C("#FFFFFF"), cloudAmount = 0.4f,
                sunCol = C("#FFF2D6"), sunElev = 50, sunYaw = 300, sunIntensity = 1.45f,
                ambSky = C("#BFE2F2"), ambEq = C("#A6D8CC"), ambGround = C("#6E6450"),
                fogCol = C("#C4E6F2"), fogStart = 110, fogEnd = 520,
                terrain = TerrainMode.Ocean, waterCol = C("#22C2C6"), waterAlpha = 0.62f,
                particles = ParticleMode.Sparkles, particleA = C("#FFFFFF"), particleB = C("#DFF6FF"), particleRate = 10f,
                accent = C("#FFB84A"), glow = C("#FFB84A"),
                exposure = 0.2f, contrast = 11f, saturation = 30f, temperature = 3f, tint = -2f, vignette = 0.16f, vignetteCol = C("#0A2430"),
                trees = new[] { "broadleaf", "drytree", "palm" }, treeWeights = new[] { 0.5f, 0.35f, 0.15f }, treeDensity = 0.4f, gorgeTree = "broadleaf",
                decor = new[] { ("tuft", 0.26f), ("bush", 0.06f), ("rock", 0.05f), ("flower", 0.02f) },
                topDecor = new[] { "tuft", "bush", "rock" },
                plateauTree = "broadleaf", plateauTree2 = "drytree",
            };
            case 6: return new Theme
            {
                key = "Lencois", title = "Lençóis Maranhenses",
                atlas = new[] { "#DCCFB4", "#EAE1CB", "#3E7A5A", "#5E9A6A", "#8DB46A", "#8A7A62", "#D8C29A", "#D2C0A0", "#E6DCC6", "#DCCDB0", "#2FB5C4", "#E8A23A", "#FFF8EA", "#F5D24A", "#FFFFFF", "#A9C08A" },
                skyZenith = C("#3C8EE6"), skyHorizon = C("#E3F1F8"), cloudCol = C("#FFFFFF"), cloudAmount = 0.45f,
                sunCol = C("#FFEFD6"), sunElev = 30, sunYaw = 300, sunIntensity = 1.2f,
                ambSky = C("#A9C6E0"), ambEq = C("#C8BEA8"), ambGround = C("#7E6E52"),
                fogCol = C("#E3F0F4"), fogStart = 60, fogEnd = 320,
                terrain = TerrainMode.Dunes, waterCol = C("#2BB8C8"), waterAlpha = 0.7f, woodStumps = false,
                particles = ParticleMode.Sand, particleA = C("#FFF8EA"), particleB = C("#F2E8D2"), particleRate = 20f,
                accent = C("#F2A23A"), glow = C("#F2A23A"),
                exposure = -0.25f, contrast = 16f, saturation = 26f, temperature = 5f, tint = 0f, vignette = 0.22f, vignetteCol = C("#203040"),
                tuftBase = R.Fern, tuftTip = R.LeafLight, bushAlt = R.PineMid, canopyAlt = R.PineMid,
                trees = new[] { "flattop" }, treeWeights = new[] { 1f }, treeDensity = 0.015f, gorgeTree = "",
                decor = new[] { ("tuft", 0.07f), ("bush", 0.008f) },
                topDecor = new[] { "tuft" },
                plateauTree = "flattop", plateauTree2 = "bush",
            };
            case 7: return new Theme
            {
                key = "Gruta", title = "Gruta de Cristais",
                atlas = new[] { "#3A5E5A", "#4E8278", "#2A3B44", "#3A4F5A", "#4A6A6A", "#4A3E3A", "#8A7058", "#5A5470", "#467068", "#3A3230", "#1E2A34", "#5CF2E6", "#B8C8D8", "#B78CF2", "#DDF6FF", "#25403E" },
                skyZenith = C("#05070C"), skyHorizon = C("#0E1622"), cloudAmount = 0f,
                sunCol = C("#8FB4E8"), sunElev = 70, sunYaw = 300, sunIntensity = 0.9f,
                ambSky = C("#5868A0"), ambEq = C("#40567A"), ambGround = C("#1E2630"),
                fogCol = C("#142838"), fogStart = 18, fogEnd = 120, caveLights = true,
                terrain = TerrainMode.Cave, waterCol = C("#163A48"), waterAlpha = 0.85f, woodStumps = false, glowMushrooms = true,
                particles = ParticleMode.Spores, particleA = C("#7CFFF2"), particleB = C("#C9A2FF"), particleRate = 18f,
                accent = C("#5CF2E6"), glow = C("#5CF2E6"),
                exposure = 0.8f, contrast = 12f, saturation = 20f, temperature = -6f, tint = 4f, bloom = 0.9f, vignette = 0.3f, vignetteCol = C("#000000"),
                trees = new[] { "stalagmite", "crystal" }, treeWeights = new[] { 0.6f, 0.4f }, treeDensity = 0.5f, gorgeTree = "stalagmite",
                decor = new[] { ("mushroom", 0.03f), ("crystalsmall", 0.025f), ("rock", 0.04f), ("tuft", 0.08f) },
                topDecor = new[] { "mushroom", "crystalsmall", "rock" },
                plateauTree = "crystal", plateauTree2 = "stalagmite",
            };
            case 8: return new Theme
            {
                key = "Neve", title = "Pico Nevado",
                atlas = new[] { "#EEF4FA", "#FFFFFF", "#244A44", "#2F5E52", "#5E8A7A", "#5A4A44", "#B89A7A", "#6E7A8A", "#E2EAF2", "#4E5664", "#9CC8E0", "#E05A4A", "#F2F0EA", "#F2C14E", "#FFFFFF", "#8A9AA8" },
                skyZenith = C("#7C98B8"), skyHorizon = C("#DCE6EE"), cloudCol = C("#F4F6FA"), cloudAmount = 0.9f,
                sunCol = C("#F2F4FF"), sunElev = 30, sunYaw = 300, sunIntensity = 1.05f,
                ambSky = C("#C8D6E6"), ambEq = C("#AAB8C8"), ambGround = C("#6A7280"),
                fogCol = C("#DCE4EC"), fogStart = 20, fogEnd = 170,
                water = WaterMode.Ice, waterCol = C("#BFE3F2"), woodStumps = false, bankHeight = 2.2f,
                particles = ParticleMode.Snow, particleA = C("#FFFFFF"), particleB = C("#E8F0FF"), particleRate = 60f,
                accent = C("#FFB46A"), glow = C("#FFB46A"),
                exposure = 0.1f, contrast = 8f, saturation = -4f, temperature = -10f, tint = 0f, vignette = 0.2f, vignetteCol = C("#203040"),
                tuftBase = R.Fern, tuftTip = R.Cream, bushAlt = R.PineMid, canopyAlt = R.PineMid,
                trees = new[] { "snowpine", "drybare" }, treeWeights = new[] { 0.85f, 0.15f }, treeDensity = 0.6f, gorgeTree = "snowpine",
                decor = new[] { ("rock", 0.04f), ("tuft", 0.05f) },
                topDecor = new[] { "rock" },
                plateauTree = "snowpine", plateauTree2 = "snowpine",
            };
            case 9: return new Theme
            {
                key = "Vulcao", title = "Vulcão",
                atlas = new[] { "#4A4440", "#625A54", "#2A2624", "#3A3430", "#5A4A3A", "#3A2C26", "#6A4A34", "#3A3634", "#7A4030", "#4A2A22", "#1A1414", "#FF7A2A", "#8A7A70", "#FFB02A", "#C8B8A8", "#3A2E2A" },
                skyZenith = C("#2A1A22"), skyHorizon = C("#C2552E"), cloudCol = C("#5A3A3A"), cloudAmount = 0.8f,
                sunCol = C("#FFA06A"), sunElev = 26, sunYaw = 300, sunIntensity = 1.6f,
                ambSky = C("#8A5048"), ambEq = C("#6A4034"), ambGround = C("#3A1E14"),
                fogCol = C("#7A4232"), fogStart = 45, fogEnd = 230,
                water = WaterMode.Lava, waterCol = C("#FF5A1A"), woodStumps = false,
                particles = ParticleMode.Embers, particleA = C("#FFB04A"), particleB = C("#FF5A1A"), particleRate = 30f,
                accent = C("#FF8A2A"), glow = C("#FF7A2A"),
                exposure = 0.2f, contrast = 18f, saturation = 45f, temperature = 4f, tint = 0f, bloom = 0.45f, neutralTonemap = true, vignette = 0.28f, vignetteCol = C("#100404"),
                trees = new[] { "drybare", "basalt" }, treeWeights = new[] { 0.5f, 0.5f }, treeDensity = 0.4f, gorgeTree = "basalt",
                decor = new[] { ("rock", 0.06f), ("basaltsmall", 0.02f) },
                topDecor = new[] { "rock" },
                plateauTree = "drybare", plateauTree2 = "basalt",
                hazardCol = C("#FF5414"), tuftBase = R.Fern, tuftTip = R.LeafLight,
            };
            default: return new Theme
            {
                key = "Ceu", title = "Acima das Nuvens",
                atlas = new[] { "#5E9A5A", "#82B86A", "#244A44", "#2F6250", "#6A9A5E", "#4E4040", "#A88A6A", "#5A5670", "#6A7A6A", "#5A4A48", "#3A3A55", "#B98CFF", "#D8D2E8", "#F2D25A", "#EAE6FF", "#3A5A4A" },
                skyZenith = C("#0B0E24"), skyHorizon = C("#3A3260"), cloudCol = C("#4A4478"), cloudAmount = 0.5f, stars = true,
                sunCol = C("#B8C6FF"), sunElev = 40, sunYaw = 300, sunIntensity = 1.15f,
                ambSky = C("#5A62A0"), ambEq = C("#40447A"), ambGround = C("#20203A"),
                fogCol = C("#2A2850"), fogStart = 40, fogEnd = 260,
                terrain = TerrainMode.None, water = WaterMode.Clouds, waterCol = C("#5A5490"), floatingOnly = true, woodStumps = false, lightning = true,
                particles = ParticleMode.Sparkles, particleA = C("#DCD6FF"), particleB = C("#B98CFF"), particleRate = 12f,
                accent = C("#B98CFF"), glow = C("#B98CFF"),
                exposure = 0.55f, contrast = 12f, saturation = 26f, temperature = -8f, tint = 6f, bloom = 0.7f, vignette = 0.28f, vignetteCol = C("#05040F"),
                trees = new[] { "pine", "broadleaf" }, treeWeights = new[] { 0.6f, 0.4f }, treeDensity = 0f, gorgeTree = "",
                decor = new (string, float)[0],
                topDecor = new[] { "tuft", "flower" },
                plateauTree = "pine", plateauTree2 = "broadleaf",
                hazardCol = C("#2A2448"), hazardIsCloud = true,
            };
        }
    }
}
