# project-roll
Jogo de plataforma 3D desenvolvido na Unity.

## Build

Editor: Unity **6000.6.9f1** com os módulos *Linux Build Support* e *Windows Build Support (Mono)*.

Os perfis ficam em `Assets/Settings/Build Profiles/` (Windows e Linux). Em **File > Build Profiles**, escolha o perfil e clique em **Build**. A lista de cenas é a global (MainMenu e Level_01 a Level_10).

## Sons

Todos os sons (efeitos, ambiente de cada bioma e música) são gerados por síntese, sem arquivos de terceiros nem licenças. O gerador fica em `Assets/Editor/AudioSynth.*.cs`; para refazer os WAVs, use o menu **Project Roll > Gerar sons** no editor. Os arquivos gerados ficam em `Assets/Resources/Audio` e são versionados, então o build não depende de rodar o gerador. Para trocar um som por outro, basta substituir o WAV mantendo o nome.
