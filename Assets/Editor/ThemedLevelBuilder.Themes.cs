using UnityEngine;

// Os 10 temas. Cada paleta tem 16 papéis com o mesmo significado em todos os temas:
// GrassTop (chão/topo), GrassLight, PineDark, PineMid, LeafLight, Bark, WoodRing, Rock, RockMoss (topo da rocha),
// Dirt (faixa de terra/areia de praia), RiverBed (fundo da água), MushCap (destaque 1), Cream, FlowerYellow, FlowerWhite, Fern.
public static partial class ThemedLevelBuilder
{
    enum WaterMode { None, Water, Ice, Lava, Clouds }
    enum ParticleMode { Pollen, Dust, Fireflies, Snow, Embers, Spores, Sparkles, Sand, Rain }

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
        public Color hazardCol; public bool hazardIsCloud, hazardWater;
        public R tuftBase = R.GrassTop, tuftTip = R.GrassLight, bushAlt = R.GrassTop, canopyAlt = R.GrassTop;
        public bool caveLights, neutralTonemap;
        public Color waterGlow = Color.black;   // emissão da água (lago luminoso)
        public bool limestone;                  // gruta de calcário: sem cristais, luz azul refletida do lago
        public bool cityLights;                 // janelas acesas que apagam com os relâmpagos
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
                key = "LagoAzul", title = "Gruta do Lago Azul",
                atlas = new[] { "#9C8462", "#B89C74", "#4E4434", "#64563F", "#806E50", "#5E4A36", "#B8986E", "#C8AC80", "#AE9168", "#74604A", "#0E3E8A", "#7CC8FF", "#EADCC0", "#DCC294", "#F4EBDA", "#6E6248" },
                skyZenith = C("#06080C"), skyHorizon = C("#101A26"), cloudAmount = 0f,
                sunCol = C("#FFF0D8"), sunElev = 72, sunYaw = 300, sunIntensity = 1.0f,
                ambSky = C("#7A7C88"), ambEq = C("#5E5A58"), ambGround = C("#2E2820"),
                fogCol = C("#10202E"), fogStart = 20, fogEnd = 130, caveLights = true, limestone = true,
                terrain = TerrainMode.Cave, waterCol = C("#1460E0"), waterAlpha = 0.82f, waterGlow = C("#0A3C9A"), woodStumps = false, lightShafts = true,
                particles = ParticleMode.Dust, particleA = C("#E6F0FF"), particleB = C("#BFD6F2"), particleRate = 14f,
                accent = C("#7CC8FF"), glow = C("#4AA0FF"),
                exposure = 0.75f, contrast = 12f, saturation = 26f, temperature = 3f, tint = 0f, bloom = 0.8f, vignette = 0.3f, vignetteCol = C("#000000"),
                trees = new[] { "stalagmite", "rockbig" }, treeWeights = new[] { 0.8f, 0.2f }, treeDensity = 0.5f, gorgeTree = "stalagmite",
                decor = new[] { ("rock", 0.05f), ("rockbig", 0.01f) },
                topDecor = new[] { "rock" },
                plateauTree = "stalagmite", plateauTree2 = "stalagmite",
            };
            case 8: return new Theme
            {
                key = "Urupema", title = "Urupema – Capital do Frio",
                atlas = new[] { "#B89A58", "#D6CDB0", "#22402E", "#2F5A3C", "#6E8A4E", "#5A4636", "#9C7A54", "#7C705E", "#9C9270", "#6A5A44", "#A8D0E4", "#C8463A", "#F2F0EA", "#E8C25A", "#FFFFFF", "#8A8A60" },
                skyZenith = C("#4C86C8"), skyHorizon = C("#DCE8F0"), cloudCol = C("#F4F6FA"), cloudAmount = 0.35f,
                sunCol = C("#FFF0DC"), sunElev = 18, sunYaw = 300, sunIntensity = 1.15f,
                ambSky = C("#C2D2E4"), ambEq = C("#B4B2A4"), ambGround = C("#5E5A4E"),
                fogCol = C("#DCE4EC"), fogStart = 25, fogEnd = 190,
                water = WaterMode.Ice, waterCol = C("#C8E6F2"), woodStumps = true, bankHeight = 1.8f,
                mist = true, mistCol = C("#EEF2F6"), mistAlpha = 0.22f,
                particles = ParticleMode.Snow, particleA = C("#FFFFFF"), particleB = C("#E8F0FF"), particleRate = 14f,
                accent = C("#FFB46A"), glow = C("#FFB46A"),
                exposure = 0.1f, contrast = 11f, saturation = 14f, temperature = -3f, tint = 0f, vignette = 0.2f, vignetteCol = C("#203040"),
                tuftBase = R.GrassTop, tuftTip = R.GrassLight, bushAlt = R.PineMid, canopyAlt = R.PineMid,
                trees = new[] { "araucaria", "bush" }, treeWeights = new[] { 0.8f, 0.2f }, treeDensity = 0.27f, gorgeTree = "araucaria",
                decor = new[] { ("tuft", 0.4f), ("rock", 0.03f), ("fence", 0.006f), ("bush", 0.02f) },
                topDecor = new[] { "tuft", "rock" },
                plateauTree = "araucaria", plateauTree2 = "araucaria",
            };
            case 9: return new Theme
            {
                key = "SaoPaulo", title = "Terra da Garoa",
                atlas = new[] { "#8C8A84", "#A4A29A", "#2E4A36", "#3E6040", "#5E7E44", "#3A3632", "#B07A5A", "#7C7C78", "#9A968C", "#C8B48E", "#4A5C70", "#C23A32", "#E2DCCC", "#E8B830", "#F2F0EA", "#5A6A4A" },
                skyZenith = C("#3E4652"), skyHorizon = C("#7A7C82"), cloudCol = C("#555A62"), cloudAmount = 0.95f,
                sunCol = C("#C8D0DC"), sunElev = 35, sunYaw = 300, sunIntensity = 0.85f,
                ambSky = C("#7E8896"), ambEq = C("#6A6C70"), ambGround = C("#34322E"),
                fogCol = C("#6A6E76"), fogStart = 18, fogEnd = 175,
                waterCol = C("#5A4E38"), waterAlpha = 0.9f, woodStumps = false, lightning = true, cityLights = true,
                particles = ParticleMode.Rain, particleA = C("#C8D0DC"), particleB = C("#E0E4EC"), particleRate = 420f,
                accent = C("#FFB04A"), glow = C("#FFB04A"),
                exposure = 0.3f, contrast = 12f, saturation = -10f, temperature = -2f, tint = 0f, bloom = 0.6f, vignette = 0.28f, vignetteCol = C("#101216"),
                trees = new[] { "building", "bush" }, treeWeights = new[] { 0.85f, 0.15f }, treeDensity = 0.5f, gorgeTree = "lamp",
                decor = new[] { ("tuft", 0.06f), ("bush", 0.02f), ("rock", 0.01f), ("lamp", 0.008f) },
                topDecor = new[] { "tuft", "rooftop" },
                plateauTree = "lamp", plateauTree2 = "bush",
                hazardCol = C("#6A5A40"), hazardWater = true, tuftBase = R.Fern, tuftTip = R.LeafLight,
            };
            default: return new Theme
            {
                key = "Rio", title = "Cidade Maravilhosa",
                atlas = new[] { "#5E9A4A", "#86B85E", "#2A5A3A", "#3C7A44", "#6CAA48", "#6A5040", "#C8925A", "#6E6A66", "#7E8A6A", "#EED6A0", "#3A6A9A", "#D8483A", "#EDE4D2", "#F2C24A", "#FFF8EE", "#4E8A4E" },
                skyZenith = C("#2A7AD8"), skyHorizon = C("#BFE2F6"), cloudCol = C("#FFFFFF"), cloudAmount = 0.2f,
                sunCol = C("#FFF2DC"), sunElev = 52, sunYaw = 300, sunIntensity = 1.45f,
                ambSky = C("#BFDDF2"), ambEq = C("#A8D0C0"), ambGround = C("#6E6450"),
                fogCol = C("#C4E2F4"), fogStart = 110, fogEnd = 640,
                terrain = TerrainMode.Ocean, waterCol = C("#2E8AA0"), waterAlpha = 0.75f, woodStumps = false,
                particles = ParticleMode.Sparkles, particleA = C("#FFFFFF"), particleB = C("#E8F6FF"), particleRate = 8f,
                accent = C("#FFB04A"), glow = C("#FFB04A"),
                exposure = 0.2f, contrast = 11f, saturation = 26f, temperature = 3f, tint = 0f, bloom = 0.5f, vignette = 0.22f, vignetteCol = C("#1A0E10"),
                trees = new[] { "palm", "broadleaf" }, treeWeights = new[] { 0.45f, 0.55f }, treeDensity = 0.45f, gorgeTree = "palm",
                decor = new[] { ("tuft", 0.25f), ("bush", 0.05f), ("house", 0.03f), ("rock", 0.02f), ("flower", 0.02f) },
                topDecor = new[] { "tuft", "flower" },
                plateauTree = "palm", plateauTree2 = "broadleaf",
                hazardCol = C("#2A7A8A"), hazardWater = true,
            };
        }
    }
}
