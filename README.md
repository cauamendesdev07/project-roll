# project-roll
Jogo de plataforma 3D desenvolvido na Unity.

## Build

Editor: Unity **6000.6.9f1** com os módulos *Linux Build Support* e *Windows Build Support (Mono)*.

### Local

Os perfis ficam em `Assets/Settings/Build Profiles/` (Windows e Linux). Em **File > Build Profiles**, escolha o perfil e clique em **Build**. A lista de cenas é a global (MainMenu e Level_01 a Level_10).

### GitHub Actions

O workflow `.github/workflows/build.yml` usa a [GameCI](https://game.ci) para gerar os builds de Windows e Linux:

- roda manualmente (aba **Actions > Build > Run workflow**), em push na `main` que altere o jogo e em tags `v*`;
- os builds ficam como *artifacts* da execução por 14 dias;
- tags `v*` (ex.: `v1.0.0`) também criam uma Release com os zips.

É preciso cadastrar em **Settings > Secrets and variables > Actions**:

| Secret | Conteúdo |
|---|---|
| `UNITY_EMAIL` | e-mail da conta Unity |
| `UNITY_PASSWORD` | senha da conta Unity |
| `UNITY_LICENSE` | conteúdo do arquivo de licença `.ulf` (licença Personal) |

Com licença Pro, use `UNITY_SERIAL` no lugar de `UNITY_LICENSE`. Veja como obter o `.ulf` na [documentação da GameCI](https://game.ci/docs/github/activation).

## Sons

Todos os sons (efeitos, ambiente de cada bioma e música) são gerados por síntese, sem arquivos de terceiros nem licenças. O gerador fica em `Assets/Editor/AudioSynth.*.cs`; para refazer os WAVs, use o menu **Project Roll > Gerar sons** no editor. Os arquivos gerados ficam em `Assets/Resources/Audio` e são versionados, então o build não depende de rodar o gerador. Para trocar um som por outro, basta substituir o WAV mantendo o nome.
